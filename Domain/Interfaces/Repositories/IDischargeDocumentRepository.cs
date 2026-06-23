using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for <see cref="DischargeDocument"/> persistence operations.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface IDischargeDocumentRepository : IRepository<DischargeDocument>
{
    /// <summary>
    /// Retrieves all discharge documents for a given client, ordered by upload date descending.
    /// </summary>
    /// <param name="clientId">The client's unique identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A read-only list of the client's discharge documents, newest first.</returns>
    Task<IReadOnlyList<DischargeDocument>> GetByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all discharge documents currently in a given processing status.
    /// Used by the background extraction job to find documents awaiting AI processing.
    /// </summary>
    /// <param name="status">The processing status to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A read-only list of matching discharge documents.</returns>
    Task<IReadOnlyList<DischargeDocument>> GetByStatusAsync(
        DischargeDocumentStatus status,
        CancellationToken cancellationToken = default);
}
