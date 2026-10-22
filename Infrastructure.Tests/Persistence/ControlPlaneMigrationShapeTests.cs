using FluentAssertions;

namespace CarePath.Infrastructure.Tests.Persistence;

/// <summary>
/// Shape guards for the control-plane migrations of both providers (ADR 0003 §8.1: same domain
/// schema intent per provider; provider-specific store types may differ).
/// </summary>
public sealed class ControlPlaneMigrationShapeTests
{
    private const string SqlServerInitialMigrationFile = "20261021201239_InitialControlPlane.cs";
    private const string PostgreSqlInitialMigrationFile = "20261021201248_InitialControlPlane.cs";

    private static readonly string[] ExpectedTables =
    [
        "AspNetUsers",
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserTokens",
        "Organizations",
        "OrganizationDomains",
        "OrganizationBrandings",
        "OrganizationMemberships",
        "OrganizationDataPlanes",
        "PlatformAuditEvents",
        "PlatformRefreshTokens"
    ];

    private static readonly string[] ForbiddenPhiColumnFragments =
    [
        "DateOfBirth",
        "Address",
        "Diagnosis",
        "Medical",
        "Insurance",
        "Allerg",
        "Latitude",
        "Longitude",
        "RawContent",
        "SourceText",
        "ResponsesJson",
        "DomainUserId",
        "ConnectionString"
    ];

    public static TheoryData<string> MigrationFiles => new() { SqlServerInitialMigrationFile, PostgreSqlInitialMigrationFile };

    [Theory]
    [MemberData(nameof(MigrationFiles))]
    public void InitialControlPlaneMigration_WhenGenerated_CreatesExpectedTablesAndNoIdentityRoles(string migrationFile)
    {
        // Arrange
        var migrationText = ReadMigration(migrationFile);

        // Assert
        foreach (var tableName in ExpectedTables)
        {
            migrationText.Should().Contain($"name: \"{tableName}\"");
        }

        migrationText.Should().NotContain("AspNetRoles");
        migrationText.Should().NotContain("AspNetUserRoles");
        migrationText.Should().NotContain("AspNetRoleClaims");
    }

    [Theory]
    [MemberData(nameof(MigrationFiles))]
    public void InitialControlPlaneMigration_WhenGenerated_ContainsNoPhiOrTenantColumns(string migrationFile)
    {
        // Arrange
        var migrationText = ReadMigration(migrationFile);

        // Assert
        foreach (var fragment in ForbiddenPhiColumnFragments)
        {
            migrationText.Should().NotContain(fragment);
        }

        migrationText.Should().NotContain("DeleteData");
        migrationText.Should().NotContain("TRUNCATE");
    }

    [Theory]
    [MemberData(nameof(MigrationFiles))]
    public void InitialControlPlaneMigration_WhenGenerated_RestrictsControlPlaneForeignKeysAndEnforcesUniqueness(string migrationFile)
    {
        // Arrange
        var migrationText = ReadMigration(migrationFile);
        var restrictedForeignKeys = new[]
        {
            "FK_OrganizationDomains_Organizations_OrganizationId",
            "FK_OrganizationBrandings_Organizations_OrganizationId",
            "FK_OrganizationMemberships_Organizations_OrganizationId",
            "FK_OrganizationDataPlanes_Organizations_OrganizationId",
            "FK_PlatformRefreshTokens_AspNetUsers_PlatformUserId",
            "FK_PlatformRefreshTokens_OrganizationMemberships_MembershipId",
            "FK_PlatformRefreshTokens_Organizations_OrganizationId",
            "FK_OrganizationMemberships_AspNetUsers_PlatformUserId"
        };
        var uniqueIndexes = new[]
        {
            "IX_Organizations_Slug",
            "IX_OrganizationDomains_HostName",
            "IX_OrganizationMemberships_OrganizationId_PlatformUserId",
            "IX_OrganizationBrandings_OrganizationId",
            "IX_OrganizationDataPlanes_OrganizationId",
            "IX_PlatformRefreshTokens_TokenHash"
        };

        // Assert
        foreach (var foreignKey in restrictedForeignKeys)
        {
            var start = migrationText.IndexOf(foreignKey, StringComparison.Ordinal);
            start.Should().BeGreaterThan(-1, $"{foreignKey} must exist");
            migrationText[start..(start + 400)].Should().Contain("ReferentialAction.Restrict");
        }

        foreach (var index in uniqueIndexes)
        {
            var start = migrationText.IndexOf(index, StringComparison.Ordinal);
            start.Should().BeGreaterThan(-1, $"{index} must exist");
            migrationText[start..(start + 300)].Should().Contain("unique: true");
        }
    }

    [Fact]
    public void InitialControlPlaneMigrations_WhenCompared_DeclareTheSameTablesForBothProviders()
    {
        // Arrange
        var sqlServerTables = ExtractCreatedTables(ReadMigration(SqlServerInitialMigrationFile));
        var postgreSqlTables = ExtractCreatedTables(ReadMigration(PostgreSqlInitialMigrationFile));

        // Assert
        postgreSqlTables.Should().BeEquivalentTo(sqlServerTables);
    }

    private static IReadOnlyList<string> ExtractCreatedTables(string migrationText)
    {
        const string marker = "migrationBuilder.CreateTable(";
        var tables = new List<string>();
        var index = migrationText.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var nameStart = migrationText.IndexOf("name: \"", index, StringComparison.Ordinal) + "name: \"".Length;
            var nameEnd = migrationText.IndexOf('"', nameStart);
            tables.Add(migrationText[nameStart..nameEnd]);
            index = migrationText.IndexOf(marker, nameEnd, StringComparison.Ordinal);
        }

        return tables;
    }

    private static string ReadMigration(string migrationFile)
    {
        var repositoryRoot = FindRepositoryRoot();
        var relativeFolder = migrationFile == PostgreSqlInitialMigrationFile
            ? Path.Combine("Infrastructure.Migrations.PostgreSql", "Migrations", "ControlPlane")
            : Path.Combine("Infrastructure", "ControlPlane", "Migrations");
        var migrationPath = Path.Combine(repositoryRoot, relativeFolder, migrationFile);

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

        throw new InvalidOperationException("Could not locate CarePath.sln above the test output directory.");
    }
}
