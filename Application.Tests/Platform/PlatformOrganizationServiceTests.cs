using System.Linq.Expressions;
using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Common.Exceptions;
using CarePath.Application.Platform.Services;
using CarePath.Contracts.Platform;
using CarePath.Domain.Entities.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Domain.Interfaces.Repositories;
using FluentAssertions;
using FluentValidation;
using Moq;
using OrganizationEntity = CarePath.Domain.Entities.Platform.Organization;

namespace CarePath.Application.Tests.Platform;

public sealed class PlatformOrganizationServiceTests
{
    [Fact]
    public async Task Register_CreatesPendingRegisteredOrganizationWithVerifiedSystemSubdomainAndAudits()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var dto = await harness.Service.RegisterAsync(new RegisterOrganizationRequest
        {
            LegalName = "BrightCare LLC",
            DisplayName = "BrightCare",
            Slug = "brightcare",
            DefaultTimeZone = "America/New_York",
            DataRegion = "us-east",
        });

        // Assert
        dto.Status.Should().Be(CarePath.Contracts.Enumerations.OrganizationStatus.Pending);
        dto.ProvisioningState.Should().Be(CarePath.Contracts.Enumerations.ProvisioningState.Registered);
        dto.Domains.Should().ContainSingle(domain => domain.HostName == "brightcare.carepathhealth.com" && domain.IsPrimary
            && domain.VerificationStatus == CarePath.Contracts.Enumerations.DomainVerificationStatus.Verified);
        dto.DataPlane.Should().BeNull();
        harness.Organizations.Should().ContainSingle(organization => organization.Slug == "brightcare" && !organization.IsAdmissible);
        harness.Brandings.Should().ContainSingle();
        harness.Audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.OrganizationRegistered);
    }

    [Theory]
    [InlineData("platform")]
    [InlineData("Bright Care")]
    [InlineData("ab")]
    public async Task Register_WhenSlugIsReservedOrMalformed_IsRejectedBeforeAnyWrite(string slug)
    {
        // Arrange
        var harness = new Harness();

        // Act
        var act = async () => await harness.Service.RegisterAsync(new RegisterOrganizationRequest { LegalName = "X", DisplayName = "X", Slug = slug });

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        harness.Organizations.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WhenSlugIsTaken_Conflicts()
    {
        // Arrange
        var harness = new Harness();
        harness.Organizations.Add(new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "taken" });

        // Act
        var act = async () => await harness.Service.RegisterAsync(new RegisterOrganizationRequest { LegalName = "Y", DisplayName = "Y", Slug = "taken" });

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("organization.slug_taken");
    }

    [Fact]
    public async Task Suspend_ThenReactivate_FollowsStatusRulesAndInvalidatesBranding()
    {
        // Arrange
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "x", Status = OrganizationStatus.Active, ProvisioningState = ProvisioningState.Provisioned };
        harness.Organizations.Add(organization);

        // Act
        var suspended = await harness.Service.SuspendAsync(organization.Id, new SuspendOrganizationRequest { Reason = "Non-payment" });
        var suspendTwice = async () => await harness.Service.SuspendAsync(organization.Id, new SuspendOrganizationRequest { Reason = "Again" });
        await suspendTwice.Should().ThrowAsync<ResourceConflictException>();
        var reactivated = await harness.Service.ReactivateAsync(organization.Id);

        // Assert
        suspended.Status.Should().Be(CarePath.Contracts.Enumerations.OrganizationStatus.Suspended);
        suspended.SuspensionReason.Should().Be("Non-payment");
        reactivated.Status.Should().Be(CarePath.Contracts.Enumerations.OrganizationStatus.Active);
        reactivated.SuspensionReason.Should().BeNull();
        harness.BrandingCache.Verify(cache => cache.Invalidate(organization.Id), Times.AtLeast(2));
        harness.Audit.Entries.Select(entry => entry.Action).Should().Contain([PlatformAuditAction.OrganizationSuspended, PlatformAuditAction.OrganizationReactivated]);
    }

    [Fact]
    public async Task Reactivate_WhenOrganizationIsNotProvisioned_Conflicts()
    {
        // Arrange — FR-015: only CP-06 provisioning can make a Pending organization usable.
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "x", Status = OrganizationStatus.Suspended, ProvisioningState = ProvisioningState.Registered };
        harness.Organizations.Add(organization);

        // Act
        var act = async () => await harness.Service.ReactivateAsync(organization.Id);

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("organization.not_provisioned");
    }

    [Fact]
    public async Task AddDomain_NormalizesHostVerifiesSystemSubdomainsOnlyAndRejectsTakenHosts()
    {
        // Arrange
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "brightcare" };
        harness.Organizations.Add(organization);

        // Act
        var system = await harness.Service.AddDomainAsync(organization.Id, new AddOrganizationDomainRequest { HostName = "BrightCare.CarePathHealth.com:443", IsPrimary = true });
        var custom = await harness.Service.AddDomainAsync(organization.Id, new AddOrganizationDomainRequest { HostName = "care.brightcare.org" });
        var taken = async () => await harness.Service.AddDomainAsync(organization.Id, new AddOrganizationDomainRequest { HostName = "care.brightcare.org" });

        // Assert
        system.HostName.Should().Be("brightcare.carepathhealth.com");
        system.VerificationStatus.Should().Be(CarePath.Contracts.Enumerations.DomainVerificationStatus.Verified);
        custom.VerificationStatus.Should().Be(CarePath.Contracts.Enumerations.DomainVerificationStatus.Pending);
        (await taken.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("organization.host_taken");
    }

    [Theory]
    [InlineData("platform.carepathhealth.com")]
    [InlineData("ops.example.net")]
    [InlineData("carepathhealth.com")]
    [InlineData("api.carepathhealth.com")]
    [InlineData("othercare.carepathhealth.com")]
    [InlineData("10.0.0.1")]
    public async Task AddDomain_WhenHostIsPlatformOwnedOrAnIpLiteral_IsRejectedWithoutAWrite(string host)
    {
        // Arrange — host-takeover guard: only the organization's own system subdomain may sit under the base domain.
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "brightcare" };
        harness.Organizations.Add(organization);

        // Act
        var act = async () => await harness.Service.AddDomainAsync(organization.Id, new AddOrganizationDomainRequest { HostName = host });

        // Assert
        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
        harness.Domains.Should().BeEmpty();
    }

    [Fact]
    public async Task AddDomain_WhenAPendingHostIsMarkedPrimary_Conflicts()
    {
        // Arrange
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "brightcare" };
        harness.Organizations.Add(organization);

        // Act
        var act = async () => await harness.Service.AddDomainAsync(organization.Id, new AddOrganizationDomainRequest { HostName = "care.brightcare.org", IsPrimary = true });

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("organization.domain_unverified");
        harness.Domains.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveDomain_WhenItIsThePrimaryHost_Conflicts()
    {
        // Arrange
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "x" };
        harness.Organizations.Add(organization);
        var primary = new OrganizationDomain { OrganizationId = organization.Id, HostName = "x.carepathhealth.com", IsPrimary = true, VerificationStatus = DomainVerificationStatus.Verified };
        var other = new OrganizationDomain { OrganizationId = organization.Id, HostName = "care.x.org", VerificationStatus = DomainVerificationStatus.Verified };
        harness.Domains.AddRange([primary, other]);

        // Act
        var act = async () => await harness.Service.RemoveDomainAsync(organization.Id, primary.Id);

        // Assert
        (await act.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("organization.primary_domain");
        primary.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetBranding_WhenNoRowExists_ReturnsDefaultsWithoutWriting()
    {
        // Arrange
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "x" };
        harness.Organizations.Add(organization);

        // Act
        var dto = await harness.Service.GetBrandingAsync(organization.Id);

        // Assert
        dto.PrimaryColorToken.Should().Be(CarePath.Contracts.Enumerations.BrandingColorToken.Teal900);
        harness.Brandings.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveDomain_WhenItIsTheLastVerifiedHost_Conflicts()
    {
        // Arrange
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "x" };
        harness.Organizations.Add(organization);
        var verified = new OrganizationDomain { OrganizationId = organization.Id, HostName = "x.carepathhealth.com", VerificationStatus = DomainVerificationStatus.Verified };
        var pending = new OrganizationDomain { OrganizationId = organization.Id, HostName = "x.example.org", VerificationStatus = DomainVerificationStatus.Pending };
        harness.Domains.AddRange([verified, pending]);

        // Act
        var removeVerified = async () => await harness.Service.RemoveDomainAsync(organization.Id, verified.Id);
        await harness.Service.RemoveDomainAsync(organization.Id, pending.Id);

        // Assert
        (await removeVerified.Should().ThrowAsync<ResourceConflictException>()).Which.Code.Should().Be("organization.last_verified_domain");
        pending.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateBranding_PersistsTokensAndInvalidatesTheCache()
    {
        // Arrange
        var harness = new Harness();
        var organization = new OrganizationEntity { LegalName = "X", DisplayName = "X", Slug = "x" };
        harness.Organizations.Add(organization);

        // Act
        var dto = await harness.Service.UpdateBrandingAsync(organization.Id, new UpdateOrganizationBrandingRequest
        {
            PrimaryColorToken = CarePath.Contracts.Enumerations.BrandingColorToken.Teal700,
            AccentColorToken = CarePath.Contracts.Enumerations.BrandingColorToken.Green,
            SupportEmail = "support@x.test",
        });

        // Assert
        dto.PrimaryColorToken.Should().Be(CarePath.Contracts.Enumerations.BrandingColorToken.Teal700);
        harness.Brandings.Single().AccentColorToken.Should().Be(BrandingColorToken.Green);
        harness.BrandingCache.Verify(cache => cache.Invalidate(organization.Id), Times.Once);
    }

    [Fact]
    public async Task Get_WhenOrganizationDoesNotExist_ThrowsNonPhiNotFound()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var act = async () => await harness.Service.GetAsync(Guid.NewGuid());

        // Assert
        (await act.Should().ThrowAsync<ResourceNotFoundException>()).Which.IsPhiResource.Should().BeFalse();
    }

    private sealed class Harness
    {
        public Harness()
        {
            var controlPlane = new Mock<IControlPlaneUnitOfWork>();
            controlPlane.SetupGet(unitOfWork => unitOfWork.Organizations).Returns(InMemoryRepository(Organizations));
            controlPlane.SetupGet(unitOfWork => unitOfWork.Domains).Returns(InMemoryRepository(Domains));
            controlPlane.SetupGet(unitOfWork => unitOfWork.Brandings).Returns(InMemoryRepository(Brandings));
            controlPlane.SetupGet(unitOfWork => unitOfWork.DataPlanes).Returns(InMemoryRepository(DataPlanes));
            controlPlane.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            controlPlane
                .Setup(unitOfWork => unitOfWork.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<CancellationToken, Task> operation, CancellationToken token) => operation(token));

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
            var settings = new Mock<IPlatformDomainSettings>();
            settings.SetupGet(s => s.PlatformBaseDomain).Returns("carepathhealth.com");
            settings.SetupGet(s => s.PlatformHostNames).Returns(new HashSet<string>(StringComparer.Ordinal) { "platform.carepathhealth.com", "ops.example.net" });

            Audit = new RecordingAuditLogger();
            Service = new PlatformOrganizationService(controlPlane.Object, currentUser.Object, Audit, BrandingCache.Object, settings.Object);
        }

        public PlatformOrganizationService Service { get; }
        public RecordingAuditLogger Audit { get; }
        public Mock<IBrandingCache> BrandingCache { get; } = new();
        public List<OrganizationEntity> Organizations { get; } = [];
        public List<OrganizationDomain> Domains { get; } = [];
        public List<OrganizationBranding> Brandings { get; } = [];
        public List<OrganizationDataPlane> DataPlanes { get; } = [];

        private static IRepository<T> InMemoryRepository<T>(List<T> store) where T : CarePath.Domain.Entities.Common.BaseEntity
        {
            IEnumerable<T> Live() => store.Where(entity => !entity.IsDeleted);
            var repository = new Mock<IRepository<T>>();
            repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => Live().FirstOrDefault(entity => entity.Id == id));
            repository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate, CancellationToken _) => (IReadOnlyList<T>)Live().Where(predicate.Compile()).ToList());
            repository.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate, CancellationToken _) => Live().Any(predicate.Compile()));
            repository.Setup(r => r.AddAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((T entity, CancellationToken _) => { store.Add(entity); return entity; });
            repository.Setup(r => r.UpdateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            repository.Setup(r => r.DeleteAsync(It.IsAny<T>(), It.IsAny<CancellationToken>()))
                .Returns((T entity, CancellationToken _) => { entity.IsDeleted = true; return Task.CompletedTask; });
            repository.Setup(r => r.GetPagedAsync(It.IsAny<Expression<Func<T, bool>>>(), It.IsAny<Expression<Func<T, string>>>(), It.IsAny<Expression<Func<T, string>>?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<T, bool>> predicate, Expression<Func<T, string>> _, Expression<Func<T, string>>? _, int _, int _, CancellationToken _) =>
                {
                    var rows = Live().Where(predicate.Compile()).ToList();
                    return ((IReadOnlyList<T>)rows, rows.Count);
                });
            return repository.Object;
        }
    }

    private sealed class RecordingAuditLogger : IPlatformAuditLogger
    {
        public List<PlatformAuditEntry> Entries { get; } = [];

        public Task LogAsync(PlatformAuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }
}
