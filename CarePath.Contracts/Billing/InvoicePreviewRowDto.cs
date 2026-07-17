namespace CarePath.Contracts.Billing;

/// <summary>
/// One eligible billable row on the invoice preview (D-S6-18). Minimum-necessary by contract:
/// service timing, billing math, and caregiver attribution only. Deliberately carries NO shift,
/// caregiver, or client identifiers and no pay, cost, margin, GPS, note, visit-note, credential
/// number, or clinical fields — reflection denylist tests enforce this.
/// </summary>
public class InvoicePreviewRowDto
{
    /// <summary>UTC date the service was delivered.</summary>
    public DateTime ServiceDateUtc { get; init; }

    /// <summary>Actual service window start (UTC).</summary>
    public DateTime ServiceStartUtc { get; init; }

    /// <summary>Actual service window end (UTC).</summary>
    public DateTime ServiceEndUtc { get; init; }

    /// <summary>Billable hours after unpaid breaks.</summary>
    public decimal BillableHours { get; init; }

    /// <summary>Hourly rate billed to the client (USD).</summary>
    public decimal BillRate { get; init; }

    /// <summary>Line total rounded to two decimals with away-from-zero midpoint rounding.</summary>
    public decimal LineTotal { get; init; }

    /// <summary>Display name of the caregiver who delivered the service.</summary>
    public string CaregiverDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Deterministic sorted set of professional credentials (RN/LPN/GNA/CNA/HHA/CRMA) valid on
    /// the service date, joined for display (e.g., <c>"CNA, GNA"</c>); <c>"Caregiver"</c> when
    /// none qualify. Training credentials and credential numbers are never included.
    /// </summary>
    public string QualificationLabel { get; init; } = string.Empty;
}
