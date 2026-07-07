using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Admin;

/// <summary>
/// Admin view of a user account (D-S6-8: role and account-status management only).
/// The three action fields are computed SERVER-side from the guardrails (last-Admin,
/// profile-role coupling) so the UI renders disabled-with-reason without duplicating rules.
/// </summary>
public class UserAccountDto
{
    /// <summary>Domain user identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Account email (login).</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Display name ("First Last").</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The user's single application role.</summary>
    public UserRole Role { get; init; }

    /// <summary>False when the account is deactivated (login rejected).</summary>
    public bool IsActive { get; init; }

    /// <summary>Last successful login (UTC), when known.</summary>
    public DateTime? LastLoginAt { get; init; }

    /// <summary>True when a caregiver profile is linked (role is coupled to the profile).</summary>
    public bool HasCaregiverProfile { get; init; }

    /// <summary>True when a client profile is linked (role is coupled to the profile).</summary>
    public bool HasClientProfile { get; init; }

    /// <summary>True when the current actor may change this user's role (guardrails permit it).</summary>
    public bool CanChangeRole { get; init; }

    /// <summary>True when the current actor may deactivate this account (guardrails permit it).</summary>
    public bool CanDeactivate { get; init; }

    /// <summary>Human-readable, PHI-free reason when an action is disabled (e.g., "Last active admin").</summary>
    public string? DisabledReason { get; init; }
}
