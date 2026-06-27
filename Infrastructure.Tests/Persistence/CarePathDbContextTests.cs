using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.Persistence;

public class CarePathDbContextTests
{

    [Fact]
    public async Task DomainQueries_WhenEntityIsSoftDeleted_ExcludeDeletedRows()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());
        await using var context = new CarePathDbContext(options, interceptor);
        var user = new Domain.Entities.Identity.User
        {
            FirstName = "Synthetic",
            LastName = "Deleted",
            Email = $"synthetic-{Guid.NewGuid():N}@carepath.local",
            PhoneNumber = "555-0100",
            Role = Domain.Enumerations.UserRole.Admin,
            IsDeleted = true
        };

        // Act
        await context.DomainUsers.AddAsync(user);
        await context.SaveChangesAsync();
        var users = await context.DomainUsers.ToListAsync();

        // Assert
        users.Should().BeEmpty();
    }

    [Fact]
    public void Model_WhenContextIsCreated_BuildsSuccessfully()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        // Act
        using var context = new CarePathDbContext(options, interceptor);
        var entityTypes = context.Model.GetEntityTypes().Select(entity => entity.ClrType).ToList();

        // Assert
        entityTypes.Should().Contain(typeof(Domain.Entities.Identity.User));
        entityTypes.Should().NotContain(typeof(Domain.Entities.Transitions.TransitionPlan));
    }
}
