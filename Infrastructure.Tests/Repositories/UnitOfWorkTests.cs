using CarePath.Domain.Entities.Billing;
using CarePath.Domain.Entities.Clinical;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Entities.Scheduling;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using CarePath.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CarePath.Infrastructure.Tests.Repositories;

public class UnitOfWorkTests
{
    [Fact]
    public async Task RepositoryProperties_WhenAccessed_ReturnExpectedRepositoryTypes()
    {
        // Arrange
        await using var context = CreateDbContext();
        await using var unitOfWork = new UnitOfWork(context);

        // Assert
        unitOfWork.Users.Should().BeAssignableTo<Repository<User>>();
        unitOfWork.Caregivers.Should().BeAssignableTo<Repository<Caregiver>>();
        unitOfWork.CaregiverCertifications.Should().BeAssignableTo<Repository<CaregiverCertification>>();
        unitOfWork.Clients.Should().BeAssignableTo<Repository<Client>>();
        unitOfWork.CarePlans.Should().BeAssignableTo<Repository<CarePlan>>();
        unitOfWork.Shifts.Should().BeAssignableTo<Repository<Shift>>();
        unitOfWork.VisitNotes.Should().BeAssignableTo<Repository<VisitNote>>();
        unitOfWork.VisitPhotos.Should().BeAssignableTo<Repository<VisitPhoto>>();
        unitOfWork.Invoices.Should().BeAssignableTo<Repository<Invoice>>();
        unitOfWork.InvoiceLineItems.Should().BeAssignableTo<Repository<InvoiceLineItem>>();
        unitOfWork.Payments.Should().BeAssignableTo<Repository<Payment>>();
    }

    [Fact]
    public async Task RepositoryProperties_WhenAccessedRepeatedly_ReturnSameInstance()
    {
        // Arrange
        await using var context = CreateDbContext();
        await using var unitOfWork = new UnitOfWork(context);

        // Assert
        unitOfWork.Users.Should().BeSameAs(unitOfWork.Users);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRepositoryAddsEntity_PersistsChanges()
    {
        // Arrange
        await using var context = CreateDbContext();
        await using var unitOfWork = new UnitOfWork(context);
        var user = CreateUser();

        // Act
        await unitOfWork.Users.AddAsync(user);
        var entriesWritten = await unitOfWork.SaveChangesAsync();

        // Assert
        entriesWritten.Should().BeGreaterThan(0);
        (await unitOfWork.Users.GetByIdAsync(user.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenOperationSucceeds_CommitsChanges()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var context = CreateSqliteDbContext(connection);
        await using var unitOfWork = new UnitOfWork(context);
        var user = CreateUser();

        // Act
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.Users.AddAsync(user, token);
            await unitOfWork.SaveChangesAsync(token);
        });

        // Assert
        await using var verificationContext = CreateSqliteDbContext(connection, ensureCreated: false);
        (await verificationContext.Set<User>().AnyAsync(persisted => persisted.Id == user.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenOperationThrows_RollsBackAndRethrows()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var context = CreateSqliteDbContext(connection);
        await using var unitOfWork = new UnitOfWork(context);
        var user = CreateUser();

        // Act
        var act = async () => await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.Users.AddAsync(user, token);
            await unitOfWork.SaveChangesAsync(token);
            throw new InvalidOperationException("Synthetic failure after save.");
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Synthetic failure after save.");
        await using var verificationContext = CreateSqliteDbContext(connection, ensureCreated: false);
        (await verificationContext.Set<User>().AnyAsync(persisted => persisted.Id == user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithResult_ReturnsOperationResult()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var context = CreateSqliteDbContext(connection);
        await using var unitOfWork = new UnitOfWork(context);
        var user = CreateUser();

        // Act
        var entriesWritten = await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.Users.AddAsync(user, token);
            return await unitOfWork.SaveChangesAsync(token);
        });

        // Assert
        entriesWritten.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenNestedTransactionAttempted_Throws()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var context = CreateSqliteDbContext(connection);
        await using var unitOfWork = new UnitOfWork(context);

        // Act
        var act = async () => await unitOfWork.ExecuteInTransactionAsync(token =>
            unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, token));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A transaction is already active*");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_AfterFailedTransaction_AllowsSubsequentTransaction()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var context = CreateSqliteDbContext(connection);
        await using var unitOfWork = new UnitOfWork(context);
        var user = CreateUser();

        var failing = async () => await unitOfWork.ExecuteInTransactionAsync(
            _ => Task.FromException(new InvalidOperationException("Synthetic failure.")));
        await failing.Should().ThrowAsync<InvalidOperationException>();

        // Act
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.Users.AddAsync(user, token);
            await unitOfWork.SaveChangesAsync(token);
        });

        // Assert
        (await unitOfWork.Users.GetByIdAsync(user.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenTransientFailureOccurs_RetriesAndCommits()
    {
        // Arrange
        await using var connection = CreateOpenSqliteConnection();
        await using var context = CreateSqliteDbContext(connection, useRetryingStrategy: true);
        await using var unitOfWork = new UnitOfWork(context);
        var user = CreateUser();
        var attempts = 0;

        // Act
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TestTransientException();
            }

            await unitOfWork.Users.AddAsync(user, token);
            await unitOfWork.SaveChangesAsync(token);
        });

        // Assert
        attempts.Should().Be(2);
        await using var verificationContext = CreateSqliteDbContext(connection, ensureCreated: false);
        (await verificationContext.Set<User>().AnyAsync(persisted => persisted.Id == user.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenRetryFollowsTrackedMutation_ReReadsDatabaseState()
    {
        // Arrange — seed a committed user, then run a transaction that reads and mutates it
        // and fails transiently once after saving. The retry must observe the database value,
        // not the previous attempt's rolled-back in-memory mutation.
        await using var connection = CreateOpenSqliteConnection();
        var seeded = CreateUser();
        seeded.FirstName = "Original";
        await using (var seedContext = CreateSqliteDbContext(connection))
        {
            seedContext.Set<User>().Add(seeded);
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateSqliteDbContext(connection, ensureCreated: false, useRetryingStrategy: true);
        await using var unitOfWork = new UnitOfWork(context);
        var attempts = 0;
        var observedFirstNames = new List<string>();

        // Act
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            attempts++;
            var tracked = await unitOfWork.Users.GetByIdAsync(seeded.Id, token);
            observedFirstNames.Add(tracked!.FirstName);
            tracked.FirstName = "Mutated";
            await unitOfWork.Users.UpdateAsync(tracked, token);
            await unitOfWork.SaveChangesAsync(token);
            if (attempts == 1)
            {
                throw new TestTransientException();
            }
        });

        // Assert
        attempts.Should().Be(2);
        observedFirstNames.Should().Equal("Original", "Original");
    }

    private static SqliteConnection CreateOpenSqliteConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static CarePathDbContext CreateSqliteDbContext(
        SqliteConnection connection,
        bool ensureCreated = true,
        bool useRetryingStrategy = false)
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseSqlite(connection, sqlite =>
            {
                if (useRetryingStrategy)
                {
                    sqlite.ExecutionStrategy(dependencies => new TestRetryingExecutionStrategy(dependencies));
                }
            })
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());
        var context = new CarePathDbContext(options, interceptor);
        if (ensureCreated)
        {
            context.Database.EnsureCreated();
        }

        return context;
    }

    /// <summary>Retries on <see cref="TestTransientException"/> so retry semantics can be exercised without SQL Server.</summary>
    private sealed class TestRetryingExecutionStrategy : ExecutionStrategy
    {
        public TestRetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
            : base(dependencies, maxRetryCount: 2, maxRetryDelay: TimeSpan.Zero)
        {
        }

        protected override bool ShouldRetryOn(Exception exception) => exception is TestTransientException;
    }

    private sealed class TestTransientException : Exception
    {
        public TestTransientException()
            : base("Synthetic transient failure.")
        {
        }
    }

    private static CarePathDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        return new CarePathDbContext(options, interceptor);
    }

    private static User CreateUser() => new()
    {
        FirstName = "Synthetic",
        LastName = "UnitOfWork",
        Email = $"uow-{Guid.NewGuid():N}@example.test",
        PhoneNumber = "555-0100",
        Role = UserRole.Admin
    };
}
