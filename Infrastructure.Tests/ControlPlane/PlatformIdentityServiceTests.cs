using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Auth;
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
using Microsoft.Extensions.Time.Testing;

namespace CarePath.Infrastructure.Tests.ControlPlane;

public sealed class PlatformIdentityServiceTests
{
    private const string Password = "ValidPassword1";

    [Fact]
    public async Task ValidateCredentials_WhenPasswordMatches_ReturnsPlatformUser()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var result = await fixture.Service.ValidateCredentialsAsync("person@carepath.local", Password);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.UserId.Should().Be(fixture.UserId);
        result.DisplayName.Should().Be("Test Person");
        result.IsPlatformAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateCredentials_AfterFiveWrongPasswords_LocksTheAccountOut()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var codes = new List<string?>();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            codes.Add((await fixture.Service.ValidateCredentialsAsync("person@carepath.local", "wrong")).FailureCode);
        }

        var lockedOut = await fixture.Service.ValidateCredentialsAsync("person@carepath.local", Password);

        // Assert
        codes.Take(4).Should().OnlyContain(code => code == "InvalidCredentials");
        codes[4].Should().Be("LockedOut");
        lockedOut.Succeeded.Should().BeFalse();
        lockedOut.FailureCode.Should().Be("LockedOut");
    }

    [Fact]
    public async Task Rotate_WhenTokenIsValidAtItsOrganization_IssuesANewTokenAndRevokesTheOld()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var membershipId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var token = await fixture.Service.IssueRefreshTokenAsync(fixture.UserId, membershipId, organizationId);

        // Act
        var rotated = await fixture.Service.RotateRefreshTokenAsync(token, RefreshTokenBinding.ForOrganization(organizationId));
        var reused = await fixture.Service.RotateRefreshTokenAsync(token, RefreshTokenBinding.ForOrganization(organizationId));

        // Assert
        rotated.Succeeded.Should().BeTrue();
        rotated.RefreshToken.Should().NotBe(token);
        rotated.MembershipId.Should().Be(membershipId);
        rotated.OrganizationId.Should().Be(organizationId);
        reused.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Rotate_WhenARevokedTokenIsReused_RevokesTheWholeFamilyAndAudits()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var membershipId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var binding = RefreshTokenBinding.ForOrganization(organizationId);
        var first = await fixture.Service.IssueRefreshTokenAsync(fixture.UserId, membershipId, organizationId);
        var second = (await fixture.Service.RotateRefreshTokenAsync(first, binding)).RefreshToken!;
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));

        // Act
        var reuse = await fixture.Service.RotateRefreshTokenAsync(first, binding);
        var liveAfterReuse = await fixture.Context.PlatformRefreshTokens.CountAsync(row => row.RevokedAtUtc == null);
        var auditsAfterReuse = fixture.Audit.Entries.Count(entry => entry.Action == PlatformAuditAction.RefreshTokenReuseDetected);
        var latest = await fixture.Service.RotateRefreshTokenAsync(second, binding);

        // Assert
        reuse.Succeeded.Should().BeFalse();
        liveAfterReuse.Should().Be(0, "reuse of a spent token revokes the family");
        auditsAfterReuse.Should().Be(1);
        latest.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Rotate_WhenASpentTokenIsRetriedWithinTheGraceWindow_FailsWithoutRevokingTheFamily()
    {
        // Arrange — a dropped response or a second tab retries the token seconds later.
        await using var fixture = await Fixture.CreateAsync();
        var organizationId = Guid.NewGuid();
        var binding = RefreshTokenBinding.ForOrganization(organizationId);
        var first = await fixture.Service.IssueRefreshTokenAsync(fixture.UserId, Guid.NewGuid(), organizationId);
        var second = (await fixture.Service.RotateRefreshTokenAsync(first, binding)).RefreshToken!;
        fixture.Clock.Advance(TimeSpan.FromSeconds(5));

        // Act
        var retry = await fixture.Service.RotateRefreshTokenAsync(first, binding);
        var stillLive = await fixture.Service.RotateRefreshTokenAsync(second, binding);

        // Assert
        retry.Succeeded.Should().BeFalse();
        stillLive.Succeeded.Should().BeTrue();
        fixture.Audit.Entries.Should().NotContain(entry => entry.Action == PlatformAuditAction.RefreshTokenReuseDetected);
    }

    [Fact]
    public async Task Rotate_WhenPresentedAtAnotherOrganizationOrThePlatformHost_IsRejected()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var organizationId = Guid.NewGuid();
        var token = await fixture.Service.IssueRefreshTokenAsync(fixture.UserId, Guid.NewGuid(), organizationId);

        // Act
        var otherOrganization = await fixture.Service.RotateRefreshTokenAsync(token, RefreshTokenBinding.ForOrganization(Guid.NewGuid()));
        var platform = await fixture.Service.RotateRefreshTokenAsync(token, RefreshTokenBinding.Platform);
        var sameOrganization = await fixture.Service.RotateRefreshTokenAsync(token, RefreshTokenBinding.ForOrganization(organizationId));

        // Assert
        otherOrganization.Succeeded.Should().BeFalse();
        platform.Succeeded.Should().BeFalse();
        sameOrganization.Succeeded.Should().BeTrue("a binding mismatch does not spend the token");
    }

    [Fact]
    public async Task Rotate_WhenTokenHasExpired_IsRejected()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var organizationId = Guid.NewGuid();
        var token = await fixture.Service.IssueRefreshTokenAsync(fixture.UserId, Guid.NewGuid(), organizationId);
        fixture.Clock.Advance(TimeSpan.FromDays(8));

        // Act
        var result = await fixture.Service.RotateRefreshTokenAsync(token, RefreshTokenBinding.ForOrganization(organizationId));

        // Assert
        result.Succeeded.Should().BeFalse();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        private Fixture(ServiceProvider provider, ControlPlaneDbContext context, PlatformIdentityService service, RecordingAuditLogger audit, FakeTimeProvider clock, Guid userId)
        {
            _provider = provider;
            Context = context;
            Service = service;
            Audit = audit;
            Clock = clock;
            UserId = userId;
        }

        public ControlPlaneDbContext Context { get; }
        public PlatformIdentityService Service { get; }
        public RecordingAuditLogger Audit { get; }
        public FakeTimeProvider Clock { get; }
        public Guid UserId { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var context = new ControlPlaneDbContext(options, new AuditableEntityInterceptor(new HttpContextAccessor()));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(context);
            services.AddIdentityCore<PlatformUser>(identity =>
            {
                identity.Password.RequireNonAlphanumeric = false;
                identity.Lockout.MaxFailedAccessAttempts = 5;
                identity.Lockout.AllowedForNewUsers = true;
            }).AddEntityFrameworkStores<ControlPlaneDbContext>();
            var provider = services.BuildServiceProvider();

            var userManager = provider.GetRequiredService<UserManager<PlatformUser>>();
            var user = new PlatformUser { UserName = "person@carepath.local", Email = "person@carepath.local", DisplayName = "Test Person" };
            (await userManager.CreateAsync(user, Password)).Succeeded.Should().BeTrue();

            var audit = new RecordingAuditLogger();
            var clock = new FakeTimeProvider();
            var service = new PlatformIdentityService(context, userManager, audit, clock);
            return new Fixture(provider, context, service, audit, clock, user.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _provider.DisposeAsync();
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
