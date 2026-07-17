using CarePath.Domain.Entities.Billing;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CarePath.Infrastructure.Tests.Billing;

/// <summary>
/// D-S6-18 persistence pins: the double-billing unique filtered index (deliberately NOT
/// excluding soft-deleted rows), append-only reconciliation configuration with restricted
/// deletes, and the fail-closed migration shape.
/// </summary>
public sealed class Sprint6BillingPersistenceTests
{
    private const string MigrationFileName = "20260717215349_AddBillingReconciliationAndShiftLineGuard.cs";

    [Fact]
    public void InvoiceLineItems_ShiftIdIndex_IsUniqueFilteredOnNonNullOnly()
    {
        var model = CreateModel();
        var entity = model.FindEntityType(typeof(InvoiceLineItem))!;
        var index = entity.GetIndexes()
            .Single(candidate => candidate.GetDatabaseName() == "UX_InvoiceLineItems_ShiftId_NotNull");

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be(
            "[ShiftId] IS NOT NULL",
            "historical soft-deleted lines must keep blocking rebilling — the filter must never mention IsDeleted");
    }

    [Fact]
    public void BillingReconciliationResolution_IsConfiguredAppendOnlySafe()
    {
        var model = CreateModel();
        var entity = model.FindEntityType(typeof(BillingReconciliationResolution))!;

        entity.GetTableName().Should().Be("BillingReconciliationResolutions");
        entity.FindProperty(nameof(BillingReconciliationResolution.Note))!.GetMaxLength().Should().Be(500);
        entity.GetForeignKeys().Should().OnlyContain(
            foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict,
            "resolution history must never be destroyed by a parent delete");
        entity.GetIndexes().Should().Contain(index =>
            index.GetDatabaseName() == "IX_BillingReconciliationResolutions_Shift_ResolvedAt");
    }

    [Fact]
    public void Migration_FailsClosedOnDuplicatesAndOnPopulatedRollback()
    {
        var migrationSource = ReadMigrationSource();

        var preflightPosition = migrationSource.IndexOf("THROW 51003", StringComparison.Ordinal);
        var indexPosition = migrationSource.IndexOf("UX_InvoiceLineItems_ShiftId_NotNull", StringComparison.Ordinal);
        preflightPosition.Should().BeGreaterThan(0, "the duplicate preflight must exist");
        indexPosition.Should().BeGreaterThan(preflightPosition, "the preflight must run before the unique index is created");
        migrationSource.Should().Contain("HAVING COUNT(*) > 1", "duplicates fail the migration rather than mutating data");
        migrationSource.Should().NotContain("DELETE FROM", "the preflight must never repair by deleting");
        migrationSource.Should().Contain("THROW 51004", "rollback fails closed once resolutions exist");
        migrationSource.Should().Contain("filter: \"[ShiftId] IS NOT NULL\"");
    }

    private static string ReadMigrationSource()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "CarePath.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        directory.Should().NotBeNull("the repository root must be locatable from the test output directory");
        var path = Path.Combine(directory!, "Infrastructure", "Migrations", MigrationFileName);
        File.Exists(path).Should().BeTrue($"migration {MigrationFileName} must exist");
        return File.ReadAllText(path);
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CarePath_MetadataOnly;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True")
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        using var context = new CarePathDbContext(options, interceptor);
        return context.Model;
    }
}
