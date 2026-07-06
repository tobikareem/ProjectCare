using CarePath.Client.Http;
using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/shifts</c> including check-in/out and nested visit notes.
/// GPS coordinates flow client → server only; no method returns coordinates.
/// </summary>
public sealed class ShiftsClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public ShiftsClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets a page of shifts (scoped server-side per role).</summary>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged shift list.</returns>
    public Task<ApiResponse<PagedResult<ShiftSummaryDto>>> GetPageAsync(
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<ShiftSummaryDto>>($"api/shifts?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Gets a shift by ID.</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The shift detail.</returns>
    public Task<ApiResponse<ShiftDetailDto>> GetAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default) =>
        GetAsync<ShiftDetailDto>($"api/shifts/{shiftId}", cancellationToken);

    /// <summary>Schedules a shift (Admin/Coordinator; passes the D-S4-4 guards server-side).</summary>
    /// <param name="request">The create request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created shift detail.</returns>
    public Task<ApiResponse<ShiftDetailDto>> CreateAsync(
        CreateShiftRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateShiftRequest, ShiftDetailDto>("api/shifts", request, cancellationToken);

    /// <summary>Updates/reschedules a shift (Admin/Coordinator; guards re-run server-side).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated shift detail.</returns>
    public Task<ApiResponse<ShiftDetailDto>> UpdateAsync(
        Guid shiftId,
        UpdateShiftRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UpdateShiftRequest, ShiftDetailDto>($"api/shifts/{shiftId}", request, cancellationToken);

    /// <summary>Checks in to a shift (assigned caregiver only).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="request">The check-in payload (GPS inbound only).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated shift detail.</returns>
    public Task<ApiResponse<ShiftDetailDto>> CheckInAsync(
        Guid shiftId,
        CheckInRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CheckInRequest, ShiftDetailDto>($"api/shifts/{shiftId}/check-in", request, cancellationToken);

    /// <summary>Checks out of a shift (assigned caregiver only).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="request">The check-out payload (GPS inbound only).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated shift detail.</returns>
    public Task<ApiResponse<ShiftDetailDto>> CheckOutAsync(
        Guid shiftId,
        CheckOutRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CheckOutRequest, ShiftDetailDto>($"api/shifts/{shiftId}/check-out", request, cancellationToken);

    /// <summary>Gets a page of a shift's visit notes (summaries — no clinical text, D-S4-7).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="paging">Paging parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The paged visit note summaries.</returns>
    public Task<ApiResponse<PagedResult<VisitNoteSummaryDto>>> GetVisitNotesAsync(
        Guid shiftId,
        PagedRequest paging,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResult<VisitNoteSummaryDto>>(
            $"api/shifts/{shiftId}/visit-notes?{paging.ToQueryString()}", cancellationToken);

    /// <summary>Submits a visit note for a shift (assigned caregiver only).</summary>
    /// <param name="shiftId">Shift identifier.</param>
    /// <param name="request">The visit note. Never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created visit note detail.</returns>
    public Task<ApiResponse<VisitNoteDetailDto>> CreateVisitNoteAsync(
        Guid shiftId,
        CreateVisitNoteRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CreateVisitNoteRequest, VisitNoteDetailDto>(
            $"api/shifts/{shiftId}/visit-notes", request, cancellationToken);
}
