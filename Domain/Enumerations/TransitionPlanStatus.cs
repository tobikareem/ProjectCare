namespace CarePath.Domain.Enumerations;

/// <summary>
/// Lifecycle status of a <see cref="Entities.Transitions.TransitionPlan"/>.
/// A plan must reach <see cref="Active"/> before any reminders are delivered to the patient.
/// </summary>
public enum TransitionPlanStatus
{
    /// <summary>AI extraction is complete but clinical review has not started.</summary>
    Draft = 1,

    /// <summary>Submitted to the clinician review queue. Awaiting e-signature.</summary>
    PendingVerification = 2,

    /// <summary>
    /// Clinician has approved and e-signed the plan. Reminders and check-ins are active.
    /// This is the only status in which reminders may be delivered.
    /// </summary>
    Active = 3,

    /// <summary>The 30-day transition window has ended with no adverse events.</summary>
    Completed = 4,

    /// <summary>The plan was cancelled before or during the transition window.</summary>
    Cancelled = 5
}
