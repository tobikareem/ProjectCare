using CarePath.Application.Abstractions.Tenancy;

namespace CarePath.WebApi.Tenancy;

/// <summary>
/// Scoped holder for the request's organization resolution. Set exactly once by
/// <see cref="OrganizationResolutionMiddleware"/>; everything downstream reads it through
/// <see cref="IOrganizationContext"/>.
/// </summary>
public sealed class OrganizationContext : IOrganizationContext
{
    /// <inheritdoc />
    public OrganizationResolutionKind Kind { get; private set; } = OrganizationResolutionKind.Unresolved;

    /// <inheritdoc />
    public Guid? OrganizationId { get; private set; }

    /// <inheritdoc />
    public string? Slug { get; private set; }

    /// <inheritdoc />
    public string? DisplayName { get; private set; }

    /// <summary>Marks the request as a platform (operator) request.</summary>
    public void SetPlatform()
    {
        Kind = OrganizationResolutionKind.Platform;
        OrganizationId = null;
        Slug = null;
        DisplayName = null;
    }

    /// <summary>Binds the request to a resolved tenant.</summary>
    /// <param name="organization">Resolved organization.</param>
    public void SetTenant(ResolvedOrganization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);
        Kind = OrganizationResolutionKind.Tenant;
        OrganizationId = organization.OrganizationId;
        Slug = organization.Slug;
        DisplayName = organization.DisplayName;
    }
}
