using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane.Auth;
using CarePath.Infrastructure.ControlPlane.Identity;
using CarePath.Infrastructure.ControlPlane.Persistence;
using CarePath.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CarePath.Infrastructure.Tests.ControlPlane;

public sealed class PlatformIdentityProvisioningServiceTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    [Fact]
    public async Task Provision_WhenEmailIsNew_CreatesIdentityWithTenantUserIdAndAnActiveMembership()
    {
        // Arrange
        using var fixture = Fixture.Create();
        var userId = Guid.NewGuid();

        // Act
        var result = await fixture.Service.ProvisionUserAsync(Request(userId, "new@carepath.local"));

        // Assert
        result.Succeeded.Should().BeTrue();
        var platformUser = await fixture.UserManager.FindByEmailAsync("new@carepath.local");
        platformUser!.Id.Should().Be(userId);
        platformUser.DisplayName.Should().Be("New Person");
        (await fixture.UserManager.CheckPasswordAsync(platformUser, "ValidPassword1")).Should().BeTrue();
        var membership = await fixture.Context.OrganizationMemberships.SingleAsync();
        membership.OrganizationId.Should().Be(OrganizationId);
        membership.PlatformUserId.Should().Be(userId);
        membership.TenantUserId.Should().Be(userId);
        membership.Role.Should().Be(UserRole.Coordinator);
        membership.Status.Should().Be(MembershipStatus.Active);
        fixture.Audit.Entries.Should().ContainSingle(entry => entry.Action == PlatformAuditAction.MembershipCreated);
    }

    [Fact]
    public async Task Provision_WhenEmailExistsUnderAnotherId_FailsWithoutCreatingAnything()
    {
        // Arrange — the caller created a fresh tenant user id, which would break the id invariant.
        using var fixture = Fixture.Create();
        await fixture.Service.ProvisionUserAsync(Request(Guid.NewGuid(), "taken@carepath.local"));

        // Act
        var result = await fixture.Service.ProvisionUserAsync(Request(Guid.NewGuid(), "taken@carepath.local"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("EmailUnavailable");
        (await fixture.Context.OrganizationMemberships.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Provision_WhenHostIsNotATenant_Fails()
    {
        // Arrange
        using var fixture = Fixture.Create(OrganizationResolutionKind.Platform);

        // Act
        var result = await fixture.Service.ProvisionUserAsync(Request(Guid.NewGuid(), "x@carepath.local"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("OrganizationUnresolved");
    }

    [Fact]
    public async Task Compensate_AfterProvisioning_DeactivatesAndSoftDeletesTheMembership()
    {
        // Arrange — the tenant transaction rolled back after provisioning committed.
        using var fixture = Fixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.ProvisionUserAsync(Request(userId, "ghost@carepath.local"));

        // Act
        await fixture.Service.CompensateAsync(userId);

        // Assert
        var membership = await fixture.Context.OrganizationMemberships.IgnoreQueryFilters().SingleAsync();
        membership.IsDeleted.Should().BeTrue();
        membership.Status.Should().Be(MembershipStatus.Inactive);
        membership.SecurityVersion.Should().Be(1);
        (await fixture.Context.OrganizationMemberships.CountAsync()).Should().Be(0, "the live-row filter must hide it");
    }

    private static IdentityProvisioningRequest Request(Guid userId, string email) =>
        new(userId, email, "555-0100", "New Person", "ValidPassword1", UserRole.Coordinator.ToString());

    private sealed class Fixture : IDisposable
    {
        private readonly ServiceProvider _provider;

        private Fixture(ServiceProvider provider, ControlPlaneDbContext context, UserManager<PlatformUser> userManager, PlatformIdentityProvisioningService service, RecordingAuditLogger audit)
        {
            _provider = provider;
            Context = context;
            UserManager = userManager;
            Service = service;
            Audit = audit;
        }

        public ControlPlaneDbContext Context { get; }
        public UserManager<PlatformUser> UserManager { get; }
        public PlatformIdentityProvisioningService Service { get; }
        public RecordingAuditLogger Audit { get; }

        public static Fixture Create(OrganizationResolutionKind kind = OrganizationResolutionKind.Tenant)
        {
            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(context);
            services.AddIdentityCore<PlatformUser>(identity => identity.Password.RequireNonAlphanumeric = false)
                .AddEntityFrameworkStores<ControlPlaneDbContext>();
            var provider = services.BuildServiceProvider();
            var userManager = provider.GetRequiredService<UserManager<PlatformUser>>();

            var organization = new Mock<IOrganizationContext>();
            organization.SetupGet(c => c.Kind).Returns(kind);
            organization.SetupGet(c => c.OrganizationId).Returns(kind == OrganizationResolutionKind.Tenant ? OrganizationId : null);
            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());

            var audit = new RecordingAuditLogger();
            var service = new PlatformIdentityProvisioningService(context, userManager, organization.Object, currentUser.Object, audit);
            return new Fixture(provider, context, userManager, service, audit);
        }

        public void Dispose()
        {
            Context.Dispose();
            _provider.Dispose();
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
