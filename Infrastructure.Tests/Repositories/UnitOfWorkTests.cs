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
using Microsoft.EntityFrameworkCore;

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
    public async Task CommitTransactionAsync_WhenNoTransactionExists_Throws()
    {
        // Arrange
        await using var context = CreateDbContext();
        await using var unitOfWork = new UnitOfWork(context);

        // Act
        var act = async () => await unitOfWork.CommitTransactionAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No active transaction exists*");
    }

    [Fact]
    public async Task RollbackTransactionAsync_WhenNoTransactionExists_Throws()
    {
        // Arrange
        await using var context = CreateDbContext();
        await using var unitOfWork = new UnitOfWork(context);

        // Act
        var act = async () => await unitOfWork.RollbackTransactionAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No active transaction exists*");
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
