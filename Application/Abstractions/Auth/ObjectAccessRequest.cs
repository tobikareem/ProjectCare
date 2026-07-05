namespace CarePath.Application.Abstractions.Auth;

public sealed record ObjectAccessRequest(
    ICurrentUserContext User,
    ProtectedResourceType ResourceType,
    Guid ResourceId,
    ObjectAccessAction Action,
    string? CorrelationId);