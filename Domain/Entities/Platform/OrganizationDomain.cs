using CarePath.Domain.Entities.Common;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Platform;

/// <summary>
/// A host name mapped to an organization. <see cref="HostName"/> is stored normalized
/// (lower-case, no port, no trailing dot) and is unique across the platform.
/// </summary>
public class OrganizationDomain : BaseEntity
{
    /// <summary>Owning organization.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Navigation to the owning organization.</summary>
    public Organization? Organization { get; set; }

    /// <summary>Normalized host name (max 253, RFC 1123).</summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>True for the organization's canonical host.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Whether this host may resolve to the organization.</summary>
    public DomainVerificationStatus VerificationStatus { get; set; } = DomainVerificationStatus.Pending;

    /// <summary>True when the host resolves: verified and not soft-deleted.</summary>
    public bool IsResolvable => VerificationStatus == DomainVerificationStatus.Verified && !IsDeleted;
}
