using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Coordinator acknowledgement of an escalation (D-S5-7): documents the human decision. The
/// chosen <see cref="EscalationLevel"/> records what the coordinator decided to do — the system
/// itself never contacts family, urgent care, or 911. Clinical PHI: never log this request body.
/// </summary>
public class AcknowledgeEscalationRequest
{
    /// <summary>The level of the coordinator's documented human decision.</summary>
    public EscalationLevel EscalationLevel { get; init; }

    /// <summary>Resolution documentation. PHI — never log.</summary>
    public string ResolutionNote { get; init; } = string.Empty;
}
