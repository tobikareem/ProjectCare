namespace CarePath.Application.Abstractions.Auth;

/// <summary>
/// The host context a refresh token is presented at. A token is valid only at the kind of host
/// it was issued for, and for tenant tokens only at the same organization.
/// </summary>
/// <param name="IsPlatform">True when presented at the platform host.</param>
/// <param name="OrganizationId">Resolved organization when presented at a tenant host.</param>
public sealed record RefreshTokenBinding(bool IsPlatform, Guid? OrganizationId)
{
    /// <summary>Binding for the platform host.</summary>
    public static RefreshTokenBinding Platform { get; } = new(true, null);

    /// <summary>Binding for a tenant host.</summary>
    public static RefreshTokenBinding ForOrganization(Guid organizationId) => new(false, organizationId);
}
