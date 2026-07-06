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

    Task<TransitionInstructionClinicalDto> ReviewInstructionAsync(
        Guid planId,
        Guid instructionId,
        ReviewInstructionRequest request,
        CancellationToken cancellationToken = default);

    Task<TransitionPlanClinicalDto> ActivatePlanAsync(
        Guid planId,
        ActivatePlanRequest request,
        CancellationToken cancellationToken = default);

    Task<TransitionReminderDto> ScheduleReminderAsync(
        Guid planId,
        ScheduleReminderRequest request,
        CancellationToken cancellationToken = default);
}
