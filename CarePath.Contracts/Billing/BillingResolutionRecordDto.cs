using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// One append-only reconciliation resolution record (D-S6-18). History is immutable: reopening
/// appends a superseding record; nothing is edited or deleted.
/// </summary>
public class BillingResolutionRecordDto
{
    /// <summary>Resolution record identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The recorded reason (<see cref="BillingReconciliationReason.Reopened"/> for reopen records).</summary>
    public BillingReconciliationReason Reason { get; init; }

    /// <summary>Display name of the staff member who recorded the decision.</summary>
    public string ResolvedByDisplayName { get; init; } = string.Empty;

    /// <summary>When the decision was recorded (UTC).</summary>
    public DateTime ResolvedAtUtc { get; init; }

    /// <summary>Optional PHI-free note recorded with the decision.</summary>
    public string? Note { get; init; }

    /// <summary>True when a later record supersedes this one.</summary>
    public bool IsSuperseded { get; init; }
}
