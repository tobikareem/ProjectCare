using CarePath.Application.Abstractions.Billing;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Billing;

/// <summary>
/// The single SQL-backed billing eligibility implementation (D-S6-18). Classification runs in
/// the database (invoice-line links INCLUDING soft-deleted rows, latest reconciliation
/// resolution, shift lifecycle/time/rate rules) so preview, creation, and reconciliation share
/// one answer; display attribution is batch-composed afterwards.
/// </summary>
public sealed class BillingEligibilityQuery : IBillingEligibilityQuery
{
    private readonly CarePathDbContext context;

    /// <summary>Creates the query over the CarePath DbContext.</summary>
    /// <param name="context">CarePath database context.</param>
    public BillingEligibilityQuery(CarePathDbContext context)
    {
        this.context = context;
    }

    private sealed record ClassifiedShift(
        Shift Shift,
        Guid? OwningInvoiceId,
        BillingReconciliationReason? LatestResolutionReason);

    /// <inheritdoc />
    public async Task<IReadOnlyList<BillingEligibilityRow>> GetPeriodRowsAsync(
        Guid clientId,
        ServiceType serviceType,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        var shifts = context.Shifts
            .Where(shift =>
                shift.ClientId == clientId
                && shift.ServiceType == serviceType
                && shift.ScheduledStartTime >= periodStartUtc
                && shift.ScheduledStartTime < periodEndUtc)
            .OrderBy(shift => shift.ScheduledStartTime)
            .ThenBy(shift => shift.Id);

        var classified = await Classified(shifts)
            .ToListAsync(cancellationToken);

        return await ComposeRowsAsync(classified, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BillingEligibilityRow?> GetShiftRowAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        var classified = await Classified(context.Shifts.Where(shift => shift.Id == shiftId))
            .ToListAsync(cancellationToken);
        if (classified.Count == 0)
        {
            return null;
        }

        var rows = await ComposeRowsAsync(classified, cancellationToken);
        return rows[0];
    }

    /// <inheritdoc />
    public async Task<ReconciliationPage> SearchReconciliationAsync(
        ReconciliationSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria.PageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(criteria), "Page number must be greater than zero.");
        }

        if (criteria.PageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(criteria), "Page size must be greater than zero.");
        }

        var notCompletedVisibleBeforeUtc = criteria.UtcNow.AddHours(-BillingMath.NotCompletedGraceHours);

        // One bounded scalar load for the ≤92-day window, then ONE classifier (the same
        // Classify used by every other path) applied in memory — the search filter, the
        // displayed rows, and the KPI math can never disagree about a shift's reason.
        var scalars = await context.Shifts
            .Where(shift =>
                shift.ScheduledStartTime >= criteria.PeriodStartUtc
                && shift.ScheduledStartTime < criteria.PeriodEndUtc
                && (!criteria.ClientId.HasValue || shift.ClientId == criteria.ClientId.Value)
                && (!criteria.ServiceType.HasValue || shift.ServiceType == criteria.ServiceType.Value))
            .Select(shift => new
            {
                shift.Id,
                shift.Status,
                shift.ScheduledStartTime,
                shift.ScheduledEndTime,
                shift.ActualStartTime,
                shift.ActualEndTime,
                shift.BreakMinutes,
                shift.BillRate,
                HasInvoiceLink = context.InvoiceLineItems.IgnoreQueryFilters().Any(line => line.ShiftId == shift.Id),
                LatestResolutionReason = context.BillingReconciliationResolutions
                    .Where(resolution => resolution.ShiftId == shift.Id)
                    .OrderByDescending(resolution => resolution.ResolvedAtUtc)
                    .ThenByDescending(resolution => resolution.CreatedAt)
                    .Select(resolution => (BillingReconciliationReason?)resolution.Reason)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var classified = scalars
            .Select(entry => new
            {
                entry.Id,
                entry.ScheduledStartTime,
                Row = new BillingEligibilityRow(
                    entry.Id,
                    Guid.Empty,
                    string.Empty,
                    null,
                    string.Empty,
                    string.Empty,
                    ServiceType.InHomeCare,
                    entry.Status,
                    entry.ScheduledStartTime,
                    entry.ScheduledEndTime,
                    entry.ActualStartTime,
                    entry.ActualEndTime,
                    entry.BreakMinutes,
                    entry.BillRate,
                    0m,
                    null,
                    Classify(
                        entry.Status,
                        entry.ActualStartTime,
                        entry.ActualEndTime,
                        entry.BreakMinutes,
                        entry.BillRate,
                        entry.HasInvoiceLink,
                        entry.LatestResolutionReason),
                    null),
            })
            // The queue lists non-eligible rows; not-completed shifts appear only after the
            // 24-hour post-scheduled-end grace (D-S6-18).
            .Where(entry => entry.Row.Reason != BillingExclusionReason.Eligible)
            .Where(entry => entry.Row.Reason != BillingExclusionReason.NotCompleted
                || entry.Row.ScheduledEndUtc <= notCompletedVisibleBeforeUtc)
            .Where(entry => !criteria.Reason.HasValue || entry.Row.Reason == criteria.Reason.Value)
            .Where(entry => !criteria.AgedRiskOnly || BillingMath.IsAgedRisk(entry.Row, criteria.UtcNow))
            .OrderBy(entry => entry.ScheduledStartTime)
            .ThenBy(entry => entry.Id)
            .ToArray();

        var totalCount = classified.Length;
        var pageShiftIds = classified
            .Skip((criteria.PageNumber - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(entry => entry.Id)
            .ToList();

        var pageClassified = await Classified(context.Shifts.Where(shift => pageShiftIds.Contains(shift.Id)))
            .ToListAsync(cancellationToken);
        var pageRows = (await ComposeRowsAsync(pageClassified, cancellationToken))
            .OrderBy(row => row.ScheduledStartUtc)
            .ThenBy(row => row.ShiftId)
            .ToArray();

        var kpiRows = classified.Select(entry => entry.Row).ToArray();
        var atRisk = kpiRows.Where(BillingMath.IsRevenueAtRisk).ToArray();
        var aged = atRisk.Where(row => BillingMath.IsAgedRisk(row, criteria.UtcNow)).ToArray();
        var kpis = new ReconciliationKpiTotals(
            UnresolvedCount: kpiRows.Count(row => row.Reason != BillingExclusionReason.AlreadyInvoiced
                && row.Reason != BillingExclusionReason.NonBillableResolved),
            RevenueAtRiskValue: atRisk.Sum(row => BillingMath.EstimatedValue(row) ?? 0m),
            AgedCount: aged.Length,
            AgedValue: aged.Sum(row => BillingMath.EstimatedValue(row) ?? 0m));

        return new ReconciliationPage(pageRows, totalCount, kpis);
    }

    private IQueryable<ClassifiedShift> Classified(IQueryable<Shift> shifts)
    {
        return shifts.Select(shift => new ClassifiedShift(
            shift,
            context.InvoiceLineItems
                .IgnoreQueryFilters()
                .Where(line => line.ShiftId == shift.Id)
                .OrderBy(line => line.CreatedAt)
                .Select(line => (Guid?)line.InvoiceId)
                .FirstOrDefault(),
            context.BillingReconciliationResolutions
                .Where(resolution => resolution.ShiftId == shift.Id)
                .OrderByDescending(resolution => resolution.ResolvedAtUtc)
                .ThenByDescending(resolution => resolution.CreatedAt)
                .Select(resolution => (BillingReconciliationReason?)resolution.Reason)
                .FirstOrDefault()));
    }

    /// <summary>
    /// THE one classification implementation (D-S6-18 deterministic precedence). Every path —
    /// preview, create revalidation, reconciliation search/filter/KPIs, and single-shift
    /// detail — funnels through this method so no two surfaces can ever disagree.
    /// </summary>
    private static BillingExclusionReason Classify(
        ShiftStatus status,
        DateTime? actualStartUtc,
        DateTime? actualEndUtc,
        int breakMinutes,
        decimal billRate,
        bool hasInvoiceLink,
        BillingReconciliationReason? latestResolutionReason)
    {
        if (hasInvoiceLink)
        {
            return BillingExclusionReason.AlreadyInvoiced;
        }

        if (latestResolutionReason is not null
            && latestResolutionReason != BillingReconciliationReason.Reopened)
        {
            return BillingExclusionReason.NonBillableResolved;
        }

        if (status is ShiftStatus.Cancelled or ShiftStatus.NoShow)
        {
            return BillingExclusionReason.CancelledOrNoShow;
        }

        if (status != ShiftStatus.Completed)
        {
            return BillingExclusionReason.NotCompleted;
        }

        if (!actualStartUtc.HasValue || !actualEndUtc.HasValue)
        {
            return BillingExclusionReason.MissingActualTime;
        }

        if ((decimal)(actualEndUtc.Value - actualStartUtc.Value).TotalMinutes - breakMinutes <= 0)
        {
            return BillingExclusionReason.InvalidBillableTime;
        }

        return billRate <= 0
            ? BillingExclusionReason.MissingBillRate
            : BillingExclusionReason.Eligible;
    }

    private async Task<IReadOnlyList<BillingEligibilityRow>> ComposeRowsAsync(
        IReadOnlyList<ClassifiedShift> classified,
        CancellationToken cancellationToken)
    {
        if (classified.Count == 0)
        {
            return Array.Empty<BillingEligibilityRow>();
        }

        var clientIds = classified.Select(entry => entry.Shift.ClientId).Distinct().ToArray();
        var caregiverIds = classified
            .Where(entry => entry.Shift.CaregiverId.HasValue)
            .Select(entry => entry.Shift.CaregiverId!.Value)
            .Distinct()
            .ToArray();

        var clients = await context.Clients
            .Where(client => clientIds.Contains(client.Id))
            .Select(client => new { client.Id, client.UserId })
            .ToListAsync(cancellationToken);
        var caregivers = await context.Caregivers
            .Where(caregiver => caregiverIds.Contains(caregiver.Id))
            .Select(caregiver => new { caregiver.Id, caregiver.UserId })
            .ToListAsync(cancellationToken);
        var userIds = clients.Select(client => client.UserId)
            .Concat(caregivers.Select(caregiver => caregiver.UserId))
            .Distinct()
            .ToArray();
        var users = await context.DomainUsers
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName })
            .ToListAsync(cancellationToken);
        var certifications = caregiverIds.Length == 0
            ? []
            : await context.CaregiverCertifications
                .Where(certification => caregiverIds.Contains(certification.CaregiverId))
                .Select(certification => new
                {
                    certification.CaregiverId,
                    certification.Type,
                    certification.ExpirationDate,
                    certification.IssueDate,
                })
                .ToListAsync(cancellationToken);

        var usersById = users.ToDictionary(user => user.Id, user => $"{user.FirstName} {user.LastName}");
        var clientUserById = clients.ToDictionary(client => client.Id, client => client.UserId);
        var caregiverUserById = caregivers.ToDictionary(caregiver => caregiver.Id, caregiver => caregiver.UserId);
        var certificationsByCaregiver = certifications
            .GroupBy(certification => certification.CaregiverId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var rows = new List<BillingEligibilityRow>(classified.Count);
        foreach (var entry in classified)
        {
            var shift = entry.Shift;
            var clientDisplayName = clientUserById.TryGetValue(shift.ClientId, out var clientUserId)
                && usersById.TryGetValue(clientUserId, out var clientName)
                    ? clientName
                    : string.Empty;
            var caregiverDisplayName = string.Empty;
            var qualificationLabel = BillingQualification.NoCredentialLabel;
            if (shift.CaregiverId.HasValue
                && caregiverUserById.TryGetValue(shift.CaregiverId.Value, out var caregiverUserId)
                && usersById.TryGetValue(caregiverUserId, out var caregiverName))
            {
                caregiverDisplayName = caregiverName;
                var caregiverCertifications = certificationsByCaregiver.TryGetValue(shift.CaregiverId.Value, out var set)
                    ? set.Select(certification => (certification.Type, certification.IssueDate, certification.ExpirationDate))
                    : [];
                qualificationLabel = BillingQualification.LabelFor(caregiverCertifications, shift.ScheduledStartTime);
            }

            rows.Add(new BillingEligibilityRow(
                shift.Id,
                shift.ClientId,
                clientDisplayName,
                shift.CaregiverId,
                caregiverDisplayName,
                qualificationLabel,
                shift.ServiceType,
                shift.Status,
                shift.ScheduledStartTime,
                shift.ScheduledEndTime,
                shift.ActualStartTime,
                shift.ActualEndTime,
                shift.BreakMinutes,
                shift.BillRate,
                shift.PayRate,
                shift.UpdatedAt,
                Reason(entry),
                entry.OwningInvoiceId));
        }

        return rows;
    }

    private static BillingExclusionReason Reason(ClassifiedShift entry) => Classify(
        entry.Shift.Status,
        entry.Shift.ActualStartTime,
        entry.Shift.ActualEndTime,
        entry.Shift.BreakMinutes,
        entry.Shift.BillRate,
        entry.OwningInvoiceId.HasValue,
        entry.LatestResolutionReason);
}
