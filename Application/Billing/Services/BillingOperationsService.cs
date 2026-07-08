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
    private const string InvoicePeriodIndexName = "IX_Invoices_Client_Service_Period";

    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserContext currentUser;
    private readonly IClientAccessEvaluator clientAccessEvaluator;
    private readonly IPhiAuditLogger auditLogger;
    private readonly IShiftBillingQuery shiftBillingQuery;
    private readonly IPersistenceConflictDetector persistenceConflictDetector;
    private readonly IValidator<CreateInvoiceRequest> createInvoiceValidator;
    private readonly IValidator<RecordPaymentRequest> recordPaymentValidator;

    public BillingOperationsService(
        IUnitOfWork unitOfWork,
        ICurrentUserContext currentUser,
        IClientAccessEvaluator clientAccessEvaluator,
        IPhiAuditLogger auditLogger,
        IShiftBillingQuery? shiftBillingQuery = null,
        IPersistenceConflictDetector? persistenceConflictDetector = null,
        IValidator<CreateInvoiceRequest>? createInvoiceValidator = null,
        IValidator<RecordPaymentRequest>? recordPaymentValidator = null)
    {
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.clientAccessEvaluator = clientAccessEvaluator;
        this.auditLogger = auditLogger;
        this.shiftBillingQuery = shiftBillingQuery ?? new NoOpShiftBillingQuery();
        this.persistenceConflictDetector = persistenceConflictDetector ?? new NoOpPersistenceConflictDetector();
        this.createInvoiceValidator = createInvoiceValidator ?? new CreateInvoiceRequestValidator();
        this.recordPaymentValidator = recordPaymentValidator ?? new RecordPaymentRequestValidator();
    }

    public async Task<InvoiceDetailDto> CreateInvoiceAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        EnsureAdminOrCoordinator();
        await createInvoiceValidator.ValidateAndThrowAsync(request, cancellationToken);

        var serviceType = (ServiceType)(int)request.ServiceType;
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

        var billableShifts = await shiftBillingQuery.GetCompletedBillableShiftsAsync(
            client.Id,
            serviceType,
            request.PeriodStartUtc,
            request.PeriodEndUtc,
            cancellationToken);
        if (billableShifts.Count == 0)
        {
            throw ValidationFailure("Period", "No completed billable shifts exist for the requested billing period.", EmptyInvoiceCode);
        }

        await AuditShiftReadsAsync(billableShifts, cancellationToken);

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

        foreach (var shift in billableShifts)
        {
            invoice.LineItems.Add(new InvoiceLineItem
            {
                InvoiceId = invoice.Id,
                Invoice = invoice,
                ShiftId = shift.Id,
                Shift = shift,
                Description = CreateLineDescription(serviceType),
                ServiceDate = shift.ScheduledStartTime.Date,
                BillableHours = shift.BillableHours,
                RatePerHour = shift.BillRate,
                CostPerHour = shift.PayRate,
            });
        }

        try
        {
            await unitOfWork.ExecuteInTransactionAsync(
                async token =>
                {
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

        await AttachClientUserAsync(invoice, cancellationToken);
        return invoice.ToDetailDto();
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

