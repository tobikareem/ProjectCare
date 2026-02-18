namespace CarePath.Domain.Enumerations;

/// <summary>
/// Employment classification for caregivers.
/// Determines the margin target and service type a caregiver is eligible for.
/// </summary>
public enum EmploymentType
{
    /// <summary>
    /// W-2 employee providing in-home care.
    /// Target gross margin: 40–45 %.
    /// </summary>
    W2Employee = 1,

    /// <summary>
    /// 1099 independent contractor providing facility staffing.
    /// Target gross margin: 25–30 %.
    /// </summary>
    Contractor1099 = 2
}
