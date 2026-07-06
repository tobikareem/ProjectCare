using CarePath.Application.Transitions.Interfaces;
using CarePath.Domain.Enumerations;

namespace CarePath.Infrastructure.Transitions.Services;

/// <summary>
/// Deterministic Sprint 5 discharge extraction stub. It uses local keyword rules only and
/// never calls external AI, OCR, messaging, or storage providers.
/// </summary>
public sealed class RuleBasedDischargeExtractionService : IDischargeExtractionService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ExtractedTransitionInstruction>> ExtractAsync(
        string rawContent,
        DischargeDocumentSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        _ = sourceType;
        cancellationToken.ThrowIfCancellationRequested();

        var instructions = SplitLines(rawContent)
            .Select(CreateInstruction)
            .ToArray();

        if (instructions.Length == 0)
        {
            instructions =
            [
                new ExtractedTransitionInstruction(
                    TransitionInstructionCategory.Other,
                    "Review discharge instructions with the care team.",
                    null,
                    0.6000m,
                    false)
            ];
        }

        return Task.FromResult<IReadOnlyList<ExtractedTransitionInstruction>>(instructions);
    }

    private static ExtractedTransitionInstruction CreateInstruction(string sourceText)
    {
        var category = Categorize(sourceText);
        var needsPharmacistReview = category == TransitionInstructionCategory.Medication
            && ContainsAny(sourceText, "warfarin", "insulin", "opioid", "oxycodone", "digoxin");
        var confidence = category == TransitionInstructionCategory.Other ? 0.6500m : 0.8500m;

        return new ExtractedTransitionInstruction(
            category,
            sourceText,
            sourceText,
            confidence,
            needsPharmacistReview);
    }

    private static TransitionInstructionCategory Categorize(string text)
    {
        if (ContainsAny(text, "medication", "medicine", "dose", "tablet", "pill", "insulin", "warfarin"))
        {
            return TransitionInstructionCategory.Medication;
        }

        if (ContainsAny(text, "appointment", "follow up", "follow-up", "visit", "clinic"))
        {
            return TransitionInstructionCategory.Appointment;
        }

        if (ContainsAny(text, "diet", "fluid", "sodium", "meal"))
        {
            return TransitionInstructionCategory.Diet;
        }

        if (ContainsAny(text, "activity", "exercise", "walking", "lift"))
        {
            return TransitionInstructionCategory.Activity;
        }

        if (ContainsAny(text, "wound", "dressing", "incision"))
        {
            return TransitionInstructionCategory.WoundCare;
        }

        if (ContainsAny(text, "call 911", "emergency", "shortness of breath", "chest pain", "fever"))
        {
            return TransitionInstructionCategory.WarningSigns;
        }

        if (ContainsAny(text, "equipment", "walker", "oxygen", "cane"))
        {
            return TransitionInstructionCategory.Equipment;
        }

        return TransitionInstructionCategory.Other;
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> SplitLines(string rawContent) =>
        rawContent
            .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(25)
            .ToArray();
}
