using CarePath.Contracts.Transitions;

namespace CarePath.Application.Transitions.Services;

public interface ITransitionsService
{
    Task<DischargeDocumentDto> CreateDischargeDocumentAsync(
        CreateDischargeDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<DischargeDocumentDto> GetDischargeDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<DischargeDocumentContentDto> GetDischargeDocumentContentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<TransitionPlanClinicalDto> ExtractDischargeDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
