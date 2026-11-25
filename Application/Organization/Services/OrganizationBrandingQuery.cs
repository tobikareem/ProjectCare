using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Organization;
using CarePath.Domain.Interfaces.Repositories;
using ContractColorToken = CarePath.Contracts.Enumerations.BrandingColorToken;

namespace CarePath.Application.Organization.Services;

/// <summary>
/// Builds the anonymous branding payload from the control plane, through the presentation-only
/// branding cache. Unknown, pending, and suspended hosts all surface as the same not-found.
/// </summary>
public sealed class OrganizationBrandingQuery : IOrganizationBrandingQuery
{
    private readonly IOrganizationContext _organizationContext;
    private readonly IControlPlaneUnitOfWork _controlPlane;
    private readonly IBrandingCache _cache;

    /// <summary>Initializes the query.</summary>
    /// <param name="organizationContext">Per-request organization context.</param>
    /// <param name="controlPlane">Control-plane unit of work.</param>
    /// <param name="cache">Presentation-only branding cache.</param>
    public OrganizationBrandingQuery(
        IOrganizationContext organizationContext,
        IControlPlaneUnitOfWork controlPlane,
        IBrandingCache cache)
    {
        _organizationContext = organizationContext;
        _controlPlane = controlPlane;
        _cache = cache;
    }

    /// <inheritdoc />
    public Task<OrganizationBrandingDto> GetForCurrentHostAsync(CancellationToken cancellationToken = default)
    {
        if (_organizationContext.Kind != OrganizationResolutionKind.Tenant || _organizationContext.OrganizationId is null)
        {
            throw new ResourceNotFoundException(isPhiResource: false);
        }

        var organizationId = _organizationContext.OrganizationId.Value;
        var displayName = _organizationContext.DisplayName ?? string.Empty;
        var slug = _organizationContext.Slug ?? string.Empty;

        return _cache.GetOrCreateAsync(organizationId, async token =>
        {
            var rows = await _controlPlane.Brandings.FindAsync(branding => branding.OrganizationId == organizationId, token);
            var branding = rows.FirstOrDefault();

            return new OrganizationBrandingDto
            {
                DisplayName = displayName,
                Slug = slug,
                PrimaryColorToken = branding is null ? ContractColorToken.Teal900 : (ContractColorToken)branding.PrimaryColorToken,
                AccentColorToken = branding is null ? ContractColorToken.Orange : (ContractColorToken)branding.AccentColorToken,
                SupportEmail = branding?.SupportEmail,
                SupportPhone = branding?.SupportPhone
            };
        }, cancellationToken);
    }
}
