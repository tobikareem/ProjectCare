namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Full visit note (renamed from <c>VisitNoteDto</c> per D-S4-7). Clinical PHI throughout:
/// endpoints returning this DTO require role AND object-level authorization, and every read is
/// audit logged. None of the free-text fields may ever appear in logs, exceptions, or URLs.
/// Lists use <see cref="VisitNoteSummaryDto"/>, which carries no clinical text.
/// </summary>
public class VisitNoteDetailDto
{
    /// <summary>Visit note identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Shift this note belongs to.</summary>
    public Guid ShiftId { get; init; }

    /// <summary>Authoring caregiver identifier.</summary>
    public Guid CaregiverId { get; init; }

    /// <summary>Visit date/time (UTC).</summary>
    public DateTime VisitDateTime { get; init; }

    /// <summary>Personal care was provided.</summary>
    public bool PersonalCare { get; init; }

    /// <summary>Meal preparation was provided.</summary>
    public bool MealPreparation { get; init; }

    /// <summary>Medication assistance was provided.</summary>
    public bool Medication { get; init; }

    /// <summary>Light housekeeping was provided.</summary>
    public bool LightHousekeeping { get; init; }

    /// <summary>Companionship was provided.</summary>
    public bool Companionship { get; init; }

    /// <summary>Transportation was provided.</summary>
    public bool Transportation { get; init; }

    /// <summary>Exercise assistance was provided.</summary>
    public bool Exercise { get; init; }

    /// <summary>Activity narrative. PHI — never log.</summary>
    public string? Activities { get; init; }

    /// <summary>Observed client condition. PHI — never log.</summary>
    public string? ClientCondition { get; init; }

    /// <summary>Concerns raised. PHI — never log.</summary>
    public string? Concerns { get; init; }

    /// <summary>Medication observations. PHI — never log.</summary>
    public string? Medications { get; init; }

    /// <summary>Systolic blood pressure, when recorded.</summary>
    public int? BloodPressureSystolic { get; init; }

    /// <summary>Diastolic blood pressure, when recorded.</summary>
    public int? BloodPressureDiastolic { get; init; }

    /// <summary>Body temperature, when recorded.</summary>
    public decimal? Temperature { get; init; }

    /// <summary>Heart rate, when recorded.</summary>
    public int? HeartRate { get; init; }

    /// <summary>Linked transition plan, when this note feeds CP-03 adherence tracking.</summary>
    public Guid? TransitionPlanId { get; init; }

    /// <summary>Photos attached to this note (metadata only).</summary>
    public IReadOnlyList<VisitPhotoDto> Photos { get; init; } = [];

    /// <summary>Short-lived, access-controlled URL to the caregiver signature blob. Null until the signed-URL service exists (D-S4-3).</summary>
    public string? CaregiverSignatureUrl { get; init; }

    /// <summary>Short-lived, access-controlled URL to the client/family signature blob. Null until the signed-URL service exists (D-S4-3).</summary>
    public string? ClientOrFamilySignatureUrl { get; init; }
}
