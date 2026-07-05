using CarePath.Contracts.Enumerations;

namespace CarePath.Client.UI.Components;

/// <summary>
/// Standard tone mappings for CarePath status enums so every screen renders statuses consistently.
/// </summary>
public static class StatusBadgeTones
{
    /// <summary>Tone for a shift status.</summary>
    /// <param name="status">The shift status.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(ShiftStatus status) => status switch
    {
        ShiftStatus.Scheduled => BadgeTone.Info,
        ShiftStatus.InProgress => BadgeTone.Warning,
        ShiftStatus.Completed => BadgeTone.Success,
        ShiftStatus.Cancelled => BadgeTone.Neutral,
        ShiftStatus.NoShow => BadgeTone.Danger,
        _ => BadgeTone.Neutral
    };

    /// <summary>Tone for an invoice status.</summary>
    /// <param name="status">The invoice status.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => BadgeTone.Neutral,
        InvoiceStatus.Sent => BadgeTone.Info,
        InvoiceStatus.Paid => BadgeTone.Success,
        InvoiceStatus.PartiallyPaid => BadgeTone.Warning,
        InvoiceStatus.Overdue => BadgeTone.Danger,
        InvoiceStatus.Cancelled => BadgeTone.Neutral,
        _ => BadgeTone.Neutral
    };

    /// <summary>Tone for a payment status.</summary>
    /// <param name="status">The payment status.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => BadgeTone.Info,
        PaymentStatus.Settled => BadgeTone.Success,
        PaymentStatus.Failed => BadgeTone.Danger,
        PaymentStatus.Refunded => BadgeTone.Warning,
        _ => BadgeTone.Neutral
    };
}
