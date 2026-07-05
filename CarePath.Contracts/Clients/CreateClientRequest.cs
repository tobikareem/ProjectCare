using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Clients;

/// <summary>
/// Request to create a client: Domain user + client profile + Identity account in one workflow
/// (D-S4-5). Admin/Coordinator only. Contains PHI — this request body must never be logged.
/// </summary>
/// <remarks>
/// CREDENTIAL SAFETY (D-S4-5): <see cref="TemporaryPassword"/> is one-time provisioning
/// material — never committed, logged, echoed in validation errors, or returned.
/// </remarks>
public class CreateClientRequest
{
    /// <summary>First name.</summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>Last name.</summary>
    public string LastName { get; init; } = string.Empty;

    /// <summary>Unique email address (becomes the Identity login).</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Contact phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>One-time temporary password for the new Identity account. Never logged or echoed.</summary>
    public string? TemporaryPassword { get; init; }

    /// <summary>Date of birth (UTC). PHI — never log.</summary>
    public DateTime DateOfBirth { get; init; }

    /// <summary>Emergency contact name.</summary>
    public string? EmergencyContactName { get; init; }

    /// <summary>Emergency contact phone.</summary>
    public string? EmergencyContactPhone { get; init; }

    /// <summary>Emergency contact relationship to the client.</summary>
    public string? EmergencyContactRelationship { get; init; }

    /// <summary>Requires dementia-trained caregivers.</summary>
    public bool RequiresDementiaCare { get; init; }

    /// <summary>Requires mobility assistance.</summary>
    public bool RequiresMobilityAssistance { get; init; }

    /// <summary>Requires medication management.</summary>
    public bool RequiresMedicationManagement { get; init; }

    /// <summary>Requires companionship services.</summary>
    public bool RequiresCompanionship { get; init; }

    /// <summary>Care delivery instructions. PHI — never log.</summary>
    public string? SpecialInstructions { get; init; }

    /// <summary>Medical conditions. PHI — never log.</summary>
    public string? MedicalConditions { get; init; }

    /// <summary>Allergies. PHI — never log.</summary>
    public string? Allergies { get; init; }

    /// <summary>In-home care or facility staffing.</summary>
    public ServiceType ServiceType { get; init; }

    /// <summary>Hourly bill rate.</summary>
    public decimal HourlyBillRate { get; init; }

    /// <summary>Estimated weekly service hours.</summary>
    public int EstimatedWeeklyHours { get; init; }
}
