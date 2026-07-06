using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;

namespace CarePath.Application.Abstractions.Billing;

public interface IShiftBillingQuery
{
    Task<IReadOnlyList<Shift>> GetCompletedBillableShiftsAsync(
        Guid clientId,
        ServiceType serviceType,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Shift>> GetCompletedBillableShiftsAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Shift> Items, int TotalCount)> GetCompletedBillableShiftPageAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
