using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for <see cref="TransitionPlan"/> persistence operations.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface ITransitionPlanRepository : IRepository<TransitionPlan>
{
    /// <summary>
    /// Retrieves the active transition plan for a given client, if one exists.
    /// A client may only have one plan in <see cref="TransitionPlanStatus.Active"/> status at a time.
    /// </summary>
    /// <param name="clientId">The client's unique identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The client's active <see cref="TransitionPlan"/>, or <c>null</c> if none exists.
    /// </returns>
    Task<TransitionPlan?> GetActiveByClientIdAsync(
        Guid clientId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all transition plans currently in <see cref="TransitionPlanStatus.Active"/> status
    /// whose <see cref="TransitionPlan.TransitionWindowEnd"/> has not yet passed.
    /// Used to populate the care coordinator dashboard.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A read-only list of all currently active transition plans.</returns>
    Task<IReadOnlyList<TransitionPlan>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a transition plan with all its child collections eagerly loaded
    /// (instructions, reminders, check-ins, escalations).
    /// Used when rendering the full plan detail view.
    /// </summary>
    /// <param name="planId">The plan's unique identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The <see cref="TransitionPlan"/> with all collections loaded,
    /// or <c>null</c> if not found.
    /// </returns>
    Task<TransitionPlan?> GetWithDetailsAsync(
        Guid planId,
        CancellationToken cancellationToken = default);
}
