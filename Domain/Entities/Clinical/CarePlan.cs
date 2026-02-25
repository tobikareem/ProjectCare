using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Identity;

namespace CarePath.Domain.Entities.Clinical;

/// <summary>
/// Care plan documenting the goals, interventions, and outcomes for a client's care programme.
/// A client may have multiple care plans over time (e.g., one per care episode or annual review).
/// Only one plan should be active at a time; use <see cref="IsActive"/> to identify the current plan.
/// </summary>
/// <remarks>
/// <para>
/// <b>PHI fields:</b> <see cref="Goals"/>, <see cref="Interventions"/>, and <see cref="Notes"/>
/// may contain Protected Health Information (care needs, diagnoses, instructions) and must be
/// treated with the same access-control and audit-logging requirements as other PHI fields.
/// </para>
/// <para>
/// <b>Active plan invariant:</b> Only one care plan per client should have <see cref="IsActive"/> = <c>true</c>
/// at any point in time. Enforce this in the Application layer when creating a new plan by
/// deactivating all existing active plans for the client first.
/// </para>
/// </remarks>
public class CarePlan : BaseEntity
{
    // Foreign Keys and Navigation

    /// <summary>Foreign key to the client this care plan belongs to.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Navigation to the <see cref="Client"/>. Required.</summary>
    public Client Client { get; set; } = null!;

    // Plan Details

    /// <summary>Short descriptive title of the care plan (e.g., "2026 Annual Care Plan — Post-Hip Replacement").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Narrative description of the care plan's scope and purpose.</summary>
    public string? Description { get; set; }

    /// <summary>UTC date the care plan takes effect.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>UTC date the care plan is scheduled to end. <c>null</c> for open-ended plans.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Indicates whether this is the client's current active care plan.
    /// Default: <c>true</c>. Set to <c>false</c> when superseded by a newer plan.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Clinical Content (PHI — apply access control consistent with other PHI fields)

    /// <summary>
    /// Measurable care goals (e.g., "Maintain independent ambulation with walker",
    /// "Reduce fall risk by strengthening lower extremities"). Free text. PHI.
    /// </summary>
    public string? Goals { get; set; }

    /// <summary>
    /// Planned care interventions to achieve the goals (e.g., "Daily ROM exercises",
    /// "Medication reminder twice daily"). Free text. PHI.
    /// </summary>
    public string? Interventions { get; set; }

    /// <summary>Additional notes from the coordinating nurse or care manager. PHI.</summary>
    public string? Notes { get; set; }
}
