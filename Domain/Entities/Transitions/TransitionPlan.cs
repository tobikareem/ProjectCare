using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Transitions;

/// <summary>
/// The clinician-verified, activated 30-day post-discharge care plan for a client.
/// One plan per discharge episode. A plan must reach <see cref="TransitionPlanStatus.Active"/>
/// via clinician e-signature before any reminders are delivered to the patient.
/// </summary>
/// <remarks>
/// <b>PHI:</b> This entity contains clinical care plan data and must be treated as
/// Protected Health Information. All reads and writes must be audit-logged. Role-based
/// authorization must be enforced on every endpoint that accesses this entity.
/// </remarks>
public class TransitionPlan : BaseEntity
{
    /// <summary>Foreign key to the client this plan belongs to.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Navigation to the associated <see cref="Client"/>.</summary>
    public Client? Client { get; set; }

    /// <summary>Foreign key to the source discharge document this plan was generated from.</summary>
    public Guid DischargeDocumentId { get; set; }

    /// <summary>Navigation to the source <see cref="DischargeDocument"/>.</summary>
    public DischargeDocument? DischargeDocument { get; set; }

    /// <summary>Name of the discharging hospital or facility. Optional; used for reporting.</summary>
    public string? HospitalName { get; set; }

    /// <summary>UTC date the patient was discharged from the facility.</summary>
    public DateTime DischargeDate { get; set; }

    /// <summary>
    /// End of the 30-day monitoring window in UTC.
    /// Always set by the Application layer as <c>DischargeDate.AddDays(30)</c> before save.
    /// Never computed in local time.
    /// </summary>
    public DateTime TransitionWindowEnd { get; set; }

    /// <summary>Current lifecycle status of the plan.</summary>
    public TransitionPlanStatus Status { get; set; } = TransitionPlanStatus.Draft;

    /// <summary>
    /// Clinical risk stratification. Controls reminder frequency and escalation response times.
    /// Defaults to <see cref="TransitionRiskLevel.Medium"/> until explicitly assessed.
    /// </summary>
    public TransitionRiskLevel RiskLevel { get; set; } = TransitionRiskLevel.Medium;

    /// <summary>
    /// The <see cref="BaseEntity.Id"/> of the clinician who reviewed and e-signed this plan.
    /// Null until the plan reaches <see cref="TransitionPlanStatus.Active"/>.
    /// </summary>
    public Guid? VerifiedBy { get; set; }

    /// <summary>UTC timestamp when the clinician completed their review and signed the plan.</summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>UTC timestamp when the plan transitioned to <see cref="TransitionPlanStatus.Active"/>.</summary>
    public DateTime? ActivatedAt { get; set; }

    // ── Computed properties (pure C# — no EF involvement) ──────────────────────

    /// <summary>
    /// Returns <c>true</c> when the plan is in <see cref="TransitionPlanStatus.Active"/> status
    /// and the current UTC time is still within the 30-day monitoring window.
    /// </summary>
    public bool IsActive => Status == TransitionPlanStatus.Active && DateTime.UtcNow <= TransitionWindowEnd;

    /// <summary>
    /// Number of days remaining in the 30-day monitoring window.
    /// Returns <c>0</c> if the window has already ended — never a negative value.
    /// </summary>
    public int DaysRemaining => Math.Max(0, (TransitionWindowEnd - DateTime.UtcNow).Days);

    // ── Navigation collections ──────────────────────────────────────────────────

    /// <summary>Individual instructions (medications, appointments, etc.) extracted from the discharge document.</summary>
    public IReadOnlyList<TransitionInstruction> Instructions { get; private set; } = new List<TransitionInstruction>();

    /// <summary>Reminders scheduled and delivered to the patient during the monitoring window.</summary>
    public IReadOnlyList<TransitionReminder> Reminders { get; private set; } = new List<TransitionReminder>();

    /// <summary>Patient symptom and adherence check-in responses received during the monitoring window.</summary>
    public IReadOnlyList<TransitionCheckIn> CheckIns { get; private set; } = new List<TransitionCheckIn>();

    /// <summary>Escalation events triggered during the monitoring window.</summary>
    public IReadOnlyList<TransitionEscalation> Escalations { get; private set; } = new List<TransitionEscalation>();
}
