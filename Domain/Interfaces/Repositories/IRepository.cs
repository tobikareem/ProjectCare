using System.Linq.Expressions;
using CarePath.Domain.Entities.Common;

namespace CarePath.Domain.Interfaces.Repositories;

/// <summary>
/// Generic repository interface defining standard CRUD and query operations for domain entities.
/// Implementations live in the Infrastructure layer and must never be referenced from the Domain layer.
/// </summary>
/// <typeparam name="T">
/// The entity type. Constrained to <see cref="BaseEntity"/> so that implementations can rely on
/// <c>Id</c>, <c>IsDeleted</c>, and other common audit fields without casting.
/// </typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>
    /// Retrieves a single entity by its unique identifier.
    /// </summary>
    /// <param name="id">The entity's <see cref="BaseEntity.Id"/>.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The matching entity, or <c>null</c> if the entity does not exist or has been soft-deleted.
    /// </returns>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all non-deleted entities of type <typeparamref name="T"/> as a fully materialized list.
    /// </summary>
    /// <remarks>
    /// This method loads all matching rows into memory. Do not call it on high-volume entities
    /// (e.g., <c>Shift</c>, <c>VisitNote</c>). Use <see cref="FindAsync"/> with a predicate,
    /// or a paged overload in the concrete repository for large collections.
    /// </remarks>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A read-only, fully materialized list of all non-deleted entities.</returns>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a fully materialized list of entities matching the given predicate expression.
    /// Only non-deleted records are searched; the Infrastructure implementation must apply an
    /// <c>IsDeleted == false</c> global query filter so that navigation-property queries are
    /// also filtered automatically.
    /// </summary>
    /// <param name="predicate">LINQ expression used to filter entities.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A read-only, fully materialized list of matching non-deleted entities.</returns>
    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the repository. The entity is not persisted until
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The tracked entity after being added to the change tracker.</returns>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an existing entity as modified. Changes are not persisted until
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    /// <param name="entity">The entity with updated property values.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes an entity by setting <see cref="BaseEntity.IsDeleted"/> to <c>true</c>.
    /// The record is preserved in the database for HIPAA audit and 6-year retention compliance.
    /// Changes are not persisted until <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </summary>
    /// <param name="entity">The entity to soft-delete.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether at least one non-deleted entity satisfies the predicate.
    /// Useful for uniqueness checks (e.g., duplicate email validation) without loading the entity.
    /// </summary>
    /// <param name="predicate">LINQ expression used to test entities.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if at least one matching non-deleted entity exists; otherwise <c>false</c>.</returns>
    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of non-deleted entities, optionally filtered by <paramref name="predicate"/>.
    /// </summary>
    /// <param name="predicate">Optional filter expression. <c>null</c> returns the total non-deleted count.</param>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>
    /// The count of matching non-deleted entities, or the total non-deleted entity count
    /// if <paramref name="predicate"/> is <c>null</c>.
    /// </returns>
    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);
}
