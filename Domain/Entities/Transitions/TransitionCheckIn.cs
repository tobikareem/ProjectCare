using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// Records a patient's symptom and adherence response to a check-in prompt
/// during the 30-day transition monitoring window.
/// When <see cref="ContainsWarningSymptom"/> is <c>true</c>, the Application layer
/// must trigger an immediate escalation evaluation regardless of risk level.
/// </summary>
/// <remarks>
/// <b>PHI:</b> <see cref="ResponsesJson"/> contains the patient's health responses.
/// This field must never appear in logs or exception messages.
/// </remarks>
public class TransitionCheckIn : BaseEntity
{
    /// <summary>Foreign key to the plan this check-in belongs to.</summary>
    public Guid TransitionPlanId { get; set; }

    /// <summary>Navigation to the parent <see cref="TransitionPlan"/>.</summary>
    public TransitionPlan? TransitionPlan { get; set; }

    /// <summary>UTC date and time the check-in response was received.</summary>
    public DateTime CheckInDate { get; set; } = DateTime.UtcNow;

    /// <summary>The channel through which the patient submitted this check-in.</summary>
    public ReminderChannel Channel { get; set; }

    /// <summary>
    /// JSON-serialised key/value map of the patient's answers to check-in prompts.
    /// Schema is determined by the clinician-authored check-in script for this plan.
    /// <b>PHI — never log this field.</b>
    /// </summary>
    public string ResponsesJson { get; set; } = "{}";

    /// <summary>
    /// Set to <c>true</c> by the Application layer when any response matches a
    /// clinician-defined warning sign pattern for this plan.
    /// Triggers immediate escalation to <see cref="EscalationLevel.CoordinatorAlert"/>.
    /// </summary>
    public bool ContainsWarningSymptom { get; set; }

    /// <summary>
    /// The <see cref="BaseEntity.Id"/> of the care coordinator who reviewed this check-in.
    /// Null if not yet reviewed.
    /// </summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>UTC timestamp when the coordinator completed their review of this check-in.</summary>
    public DateTime? ReviewedAt { get; set; }
}
