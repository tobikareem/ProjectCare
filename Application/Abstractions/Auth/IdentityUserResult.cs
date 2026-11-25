namespace CarePath.Application.Abstractions.Auth;

/// <summary>Outcome of a credential check or user read. Never carries the password or any PHI.</summary>
/// <param name="Succeeded">True when the user exists and, for credential checks, the password matched.</param>
/// <param name="UserId">Platform user id (equals the tenant <c>User.Id</c> by construction).</param>
/// <param name="Email">Login email.</param>
/// <param name="DisplayName">Display name for headers; never patient data.</param>
/// <param name="IsPlatformAdmin">True for CarePath operators.</param>
/// <param name="FailureCode">Non-PHI failure code when <paramref name="Succeeded"/> is false.</param>
public sealed record IdentityUserResult(
    bool Succeeded,
    Guid? UserId,
    string? Email,
    string? DisplayName,
    bool IsPlatformAdmin,
    string? FailureCode)
{
    /// <summary>A failed result with the given code.</summary>
    public static IdentityUserResult Failed(string failureCode) =>
        new(false, null, null, null, false, failureCode);
}
