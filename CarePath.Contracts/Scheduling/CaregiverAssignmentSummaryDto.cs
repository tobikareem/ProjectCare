namespace CarePath.Contracts.Scheduling;

/// <summary>Minimum-necessary caregiver relationship summary for staff client detail.</summary>
public sealed record CaregiverAssignmentSummaryDto(
    Guid CaregiverId,
    string CaregiverDisplayName,
    DateTime FirstAssignedAtUtc,
    DateTime LastAssignedAtUtc,
    DateTime? LastShiftAtUtc,
    DateTime? NextShiftAtUtc,
    int CompletedShiftCount,
    AssignmentRelationshipStatus Status);
