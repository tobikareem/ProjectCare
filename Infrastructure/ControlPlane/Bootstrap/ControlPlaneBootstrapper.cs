using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Platform;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarePath.Infrastructure.ControlPlane.Bootstrap;

/// <summary>
/// Idempotent startup steps that make the existing deployment tenant one (CP-04 FR-020).
/// Every step is safe to re-run on each startup and never guesses: missing configuration on an
/// empty control plane fails startup with the configuration key in the message.
/// </summary>
public sealed class ControlPlaneBootstrapper
{
    private const string DefaultOrganizationKey = "ControlPlane:DefaultOrganization";

    private readonly ControlPlaneDbContext _context;
    private readonly ControlPlaneOptions _options;
    private readonly IPlatformAuditLogger _auditLogger;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ControlPlaneBootstrapper> _logger;

    /// <summary>Initializes the bootstrapper.</summary>
    /// <param name="context">Control-plane context.</param>
    /// <param name="options">Control-plane options.</param>
    /// <param name="auditLogger">Platform audit logger.</param>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="logger">Logger; emits slugs and counts only.</param>
    public ControlPlaneBootstrapper(
        ControlPlaneDbContext context,
        IOptions<ControlPlaneOptions> options,
        IPlatformAuditLogger auditLogger,
        TimeProvider timeProvider,
        ILogger<ControlPlaneBootstrapper> logger)
    {
        _context = context;
        _options = options.Value;
        _auditLogger = auditLogger;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Ensures the default organization exists as Active and Provisioned with its verified host
    /// names, default branding, and data-plane locator. A second run finds it by slug and changes
    /// nothing. On an empty control plane the <c>ControlPlane:DefaultOrganization</c> section is
    /// required; on a populated one its absence is a no-op.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The default organization's id, or <c>null</c> when no default is configured and the control plane already has organizations.</returns>
    /// <exception cref="InvalidOperationException">The control plane is empty and the section is missing or invalid.</exception>
    public async Task<Guid?> EnsureDefaultOrganizationAsync(CancellationToken cancellationToken = default)
    {
        var defaults = _options.DefaultOrganization;
        var anyOrganization = await _context.Organizations.AnyAsync(cancellationToken);

        if (defaults is null)
        {
            if (!anyOrganization)
            {
                throw new InvalidOperationException(
                    $"The control plane is empty and '{DefaultOrganizationKey}' is not configured. The backfill never guesses the default organization.");
            }

            return null;
        }

        if (!OrganizationSlug.IsValid(defaults.Slug))
        {
            throw new InvalidOperationException($"'{DefaultOrganizationKey}:Slug' is not a valid, non-reserved slug.");
        }

        if (string.IsNullOrWhiteSpace(defaults.DisplayName))
        {
            throw new InvalidOperationException($"'{DefaultOrganizationKey}:DisplayName' must be set.");
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(defaults.DefaultTimeZone, out _))
        {
            throw new InvalidOperationException($"'{DefaultOrganizationKey}:DefaultTimeZone' is not a known IANA time zone id.");
        }

        var hosts = NormalizeHosts(defaults.HostNames);

        // IgnoreQueryFilters: a soft-deleted organization still owns the unique slug, and the
        // backfill must say so instead of failing on a unique-index violation.
        var existing = await _context.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(organization => organization.Slug == defaults.Slug, cancellationToken);
        if (existing is not null)
        {
            if (existing.IsDeleted)
            {
                throw new InvalidOperationException(
                    $"'{DefaultOrganizationKey}:Slug' names an organization that was removed; choose another slug or restore it.");
            }

            _logger.LogInformation("Default organization {Slug} already present; backfill left it unchanged.", existing.Slug);
            return existing.Id;
        }

        var takenHostCount = await _context.OrganizationDomains
            .CountAsync(domain => hosts.Contains(domain.HostName), cancellationToken);
        if (takenHostCount > 0)
        {
            throw new InvalidOperationException(
                $"'{DefaultOrganizationKey}:HostNames' includes a host already registered to another organization.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var organization = new Organization
        {
            LegalName = string.IsNullOrWhiteSpace(defaults.LegalName) ? defaults.DisplayName : defaults.LegalName,
            DisplayName = defaults.DisplayName,
            Slug = defaults.Slug,
            Status = OrganizationStatus.Active,
            ProvisioningState = ProvisioningState.Provisioned,
            DefaultTimeZone = defaults.DefaultTimeZone,
            DataRegion = defaults.DataRegion
        };

        _context.Organizations.Add(organization);
        for (var index = 0; index < hosts.Count; index++)
        {
            _context.OrganizationDomains.Add(new OrganizationDomain
            {
                OrganizationId = organization.Id,
                HostName = hosts[index],
                IsPrimary = index == 0,
                VerificationStatus = DomainVerificationStatus.Verified
            });
        }

        _context.OrganizationBrandings.Add(new OrganizationBranding { OrganizationId = organization.Id });
        _context.OrganizationDataPlanes.Add(new OrganizationDataPlane
        {
            OrganizationId = organization.Id,
            Provider = defaults.DataPlane.Provider,
            SecretReference = defaults.DataPlane.SecretReference,
            RegisteredAtUtc = now
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two instances started together; the other one won the unique slug. Re-read and continue.
            _context.ChangeTracker.Clear();
            var winner = await _context.Organizations
                .FirstOrDefaultAsync(candidate => candidate.Slug == defaults.Slug, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            _logger.LogInformation("Default organization {Slug} was created concurrently by another instance.", winner.Slug);
            return winner.Id;
        }

        await _auditLogger.LogAsync(
            new PlatformAuditEntry(
                ActorPlatformUserId: null,
                OrganizationId: organization.Id,
                PlatformAuditAction.BackfillOrganizationCreated,
                EntityType: nameof(Organization),
                EntityId: organization.Id,
                CorrelationId: null),
            cancellationToken);

        _logger.LogInformation("Backfill created default organization {Slug} with {HostCount} host(s).", organization.Slug, hosts.Count);
        return organization.Id;
    }

    private static List<string> NormalizeHosts(string[] configured)
    {
        if (configured.Length == 0)
        {
            throw new InvalidOperationException($"'{DefaultOrganizationKey}:HostNames' must list at least one host name.");
        }

        var hosts = new List<string>();
        foreach (var raw in configured)
        {
            var normalized = HostNameNormalizer.Normalize(raw)
                ?? throw new InvalidOperationException($"'{DefaultOrganizationKey}:HostNames' contains an entry that is not a valid host name.");
            if (!hosts.Contains(normalized, StringComparer.Ordinal))
            {
                hosts.Add(normalized);
            }
        }

        return hosts;
    }
}
