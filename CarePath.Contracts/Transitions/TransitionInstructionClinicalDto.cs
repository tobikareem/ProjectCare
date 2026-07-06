using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Instruction view for clinician review (Clinician/Coordinator endpoints only; reads audited).
/// Carries <c>SourceText</c> and confidence per D-S5-3 — this DTO must NEVER be returned on
/// patient-facing or care-team routes.
/// </summary>
public class TransitionInstructionClinicalDto
{
    /// <summary>Instruction identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owning transition plan.</summary>
    public Guid TransitionPlanId { get; init; }

    /// <summary>Instruction category.</summary>
    public TransitionInstructionCategory Category { get; init; }

    /// <summary>The instruction text (as extracted or clinician-modified). PHI — never log.</summary>
    public string InstructionText { get; init; } = string.Empty;

    /// <summary>Original discharge document excerpt. PHI — never log (D-S5-3).</summary>
    public string? SourceText { get; init; }

    /// <summary>Extraction confidence (0..1).</summary>
    public decimal ConfidenceScore { get; init; }

    /// <summary>True when confidence is below the 0.75 review threshold.</summary>
    public bool IsLowConfidence { get; init; }

    /// <summary>Clinician note added during review. PHI — never log.</summary>
    public string? ClinicalNote { get; init; }

    /// <summary>True when a pharmacist should double-check this instruction.</summary>
    public bool NeedsPharmacistReview { get; init; }

    /// <summary>Review status.</summary>
    public TransitionInstructionStatus Status { get; init; }
}
