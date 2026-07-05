namespace CarePath.Contracts.Clients;

/// <summary>
/// Request to create a care plan (client ID travels in the route). Admin/Coordinator/Clinician.
/// Clinical PHI — this request body must never be logged.
/// </summary>
public class CreateCarePlanRequest
{
    /// <summary>Plan title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Plan description. PHI — never log.</summary>
    public string? Description { get; init; }

    /// <summary>Plan start date (UTC).</summary>
    public DateTime StartDate { get; init; }

    /// <summary>Plan end date (UTC), when bounded. Must be after <see cref="StartDate"/>.</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>Care goals. PHI — never log.</summary>
    public string? Goals { get; init; }

    /// <summary>Planned interventions. PHI — never log.</summary>
    public string? Interventions { get; init; }

    /// <summary>Clinical notes. PHI — never log.</summary>
    public string? Notes { get; init; }
}
