using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Identity;

/// <summary>
/// Caregiver profile entity representing care providers who deliver services to clients.
/// Caregivers are either W-2 employees (in-home care) or 1099 contractors (facility staffing).
/// </summary>
/// <remarks>
/// <para>
/// <b>Employment type and margin targets:</b>
/// <list type="bullet">
///   <item><see cref="EmploymentType.W2Employee"/> — In-home care; target gross margin 40-45%.</item>
///   <item><see cref="EmploymentType.Contractor1099"/> — Facility staffing; target gross margin 25-30%.</item>
/// </list>
/// </para>
/// <para>
/// <b>Scheduling:</b> Use the <c>Has*</c> skill flags and <c>Available*</c> flags together with
/// <see cref="CaregiverCertification"/> records when matching a caregiver to a shift requirement.
/// </para>
/// <para>
/// <b>Performance counters:</b> <see cref="TotalShiftsCompleted"/> and <see cref="NoShowCount"/>
/// are event-sourced counters. Use <see cref="RecordCompletedShift"/> and <see cref="RecordNoShow"/>
/// to increment them; never set them directly.
/// </para>
/// </remarks>
public class Caregiver : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the <see cref="User"/> account that owns this profile.</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owning <see cref="User"/>. Required.</summary>
    public User User { get; set; } = null!;

    // Employment Details

    /// <summary>
    /// Employment classification. Determines billing model and target margin.
    /// Default: <see cref="EmploymentType.W2Employee"/> (in-home care).
    /// </summary>
    public EmploymentType EmploymentType { get; set; } = EmploymentType.W2Employee;

    /// <summary>
    /// Hourly pay rate in USD. Copied to <c>Shift.PayRate</c> at shift creation
    /// to preserve the historical rate at time of service.
    /// </summary>
    public decimal HourlyPayRate { get; set; }

    /// <summary>UTC date the caregiver was hired or contracted. Set by the Application layer on creation.</summary>
    public DateTime HireDate { get; set; }

    /// <summary>UTC date employment ended. <c>null</c> if currently active.</summary>
    public DateTime? TerminationDate { get; set; }

    // Certifications

    /// <summary>All certifications held by this caregiver. See <see cref="CaregiverCertification"/>.</summary>
    public ICollection<CaregiverCertification> Certifications { get; set; } = new List<CaregiverCertification>();

    // Skills and Specialties (used for caregiver-client matching)

    /// <summary>Caregiver has completed dementia care specialisation training.</summary>
    public bool HasDementiaCare { get; set; }

    /// <summary>Caregiver has completed Alzheimer's disease care specialisation training.</summary>
    public bool HasAlzheimersCare { get; set; }

    /// <summary>Caregiver is qualified to assist with client mobility (transfers, ambulation).</summary>
    public bool HasMobilityAssistance { get; set; }

    /// <summary>Caregiver is authorised to assist with or administer medications.</summary>
    public bool HasMedicationManagement { get; set; }

    // Availability

    /// <summary>Available to work standard weekday hours (Mon-Fri). Default: <c>true</c>.</summary>
    public bool AvailableWeekdays { get; set; } = true;

    /// <summary>Available to work weekend hours (Sat-Sun).</summary>
    public bool AvailableWeekends { get; set; }

    /// <summary>Available for overnight or late-night shifts.</summary>
    public bool AvailableNights { get; set; }

    /// <summary>Maximum hours the caregiver may work per week. Default: 40.</summary>
    public int MaxWeeklyHours { get; set; } = 40;

    // Performance Metrics

    /// <summary>
    /// Rolling average client/coordinator rating on a 1.0–5.0 scale.
    /// <c>null</c> if no ratings have been recorded yet.
    /// Updated by the Application layer when new ratings are submitted.
    /// </summary>
    public decimal? AverageRating { get; set; }

    /// <summary>
    /// Cumulative count of shifts this caregiver has successfully completed.
    /// Increment via <see cref="RecordCompletedShift"/>; do not set directly.
    /// </summary>
    public int TotalShiftsCompleted { get; private set; }

    /// <summary>
    /// Cumulative count of no-show events (shifts where the caregiver failed to appear).
    /// Increment via <see cref="RecordNoShow"/>; do not set directly.
    /// </summary>
    public int NoShowCount { get; private set; }

    // Navigation Collections

    /// <summary>All shifts assigned to this caregiver.</summary>
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    /// <summary>All visit notes authored by this caregiver.</summary>
    public ICollection<VisitNote> VisitNotes { get; set; } = new List<VisitNote>();

    // Domain Methods

    /// <summary>
    /// Increments <see cref="TotalShiftsCompleted"/> when a shift is confirmed completed.
    /// Call this when a shift transitions to <c>ShiftStatus.Completed</c>.
    /// </summary>
    public void RecordCompletedShift() => TotalShiftsCompleted++;

    /// <summary>
    /// Increments <see cref="NoShowCount"/> when this caregiver fails to appear for an assigned shift.
    /// Call this when a shift transitions to <c>ShiftStatus.NoShow</c>.
    /// </summary>
    public void RecordNoShow() => NoShowCount++;
}
