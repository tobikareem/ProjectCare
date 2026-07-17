using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using CarePath.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.Repositories;

public class RepositoryTests
{
    [Fact]
    public async Task AddAsync_WhenSaved_PersistsEntity()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        var user = CreateUser("add@example.test");

        // Act
        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // Assert
        var saved = await repository.GetByIdAsync(user.Id);
        saved.Should().NotBeNull();
        saved!.Email.Should().Be("add@example.test");
    }

    [Fact]
    public async Task GetByIdAsync_WhenEntityIsSoftDeleted_ReturnsNull()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        var user = CreateUser("deleted@example.test");
        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        await repository.DeleteAsync(user);
        await context.SaveChangesAsync();
        var result = await repository.GetByIdAsync(user.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindAsync_WhenPredicateMatches_ReturnsMaterializedReadOnlyList()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        await repository.AddAsync(CreateUser("admin@example.test", UserRole.Admin));
        await repository.AddAsync(CreateUser("caregiver@example.test", UserRole.Caregiver));
        await context.SaveChangesAsync();

        // Act
        var admins = await repository.FindAsync(user => user.Role == UserRole.Admin);

        // Assert
        admins.Should().HaveCount(1);
        admins[0].Email.Should().Be("admin@example.test");
    }

    [Fact]
    public async Task UpdateAsync_WhenSaved_PersistsChanges()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        var user = CreateUser("update@example.test");
        await repository.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        user.FirstName = "Updated";
        await repository.UpdateAsync(user);
        await context.SaveChangesAsync();

        // Assert
        var saved = await repository.GetByIdAsync(user.Id);
        saved!.FirstName.Should().Be("Updated");
    }

    [Fact]
    public async Task CountAndExistsAsync_WhenCalled_RespectGlobalQueryFilters()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        var active = CreateUser("active@example.test");
        var deleted = CreateUser("hidden@example.test");
        deleted.IsDeleted = true;
        await context.DomainUsers.AddRangeAsync(active, deleted);
        await context.SaveChangesAsync();

        // Act
        var count = await repository.CountAsync();
        var deletedExists = await repository.ExistsAsync(user => user.Email == "hidden@example.test");

        // Assert
        count.Should().Be(1);
        deletedExists.Should().BeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_WhenCalled_ReturnsRequestedPageAndTotalCount()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        for (var index = 0; index < 5; index++)
        {
            await repository.AddAsync(CreateUser($"page-{index}@example.test"));
        }
        await context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await repository.GetPagedAsync(pageNumber: 2, pageSize: 2);

        // Assert
        totalCount.Should().Be(5);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_WhenPredicateProvided_ReturnsFilteredPageAndTotalCount()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        await repository.AddAsync(CreateUser("admin-1@example.test", UserRole.Admin));
        await repository.AddAsync(CreateUser("caregiver-1@example.test", UserRole.Caregiver));
        await repository.AddAsync(CreateUser("caregiver-2@example.test", UserRole.Caregiver));
        await context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await repository.GetPagedAsync(
            user => user.Role == UserRole.Caregiver,
            pageNumber: 1,
            pageSize: 10);

        // Assert
        totalCount.Should().Be(2);
        items.Should().OnlyContain(user => user.Role == UserRole.Caregiver);
    }
    [Fact]
    public async Task GetPagedAsync_WhenOrderKeysProvided_ReturnsDeterministicallyOrderedFilteredPage()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        var zoeAdams = CreateUser("zoe@example.test");
        zoeAdams.FirstName = "Zoe";
        zoeAdams.LastName = "Adams";
        var amyBrown = CreateUser("amy@example.test");
        amyBrown.FirstName = "Amy";
        amyBrown.LastName = "Brown";
        var alexBrown = CreateUser("alex@example.test");
        alexBrown.FirstName = "Alex";
        alexBrown.LastName = "Brown";
        var filteredOut = CreateUser("caregiver@example.test", UserRole.Caregiver);
        await repository.AddAsync(amyBrown);
        await repository.AddAsync(zoeAdams);
        await repository.AddAsync(alexBrown);
        await repository.AddAsync(filteredOut);
        await context.SaveChangesAsync();

        // Act
        var (items, totalCount) = await repository.GetPagedAsync(
            user => user.Role == UserRole.Admin,
            user => user.LastName,
            user => user.FirstName,
            pageNumber: 1,
            pageSize: 10);

        // Assert
        totalCount.Should().Be(3);
        items.Select(user => user.Email).Should().Equal(
            "zoe@example.test",
            "alex@example.test",
            "amy@example.test");
    }

    [Fact]
    public async Task GetPagedDescendingAsync_WhenOrderKeyProvided_ReturnsDescendingPageWithIdTiebreaker()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);
        var zoe = CreateUser("zoe@example.test");
        zoe.FirstName = "Zoe";
        var amyOne = CreateUser("amy-one@example.test");
        amyOne.FirstName = "Amy";
        var amyTwo = CreateUser("amy-two@example.test");
        amyTwo.FirstName = "Amy";
        var filteredOut = CreateUser("caregiver@example.test", UserRole.Caregiver);
        filteredOut.FirstName = "Zz";
        await repository.AddAsync(amyOne);
        await repository.AddAsync(zoe);
        await repository.AddAsync(amyTwo);
        await repository.AddAsync(filteredOut);
        await context.SaveChangesAsync();
        var expectedAmyOrder = new[] { amyOne, amyTwo }
            .OrderBy(user => user.Id)
            .Select(user => user.Email)
            .ToArray();

        // Act
        var (items, totalCount) = await repository.GetPagedDescendingAsync(
            user => user.Role == UserRole.Admin,
            user => user.FirstName,
            pageNumber: 1,
            pageSize: 10);

        // Assert
        totalCount.Should().Be(3);
        items.Select(user => user.Email).Should().Equal(
            "zoe@example.test",
            expectedAmyOrder[0],
            expectedAmyOrder[1]);
    }

    [Theory]
    [InlineData(0, 10, "pageNumber")]
    [InlineData(1, 0, "pageSize")]
    public async Task GetPagedAsync_WhenArgumentsAreInvalid_Throws(int pageNumber, int pageSize, string parameterName)
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = new Repository<User>(context);

        // Act
        var act = async () => await repository.GetPagedAsync(pageNumber, pageSize);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .Where(exception => exception.ParamName == parameterName);
    }

    private static CarePathDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        return new CarePathDbContext(options, interceptor);
    }

    private static User CreateUser(string email, UserRole role = UserRole.Admin) => new()
    {
        FirstName = "Synthetic",
        LastName = "Repository",
        Email = email,
        PhoneNumber = "555-0100",
        Role = role
    };
}
