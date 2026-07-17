using CarePath.Domain.Entities.Common;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;

namespace CarePath.Domain.Entities.Billing;

/// <summary>
/// Append-only record of an authorized non-billable decision (or its reopening) for one shift
/// (D-S6-18). Resolution history is immutable: corrections never edit or delete prior records —
/// reopening appends a new record whose <see cref="SupersedesResolutionId"/> points at the
/// decision it supersedes. A shift is considered resolved while its latest record's
/// <see cref="Reason"/> is not <see cref="BillingReconciliationReason.Reopened"/>.
/// </summary>
public class BillingReconciliationResolution : BaseEntity
{
    /// <summary>Foreign key to the <see cref="Shift"/> this decision covers.</summary>
    public Guid ShiftId { get; set; }

    /// <summary>Navigation to the shift. Deletion is restricted — clinical records are never cascaded.</summary>
    public Shift? Shift { get; set; }

    /// <summary>The recorded operational reason. PHI-free by definition.</summary>
    public BillingReconciliationReason Reason { get; set; }

    /// <summary>User ID of the staff member who recorded the decision.</summary>
    public Guid ResolvedByUserId { get; set; }

    /// <summary>When the decision was recorded (UTC).</summary>
    public DateTime ResolvedAtUtc { get; set; }

    /// <summary>Optional bounded note. PHI-free by policy — never clinical context.</summary>
    public string? Note { get; set; }

    /// <summary>Prior resolution superseded by this record (reopen linkage); null for first decisions.</summary>
    public Guid? SupersedesResolutionId { get; set; }

    /// <summary>Navigation to the superseded resolution.</summary>
    public BillingReconciliationResolution? SupersedesResolution { get; set; }
}
