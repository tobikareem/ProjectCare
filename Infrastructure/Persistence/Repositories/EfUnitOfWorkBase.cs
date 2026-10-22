using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace CarePath.Infrastructure.Persistence.Repositories;

/// <summary>
/// Shared transaction and disposal mechanics for the EF Core units of work. Each derived type
/// exposes the repositories of exactly one <see cref="DbContext"/>; no transaction ever spans two.
/// </summary>
public abstract class EfUnitOfWorkBase : IDisposable, IAsyncDisposable
{
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    /// <summary>Initializes the base over the context whose transactions it manages.</summary>
    /// <param name="context">Context this unit of work owns.</param>
    protected EfUnitOfWorkBase(DbContext context)
    {
        Context = context;
    }

    /// <summary>The owned context.</summary>
    protected DbContext Context { get; }

    /// <summary>Persists all tracked changes.</summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries written.</returns>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await Context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Runs <paramref name="operation"/> in a read-committed transaction.</summary>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync(IsolationLevel.ReadCommitted, operation, cancellationToken);
    }

    /// <summary>Runs <paramref name="operation"/> in a transaction at the requested isolation level.</summary>
    /// <param name="isolationLevel">Database isolation level for the transaction.</param>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
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

    /// <summary>Runs <paramref name="operation"/> in a read-committed transaction and returns its result.</summary>
    /// <typeparam name="TResult">Type of the value produced by the operation.</typeparam>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        return ExecuteInTransactionAsync(IsolationLevel.ReadCommitted, operation, cancellationToken);
    }

    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction at the requested isolation level and
    /// returns its result. The delegate may run more than once under the retrying execution
    /// strategy; tracked state is cleared between attempts so retries re-read database truth.
    /// </summary>
    /// <typeparam name="TResult">Type of the value produced by the operation.</typeparam>
    /// <param name="isolationLevel">Database isolation level for the transaction.</param>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <exception cref="InvalidOperationException">A transaction is already active on this instance.</exception>
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

        var strategy = Context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            operation,
            async (op, token) =>
            {
                _currentTransaction = await Context.Database.BeginTransactionAsync(isolationLevel, token);
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
                    Context.ChangeTracker.Clear();
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
        Context.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
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

        await Context.DisposeAsync();
        _disposed = true;
        GC.SuppressFinalize(this);
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
