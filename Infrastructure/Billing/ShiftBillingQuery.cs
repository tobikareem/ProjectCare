using CarePath.Application.Abstractions.Billing;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Billing;

/// <summary>EF Core query service for completed, billable shift billing projections.</summary>
public sealed class ShiftBillingQuery : IShiftBillingQuery
{
    private readonly CarePathDbContext context;

    /// <summary>Initializes a new query service over the CarePath DbContext.</summary>
    /// <param name="context">CarePath database context.</param>
    public ShiftBillingQuery(CarePathDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Shift>> GetCompletedBillableShiftsAsync(
        Guid clientId,
        ServiceType serviceType,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        return await Query(periodStartUtc, periodEndUtc)
            .Where(shift => shift.ClientId == clientId && shift.ServiceType == serviceType)
            .OrderBy(shift => shift.ScheduledStartTime)
            .ThenBy(shift => shift.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Shift>> GetCompletedBillableShiftsAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default)
    {
        return await Query(periodStartUtc, periodEndUtc)
            .OrderBy(shift => shift.ScheduledStartTime)
            .ThenBy(shift => shift.Id)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Shift> Items, int TotalCount)> GetCompletedBillableShiftPageAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than zero.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than zero.");
        }

        var query = Query(periodStartUtc, periodEndUtc)
            .OrderBy(shift => shift.ScheduledStartTime)
            .ThenBy(shift => shift.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private IQueryable<Shift> Query(DateTime periodStartUtc, DateTime periodEndUtc)
    {
        return context.Shifts.Where(shift =>
            shift.Status == ShiftStatus.Completed
            && shift.ScheduledStartTime >= periodStartUtc
            && shift.ScheduledStartTime < periodEndUtc
            && shift.ActualStartTime.HasValue
            && shift.ActualEndTime.HasValue
            && EF.Functions.DateDiffMinute(shift.ActualStartTime.Value, shift.ActualEndTime.Value) > shift.BreakMinutes);
    }
}
