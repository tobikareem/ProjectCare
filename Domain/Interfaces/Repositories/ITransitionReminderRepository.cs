using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for <see cref="TransitionReminder"/> persistence operations.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface ITransitionReminderRepository : IRepository<TransitionReminder>
{
    /// <summary>
    /// Retrieves all reminders for a given transition plan, ordered by scheduled time ascending.
    /// </summary>
    /// <param name="planId">The transition plan's unique identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A read-only list of reminders for the plan, earliest first.</returns>
    Task<IReadOnlyList<TransitionReminder>> GetByPlanIdAsync(
        Guid planId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all reminders in <see cref="ReminderStatus.Scheduled"/> status
    /// whose <see cref="TransitionReminder.ScheduledAt"/> is at or before <paramref name="asOf"/>.
    /// Used by the reminder dispatch background service to find reminders ready to send.
    /// </summary>
    /// <param name="asOf">The reference time — typically <c>DateTime.UtcNow</c>.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A read-only list of reminders ready for dispatch.</returns>
    Task<IReadOnlyList<TransitionReminder>> GetDueAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all reminders in <see cref="ReminderStatus.Scheduled"/> status
    /// whose <see cref="TransitionReminder.ScheduledAt"/> has passed.
    /// Used by the escalation evaluator to identify overdue reminders that were never sent.
    /// </summary>
    /// <param name="asOf">The reference time — typically <c>DateTime.UtcNow</c>.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A read-only list of overdue undelivered reminders.</returns>
    Task<IReadOnlyList<TransitionReminder>> GetOverdueAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default);
}
