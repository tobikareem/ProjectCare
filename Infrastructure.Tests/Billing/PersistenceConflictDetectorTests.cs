using System.Reflection;
using CarePath.Application.Abstractions.Billing;
using CarePath.Infrastructure;
using CarePath.Infrastructure.Billing;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CarePath.Infrastructure.Tests.Billing;

public class PersistenceConflictDetectorTests
{
    private const string IndexName = "UX_InvoiceLineItems_ShiftId_NotNull";
    private const int UniqueConstraintViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    [Theory]
    [InlineData(UniqueConstraintViolation)]
    [InlineData(UniqueIndexViolation)]
    public void SqlServer_WhenUniqueViolationNamesTheIndex_ReturnsTrue(int errorNumber)
    {
        // Arrange
        var detector = new SqlServerPersistenceConflictDetector();
        var exception = new DbUpdateException(
            "Save failed.",
            CreateSqlException(errorNumber, $"Cannot insert duplicate key row in object 'dbo.InvoiceLineItems' with unique index '{IndexName}'."));

        // Act
        var isConflict = detector.IsUniqueConstraintConflict(exception, IndexName);

        // Assert
        isConflict.Should().BeTrue();
    }

    [Theory]
    [InlineData(547)]      // FK violation
    [InlineData(1205)]     // deadlock victim
    [InlineData(-2)]       // timeout
    public void SqlServer_WhenErrorIsNotAUniqueViolation_ReturnsFalse(int errorNumber)
    {
        // Arrange
        var detector = new SqlServerPersistenceConflictDetector();
        var exception = new DbUpdateException(
            "Save failed.",
            CreateSqlException(errorNumber, $"Some other failure mentioning {IndexName}."));

        // Act
        var isConflict = detector.IsUniqueConstraintConflict(exception, IndexName);

        // Assert
        isConflict.Should().BeFalse();
    }

    [Fact]
    public void SqlServer_WhenUniqueViolationNamesADifferentIndex_ReturnsFalse()
    {
        // Arrange
        var detector = new SqlServerPersistenceConflictDetector();
        var exception = new DbUpdateException(
            "Save failed.",
            CreateSqlException(UniqueConstraintViolation, "Cannot insert duplicate key row ... unique index 'IX_Some_Other_Index'."));

        // Act
        var isConflict = detector.IsUniqueConstraintConflict(exception, IndexName);

        // Assert
        isConflict.Should().BeFalse();
    }

    [Fact]
    public void PostgreSql_WhenSqlState23505NamesTheConstraint_ReturnsTrue()
    {
        // Arrange
        var detector = new PostgreSqlPersistenceConflictDetector();
        var exception = new DbUpdateException("Save failed.", CreatePostgresException("23505", IndexName));

        // Act
        var isConflict = detector.IsUniqueConstraintConflict(exception, IndexName);

        // Assert
        isConflict.Should().BeTrue();
    }

    [Theory]
    [InlineData("23503")]  // foreign_key_violation
    [InlineData("23502")]  // not_null_violation
    [InlineData("40P01")]  // deadlock_detected
    public void PostgreSql_WhenSqlStateIsNot23505_ReturnsFalse(string sqlState)
    {
        // Arrange
        var detector = new PostgreSqlPersistenceConflictDetector();
        var exception = new DbUpdateException("Save failed.", CreatePostgresException(sqlState, IndexName));

        // Act
        var isConflict = detector.IsUniqueConstraintConflict(exception, IndexName);

        // Assert
        isConflict.Should().BeFalse();
    }

    [Fact]
    public void PostgreSql_WhenUniqueViolationNamesADifferentConstraint_ReturnsFalse()
    {
        // Arrange
        var detector = new PostgreSqlPersistenceConflictDetector();
        var exception = new DbUpdateException("Save failed.", CreatePostgresException("23505", "IX_Some_Other_Index"));

        // Act
        var isConflict = detector.IsUniqueConstraintConflict(exception, IndexName);

        // Assert
        isConflict.Should().BeFalse();
    }

    [Fact]
    public void EachDetector_IgnoresTheOtherProvidersUniqueViolation()
    {
        // Arrange — a misconfigured provider must not silently classify foreign exceptions.
        var sqlServerDetector = new SqlServerPersistenceConflictDetector();
        var postgreSqlDetector = new PostgreSqlPersistenceConflictDetector();
        var postgresConflict = new DbUpdateException("Save failed.", CreatePostgresException("23505", IndexName));
        var sqlServerConflict = new DbUpdateException(
            "Save failed.",
            CreateSqlException(UniqueConstraintViolation, $"duplicate key ... '{IndexName}'."));

        // Act & Assert
        sqlServerDetector.IsUniqueConstraintConflict(postgresConflict, IndexName).Should().BeFalse();
        postgreSqlDetector.IsUniqueConstraintConflict(sqlServerConflict, IndexName).Should().BeFalse();
    }

    [Fact]
    public void BothDetectors_WhenExceptionIsUnrelated_ReturnFalse()
    {
        // Arrange
        var unrelated = new InvalidOperationException("Nothing to do with persistence.");

        // Act & Assert
        new SqlServerPersistenceConflictDetector().IsUniqueConstraintConflict(unrelated, IndexName).Should().BeFalse();
        new PostgreSqlPersistenceConflictDetector().IsUniqueConstraintConflict(unrelated, IndexName).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, typeof(SqlServerPersistenceConflictDetector))]
    [InlineData("SqlServer", typeof(SqlServerPersistenceConflictDetector))]
    [InlineData("PostgreSql", typeof(PostgreSqlPersistenceConflictDetector))]
    [InlineData("postgresql", typeof(PostgreSqlPersistenceConflictDetector))]
    public void AddInfrastructure_SelectsDetectorFromDatabaseProviderConfiguration(string? configuredProvider, Type expected)
    {
        // Arrange
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=carepath;Username=test",
            ["Database:Provider"] = configuredProvider,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();

        // Act
        services.AddInfrastructure(configuration);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var detector = scope.ServiceProvider.GetRequiredService<IPersistenceConflictDetector>();

        // Assert
        detector.Should().BeOfType(expected);
    }

    private static PostgresException CreatePostgresException(string sqlState, string constraintName) =>
        new(
            messageText: $"duplicate key value violates unique constraint \"{constraintName}\"",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState,
            constraintName: constraintName);

    // SqlException has no public constructor; Microsoft.Data.SqlClient only exposes the
    // internal CreateException factory, so tests build one through reflection.
    private static SqlException CreateSqlException(int errorNumber, string message)
    {
        var errorCollection = (SqlErrorCollection)typeof(SqlErrorCollection)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, binder: null, Type.EmptyTypes, modifiers: null)!
            .Invoke(null);

        var errorConstructor = typeof(SqlError)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(constructor => constructor.GetParameters().Length == 8);
        var error = errorConstructor.Invoke(
            [errorNumber, (byte)0, (byte)0, "test-server", message, "test-procedure", 0, null]);

        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(errorCollection, [error]);

        var createException = typeof(SqlException)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "CreateException"
                && method.GetParameters().Length == 2
                && method.GetParameters()[1].ParameterType == typeof(string));

        return (SqlException)createException.Invoke(null, [errorCollection, "16.0.1000"])!;
    }
}
