using System.Security.Claims;
using CarePath.Client.Http;
using CarePath.Contracts.Auth;
using Microsoft.AspNetCore.Components.Authorization;

namespace CarePath.Web.Auth;

/// <summary>
/// Blazor authentication state derived from the D-S6-2 in-memory token session. Tokens never
/// touch cookies or browser storage — the session (and the authenticated state) ends with the
/// app instance. The server re-authorizes every API call; these claims drive UI gating only.
/// </summary>
public sealed class TokenAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string AuthenticationType = "CarePathJwt";

    /// <summary>Claim carrying the session's organization id (CP-04).</summary>
    public const string OrganizationIdClaim = "organization_id";

    /// <summary>Claim carrying the session's organization display name (CP-04).</summary>
    public const string OrganizationNameClaim = "organization_name";

    /// <summary>Claim carrying the session's membership id (CP-04).</summary>
    public const string MembershipIdClaim = "membership_id";

    /// <summary>Claim carrying the session kind (Tenant or Platform).</summary>
    public const string TokenKindClaim = "token_kind";

    private readonly InMemoryAccessTokenProvider _tokens;

    /// <summary>Creates the provider.</summary>
    /// <param name="tokens">The in-memory token store shared with the API message handler.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="tokens"/> is null.</exception>
    public TokenAuthenticationStateProvider(InMemoryAccessTokenProvider tokens) =>
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));

    /// <summary>True when an unexpired session is present.</summary>
    public bool HasActiveSession => IsActive(_tokens.CurrentSession);

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var session = _tokens.CurrentSession;
        var identity = IsActive(session) && session is not null
            ? new ClaimsIdentity(BuildClaims(session), AuthenticationType)
            : new ClaimsIdentity();

        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }

    /// <summary>Stores the session after a successful login and notifies subscribers.</summary>
    /// <param name="session">The token response from <c>AuthClient</c>. Never logged.</param>
    public void MarkSignedIn(AuthTokenResponse session)
    {
        _tokens.SetSession(session);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>Clears the session (sign-out) and notifies subscribers.</summary>
    public void SignOut()
    {
        _tokens.ClearSession();
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static List<Claim> BuildClaims(AuthTokenResponse session)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, session.DisplayName),
            new(TokenKindClaim, session.TokenKind.ToString()),
        };

        // Platform sessions carry no tenant role; never synthesize one.
        if (session.Role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, session.Role.Value.ToString()));
        }

        if (session.OrganizationId is not null)
        {
            claims.Add(new Claim(OrganizationIdClaim, session.OrganizationId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(session.OrganizationDisplayName))
        {
            claims.Add(new Claim(OrganizationNameClaim, session.OrganizationDisplayName));
        }

        if (session.MembershipId is not null)
        {
            claims.Add(new Claim(MembershipIdClaim, session.MembershipId.Value.ToString()));
        }

        return claims;
    }

    private static bool IsActive(AuthTokenResponse? session) =>
        session is not null && session.ExpiresAtUtc > DateTime.UtcNow;
}
