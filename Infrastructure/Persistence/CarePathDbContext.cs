using System.Linq.Expressions;
using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence.Converters;
using CarePath.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CarePath.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for CarePath persistence and ASP.NET Core Identity tables.
/// </summary>
public class CarePathDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private static readonly UtcDateTimeConverter UtcDateTimeConverter = new();
    private static readonly NullableUtcDateTimeConverter NullableUtcDateTimeConverter = new();
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;

    /// <summary>Initializes a new CarePath database context.</summary>
    /// <param name="options">EF Core DbContext options.</param>
    /// <param name="auditableEntityInterceptor">Audit-field interceptor.</param>
    public CarePathDbContext(
        DbContextOptions<CarePathDbContext> options,
        AuditableEntityInterceptor auditableEntityInterceptor)
        : base(options)
    {
        _auditableEntityInterceptor = auditableEntityInterceptor;
    }

    /// <summary>Domain users.</summary>
    public DbSet<User> DomainUsers => Set<User>();

    /// <summary>Caregiver profiles.</summary>
    public DbSet<Caregiver> Caregivers => Set<Caregiver>();

    /// <summary>Caregiver certifications.</summary>
    public DbSet<CaregiverCertification> CaregiverCertifications => Set<CaregiverCertification>();

    /// <summary>Client profiles.</summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>Care plans.</summary>
    public DbSet<CarePlan> CarePlans => Set<CarePlan>();

    /// <summary>Shifts.</summary>
    public DbSet<Shift> Shifts => Set<Shift>();

    /// <summary>Visit notes.</summary>
    public DbSet<VisitNote> VisitNotes => Set<VisitNote>();

    /// <summary>Visit photos.</summary>
    public DbSet<VisitPhoto> VisitPhotos => Set<VisitPhoto>();

    /// <summary>Invoices.</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>Invoice line items.</summary>
    public DbSet<InvoiceLineItem> InvoiceLineItems => Set<InvoiceLineItem>();

    /// <summary>Payments.</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    // CP-03 Transitions entities (DischargeDocument, TransitionPlan, etc.) are intentionally
    // not mapped here yet. They are out of CP-02 scope and have no entity configurations, so
    // mapping them now would emit an incomplete, HIPAA-unsafe schema (cascade deletes on PHI,
    // unbounded PHI columns). They will be added with their configurations during CP-03.

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

        builder.ApplyConfigurationsFromAssembly(typeof(CarePathDbContext).Assembly);
        ApplyBaseEntityConventions(builder);
    }

    private static void ApplyBaseEntityConventions(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(entityType => typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)))
        {
            ApplySoftDeleteQueryFilter(builder, entityType);
            ApplyUtcDateTimeConverters(entityType);
        }
    }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder builder, IMutableEntityType entityType)
    {
        var parameter = Expression.Parameter(entityType.ClrType, "entity");
        var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        var filter = Expression.Lambda(Expression.Equal(property, Expression.Constant(false)), parameter);

        builder.Entity(entityType.ClrType).HasQueryFilter(filter);
    }

    private static void ApplyUtcDateTimeConverters(IMutableEntityType entityType)
    {
        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType == typeof(DateTime))
            {
                property.SetValueConverter(UtcDateTimeConverter);
                property.SetColumnType("datetime2");
            }
            else if (property.ClrType == typeof(DateTime?))
            {
                property.SetValueConverter(NullableUtcDateTimeConverter);
                property.SetColumnType("datetime2");
            }
        }
    }
}
