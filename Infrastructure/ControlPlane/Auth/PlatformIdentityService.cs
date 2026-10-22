using System.Security.Cryptography;
using System.Text;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.ControlPlane.Auth;

/// <summary>
/// Control-plane identity: credential checks with lockout, and server-tracked refresh tokens
/// bound to a membership and organization (ADR 0003 §10.1). Uses <see cref="UserManager{TUser}"/>
/// only, so there is no dependency on cookie authentication schemes.
/// </summary>
public sealed class PlatformIdentityService : IIdentityService
{
    private const int RefreshTokenBytes = 32;
    private const string InvalidCredentials = "InvalidCredentials";
    private const string LockedOut = "LockedOut";
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    // A client that retries a dropped refresh response, or two tabs refreshing together, presents
    // the spent token again within seconds. That is not treated as theft: inside the window the
    // replay simply fails (the caller still gets the generic 401 and must sign in again) without
    // revoking the whole family; reuse after the window revokes every token of the family.
    private static readonly TimeSpan ReuseGraceWindow = TimeSpan.FromSeconds(30);

    // Hashed once per process so an unknown email costs the same password-hash work as a wrong
    // password: response bodies are already identical, this removes the timing oracle.
    private static readonly PlatformUser TimingDecoy = new() { UserName = "decoy", Email = "decoy" };
    private static string? _decoyHash;

    private readonly ControlPlaneDbContext _context;
    private readonly UserManager<PlatformUser> _userManager;
    private readonly IPlatformAuditLogger _auditLogger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes the service.</summary>
    public PlatformIdentityService(
        ControlPlaneDbContext context,
        UserManager<PlatformUser> userManager,
        IPlatformAuditLogger auditLogger,
        TimeProvider timeProvider)
    {
        _context = context;
        _userManager = userManager;
        _auditLogger = auditLogger;
        _timeProvider = timeProvider;
    }

    private void BurnPasswordHash(string password)
    {
        _decoyHash ??= _userManager.PasswordHasher.HashPassword(TimingDecoy, Guid.NewGuid().ToString("N"));
        _userManager.PasswordHasher.VerifyHashedPassword(TimingDecoy, _decoyHash, password);
    }

    /// <inheritdoc />
    public async Task<IdentityUserResult> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return IdentityUserResult.Failed(InvalidCredentials);
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            BurnPasswordHash(password);
            return IdentityUserResult.Failed(InvalidCredentials);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return IdentityUserResult.Failed(LockedOut);
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            await _userManager.AccessFailedAsync(user);
            return IdentityUserResult.Failed(await _userManager.IsLockedOutAsync(user) ? LockedOut : InvalidCredentials);
        }

        if (user.AccessFailedCount > 0)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        return Success(user);
    }

    /// <inheritdoc />
    public async Task<IdentityUserResult> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null ? IdentityUserResult.Failed("UserNotFound") : Success(user);
    }

    /// <inheritdoc />
    public async Task<string> IssueRefreshTokenAsync(Guid userId, Guid? membershipId, Guid? organizationId, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var token = CreateOpaqueToken();

        _context.PlatformRefreshTokens.Add(new PlatformRefreshToken
        {
            PlatformUserId = userId,
            MembershipId = membershipId,
            OrganizationId = organizationId,
            TokenHash = Hash(token),
            ExpiresAtUtc = now.Add(RefreshTokenLifetime),
            CreatedAtUtc = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        return token;
    }

    /// <inheritdoc />
    public async Task<RefreshTokenRotationResult> RotateRefreshTokenAsync(
        string refreshToken,
        RefreshTokenBinding expectedBinding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedBinding);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return RefreshTokenRotationResult.Failed(InvalidCredentials);
        }

        var tokenHash = Hash(refreshToken);
        var row = await _context.PlatformRefreshTokens
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        if (row is null)
        {
            return RefreshTokenRotationResult.Failed(InvalidCredentials);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (row.RevokedAtUtc is not null)
        {
            if (now - row.RevokedAtUtc.Value < ReuseGraceWindow)
            {
                return RefreshTokenRotationResult.Failed(InvalidCredentials);
            }

            // Reuse of a spent token after the grace window means the family is compromised; revoke all of it.
            await RevokeFamilyAsync(row, now, cancellationToken);
            await _auditLogger.LogAsync(
                new PlatformAuditEntry(row.PlatformUserId, row.OrganizationId, PlatformAuditAction.RefreshTokenReuseDetected, nameof(PlatformRefreshToken), row.Id, null),
                cancellationToken);
            return RefreshTokenRotationResult.Failed(InvalidCredentials);
        }

        if (row.ExpiresAtUtc <= now || !MatchesBinding(row, expectedBinding))
        {
            return RefreshTokenRotationResult.Failed(InvalidCredentials);
        }

        var user = await _userManager.FindByIdAsync(row.PlatformUserId.ToString());
        if (user is null || await _userManager.IsLockedOutAsync(user))
        {
            return RefreshTokenRotationResult.Failed(InvalidCredentials);
        }

        if (!await TryRevokeAsync(row, now, cancellationToken))
        {
            // A concurrent rotation won; only one caller may continue with this token.
            return RefreshTokenRotationResult.Failed(InvalidCredentials);
        }

        var rotatedToken = CreateOpaqueToken();
        _context.PlatformRefreshTokens.Add(new PlatformRefreshToken
        {
            PlatformUserId = row.PlatformUserId,
            MembershipId = row.MembershipId,
            OrganizationId = row.OrganizationId,
            TokenHash = Hash(rotatedToken),
            ExpiresAtUtc = now.Add(RefreshTokenLifetime),
            CreatedAtUtc = now
        });
        await _context.SaveChangesAsync(cancellationToken);

        return new RefreshTokenRotationResult(true, Success(user), rotatedToken, row.MembershipId, row.OrganizationId, null);
    }

    private static bool MatchesBinding(PlatformRefreshToken row, RefreshTokenBinding binding) =>
        binding.IsPlatform
            ? row.MembershipId is null && row.OrganizationId is null
            : row.OrganizationId is not null && row.OrganizationId == binding.OrganizationId;

    private async Task<bool> TryRevokeAsync(PlatformRefreshToken row, DateTime now, CancellationToken cancellationToken)
    {
        if (_context.Database.IsRelational())
        {
            var updated = await _context.PlatformRefreshTokens
                .Where(candidate => candidate.Id == row.Id && candidate.RevokedAtUtc == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.RevokedAtUtc, now), cancellationToken);
            if (updated == 1)
            {
                _context.Entry(row).State = EntityState.Detached;
            }

            return updated == 1;
        }

        row.RevokedAtUtc = now;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RevokeFamilyAsync(PlatformRefreshToken row, DateTime now, CancellationToken cancellationToken)
    {
        var family = _context.PlatformRefreshTokens.Where(candidate =>
            candidate.RevokedAtUtc == null
            && (row.MembershipId != null
                ? candidate.MembershipId == row.MembershipId
                : candidate.PlatformUserId == row.PlatformUserId && candidate.MembershipId == null));

        if (_context.Database.IsRelational())
        {
            await family.ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.RevokedAtUtc, now), cancellationToken);
            return;
        }

        foreach (var token in await family.ToListAsync(cancellationToken))
        {
            token.RevokedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static IdentityUserResult Success(PlatformUser user) =>
        new(true, user.Id, user.Email, user.DisplayName, user.IsPlatformAdmin, null);

    private static string CreateOpaqueToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

    private static string Hash(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
