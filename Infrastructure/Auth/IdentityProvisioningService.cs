using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace CarePath.Infrastructure.Auth;

/// <summary>
/// ASP.NET Core Identity implementation of Sprint 4 account provisioning.
/// </summary>
public sealed class IdentityProvisioningService : IIdentityProvisioningService
{
    private readonly UserManager<ApplicationUser> userManager;
    private readonly RoleManager<IdentityRole<Guid>> roleManager;

    /// <summary>Initializes a new Identity provisioning service.</summary>
    /// <param name="userManager">ASP.NET Core Identity user manager.</param>
    /// <param name="roleManager">ASP.NET Core Identity role manager.</param>
    public IdentityProvisioningService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        this.userManager = userManager;
        this.roleManager = roleManager;
    }

    /// <inheritdoc />
    public async Task<IdentityProvisioningResult> ProvisionUserAsync(
        IdentityProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return IdentityProvisioningResult.Failed("EmailUnavailable");
        }

        if (!await roleManager.RoleExistsAsync(request.Role))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(request.Role));
            if (!roleResult.Succeeded)
            {
                return IdentityProvisioningResult.Failed("RoleProvisioningFailed");
            }
        }

        var identityUser = new ApplicationUser
        {
            Id = request.DomainUserId,
            DomainUserId = request.DomainUserId,
            UserName = request.Email,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(identityUser, request.TemporaryPassword);
        if (!createResult.Succeeded)
        {
            return IdentityProvisioningResult.Failed("IdentityProvisioningFailed");
        }

        var addRoleResult = await userManager.AddToRoleAsync(identityUser, request.Role);
        return addRoleResult.Succeeded
            ? IdentityProvisioningResult.Success()
            : IdentityProvisioningResult.Failed("RoleAssignmentFailed");
    }
}
