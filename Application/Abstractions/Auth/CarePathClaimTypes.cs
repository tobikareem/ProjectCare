namespace CarePath.Application.Abstractions.Auth;

/// <summary>Custom JWT claim names and values introduced by CP-04 (organization-bound tokens).</summary>
public static class CarePathClaimTypes
{
    /// <summary>Organization the token was issued for; absent on platform tokens.</summary>
    public const string OrganizationId = "organization_id";

    /// <summary>Membership the token was issued for; absent on platform tokens.</summary>
    public const string MembershipId = "membership_id";

    /// <summary>Token kind discriminator.</summary>
    public const string TokenKind = "token_kind";

    /// <summary><see cref="TokenKind"/> value for a token bound to one organization.</summary>
    public const string TenantTokenKind = "tenant";

    /// <summary><see cref="TokenKind"/> value for a platform-operator token issued at the platform host.</summary>
    public const string PlatformTokenKind = "platform";
}
