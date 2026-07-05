using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Clients;

/// <summary>
/// Client row for lists and dashboards. Minimum necessary: carries age (not date of birth)
/// and NO clinical fields, insurance identifiers, or location data.
/// </summary>
public class ClientSummaryDto
{
    /// <summary>Client identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Linked domain user identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>Display name ("First Last").</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>In-home care or facility staffing.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Age in years, computed server-side. Date of birth is not exposed on summaries.</summary>
    public int Age { get; init; }

    /// <summary>False when the client's user account is deactivated.</summary>
    public bool IsActive { get; init; }
}
