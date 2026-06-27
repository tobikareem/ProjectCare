using System.Security.Claims;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.Interceptors;

public class AuditableEntityInterceptorTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenEntityAdded_SetsCreatedByFromCurrentUser()
    {
        // Arrange
        var httpContextAccessor = CreateHttpContextAccessor("user-123");
        await using var context = CreateDbContext(httpContextAccessor);
        var user = CreateUser();
        user.CreatedBy = "spoofed-user";

        // Act
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Assert
        user.CreatedBy.Should().Be("user-123");
        user.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        user.UpdatedBy.Should().BeNull();
        user.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityModified_PreservesCreatedByAndSetsUpdatedBy()
    {
        // Arrange - a different actor performs the create vs. the update so the
        // CreatedBy-is-never-overwritten invariant is actually exercised.
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = CreateHttpContext("creator-123")
        };
        await using var context = CreateDbContext(httpContextAccessor);
        var user = CreateUser();
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Act
        httpContextAccessor.HttpContext = CreateHttpContext("updater-456");
        user.FirstName = "Updated";
        await context.SaveChangesAsync();

        // Assert
        user.CreatedBy.Should().Be("creator-123");
        user.UpdatedBy.Should().Be("updater-456");
        user.UpdatedAt.Should().NotBeNull();
        user.UpdatedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenHttpContextMissing_UsesSystemActor()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor();
        await using var context = CreateDbContext(httpContextAccessor);
        var user = CreateUser();
        user.CreatedBy = "spoofed-user";

        // Act
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();

        // Assert
        user.CreatedBy.Should().Be("System");
    }

    private static TestAuditDbContext CreateDbContext(IHttpContextAccessor httpContextAccessor)
    {
        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntityInterceptor(httpContextAccessor))
            .Options;

        return new TestAuditDbContext(options);
    }

    private static IHttpContextAccessor CreateHttpContextAccessor(string userId) =>
        new HttpContextAccessor { HttpContext = CreateHttpContext(userId) };

    private static HttpContext CreateHttpContext(string userId)
    {
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
            authenticationType: "Test"));

        return new DefaultHttpContext { User = claimsPrincipal };
    }

    private static User CreateUser() => new()
    {
        FirstName = "Synthetic",
        LastName = "User",
        Email = $"synthetic-{Guid.NewGuid():N}@carepath.local",
        PhoneNumber = "555-0100",
        Role = UserRole.Admin
    };

    private sealed class TestAuditDbContext : DbContext
    {
        public TestAuditDbContext(DbContextOptions<TestAuditDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
    }
}
