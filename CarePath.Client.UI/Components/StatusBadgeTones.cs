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

    /// <summary>Tone for a transition plan status.</summary>
    /// <param name="status">The plan status.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(TransitionPlanStatus status) => status switch
    {
        TransitionPlanStatus.Draft => BadgeTone.Neutral,
        TransitionPlanStatus.PendingVerification => BadgeTone.Warning,
        TransitionPlanStatus.Active => BadgeTone.Success,
        TransitionPlanStatus.Completed => BadgeTone.Info,
        TransitionPlanStatus.Cancelled => BadgeTone.Neutral,
        _ => BadgeTone.Neutral
    };

    /// <summary>Tone for a transition risk level.</summary>
    /// <param name="level">The risk level.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(TransitionRiskLevel level) => level switch
    {
        TransitionRiskLevel.Low => BadgeTone.Success,
        TransitionRiskLevel.Medium => BadgeTone.Warning,
        TransitionRiskLevel.High => BadgeTone.Danger,
        _ => BadgeTone.Neutral
    };

    /// <summary>Tone for a reminder status.</summary>
    /// <param name="status">The reminder status.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(ReminderStatus status) => status switch
    {
        ReminderStatus.Scheduled => BadgeTone.Info,
        ReminderStatus.Sent => BadgeTone.Info,
        ReminderStatus.Acknowledged => BadgeTone.Success,
        ReminderStatus.Missed => BadgeTone.Danger,
        ReminderStatus.Failed => BadgeTone.Danger,
        _ => BadgeTone.Neutral
    };

    /// <summary>Tone for an escalation level.</summary>
    /// <param name="level">The escalation level.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(EscalationLevel level) => level switch
    {
        EscalationLevel.CoordinatorAlert => BadgeTone.Warning,
        EscalationLevel.FamilyNotification => BadgeTone.Warning,
        EscalationLevel.UrgentCare => BadgeTone.Danger,
        EscalationLevel.Emergency911 => BadgeTone.Danger,
        _ => BadgeTone.Neutral
    };

    /// <summary>Tone for a discharge document status.</summary>
    /// <param name="status">The document status.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(DischargeDocumentStatus status) => status switch
    {
        DischargeDocumentStatus.Pending => BadgeTone.Neutral,
        DischargeDocumentStatus.Extracting => BadgeTone.Info,
        DischargeDocumentStatus.AwaitingReview => BadgeTone.Warning,
        DischargeDocumentStatus.Approved => BadgeTone.Success,
        DischargeDocumentStatus.Rejected => BadgeTone.Danger,
        _ => BadgeTone.Neutral
    };

    /// <summary>Tone for a transition instruction review status.</summary>
    /// <param name="status">The instruction status.</param>
    /// <returns>The badge tone.</returns>
    public static BadgeTone For(TransitionInstructionStatus status) => status switch
    {
        TransitionInstructionStatus.Pending => BadgeTone.Warning,
        TransitionInstructionStatus.Approved => BadgeTone.Success,
        TransitionInstructionStatus.Modified => BadgeTone.Success,
        TransitionInstructionStatus.Rejected => BadgeTone.Neutral,
        _ => BadgeTone.Neutral
    };
}
