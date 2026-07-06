using CarePath.Domain.Enumerations;

namespace CarePath.Application.Transitions.Interfaces;

public sealed record ExtractedTransitionInstruction(
    TransitionInstructionCategory Category,
    string InstructionText,
    string? SourceText,
    decimal ConfidenceScore,
    bool NeedsPharmacistReview);
