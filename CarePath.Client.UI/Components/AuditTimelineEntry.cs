namespace CarePath.Client.UI.Components;

/// <summary>
/// One entry for <c>AuditTimeline</c>. Presentation-only (D-S6-4): pages supply entries; no
/// audit-log read API exists yet. All values must be PHI-free (actions and actor roles/names
/// of staff, never patient data).
/// </summary>
/// <param name="TimestampUtc">When the event occurred (UTC).</param>
/// <param name="Action">Short action label (e.g., "Plan activated").</param>
/// <param name="Actor">Acting staff member's display name or role.</param>
/// <param name="Description">Optional PHI-free detail line.</param>
public sealed record AuditTimelineEntry(
    DateTime TimestampUtc,
    string Action,
    string Actor,
    string? Description = null);
