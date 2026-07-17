namespace CarePath.Contracts.Clients;

/// <summary>
/// Minimum-necessary care plan row for authorized list views (D-S6-14). Carries schedule
/// metadata and the plan title only; the clinical narrative fields (description, goals,
/// interventions, notes) are deliberately excluded and are served exclusively by the
/// audited per-plan detail read at <c>GET /api/care-plans/{id}</c>.
/// </summary>
/// <remarks>
/// <see cref="Title"/> remains healthcare-context data: the list route still requires the
/// same role and object-level authorization as the detail read, and titles are never logged.
/// </remarks>
public class CarePlanSummaryDto
{
    /// <summary>Care plan identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owning client identifier.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Plan title. Healthcare-context data — never log.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Plan start date (UTC).</summary>
    public DateTime StartDate { get; init; }

    /// <summary>Plan end date (UTC), when bounded.</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>True while the plan is in effect.</summary>
    public bool IsActive { get; init; }
}
