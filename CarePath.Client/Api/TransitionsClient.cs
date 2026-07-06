using CarePath.Client.Http;
using CarePath.Contracts.Common;
using CarePath.Contracts.Transitions;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/transitions</c>. Route shapes mirror the Sprint 5 endpoint matrix;
/// authorization, grant evaluation, and the D-S5 safety guards are all enforced server-side.
/// </summary>
/// <remarks>
/// AUDIENCE NOTE: methods returning clinical DTOs succeed only for Coordinator/Clinician
/// callers; <see cref="GetPatientViewAsync"/> is for Client-role callers (self or grantee);
/// <see cref="GetCareTeamPlanForClientAsync"/> is for assigned caregivers — the server returns
/// the care-team-safe shape for that role on the shared client-plan route (D-S5-3).
/// </remarks>
public sealed class TransitionsClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public TransitionsClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Submits discharge intake (Coordinator). Request contains PHI — never logged client-side.</summary>
    /// <param name="request">The intake request (metadata + approved raw text only).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created document status view.</returns>
    public Task<ApiResponse<DischargeDocumentDto>> CreateDischargeDocumentAsync(
        CreateDischargeDocumentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateDischargeDocumentRequest, DischargeDocumentDto>(
            "api/transitions/documents", request, cancellationToken);

    /// <summary>Gets document status/metadata (Coordinator/Clinician). No raw content.</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The document status view.</returns>
    public Task<ApiResponse<DischargeDocumentDto>> GetDischargeDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<DischargeDocumentDto>($"api/transitions/documents/{documentId}", cancellationToken);

    /// <summary>Gets raw discharge content for review (Coordinator/Clinician; server audits every read).</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The raw content view.</returns>
    public Task<ApiResponse<DischargeDocumentContentDto>> GetDischargeDocumentContentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<DischargeDocumentContentDto>(
            $"api/transitions/documents/{documentId}/content", cancellationToken);

    /// <summary>Triggers extraction for a document (Coordinator). Stub creates a draft plan.</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The response envelope (202 Accepted on success).</returns>
    public Task<ApiResponse> RequestExtractionAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        PostAsync($"api/transitions/documents/{documentId}/extract", cancellationToken);

    /// <summary>Gets the coordinator dashboard page of plans (Coordinator/Clinician).</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged plan summaries.</returns>
    public Task<ApiResponse<PagedResult<TransitionPlanSummaryDto>>> GetPlansAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<TransitionPlanSummaryDto>>(
            $"api/transitions/plans?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Gets the full clinical plan view (Coordinator/Clinician; server audits every read).</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The clinical plan view.</returns>
    public Task<ApiResponse<TransitionPlanClinicalDto>> GetPlanAsync(
        Guid planId,
        CancellationToken cancellationToken = default) =>
        GetAsync<TransitionPlanClinicalDto>($"api/transitions/plans/{planId}", cancellationToken);

    /// <summary>Gets the patient/family view of a plan (Client role: self or grantee).</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The patient-facing plan view (approved instructions only).</returns>
    public Task<ApiResponse<TransitionPlanPatientFacingDto>> GetPatientViewAsync(
        Guid planId,
        CancellationToken cancellationToken = default) =>
        GetAsync<TransitionPlanPatientFacingDto>(
            $"api/transitions/plans/{planId}/patient-view", cancellationToken);

    /// <summary>Records a clinician review decision for one instruction (Clinician).</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="instructionId">Instruction identifier.</param>
    /// <param name="request">The review action. PHI — never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated clinical instruction view.</returns>
    public Task<ApiResponse<TransitionInstructionClinicalDto>> ReviewInstructionAsync(
        Guid planId,
        Guid instructionId,
        ReviewInstructionRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<ReviewInstructionRequest, TransitionInstructionClinicalDto>(
            $"api/transitions/plans/{planId}/instructions/{instructionId}", request, cancellationToken);

    /// <summary>E-signs and activates a plan (Clinician; server enforces D-S5-5 preconditions).</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="request">The activation payload.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The activated clinical plan view.</returns>
    public Task<ApiResponse<TransitionPlanClinicalDto>> ActivatePlanAsync(
        Guid planId,
        ActivatePlanRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ActivatePlanRequest, TransitionPlanClinicalDto>(
            $"api/transitions/plans/{planId}/activate", request, cancellationToken);

    /// <summary>Schedules a reminder record (Coordinator; guarded per D-S5-6 server-side).</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="request">The reminder request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created reminder record.</returns>
    public Task<ApiResponse<TransitionReminderDto>> ScheduleReminderAsync(
        Guid planId,
        ScheduleReminderRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ScheduleReminderRequest, TransitionReminderDto>(
            $"api/transitions/plans/{planId}/reminders", request, cancellationToken);

    /// <summary>Submits a symptom check-in (Client role: self or grantee). Request contains PHI — never logged client-side.</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="request">The check-in submission.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The acknowledgement view (no responses echoed).</returns>
    public Task<ApiResponse<TransitionCheckInDto>> SubmitCheckInAsync(
        Guid planId,
        CreateCheckInRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateCheckInRequest, TransitionCheckInDto>(
            $"api/transitions/plans/{planId}/check-ins", request, cancellationToken);

    /// <summary>Gets a plan's escalation history (Coordinator).</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The escalation records.</returns>
    public Task<ApiResponse<IReadOnlyList<TransitionEscalationDto>>> GetEscalationsAsync(
        Guid planId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<TransitionEscalationDto>>(
            $"api/transitions/plans/{planId}/escalations", cancellationToken);

    /// <summary>Acknowledges an escalation with a documented human decision (Coordinator, D-S5-7).</summary>
    /// <param name="escalationId">Escalation identifier.</param>
    /// <param name="request">The acknowledgement. PHI — never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated escalation record.</returns>
    public Task<ApiResponse<TransitionEscalationDto>> AcknowledgeEscalationAsync(
        Guid escalationId,
        AcknowledgeEscalationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<AcknowledgeEscalationRequest, TransitionEscalationDto>(
            $"api/transitions/escalations/{escalationId}/acknowledge", request, cancellationToken);

    /// <summary>Gets a client's plan in the clinical shape (Coordinator/Clinician callers).</summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The clinical plan view.</returns>
    public Task<ApiResponse<TransitionPlanClinicalDto>> GetPlanForClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        GetAsync<TransitionPlanClinicalDto>(
            $"api/transitions/plans/client/{clientId}", cancellationToken);

    /// <summary>
    /// Gets a client's plan in the care-team-safe shape (assigned Caregiver callers — the
    /// server shapes the response by role on this route; requires a current Scheduled or
    /// InProgress shift for the client).
    /// </summary>
    /// <param name="clientId">Client identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The care-team-safe plan view.</returns>
    public Task<ApiResponse<TransitionPlanCareTeamDto>> GetCareTeamPlanForClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default) =>
        GetAsync<TransitionPlanCareTeamDto>(
            $"api/transitions/plans/client/{clientId}", cancellationToken);
}
