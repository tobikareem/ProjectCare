namespace CarePath.Contracts.Enumerations;

/// <summary>What a session token is bound to.</summary>
public enum AuthTokenKind
{
    /// <summary>Bound to exactly one organization; carries one tenant role.</summary>
    Tenant = 1,

    /// <summary>Platform-operator session issued at the platform host; carries no tenant role.</summary>
    Platform = 2
}
