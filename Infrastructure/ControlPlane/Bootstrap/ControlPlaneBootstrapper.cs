using System.Data.Common;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Platform;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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
    private const string PlatformAdminKey = "ControlPlane:PlatformAdmin";
    private const string LegacyIdentityTable = "AspNetUsers";

    private readonly ControlPlaneDbContext _context;
    private readonly CarePathDbContext _tenant;
    private readonly UserManager<PlatformUser> _userManager;
    private readonly ControlPlaneOptions _options;
    private readonly IPlatformAuditLogger _auditLogger;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ControlPlaneBootstrapper> _logger;

    /// <summary>Initializes the bootstrapper.</summary>
    /// <param name="context">Control-plane context.</param>
    /// <param name="tenant">Tenant (operational) context of the existing deployment.</param>
    /// <param name="userManager">Platform identity manager, used only for the platform administrator bootstrap.</param>
    /// <param name="options">Control-plane options.</param>
    /// <param name="auditLogger">Platform audit logger.</param>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="logger">Logger; emits slugs and counts only.</param>
    public ControlPlaneBootstrapper(
        ControlPlaneDbContext context,
        CarePathDbContext tenant,
        UserManager<PlatformUser> userManager,
        IOptions<ControlPlaneOptions> options,
        IPlatformAuditLogger auditLogger,
        TimeProvider timeProvider,
        ILogger<ControlPlaneBootstrapper> logger)
    {
        _context = context;
        _tenant = tenant;
        _userManager = userManager;
        _options = options.Value;
        _auditLogger = auditLogger;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Copies every row of the legacy tenant <c>AspNetUsers</c> table into the control plane
    /// verbatim (password hash, security and concurrency stamps, lockout state), so existing
    /// credentials keep working. No-op when the legacy table is absent or every row is present.
    /// Fails closed when a legacy row violates the platform-user/tenant-user id invariant.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The number of identities copied.</returns>
    public async Task<int> CopyLegacyIdentityAsync(CancellationToken cancellationToken = default)
    {
        if (!await _tenant.Database.CanConnectAsync(cancellationToken) || !await LegacyTableExistsAsync(cancellationToken))
        {
            return 0;
        }

        var legacyRows = await ReadLegacyRowsAsync(cancellationToken);
        if (legacyRows.Count == 0)
        {
            return 0;
        }

        var existingIds = (await _context.Users.Select(user => user.Id).ToListAsync(cancellationToken)).ToHashSet();
        var displayNames = await _tenant.DomainUsers
            .IgnoreQueryFilters()
            .Select(user => new { user.Id, user.FirstName, user.LastName })
            .ToDictionaryAsync(user => user.Id, user => $"{user.FirstName} {user.LastName}".Trim(), cancellationToken);

        var copied = 0;
        foreach (var row in legacyRows)
        {
            if (row.Id != row.DomainUserId)
            {
                throw new InvalidOperationException(
                    "A legacy identity row does not share its id with its tenant user; the backfill cannot continue (D-CP04-02).");
            }

            if (existingIds.Contains(row.Id))
            {
                continue;
            }

            _context.Users.Add(new PlatformUser
            {
                Id = row.Id,
                UserName = row.UserName,
                NormalizedUserName = row.NormalizedUserName,
                Email = row.Email,
                NormalizedEmail = row.NormalizedEmail,
                EmailConfirmed = row.EmailConfirmed,
                PasswordHash = row.PasswordHash,
                SecurityStamp = row.SecurityStamp,
                ConcurrencyStamp = row.ConcurrencyStamp,
                PhoneNumber = row.PhoneNumber,
                PhoneNumberConfirmed = row.PhoneNumberConfirmed,
                TwoFactorEnabled = row.TwoFactorEnabled,
                LockoutEnd = row.LockoutEnd,
                LockoutEnabled = row.LockoutEnabled,
                AccessFailedCount = row.AccessFailedCount,
                DisplayName = displayNames.TryGetValue(row.Id, out var name) && name.Length > 0 ? name : row.Email ?? string.Empty,
                IsPlatformAdmin = false
            });
            copied++;
        }

        if (copied > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Legacy identity copy: {Copied} identit(ies) copied, {Skipped} already present.", copied, legacyRows.Count - copied);
        return copied;
    }

    /// <summary>
    /// Creates one membership in <paramref name="organizationId"/> for every non-deleted tenant
    /// user that has a platform identity and no membership yet, using the user's current
    /// <c>User.Role</c> and <c>User.IsActive</c> (inactive users get an Inactive membership so an
    /// administrator can reactivate them later), and records <c>User.PlatformUserId</c>. Idempotent.
    /// </summary>
    /// <param name="organizationId">The default organization.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The number of memberships created.</returns>
    public async Task<int> EnsureMembershipsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (!await _tenant.Database.CanConnectAsync(cancellationToken))
        {
            return 0;
        }

        var users = await _tenant.DomainUsers.ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return 0;
        }

        var platformUserIds = (await _context.Users.Select(user => user.Id).ToListAsync(cancellationToken)).ToHashSet();
        var memberIds = (await _context.OrganizationMemberships
            .Where(membership => membership.OrganizationId == organizationId)
            .Select(membership => membership.PlatformUserId)
            .ToListAsync(cancellationToken)).ToHashSet();

        var created = 0;
        var withoutIdentity = 0;
        var tenantChanged = false;
        foreach (var user in users)
        {
            if (!platformUserIds.Contains(user.Id))
            {
                withoutIdentity++;
                continue;
            }

            if (user.PlatformUserId != user.Id)
            {
                user.PlatformUserId = user.Id;
                tenantChanged = true;
            }

            if (memberIds.Contains(user.Id))
            {
                continue;
            }

            _context.OrganizationMemberships.Add(new OrganizationMembership
            {
                OrganizationId = organizationId,
                PlatformUserId = user.Id,
                TenantUserId = user.Id,
                Role = user.Role,
                Status = user.IsActive ? MembershipStatus.Active : MembershipStatus.Inactive
            });
            created++;
        }

        if (created > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _auditLogger.LogAsync(
                new PlatformAuditEntry(null, organizationId, PlatformAuditAction.BackfillMembershipsCreated, nameof(OrganizationMembership), null, null),
                cancellationToken);
        }

        if (tenantChanged)
        {
            await _tenant.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Membership backfill: {Created} created, {WithoutIdentity} user(s) have no platform identity.", created, withoutIdentity);
        return created;
    }

    /// <summary>
    /// Creates the first platform administrator from <c>ControlPlane:PlatformAdmin</c> when none
    /// exists. The password must come from user secrets or environment; a missing password fails
    /// closed, and an existing non-administrator with the configured email is never elevated.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>True when an administrator was created.</returns>
    public async Task<bool> EnsurePlatformAdminAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Users.AnyAsync(user => user.IsPlatformAdmin, cancellationToken))
        {
            return false;
        }

        var admin = _options.PlatformAdmin;
        if (admin is null)
        {
            _logger.LogWarning("No platform administrator exists and '{Key}' is not configured; platform endpoints stay unusable.", PlatformAdminKey);
            return false;
        }

        if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
        {
            throw new InvalidOperationException(
                $"'{PlatformAdminKey}:Email' and '{PlatformAdminKey}:Password' must be set (password via user secrets or environment) to bootstrap the first platform administrator.");
        }

        var existing = await _userManager.FindByEmailAsync(admin.Email);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"'{PlatformAdminKey}:Email' already belongs to a non-administrator identity; the bootstrap never elevates an existing account. "
                + "Change the configured email to an unused address (or promote the account through a reviewed data change) before starting again.");
        }

        var user = new PlatformUser
        {
            UserName = admin.Email,
            Email = admin.Email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(admin.DisplayName) ? "Platform Operator" : admin.DisplayName,
            IsPlatformAdmin = true
        };

        var result = await _userManager.CreateAsync(user, admin.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"The platform administrator could not be created: {string.Join("; ", result.Errors.Select(error => error.Code))}.");
        }

        await _auditLogger.LogAsync(
            new PlatformAuditEntry(user.Id, null, PlatformAuditAction.PlatformAdminBootstrapped, nameof(PlatformUser), user.Id, null),
            cancellationToken);
        _logger.LogInformation("Platform administrator bootstrapped.");
        return true;
    }

    /// <summary>
    /// Fails startup when more than one Active organization exists and
    /// <see cref="ControlPlaneOptions.AllowMultipleActiveOrganizations"/> is false. Every request
    /// still reaches the single configured tenant database until CP-06 routes data planes, so a
    /// second Active organization would share PHI with the first (review finding C1). The
    /// platform API enforces the same rule on reactivation; this is the restart backstop.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">More than one Active organization exists.</exception>
    public async Task EnsureSingleOperationalOrganizationAsync(CancellationToken cancellationToken = default)
    {
        if (_options.AllowMultipleActiveOrganizations)
        {
            return;
        }

        var active = await _context.Organizations.CountAsync(organization => organization.Status == OrganizationStatus.Active, cancellationToken);
        if (active > 1)
        {
            throw new InvalidOperationException(
                $"{active} Active organizations exist but this release serves one operational database (CP-06 routing is not implemented). "
                + "Suspend all but the backfilled organization, or set ControlPlane:AllowMultipleActiveOrganizations=true only for synthetic data.");
        }
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

        var hosts = NormalizeHosts(defaults.HostNames, _options.PlatformHostNames);

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

    private static List<string> NormalizeHosts(string[] configured, string[] platformHostNames)
    {
        if (configured.Length == 0)
        {
            throw new InvalidOperationException($"'{DefaultOrganizationKey}:HostNames' must list at least one host name.");
        }

        // The middleware checks platform hosts first, so an organization host equal to one would
        // silently become the platform surface: refuse it at startup.
        var platformHosts = platformHostNames.Select(HostNameNormalizer.Normalize).OfType<string>().ToHashSet(StringComparer.Ordinal);

        var hosts = new List<string>();
        foreach (var raw in configured)
        {
            var normalized = HostNameNormalizer.Normalize(raw)
                ?? throw new InvalidOperationException($"'{DefaultOrganizationKey}:HostNames' contains an entry that is not a valid host name.");
            if (platformHosts.Contains(normalized))
            {
                throw new InvalidOperationException($"'{DefaultOrganizationKey}:HostNames' contains a host that is also listed in 'ControlPlane:PlatformHostNames'.");
            }

            if (!hosts.Contains(normalized, StringComparer.Ordinal))
            {
                hosts.Add(normalized);
            }
        }

        return hosts;
    }

    private async Task<bool> LegacyTableExistsAsync(CancellationToken cancellationToken)
    {
        var providerName = _tenant.Database.ProviderName ?? string.Empty;
        var connection = _tenant.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = providerName switch
        {
            _ when providerName.Contains("Sqlite", StringComparison.Ordinal) =>
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AspNetUsers'",
            _ when providerName.Contains("Npgsql", StringComparison.Ordinal) =>
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'AspNetUsers' AND table_schema = current_schema()",
            _ =>
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'AspNetUsers' AND table_schema = SCHEMA_NAME()"
        };

        var opened = await OpenIfNeededAsync(connection, cancellationToken);
        try
        {
            var count = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
            return count > 0;
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<List<LegacyIdentityRow>> ReadLegacyRowsAsync(CancellationToken cancellationToken)
    {
        var dialect = SqlDialect.For(_tenant.Database.ProviderName);
        var columns = new[]
        {
            "Id", "DomainUserId", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
            "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed",
            "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount"
        };

        var connection = _tenant.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {string.Join(", ", columns.Select(dialect.Quote))} FROM {dialect.Quote(LegacyIdentityTable)}";

        var opened = await OpenIfNeededAsync(connection, cancellationToken);
        var rows = new List<LegacyIdentityRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new LegacyIdentityRow(
                    ReadGuid(reader, 0),
                    ReadGuid(reader, 1),
                    ReadString(reader, 2),
                    ReadString(reader, 3),
                    ReadString(reader, 4),
                    ReadString(reader, 5),
                    ReadBool(reader, 6),
                    ReadString(reader, 7),
                    ReadString(reader, 8),
                    ReadString(reader, 9),
                    ReadString(reader, 10),
                    ReadBool(reader, 11),
                    ReadBool(reader, 12),
                    ReadDateTimeOffset(reader, 13),
                    ReadBool(reader, 14),
                ReadInt(reader, 15)));
            }
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }

        return rows;
    }

    private static async Task<bool> OpenIfNeededAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State == System.Data.ConnectionState.Open)
        {
            return false;
        }

        await connection.OpenAsync(cancellationToken);
        return true;
    }

    private static Guid ReadGuid(DbDataReader reader, int ordinal) =>
        reader.GetValue(ordinal) switch
        {
            Guid guid => guid,
            string text => Guid.Parse(text),
            byte[] bytes => new Guid(bytes),
            var other => throw new InvalidOperationException($"Unexpected legacy identity value type {other.GetType().Name}.")
        };

    private static string? ReadString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    private static bool ReadBool(DbDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    private static int ReadInt(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset? ReadDateTimeOffset(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : reader.GetValue(ordinal) switch
            {
                DateTimeOffset offset => offset,
                DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
                string text => DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture),
                var other => throw new InvalidOperationException($"Unexpected legacy lockout value type {other.GetType().Name}.")
            };

    private sealed record LegacyIdentityRow(
        Guid Id,
        Guid DomainUserId,
        string? UserName,
        string? NormalizedUserName,
        string? Email,
        string? NormalizedEmail,
        bool EmailConfirmed,
        string? PasswordHash,
        string? SecurityStamp,
        string? ConcurrencyStamp,
        string? PhoneNumber,
        bool PhoneNumberConfirmed,
        bool TwoFactorEnabled,
        DateTimeOffset? LockoutEnd,
        bool LockoutEnabled,
        int AccessFailedCount);
}
