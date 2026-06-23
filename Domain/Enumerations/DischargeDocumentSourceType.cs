namespace CarePath.Domain.Enumerations;

/// <summary>
/// Describes how a discharge document was ingested into CarePath Transitions.
/// </summary>
public enum DischargeDocumentSourceType
{
    /// <summary>
    /// The document was uploaded as a PDF file by a coordinator or clinician.
    /// </summary>
    PdfUpload = 1,

    /// <summary>
    /// The document was uploaded as a photo (JPEG or PNG) taken of a paper discharge form.
    /// </summary>
    PhotoUpload = 2,

    /// <summary>
    /// The document was imported programmatically from a hospital EHR system via HL7 FHIR R4.
    /// Phase 2 capability — not available in MVP.
    /// </summary>
    FhirImport = 3
}
