using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Billing;
using CarePath.Application.Billing.Validators;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Common.Mapping;
using CarePath.Application.Common.Paging;
using CarePath.Contracts.Billing;
using CarePath.Contracts.Common;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentValidation;
using FluentValidation.Results;

namespace CarePath.Application.Billing.Services;

public sealed class BillingOperationsService : IBillingOperationsService
{
    private const string DuplicateInvoiceCode = "invoice.duplicate";
    private const string EmptyInvoiceCode = "invoice.no_billable_shifts";
    private const string PreviewStaleCode = "invoice.preview_stale";
    private const string InvoicePeriodIndexName = "IX_Invoices_Client_Service_Period";
    private const string ShiftLineUniqueIndexName = "UX_InvoiceLineItems_ShiftId_NotNull";

    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IClientAccessEvaluator clientAccessEvaluator;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IShiftBillingQuery shiftBillingQuery;
    private readonly IBillingEligibilityQuery eligibilityQuery;
    private readonly IInvoicePreviewTokenService previewTokens;
    private readonly IPersistenceConflictDetector persistenceConflictDetector;
    private readonly IValidator<CreateInvoiceRequest> createInvoiceValidator;
    private readonly IValidator<InvoicePreviewRequest> previewValidator;
    private readonly IValidator<RecordPaymentRequest> recordPaymentValidator;

    public BillingOperationsService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IClientAccessEvaluator clientAccessEvaluator,
        IPhiAuditLogger auditLogger,
        IBillingEligibilityQuery eligibilityQuery,
        IInvoicePreviewTokenService previewTokens,
        IShiftBillingQuery? shiftBillingQuery = null,
        IPersistenceConflictDetector? persistenceConflictDetector = null,
        IValidator<CreateInvoiceRequest>? createInvoiceValidator = null,
        IValidator<InvoicePreviewRequest>? previewValidator = null,
        IValidator<RecordPaymentRequest>? recordPaymentValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.clientAccessEvaluator = clientAccessEvaluator;
        this.auditLogger = auditLogger;
        this.eligibilityQuery = eligibilityQuery;
        this.previewTokens = previewTokens;
        this.shiftBillingQuery = shiftBillingQuery ?? new NoOpShiftBillingQuery();
        this.persistenceConflictDetector = persistenceConflictDetector ?? new NoOpPersistenceConflictDetector();
        this.createInvoiceValidator = createInvoiceValidator ?? new CreateInvoiceRequestValidator();
        this.previewValidator = previewValidator ?? new InvoicePreviewRequestValidator();
        this.recordPaymentValidator = recordPaymentValidator ?? new RecordPaymentRequestValidator();
    }

    public async Task<InvoicePreviewResponseDto> PreviewInvoiceAsync(
        InvoicePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await previewValidator.ValidateAndThrowAsync(request, cancellationToken);
        _ = await GetClientAsync(request.ClientId, cancellationToken);

        var serviceType = (ServiceType)(int)request.ServiceType;
        var rows = await eligibilityQuery.GetPeriodRowsAsync(
            request.ClientId,
            serviceType,
            request.PeriodStartUtc,
            request.PeriodEndUtc,
            cancellationToken);

        var eligible = rows.Where(row => row.Reason == BillingExclusionReason.Eligible).ToArray();
        var exclusionCounts = rows
            .Where(row => row.Reason != BillingExclusionReason.Eligible)
            .GroupBy(row => row.Reason)
            .OrderBy(group => group.Key)
            .Select(group => new InvoiceExclusionCountDto
            {
                Reason = (CarePath.Contracts.Enumerations.BillingExclusionReason)(int)group.Key,
                Count = group.Count(),
            })
            .ToArray();

        var subtotal = eligible.Sum(row => BillingMath.LineTotal(row) ?? 0m);
        var totalHours = eligible.Sum(row => BillingMath.BillableHours(row) ?? 0m);
        var pagedEligible = eligible
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArray();

        // Audit the shifts whose data is returned on this page (codebase-standard bound);
        // the full-set scan feeds only aggregate counts and the fingerprint.
        foreach (var row in pagedEligible)
        {
            await AuditAsync(ProtectedResourceType.Shift, row.ShiftId, AuditAction.Read, cancellationToken);
        }

        var pageRows = pagedEligible
            .Select(row => row.ToPreviewRowDto())
            .ToArray();

        var previewToken = string.Empty;
        var expiresAtUtc = default(DateTime);
        if (eligible.Length > 0)
        {
            var fingerprint = BuildFingerprint(request.ClientId, (int)serviceType, request.PeriodStartUtc, request.PeriodEndUtc, eligible, subtotal, totalHours);
            previewToken = previewTokens.Protect(fingerprint, out expiresAtUtc);
        }

        return new InvoicePreviewResponseDto
        {
            Rows = pageRows,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            EligibleShiftCount = eligible.Length,
            TotalBillableHours = totalHours,
            Subtotal = subtotal,
            ExclusionCounts = exclusionCounts,
            PreviewToken = previewToken,
            PreviewTokenExpiresAtUtc = expiresAtUtc,
        };
    }

    public async Task<InvoiceDetailDto> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await createInvoiceValidator.ValidateAndThrowAsync(request, cancellationToken);

        var fingerprint = previewTokens.Unprotect(request.PreviewToken)
            ?? throw PreviewStaleConflict();
        var serviceType = (ServiceType)(int)request.ServiceType;
        if (fingerprint.ClientId != request.ClientId
            || fingerprint.ServiceType != (int)serviceType
            || fingerprint.PeriodStartUtc != request.PeriodStartUtc
            || fingerprint.PeriodEndUtc != request.PeriodEndUtc)
        {
            throw PreviewStaleConflict();
        }

        var client = await GetClientAsync(request.ClientId, cancellationToken);
        var duplicateExists = await unitOfWork.Invoices.ExistsAsync(
            invoice => invoice.ClientId == client.Id
                && invoice.ServiceType == serviceType
                && invoice.PeriodStartUtc == request.PeriodStartUtc
                && invoice.PeriodEndUtc == request.PeriodEndUtc,
            cancellationToken);
        if (duplicateExists)
        {
            throw DuplicateInvoiceConflict();
        }

        var invoice = new Invoice
        {
            ClientId = client.Id,
            Client = client,
            InvoiceDate = request.PeriodEndUtc,
            DueDate = request.DueDate,
            Status = InvoiceStatus.Sent,
            ServiceType = serviceType,
            PeriodStartUtc = request.PeriodStartUtc,
            PeriodEndUtc = request.PeriodEndUtc,
            TaxAmount = request.TaxAmount,
            Notes = request.Notes,
        };
        invoice.InvoiceNumber = CreateInvoiceNumber(invoice);

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
                    // D-S6-18: creation re-evaluates eligibility INSIDE the transaction through
                    // the same shared query the preview used; any drift stales the preview.
                    var rows = await eligibilityQuery.GetPeriodRowsAsync(
                        client.Id,
                        serviceType,
                        request.PeriodStartUtc,
                        request.PeriodEndUtc,
                        token);
                    var eligible = rows
                        .Where(row => row.Reason == BillingExclusionReason.Eligible)
                        .ToArray();
                    if (eligible.Length == 0)
                    {
                        throw ValidationFailure("Period", "No completed billable shifts exist for the requested billing period.", EmptyInvoiceCode);
                    }

                    // Fail-closed duplicate preflight inside the new invoice itself.
                    if (eligible.Select(row => row.ShiftId).Distinct().Count() != eligible.Length)
                    {
                        throw PreviewStaleConflict();
                    }

                    var subtotal = eligible.Sum(row => BillingMath.LineTotal(row) ?? 0m);
                    var totalHours = eligible.Sum(row => BillingMath.BillableHours(row) ?? 0m);
                    var current = BuildFingerprint(client.Id, (int)serviceType, request.PeriodStartUtc, request.PeriodEndUtc, eligible, subtotal, totalHours);
                    if (!FingerprintMatches(fingerprint, current))
                    {
                        throw PreviewStaleConflict();
                    }

                    foreach (var row in eligible)
                    {
                        await AuditAsync(ProtectedResourceType.Shift, row.ShiftId, AuditAction.Read, token);
                        invoice.LineItems.Add(new InvoiceLineItem
                        {
                            InvoiceId = invoice.Id,
                            Invoice = invoice,
                            ShiftId = row.ShiftId,
                            Description = CreateLineDescription(serviceType),
                            ServiceDate = row.ScheduledStartUtc.Date,
                            BillableHours = BillingMath.BillableHours(row) ?? 0m,
                            RatePerHour = row.BillRate,
                            CostPerHour = row.PayRate,
                        });
                    }

                    await unitOfWork.Invoices.AddAsync(invoice, token);
                    foreach (var lineItem in invoice.LineItems)
                    {
                        await unitOfWork.InvoiceLineItems.AddAsync(lineItem, token);
                    }

                    await unitOfWork.SaveChangesAsync(token);
                    await AuditAsync(ProtectedResourceType.Invoice, invoice.Id, AuditAction.Create, token);
                },
                cancellationToken);
        }
        catch (Exception exception) when (persistenceConflictDetector.IsUniqueConstraintConflict(exception, InvoicePeriodIndexName))
        {
            throw DuplicateInvoiceConflict();
        }
        catch (Exception exception) when (persistenceConflictDetector.IsUniqueConstraintConflict(exception, ShiftLineUniqueIndexName))
        {
            // A concurrent create billed one of these shifts first — refresh and re-preview.
            throw PreviewStaleConflict();
        }

        await AttachClientUserAsync(invoice, cancellationToken);
        return invoice.ToDetailDto();
    }

    private static InvoicePreviewFingerprint BuildFingerprint(
        Guid clientId,
        int serviceType,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        IReadOnlyList<BillingEligibilityRow> eligibleRows,
        decimal subtotal,
        decimal totalHours)
    {
        return new InvoicePreviewFingerprint(
            clientId,
            serviceType,
            periodStartUtc,
            periodEndUtc,
            eligibleRows.Select(row => row.ShiftId).OrderBy(id => id).ToArray(),
            BillingMath.ComputeInputsHash(eligibleRows),
            subtotal,
            totalHours);
    }

    private static bool FingerprintMatches(InvoicePreviewFingerprint issued, InvoicePreviewFingerprint current)
    {
        return issued.ClientId == current.ClientId
            && issued.ServiceType == current.ServiceType
            && issued.PeriodStartUtc == current.PeriodStartUtc
            && issued.PeriodEndUtc == current.PeriodEndUtc
            && issued.EligibleShiftIds.SequenceEqual(current.EligibleShiftIds)
            && string.Equals(issued.InputsHash, current.InputsHash, StringComparison.Ordinal)
            && issued.Subtotal == current.Subtotal
            && issued.TotalBillableHours == current.TotalBillableHours;
    }

    private static ResourceConflictException PreviewStaleConflict()
    {
        return new ResourceConflictException(PreviewStaleCode, "The preview is no longer current. Refresh the preview and try again.");
    }

    public async Task<InvoiceDetailDto> RecordPaymentAsync(Guid invoiceId, RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await recordPaymentValidator.ValidateAndThrowAsync(request, cancellationToken);

        var invoice = await GetInvoiceEntityAsync(invoiceId, cancellationToken);
        await LoadInvoiceChildrenAsync(invoice, cancellationToken);

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Invoice = invoice,
            PaymentDate = request.PaymentDate,
            Amount = request.Amount,
            Method = (PaymentMethod)(int)request.Method,
            ReferenceNumber = request.ReferenceNumber,
            Notes = request.Notes,
            Status = PaymentStatus.Settled,
        };
        invoice.Payments.Add(payment);
        invoice.RecalculateStatus();
        if (invoice.Status == InvoiceStatus.Paid && invoice.PaidDate is null)
        {
            invoice.PaidDate = request.PaymentDate;
        }

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                await unitOfWork.Payments.AddAsync(payment, token);
                await unitOfWork.Invoices.UpdateAsync(invoice, token);
                await unitOfWork.SaveChangesAsync(token);
                await AuditAsync(ProtectedResourceType.Payment, payment.Id, AuditAction.Create, token);
                await AuditAsync(ProtectedResourceType.Invoice, invoice.Id, AuditAction.Update, token);
            },
            cancellationToken);

        await AttachClientUserAsync(invoice, cancellationToken);
        return invoice.ToDetailDto();
    }

    public async Task<InvoiceDetailDto> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await GetInvoiceEntityAsync(invoiceId, cancellationToken);
        await EnsureCanReadInvoiceAsync(invoice, cancellationToken);
        await LoadInvoiceChildrenAsync(invoice, cancellationToken);
        await AttachClientUserAsync(invoice, cancellationToken);
        await AuditAsync(ProtectedResourceType.Invoice, invoice.Id, AuditAction.Read, cancellationToken);
        return invoice.ToDetailDto();
    }

    public async Task<PagedResult<InvoiceSummaryDto>> GetInvoicesAsync(PagedRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        var (invoices, totalCount) = await unitOfWork.Invoices.GetPagedAsync(request.PageNumber, request.PageSize, cancellationToken);
        foreach (var invoice in invoices)
        {
            await LoadInvoiceChildrenAsync(invoice, cancellationToken);
            await AttachClientUserAsync(invoice, cancellationToken);
            await AuditAsync(ProtectedResourceType.Invoice, invoice.Id, AuditAction.Read, cancellationToken);
        }

        return PagedResultFactory.Create(
            invoices.Select(invoice => invoice.ToSummaryDto()).ToArray(),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    public async Task<MarginSummaryDto> GetMarginSummaryAsync(DateTime periodStartUtc, DateTime periodEndUtc, CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        EnsureValidPeriod(periodStartUtc, periodEndUtc);
        var shifts = await shiftBillingQuery.GetCompletedBillableShiftsAsync(periodStartUtc, periodEndUtc, cancellationToken);
        await AuditShiftReadsAsync(shifts, cancellationToken);
        return shifts.ToMarginSummaryDto(periodStartUtc, periodEndUtc);
    }

    public async Task<PagedResult<ShiftMarginDto>> GetShiftMarginsAsync(
        PagedRequest request,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureAdmin();
        EnsureValidPeriod(periodStartUtc, periodEndUtc);
        var (shifts, totalCount) = await shiftBillingQuery.GetCompletedBillableShiftPageAsync(
            periodStartUtc,
            periodEndUtc,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
        await AuditShiftReadsAsync(shifts, cancellationToken);

        return PagedResultFactory.Create(
            shifts.Select(shift => shift.ToMarginDto()).ToArray(),
            totalCount,
            request.PageNumber,
            request.PageSize);
    }

    private async Task AuditShiftReadsAsync(IReadOnlyList<Shift> shifts, CancellationToken cancellationToken)
    {
        foreach (var shift in shifts)
        {
            await AuditAsync(ProtectedResourceType.Shift, shift.Id, AuditAction.Read, cancellationToken);
        }
    }

    private async Task EnsureCanReadInvoiceAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        if (HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            return;
        }

        if (HasRole(ApplicationRoles.Client) && currentUser.UserId.HasValue)
        {
            var clients = await unitOfWork.Clients.FindAsync(client => client.UserId == currentUser.UserId.Value, cancellationToken);
            if (clients.Any(client => client.Id == invoice.ClientId))
            {
                return;
            }

            var access = await clientAccessEvaluator.EvaluateAsync(currentUser.UserId.Value, invoice.ClientId, AccessScope.Full, cancellationToken);
            if (access.IsAuthorized)
            {
                return;
            }
        }

        await AuditAsync(ProtectedResourceType.Invoice, invoice.Id, AuditAction.AccessDenied, cancellationToken);
        throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
    }

    private async Task LoadInvoiceChildrenAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        invoice.LineItems = (await unitOfWork.InvoiceLineItems.FindAsync(lineItem => lineItem.InvoiceId == invoice.Id, cancellationToken)).ToList();
        invoice.Payments = (await unitOfWork.Payments.FindAsync(payment => payment.InvoiceId == invoice.Id, cancellationToken)).ToList();
    }

    private async Task AttachClientUserAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        invoice.Client = await GetClientAsync(invoice.ClientId, cancellationToken);
        invoice.Client.User = await unitOfWork.Users.GetByIdAsync(invoice.Client.UserId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<Client> GetClientAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Clients.GetByIdAsync(clientId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private async Task<Invoice> GetInvoiceEntityAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        return await unitOfWork.Invoices.GetByIdAsync(invoiceId, cancellationToken)
            ?? throw new ResourceNotFoundException(isPhiResource: true);
    }

    private void EnsureAdminOrCoordinator()
    {
        if (!HasAnyRole(ApplicationRoles.Admin, ApplicationRoles.Coordinator))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private void EnsureAdmin()
    {
        if (!HasRole(ApplicationRoles.Admin))
        {
            throw new ResourceAccessDeniedException("RoleInsufficient", isPhiResource: true);
        }
    }

    private static void EnsureValidPeriod(DateTime periodStartUtc, DateTime periodEndUtc)
    {
        if (periodStartUtc.Kind != DateTimeKind.Utc || periodEndUtc.Kind != DateTimeKind.Utc || periodEndUtc <= periodStartUtc)
        {
            throw new ValidationException("The reporting period is invalid.");
        }
    }

    private bool HasAnyRole(params string[] roles) => roles.Any(HasRole);

    private bool HasRole(string role) => currentUser.Roles.Contains(role);

    private Task AuditAsync(ProtectedResourceType entityType, Guid entityId, AuditAction action, CancellationToken cancellationToken)
    {
        return auditLogger.LogAsync(
            new PhiAuditEntry(
                currentUser.UserId,
                currentUser.UserId.HasValue ? AuditActorType.User : AuditActorType.Anonymous,
                DateTime.UtcNow,
                action,
                entityType,
                entityId,
                currentUser.CorrelationId),
            cancellationToken);
    }

    private static string CreateInvoiceNumber(Invoice invoice)
    {
        return $"INV-{invoice.PeriodEndUtc:yyyyMMdd}-{invoice.Id.ToString("N")[..8].ToUpperInvariant()}";
    }

    private static string CreateLineDescription(ServiceType serviceType)
    {
        return serviceType == ServiceType.FacilityStaffing
            ? "Facility staffing service"
            : "In-home care service";
    }

    private static ResourceConflictException DuplicateInvoiceConflict()
    {
        return new ResourceConflictException(DuplicateInvoiceCode, "An invoice already exists for the requested billing period.");
    }

    private static ValidationException ValidationFailure(string propertyName, string message, string code)
    {
        return new ValidationException(new[] { new ValidationFailure(propertyName, message) { ErrorCode = code } });
    }
}

