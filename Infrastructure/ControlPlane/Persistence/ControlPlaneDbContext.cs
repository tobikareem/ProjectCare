using CarePath.Domain.Entities.Platform;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence.Configurations;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.ControlPlane.Persistence;

/// <summary>
/// EF Core context for the control-plane database: platform identity, organizations, host
/// mapping, branding, memberships, data-plane locators, refresh tokens, and the append-only
/// platform audit log. Physically separate from <see cref="CarePathDbContext"/>; holds no PHI.
/// </summary>
/// <remarks>
/// Derives from <see cref="IdentityUserContext{TUser, TKey}"/> rather than
/// <c>IdentityDbContext</c>: the control plane has no Identity roles (D-CP04-04).
/// </remarks>
public class ControlPlaneDbContext : IdentityUserContext<PlatformUser, Guid>
{
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;

    /// <summary>Initializes a new control-plane context.</summary>
    /// <param name="options">EF Core DbContext options.</param>
    /// <param name="auditableEntityInterceptor">Audit-field interceptor shared with the tenant context.</param>
    public ControlPlaneDbContext(
        DbContextOptions<ControlPlaneDbContext> options,
        AuditableEntityInterceptor auditableEntityInterceptor)
        : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
    }

    /// <summary>Customer organizations.</summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>Host names mapped to organizations.</summary>
    public DbSet<OrganizationDomain> OrganizationDomains => Set<OrganizationDomain>();

    /// <summary>White-label branding per organization.</summary>
    public DbSet<OrganizationBranding> OrganizationBrandings => Set<OrganizationBranding>();

    /// <summary>Authoritative role records.</summary>
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    /// <summary>Data-plane locator records.</summary>
    public DbSet<OrganizationDataPlane> OrganizationDataPlanes => Set<OrganizationDataPlane>();

    /// <summary>Append-only platform audit log.</summary>
    public DbSet<PlatformAuditEvent> PlatformAuditEvents => Set<PlatformAuditEvent>();

    /// <summary>Server-tracked refresh tokens.</summary>
    public DbSet<PlatformRefreshToken> PlatformRefreshTokens => Set<PlatformRefreshToken>();

    /// <inheritdoc />
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectAuditMutations();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc />
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectAuditMutations();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Explicit list: an assembly scan would also pick up the tenant configurations.
        var dialect = SqlDialect.For(Database.ProviderName);
        builder.ApplyConfiguration(new PlatformUserConfiguration());
        builder.ApplyConfiguration(new OrganizationConfiguration());
        builder.ApplyConfiguration(new OrganizationDomainConfiguration(dialect));
        builder.ApplyConfiguration(new OrganizationBrandingConfiguration());
        builder.ApplyConfiguration(new OrganizationMembershipConfiguration(dialect));
        builder.ApplyConfiguration(new OrganizationDataPlaneConfiguration());
        builder.ApplyConfiguration(new PlatformAuditEventConfiguration());
        builder.ApplyConfiguration(new PlatformRefreshTokenConfiguration());

        BaseEntityModelConventions.Apply(builder);

        // Not a BaseEntity, but its timestamps must still round-trip as UTC.
        var refreshTokenType = builder.Model.FindEntityType(typeof(PlatformRefreshToken));
        if (refreshTokenType is not null)
        {
            BaseEntityModelConventions.ApplyUtcDateTimeConverters(refreshTokenType);
        }
    }

    // The audit log is append-only in code, not only by convention: any tracked audit row that is
    // not a plain insert fails the save before it reaches the database.
    private void RejectAuditMutations()
    {
        var mutated = ChangeTracker.Entries<PlatformAuditEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (mutated)
        {
            throw new InvalidOperationException("PlatformAuditEvent rows are append-only and cannot be updated or deleted.");
        }
    }
}
