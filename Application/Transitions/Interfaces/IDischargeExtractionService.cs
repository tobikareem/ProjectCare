using CarePath.Domain.Enumerations;

namespace CarePath.Application.Transitions.Interfaces;

public interface IDischargeExtractionService
{
    Task<IReadOnlyList<ExtractedTransitionInstruction>> ExtractAsync(
        string rawContent,
        DischargeDocumentSourceType sourceType,
        CancellationToken cancellationToken = default);
}
