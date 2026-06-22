namespace CarePath.Domain.Enumerations;

/// <summary>
/// The event that caused a <see cref="Entities.Transitions.TransitionEscalation"/> to be created.
/// </summary>
public enum EscalationTriggerType
{
    /// <summary>
    /// Patient missed a critical reminder (medication, appointment, or wound care)
    /// within the window defined by the plan's <see cref="TransitionRiskLevel"/>.
    /// </summary>
    MissedCriticalTask = 1,

    /// <summary>
    /// A symptom check-in response matched one or more clinician-defined warning signs.
    /// Always triggers immediate escalation regardless of risk level.
    /// </summary>
    WarningSymptomsReported = 2,

    /// <summary>
    /// The coordinator's outbound contact attempt to the patient failed
    /// (no answer, wrong number, unreachable).
    /// </summary>
    FailedContact = 3,

    /// <summary>
    /// An in-home caregiver raised a concern in a VisitNote linked to this transition plan.
    /// </summary>
    CaregiverAlert = 4
}
