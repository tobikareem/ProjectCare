using CarePath.Application.Abstractions.Auth;
using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.Auth;
using CarePath.Infrastructure.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.Auth;

public class ClientAccessEvaluatorTests
{

    [Fact]
    public async Task EvaluateAsync_WhenUserOwnsClientProfile_AuthorizesWithoutExplicitGrant()
    {
        // Arrange
        await using var context = CreateDbContext();
        var userId = Guid.NewGuid();
        var client = new Client
        {
            UserId = userId,
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        await context.Clients.AddAsync(client);
        await context.SaveChangesAsync();
        var evaluator = new ClientAccessEvaluator(context);

        // Act
        var result = await evaluator.EvaluateAsync(userId, client.Id, AccessScope.Full);

        // Assert
        result.IsAuthorized.Should().BeTrue();
        result.DenialCode.Should().BeNull();
    }
    [Fact]
    public async Task EvaluateAsync_WhenFullGrantRequiresFull_Authorizes()
    {
        // Arrange
        await using var context = CreateDbContext();
        var grant = CreateGrant(AccessScope.Full);
        await context.ClientAccessGrants.AddAsync(grant);
        await context.SaveChangesAsync();
        var evaluator = new ClientAccessEvaluator(context);

        // Act
        var result = await evaluator.EvaluateAsync(grant.GranteeUserId, grant.ClientId, AccessScope.Full);

        // Assert
        result.IsAuthorized.Should().BeTrue();
        result.DenialCode.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WhenFullGrantRequiresPatientFacing_Authorizes()
    {
        // Arrange
        await using var context = CreateDbContext();
        var grant = CreateGrant(AccessScope.Full);
        await context.ClientAccessGrants.AddAsync(grant);
        await context.SaveChangesAsync();
        var evaluator = new ClientAccessEvaluator(context);

        // Act
        var result = await evaluator.EvaluateAsync(grant.GranteeUserId, grant.ClientId, AccessScope.PatientFacing);

        // Assert
        result.IsAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WhenPatientFacingGrantRequiresFull_DeniesScopeInsufficient()
    {
        // Arrange
        await using var context = CreateDbContext();
        var grant = CreateGrant(AccessScope.PatientFacing);
        await context.ClientAccessGrants.AddAsync(grant);
        await context.SaveChangesAsync();
        var evaluator = new ClientAccessEvaluator(context);

        // Act
        var result = await evaluator.EvaluateAsync(grant.GranteeUserId, grant.ClientId, AccessScope.Full);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.DenialCode.Should().Be(ClientAccessEvaluationResult.ScopeInsufficient);
    }

    [Fact]
    public async Task EvaluateAsync_WhenGrantIsRevoked_DeniesNoGrant()
    {
        // Arrange
        await using var context = CreateDbContext();
        var grant = CreateGrant(AccessScope.Full);
        grant.RevokedAtUtc = DateTime.UtcNow;
        grant.RevokedByUserId = Guid.NewGuid();
        await context.ClientAccessGrants.AddAsync(grant);
        await context.SaveChangesAsync();
        var evaluator = new ClientAccessEvaluator(context);

        // Act
        var result = await evaluator.EvaluateAsync(grant.GranteeUserId, grant.ClientId, AccessScope.PatientFacing);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.DenialCode.Should().Be(ClientAccessEvaluationResult.NoGrant);
    }

    [Fact]
    public async Task EvaluateAsync_WhenGrantIsSoftDeleted_DeniesNoGrant()
    {
        // Arrange
        await using var context = CreateDbContext();
        var grant = CreateGrant(AccessScope.Full);
        grant.IsDeleted = true;
        await context.ClientAccessGrants.AddAsync(grant);
        await context.SaveChangesAsync();
        var evaluator = new ClientAccessEvaluator(context);

        // Act
        var result = await evaluator.EvaluateAsync(grant.GranteeUserId, grant.ClientId, AccessScope.PatientFacing);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.DenialCode.Should().Be(ClientAccessEvaluationResult.NoGrant);
    }

    [Fact]
    public async Task EvaluateAsync_WhenNoGrantExists_DeniesNoGrant()
    {
        // Arrange
        await using var context = CreateDbContext();
        var evaluator = new ClientAccessEvaluator(context);

        // Act
        var result = await evaluator.EvaluateAsync(Guid.NewGuid(), Guid.NewGuid(), AccessScope.PatientFacing);

        // Assert
        result.IsAuthorized.Should().BeFalse();
        result.DenialCode.Should().Be(ClientAccessEvaluationResult.NoGrant);
    }

    private static CarePathDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CarePathDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var interceptor = new AuditableEntityInterceptor(new HttpContextAccessor());

        return new CarePathDbContext(options, interceptor);
    }

    private static ClientAccessGrant CreateGrant(AccessScope scope) => new()
    {
        GranteeUserId = Guid.NewGuid(),
        ClientId = Guid.NewGuid(),
        AccessScope = scope,
        GrantedByUserId = Guid.NewGuid(),
        GrantedAtUtc = DateTime.UtcNow
    };
}
