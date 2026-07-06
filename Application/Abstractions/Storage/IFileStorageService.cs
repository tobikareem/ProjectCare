namespace CarePath.Application.Abstractions.Storage;

public interface IFileStorageService
{
    Task<string> SaveAsync(FileStorageWriteRequest request, CancellationToken cancellationToken = default);

    Task<Uri?> CreateReadUrlAsync(string objectId, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectId, CancellationToken cancellationToken = default);
}

