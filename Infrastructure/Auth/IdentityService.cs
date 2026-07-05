using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Auth;

/// <summary>
/// Validates Identity credentials and returns client-safe identity metadata for token issuance.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly CarePathDbContext dbContext;
    private readonly SignInManager<ApplicationUser> signInManager;
    private readonly UserManager<ApplicationUser> userManager;

    /// <summary>Initializes an Identity-backed identity service.</summary>
    /// <param name="dbContext">CarePath database context.</param>
    /// <param name="signInManager">ASP.NET Core Identity sign-in manager.</param>
    /// <param name="userManager">ASP.NET Core Identity user manager.</param>
    public IdentityService(
        CarePathDbContext dbContext,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        this.dbContext = dbContext;
        this.signInManager = signInManager;
        this.userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<IdentityUserResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            return IdentityUserResult.Failed("InvalidCredentials");
        }

        var availabilityFailure = GetAvailabilityFailure(user);
        if (availabilityFailure is not null)
        {
            return IdentityUserResult.Failed(availabilityFailure);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return IdentityUserResult.Failed("LockedOut");
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            password,
            lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            return IdentityUserResult.Failed("LockedOut");
        }

        if (!signInResult.Succeeded)
        {
            return IdentityUserResult.Failed("InvalidCredentials");
        }

        user.DomainUser.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await CreateSuccessResultAsync(user);
    }

    /// <inheritdoc />
    public async Task<IdentityUserResult> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return IdentityUserResult.Failed("UserNotFound");
        }

        var availabilityFailure = GetAvailabilityFailure(user);
        if (availabilityFailure is not null)
        {
            return IdentityUserResult.Failed(availabilityFailure);
        }

        return await CreateSuccessResultAsync(user);
    }

    private Task<ApplicationUser?> FindUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return userManager.Users
            .IgnoreQueryFilters()
            .Include(identityUser => identityUser.DomainUser)
            .SingleOrDefaultAsync(identityUser => identityUser.Email == email, cancellationToken);
    }

    private Task<ApplicationUser?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return userManager.Users
            .IgnoreQueryFilters()
            .Include(identityUser => identityUser.DomainUser)
            .SingleOrDefaultAsync(identityUser => identityUser.Id == userId, cancellationToken);
    }

    private static string? GetAvailabilityFailure(ApplicationUser user)
    {
        if (user.DomainUser.IsDeleted)
        {
            return "UserNotFound";
        }

        if (!user.DomainUser.IsActive)
        {
            return "InactiveUser";
        }

        return null;
    }

    private async Task<IdentityUserResult> CreateSuccessResultAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new IdentityUserResult(
            true,
            user.Id,
            user.Email,
            new HashSet<string>(roles, StringComparer.Ordinal),
            null);
    }
}