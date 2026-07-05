using CarePath.Domain.Enumerations;

namespace CarePath.Application.Scheduling.Commands;

public sealed record CreateShiftCommand(
    Guid ClientId,
    Guid CaregiverId,
    DateTime ScheduledStartUtc,
    DateTime ScheduledEndUtc,
    int? BreakMinutes,
    decimal BillRate,
    decimal PayRate,
    ServiceType ServiceType);