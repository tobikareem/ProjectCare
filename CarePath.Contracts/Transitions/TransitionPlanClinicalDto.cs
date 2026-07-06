using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Full clinical plan view for Clinician/Coordinator review and activation screens. Clinical
/// PHI: reads audited; never returned on patient-facing or care-team routes (D-S5-3).
/// </summary>
public class TransitionPlanClinicalDto
{
    /// <summary>Plan identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Client identifier.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Client display name.</summary>
    public string ClientFullName { get; init; } = string.Empty;

    /// <summary>Source discharge document.</summary>
    public Guid DischargeDocumentId { get; init; }

    /// <summary>Discharging hospital, when recorded.</summary>
    public string? HospitalName { get; init; }

    /// <summary>Hospital discharge date (UTC).</summary>
    public DateTime DischargeDate { get; init; }

    /// <summary>End of the 30-day transition window (UTC), computed at activation (D-S5-5).</summary>
    public DateTime TransitionWindowEnd { get; init; }

    /// <summary>Plan workflow status.</summary>
    public TransitionPlanStatus Status { get; init; }

    /// <summary>Assessed readmission risk.</summary>
    public TransitionRiskLevel RiskLevel { get; init; }

    /// <summary>Clinician who verified/e-signed the plan (D-S5-5 e-signature).</summary>
    public Guid? VerifiedBy { get; init; }

    /// <summary>When the plan was verified (UTC).</summary>
    public DateTime? VerifiedAt { get; init; }

    /// <summary>When the plan was activated (UTC).</summary>
    public DateTime? ActivatedAt { get; init; }

    /// <summary>True while the plan is Active and inside its window, computed server-side.</summary>
    public bool IsActive { get; init; }

    /// <summary>Days remaining in the window (0 when past), computed server-side.</summary>
    public int DaysRemaining { get; init; }

    /// <summary>All instructions with review state, source text, and confidence.</summary>
    public IReadOnlyList<TransitionInstructionClinicalDto> Instructions { get; init; } = [];
}
