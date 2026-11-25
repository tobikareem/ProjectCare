namespace CarePath.Application.Abstractions.Tenancy;

/// <summary>
/// Non-PHI identity of the organization a request host resolved to.
/// </summary>
/// <param name="OrganizationId">Organization identifier.</param>
/// <param name="Slug">Immutable organization slug.</param>
/// <param name="DisplayName">Display name for presentation only.</param>
public sealed record ResolvedOrganization(Guid OrganizationId, string Slug, string DisplayName);
