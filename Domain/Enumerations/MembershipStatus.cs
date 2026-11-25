namespace CarePath.Domain.Enumerations;

/// <summary>
/// Status of a platform user's membership in one organization. Inactive memberships refuse
/// login and refresh for that organization only.
/// </summary>
public enum MembershipStatus
{
    /// <summary>The user may log in at the organization's hosts.</summary>
    Active = 1,

    /// <summary>The user is refused at the organization's hosts; other memberships are unaffected.</summary>
    Inactive = 2
}
