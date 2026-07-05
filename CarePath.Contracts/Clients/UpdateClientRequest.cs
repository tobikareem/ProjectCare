using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Clients;

/// <summary>
/// Request to update a client profile. Admin/Coordinator only. Contains PHI — this request
/// body must never be logged. Name/email/date-of-birth corrections are separate,
/// Identity-adjacent operations and are not part of this request.
/// </summary>
public class UpdateClientRequest
{
    /// <summary>Contact phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

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
