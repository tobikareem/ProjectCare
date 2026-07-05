namespace CarePath.Application.Abstractions.Auth;

public static class ApplicationRoles
{
    public const string Admin = "Admin";
    public const string Coordinator = "Coordinator";
    public const string Caregiver = "Caregiver";
    public const string Client = "Client";
    public const string FacilityManager = "FacilityManager";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Admin,
        Coordinator,
        Caregiver,
        Client,
        FacilityManager
    };
}