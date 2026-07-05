namespace CarePath.Contracts.Scheduling;

/// <summary>
/// Visit note row for paged lists (D-S4-7). Deliberately carries NO clinical free-text, vitals,
/// or signature URLs — list screens show what happened and whether concerns exist; the full
/// clinical content requires the audited detail read (<see cref="VisitNoteDetailDto"/>).
/// </summary>
public class VisitNoteSummaryDto
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

    /// <summary>True when the note contains concern text (computed server-side; the text itself is not included).</summary>
    public bool HasConcerns { get; init; }
}
