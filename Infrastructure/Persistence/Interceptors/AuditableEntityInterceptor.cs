using System.Security.Claims;
using CarePath.Domain.Entities.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CarePath.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Populates audit fields for domain entities during EF Core save operations.
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private const string SystemActor = "System";
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Initializes a new audit interceptor.</summary>
    /// <param name="httpContextAccessor">Accessor for the current HTTP user context.</param>
    public AuditableEntityInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var actor = GetCurrentActor();
        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            ApplyAuditFields(entry, actor, now);
        }
    }

    private static void ApplyAuditFields(EntityEntry<BaseEntity> entry, string actor, DateTime now)
    {
        if (entry.State == EntityState.Added)
        {
            entry.Property(nameof(BaseEntity.CreatedAt)).CurrentValue = now;
            entry.Entity.CreatedBy = actor;
            return;
        }

        if (entry.State == EntityState.Modified)
        {
            entry.Entity.UpdatedAt = now;
            entry.Entity.UpdatedBy = actor;
        }
    }

    private string GetCurrentActor()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.FindFirstValue("sub")
            ?? user?.Identity?.Name
            ?? SystemActor;
    }
}
