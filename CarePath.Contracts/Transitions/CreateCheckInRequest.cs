using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Patient (self or grantee) symptom check-in submission (plan ID travels in the route).
/// Clinical PHI: never log this request body; <see cref="ResponsesJson"/> is never echoed back
/// in any response or validation error. The server evaluates warning symptoms and creates a
/// coordinator escalation record when found (D-S5-7).
/// </summary>
public class CreateCheckInRequest
{
    /// <summary>Channel the check-in arrives on. App for Sprint 5; Sms/Voice arrive in Sprint 7.</summary>
    public ReminderChannel Channel { get; init; } = ReminderChannel.App;

    /// <summary>Structured symptom/adherence responses as JSON. PHI — never log, never echo.</summary>
    public string ResponsesJson { get; init; } = string.Empty;
}
