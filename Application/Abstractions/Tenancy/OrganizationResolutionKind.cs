namespace CarePath.Application.Abstractions.Tenancy;

/// <summary>
/// Outcome of resolving the request host before authentication.
/// </summary>
public enum OrganizationResolutionKind
{
    /// <summary>The host maps to no admissible organization and is not the platform host.</summary>
    Unresolved = 0,

    /// <summary>The host is a configured platform (operator) host; no organization is bound.</summary>
    Platform = 1,

    /// <summary>The host maps to an active, provisioned organization.</summary>
    Tenant = 2
}
