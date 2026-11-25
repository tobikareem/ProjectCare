using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.ControlPlane;

public sealed class ControlPlaneDbContextTests
{
    [Fact]
    public void Model_WhenContextIsCreated_ContainsControlPlaneTypesAndNoTenantOrRoleTypes()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act
        var entityTypes = context.Model.GetEntityTypes().Select(entity => entity.ClrType).ToList();

        // Assert
        entityTypes.Should().Contain(
        [
            typeof(Organization),
            typeof(OrganizationDomain),
            typeof(OrganizationBranding),
            typeof(OrganizationMembership),
            typeof(OrganizationDataPlane),
            typeof(PlatformAuditEvent),
            typeof(PlatformRefreshToken),
            typeof(PlatformUser)
        ]);
        entityTypes.Should().NotContain(typeof(Domain.Entities.Identity.User));
        entityTypes.Should().NotContain(typeof(Domain.Entities.Identity.Client));
        entityTypes.Select(type => type.Name).Should().NotContain(name => name.StartsWith("IdentityRole", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Organizations_WhenSoftDeleted_AreExcludedByTheQueryFilter()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        context.Organizations.Add(new Organization { LegalName = "Gone LLC", DisplayName = "Gone", Slug = "gone", IsDeleted = true });
        context.Organizations.Add(new Organization { LegalName = "Here LLC", DisplayName = "Here", Slug = "here" });
        await context.SaveChangesAsync();

        // Act
        var slugs = await context.Organizations.Select(organization => organization.Slug).ToListAsync();

        // Assert
        slugs.Should().Equal("here");
    }

    [Fact]
    public async Task SaveChanges_WhenEntityIsAdded_PopulatesAuditFieldsThroughTheSharedInterceptor()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var organization = new Organization { LegalName = "Audit LLC", DisplayName = "Audit", Slug = "audit" };

        // Act
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        // Assert
        organization.CreatedBy.Should().Be("System");
        organization.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Model_WhenInspectedOnSqlServer_HasExpectedUniqueIndexesAndRestrictDeletes()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MetadataOnly;Encrypt=True;TrustServerCertificate=True;")
            .Options;
        using var context = new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
        var model = context.Model;

        // Act
        var slugIndex = model.FindEntityType(typeof(Organization))!.GetIndexes().Single(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(Organization.Slug));
        var hostIndex = model.FindEntityType(typeof(OrganizationDomain))!.GetIndexes().Single(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(OrganizationDomain.HostName));
        var membershipIndex = model.FindEntityType(typeof(OrganizationMembership))!.GetIndexes().Single(index => index.IsUnique);
        var tokenIndex = model.FindEntityType(typeof(PlatformRefreshToken))!.GetIndexes().Single(index => index.Properties.Count == 1 && index.Properties[0].Name == nameof(PlatformRefreshToken.TokenHash));
        var foreignKeys = model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()).ToList();

        // Assert
        slugIndex.IsUnique.Should().BeTrue();
        hostIndex.IsUnique.Should().BeTrue();
        membershipIndex.IsUnique.Should().BeTrue();
        membershipIndex.Properties.Select(property => property.Name).Should().Equal(nameof(OrganizationMembership.OrganizationId), nameof(OrganizationMembership.PlatformUserId));
        tokenIndex.IsUnique.Should().BeTrue();
        foreignKeys.Should().NotBeEmpty();
        foreignKeys.Where(foreignKey => foreignKey.DeclaringEntityType.ClrType.Namespace == typeof(Organization).Namespace || foreignKey.DeclaringEntityType.ClrType == typeof(PlatformRefreshToken))
            .Should().OnlyContain(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void Model_WhenInspected_StoresEnumsAsBoundedStrings()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Act
        var status = context.Model.FindEntityType(typeof(Organization))!.FindProperty(nameof(Organization.Status))!;
        var role = context.Model.FindEntityType(typeof(OrganizationMembership))!.FindProperty(nameof(OrganizationMembership.Role))!;

        // Assert
        status.GetProviderClrType().Should().Be(typeof(string));
        status.GetMaxLength().Should().Be(32);
        role.GetProviderClrType().Should().Be(typeof(string));
        role.GetMaxLength().Should().Be(32);
    }

    [Fact]
    public void ControlPlaneUnitOfWork_WhenInspected_ExposesNoRepositoryForPlatformAuditEvent()
    {
        // Act
        var repositoryTypes = typeof(IControlPlaneUnitOfWork)
            .GetProperties()
            .Select(property => property.PropertyType)
            .Where(type => type.IsGenericType)
            .Select(type => type.GetGenericArguments()[0])
            .ToList();

        // Assert
        repositoryTypes.Should().NotContain(typeof(PlatformAuditEvent));
        repositoryTypes.Should().Contain(typeof(OrganizationMembership));
    }

    [Fact]
    public async Task ControlPlaneUnitOfWork_WhenUsed_RoundTripsThroughRepositories()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        await using var unitOfWork = new ControlPlaneUnitOfWork(context);
        var organization = new Organization { LegalName = "Round Trip LLC", DisplayName = "Round Trip", Slug = "round-trip", Status = OrganizationStatus.Active, ProvisioningState = ProvisioningState.Provisioned };

        // Act
        await unitOfWork.Organizations.AddAsync(organization);
        await unitOfWork.SaveChangesAsync();
        var loaded = await unitOfWork.Organizations.GetByIdAsync(organization.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.IsAdmissible.Should().BeTrue();
    }

    [Fact]
    public async Task SaveChanges_WhenAnAuditRowIsModifiedOrDeleted_Throws()
    {
        // Arrange
        await using var context = CreateInMemoryContext();
        var auditEvent = new PlatformAuditEvent { Action = PlatformAuditAction.OrganizationRegistered, EntityType = "Organization", OccurredAtUtc = DateTime.UtcNow };
        context.PlatformAuditEvents.Add(auditEvent);
        await context.SaveChangesAsync();

        // Act
        auditEvent.EntityType = "Tampered";
        var update = async () => await context.SaveChangesAsync();
        var updateError = await update.Should().ThrowAsync<InvalidOperationException>();

        context.ChangeTracker.Clear();
        context.PlatformAuditEvents.Remove(auditEvent);
        var delete = async () => await context.SaveChangesAsync();

        // Assert
        updateError.WithMessage("*append-only*");
        await delete.Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");
    }

    [Fact]
    public void Model_WhenInspectedOnSqlServer_FiltersLiveRowsOnSoftDeleteAwareUniqueIndexes()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MetadataOnly;Encrypt=True;TrustServerCertificate=True;")
            .Options;
        using var context = new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));

        // Act
        var hostIndex = context.Model.FindEntityType(typeof(OrganizationDomain))!.GetIndexes().Single(index => index.IsUnique);
        var membershipIndex = context.Model.FindEntityType(typeof(OrganizationMembership))!.GetIndexes().Single(index => index.IsUnique);
        var membershipForeignKeys = context.Model.FindEntityType(typeof(OrganizationMembership))!.GetForeignKeys().Select(key => key.PrincipalEntityType.ClrType).ToList();
        var tokenForeignKeys = context.Model.FindEntityType(typeof(PlatformRefreshToken))!.GetForeignKeys().Select(key => key.PrincipalEntityType.ClrType).ToList();

        // Assert
        hostIndex.GetFilter().Should().Be("[IsDeleted] = 0");
        membershipIndex.GetFilter().Should().Be("[IsDeleted] = 0");
        membershipForeignKeys.Should().BeEquivalentTo([typeof(Organization), typeof(PlatformUser)]);
        tokenForeignKeys.Should().BeEquivalentTo([typeof(PlatformUser), typeof(OrganizationMembership), typeof(Organization)]);
    }

    [Fact]
    public void Model_WhenInspectedOnPostgreSql_UsesBooleanFilterSyntax()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql("Host=metadata-only;Database=metadata;Username=metadata")
            .Options;
        using var context = new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));

        // Act
        var hostIndex = context.Model.FindEntityType(typeof(OrganizationDomain))!.GetIndexes().Single(index => index.IsUnique);

        // Assert
        hostIndex.GetFilter().Should().Be("\"IsDeleted\" = false");
    }

    private static ControlPlaneDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
    }
}
