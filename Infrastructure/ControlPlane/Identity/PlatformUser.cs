using Microsoft.AspNetCore.Identity;

namespace CarePath.Infrastructure.ControlPlane.Identity;

/// <summary>
/// ASP.NET Core Identity user stored in the control plane (CP-04). One platform identity may
/// hold memberships in many organizations; its <see cref="IdentityUser{TKey}.Id"/> equals the
/// tenant <c>User.Id</c> by construction (D-CP04-02).
/// </summary>
/// <remarks>
/// Carries no organization, role, or clinical field. Tenant roles live on
/// <c>OrganizationMembership</c>; the platform operator role is <see cref="IsPlatformAdmin"/>.
/// Classification: email + display name + a Client-role membership identifies a person as a
/// client of a home-care agency, so the control plane is treated as a sensitive store with the
/// same encryption and access controls as tenant data (design spec, HIPAA section; S8-TASK-002).
/// </remarks>
public class PlatformUser : IdentityUser<Guid>
{
    /// <summary>Display name shown in headers and audit views (max 100). Never clinical data.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>True for CarePath operators. Platform tokens are issued only to these users and only at the platform host.</summary>
    public bool IsPlatformAdmin { get; set; }
}
