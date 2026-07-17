using CarePath.Application.Scheduling.Queries;
using CarePath.Contracts.Common;
using CarePath.Contracts.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ContractServiceType = CarePath.Contracts.Enumerations.ServiceType;
using ContractStatus = CarePath.Contracts.Scheduling.AssignmentRelationshipStatus;

namespace CarePath.Infrastructure.Scheduling;

/// <summary>Executes assignment aggregation, filtering, ordering, and paging in SQL.</summary>
public sealed class AssignmentHistoryQuery : IAssignmentHistoryQuery
{
    private readonly CarePathDbContext dbContext;

    /// <summary>Initializes the query service.</summary>
    public AssignmentHistoryQuery(CarePathDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CaregiverAssignmentSummaryDto>> GetCaregiversForClientAsync(
        Guid clientId,
        AssignmentHistorySearchRequest request,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var shifts = dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.ClientId == clientId && shift.CaregiverId != null && shift.Status != ShiftStatus.Cancelled);

        var searchTerm = request.SearchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{EscapeLikePattern(searchTerm)}%";
            shifts = shifts.Where(shift => EF.Functions.Like(
                shift.Caregiver!.User.FirstName + " " + shift.Caregiver.User.LastName,
                pattern,
                "\\"));
        }

        var summaries = shifts
            .GroupBy(shift => new
            {
                CaregiverId = shift.CaregiverId!.Value,
                shift.Caregiver!.User.FirstName,
                shift.Caregiver.User.LastName,
            })
            .Select(group => new
            {
                group.Key.CaregiverId,
                DisplayName = group.Key.FirstName + " " + group.Key.LastName,
                FirstAssignedAtUtc = group.Min(shift => shift.ScheduledStartTime),
                LastAssignedAtUtc = group.Max(shift => shift.ScheduledEndTime),
                LastShiftAtUtc = group.Max(shift => shift.ScheduledStartTime < utcNow ? (DateTime?)shift.ScheduledStartTime : null),
                NextShiftAtUtc = group.Min(shift =>
                    shift.Status == ShiftStatus.Scheduled && shift.ScheduledStartTime >= utcNow
                        ? (DateTime?)shift.ScheduledStartTime
                        : null),
                CompletedShiftCount = group.Count(shift => shift.Status == ShiftStatus.Completed),
                IsCurrent = group.Count(shift =>
                    (shift.Status == ShiftStatus.Scheduled || shift.Status == ShiftStatus.InProgress) && shift.ScheduledEndTime >= utcNow) > 0,
            });

        if (request.FromUtc.HasValue)
        {
            summaries = summaries.Where(item => item.LastAssignedAtUtc > request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            summaries = summaries.Where(item => item.FirstAssignedAtUtc < request.ToUtc.Value);
        }

        if (request.Status == ContractStatus.Current)
        {
            summaries = summaries.Where(item => item.IsCurrent);
        }
        else if (request.Status == ContractStatus.Previous)
        {
            summaries = summaries.Where(item => !item.IsCurrent);
        }
        var totalCount = await summaries.CountAsync(cancellationToken);
        var items = await summaries
            .OrderByDescending(item => item.IsCurrent)
            .ThenByDescending(item => item.NextShiftAtUtc ?? item.LastAssignedAtUtc)
            .ThenBy(item => item.CaregiverId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new CaregiverAssignmentSummaryDto(
                item.CaregiverId,
                item.DisplayName,
                item.FirstAssignedAtUtc,
                item.LastAssignedAtUtc,
                item.LastShiftAtUtc,
                item.NextShiftAtUtc,
                item.CompletedShiftCount,
                item.IsCurrent ? ContractStatus.Current : ContractStatus.Previous))
            .ToArrayAsync(cancellationToken);

        return CreatePage(items, totalCount, request);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ClientAssignmentSummaryDto>> GetClientsForCaregiverAsync(
        Guid caregiverId,
        AssignmentHistorySearchRequest request,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var shifts = dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.CaregiverId == caregiverId && shift.Status != ShiftStatus.Cancelled);

        var searchTerm = request.SearchTerm?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{EscapeLikePattern(searchTerm)}%";
            shifts = shifts.Where(shift => EF.Functions.Like(
                shift.Client.User.FirstName + " " + shift.Client.User.LastName,
                pattern,
                "\\"));
        }

        var summaries = shifts
            .GroupBy(shift => new
            {
                shift.ClientId,
                shift.Client.User.FirstName,
                shift.Client.User.LastName,
                shift.Client.ServiceType,
            })
            .Select(group => new
            {
                group.Key.ClientId,
                DisplayName = group.Key.FirstName + " " + group.Key.LastName,
                group.Key.ServiceType,
                FirstAssignedAtUtc = group.Min(shift => shift.ScheduledStartTime),
                LastAssignedAtUtc = group.Max(shift => shift.ScheduledEndTime),
                LastShiftAtUtc = group.Max(shift => shift.ScheduledStartTime < utcNow ? (DateTime?)shift.ScheduledStartTime : null),
                NextShiftAtUtc = group.Min(shift =>
                    shift.Status == ShiftStatus.Scheduled && shift.ScheduledStartTime >= utcNow
                        ? (DateTime?)shift.ScheduledStartTime
                        : null),
                CompletedShiftCount = group.Count(shift => shift.Status == ShiftStatus.Completed),
                IsCurrent = group.Count(shift =>
                    (shift.Status == ShiftStatus.Scheduled || shift.Status == ShiftStatus.InProgress) && shift.ScheduledEndTime >= utcNow) > 0,
            });

        if (request.FromUtc.HasValue)
        {
            summaries = summaries.Where(item => item.LastAssignedAtUtc > request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            summaries = summaries.Where(item => item.FirstAssignedAtUtc < request.ToUtc.Value);
        }

        if (request.Status == ContractStatus.Current)
        {
            summaries = summaries.Where(item => item.IsCurrent);
        }
        else if (request.Status == ContractStatus.Previous)
        {
            summaries = summaries.Where(item => !item.IsCurrent);
        }
        var totalCount = await summaries.CountAsync(cancellationToken);
        var items = await summaries
            .OrderByDescending(item => item.IsCurrent)
            .ThenByDescending(item => item.NextShiftAtUtc ?? item.LastAssignedAtUtc)
            .ThenBy(item => item.ClientId)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new ClientAssignmentSummaryDto(
                item.ClientId,
                item.DisplayName,
                (ContractServiceType)(int)item.ServiceType,
                item.FirstAssignedAtUtc,
                item.LastAssignedAtUtc,
                item.LastShiftAtUtc,
                item.NextShiftAtUtc,
                item.CompletedShiftCount,
                item.IsCurrent ? ContractStatus.Current : ContractStatus.Previous))
            .ToArrayAsync(cancellationToken);

        return CreatePage(items, totalCount, request);
    }

    private static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private static PagedResult<T> CreatePage<T>(IReadOnlyList<T> items, int totalCount, AssignmentHistorySearchRequest request) => new()
    {
        Items = items,
        PageNumber = request.PageNumber,
        PageSize = request.PageSize,
        TotalCount = totalCount,
    };

}
