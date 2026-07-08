using CarePath.Application.Abstractions.Auth;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CarePath.Infrastructure.Auth;

/// <summary>
/// Keeps ASP.NET Core Identity role assignments aligned with the Domain single-role model.
/// </summary>
public sealed class IdentityRoleManagementService : IIdentityRoleManagementService
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly RoleManager<IdentityRole<Guid>> roleManager;

    /// <summary>Initializes a role management service.</summary>
    public IdentityRoleManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        this.userManager = userManager;
        this.roleManager = roleManager;
    }

    /// <inheritdoc />
    public async Task<bool> ReplaceUserRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        if (!Enum.TryParse<UserRole>(role, ignoreCase: false, out var parsedRole) ||
            !Enum.IsDefined(parsedRole))
        {
            return false;
        }

        role = parsedRole.ToString();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            if (!roleResult.Succeeded)
            {
                return false;
            }
        }

        var existingRoles = await userManager.GetRolesAsync(user);
        if (existingRoles.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, existingRoles);
            if (!removeResult.Succeeded)
            {
                return false;
            }
        }

        var addResult = await userManager.AddToRoleAsync(user, role);
        return addResult.Succeeded;
    }
}
