namespace CarePath.Application.Abstractions.Tenancy;

/// <summary>
/// Per-request organization context established by host resolution before authentication.
/// Authentication rejects any token whose organization does not match this context.
/// </summary>
public interface IOrganizationContext
{
    /// <summary>How the request host resolved.</summary>
    OrganizationResolutionKind Kind { get; }

    /// <summary>Resolved organization id; <c>null</c> unless <see cref="Kind"/> is <see cref="OrganizationResolutionKind.Tenant"/>.</summary>
    Guid? OrganizationId { get; }

    /// <summary>Resolved organization slug; <c>null</c> unless resolved to a tenant.</summary>
    string? Slug { get; }

    /// <summary>Resolved organization display name; <c>null</c> unless resolved to a tenant.</summary>
    string? DisplayName { get; }
}
