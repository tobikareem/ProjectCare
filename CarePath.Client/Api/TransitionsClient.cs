using CarePath.Client.Http;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using CarePath.Contracts.Transitions;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/transitions</c>. Route shapes mirror the Sprint 5 endpoint matrix;
/// authorization, grant evaluation, and the D-S5 safety guards are all enforced server-side.
/// </summary>
/// <remarks>
/// AUDIENCE NOTE: methods returning clinical DTOs succeed for Admin/Coordinator/Clinician
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

    /// <summary>Submits discharge intake (Admin/Coordinator). Request contains PHI — never logged client-side.</summary>
    /// <param name="request">The intake request (metadata + approved raw text only).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created document status view.</returns>
    public Task<ApiResponse<DischargeDocumentDto>> CreateDischargeDocumentAsync(
        CreateDischargeDocumentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateDischargeDocumentRequest, DischargeDocumentDto>(
            "api/transitions/documents", request, cancellationToken);

    /// <summary>Gets document status/metadata (Admin/Coordinator/Clinician). No raw content.</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The document status view.</returns>
    public Task<ApiResponse<DischargeDocumentDto>> GetDischargeDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<DischargeDocumentDto>($"api/transitions/documents/{documentId}", cancellationToken);

    /// <summary>Gets raw discharge content for review (Admin/Coordinator/Clinician; server audits every read).</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The raw content view.</returns>
    public Task<ApiResponse<DischargeDocumentContentDto>> GetDischargeDocumentContentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<DischargeDocumentContentDto>(
            $"api/transitions/documents/{documentId}/content", cancellationToken);

    /// <summary>Triggers extraction for a document (Admin/Coordinator). Stub creates a draft plan.</summary>
    /// <param name="documentId">Document identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The response envelope (202 Accepted on success).</returns>
    public Task<ApiResponse> RequestExtractionAsync(
        Guid documentId,
        CancellationToken cancellationToken = default) =>
        PostAsync($"api/transitions/documents/{documentId}/extract", cancellationToken);

    /// <summary>Gets a page of discharge document status/metadata rows (Admin/Coordinator/Clinician) for the recent-uploads view. No raw content.</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged document status views.</returns>
    public Task<ApiResponse<PagedResult<DischargeDocumentDto>>> GetDischargeDocumentsAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<DischargeDocumentDto>>(
            $"api/transitions/documents?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Gets the coordinator dashboard page of plans (Admin/Coordinator/Clinician).</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="status">Optional plan status filter (e.g., PendingVerification for the review queue).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged plan summaries.</returns>
    public Task<ApiResponse<PagedResult<TransitionPlanSummaryDto>>> GetPlansAsync(
        PagedRequest paging,
        TransitionPlanStatus? status = null,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<TransitionPlanSummaryDto>>(
            status.HasValue
                ? $"api/transitions/plans?{paging.ToQueryString()}&status={status.Value}"
                : $"api/transitions/plans?{paging.ToQueryString()}",
            cancellationToken);

    /// <summary>Gets the full clinical plan view (Admin/Coordinator/Clinician; server audits every read).</summary>
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

    /// <summary>Records a clinician review decision for one instruction (Admin/Clinician).</summary>
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

    /// <summary>E-signs and activates a plan (Admin/Clinician; server enforces D-S5-5 preconditions).</summary>
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

    /// <summary>Schedules a reminder record (Admin/Coordinator; guarded per D-S5-6 server-side).</summary>
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

    /// <summary>Gets a plan's reminder records ordered by scheduled time (Admin/Coordinator/Clinician; server audits every read).</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The reminder records.</returns>
    public Task<ApiResponse<IReadOnlyList<TransitionReminderDto>>> GetRemindersAsync(
        Guid planId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<TransitionReminderDto>>(
            $"api/transitions/plans/{planId}/reminders", cancellationToken);

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

    /// <summary>
    /// Gets a plan's check-in history, most recent first (Admin/Coordinator/Clinician; server audits
    /// every read). The DTO carries the warning flag and review metadata only — submitted
    /// responses are never echoed (D-S5-3).
    /// </summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The check-in records.</returns>
    public Task<ApiResponse<IReadOnlyList<TransitionCheckInDto>>> GetCheckInsAsync(
        Guid planId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<TransitionCheckInDto>>(
            $"api/transitions/plans/{planId}/check-ins", cancellationToken);

    /// <summary>Gets a plan's escalation history (Admin/Coordinator).</summary>
    /// <param name="planId">Plan identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The escalation records.</returns>
    public Task<ApiResponse<IReadOnlyList<TransitionEscalationDto>>> GetEscalationsAsync(
        Guid planId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<TransitionEscalationDto>>(
            $"api/transitions/plans/{planId}/escalations", cancellationToken);

    /// <summary>Gets the coordinator escalation queue across transition plans (Admin/Coordinator).</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="openOnly">When true, returns only unacknowledged escalations.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged escalation queue.</returns>
    public Task<ApiResponse<PagedResult<TransitionEscalationDto>>> GetEscalationQueueAsync(
        PagedRequest paging,
        bool openOnly = true,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<TransitionEscalationDto>>(
            $"api/transitions/escalations?{paging.ToQueryString()}&openOnly={openOnly.ToString().ToLowerInvariant()}",
            cancellationToken);

    /// <summary>Acknowledges an escalation with a documented human decision (Admin/Coordinator, D-S5-7).</summary>
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

    /// <summary>Gets a client's plan in the clinical shape (Admin/Coordinator/Clinician callers).</summary>
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
