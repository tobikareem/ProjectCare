using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Discharge intake request (Coordinator). Sprint 5 accepts metadata plus clinician-approved
/// raw text ONLY — binary uploads are rejected with a stable error code until the Sprint 7
/// secure-storage gates land. Clinical PHI: this request body must never be logged.
/// </summary>
public class CreateDischargeDocumentRequest
{
    /// <summary>Client the document belongs to.</summary>
    public Guid ClientId { get; init; }

    /// <summary>How the document arrived.</summary>
    public DischargeDocumentSourceType SourceType { get; init; }

    /// <summary>Raw discharge text. PHI — never log, never echo in validation errors.</summary>
    public string RawContent { get; init; } = string.Empty;

    /// <summary>External reference (e.g., hospital document number). Must be PHI-minimal.</summary>
    public string? SourceReference { get; init; }

    /// <summary>Discharging hospital name, when known.</summary>
    public string? HospitalName { get; init; }

    /// <summary>Hospital discharge date (UTC). Basis for the 30-day window at activation.</summary>
    public DateTime DischargeDate { get; init; }
}
