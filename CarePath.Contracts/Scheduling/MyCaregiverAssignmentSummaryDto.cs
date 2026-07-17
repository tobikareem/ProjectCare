namespace CarePath.Contracts.Scheduling;

/// <summary>Client-self caregiver relationship summary containing only care-team essentials.</summary>
public sealed record MyCaregiverAssignmentSummaryDto(
    string CaregiverDisplayName,
    DateTime FirstAssignedAtUtc,
    DateTime LastAssignedAtUtc,
    DateTime? NextShiftAtUtc,
    AssignmentRelationshipStatus Status);
