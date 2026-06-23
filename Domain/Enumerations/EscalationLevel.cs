namespace CarePath.Domain.Enumerations;

/// <summary>
/// Severity level of a <see cref="CarePath.Domain.Entities.Transitions.TransitionEscalation"/>.
/// Determines what action the care coordinator takes.
/// The system never escalates autonomously beyond creating the record and alerting the coordinator.
/// All human contact decisions are made by the coordinator.
/// </summary>
public enum EscalationLevel
{
    /// <summary>
    /// An alert surfaces on the care coordinator's dashboard.
    /// Coordinator reviews and decides next action.
    /// </summary>
    CoordinatorAlert = 1,

    /// <summary>
    /// Coordinator notifies the patient's authorised family proxy.
    /// Used when the patient is unreachable or needs family support.
    /// </summary>
    FamilyNotification = 2,

    /// <summary>
    /// Coordinator advises the patient or family to seek urgent care (urgent care centre or telehealth).
    /// Used for concerning symptoms that do not require emergency services.
    /// </summary>
    UrgentCare = 3,

    /// <summary>
    /// Coordinator advises the patient or family to call 911 immediately.
    /// The system never calls 911 autonomously — this level only flags the coordinator to act.
    /// </summary>
    Emergency911 = 4
}
