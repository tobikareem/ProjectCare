using CarePath.Domain.Enumerations;

namespace CarePath.Application.Abstractions.Billing;

/// <summary>
/// One shift classified for billing (D-S6-18). Internal cross-layer projection — IDs are
/// permitted here; DTO mapping applies the minimum-necessary contract shapes.
/// </summary>
/// <param name="ShiftId">Shift identifier.</param>
/// <param name="ClientId">Owning client identifier.</param>
/// <param name="ClientDisplayName">Client (billing account) display name.</param>
/// <param name="CaregiverId">Assigned caregiver, when any.</param>
/// <param name="CaregiverDisplayName">Caregiver display name; empty when unassigned.</param>
/// <param name="QualificationLabel">Deterministic professional-credential label valid on the service date.</param>
/// <param name="ServiceType">Shift service line.</param>
/// <param name="ShiftStatus">Shift lifecycle status.</param>
/// <param name="ScheduledStartUtc">Scheduled window start (UTC).</param>
/// <param name="ScheduledEndUtc">Scheduled window end (UTC).</param>
/// <param name="ActualStartUtc">Actual check-in (UTC), when captured.</param>
/// <param name="ActualEndUtc">Actual check-out (UTC), when captured.</param>
/// <param name="BreakMinutes">Unpaid break minutes.</param>
/// <param name="BillRate">Hourly bill rate (USD).</param>
/// <param name="PayRate">Hourly pay rate (USD). Internal margin input only — never mapped to any billing DTO.</param>
/// <param name="ShiftUpdatedAtUtc">Shift's last update stamp — part of the preview fingerprint.</param>
/// <param name="Reason">The single classified reason (deterministic precedence).</param>
/// <param name="OwningInvoiceId">Owning invoice when <see cref="BillingExclusionReason.AlreadyInvoiced"/>.</param>
public sealed record BillingEligibilityRow(
    Guid ShiftId,
    Guid ClientId,
    string ClientDisplayName,
    Guid? CaregiverId,
    string CaregiverDisplayName,
    string QualificationLabel,
    ServiceType ServiceType,
    ShiftStatus ShiftStatus,
    DateTime ScheduledStartUtc,
    DateTime ScheduledEndUtc,
    DateTime? ActualStartUtc,
    DateTime? ActualEndUtc,
    int BreakMinutes,
    decimal BillRate,
    decimal PayRate,
    DateTime? ShiftUpdatedAtUtc,
    BillingExclusionReason Reason,
    Guid? OwningInvoiceId);

/// <summary>Reconciliation search criteria (D-S6-18). All timestamps UTC; period is half-open.</summary>
public sealed record ReconciliationSearchCriteria(
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    Guid? ClientId,
    ServiceType? ServiceType,
    BillingExclusionReason? Reason,
    bool AgedRiskOnly,
    int PageNumber,
    int PageSize,
    DateTime UtcNow);

/// <summary>KPI totals across the entire filtered reconciliation result (D-S6-18).</summary>
public sealed record ReconciliationKpiTotals(
    int UnresolvedCount,
    decimal RevenueAtRiskValue,
    int AgedCount,
    decimal AgedValue);

/// <summary>One server-paged reconciliation result page plus full-filter KPI totals.</summary>
public sealed record ReconciliationPage(
    IReadOnlyList<BillingEligibilityRow> Rows,
    int TotalCount,
    ReconciliationKpiTotals Kpis);
