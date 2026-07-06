namespace CarePath.Contracts.Transitions;

/// <summary>
/// Patient/family view of an active plan (D-S5-3): approved patient-facing instructions only.
/// NEVER includes review status, risk level, verifier identities, source text, confidence
/// scores, clinician notes, or <c>ResponsesJson</c>. Served only to the client themself or a
/// grantee under an unrevoked access grant.
/// </summary>
public class TransitionPlanPatientFacingDto
{
    /// <summary>Plan identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Discharging hospital, when recorded.</summary>
    public string? HospitalName { get; init; }

    /// <summary>Hospital discharge date (UTC).</summary>
    public DateTime DischargeDate { get; init; }

    /// <summary>End of the 30-day support window (UTC).</summary>
    public DateTime TransitionWindowEnd { get; init; }

    /// <summary>True while the plan is active.</summary>
    public bool IsActive { get; init; }

    /// <summary>Days remaining in the support window (0 when past).</summary>
    public int DaysRemaining { get; init; }

    /// <summary>Clinician-approved instructions (Approved/Modified only).</summary>
    public IReadOnlyList<TransitionInstructionPatientFacingDto> Instructions { get; init; } = [];
}
