using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Identity;

/// <summary>
/// Client (care recipient) profile entity. Stores personal details, care requirements,
/// medical information (PHI), GPS location for geofencing, and billing details.
/// </summary>
/// <remarks>
/// <para>
/// <b>PHI fields:</b> <see cref="MedicalConditions"/> and <see cref="Allergies"/> contain
/// Protected Health Information and must be encrypted at rest. This is enforced in the
/// Infrastructure layer via EF Core value converters.
/// </para>
/// <para>
/// <b>Caregiver matching:</b> The <c>Requires*</c> care-requirement flags are compared against
/// the caregiver's <c>Has*</c> skill flags and <see cref="CaregiverCertification"/> records
/// when scheduling coordinators assign a shift.
/// </para>
/// <para>
/// <b>GPS geofencing:</b> <see cref="Latitude"/> and <see cref="Longitude"/> define the
/// client's service location. The mobile app validates caregiver check-in proximity
/// at shift start and check-out at shift end.
/// </para>
/// </remarks>
public class Client : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the <see cref="User"/> account that owns this profile.</summary>
    public Guid UserId { get; set; }

    /// <summary>Navigation to the owning <see cref="User"/>. Required.</summary>
    public User User { get; set; } = null!;

    // Personal Details

    /// <summary>Client's date of birth. Used to compute <see cref="Age"/> and for insurance eligibility.</summary>
    public DateTime DateOfBirth { get; set; }

    /// <summary>Full name of the primary emergency contact person.</summary>
    public string? EmergencyContactName { get; set; }

    /// <summary>Phone number of the emergency contact (US format).</summary>
    public string? EmergencyContactPhone { get; set; }

    /// <summary>Relationship of the emergency contact to the client (e.g., "Daughter", "Spouse").</summary>
    public string? EmergencyContactRelationship { get; set; }

    // Care Requirements (used for caregiver-client matching)

    /// <summary>Client requires a caregiver qualified in dementia care.</summary>
    public bool RequiresDementiaCare { get; set; }

    /// <summary>Client requires physical mobility assistance (transfers, ambulation support).</summary>
    public bool RequiresMobilityAssistance { get; set; }

    /// <summary>Client requires assistance with or administration of medications.</summary>
    public bool RequiresMedicationManagement { get; set; }

    /// <summary>Client requires companionship services (social engagement, activities).</summary>
    public bool RequiresCompanionship { get; set; }

    /// <summary>
    /// Free-text special instructions for caregivers (e.g., "Use Hoyer lift for all transfers",
    /// "Client prefers lights dimmed in the afternoon"). Shown on the caregiver's shift brief.
    /// </summary>
    public string? SpecialInstructions { get; set; }

    /// <summary>
    /// Client's known medical conditions (PHI — encrypt at rest).
    /// Example: "Type 2 Diabetes, Hypertension, Stage 3 CKD".
    /// </summary>
    public string? MedicalConditions { get; set; }

    /// <summary>
    /// Client's known allergies (PHI — encrypt at rest).
    /// Example: "Penicillin, Peanuts, Latex".
    /// </summary>
    public string? Allergies { get; set; }

    // Service Details

    /// <summary>
    /// Care delivery model. Determines billing rate range and margin target.
    /// Default: <see cref="ServiceType.InHomeCare"/> ($30-45/hr, 40-45% margin).
    /// </summary>
    public ServiceType ServiceType { get; set; } = ServiceType.InHomeCare;

    /// <summary>
    /// Hourly rate charged to the client in USD. Copied to <c>Shift.BillRate</c> at shift
    /// creation to preserve the historical rate at time of service.
    /// Typical range: $30-$90 depending on <see cref="ServiceType"/> and service level.
    /// </summary>
    public decimal HourlyBillRate { get; set; }

    /// <summary>Expected weekly care hours. Used for workforce planning and invoicing estimates.</summary>
    public int EstimatedWeeklyHours { get; set; }

    // GPS for Check-In Verification (geofencing)

    /// <summary>
    /// Latitude of the client's service location. Used to verify caregiver
    /// check-in proximity via geofencing in the mobile app.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude of the client's service location. Used to verify caregiver
    /// check-in proximity via geofencing in the mobile app.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Human-readable location notes (e.g., "Park in the rear driveway; gate code 4321").
    /// Displayed to caregiver before shift check-in.
    /// </summary>
    public string? LocationNotes { get; set; }

    // Billing

    /// <summary>Name of the client's private insurance carrier (e.g., "Blue Cross Blue Shield of Maryland").</summary>
    public string? InsuranceProvider { get; set; }

    /// <summary>Insurance policy or member number used for claims submission.</summary>
    public string? InsurancePolicyNumber { get; set; }

    /// <summary>
    /// Maryland Medicaid (Medical Assistance Program) recipient number.
    /// Claims submitted to Maryland MMIS. Required when <c>PaymentMethod.Medicaid</c> is used.
    /// </summary>
    public string? MedicaidNumber { get; set; }

    // Navigation Collections

    /// <summary>All shifts scheduled for this client.</summary>
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    /// <summary>All care plans associated with this client.</summary>
    public ICollection<CarePlan> CarePlans { get; set; } = new List<CarePlan>();

    /// <summary>All invoices issued to this client.</summary>
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    // Computed Properties

    /// <summary>
    /// Age in whole years, computed from <see cref="DateOfBirth"/> relative to the current UTC date.
    /// Correctly handles leap years (e.g., clients born on Feb 29).
    /// </summary>
    public int Age
    {
        get
        {
            var today = DateTime.UtcNow;
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}
