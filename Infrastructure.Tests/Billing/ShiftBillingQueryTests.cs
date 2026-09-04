using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Billing;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.Billing;

public class ShiftBillingQueryTests
{
    private static readonly DateTime PeriodStart = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

    private static readonly Guid WellOverBreakId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OneSecondOverBreakId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ExactlyBreakId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UnderBreakId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid MissingActualsId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid NotCompletedId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public void Query_OnSqlServer_TranslatesBillablePredicateToDateAddInSql()
    {
        // Arrange
        using var context = CreateMetadataOnlyContext(useNpgsql: false);

        // Act
        var sql = BuildFilteredQuery(context).ToQueryString();

        // Assert
        sql.Should().Contain("DATEADD(minute");
        sql.Should().Contain("[ActualEndTime] > DATEADD");
        sql.Should().NotContain("DATEDIFF");
    }

    [Fact]
    public void Query_OnPostgreSql_TranslatesBillablePredicateToIntervalInSql()
    {
        // Arrange
        using var context = CreateMetadataOnlyContext(useNpgsql: true);

        // Act
        var sql = BuildFilteredQuery(context).ToQueryString();

        // Assert
        sql.Should().Contain("AS interval");
        sql.Should().Contain("\"ActualEndTime\" > s.\"ActualStartTime\" +");
        sql.Should().NotContain("DATEDIFF");
    }

    [Fact]
    public async Task GetCompletedBillableShiftsAsync_FiltersInTheDatabase_OnBillableBoundaries()
    {
        // Arrange
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        using var context = CreateSqliteContext(connection);
        await context.Database.EnsureCreatedAsync();
        await SeedBoundaryShiftsAsync(context);
        var query = new ShiftBillingQuery(context);

        // Act
        var shifts = await query.GetCompletedBillableShiftsAsync(PeriodStart, PeriodEnd);

        // Assert
        shifts.Select(shift => shift.Id).Should().BeEquivalentTo([WellOverBreakId, OneSecondOverBreakId]);
    }

    [Fact]
    public async Task GetCompletedBillableShiftsAsync_WhenRemainderIsUnderOneMinute_StillBillable()
    {
        // Arrange — the case DATEDIFF(minute, ...) used to exclude: 30m30s worked, 30m break.
        using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        using var context = CreateSqliteContext(connection);
        await context.Database.EnsureCreatedAsync();
        await SeedBoundaryShiftsAsync(context);
        var query = new ShiftBillingQuery(context);

        // Act
        var shifts = await query.GetCompletedBillableShiftsAsync(PeriodStart, PeriodEnd);

        // Assert — agrees with the domain rule, which reports positive billable hours here.
        var subMinute = shifts.Single(shift => shift.Id == OneSecondOverBreakId);
        subMinute.BillableHours.Should().BeGreaterThan(0m);
    }

    private static IQueryable<Shift> BuildFilteredQuery(CarePathDbContext context)
    {
        return context.Shifts.Where(shift =>
            shift.Status == ShiftStatus.Completed
            && shift.ActualStartTime.HasValue
            && shift.ActualEndTime.HasValue
            && shift.ActualEndTime.Value > shift.ActualStartTime.Value.AddMinutes(shift.BreakMinutes));
    }

    private static CarePathDbContext CreateMetadataOnlyContext(bool useNpgsql)
    {
        var builder = new DbContextOptionsBuilder<CarePathDbContext>();
        if (useNpgsql)
        {
            builder.UseNpgsql("Host=metadata-only;Database=carepath;Username=none");
        }
        else
        {
            builder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CarePath_MetadataOnly;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True");
        }

        return new CarePathDbContext(builder.Options, new AuditableEntityInterceptor(new HttpContextAccessor()));
    }

    private static CarePathDbContext CreateSqliteContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseSqlite(connection)
            .Options;

        return new CarePathDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
    }

    private static async Task SeedBoundaryShiftsAsync(CarePathDbContext context)
    {
        var clientUser = new User
        {
            FirstName = "Boundary",
            LastName = "Client",
            Email = $"{Guid.NewGuid():N}@example.test",
            PhoneNumber = "555-0100",
            Role = UserRole.Client,
        };
        var client = new Client
        {
            UserId = clientUser.Id,
            User = clientUser,
            DateOfBirth = new DateTime(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ServiceType = ServiceType.InHomeCare,
            HourlyBillRate = 35m,
            EstimatedWeeklyHours = 10,
        };

        await context.DomainUsers.AddAsync(clientUser);
        await context.Clients.AddAsync(client);
        await context.Shifts.AddRangeAsync(
            // 4h worked, 30m break — comfortably billable.
            CreateShift(client, WellOverBreakId, ShiftStart.AddHours(4), breakMinutes: 30),
            // 30m30s worked, 30m break — 30 seconds billable. DATEDIFF(minute) reported 30
            // and excluded this; the instant comparison includes it, matching BillableHours.
            CreateShift(client, OneSecondOverBreakId, ShiftStart.AddMinutes(30).AddSeconds(30), breakMinutes: 30),
            // Exactly the break — zero billable time, excluded.
            CreateShift(client, ExactlyBreakId, ShiftStart.AddMinutes(30), breakMinutes: 30),
            // Break longer than the shift — negative billable time, excluded.
            CreateShift(client, UnderBreakId, ShiftStart.AddMinutes(20), breakMinutes: 30),
            // Never clocked in or out.
            CreateShift(client, MissingActualsId, actualEnd: null, breakMinutes: 0),
            // Billable duration, but not completed.
            CreateShift(client, NotCompletedId, ShiftStart.AddHours(4), breakMinutes: 0, status: ShiftStatus.InProgress));

        await context.SaveChangesAsync();
    }

    private static Shift CreateShift(
        Client client,
        Guid id,
        DateTime? actualEnd,
        int breakMinutes,
        ShiftStatus status = ShiftStatus.Completed) => new()
    {
        Id = id,
        ClientId = client.Id,
        Client = client,
        ServiceType = ServiceType.InHomeCare,
        Status = status,
        ScheduledStartTime = ShiftStart,
        ScheduledEndTime = ShiftStart.AddHours(4),
        ActualStartTime = actualEnd is null ? null : ShiftStart,
        ActualEndTime = actualEnd,
        BreakMinutes = breakMinutes,
        BillRate = 35m,
        PayRate = 20m,
    };
}
