using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Transitions;
using System.Data;

namespace CarePath.Domain.Interfaces.Repositories;

/// <summary>
/// Unit of work interface that groups all entity repositories under a single transactional boundary.
/// Implementations live in the Infrastructure layer (EF Core <c>DbContext</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Usage pattern:</b> Resolve <see cref="IUnitOfWork"/> from the DI container (scoped lifetime),
/// perform operations through the repository properties, then call
/// <see cref="SaveChangesAsync"/> once to persist all changes atomically.
/// </para>
/// <para>
/// <b>Transactions:</b> For multi-aggregate operations that must be atomic (e.g., creating a
/// <see cref="Shift"/> and updating a <see cref="Caregiver"/> counter in the same round-trip),
/// pass the work as a delegate to <c>ExecuteInTransactionAsync</c>. The implementation runs the
/// delegate through the configured retrying execution strategy inside a transaction, committing
/// on success and rolling back when the delegate throws. Only one transaction may be active per
/// unit-of-work instance at a time.
/// </para>
/// <para>
/// <b>Disposal:</b> Implement both <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>
/// to release the underlying <c>DbContext</c> on both synchronous and asynchronous code paths.
/// Prefer <c>await using</c> in manual-resolution scenarios. The DI container manages lifetime
/// automatically for scoped registrations.
/// </para>
/// </remarks>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    // â”€â”€ Identity Repositories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Repository for <see cref="User"/> entities.</summary>
    IRepository<User> Users { get; }

    /// <summary>Repository for <see cref="Caregiver"/> profiles.</summary>
    IRepository<Caregiver> Caregivers { get; }

    /// <summary>Repository for <see cref="CaregiverCertification"/> records.</summary>
    IRepository<CaregiverCertification> CaregiverCertifications { get; }

    /// <summary>Repository for <see cref="Client"/> profiles.</summary>
    IRepository<Client> Clients { get; }

    /// <summary>Repository for <see cref="ClientAccessGrant"/> records.</summary>
    IRepository<ClientAccessGrant> ClientAccessGrants { get; }

    // â”€â”€ Clinical Repositories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Repository for <see cref="CarePlan"/> documents.</summary>
    IRepository<CarePlan> CarePlans { get; }

    // â”€â”€ Scheduling Repositories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Repository for <see cref="Shift"/> entities.</summary>
    IRepository<Shift> Shifts { get; }

    /// <summary>Repository for <see cref="VisitNote"/> entities.</summary>
    IRepository<VisitNote> VisitNotes { get; }

    /// <summary>Repository for <see cref="VisitPhoto"/> entities.</summary>
    IRepository<VisitPhoto> VisitPhotos { get; }

    // â”€â”€ Billing Repositories â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Repository for <see cref="Invoice"/> entities.</summary>
    IRepository<Invoice> Invoices { get; }

    /// <summary>Repository for <see cref="InvoiceLineItem"/> entities.</summary>
    IRepository<InvoiceLineItem> InvoiceLineItems { get; }

    /// <summary>Repository for <see cref="Payment"/> entities.</summary>
    IRepository<Payment> Payments { get; }

    /// <summary>Repository for <see cref="DischargeDocument"/> entities.</summary>
    IRepository<DischargeDocument> DischargeDocuments { get; }

    /// <summary>Repository for <see cref="TransitionPlan"/> entities.</summary>
    IRepository<TransitionPlan> TransitionPlans { get; }

    /// <summary>Repository for <see cref="TransitionInstruction"/> entities.</summary>
    IRepository<TransitionInstruction> TransitionInstructions { get; }

    /// <summary>Repository for <see cref="TransitionReminder"/> entities.</summary>
    IRepository<TransitionReminder> TransitionReminders { get; }

    /// <summary>Repository for <see cref="TransitionCheckIn"/> entities.</summary>
    IRepository<TransitionCheckIn> TransitionCheckIns { get; }

    /// <summary>Repository for <see cref="TransitionEscalation"/> entities.</summary>
    IRepository<TransitionEscalation> TransitionEscalations { get; }

    // â”€â”€ Persistence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Persists all changes tracked across every repository in this unit of work to the database.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // â”€â”€ Transaction Management â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Executes <paramref name="operation"/> inside a database transaction that is compatible
    /// with the configured retrying execution strategy, committing on success and rolling back
    /// and rethrowing when the operation throws.
    /// </summary>
    /// <remarks>
    /// The delegate may be invoked more than once when the provider retries after a transient
    /// failure. Between attempts the implementation rolls back and resets tracked state, so
    /// reads inside the delegate always observe database truth on a retry — never a previous
    /// attempt's in-memory mutations. Entities constructed or fetched outside the delegate are
    /// detached by that reset; re-adding them replays cleanly on their client-generated keys or
    /// fails with a key conflict, never silently no-ops. The wrapper never calls
    /// <see cref="SaveChangesAsync"/> implicitly — the operation must persist its own changes
    /// before returning.
    /// </remarks>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the implementation if a transaction is already active on this unit-of-work instance.
    /// </exception>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a database transaction at the requested
    /// isolation level. Use <see cref="IsolationLevel.Serializable"/> when enforcing aggregate
    /// invariants that depend on counts or uniqueness checks. See
    /// <see cref="ExecuteInTransactionAsync(Func{CancellationToken, Task}, CancellationToken)"/>
    /// for retry semantics.
    /// </summary>
    /// <param name="isolationLevel">Database isolation level for the transaction.</param>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the implementation if a transaction is already active on this unit-of-work instance.
    /// </exception>
    Task ExecuteInTransactionAsync(IsolationLevel isolationLevel, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a database transaction and returns its
    /// result. Commits on success; rolls back and rethrows on failure. See
    /// <see cref="ExecuteInTransactionAsync(Func{CancellationToken, Task}, CancellationToken)"/>
    /// for retry semantics.
    /// </summary>
    /// <typeparam name="TResult">Type of the value produced by the operation.</typeparam>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the implementation if a transaction is already active on this unit-of-work instance.
    /// </exception>
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a database transaction at the requested
    /// isolation level and returns its result. See
    /// <see cref="ExecuteInTransactionAsync(Func{CancellationToken, Task}, CancellationToken)"/>
    /// for retry semantics.
    /// </summary>
    /// <typeparam name="TResult">Type of the value produced by the operation.</typeparam>
    /// <param name="isolationLevel">Database isolation level for the transaction.</param>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the implementation if a transaction is already active on this unit-of-work instance.
    /// </exception>
    Task<TResult> ExecuteInTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);
}
