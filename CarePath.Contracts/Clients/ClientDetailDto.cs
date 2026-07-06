using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Clients;

/// <summary>
/// Full client view for Coordinator/Clinician screens. PHI-heavy: endpoints returning this DTO
/// require role AND object-level authorization, and every read is audit logged.
/// Deliberately excluded even here: insurance identifiers (InsuranceProvider, PolicyNumber,
/// MedicaidNumber) and raw GPS coordinates — those get separate, narrower billing/dispatch
/// contracts if a workflow ever needs them client-side.
/// </summary>
public class ClientDetailDto
{
    /// <summary>Client identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Linked domain user identifier.</summary>
    public Guid UserId { get; init; }

    /// <summary>Display name ("First Last").</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Contact phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Date of birth (UTC). PHI.</summary>
    public DateTime DateOfBirth { get; init; }

    /// <summary>Age in years, computed server-side.</summary>
    public int Age { get; init; }

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


    /// <summary>Estimated weekly service hours.</summary>
    public int EstimatedWeeklyHours { get; init; }
}
