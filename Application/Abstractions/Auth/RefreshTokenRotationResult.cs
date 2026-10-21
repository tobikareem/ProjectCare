namespace CarePath.Application.Abstractions.Auth;

/// <summary>Outcome of a refresh-token rotation.</summary>
/// <param name="Succeeded">True when a new token was issued.</param>
/// <param name="User">The token's user when successful.</param>
/// <param name="RefreshToken">The newly issued opaque refresh token.</param>
/// <param name="MembershipId">Membership the token family is bound to; <c>null</c> for platform sessions.</param>
/// <param name="OrganizationId">Organization the token family is bound to; <c>null</c> for platform sessions.</param>
/// <param name="FailureCode">Non-PHI failure code when unsuccessful.</param>
public sealed record RefreshTokenRotationResult(
    bool Succeeded,
    IdentityUserResult? User,
    string? RefreshToken,
    Guid? MembershipId,
    Guid? OrganizationId,
    string? FailureCode)
{
    /// <summary>A failed result with the given code.</summary>
    public static RefreshTokenRotationResult Failed(string failureCode) =>
        new(false, null, null, null, null, failureCode);
}
