namespace CarePath.Contracts.Transitions;

/// <summary>
/// Raw discharge content for the clinician review screen. Clinical PHI: served ONLY by the
/// dedicated Clinician/Coordinator <c>/content</c> endpoint; every read is audit logged
/// (D-S5-3). <c>RawContent</c> must never appear in logs, errors, URLs, or any other contract.
/// </summary>
public class DischargeDocumentContentDto
{
    /// <summary>Document identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Raw discharge text. PHI — never log.</summary>
    public string? RawContent { get; init; }
}
