using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;

namespace CarePath.Application.Scheduling.Services;

public interface IVisitDocumentationService
{
    Task<VisitNoteDetailDto> CreateVisitNoteAsync(Guid shiftId, CreateVisitNoteRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<VisitNoteSummaryDto>> GetVisitNotesAsync(Guid shiftId, PagedRequest request, CancellationToken cancellationToken = default);

    Task<VisitNoteDetailDto> GetVisitNoteAsync(Guid visitNoteId, CancellationToken cancellationToken = default);

    Task<VisitPhotoDto> AddVisitPhotoAsync(Guid visitNoteId, string fileName, string contentType, Stream content, string? caption, DateTime takenAtUtc, CancellationToken cancellationToken = default);
}
