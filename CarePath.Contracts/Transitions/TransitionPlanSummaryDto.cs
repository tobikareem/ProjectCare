using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Coordinator dashboard row: plan progress and workload signals, no instruction content.
/// </summary>
public class TransitionPlanSummaryDto
{
    /// <summary>Plan identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Client identifier.</summary>
    public Guid ClientId { get; init; }

    /// <summary>Client display name.</summary>
    public string ClientFullName { get; init; } = string.Empty;

    /// <summary>Discharging hospital, when recorded.</summary>
    public string? HospitalName { get; init; }

    /// <summary>Hospital discharge date (UTC).</summary>
    public DateTime DischargeDate { get; init; }

    /// <summary>End of the 30-day transition window (UTC).</summary>
    public DateTime TransitionWindowEnd { get; init; }

    /// <summary>Plan workflow status.</summary>
    public TransitionPlanStatus Status { get; init; }

    /// <summary>Assessed readmission risk.</summary>
    public TransitionRiskLevel RiskLevel { get; init; }

    /// <summary>Days remaining in the window (0 when past), computed server-side.</summary>
    public int DaysRemaining { get; init; }

    /// <summary>Instructions still awaiting clinician review.</summary>
    public int PendingInstructionCount { get; init; }

    /// <summary>Escalations not yet acknowledged by a coordinator.</summary>
    public int OpenEscalationCount { get; init; }
}
