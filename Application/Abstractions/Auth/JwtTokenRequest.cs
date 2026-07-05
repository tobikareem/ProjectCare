namespace CarePath.Application.Abstractions.Auth;

public sealed record JwtTokenRequest(
    Guid UserId,
    string Email,
    IReadOnlySet<string> Roles,
    string? CorrelationId);