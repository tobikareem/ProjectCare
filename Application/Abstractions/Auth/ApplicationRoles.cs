namespace CarePath.Application.Abstractions.Auth;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Coordinator = "Coordinator";
    public const string Caregiver = "Caregiver";
    public const string Client = "Client";
    public const string FacilityManager = "FacilityManager";
    public const string Clinician = "Clinician";

    /// <summary>
    /// Control-plane operator role (CP-04). Issued only in platform tokens; deliberately absent
    /// from <see cref="All"/> so tenant role policies and the Contracts enum parity are unaffected.
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Admin,
        Coordinator,
        Caregiver,
        Client,
        FacilityManager,
        Clinician
    };
}