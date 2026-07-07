using CarePath.Contracts.Common;
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

    Task<PagedResult<TransitionPlanSummaryDto>> GetPlansAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<TransitionPlanClinicalDto> GetPlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<TransitionPlanPatientFacingDto> GetPatientPlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<object> GetPlanForClientAsync(
        Guid clientId,
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

    Task<TransitionCheckInDto> CreateCheckInAsync(
        Guid planId,
        CreateCheckInRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransitionEscalationDto>> GetEscalationsAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<TransitionEscalationDto>> GetEscalationQueueAsync(
        PagedRequest request,
        bool openOnly = true,
        CancellationToken cancellationToken = default);

    Task<TransitionEscalationDto> AcknowledgeEscalationAsync(
        Guid escalationId,
        AcknowledgeEscalationRequest request,
        CancellationToken cancellationToken = default);
}
