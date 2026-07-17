using CarePath.Application.Abstractions.Billing;
using CarePath.Domain.Entities.Billing;
using CarePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Billing;

/// <summary>
/// Append-only store for reconciliation resolutions (D-S6-18) over the shared scoped
/// <see cref="CarePathDbContext"/>, so appends participate in the caller's unit-of-work
/// transaction.
/// </summary>
public sealed class BillingReconciliationStore : IBillingReconciliationStore
{
    private readonly CarePathDbContext context;

    /// <summary>Creates the store over the CarePath DbContext.</summary>
    /// <param name="context">CarePath database context.</param>
    public BillingReconciliationStore(CarePathDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BillingReconciliationResolution>> GetHistoryAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        return await context.BillingReconciliationResolutions
            .Where(resolution => resolution.ShiftId == shiftId)
            .OrderByDescending(resolution => resolution.ResolvedAtUtc)
            .ThenByDescending(resolution => resolution.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BillingReconciliationResolution?> GetLatestAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        return await context.BillingReconciliationResolutions
            .Where(resolution => resolution.ShiftId == shiftId)
            .OrderByDescending(resolution => resolution.ResolvedAtUtc)
            .ThenByDescending(resolution => resolution.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AppendAsync(
        BillingReconciliationResolution resolution,
        CancellationToken cancellationToken = default)
    {
        await context.BillingReconciliationResolutions.AddAsync(resolution, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
