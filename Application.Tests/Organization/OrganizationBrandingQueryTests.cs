using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Organization.Services;
using CarePath.Contracts.Organization;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using Moq;
using System.Linq.Expressions;
using ContractColorToken = CarePath.Contracts.Enumerations.BrandingColorToken;

namespace CarePath.Application.Tests.Organization;

public sealed class OrganizationBrandingQueryTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task GetForCurrentHost_WhenResolvedToTenantWithBranding_MapsTokensAndSupportFields()
    {
        // Arrange
        var branding = new OrganizationBranding
        {
            OrganizationId = OrganizationId,
            PrimaryColorToken = BrandingColorToken.Teal700,
            AccentColorToken = BrandingColorToken.Green,
            SupportEmail = "support@brightcare.test",
            SupportPhone = "555-0199",
            LogoStorageKey = "private/logo.png"
        };
        var query = CreateQuery(OrganizationResolutionKind.Tenant, [branding]);

        // Act
        var dto = await query.GetForCurrentHostAsync();

        // Assert
        dto.DisplayName.Should().Be("BrightCare");
        dto.Slug.Should().Be("brightcare");
        dto.PrimaryColorToken.Should().Be(ContractColorToken.Teal700);
        dto.AccentColorToken.Should().Be(ContractColorToken.Green);
        dto.SupportEmail.Should().Be("support@brightcare.test");
        typeof(OrganizationBrandingDto).GetProperties().Select(property => property.Name).Should().NotContain("LogoStorageKey");
    }

    [Fact]
    public async Task GetForCurrentHost_WhenTenantHasNoBrandingRow_ReturnsDefaults()
    {
        // Arrange
        var query = CreateQuery(OrganizationResolutionKind.Tenant, []);

        // Act
        var dto = await query.GetForCurrentHostAsync();

        // Assert
        dto.PrimaryColorToken.Should().Be(ContractColorToken.Teal900);
        dto.AccentColorToken.Should().Be(ContractColorToken.Orange);
        dto.SupportEmail.Should().BeNull();
    }

    [Theory]
    [InlineData(OrganizationResolutionKind.Unresolved)]
    [InlineData(OrganizationResolutionKind.Platform)]
    public async Task GetForCurrentHost_WhenNotResolvedToTenant_ThrowsNonPhiNotFound(OrganizationResolutionKind kind)
    {
        // Arrange
        var query = CreateQuery(kind, []);

        // Act
        var act = async () => await query.GetForCurrentHostAsync();

        // Assert
        (await act.Should().ThrowAsync<ResourceNotFoundException>()).Which.IsPhiResource.Should().BeFalse();
    }

    private static OrganizationBrandingQuery CreateQuery(OrganizationResolutionKind kind, IReadOnlyList<OrganizationBranding> rows)
    {
        var context = new Mock<IOrganizationContext>();
        context.SetupGet(c => c.Kind).Returns(kind);
        context.SetupGet(c => c.OrganizationId).Returns(kind == OrganizationResolutionKind.Tenant ? OrganizationId : null);
        context.SetupGet(c => c.Slug).Returns(kind == OrganizationResolutionKind.Tenant ? "brightcare" : null);
        context.SetupGet(c => c.DisplayName).Returns(kind == OrganizationResolutionKind.Tenant ? "BrightCare" : null);

        var brandings = new Mock<IRepository<OrganizationBranding>>();
        brandings
            .Setup(repository => repository.FindAsync(It.IsAny<Expression<Func<OrganizationBranding, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        var unitOfWork = new Mock<IControlPlaneUnitOfWork>();
        unitOfWork.SetupGet(u => u.Brandings).Returns(brandings.Object);

        var cache = new Mock<IBrandingCache>();
        cache
            .Setup(c => c.GetOrCreateAsync(It.IsAny<Guid>(), It.IsAny<Func<CancellationToken, Task<OrganizationBrandingDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Func<CancellationToken, Task<OrganizationBrandingDto>> factory, CancellationToken token) => factory(token));

        return new OrganizationBrandingQuery(context.Object, unitOfWork.Object, cache.Object);
    }
}
