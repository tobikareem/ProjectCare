namespace CarePath.Domain.Enumerations;

/// <summary>
/// Actions recorded in the append-only, non-PHI platform audit log.
/// </summary>
public enum PlatformAuditAction
{
    /// <summary>A platform administrator registered a new organization.</summary>
    OrganizationRegistered = 1,

    /// <summary>Organization names, time zone, or region changed.</summary>
    OrganizationUpdated = 2,

    /// <summary>Organization suspended.</summary>
    OrganizationSuspended = 3,

    /// <summary>Organization reactivated.</summary>
    OrganizationReactivated = 4,

    /// <summary>A host name was added to an organization.</summary>
    DomainAdded = 5,

    /// <summary>A host name was removed from an organization.</summary>
    DomainRemoved = 6,

    /// <summary>Branding fields changed.</summary>
    BrandingUpdated = 7,

    /// <summary>A membership was created.</summary>
    MembershipCreated = 8,

    /// <summary>A membership's role changed.</summary>
    MembershipRoleChanged = 9,

    /// <summary>A membership was deactivated.</summary>
    MembershipDeactivated = 10,

    /// <summary>A membership was reactivated.</summary>
    MembershipReactivated = 11,

    /// <summary>The first platform administrator was created from configuration.</summary>
    PlatformAdminBootstrapped = 12,

    /// <summary>Valid credentials presented at a host where the user holds no membership.</summary>
    LoginDeniedNoMembership = 13,

    /// <summary>Valid credentials presented at a host where the user's membership is inactive.</summary>
    LoginDeniedInactiveMembership = 14,

    /// <summary>Valid credentials presented at the platform host by a non-administrator.</summary>
    LoginDeniedNotPlatformAdmin = 15,

    /// <summary>A request arrived at a host that does not resolve to an admissible organization (host hash only).</summary>
    HostResolutionDenied = 16,

    /// <summary>A revoked refresh token was presented; the token family was revoked.</summary>
    RefreshTokenReuseDetected = 17,

    /// <summary>Startup backfill created the default organization.</summary>
    BackfillOrganizationCreated = 18,

    /// <summary>Startup backfill created memberships for existing tenant users.</summary>
    BackfillMembershipsCreated = 19
}
