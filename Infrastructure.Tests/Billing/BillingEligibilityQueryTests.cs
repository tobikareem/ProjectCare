using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Billing;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.Billing;

public sealed class BillingEligibilityQueryTests
{
    private static readonly DateTime PreviewPeriodStart = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PreviewPeriodEnd = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SharedShiftStart = new(2026, 6, 2, 13, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FirstShiftId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid SecondShiftId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    [Fact]
    public async Task GetPeriodRowsAsync_WhenRowsExist_OrdersBeforeProjectionAndTranslatesOnRelationalProvider()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CreateDbContext(connection);
        await context.Database.EnsureCreatedAsync();

        var clientUser = CreateUser(UserRole.Client);
        var client = new Client
        {
            UserId = clientUser.Id,
            User = clientUser,
            DateOfBirth = new DateTime(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ServiceType = ServiceType.InHomeCare,
            HourlyBillRate = 35m,
            EstimatedWeeklyHours = 10,
        };
        var secondShift = CreateShift(client, SharedShiftStart, SecondShiftId);
        var firstShift = CreateShift(client, SharedShiftStart, FirstShiftId);
        await context.DomainUsers.AddAsync(clientUser);
        await context.Clients.AddAsync(client);
        await context.Shifts.AddRangeAsync(secondShift, firstShift);
        await context.SaveChangesAsync();

        var query = new BillingEligibilityQuery(context);

        var rows = await query.GetPeriodRowsAsync(
            client.Id,
            ServiceType.InHomeCare,
            PreviewPeriodStart,
            PreviewPeriodEnd);

        rows.Select(row => row.ShiftId).Should().Equal(firstShift.Id, secondShift.Id);
        rows.Should().OnlyContain(row => row.Reason == BillingExclusionReason.Eligible);
    }

    [Fact]
    public async Task GetPeriodRowsAsync_WhenRunOnSqlServer_TranslatesCorrelatedProjection()
    {
        var databaseName = $"CarePath_BillingEligibility_{Guid.NewGuid():N}";
        var database = CreateSqlServerTestDatabase(databaseName);
        if (database is null)
        {
            return;
        }

        await using var context = CreateDbContext(database.ConnectionString);
        var databaseCreated = false;
        try
        {
            await context.Database.EnsureCreatedAsync();
            databaseCreated = true;
            var client = await SeedEligibleShiftsAsync(context);
            var query = new BillingEligibilityQuery(context);

            var rows = await query.GetPeriodRowsAsync(
                client.Id,
                ServiceType.InHomeCare,
                PreviewPeriodStart,
                PreviewPeriodEnd);

            rows.Select(row => row.ShiftId).Should().Equal(FirstShiftId, SecondShiftId);
        }
        catch (SqlException) when (!database.Required)
        {
            return;
        }
        finally
        {
            if (databaseCreated && OwnsGeneratedTestDatabase(database.ConnectionString, databaseName))
            {
                await context.Database.EnsureDeletedAsync();
            }
        }
    }

    private static CarePathDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseSqlite(connection)
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        return new CarePathDbContext(options, interceptor);
    }

    private static CarePathDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        return new CarePathDbContext(options, interceptor);
    }

    private static SqlServerTestDatabase? CreateSqlServerTestDatabase(string databaseName)
    {
        var configured = Environment.GetEnvironmentVariable("CAREPATH_TEST_SQLSERVER_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var placeholderCount = configured.Split("{DatabaseName}", StringSplitOptions.None).Length - 1;
            if (placeholderCount != 1)
            {
                throw new InvalidOperationException("CAREPATH_TEST_SQLSERVER_CONNECTION_STRING must contain exactly one {DatabaseName} placeholder.");
            }

            var connectionString = configured.Replace("{DatabaseName}", databaseName, StringComparison.Ordinal);
            EnsureGeneratedTestDatabaseName(connectionString, databaseName);
            return new SqlServerTestDatabase(connectionString, Required: true);
        }

        if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CAREPATH_TEST_SQLSERVER_CONNECTION_STRING is required in CI.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var localDbConnectionString = $@"Server=(localdb)\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        EnsureGeneratedTestDatabaseName(localDbConnectionString, databaseName);
        return new SqlServerTestDatabase(localDbConnectionString, Required: false);
    }

    private static bool OwnsGeneratedTestDatabase(string connectionString, string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return string.Equals(builder.InitialCatalog, databaseName, StringComparison.Ordinal)
            && databaseName.StartsWith("CarePath_BillingEligibility_", StringComparison.Ordinal);
    }

    private static void EnsureGeneratedTestDatabaseName(string connectionString, string databaseName)
    {
        if (!OwnsGeneratedTestDatabase(connectionString, databaseName))
        {
            throw new InvalidOperationException("SQL Server billing eligibility tests may only target generated CarePath_BillingEligibility_* databases.");
        }
    }

    private static async Task<Client> SeedEligibleShiftsAsync(CarePathDbContext context)
    {
        var clientUser = CreateUser(UserRole.Client);
        var client = new Client
        {
            UserId = clientUser.Id,
            User = clientUser,
            DateOfBirth = new DateTime(1950, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ServiceType = ServiceType.InHomeCare,
            HourlyBillRate = 35m,
            EstimatedWeeklyHours = 10,
        };
        var secondShift = CreateShift(client, SharedShiftStart, SecondShiftId);
        var firstShift = CreateShift(client, SharedShiftStart, FirstShiftId);

        await context.DomainUsers.AddAsync(clientUser);
        await context.Clients.AddAsync(client);
        await context.Shifts.AddRangeAsync(secondShift, firstShift);
        await context.SaveChangesAsync();
        return client;
    }

    private static User CreateUser(UserRole role) => new()
    {
        FirstName = "Synthetic",
        LastName = "User",
        Email = $"{Guid.NewGuid():N}@example.test",
        PhoneNumber = "555-0100",
        Role = role,
    };

    private static Shift CreateShift(Client client, DateTime startsAtUtc, Guid id) => new()
    {
        Id = id,
        ClientId = client.Id,
        Client = client,
        ServiceType = ServiceType.InHomeCare,
        Status = ShiftStatus.Completed,
        ScheduledStartTime = startsAtUtc,
        ScheduledEndTime = startsAtUtc.AddHours(4),
        ActualStartTime = startsAtUtc,
        ActualEndTime = startsAtUtc.AddHours(4),
        BreakMinutes = 0,
        BillRate = 35m,
        PayRate = 20m,
    };

    private sealed record SqlServerTestDatabase(string ConnectionString, bool Required);
}
