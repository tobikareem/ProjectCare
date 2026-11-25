using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Infrastructure.Persistence.Repositories;

namespace CarePath.Infrastructure.ControlPlane.Persistence;

/// <summary>
/// EF Core unit of work over <see cref="ControlPlaneDbContext"/>. Exposes no repository for
/// <see cref="PlatformAuditEvent"/>: that table is append-only via the platform audit logger.
/// </summary>
public sealed class ControlPlaneUnitOfWork : EfUnitOfWorkBase, IControlPlaneUnitOfWork
{
    private readonly ControlPlaneDbContext _context;
    private IRepository<Organization>? _organizations;
    private IRepository<OrganizationDomain>? _domains;
    private IRepository<OrganizationBranding>? _brandings;
    private IRepository<OrganizationMembership>? _memberships;
    private IRepository<OrganizationDataPlane>? _dataPlanes;

    /// <summary>Initializes a new unit of work over the control-plane context.</summary>
    /// <param name="context">Control-plane database context.</param>
    public ControlPlaneUnitOfWork(ControlPlaneDbContext context)
        : base(context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public IRepository<Organization> Organizations => _organizations ??= new Repository<Organization>(_context);

    /// <inheritdoc />
    public IRepository<OrganizationDomain> Domains => _domains ??= new Repository<OrganizationDomain>(_context);

    /// <inheritdoc />
    public IRepository<OrganizationBranding> Brandings => _brandings ??= new Repository<OrganizationBranding>(_context);

    /// <inheritdoc />
    public IRepository<OrganizationMembership> Memberships => _memberships ??= new Repository<OrganizationMembership>(_context);

    /// <inheritdoc />
    public IRepository<OrganizationDataPlane> DataPlanes => _dataPlanes ??= new Repository<OrganizationDataPlane>(_context);
}
