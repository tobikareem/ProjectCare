namespace CarePath.Application.Abstractions.Storage;

public sealed record FileStorageWriteRequest(
    string FileName,
    string ContentType,
    Stream Content);
