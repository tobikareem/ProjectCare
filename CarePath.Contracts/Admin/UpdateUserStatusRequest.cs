namespace CarePath.Contracts.Admin;

/// <summary>
/// Admin request to activate or deactivate an account (D-S6-8). Deactivation is the "remove
/// access" operation — login then fails with the generic auth code; never a hard delete.
/// Guardrail: the last active Admin cannot be deactivated.
/// </summary>
public class UpdateUserStatusRequest
{
    /// <summary>True to activate; false to deactivate.</summary>
    public bool IsActive { get; init; }
}
