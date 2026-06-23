using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// Records an escalation event triggered during a patient's 30-day transition window.
/// Escalations are surfaced on the care coordinator dashboard and require a human decision.
/// The system never autonomously contacts family, recommends urgent care, or calls emergency services.
/// </summary>
public class TransitionEscalation : BaseEntity
{
    /// <summary>Foreign key to the plan that generated this escalation.</summary>
    public Guid TransitionPlanId { get; set; }

    /// <summary>Navigation to the parent <see cref="TransitionPlan"/>.</summary>
    public TransitionPlan? TransitionPlan { get; set; }

    /// <summary>The event that caused this escalation to be created.</summary>
    public EscalationTriggerType TriggerType { get; set; }

    /// <summary>
    /// Human-readable description of the specific event that triggered this escalation.
    /// For example: "Medication reminder for Metformin 500mg missed at 08:00 UTC".
    /// Must not contain PHI such as patient names or diagnosis details.
    /// </summary>
    public string TriggerDetails { get; set; } = string.Empty;

    /// <summary>Severity level that determines the recommended coordinator action.</summary>
    public EscalationLevel EscalationLevel { get; set; }

    /// <summary>UTC timestamp when this escalation record was created.</summary>
    public DateTime EscalatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The <see cref="BaseEntity.Id"/> of the care coordinator who acknowledged this escalation.
    /// Null if the escalation has not yet been reviewed.
    /// </summary>
    public Guid? AcknowledgedBy { get; set; }

    /// <summary>UTC timestamp when the coordinator acknowledged and acted on this escalation.</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Free-text note documenting what action the coordinator took in response to this escalation.
    /// Required when acknowledging. Must not contain PHI patient identifiers.
    /// </summary>
    public string? ResolutionNote { get; set; }
}
