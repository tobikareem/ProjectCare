namespace CarePath.Contracts.Clients;

/// <summary>
/// Request to update a care plan. Admin/Coordinator/Clinician. Clinical PHI — this request
/// body must never be logged.
/// </summary>
public class UpdateCarePlanRequest
{
    /// <summary>Plan title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Plan description. PHI — never log.</summary>
    public string? Description { get; init; }

    /// <summary>Plan end date (UTC), when bounded.</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>True while the plan is in effect.</summary>
    public bool IsActive { get; init; }

    /// <summary>Care goals. PHI — never log.</summary>
    public string? Goals { get; init; }

    /// <summary>Planned interventions. PHI — never log.</summary>
    public string? Interventions { get; init; }

    /// <summary>Clinical notes. PHI — never log.</summary>
    public string? Notes { get; init; }
}
