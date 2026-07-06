using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Transitions;

/// <summary>
/// Clinician e-sign activation payload (D-S5-5). The server enforces: Clinician role, plan in
/// PendingVerification, no Pending instructions, and computes the 30-day window server-side.
/// The e-signature is the authenticated clinician identity + server clock recorded on the plan.
/// </summary>
public class ActivatePlanRequest
{
    /// <summary>Assessed readmission risk, set at activation (controls reminder intensity).</summary>
    public TransitionRiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Explicit e-sign confirmation. Must be <c>true</c>; guards against accidental activation
    /// by generic API tooling.
    /// </summary>
    public bool ConfirmESignature { get; init; }
}
