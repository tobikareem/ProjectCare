namespace CarePath.Domain.Enumerations;

/// <summary>
/// Scope of a client access grant issued to a Client-role user such as a family proxy.
/// </summary>
public enum AccessScope
{
    /// <summary>
    /// Allows only explicitly patient-facing read models. Operational staff fields,
    /// rates, margins, raw clinical text, and internal notes remain unavailable.
    /// </summary>
    PatientFacing = 1,

    /// <summary>
    /// Allows access to PHI-bearing client records on endpoints that explicitly
    /// permit Client-role users and still pass object-level authorization.
    /// </summary>
    Full = 2
}
