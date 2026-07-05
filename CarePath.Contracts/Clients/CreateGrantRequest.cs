using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Clients;

/// <summary>
/// Request to grant a Client-role user (family proxy) access to a client's records (D-S4-1).
/// The target client travels in the route; Admin/Coordinator only.
/// </summary>
public class CreateGrantRequest
{
    /// <summary>User (Client role) to receive access.</summary>
    public Guid GranteeUserId { get; init; }

    /// <summary>Scope of access to grant.</summary>
    public AccessScope Scope { get; init; }
}
