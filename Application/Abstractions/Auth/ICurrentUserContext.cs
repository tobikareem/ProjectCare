namespace CarePath.Application.Abstractions.Auth;

/// <summary>The authenticated caller, as established from the validated access token.</summary>
public interface ICurrentUserContext
{
    /// <summary>Platform user id (equals the tenant <c>User.Id</c>); <c>null</c> when anonymous.</summary>
    Guid? UserId { get; }

    /// <summary>Principal name.</summary>
    string? UserName { get; }

    /// <summary>True when a validated token is present.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Role claims carried by the token.</summary>
    IReadOnlySet<string> Roles { get; }

    /// <summary>Request correlation id.</summary>
    string? CorrelationId { get; }

    /// <summary>Organization the token is bound to; <c>null</c> for anonymous and platform requests.</summary>
    Guid? OrganizationId { get; }

    /// <summary>Membership the token is bound to; <c>null</c> for anonymous and platform requests.</summary>
    Guid? MembershipId { get; }
}
