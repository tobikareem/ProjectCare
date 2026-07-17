namespace CarePath.Contracts.Billing;

/// <summary>
/// Guarded reconciliation drill-in for one shift (D-S6-18): the current row classification
/// plus the append-only resolution history. Already-invoiced rows expose only the owning
/// invoice ID for navigation — never invoice content.
/// </summary>
public class BillingReconciliationDetailDto
{
    /// <summary>The current classified row for this shift.</summary>
    public BillingReconciliationRowDto Row { get; init; } = new();

    /// <summary>Append-only resolution history, newest first. Empty when never resolved.</summary>
    public IReadOnlyList<BillingResolutionRecordDto> ResolutionHistory { get; init; } = [];
}
