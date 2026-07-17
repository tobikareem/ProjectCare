using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Billing;

/// <summary>
/// Records an audited non-billable resolution for one shift (D-S6-18). Admin/Coordinator only.
/// Appends a new resolution record — never edits history.
/// </summary>
public class ResolveNonBillableRequest
{
    /// <summary>Maximum note length.</summary>
    public const int NoteMaxLength = 500;

    /// <summary>
    /// The operational reason. <see cref="BillingReconciliationReason.Reopened"/> is rejected —
    /// reopening uses the dedicated reopen command.
    /// </summary>
    public BillingReconciliationReason Reason { get; init; }

    /// <summary>Optional PHI-free note (max 500 chars). Never place clinical context here.</summary>
    public string? Note { get; init; }
}
