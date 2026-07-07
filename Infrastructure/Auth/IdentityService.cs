using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure.Identity;
using CarePath.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CarePath.Infrastructure.Auth;

/// <summary>
/// Validates Identity credentials and returns client-safe identity metadata for token issuance.
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private const int RefreshTokenBytes = 32;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

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
    public async Task<string> IssueRefreshTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("Authenticated user could not be found.");

        var token = CreateOpaqueRefreshToken();
        user.RefreshTokenHash = HashRefreshToken(token);
        user.RefreshTokenExpiresAtUtc = DateTime.UtcNow.Add(RefreshTokenLifetime);

        await dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    /// <inheritdoc />
    public async Task<RefreshTokenRotationResult> RotateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return RefreshTokenRotationResult.Failed("InvalidCredentials");
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var matchingUsers = await userManager.Users
            .IgnoreQueryFilters()
            .Include(identityUser => identityUser.DomainUser)
            .Where(identityUser => identityUser.RefreshTokenHash == tokenHash)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matchingUsers.Count != 1)
        {
            return RefreshTokenRotationResult.Failed("InvalidCredentials");
        }

        var user = matchingUsers[0];
        var now = DateTime.UtcNow;
        if (user.RefreshTokenExpiresAtUtc is null || user.RefreshTokenExpiresAtUtc <= now)
        {
            return RefreshTokenRotationResult.Failed("InvalidCredentials");
        }

        var availabilityFailure = GetAvailabilityFailure(user);
        if (availabilityFailure is not null || await userManager.IsLockedOutAsync(user))
        {
            return RefreshTokenRotationResult.Failed("InvalidCredentials");
        }

        var rotatedToken = CreateOpaqueRefreshToken();
        var rotatedTokenHash = HashRefreshToken(rotatedToken);
        var rotatedTokenExpiresAtUtc = now.Add(RefreshTokenLifetime);

        if (dbContext.Database.IsRelational())
        {
            var updatedRows = await dbContext.Set<ApplicationUser>()
                .Where(identityUser =>
                    identityUser.Id == user.Id &&
                    identityUser.RefreshTokenHash == tokenHash &&
                    identityUser.RefreshTokenExpiresAtUtc > now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(identityUser => identityUser.RefreshTokenHash, rotatedTokenHash)
                        .SetProperty(identityUser => identityUser.RefreshTokenExpiresAtUtc, rotatedTokenExpiresAtUtc),
                    cancellationToken);

            if (updatedRows != 1)
            {
                return RefreshTokenRotationResult.Failed("InvalidCredentials");
            }
        }
        else
        {
            user.RefreshTokenHash = rotatedTokenHash;
            user.RefreshTokenExpiresAtUtc = rotatedTokenExpiresAtUtc;

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new RefreshTokenRotationResult(
            true,
            await CreateSuccessResultAsync(user),
            rotatedToken,
            null);
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
            null,
            user.DomainUser.FullName);
    }

    private static string CreateOpaqueRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
    }

    private static string HashRefreshToken(string refreshToken)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(hash);
    }
}
