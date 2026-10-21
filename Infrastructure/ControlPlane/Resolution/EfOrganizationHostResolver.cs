using CarePath.Application.Abstractions.Tenancy;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.ControlPlane.Resolution;

/// <summary>
/// Resolves a normalized host with one projected control-plane query per call. Reads status and
/// provisioning state authoritatively every time; nothing security-relevant is cached (D-CP04-03).
/// </summary>
public sealed class EfOrganizationHostResolver : IOrganizationHostResolver
{
    private readonly ControlPlaneDbContext _context;

    /// <summary>Initializes the resolver.</summary>
    /// <param name="context">Control-plane context.</param>
    public EfOrganizationHostResolver(ControlPlaneDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<HostResolution> ResolveAsync(string normalizedHost, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedHost);

        // Soft-deleted domains and organizations are filtered out by the global query filter, so a
        // removed host reads as unknown. The join is by primary key on a table of tens of rows.
        var match = await _context.OrganizationDomains
            .AsNoTracking()
            .Where(domain => domain.HostName == normalizedHost)
            .Select(domain => new
            {
                domain.VerificationStatus,
                domain.OrganizationId,
                domain.Organization!.Slug,
                domain.Organization.DisplayName,
                domain.Organization.Status,
                domain.Organization.ProvisioningState
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (match is null)
        {
            return HostResolution.Denied(HostDenialReason.UnknownHost);
        }

        if (match.VerificationStatus != DomainVerificationStatus.Verified)
        {
            return HostResolution.Denied(HostDenialReason.UnverifiedDomain);
        }

        if (match.Status != OrganizationStatus.Active || match.ProvisioningState != ProvisioningState.Provisioned)
        {
            return HostResolution.Denied(HostDenialReason.OrganizationNotAdmissible);
        }

        return HostResolution.Resolved(new ResolvedOrganization(match.OrganizationId, match.Slug, match.DisplayName));
    }
}
