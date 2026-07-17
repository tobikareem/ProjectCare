using CarePath.Domain.Enumerations;

namespace CarePath.Application.Abstractions.Billing;

/// <summary>
/// The ONE shared, SQL-backed billing eligibility implementation (D-S6-18). Preview, invoice
/// creation, and reconciliation all classify shifts through this query so their answers can
/// never drift apart. Classification applies exactly one <see cref="BillingExclusionReason"/>
/// per shift in ascending precedence; already-invoiced detection includes soft-deleted invoice
/// lines so historical links keep blocking rebilling.
/// </summary>
public interface IBillingEligibilityQuery
{
    /// <summary>
    /// Classifies every shift for one client/service line in a half-open UTC period, ordered
    /// by scheduled start then shift ID.
    /// </summary>
    Task<IReadOnlyList<BillingEligibilityRow>> GetPeriodRowsAsync(
        Guid clientId,
        ServiceType serviceType,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Classifies a single shift, or returns null when the shift does not exist.</summary>
    Task<BillingEligibilityRow?> GetShiftRowAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-paged reconciliation search: non-eligible rows in the criteria window (with the
    /// D-S6-18 24-hour gate on not-completed shifts), ordered oldest service date then shift
    /// ID, plus KPI totals across the entire filtered set.
    /// </summary>
    Task<ReconciliationPage> SearchReconciliationAsync(
        ReconciliationSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
