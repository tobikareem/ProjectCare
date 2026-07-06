using System.Text.RegularExpressions;
using FluentAssertions;

namespace CarePath.Infrastructure.Tests.Persistence;

public class MigrationShapeTests
{
    private const string InitialMigrationFile = "20260628003352_InitialCreate.cs";
    private const string ClientAccessGrantMigrationFile = "20260705080331_AddClientAccessGrants.cs";
    private const string AddTransitionsMigrationFile = "20260706085154_AddTransitions.cs";

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

    [Fact]
    public void AddTransitionsMigration_WhenGenerated_UsesOnlyApprovedUnboundedPhiColumns()
    {
        // Arrange
        var migrationText = ReadMigration(AddTransitionsMigrationFile);

        // Assert
        migrationText.Should().Contain("RawContent = table.Column<string>(type: \"nvarchar(max)\", nullable: true)");
        migrationText.Should().Contain("SourceText = table.Column<string>(type: \"nvarchar(max)\", nullable: true)");
        migrationText.Should().Contain("ResponsesJson = table.Column<string>(type: \"nvarchar(max)\", nullable: false)");
        migrationText.Should().Contain("HospitalName = table.Column<string>(type: \"nvarchar(100)\", maxLength: 100, nullable: true)");
        migrationText.Should().Contain("SourceReference = table.Column<string>(type: \"nvarchar(200)\", maxLength: 200, nullable: true)");
        migrationText.Should().Contain("InstructionText = table.Column<string>(type: \"nvarchar(2000)\", maxLength: 2000, nullable: false)");
        migrationText.Should().Contain("TriggerDetails = table.Column<string>(type: \"nvarchar(1000)\", maxLength: 1000, nullable: false)");
        migrationText.Should().Contain("ResolutionNote = table.Column<string>(type: \"nvarchar(2000)\", maxLength: 2000, nullable: true)");
        Regex.Matches(migrationText, "nvarchar\\(max\\)").Should().HaveCount(3);
    }

    [Fact]
    public void AddTransitionsMigration_WhenGenerated_RestrictsAllTransitionsForeignKeys()
    {
        // Arrange
        var migrationText = ReadMigration(AddTransitionsMigrationFile);
        var foreignKeys = new[]
        {
            "FK_DischargeDocuments_Clients_ClientId",
            "FK_TransitionPlans_Clients_ClientId",
            "FK_TransitionPlans_DischargeDocuments_DischargeDocumentId",
            "FK_TransitionCheckIns_TransitionPlans_TransitionPlanId",
            "FK_TransitionEscalations_TransitionPlans_TransitionPlanId",
            "FK_TransitionInstructions_TransitionPlans_TransitionPlanId",
            "FK_TransitionReminders_TransitionInstructions_TransitionInstructionId",
            "FK_TransitionReminders_TransitionPlans_TransitionPlanId",
            "FK_VisitNotes_TransitionPlans_TransitionPlanId"
        };

        // Assert
        foreach (var foreignKeyName in foreignKeys)
        {
            AssertRestrictForeignKey(migrationText, foreignKeyName);
        }

        migrationText.Should().NotContain("ReferentialAction.Cascade");
        migrationText.Should().NotContain("ReferentialAction.SetNull");
    }

    [Fact]
    public void AddTransitionsMigration_WhenRolledBack_FailsClosedInsteadOfDroppingPhiTables()
    {
        // Arrange
        var migrationText = ReadMigration(AddTransitionsMigrationFile);
        var downBody = Regex.Match(
            migrationText,
            "protected override void Down\\(MigrationBuilder migrationBuilder\\)[\\s\\S]*?\\n        }").Value;

        // Assert
        downBody.Should().Contain("forward-only");
        downBody.Should().Contain("THROW 51002");
        downBody.Should().NotContain("DropTable");
        downBody.Should().NotContain("DropColumn");
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
