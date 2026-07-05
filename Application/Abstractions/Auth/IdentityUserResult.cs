namespace CarePath.Application.Abstractions.Auth;

public sealed record IdentityUserResult(
    bool Succeeded,
    Guid? UserId,
    string? Email,
    IReadOnlySet<string> Roles,
    string? FailureCode)
{
    public static IdentityUserResult Failed(string failureCode) =>
        new(false, null, null, new HashSet<string>(StringComparer.Ordinal), failureCode);
}