using CarePath.Application.Abstractions.Tenancy;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.ControlPlane.Resolution;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Infrastructure.Tests.ControlPlane;

public sealed class EfOrganizationHostResolverTests
{
    [Fact]
    public async Task Resolve_WhenHostIsVerifiedAndOrganizationIsAdmissible_ReturnsTenant()
    {
        // Arrange
        await using var context = CreateContext();
        var organization = AddOrganization(context, "brightcare", OrganizationStatus.Active, ProvisioningState.Provisioned);
        AddDomain(context, organization, "brightcare.test", DomainVerificationStatus.Verified);
        await context.SaveChangesAsync();
        var resolver = new EfOrganizationHostResolver(context);

        // Act
        var resolution = await resolver.ResolveAsync("brightcare.test");

        // Assert
        resolution.IsResolved.Should().BeTrue();
        resolution.Organization!.OrganizationId.Should().Be(organization.Id);
        resolution.Organization.Slug.Should().Be("brightcare");
    }

    [Theory]
    [InlineData(OrganizationStatus.Pending, ProvisioningState.Registered, DomainVerificationStatus.Verified, false, HostDenialReason.OrganizationNotAdmissible)]
    [InlineData(OrganizationStatus.Suspended, ProvisioningState.Provisioned, DomainVerificationStatus.Verified, false, HostDenialReason.OrganizationNotAdmissible)]
    [InlineData(OrganizationStatus.Active, ProvisioningState.Provisioned, DomainVerificationStatus.Pending, false, HostDenialReason.UnverifiedDomain)]
    [InlineData(OrganizationStatus.Active, ProvisioningState.Provisioned, DomainVerificationStatus.Verified, true, HostDenialReason.UnknownHost)]
    public async Task Resolve_WhenHostIsNotAdmissible_DeniesWithLogOnlyReason(
        OrganizationStatus status,
        ProvisioningState provisioningState,
        DomainVerificationStatus verification,
        bool domainDeleted,
        HostDenialReason expectedReason)
    {
        // Arrange
        await using var context = CreateContext();
        var organization = AddOrganization(context, "org", status, provisioningState);
        var domain = AddDomain(context, organization, "org.test", verification);
        domain.IsDeleted = domainDeleted;
        await context.SaveChangesAsync();
        var resolver = new EfOrganizationHostResolver(context);

        // Act
        var resolution = await resolver.ResolveAsync("org.test");

        // Assert
        resolution.IsResolved.Should().BeFalse();
        resolution.Organization.Should().BeNull();
        resolution.DenialReason.Should().Be(expectedReason);
    }

    [Fact]
    public async Task Resolve_WhenHostIsUnknown_DeniesAsUnknown()
    {
        // Arrange
        await using var context = CreateContext();
        var resolver = new EfOrganizationHostResolver(context);

        // Act
        var resolution = await resolver.ResolveAsync("nobody.test");

        // Assert
        resolution.DenialReason.Should().Be(HostDenialReason.UnknownHost);
    }

    private static Organization AddOrganization(ControlPlaneDbContext context, string slug, OrganizationStatus status, ProvisioningState state)
    {
        var organization = new Organization { LegalName = slug, DisplayName = slug, Slug = slug, Status = status, ProvisioningState = state };
        context.Organizations.Add(organization);
        return organization;
    }

    private static OrganizationDomain AddDomain(ControlPlaneDbContext context, Organization organization, string host, DomainVerificationStatus status)
    {
        var domain = new OrganizationDomain { OrganizationId = organization.Id, HostName = host, IsPrimary = true, VerificationStatus = status };
        context.OrganizationDomains.Add(domain);
        return domain;
    }

    private static ControlPlaneDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));
    }
}
