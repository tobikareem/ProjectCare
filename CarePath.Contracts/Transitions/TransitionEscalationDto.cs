using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// A coordinator escalation record (D-S5-7: records only — the system never acts on these
/// autonomously). Coordinator-facing clinical PHI: reads audited; trigger details never logged.
/// </summary>
public class TransitionEscalationDto
{
    /// <summary>Escalation identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owning transition plan.</summary>
    public Guid TransitionPlanId { get; init; }

    /// <summary>What triggered the escalation.</summary>
    public EscalationTriggerType TriggerType { get; init; }

    /// <summary>Trigger context for the coordinator. PHI — never log.</summary>
    public string TriggerDetails { get; init; } = string.Empty;

    /// <summary>Escalation level. CoordinatorAlert unless a coordinator documented a higher-level human decision.</summary>
    public EscalationLevel EscalationLevel { get; init; }

    /// <summary>When the escalation was created (UTC).</summary>
    public DateTime EscalatedAt { get; init; }

    /// <summary>Coordinator who acknowledged it, when acknowledged.</summary>
    public Guid? AcknowledgedBy { get; init; }

    /// <summary>When it was acknowledged (UTC).</summary>
    public DateTime? AcknowledgedAt { get; init; }

    /// <summary>Coordinator's resolution documentation. PHI — never log.</summary>
    public string? ResolutionNote { get; init; }
}
