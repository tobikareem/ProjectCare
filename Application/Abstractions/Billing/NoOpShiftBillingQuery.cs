using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;

namespace CarePath.Application.Abstractions.Billing;

public sealed class NoOpShiftBillingQuery : IShiftBillingQuery
{
    public Task<IReadOnlyList<Shift>> GetCompletedBillableShiftsAsync(
        Guid clientId,
        ServiceType serviceType,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task<IReadOnlyList<Shift>> GetCompletedBillableShiftsAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default) => throw NotConfigured();

    public Task<(IReadOnlyList<Shift> Items, int TotalCount)> GetCompletedBillableShiftPageAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default) => throw NotConfigured();

    private static InvalidOperationException NotConfigured() => new("Shift billing query is not configured.");
}
