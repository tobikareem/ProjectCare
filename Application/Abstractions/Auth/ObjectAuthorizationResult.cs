namespace CarePath.Application.Abstractions.Auth;

public sealed record ObjectAuthorizationResult(
    bool IsAuthorized,
    string? DenialCode = null)
{
    public static ObjectAuthorizationResult Authorized() => new(true);

    public static ObjectAuthorizationResult Denied(string denialCode) => new(false, denialCode);
}