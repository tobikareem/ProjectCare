namespace CarePath.Application.Abstractions.Auth;

public sealed record JwtTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string TokenType = "Bearer");