namespace CarePath.Application.Abstractions.Auth;

/// <summary>Non-PHI identity lookups the Application layer needs for membership listings.</summary>
/// <param name="Id">Platform user id.</param>
/// <param name="Email">Login email.</param>
/// <param name="DisplayName">Display name.</param>
public sealed record PlatformUserSummary(Guid Id, string Email, string DisplayName);

/// <summary>Read-only access to platform user identity fields (never credentials).</summary>
public interface IPlatformUserDirectory
{
    /// <summary>Returns the summaries for the given ids; unknown ids are omitted.</summary>
    Task<IReadOnlyDictionary<Guid, PlatformUserSummary>> GetSummariesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>Returns the summary for an email, or <c>null</c>.</summary>
    Task<PlatformUserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
}
