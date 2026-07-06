using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Care-team-safe plan view for assigned caregivers (D-S5-3): operational context (status,
/// risk, window) plus approved instructions only. NEVER includes source text, confidence
/// scores, clinician notes, review states, or <c>ResponsesJson</c>.
/// </summary>
public class TransitionPlanCareTeamDto
{
    /// <summary>Plan identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Client identifier.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Discharging hospital, when recorded.</summary>
    public string? HospitalName { get; init; }

    /// <summary>Hospital discharge date (UTC).</summary>
    public DateTime DischargeDate { get; init; }

    /// <summary>End of the 30-day transition window (UTC).</summary>
    public DateTime TransitionWindowEnd { get; init; }

    /// <summary>Plan workflow status.</summary>
    public TransitionPlanStatus Status { get; init; }

    /// <summary>Assessed readmission risk (helps caregivers prioritize observations).</summary>
    public TransitionRiskLevel RiskLevel { get; init; }

    /// <summary>True while the plan is Active and inside its window.</summary>
    public bool IsActive { get; init; }

    /// <summary>Days remaining in the window (0 when past).</summary>
    public int DaysRemaining { get; init; }

    /// <summary>Clinician-approved instructions (Approved/Modified only).</summary>
    public IReadOnlyList<TransitionInstructionPatientFacingDto> Instructions { get; init; } = [];
}
