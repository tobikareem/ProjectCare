using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Identity;

namespace CarePath.Domain.Entities.Scheduling;

/// <summary>
/// Documents care activities, client condition, and vital signs recorded during a <see cref="Shift"/>.
/// Required for HIPAA compliance, quality assurance, and clinical continuity of care.
/// </summary>
/// <remarks>
/// <para>
/// <b>PHI fields:</b> <see cref="Activities"/>, <see cref="ClientCondition"/>,
/// <see cref="Concerns"/>, and <see cref="Medications"/> contain Protected Health Information
/// and must be encrypted at rest (enforced in the Infrastructure layer).
/// <see cref="CaregiverSignatureUrl"/> and <see cref="ClientOrFamilySignatureUrl"/> are biometric
/// PHI (digital signatures) stored as blob storage URLs; the blobs themselves must be access-controlled.
/// </para>
/// <para>
/// <b>Activity checkboxes</b> enable fast structured entry on the mobile app; the free-text
/// <see cref="Activities"/> field captures additional detail. At least one activity or a
/// non-empty <see cref="ClientCondition"/> entry should be present for a complete note.
/// </para>
/// <para>
/// <b>Visit date/time:</b> <see cref="VisitDateTime"/> must reflect the actual care delivery time
/// (typically the shift start), not the note-submission time. The Application layer is responsible
/// for supplying this value from shift context. HIPAA documentation accuracy depends on this distinction.
/// </para>
/// </remarks>
public class VisitNote : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the shift this note documents.</summary>
    public Guid ShiftId { get; set; }

    /// <summary>Navigation to the parent <see cref="Shift"/>. Required.</summary>
    public Shift Shift { get; set; } = null!;

    /// <summary>Foreign key to the caregiver who authored this note.</summary>
    public Guid CaregiverId { get; set; }

    /// <summary>Navigation to the authoring <see cref="Caregiver"/>. Required.</summary>
    public Caregiver Caregiver { get; set; } = null!;

    /// <summary>
    /// UTC date/time of the actual care visit. Must reflect actual care delivery time
    /// (typically shift start), not note-submission time. Set by the Application layer from shift context.
    /// </summary>
    public DateTime VisitDateTime { get; set; }

    // Activity Checkboxes (structured entry for quick mobile input)

    /// <summary>Personal care activities performed (bathing, dressing, grooming, toileting).</summary>
    public bool PersonalCare { get; set; }

    /// <summary>Meal preparation or feeding assistance provided.</summary>
    public bool MealPreparation { get; set; }

    /// <summary>Medication reminder, assistance, or administration performed.</summary>
    public bool Medication { get; set; }

    /// <summary>Light housekeeping tasks performed (tidying, laundry, vacuuming).</summary>
    public bool LightHousekeeping { get; set; }

    /// <summary>Companionship or social engagement activities provided.</summary>
    public bool Companionship { get; set; }

    /// <summary>Transportation or errand assistance provided.</summary>
    public bool Transportation { get; set; }

    /// <summary>Exercise, range-of-motion, or physical activity facilitated.</summary>
    public bool Exercise { get; set; }

    // Free-Text Notes (PHI — encrypt at rest)

    /// <summary>Narrative description of care activities performed (PHI — encrypt at rest). Free text.</summary>
    public string? Activities { get; set; }

    /// <summary>Observation of client's physical and mental condition during the visit (PHI — encrypt at rest). Free text.</summary>
    public string? ClientCondition { get; set; }

    /// <summary>
    /// Concerns or incidents noted during the visit (PHI — encrypt at rest).
    /// Any fall, behaviour change, or clinical concern should be documented here.
    /// </summary>
    public string? Concerns { get; set; }

    /// <summary>Medications given or administered during the visit, with dose and time (PHI — encrypt at rest). Free text.</summary>
    public string? Medications { get; set; }

    // Optional Vital Signs

    /// <summary>Systolic blood pressure reading in mmHg (e.g., 120 for 120/80).</summary>
    public int? BloodPressureSystolic { get; set; }

    /// <summary>Diastolic blood pressure reading in mmHg (e.g., 80 for 120/80).</summary>
    public int? BloodPressureDiastolic { get; set; }

    /// <summary>Body temperature in degrees Fahrenheit.</summary>
    public decimal? Temperature { get; set; }

    /// <summary>Heart rate in beats per minute (BPM).</summary>
    public int? HeartRate { get; set; }

    // Navigation Collections

    /// <summary>Photos attached to this visit note. Stored as URLs to external blob storage.</summary>
    public ICollection<VisitPhoto> Photos { get; set; } = new List<VisitPhoto>();

    // Signatures (stored as blob storage URLs — biometric PHI; blobs must be access-controlled)

    /// <summary>
    /// URL to the caregiver's digital signature image in external blob storage (e.g., Azure Blob Storage).
    /// Biometric PHI — the blob must be access-controlled and audited. Max 500 characters.
    /// <c>null</c> if the signature has not yet been collected.
    /// </summary>
    public string? CaregiverSignatureUrl { get; set; }

    /// <summary>
    /// URL to the client or family member's digital signature image in external blob storage.
    /// Biometric PHI — the blob must be access-controlled and audited. Max 500 characters.
    /// Confirms that care was received as documented. <c>null</c> if not yet collected.
    /// </summary>
    public string? ClientOrFamilySignatureUrl { get; set; }
}
