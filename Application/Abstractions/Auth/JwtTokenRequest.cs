namespace CarePath.Application.Abstractions.Auth;

/// <summary>Inputs for issuing an access token. Every token is bound to one organization or to the platform.</summary>
/// <param name="UserId">Platform user id (<c>sub</c>).</param>
/// <param name="Email">Login email.</param>
/// <param name="Roles">Exactly one tenant role, or <c>PlatformAdmin</c>.</param>
/// <param name="CorrelationId">Request correlation id.</param>
/// <param name="TokenKind"><see cref="CarePathClaimTypes.TenantTokenKind"/> or <see cref="CarePathClaimTypes.PlatformTokenKind"/>.</param>
/// <param name="OrganizationId">Organization for tenant tokens; <c>null</c> for platform tokens.</param>
/// <param name="MembershipId">Membership for tenant tokens; <c>null</c> for platform tokens.</param>
public sealed record JwtTokenRequest(
    Guid UserId,
    string Email,
    IReadOnlySet<string> Roles,
    string? CorrelationId,
    string TokenKind,
    Guid? OrganizationId,
    Guid? MembershipId);
