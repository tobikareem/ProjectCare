using CarePath.Application.Abstractions.Auth;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.ControlPlane.Auth;

/// <summary>Read-only platform user lookups; never returns credentials or stamps.</summary>
public sealed class PlatformUserDirectory : IPlatformUserDirectory
{
    private readonly ControlPlaneDbContext _context;
    private readonly UserManager<PlatformUser> _userManager;

    /// <summary>Initializes the directory.</summary>
    public PlatformUserDirectory(ControlPlaneDbContext context, UserManager<PlatformUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, PlatformUserSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, PlatformUserSummary>();
        }

        var ids = userIds.ToArray();
        return await _context.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .Select(user => new PlatformUserSummary(user.Id, user.Email ?? string.Empty, user.DisplayName))
            .ToDictionaryAsync(summary => summary.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PlatformUserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        return user is null ? null : new PlatformUserSummary(user.Id, user.Email ?? string.Empty, user.DisplayName);
    }
}
