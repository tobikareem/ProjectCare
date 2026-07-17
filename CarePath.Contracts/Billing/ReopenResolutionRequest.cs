namespace CarePath.Contracts.Billing;

/// <summary>
/// Reopens a previously resolved shift (D-S6-18). Appends a superseding
/// <c>Reopened</c> record and returns the shift to the unresolved queue; the prior
/// resolution is preserved unchanged.
/// </summary>
public class ReopenResolutionRequest
{
    /// <summary>Maximum note length.</summary>
    public const int NoteMaxLength = 500;

    /// <summary>Optional PHI-free note explaining the reopen (max 500 chars).</summary>
    public string? Note { get; init; }
}
