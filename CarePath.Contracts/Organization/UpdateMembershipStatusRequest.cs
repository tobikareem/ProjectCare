namespace CarePath.Contracts.Organization;

/// <summary>Activates or deactivates a membership.</summary>
public sealed class UpdateMembershipStatusRequest
{
    /// <summary>True to activate, false to deactivate.</summary>
    public bool IsActive { get; init; }
}
