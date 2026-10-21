namespace CarePath.Application.Abstractions.Auth;

/// <summary>
/// Credential validation and refresh-token lifecycle against the control-plane identity store.
/// Carries no organization or role knowledge: membership checks are the login service's job.
/// </summary>
public interface IIdentityService
{
    /// <summary>Validates an email/password pair with lockout semantics.</summary>
    Task<IdentityUserResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a platform user by id.</summary>
    Task<IdentityUserResult> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Issues a refresh token bound to a user and, for tenant sessions, one membership and organization.</summary>
    /// <param name="userId">Platform user id.</param>
    /// <param name="membershipId">Membership for tenant sessions; <c>null</c> for platform sessions.</param>
    /// <param name="organizationId">Organization for tenant sessions; <c>null</c> for platform sessions.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task<string> IssueRefreshTokenAsync(
        Guid userId,
        Guid? membershipId,
        Guid? organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates a refresh token presented at the given host binding. Rejects expired, revoked,
    /// wrong-organization, and wrong-kind tokens; reuse of a revoked token revokes the family.
    /// </summary>
    Task<RefreshTokenRotationResult> RotateRefreshTokenAsync(
        string refreshToken,
        RefreshTokenBinding expectedBinding,
        CancellationToken cancellationToken = default);
}
