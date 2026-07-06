using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Discharge document status/metadata view. Deliberately carries NO <c>RawContent</c> —
/// that is served only by the dedicated, audited <c>/content</c> endpoint (D-S5-3).
/// </summary>
public class DischargeDocumentDto
{
    /// <summary>Document identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Client the document belongs to.</summary>
    public Guid ClientId { get; init; }

    /// <summary>How the document arrived.</summary>
    public DischargeDocumentSourceType SourceType { get; init; }

    /// <summary>External reference (e.g., hospital document number). Must be PHI-minimal.</summary>
    public string? SourceReference { get; init; }

    /// <summary>Extraction workflow status.</summary>
    public DischargeDocumentStatus Status { get; init; }

    /// <summary>User who submitted the document.</summary>
    public Guid UploadedBy { get; init; }

    /// <summary>When the document was submitted (UTC).</summary>
    public DateTime UploadedAt { get; init; }
}
