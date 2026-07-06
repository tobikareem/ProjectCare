namespace CarePath.Contracts.Enumerations;

/// <summary>
/// Client-safe mirror of <c>CarePath.Domain.Enumerations.DischargeDocumentSourceType</c>.
/// Values must stay identical to the Domain enum (parity is verified by tests).
/// </summary>
public enum DischargeDocumentSourceType
{
    /// <summary>Uploaded PDF document (binary persistence gated until Sprint 7).</summary>
    PdfUpload = 1,

    /// <summary>Uploaded photo of discharge paperwork (binary persistence gated until Sprint 7).</summary>
    PhotoUpload = 2,

    /// <summary>FHIR import from a hospital system (Sprint 7).</summary>
    FhirImport = 3
}
