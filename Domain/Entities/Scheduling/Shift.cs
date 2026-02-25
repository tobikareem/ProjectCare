using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Scheduling;

/// <summary>
/// A scheduled care session delivered by a <see cref="Caregiver"/> to a <see cref="Client"/>.
/// Tracks scheduling, GPS check-in/out, financial details, and gross-margin calculations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Financial history preservation:</b> <see cref="BillRate"/> and <see cref="PayRate"/> are
/// copied from <c>Client.HourlyBillRate</c> and <c>Caregiver.HourlyPayRate</c> at shift creation.
/// This preserves the rates that were in effect at time of service, even if the client or caregiver
/// rates change later.
/// </para>
/// <para>
/// <b>Status lifecycle:</b>
/// <see cref="ShiftStatus.Scheduled"/> → <see cref="ShiftStatus.InProgress"/> → <see cref="ShiftStatus.Completed"/>;
/// or → <see cref="ShiftStatus.Cancelled"/>; or → <see cref="ShiftStatus.NoShow"/>.
/// </para>
/// <para>
/// <b>Margin targets:</b>
/// <list type="bullet">
///   <item>In-home care (W-2): target <see cref="GrossMarginPercentage"/> 40-45%.</item>
///   <item>Facility staffing (1099): target <see cref="GrossMarginPercentage"/> 25-30%.</item>
/// </list>
/// </para>
/// </remarks>
public class Shift : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the client receiving care on this shift.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Navigation to the <see cref="Client"/>. Required.</summary>
    public Client Client { get; set; } = null!;

    /// <summary>
    /// Foreign key to the assigned caregiver. <c>null</c> when the shift is unassigned
    /// (open shift awaiting a caregiver to claim or be assigned by a coordinator).
    /// </summary>
    public Guid? CaregiverId { get; set; }

    /// <summary>Navigation to the assigned <see cref="Caregiver"/>. <c>null</c> if unassigned.</summary>
    public Caregiver? Caregiver { get; set; }

    // Scheduling

    /// <summary>UTC date/time the shift is scheduled to start.</summary>
    public DateTime ScheduledStartTime { get; set; }

    /// <summary>UTC date/time the shift is scheduled to end.</summary>
    public DateTime ScheduledEndTime { get; set; }

    /// <summary>
    /// UTC date/time the caregiver actually checked in. <c>null</c> if not yet started.
    /// May differ from <see cref="ScheduledStartTime"/> (late arrival or early check-in).
    /// </summary>
    public DateTime? ActualStartTime { get; set; }

    /// <summary>
    /// UTC date/time the caregiver actually checked out. <c>null</c> if shift not yet completed.
    /// </summary>
    public DateTime? ActualEndTime { get; set; }

    /// <summary>Current lifecycle status of the shift.</summary>
    public ShiftStatus Status { get; set; } = ShiftStatus.Scheduled;

    /// <summary>Service delivery model for this shift (in-home or facility).</summary>
    public ServiceType ServiceType { get; set; }

    // Financial Details (historical snapshot — copied at shift creation)

    /// <summary>
    /// Hourly rate billed to the client (USD). Copied from <c>Client.HourlyBillRate</c>
    /// at shift creation to preserve the rate at time of service.
    /// </summary>
    public decimal BillRate { get; set; }

    /// <summary>
    /// Hourly pay rate for the caregiver (USD). Copied from <c>Caregiver.HourlyPayRate</c>
    /// at shift creation to preserve the rate at time of service.
    /// </summary>
    public decimal PayRate { get; set; }

    /// <summary>
    /// Overtime pay rate for W-2 employees (typically 1.5× <see cref="PayRate"/>).
    /// <c>null</c> if not applicable (1099 contractors do not receive overtime).
    /// </summary>
    public decimal? OvertimePayRate { get; set; }

    /// <summary>Weekend pay premium in USD per hour. <c>null</c> if none applies.</summary>
    public decimal? WeekendPremium { get; set; }

    /// <summary>Holiday pay premium in USD per hour. <c>null</c> if none applies.</summary>
    public decimal? HolidayPremium { get; set; }

    // GPS Tracking (for in-home care geofencing)

    /// <summary>Latitude recorded at caregiver check-in. <c>null</c> if not yet checked in or not captured.</summary>
    public double? CheckInLatitude { get; set; }

    /// <summary>Longitude recorded at caregiver check-in. <c>null</c> if not yet checked in or not captured.</summary>
    public double? CheckInLongitude { get; set; }

    /// <summary>UTC timestamp of the caregiver's GPS check-in. <c>null</c> if not yet checked in.</summary>
    public DateTime? CheckInTime { get; set; }

    /// <summary>Latitude recorded at caregiver check-out. <c>null</c> if not yet checked out or not captured.</summary>
    public double? CheckOutLatitude { get; set; }

    /// <summary>Longitude recorded at caregiver check-out. <c>null</c> if not yet checked out or not captured.</summary>
    public double? CheckOutLongitude { get; set; }

    /// <summary>UTC timestamp of the caregiver's GPS check-out. <c>null</c> if not yet checked out.</summary>
    public DateTime? CheckOutTime { get; set; }

    // Break Time

    /// <summary>
    /// Unpaid break duration in minutes. Subtracted from total shift time when computing
    /// <see cref="BillableHours"/>. Required for labor-law compliance (Maryland break rules).
    /// Default: 0.
    /// </summary>
    public int BreakMinutes { get; set; }

    // Notes and Cancellation

    /// <summary>General notes added by the coordinator or caregiver about this shift.</summary>
    public string? Notes { get; set; }

    /// <summary>Reason provided when a shift is cancelled. Required when <see cref="Status"/> is <see cref="ShiftStatus.Cancelled"/>.</summary>
    public string? CancellationReason { get; set; }

    /// <summary>UTC timestamp when the shift was cancelled. <c>null</c> if not cancelled.</summary>
    public DateTime? CancelledAt { get; set; }

    // Navigation Collections

    /// <summary>Visit notes documented by the caregiver during this shift.</summary>
    public ICollection<VisitNote> VisitNotes { get; set; } = new List<VisitNote>();

    // Computed Properties (Business Logic)

    /// <summary>Duration from <see cref="ScheduledStartTime"/> to <see cref="ScheduledEndTime"/>.</summary>
    public TimeSpan ScheduledDuration => ScheduledEndTime - ScheduledStartTime;

    /// <summary>
    /// Actual shift duration. <c>null</c> if <see cref="ActualStartTime"/> or
    /// <see cref="ActualEndTime"/> has not been recorded yet.
    /// </summary>
    public TimeSpan? ActualDuration =>
        ActualStartTime.HasValue && ActualEndTime.HasValue
            ? ActualEndTime.Value - ActualStartTime.Value
            : null;

    /// <summary>
    /// Billable hours = (ActualEnd − ActualStart − <see cref="BreakMinutes"/>) / 60.
    /// Returns <c>0</c> if the shift has not been completed (either time is <c>null</c>).
    /// </summary>
    public decimal BillableHours
    {
        get
        {
            if (!ActualStartTime.HasValue || !ActualEndTime.HasValue)
                return 0m;

            var totalMinutes = (ActualEndTime.Value - ActualStartTime.Value).TotalMinutes - BreakMinutes;
            return totalMinutes <= 0 ? 0m : (decimal)(totalMinutes / 60.0);
        }
    }

    /// <summary>
    /// Total gross margin for this shift in USD: (BillRate − PayRate) × BillableHours.
    /// Returns <c>0</c> when the shift is not yet completed (no actual times recorded).
    /// </summary>
    public decimal GrossMargin => (BillRate - PayRate) * BillableHours;

    /// <summary>
    /// Gross margin as a percentage of total shift revenue: (GrossMargin / (BillRate × BillableHours)) × 100.
    /// Returns <c>0</c> when <see cref="BillRate"/> is zero or the shift is not yet completed.
    /// Target: 40-45% for in-home care (W-2); 25-30% for facility staffing (1099).
    /// </summary>
    public decimal GrossMarginPercentage =>
        BillRate > 0 && BillableHours > 0
            ? (GrossMargin / (BillRate * BillableHours)) * 100
            : 0m;
}
