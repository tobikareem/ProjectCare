using CarePath.Application.Abstractions.Auth;

namespace CarePath.WebApi.Security;

public static class CarePathAuthorizationPolicies
{
    public const string Admin = ApplicationRoles.Admin;
    public const string Coordinator = ApplicationRoles.Coordinator;
    public const string Caregiver = ApplicationRoles.Caregiver;
    public const string Client = ApplicationRoles.Client;
    public const string FacilityManager = ApplicationRoles.FacilityManager;
    public const string Clinician = ApplicationRoles.Clinician;
}