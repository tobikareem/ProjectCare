using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Acknowledgement view of a recorded check-in. Deliberately carries NO <c>ResponsesJson</c>
/// (D-S5-3) — the submitted symptom responses are clinical PHI reviewed server-side; only the
/// warning flag and review metadata are echoed.
/// </summary>
public class TransitionCheckInDto
{
    /// <summary>Check-in identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owning transition plan.</summary>
    public Guid TransitionPlanId { get; init; }

    /// <summary>When the check-in was recorded (UTC).</summary>
    public DateTime CheckInDate { get; init; }

    /// <summary>Channel the check-in arrived on.</summary>
    public ReminderChannel Channel { get; init; }

    /// <summary>True when any response matched a warning symptom (triggers a coordinator escalation).</summary>
    public bool ContainsWarningSymptom { get; init; }

    /// <summary>Coordinator/clinician who reviewed the check-in, when reviewed.</summary>
    public Guid? ReviewedBy { get; init; }

    /// <summary>When the check-in was reviewed (UTC).</summary>
    public DateTime? ReviewedAt { get; init; }
}
