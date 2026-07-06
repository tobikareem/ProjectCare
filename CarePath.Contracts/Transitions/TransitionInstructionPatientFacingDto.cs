using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Patient-facing / care-team-safe instruction view (D-S5-3): only clinician-approved content,
/// with NO source text, confidence scores, review status, or clinician notes. Servers must only
/// project instructions whose status is Approved or Modified into this DTO.
/// </summary>
public class TransitionInstructionPatientFacingDto
{
    /// <summary>Instruction identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Instruction category.</summary>
    public TransitionInstructionCategory Category { get; init; }

    /// <summary>The clinician-approved instruction text. PHI — never log.</summary>
    public string InstructionText { get; init; } = string.Empty;
}
