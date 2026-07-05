namespace CarePath.Contracts.Clients;

/// <summary>
/// A client's care plan. Clinical PHI: endpoints returning this DTO require role AND
/// object-level authorization, and every read is audit logged.
/// </summary>
public class CarePlanDto
{
    /// <summary>Care plan identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owning client identifier.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Plan title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Plan description. PHI — never log.</summary>
    public string? Description { get; init; }

    /// <summary>Plan start date (UTC).</summary>
    public DateTime StartDate { get; init; }

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
