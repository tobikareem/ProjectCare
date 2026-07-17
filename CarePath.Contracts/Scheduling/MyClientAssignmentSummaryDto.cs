using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>Caregiver-self relationship summary with an abbreviated client display name.</summary>
public sealed record MyClientAssignmentSummaryDto(
    string ClientDisplayName,
    ServiceType ServiceType,
    DateTime FirstAssignedAtUtc,
    DateTime LastAssignedAtUtc,
    DateTime? NextShiftAtUtc,
    int CompletedShiftCount,
    AssignmentRelationshipStatus Status);
