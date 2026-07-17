using CarePath.Contracts.Scheduling;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using CarePath.Infrastructure.Scheduling;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.Scheduling;

public sealed class AssignmentHistoryQueryTests
{
    [Fact]
    public async Task GetCaregiversForClientAsync_MixedShifts_GroupsFiltersOrdersAndPagesInDatabase()
    {
        var now = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var client = NewClient("Jordan", "Mitchell");
        var current = NewCaregiver("Amara", "Williams");
        var previous = NewCaregiver("David", "Okafor");
        context.AddRange(client, current, previous);
        context.Shifts.AddRange(
            NewShift(client, current, now.AddDays(-2), now.AddDays(-2).AddHours(4), ShiftStatus.Completed),
            NewShift(client, current, now.AddDays(1), now.AddDays(1).AddHours(4), ShiftStatus.Scheduled),
            NewShift(client, previous, now.AddDays(-10), now.AddDays(-10).AddHours(4), ShiftStatus.Completed),
            NewShift(client, previous, now.AddDays(2), now.AddDays(2).AddHours(4), ShiftStatus.Cancelled));
        await context.SaveChangesAsync();
        var query = new AssignmentHistoryQuery(context);

        var result = await query.GetCaregiversForClientAsync(client.Id, new AssignmentHistorySearchRequest { PageSize = 1 }, now);

        result.TotalCount.Should().Be(2);
        result.Items.Should().ContainSingle();
        result.Items[0].CaregiverId.Should().Be(current.Id);
        result.Items[0].Status.Should().Be(AssignmentRelationshipStatus.Current);
        result.Items[0].CompletedShiftCount.Should().Be(1);
    }

    [Fact]
    public async Task GetClientsForCaregiverAsync_SearchAndPreviousFilter_ReturnsMatchingRelationshipOnly()
    {
        var now = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var caregiver = NewCaregiver("Amara", "Williams");
        var jordan = NewClient("Jordan", "Mitchell");
        var casey = NewClient("Casey", "Rivera");
        context.AddRange(caregiver, jordan, casey);
        context.Shifts.AddRange(
            NewShift(jordan, caregiver, now.AddDays(-3), now.AddDays(-3).AddHours(4), ShiftStatus.Completed),
            NewShift(casey, caregiver, now.AddDays(1), now.AddDays(1).AddHours(4), ShiftStatus.Scheduled));
        await context.SaveChangesAsync();
        var query = new AssignmentHistoryQuery(context);

        var result = await query.GetClientsForCaregiverAsync(caregiver.Id, new AssignmentHistorySearchRequest { SearchTerm = "Jordan", Status = AssignmentRelationshipStatus.Previous }, now);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.ClientDisplayName.Should().Be("Jordan Mitchell");
    }

    private static CarePathDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>().UseSqlite(connection).Options;
        return new CarePathDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
    }

    private static Client NewClient(string firstName, string lastName)
    {
        var user = NewUser(firstName, lastName, UserRole.Client);
        return new Client { Id = Guid.NewGuid(), UserId = user.Id, User = user, DateOfBirth = new DateTime(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc), ServiceType = ServiceType.InHomeCare };
    }

    private static Caregiver NewCaregiver(string firstName, string lastName)
    {
        var user = NewUser(firstName, lastName, UserRole.Caregiver);
        return new Caregiver { Id = Guid.NewGuid(), UserId = user.Id, User = user, HireDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
    }

    private static User NewUser(string firstName, string lastName, UserRole role) => new() { Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName, Email = $"{Guid.NewGuid():N}@example.test", PhoneNumber = "555-0100", Role = role, IsActive = true };

    private static Shift NewShift(Client client, Caregiver caregiver, DateTime start, DateTime end, ShiftStatus status) => new() { Id = Guid.NewGuid(), ClientId = client.Id, Client = client, CaregiverId = caregiver.Id, Caregiver = caregiver, ScheduledStartTime = start, ScheduledEndTime = end, Status = status, ServiceType = ServiceType.InHomeCare };
}
