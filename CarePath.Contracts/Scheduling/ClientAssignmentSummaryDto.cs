using CarePath.Contracts.Enumerations;

namespace CarePath.Contracts.Scheduling;

/// <summary>Minimum-necessary client relationship summary for staff caregiver detail.</summary>
public sealed record ClientAssignmentSummaryDto(
    Guid ClientId,
    string ClientDisplayName,
    ServiceType ServiceType,
    DateTime FirstAssignedAtUtc,
    DateTime LastAssignedAtUtc,
    DateTime? LastShiftAtUtc,
    DateTime? NextShiftAtUtc,
    int CompletedShiftCount,
    AssignmentRelationshipStatus Status);
