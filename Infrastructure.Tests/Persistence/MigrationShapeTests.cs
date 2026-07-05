using System.Text.RegularExpressions;
using FluentAssertions;

namespace CarePath.Infrastructure.Tests.Persistence;

public class MigrationShapeTests
{
    private const string InitialMigrationFile = "20260628003352_InitialCreate.cs";

    [Fact]
    public void InitialCreateMigration_WhenGenerated_CreatesExpectedCp01TablesOnly()
    {
        // Arrange
        var migrationText = ReadInitialMigration();
        var expectedTables = new[]
        {
            "Users",
            "Caregivers",
            "CaregiverCertifications",
            "Clients",
            "CarePlans",
            "Shifts",
            "VisitNotes",
            "VisitPhotos",
            "Invoices",
            "InvoiceLineItems",
            "Payments",
            "AspNetUsers"
        };

        // Assert
        foreach (var tableName in expectedTables)
        {
            migrationText.Should().Contain($"name: \"{tableName}\"");
        }

        migrationText.Should().NotContain("Transition");
        migrationText.Should().NotContain("Discharge");
    }

    [Fact]
    public void InitialCreateMigration_WhenGenerated_DoesNotContainDestructiveDataOperationsOrRawPhiColumns()
    {
        // Arrange
        var migrationText = ReadInitialMigration();

        // Assert
        migrationText.Should().NotContain("DeleteData");
        migrationText.Should().NotContain("TRUNCATE");
        migrationText.Should().NotContain("RawContent");
        migrationText.Should().NotContain("SourceText");
    }

    [Fact]
    public void InitialCreateMigration_WhenGenerated_RestrictsPhiForeignKeys()
    {
        // Arrange
        var migrationText = ReadInitialMigration();
        var phiForeignKeys = new[]
        {
            "FK_AspNetUsers_Users_DomainUserId",
            "FK_Caregivers_Users_UserId",
            "FK_Clients_Users_UserId",
            "FK_CaregiverCertifications_Caregivers_CaregiverId",
            "FK_CarePlans_Clients_ClientId",
            "FK_Invoices_Clients_ClientId",
            "FK_Shifts_Caregivers_CaregiverId",
            "FK_Shifts_Clients_ClientId",
            "FK_Payments_Invoices_InvoiceId",
            "FK_InvoiceLineItems_Invoices_InvoiceId",
            "FK_InvoiceLineItems_Shifts_ShiftId",
            "FK_VisitNotes_Caregivers_CaregiverId",
            "FK_VisitNotes_Shifts_ShiftId",
            "FK_VisitPhotos_VisitNotes_VisitNoteId"
        };

        // Assert
        foreach (var foreignKeyName in phiForeignKeys)
        {
            var pattern = $"name: \"{Regex.Escape(foreignKeyName)}\"[\\s\\S]*?onDelete: ReferentialAction\\.Restrict";
            Regex.IsMatch(migrationText, pattern).Should().BeTrue($"{foreignKeyName} must use Restrict delete behavior");
        }
    }

    private static string ReadInitialMigration()
    {
        var repositoryRoot = FindRepositoryRoot();
        var migrationPath = Path.Combine(repositoryRoot, "Infrastructure", "Migrations", InitialMigrationFile);

        File.Exists(migrationPath).Should().BeTrue($"expected migration file at {migrationPath}");
        return File.ReadAllText(migrationPath);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CarePath.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate CarePath.sln from test output directory.");
    }
}

