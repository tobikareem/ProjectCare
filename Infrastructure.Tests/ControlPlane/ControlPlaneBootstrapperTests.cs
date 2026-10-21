using CarePath.Application.Abstractions.Audit;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane;
using CarePath.Infrastructure.ControlPlane.Bootstrap;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace CarePath.Infrastructure.Tests.ControlPlane;

public sealed class ControlPlaneBootstrapperTests
{
    [Fact]
    public async Task EnsureDefaultOrganization_OnEmptyControlPlane_CreatesOrganizationDomainsBrandingAndDataPlane()
    {
        // Arrange
        await using var context = CreateContext();
        var audit = new Mock<IPlatformAuditLogger>();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), audit.Object);

        // Act
        var organizationId = await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        var organization = await context.Organizations.SingleAsync();
        organizationId.Should().Be(organization.Id);
        organization.Slug.Should().Be("carepath");
        organization.Status.Should().Be(OrganizationStatus.Active);
        organization.ProvisioningState.Should().Be(ProvisioningState.Provisioned);
        organization.IsAdmissible.Should().BeTrue();

        var domains = await context.OrganizationDomains.OrderBy(domain => domain.HostName).ToListAsync();
        domains.Select(domain => domain.HostName).Should().Equal("carepath.localhost", "localhost");
        domains.Should().OnlyContain(domain => domain.VerificationStatus == DomainVerificationStatus.Verified);
        domains.Single(domain => domain.IsPrimary).HostName.Should().Be("localhost");

        (await context.OrganizationBrandings.SingleAsync()).OrganizationId.Should().Be(organization.Id);
        var dataPlane = await context.OrganizationDataPlanes.SingleAsync();
        dataPlane.SecretReference.Should().Be("ConnectionStrings:DefaultConnection");
        dataPlane.Provider.Should().Be(DataPlaneProvider.SqlServer);

        audit.Verify(logger => logger.LogAsync(
            It.Is<PlatformAuditEntry>(entry => entry.Action == PlatformAuditAction.BackfillOrganizationCreated && entry.OrganizationId == organization.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenRunTwice_ChangesNothing()
    {
        // Arrange
        await using var context = CreateContext();
        var audit = new Mock<IPlatformAuditLogger>();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), audit.Object);
        var firstId = await bootstrapper.EnsureDefaultOrganizationAsync();
        var snapshot = await Snapshot(context);

        // Act
        var secondId = await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        secondId.Should().Be(firstId);
        (await Snapshot(context)).Should().Be(snapshot);
        audit.Verify(logger => logger.LogAsync(It.IsAny<PlatformAuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenControlPlaneIsEmptyAndConfigurationIsMissing_ThrowsNamingTheKey()
    {
        // Arrange
        await using var context = CreateContext();
        var bootstrapper = CreateBootstrapper(context, new ControlPlaneOptions(), Mock.Of<IPlatformAuditLogger>());

        // Act
        var act = async () => await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ControlPlane:DefaultOrganization*");
        (await context.Organizations.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenControlPlaneHasOrganizationsAndNoConfiguration_IsANoOp()
    {
        // Arrange
        await using var context = CreateContext();
        context.Organizations.Add(new Organization { LegalName = "Other", DisplayName = "Other", Slug = "other" });
        await context.SaveChangesAsync();
        var bootstrapper = CreateBootstrapper(context, new ControlPlaneOptions(), Mock.Of<IPlatformAuditLogger>());

        // Act
        var result = await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        result.Should().BeNull();
        (await context.Organizations.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenSlugBelongsToARemovedOrganization_ThrowsAConfigurationError()
    {
        // Arrange
        await using var context = CreateContext();
        context.Organizations.Add(new Organization { LegalName = "Old", DisplayName = "Old", Slug = "carepath", IsDeleted = true });
        await context.SaveChangesAsync();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), Mock.Of<IPlatformAuditLogger>());

        // Act
        var act = async () => await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*removed*");
    }

    [Fact]
    public async Task EnsureDefaultOrganization_WhenAHostBelongsToAnotherOrganization_ThrowsAConfigurationError()
    {
        // Arrange
        await using var context = CreateContext();
        var other = new Organization { LegalName = "Other", DisplayName = "Other", Slug = "other" };
        context.Organizations.Add(other);
        context.OrganizationDomains.Add(new OrganizationDomain { OrganizationId = other.Id, HostName = "localhost", VerificationStatus = DomainVerificationStatus.Verified });
        await context.SaveChangesAsync();
        var bootstrapper = CreateBootstrapper(context, DefaultOptions(), Mock.Of<IPlatformAuditLogger>());

        // Act
        var act = async () => await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already registered*");
    }

    [Theory]
    [InlineData("Platform", "localhost")]
    [InlineData("ab", "localhost")]
    [InlineData("carepath", "bad host")]
    [InlineData("carepath", "")]
    public async Task EnsureDefaultOrganization_WhenSlugOrHostIsInvalid_ThrowsAndWritesNothing(string slug, string host)
    {
        // Arrange
        await using var context = CreateContext();
        var options = DefaultOptions();
        options.DefaultOrganization!.Slug = slug;
        options.DefaultOrganization.HostNames = host.Length == 0 ? [] : [host];
        var bootstrapper = CreateBootstrapper(context, options, Mock.Of<IPlatformAuditLogger>());

        // Act
        var act = async () => await bootstrapper.EnsureDefaultOrganizationAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ControlPlane:DefaultOrganization*");
        (await context.Organizations.AnyAsync()).Should().BeFalse();
    }

    private static async Task<string> Snapshot(ControlPlaneDbContext context)
    {
        var organizations = await context.Organizations.Select(organization => new { organization.Id, organization.UpdatedAt }).ToListAsync();
        var domains = await context.OrganizationDomains.CountAsync();
        var brandings = await context.OrganizationBrandings.CountAsync();
        var dataPlanes = await context.OrganizationDataPlanes.CountAsync();
        return $"{string.Join(',', organizations)}|{domains}|{brandings}|{dataPlanes}";
    }

    private static ControlPlaneOptions DefaultOptions() => new()
    {
        DefaultOrganization = new DefaultOrganizationOptions
        {
            LegalName = "CarePath Health LLC",
            DisplayName = "CarePath Health",
            Slug = "carepath",
            HostNames = ["LOCALHOST:5240", "carepath.localhost", "localhost"],
            DataPlane = new DefaultDataPlaneOptions { Provider = DataPlaneProvider.SqlServer, SecretReference = "ConnectionStrings:DefaultConnection" }
        }
    };

    private static ControlPlaneBootstrapper CreateBootstrapper(ControlPlaneDbContext context, ControlPlaneOptions options, IPlatformAuditLogger audit) =>
        new(context, Options.Create(options), audit, new FakeTimeProvider(), NullLogger<ControlPlaneBootstrapper>.Instance);

    private static ControlPlaneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
    }
}
