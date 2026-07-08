using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Entities.Transitions;
using CarePath.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace CarePath.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core unit of work implementation for CarePath repositories and transaction boundaries.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CarePathDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    private IRepository<User>? _users;
    private IRepository<Caregiver>? _caregivers;
    private IRepository<CaregiverCertification>? _caregiverCertifications;
    private IRepository<Client>? _clients;
    private IRepository<ClientAccessGrant>? _clientAccessGrants;
    private IRepository<CarePlan>? _carePlans;
    private IRepository<Shift>? _shifts;
    private IRepository<VisitNote>? _visitNotes;
    private IRepository<VisitPhoto>? _visitPhotos;
    private IRepository<Invoice>? _invoices;
    private IRepository<InvoiceLineItem>? _invoiceLineItems;
    private IRepository<Payment>? _payments;
    private IRepository<DischargeDocument>? _dischargeDocuments;
    private IRepository<TransitionPlan>? _transitionPlans;
    private IRepository<TransitionInstruction>? _transitionInstructions;
    private IRepository<TransitionReminder>? _transitionReminders;
    private IRepository<TransitionCheckIn>? _transitionCheckIns;
    private IRepository<TransitionEscalation>? _transitionEscalations;

    /// <summary>Initializes a new unit of work over a CarePath database context.</summary>
    /// <param name="context">CarePath database context.</param>
    public UnitOfWork(CarePathDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public IRepository<User> Users => _users ??= new Repository<User>(_context);

    /// <inheritdoc />
    public IRepository<Caregiver> Caregivers => _caregivers ??= new Repository<Caregiver>(_context);

    /// <inheritdoc />
    public IRepository<CaregiverCertification> CaregiverCertifications =>
        _caregiverCertifications ??= new Repository<CaregiverCertification>(_context);

    /// <inheritdoc />
    public IRepository<Client> Clients => _clients ??= new Repository<Client>(_context);

    /// <inheritdoc />
    public IRepository<ClientAccessGrant> ClientAccessGrants =>
        _clientAccessGrants ??= new Repository<ClientAccessGrant>(_context);

    /// <inheritdoc />
    public IRepository<CarePlan> CarePlans => _carePlans ??= new Repository<CarePlan>(_context);

    /// <inheritdoc />
    public IRepository<Shift> Shifts => _shifts ??= new Repository<Shift>(_context);

    /// <inheritdoc />
    public IRepository<VisitNote> VisitNotes => _visitNotes ??= new Repository<VisitNote>(_context);

    /// <inheritdoc />
    public IRepository<VisitPhoto> VisitPhotos => _visitPhotos ??= new Repository<VisitPhoto>(_context);

    /// <inheritdoc />
    public IRepository<Invoice> Invoices => _invoices ??= new Repository<Invoice>(_context);

    /// <inheritdoc />
    public IRepository<InvoiceLineItem> InvoiceLineItems =>
        _invoiceLineItems ??= new Repository<InvoiceLineItem>(_context);

    /// <inheritdoc />
    public IRepository<Payment> Payments => _payments ??= new Repository<Payment>(_context);

    /// <inheritdoc />
    public IRepository<DischargeDocument> DischargeDocuments =>
        _dischargeDocuments ??= new Repository<DischargeDocument>(_context);

    /// <inheritdoc />
    public IRepository<TransitionPlan> TransitionPlans =>
        _transitionPlans ??= new Repository<TransitionPlan>(_context);

    /// <inheritdoc />
    public IRepository<TransitionInstruction> TransitionInstructions =>
        _transitionInstructions ??= new Repository<TransitionInstruction>(_context);

    /// <inheritdoc />
    public IRepository<TransitionReminder> TransitionReminders =>
        _transitionReminders ??= new Repository<TransitionReminder>(_context);

    /// <inheritdoc />
    public IRepository<TransitionCheckIn> TransitionCheckIns =>
        _transitionCheckIns ??= new Repository<TransitionCheckIn>(_context);

    /// <inheritdoc />
    public IRepository<TransitionEscalation> TransitionEscalations =>
        _transitionEscalations ??= new Repository<TransitionEscalation>(_context);

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync(IsolationLevel.ReadCommitted, operation, cancellationToken);
    }

    /// <inheritdoc />
    public Task ExecuteInTransactionAsync(
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return ExecuteInTransactionAsync<object?>(
            isolationLevel,
            async token =>
            {
                await operation(token);
                return null;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync(IsolationLevel.ReadCommitted, operation, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        IsolationLevel isolationLevel,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_currentTransaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active for this unit of work.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            operation,
            async (op, token) =>
            {
                _currentTransaction = await _context.Database.BeginTransactionAsync(isolationLevel, token);
                try
                {
                    var result = await op(token);
                    await _currentTransaction.CommitAsync(token);
                    return result;
                }
                catch
                {
                    try
                    {
                        await _currentTransaction.RollbackAsync(token);
                    }
                    catch
                    {
                        // A rollback can fail on the same broken connection that faulted the
                        // attempt; swallowing it keeps the original exception flowing to the
                        // execution strategy's transient classification.
                    }

                    // Reset tracked state so a retried delegate re-reads database truth instead
                    // of the previous attempt's mutated instances. Replayed inserts of entities
                    // built outside the delegate then fail cleanly on their client-generated
                    // keys rather than silently no-op or duplicate.
                    _context.ChangeTracker.Clear();
                    throw;
                }
                finally
                {
                    // Clears _currentTransaction between retry attempts so the next
                    // attempt can begin a fresh transaction and the nested-call guard re-arms.
                    await DisposeCurrentTransactionAsync();
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _currentTransaction?.Dispose();
        _context.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
        }

        await _context.DisposeAsync();
        _disposed = true;
    }

    private async Task DisposeCurrentTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
