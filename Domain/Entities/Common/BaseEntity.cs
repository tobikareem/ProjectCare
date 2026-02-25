namespace CarePath.Domain.Entities.Common;

/// <summary>
/// Abstract base class providing common audit fields and soft delete support
/// for all domain entities. Inheriting from this class ensures every entity
/// has a globally unique identifier, full HIPAA audit trail, and complies
/// with Maryland's 6-year medical record retention requirement.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// Globally unique identifier. Initialised to a new GUID on construction,
    /// so entities have a valid ID before they are persisted.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when the entity was first created. Defaults to the
    /// current UTC time at construction so the value is always populated.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the entity was last modified.
    /// <c>null</c> indicates the entity has never been updated since creation.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Identity of the user (or system process) that created the entity.
    /// Used for HIPAA audit trail. Typically stores the authenticated user's
    /// sub/email from the JWT token.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Identity of the user (or system process) that last modified the entity.
    /// Used for HIPAA audit trail. <c>null</c> if the entity has never been updated.
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Soft-delete flag. When <c>true</c> the entity is logically deleted and
    /// must be excluded from all standard queries, but the record is preserved
    /// in the database to satisfy the 6-year Maryland medical records retention
    /// requirement and HIPAA audit trail obligations.
    /// </summary>
    public bool IsDeleted { get; set; }
}
