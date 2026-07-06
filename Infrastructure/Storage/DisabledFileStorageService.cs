using CarePath.Application.Abstractions.Storage;

namespace CarePath.Infrastructure.Storage;

/// <summary>
/// Fail-closed storage implementation used until an approved private storage provider is enabled.
/// </summary>
public sealed class DisabledFileStorageService : IFileStorageService
{
    /// <inheritdoc />
    public Task<string> SaveAsync(FileStorageWriteRequest request, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("File storage is not configured.");
    }

    /// <inheritdoc />
    public Task<Uri?> CreateReadUrlAsync(string objectId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        _ = objectId;
        _ = timeToLive;
        _ = cancellationToken;
        return Task.FromResult<Uri?>(null);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string objectId, CancellationToken cancellationToken = default)
    {
        _ = objectId;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
