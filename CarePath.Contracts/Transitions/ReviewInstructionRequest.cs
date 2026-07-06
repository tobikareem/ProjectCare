using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Clinician review action for one instruction. <see cref="Status"/> must be a terminal review
/// state (Approved, Modified, or Rejected — never Pending); Modified requires
/// <see cref="ModifiedInstructionText"/>. Clinical PHI: never log this request body.
/// </summary>
public class ReviewInstructionRequest
{
    /// <summary>Review outcome: Approved, Modified, or Rejected.</summary>
    public TransitionInstructionStatus Status { get; init; }

    /// <summary>Replacement instruction text when the outcome is Modified. PHI — never log.</summary>
    public string? ModifiedInstructionText { get; init; }

    /// <summary>Clinician note documenting the decision. PHI — never log.</summary>
    public string? ClinicalNote { get; init; }

    /// <summary>Flag the instruction for pharmacist double-check.</summary>
    public bool NeedsPharmacistReview { get; init; }
}
