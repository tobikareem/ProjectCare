using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// One unresolved-service row in the reconciliation queue (D-S6-18). Carries service, client,
/// and caregiver display attribution, the single classified reason, aging, estimated billable
/// value when calculable, and the server-computed corrective destination. Deliberately carries
/// no caregiver/client identifiers beyond the Shift ID needed for detail navigation, and no
/// pay, cost, margin, GPS, note, visit-note, credential-number, or clinical fields.
/// </summary>
public class BillingReconciliationRowDto
{
    /// <summary>Shift identifier — the reconciliation drill-in key.</summary>
    public Guid ShiftId { get; init; }

    /// <summary>UTC date the service was (or was to be) delivered.</summary>
    public DateTime ServiceDateUtc { get; init; }

    /// <summary>Scheduled service window start (UTC).</summary>
    public DateTime ScheduledStartUtc { get; init; }

    /// <summary>Scheduled service window end (UTC).</summary>
    public DateTime ScheduledEndUtc { get; init; }

    /// <summary>Client (billing account) display name.</summary>
    public string ClientDisplayName { get; init; } = string.Empty;

    /// <summary>Display name of the caregiver attributed to the service; empty when unassigned.</summary>
    public string CaregiverDisplayName { get; init; } = string.Empty;

    /// <summary>Service line of the shift.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>The single classified reason for this row (deterministic precedence).</summary>
    public BillingExclusionReason Reason { get; init; }

    /// <summary>Whole days since the service date (UTC), for aging display and filters.</summary>
    public int AgeDays { get; init; }

    /// <summary>True when the row counts toward revenue at risk (see D-S6-18 rules).</summary>
    public bool IsRevenueAtRisk { get; init; }

    /// <summary>Estimated billable value (USD, rounded) when hours and rate allow calculation.</summary>
    public decimal? EstimatedValue { get; init; }

    /// <summary>Owning invoice for already-invoiced rows; null otherwise.</summary>
    public Guid? OwningInvoiceId { get; init; }

    /// <summary>Server-computed corrective destination for the UI.</summary>
    public BillingCorrectiveDestination CorrectiveDestination { get; init; }
}
