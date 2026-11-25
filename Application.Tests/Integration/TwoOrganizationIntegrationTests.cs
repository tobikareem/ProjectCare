using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarePath.Contracts.Auth;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using CarePath.Contracts.Organization;
using CarePath.Contracts.Platform;
using CarePath.WebApi.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CarePath.Application.Tests.Integration;

/// <summary>
/// CP-04 success criteria (requirements §5.1, TASK-098) proven end-to-end in one process: two
/// Active organizations, one control plane, the real middleware and JWT pipeline.
/// </summary>
public sealed class TwoOrganizationIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ControlPlaneTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ControlPlaneTestHost.CreateAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Login_AtEachHost_IssuesATokenBoundToThatOrganizationOnly()
    {
        // Act — the shared clinician holds a membership in both organizations.
        var brightCare = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.SharedClinician);
        var helpingHands = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.HelpingHandsHost, _host.SharedClinician);

        // Assert
        brightCare.TokenKind.Should().Be(AuthTokenKind.Tenant);
        brightCare.OrganizationId.Should().Be(_host.BrightCareId);
        brightCare.OrganizationSlug.Should().Be("brightcare");
        brightCare.Role.Should().Be(UserRole.Clinician);
        helpingHands.OrganizationId.Should().Be(_host.HelpingHandsId);
        helpingHands.MembershipId.Should().NotBeNull().And.NotBe(brightCare.MembershipId!.Value);
        helpingHands.AccessToken.Should().NotBe(brightCare.AccessToken);
    }

    [Fact]
    public async Task Login_WithoutAMembershipAtThatHost_IsTheSameGeneric401AsAnUnknownHostAndWrongPassword()
    {
        // Act
        using var noMembership = await _host.LoginAsync(ControlPlaneTestHost.HelpingHandsHost, _host.BrightCareAdmin);
        using var unknownHost = await _host.LoginAsync(ControlPlaneTestHost.UnknownHost, _host.BrightCareAdmin);
        using var wrongPasswordClient = _host.ClientFor(ControlPlaneTestHost.BrightCareHost);
        using var wrongPassword = await wrongPasswordClient.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = _host.BrightCareAdmin.Email, Password = "Wrong-Pass-1" });

        // Assert — byte-identical bodies: no membership, host, or account oracle (requirement §4.4).
        var bodies = new[] { await Body(noMembership), await Body(unknownHost), await Body(wrongPassword) };
        new[] { noMembership.StatusCode, unknownHost.StatusCode, wrongPassword.StatusCode }.Should().AllBeEquivalentTo(HttpStatusCode.Unauthorized);
        bodies.Distinct().Should().ContainSingle();
        bodies[0].Should().Contain(UnauthorizedResponseWriter.Code);
    }

    [Fact]
    public async Task TenantToken_PresentedAtAnotherOrganizationHost_IsRejectedBeforeAnyControllerRuns()
    {
        // Arrange
        var token = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.BrightCareAdmin);
        var probesBefore = _host.Probe.Count;

        // Act
        using var ownHost = _host.ClientFor(ControlPlaneTestHost.BrightCareHost, token.AccessToken);
        using var atOwnHost = await ownHost.GetAsync("api/integration/probe");
        using var otherHost = _host.ClientFor(ControlPlaneTestHost.HelpingHandsHost, token.AccessToken);
        using var atOtherHost = await otherHost.GetAsync("api/integration/probe");
        using var unknown = _host.ClientFor(ControlPlaneTestHost.UnknownHost, token.AccessToken);
        using var atUnknownHost = await unknown.GetAsync("api/integration/probe");
        using var platform = _host.ClientFor(ControlPlaneTestHost.PlatformHost, token.AccessToken);
        using var atPlatformHost = await platform.GetAsync("api/platform/organizations");

        // Assert
        atOwnHost.StatusCode.Should().Be(HttpStatusCode.OK);
        atOtherHost.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        atUnknownHost.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        atPlatformHost.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Body(atOtherHost)).Should().Be(await Body(atUnknownHost));
        _host.Probe.Count.Should().Be(probesBefore + 1);
    }

    [Fact]
    public async Task TenantToken_AtItsOwnHost_Gets403OnPlatformRoutesAndPlatformToken_Gets401AtTenantHosts()
    {
        // Arrange
        var tenant = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.BrightCareAdmin);
        var platform = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.PlatformHost, _host.PlatformOperator);

        // Act
        using var tenantClient = _host.ClientFor(ControlPlaneTestHost.BrightCareHost, tenant.AccessToken);
        using var tenantOnPlatformRoute = await tenantClient.GetAsync("api/platform/organizations");
        using var platformAtTenantHost = _host.ClientFor(ControlPlaneTestHost.BrightCareHost, platform.AccessToken);
        using var platformOnTenantRoute = await platformAtTenantHost.GetAsync("api/organization/memberships");
        using var platformClient = _host.ClientFor(ControlPlaneTestHost.PlatformHost, platform.AccessToken);
        using var platformOnPlatformRoute = await platformClient.GetAsync("api/platform/organizations");

        // Assert
        platform.TokenKind.Should().Be(AuthTokenKind.Platform);
        platform.Role.Should().BeNull();
        platform.OrganizationId.Should().BeNull();
        tenantOnPlatformRoute.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        platformOnTenantRoute.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        platformOnPlatformRoute.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await platformOnPlatformRoute.Content.ReadFromJsonAsync<PagedResult<OrganizationSummaryDto>>(JsonOptions);
        page!.Items.Select(item => item.Slug).Should().BeEquivalentTo(["brightcare", "helpinghands"]);
    }

    [Fact]
    public async Task MembershipList_AtAHost_ContainsOnlyThatOrganizationsMembers()
    {
        // Arrange
        var token = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.BrightCareAdmin);

        // Act
        using var client = _host.ClientFor(ControlPlaneTestHost.BrightCareHost, token.AccessToken);
        var page = await client.GetFromJsonAsync<PagedResult<MembershipDto>>("api/organization/memberships?pageNumber=1&pageSize=20", JsonOptions);

        // Assert
        page!.Items.Select(item => item.Email).Should().BeEquivalentTo([_host.BrightCareAdmin.Email, _host.SharedClinician.Email]);
        page.Items.Should().NotContain(item => item.Email == _host.HelpingHandsCaregiver.Email);
    }

    [Fact]
    public async Task Suspension_TakesEffectOnTheNextRequestWithoutWaitingForTokenExpiry()
    {
        // Arrange
        var operatorToken = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.PlatformHost, _host.PlatformOperator);
        var caregiverToken = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.HelpingHandsHost, _host.HelpingHandsCaregiver);
        using var caregiverClient = _host.ClientFor(ControlPlaneTestHost.HelpingHandsHost, caregiverToken.AccessToken);
        using var before = await caregiverClient.GetAsync("api/integration/probe");

        // Act
        using var platformClient = _host.ClientFor(ControlPlaneTestHost.PlatformHost, operatorToken.AccessToken);
        using var suspend = await platformClient.PostAsJsonAsync($"api/platform/organizations/{_host.HelpingHandsId}/suspend", new SuspendOrganizationRequest { Reason = "Contract lapsed" });
        using var after = await caregiverClient.GetAsync("api/integration/probe");
        using var login = await _host.LoginAsync(ControlPlaneTestHost.HelpingHandsHost, _host.HelpingHandsCaregiver);
        using var branding = await _host.ClientFor(ControlPlaneTestHost.HelpingHandsHost).GetAsync("api/organization/branding");
        using var reactivate = await platformClient.PostAsync($"api/platform/organizations/{_host.HelpingHandsId}/reactivate", content: null);
        using var restored = await caregiverClient.GetAsync("api/integration/probe");

        // Assert
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        suspend.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        branding.StatusCode.Should().Be(HttpStatusCode.NotFound);
        reactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_AtAnotherHostOrAfterDeactivation_IsRejectedWhileRefreshAtTheIssuingHostRotates()
    {
        // Arrange
        var adminToken = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.BrightCareAdmin);
        var clinicianToken = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.SharedClinician);

        // Act — cross-host refresh
        using var otherHost = _host.ClientFor(ControlPlaneTestHost.HelpingHandsHost);
        using var crossHost = await otherHost.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest { RefreshToken = clinicianToken.RefreshToken });

        // Act — refresh at the issuing host rotates
        using var ownHost = _host.ClientFor(ControlPlaneTestHost.BrightCareHost);
        using var rotated = await ownHost.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest { RefreshToken = clinicianToken.RefreshToken });
        var rotatedTokens = await rotated.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);

        // Act — deactivate the clinician's BrightCare membership, then refresh the rotated token
        using var adminClient = _host.ClientFor(ControlPlaneTestHost.BrightCareHost, adminToken.AccessToken);
        using var deactivate = await adminClient.PutAsJsonAsync($"api/organization/memberships/{clinicianToken.MembershipId}/status", new UpdateMembershipStatusRequest { IsActive = false });
        using var afterDeactivation = await ownHost.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest { RefreshToken = rotatedTokens!.RefreshToken });
        using var accessAfterDeactivation = _host.ClientFor(ControlPlaneTestHost.BrightCareHost, rotatedTokens.AccessToken);
        using var accessTokenStillValid = await accessAfterDeactivation.GetAsync("api/integration/probe");

        // Assert
        crossHost.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        rotatedTokens.OrganizationId.Should().Be(_host.BrightCareId);
        rotatedTokens.RefreshToken.Should().NotBe(clinicianToken.RefreshToken);
        deactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        afterDeactivation.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Characterization (D-CP04-05): the already-issued access token stays valid until it expires;
        // CP-05 adds the SecurityVersion claim check that flips this to 401 on the next request.
        accessTokenStillValid.StatusCode.Should().Be(HttpStatusCode.OK);

        // The Helping Hands membership is untouched in the control plane. (A login there is not
        // asserted: this host shares one tenant context between both organizations, as the
        // development seed does, so the mirrored User.IsActive = false applies to both until CP-06
        // gives each organization its own data plane — D-CP04-09.)
        await _host.WithControlPlaneAsync(async controlPlane =>
        {
            var memberships = await controlPlane.OrganizationMemberships
                .Where(candidate => candidate.PlatformUserId == _host.SharedClinician.Id)
                .ToListAsync();
            memberships.Single(candidate => candidate.OrganizationId == _host.BrightCareId).Status.Should().Be(CarePath.Domain.Enumerations.MembershipStatus.Inactive);
            memberships.Single(candidate => candidate.OrganizationId == _host.HelpingHandsId).Status.Should().Be(CarePath.Domain.Enumerations.MembershipStatus.Active);
        });
    }

    [Fact]
    public async Task MembershipChange_MirrorsTheTenantUserAndBumpsSecurityVersion()
    {
        // Arrange
        var adminToken = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.BrightCareAdmin);
        var clinicianToken = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.SharedClinician);

        // Act
        using var adminClient = _host.ClientFor(ControlPlaneTestHost.BrightCareHost, adminToken.AccessToken);
        using var changed = await adminClient.PutAsJsonAsync(
            $"api/organization/memberships/{clinicianToken.MembershipId}/role",
            new UpdateMembershipRoleRequest { Role = UserRole.Coordinator });
        using var selfDemotion = await adminClient.PutAsJsonAsync(
            $"api/organization/memberships/{adminToken.MembershipId}/role",
            new UpdateMembershipRoleRequest { Role = UserRole.Coordinator });

        // Assert
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        selfDemotion.StatusCode.Should().Be(HttpStatusCode.Conflict, "the last active Admin cannot be demoted");
        await _host.WithControlPlaneAsync(async controlPlane =>
        {
            var membership = await controlPlane.OrganizationMemberships.SingleAsync(candidate => candidate.Id == clinicianToken.MembershipId);
            membership.Role.Should().Be(CarePath.Domain.Enumerations.UserRole.Coordinator);
            membership.SecurityVersion.Should().Be(1, "one change since the seeded value of 0");
            var audit = await controlPlane.PlatformAuditEvents.Where(entry => entry.Action == CarePath.Domain.Enumerations.PlatformAuditAction.MembershipRoleChanged).ToListAsync();
            audit.Should().ContainSingle();
        });
    }

    [Fact]
    public async Task Branding_ResolvesPerHostAnonymously()
    {
        // Act
        using var brightCare = _host.ClientFor(ControlPlaneTestHost.BrightCareHost);
        var first = await brightCare.GetFromJsonAsync<OrganizationBrandingDto>("api/organization/branding", JsonOptions);
        using var helpingHands = _host.ClientFor(ControlPlaneTestHost.HelpingHandsHost);
        var second = await helpingHands.GetFromJsonAsync<OrganizationBrandingDto>("api/organization/branding", JsonOptions);
        using var unknown = await _host.ClientFor(ControlPlaneTestHost.UnknownHost).GetAsync("api/organization/branding");
        using var platform = await _host.ClientFor(ControlPlaneTestHost.PlatformHost).GetAsync("api/organization/branding");

        // Assert
        first!.DisplayName.Should().Be("BrightCare");
        first.PrimaryColorToken.Should().Be(BrandingColorToken.Teal700);
        second!.DisplayName.Should().Be("Helping Hands");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        platform.StatusCode.Should().Be(HttpStatusCode.NotFound, "the platform host is not an organization");
    }

    [Theory]
    [InlineData("BRIGHTCARE.TEST")]
    [InlineData("brightcare.test.")]
    [InlineData("brightcare.test:8443")]
    public async Task HostHeader_VariantsOfTheSameHost_ResolveToTheSameOrganization(string hostHeader)
    {
        // Act
        using var client = _host.ClientFor(hostHeader);
        var branding = await client.GetFromJsonAsync<OrganizationBrandingDto>("api/organization/branding", JsonOptions);

        // Assert
        branding!.Slug.Should().Be("brightcare");
    }

    [Fact]
    public async Task ForwardedHostHeader_WithoutATrustedProxy_NeverChoosesTheOrganization()
    {
        // Arrange — ControlPlane:TrustedProxies is empty in this host: forwarded headers are ignored.
        var token = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.BrightCareHost, _host.BrightCareAdmin);
        var probesBefore = _host.Probe.Count;

        // Act
        using var unknown = _host.ClientFor(ControlPlaneTestHost.UnknownHost, token.AccessToken);
        unknown.DefaultRequestHeaders.Add("X-Forwarded-Host", ControlPlaneTestHost.BrightCareHost);
        using var atUnknown = await unknown.GetAsync("api/integration/probe");
        using var platform = _host.ClientFor(ControlPlaneTestHost.PlatformHost, token.AccessToken);
        platform.DefaultRequestHeaders.Add("X-Forwarded-Host", ControlPlaneTestHost.BrightCareHost);
        using var atPlatform = await platform.GetAsync("api/integration/probe");
        using var anonymous = _host.ClientFor(ControlPlaneTestHost.UnknownHost);
        anonymous.DefaultRequestHeaders.Add("X-Forwarded-Host", ControlPlaneTestHost.BrightCareHost);
        using var branding = await anonymous.GetAsync("api/organization/branding");

        // Assert
        atUnknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        atPlatform.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        branding.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _host.Probe.Count.Should().Be(probesBefore);
    }

    [Fact]
    public async Task RefreshToken_OfTheWrongKindOrAtASuspendedOrganization_IsRejected()
    {
        // Arrange
        var tenant = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.HelpingHandsHost, _host.HelpingHandsCaregiver);
        var platform = await _host.LoginExpectingSuccessAsync(ControlPlaneTestHost.PlatformHost, _host.PlatformOperator);

        // Act — kind mismatch in both directions
        using var tenantHost = _host.ClientFor(ControlPlaneTestHost.HelpingHandsHost);
        using var platformRefreshAtTenantHost = await tenantHost.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest { RefreshToken = platform.RefreshToken });
        using var platformHost = _host.ClientFor(ControlPlaneTestHost.PlatformHost);
        using var tenantRefreshAtPlatformHost = await platformHost.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest { RefreshToken = tenant.RefreshToken });

        // Act — suspension closes refresh too
        using var operatorClient = _host.ClientFor(ControlPlaneTestHost.PlatformHost, platform.AccessToken);
        using var suspend = await operatorClient.PostAsJsonAsync($"api/platform/organizations/{_host.HelpingHandsId}/suspend", new SuspendOrganizationRequest { Reason = "Audit hold" });
        using var refreshWhileSuspended = await tenantHost.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest { RefreshToken = tenant.RefreshToken });
        using var reactivate = await operatorClient.PostAsync($"api/platform/organizations/{_host.HelpingHandsId}/reactivate", content: null);

        // Assert
        platformRefreshAtTenantHost.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        tenantRefreshAtPlatformHost.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        suspend.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshWhileSuspended.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        reactivate.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PlatformLogin_ByAnOrganizationMemberWhoIsNotAnOperator_IsTheGeneric401AndAudited()
    {
        // Act
        using var response = await _host.LoginAsync(ControlPlaneTestHost.PlatformHost, _host.BrightCareAdmin);
        using var reference = await _host.LoginAsync(ControlPlaneTestHost.UnknownHost, _host.BrightCareAdmin);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Body(response)).Should().Be(await Body(reference));
        await _host.WithControlPlaneAsync(async controlPlane =>
        {
            var denied = await controlPlane.PlatformAuditEvents
                .Where(entry => entry.Action == CarePath.Domain.Enumerations.PlatformAuditAction.LoginDeniedNotPlatformAdmin)
                .ToListAsync();
            denied.Should().ContainSingle().Which.ActorPlatformUserId.Should().Be(_host.BrightCareAdmin.Id);
            var hostDenials = await controlPlane.PlatformAuditEvents
                .Where(entry => entry.Action == CarePath.Domain.Enumerations.PlatformAuditAction.HostResolutionDenied)
                .ToListAsync();
            hostDenials.Should().NotBeEmpty();
            hostDenials.Should().OnlyContain(entry => entry.HostHash != null && !entry.HostHash.Contains(ControlPlaneTestHost.UnknownHost));
        });
    }

    private static async Task<string> Body(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();
}
