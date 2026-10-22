using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Platform;

/// <summary>
/// Locator record for an organization's operational database. Never holds a connection
/// string; <see cref="SecretReference"/> names a configuration or secret-manager key.
/// </summary>
/// <remarks>
/// <see cref="SchemaVersion"/> is an orchestrator-maintained admission projection; the
/// database's own migration history remains authoritative (ADR 0003 §8.4). CP-04 populates
/// this record only for the backfilled organization.
/// </remarks>
public class OrganizationDataPlane : BaseEntity
{
    /// <summary>Owning organization (unique).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Navigation to the owning organization.</summary>
    public Organization? Organization { get; set; }

    /// <summary>Database engine of the data plane.</summary>
    public DataPlaneProvider Provider { get; set; }

    /// <summary>
    /// Key that resolves to the connection string (max 200), e.g.
    /// <c>ConnectionStrings:SqlServerConnection</c>. Never a connection string itself.
    /// </summary>
    public string SecretReference { get; set; } = string.Empty;

    /// <summary>Admission projection of the applied schema version (max 64). <c>null</c> until CP-06.</summary>
    public string? SchemaVersion { get; set; }

    /// <summary>UTC timestamp when the data plane was registered.</summary>
    public DateTime RegisteredAtUtc { get; set; }
}
