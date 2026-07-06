using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;

namespace CarePath.Client.Api;

/// <summary>
/// Typed client for <c>/api/visit-notes</c>. Detail reads are audited server-side.
/// </summary>
public sealed class VisitNotesClient : ApiClientBase
{
    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    public VisitNotesClient(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>Gets a visit note by ID (clinical PHI — server audits every read).</summary>
    /// <param name="visitNoteId">Visit note identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The visit note detail.</returns>
    public Task<ApiResponse<VisitNoteDetailDto>> GetAsync(
        Guid visitNoteId,
        CancellationToken cancellationToken = default) =>
        GetAsync<VisitNoteDetailDto>($"api/visit-notes/{visitNoteId}", cancellationToken);

    /// <summary>
    /// Uploads a photo for a visit note (assigned caregiver only). Multipart form upload; the
    /// caller owns and disposes <paramref name="photoStream"/>.
    /// </summary>
    /// <param name="visitNoteId">Visit note identifier.</param>
    /// <param name="photoStream">Photo content stream.</param>
    /// <param name="fileName">Original file name (no PHI in file names).</param>
    /// <param name="contentType">MIME type (e.g., <c>image/jpeg</c>).</param>
    /// <param name="takenAtUtc">When the photo was taken (UTC).</param>
    /// <param name="caption">Optional caption. Never logged client-side.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created photo metadata.</returns>
    public async Task<ApiResponse<VisitPhotoDto>> AddPhotoAsync(
        Guid visitNoteId,
        Stream photoStream,
        string fileName,
        string contentType,
        DateTime takenAtUtc,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var fileContent = new StreamContent(photoStream);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent(takenAtUtc.ToString("O")), "takenAtUtc");

        if (!string.IsNullOrWhiteSpace(caption))
        {
            content.Add(new StringContent(caption), "caption");
        }

        return await PostMultipartAsync<VisitPhotoDto>(
            $"api/visit-notes/{visitNoteId}/photos", content, cancellationToken).ConfigureAwait(false);
    }
}
