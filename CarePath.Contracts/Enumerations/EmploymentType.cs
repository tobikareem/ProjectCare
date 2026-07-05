namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.EmploymentType</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum EmploymentType
{
    /// <summary>W-2 employee (in-home care).</summary>
    W2Employee = 1,

    /// <summary>1099 contractor (facility staffing).</summary>
    Contractor1099 = 2
}
