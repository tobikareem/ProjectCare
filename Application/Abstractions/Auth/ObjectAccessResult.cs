namespace CarePath.Application.Abstractions.Auth;

public sealed record ObjectAccessResult(
    bool IsAuthorized,
    bool ShouldRevealExistence,
    string? DenialCode)
{
    public static ObjectAccessResult Authorized() => new(true, true, null);

    public static ObjectAccessResult DeniedWithoutDisclosure(string denialCode) =>
        new(false, false, denialCode);
}