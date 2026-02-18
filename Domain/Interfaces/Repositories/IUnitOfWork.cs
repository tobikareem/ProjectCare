using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;

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
/// wrap the operations with <see cref="BeginTransactionAsync"/> /
/// <see cref="CommitTransactionAsync"/> / <see cref="RollbackTransactionAsync"/>.
/// Only one transaction may be active per unit-of-work instance at a time.
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
    // ── Identity Repositories ─────────────────────────────────────────────────

    /// <summary>Repository for <see cref="User"/> entities.</summary>
    IRepository<User> Users { get; }

    /// <summary>Repository for <see cref="Caregiver"/> profiles.</summary>
    IRepository<Caregiver> Caregivers { get; }

    /// <summary>Repository for <see cref="CaregiverCertification"/> records.</summary>
    IRepository<CaregiverCertification> CaregiverCertifications { get; }

    /// <summary>Repository for <see cref="Client"/> profiles.</summary>
    IRepository<Client> Clients { get; }

    // ── Clinical Repositories ─────────────────────────────────────────────────

    /// <summary>Repository for <see cref="CarePlan"/> documents.</summary>
    IRepository<CarePlan> CarePlans { get; }

    // ── Scheduling Repositories ───────────────────────────────────────────────

    /// <summary>Repository for <see cref="Shift"/> entities.</summary>
    IRepository<Shift> Shifts { get; }

    /// <summary>Repository for <see cref="VisitNote"/> entities.</summary>
    IRepository<VisitNote> VisitNotes { get; }

    /// <summary>Repository for <see cref="VisitPhoto"/> entities.</summary>
    IRepository<VisitPhoto> VisitPhotos { get; }

    // ── Billing Repositories ──────────────────────────────────────────────────

    /// <summary>Repository for <see cref="Invoice"/> entities.</summary>
    IRepository<Invoice> Invoices { get; }

    /// <summary>Repository for <see cref="InvoiceLineItem"/> entities.</summary>
    IRepository<InvoiceLineItem> InvoiceLineItems { get; }

    /// <summary>Repository for <see cref="Payment"/> entities.</summary>
    IRepository<Payment> Payments { get; }

    // ── Persistence ───────────────────────────────────────────────────────────

    /// <summary>
    /// Persists all changes tracked across every repository in this unit of work to the database.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ── Transaction Management ────────────────────────────────────────────────

    /// <summary>
    /// Begins a new explicit database transaction. Use when multiple
    /// <see cref="SaveChangesAsync"/> calls must be atomic across aggregates.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the implementation if a transaction is already active on this unit-of-work instance.
    /// </exception>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction, making all changes permanent.
    /// Must be preceded by a call to <see cref="BeginTransactionAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the implementation if no active transaction exists when this method is called.
    /// </exception>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction, discarding all uncommitted changes.
    /// Call this in a <c>catch</c> block when an error occurs after
    /// <see cref="BeginTransactionAsync"/> has been called.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown by the implementation if no active transaction exists when this method is called.
    /// </exception>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
