using CarePath.Domain.Entities.Billing;

namespace CarePath.Application.Abstractions.Billing;

/// <summary>
/// Append-only persistence for <see cref="BillingReconciliationResolution"/> records
/// (D-S6-18). There is deliberately no update or delete surface — corrections append
/// superseding records and history is immutable.
/// </summary>
public interface IBillingReconciliationStore
{
    /// <summary>Full resolution history for a shift, newest decision first.</summary>
    Task<IReadOnlyList<BillingReconciliationResolution>> GetHistoryAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    /// <summary>The latest decision for a shift, or null when never resolved.</summary>
    Task<BillingReconciliationResolution?> GetLatestAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    /// <summary>Appends a new resolution record. Participates in the ambient transaction.</summary>
    Task AppendAsync(
        BillingReconciliationResolution resolution,
        CancellationToken cancellationToken = default);
}
