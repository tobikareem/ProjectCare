using CarePath.Domain.Entities.Platform;
using System.Data;

namespace CarePath.Domain.Interfaces.Repositories;

/// <summary>
/// Unit of work over the control-plane database (organizations, domains, branding,
/// memberships, data-plane locators). Physically separate from <see cref="IUnitOfWork"/>, which
/// owns the operational (PHI) database; no transaction spans both.
/// </summary>
/// <remarks>
/// <see cref="PlatformAuditEvent"/> is deliberately absent: it is append-only and written through
/// the platform audit logger abstraction, never through a generic repository.
/// </remarks>
public interface IControlPlaneUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>Repository for <see cref="Organization"/> records.</summary>
    IRepository<Organization> Organizations { get; }

    /// <summary>Repository for <see cref="OrganizationDomain"/> records.</summary>
    IRepository<OrganizationDomain> Domains { get; }

    /// <summary>Repository for <see cref="OrganizationBranding"/> records.</summary>
    IRepository<OrganizationBranding> Brandings { get; }

    /// <summary>Repository for <see cref="OrganizationMembership"/> records.</summary>
    IRepository<OrganizationMembership> Memberships { get; }

    /// <summary>Repository for <see cref="OrganizationDataPlane"/> records.</summary>
    IRepository<OrganizationDataPlane> DataPlanes { get; }

    /// <summary>Persists all tracked changes to the control-plane database.</summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a control-plane transaction with the same
    /// retry and rollback semantics as <see cref="IUnitOfWork.ExecuteInTransactionAsync(Func{CancellationToken, Task}, CancellationToken)"/>.
    /// </summary>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a control-plane transaction at the requested
    /// isolation level.
    /// </summary>
    /// <param name="isolationLevel">Database isolation level for the transaction.</param>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task ExecuteInTransactionAsync(IsolationLevel isolationLevel, Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a control-plane transaction and returns its result.
    /// </summary>
    /// <typeparam name="TResult">Type of the value produced by the operation.</typeparam>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a control-plane transaction at the requested
    /// isolation level and returns its result.
    /// </summary>
    /// <typeparam name="TResult">Type of the value produced by the operation.</typeparam>
    /// <param name="isolationLevel">Database isolation level for the transaction.</param>
    /// <param name="operation">Transactional work; receives the token to pass to inner calls.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task<TResult> ExecuteInTransactionAsync<TResult>(IsolationLevel isolationLevel, Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);
}
