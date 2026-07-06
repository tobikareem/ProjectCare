using CarePath.Application.Abstractions.Storage;
using Microsoft.Extensions.Configuration;

namespace CarePath.Infrastructure.Storage;

/// <summary>
/// Explicitly enabled private local file storage for opaque visit-photo objects.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string rootPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileStorageService"/> class.
    /// </summary>
    public LocalFileStorageService(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("Storage:EnableLocalPrivateStorage"))
        {
            throw new InvalidOperationException("Local private storage is disabled.");
        }

        rootPath = configuration["Storage:LocalPrivateRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "private-storage");
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(FileStorageWriteRequest request, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(rootPath);
        var extension = Path.GetExtension(request.FileName);
        var objectId = $"{Guid.NewGuid():N}{extension}";
        var targetPath = ResolveObjectPath(objectId);
        await using var output = File.Create(targetPath);
        await request.Content.CopyToAsync(output, cancellationToken);
        return objectId;
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
        _ = cancellationToken;
        var path = ResolveObjectPath(objectId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolveObjectPath(string objectId)
    {
        var fileName = Path.GetFileName(objectId);
        return Path.Combine(rootPath, fileName);
    }
}
