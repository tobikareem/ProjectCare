namespace CarePath.Contracts.Platform;

/// <summary>Suspends an organization. All logins and tokens are refused immediately; no data is deleted.</summary>
public sealed class SuspendOrganizationRequest
{
    /// <summary>Operator reason (max 200). Never patient data.</summary>
    public string Reason { get; init; } = string.Empty;
}
