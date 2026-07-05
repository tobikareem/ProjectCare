namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Caregiver's visit documentation submission (shift ID travels in the route; assigned
/// caregiver only). Clinical PHI throughout — this request body must never be logged.
/// </summary>
public class CreateVisitNoteRequest
{
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
}
