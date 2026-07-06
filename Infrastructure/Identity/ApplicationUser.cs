using CarePath.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace CarePath.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity user linked to a pure Domain <see cref="User"/> record.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Foreign key to the associated domain user profile.</summary>
    public Guid DomainUserId { get; set; }

    /// <summary>Navigation to the associated domain user profile.</summary>
    public User DomainUser { get; set; } = null!;

    /// <summary>SHA-256 hash of the currently valid opaque refresh token.</summary>
    public string? RefreshTokenHash { get; set; }

    /// <summary>UTC expiration for the currently valid refresh token.</summary>
    public DateTime? RefreshTokenExpiresAtUtc { get; set; }
}
