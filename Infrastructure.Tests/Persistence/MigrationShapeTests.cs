using System.Text.RegularExpressions;
using FluentAssertions;

namespace CarePath.Infrastructure.Tests.Persistence;

public class MigrationShapeTests
{
    private const string InitialMigrationFile = "20260628003352_InitialCreate.cs";
    private const string ClientAccessGrantMigrationFile = "20260705080331_AddClientAccessGrants.cs";

    [Fact]
    public void InitialCreateMigration_WhenGenerated_CreatesExpectedCp01TablesOnly()
    {
        // Arrange
        var migrationText = ReadMigration(InitialMigrationFile);
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
        var migrationText = ReadMigration(InitialMigrationFile);

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
        var migrationText = ReadMigration(InitialMigrationFile);
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
            AssertRestrictForeignKey(migrationText, foreignKeyName);
        }
    }

    [Fact]
    public void AddClientAccessGrantsMigration_WhenGenerated_CreatesGrantTableOnlyWithBoundedColumns()
    {
        // Arrange
        var migrationText = ReadMigration(ClientAccessGrantMigrationFile);

        // Assert
        migrationText.Should().Contain("name: \"ClientAccessGrants\"");
        migrationText.Should().Contain("AccessScope = table.Column<int>(type: \"int\", nullable: false)");
        migrationText.Should().Contain("CreatedBy = table.Column<string>(type: \"nvarchar(256)\", maxLength: 256, nullable: true)");
        migrationText.Should().Contain("UpdatedBy = table.Column<string>(type: \"nvarchar(256)\", maxLength: 256, nullable: true)");
        migrationText.Should().NotContain("nvarchar(max)");
        migrationText.Should().NotContain("DeleteData");
        migrationText.Should().NotContain("RawContent");
        migrationText.Should().NotContain("SourceText");
    }

    [Fact]
    public void AddClientAccessGrantsMigration_WhenGenerated_RestrictsAllGrantForeignKeys()
    {
        // Arrange
        var migrationText = ReadMigration(ClientAccessGrantMigrationFile);
        var foreignKeys = new[]
        {
            "FK_ClientAccessGrants_Clients_ClientId",
            "FK_ClientAccessGrants_Users_GrantedByUserId",
            "FK_ClientAccessGrants_Users_GranteeUserId",
            "FK_ClientAccessGrants_Users_RevokedByUserId"
        };

        // Assert
        foreach (var foreignKeyName in foreignKeys)
        {
            AssertRestrictForeignKey(migrationText, foreignKeyName);
        }

        migrationText.Should().NotContain("ReferentialAction.Cascade");
    }

    private static void AssertRestrictForeignKey(string migrationText, string foreignKeyName)
    {
        var pattern = $"name: \"{Regex.Escape(foreignKeyName)}\"[\\s\\S]*?onDelete: ReferentialAction\\.Restrict";
        Regex.IsMatch(migrationText, pattern).Should().BeTrue($"{foreignKeyName} must use Restrict delete behavior");
    }

    private static string ReadMigration(string migrationFile)
    {
        var repositoryRoot = FindRepositoryRoot();
        var migrationPath = Path.Combine(repositoryRoot, "Infrastructure", "Migrations", migrationFile);

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
