using System.Net;
using System.Text;
using CarePath.Client.Api;
using CarePath.Contracts.Common;
using CarePath.Contracts.Organization;
using CarePath.Contracts.Platform;
using FluentAssertions;

namespace CarePath.Application.Tests.WebApi;

/// <summary>
/// Pins the CP-04 typed-client request shapes to the WebApi route templates asserted in
/// <see cref="Sprint4ControllerContractTests"/>, so client and server cannot drift apart
/// silently (TASK-096). Membership and branding clients never send an organization id.
/// </summary>
public sealed class Cp04ClientRouteAlignmentTests
{
    private static readonly Guid FixedId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
    private static readonly Guid DomainId = Guid.Parse("0d2a1b3c-4e5f-4a6b-8c7d-9e0f1a2b3c4d");

    [Fact]
    public async Task BrandingGetAsync_WhenCalled_TargetsAnonymousBrandingRouteWithNoIdentifier()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new OrganizationBrandingClient(CreateHttpClient(handler));

        // Act
        await client.GetAsync();

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/organization/branding");
    }

    [Fact]
    public async Task ReactivateAsync_WhenCalled_SendsNoBodyAndReadsThePayload()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new PlatformOrganizationsClient(CreateHttpClient(handler));

        // Act
        var response = await client.ReactivateAsync(FixedId);

        // Assert
        handler.LastRequest!.Content.Should().BeNull();
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task MembershipsGetPageAsync_WhenCalled_TargetsMembershipsRouteWithPagingOnly()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new OrganizationMembershipsClient(CreateHttpClient(handler));

        // Act
        await client.GetPageAsync(new PagedRequest { PageNumber = 2, PageSize = 25 });

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/organization/memberships?pageNumber=2&pageSize=25");
    }

    [Fact]
    public async Task MembershipsCreateAsync_WhenCalled_PostsToMembershipsRouteWithBodyNotQuery()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new OrganizationMembershipsClient(CreateHttpClient(handler));

        // Act
        await client.CreateAsync(new CreateMembershipRequest { Email = "person@example.test", TemporaryPassword = "Secret-Value-1" });

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/organization/memberships");
        handler.LastRequest.RequestUri.PathAndQuery.Should().NotContain("Secret");
        handler.LastBody.Should().Contain("person@example.test");
    }

    [Fact]
    public async Task MembershipsUpdateRoleAndStatusAsync_WhenCalled_TargetGuidScopedPutRoutes()
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new OrganizationMembershipsClient(CreateHttpClient(handler));

        // Act
        await client.UpdateRoleAsync(FixedId, new UpdateMembershipRoleRequest());
        var rolePath = handler.LastRequest!.RequestUri!.PathAndQuery;
        await client.UpdateStatusAsync(FixedId, new UpdateMembershipStatusRequest());

        // Assert
        rolePath.Should().Be($"/api/organization/memberships/{FixedId}/role");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Put);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be($"/api/organization/memberships/{FixedId}/status");
    }

    [Theory]
    [InlineData("list", "GET", "/api/platform/organizations?pageNumber=1&pageSize=20")]
    [InlineData("register", "POST", "/api/platform/organizations")]
    [InlineData("get", "GET", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    [InlineData("update", "PUT", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    [InlineData("suspend", "POST", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff/suspend")]
    [InlineData("reactivate", "POST", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff/reactivate")]
    [InlineData("domains", "GET", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff/domains")]
    [InlineData("add-domain", "POST", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff/domains")]
    [InlineData("remove-domain", "DELETE", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff/domains/0d2a1b3c-4e5f-4a6b-8c7d-9e0f1a2b3c4d")]
    [InlineData("branding", "GET", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff/branding")]
    [InlineData("update-branding", "PUT", "/api/platform/organizations/6f9619ff-8b86-d011-b42d-00cf4fc964ff/branding")]
    public async Task PlatformOrganizationsClient_EachOperation_TargetsItsControllerRoute(string operation, string method, string expectedPath)
    {
        // Arrange
        var handler = new RecordingHandler();
        var client = new PlatformOrganizationsClient(CreateHttpClient(handler));

        // Act
        Task call = operation switch
        {
            "list" => client.GetPageAsync(new PagedRequest()),
            "register" => client.RegisterAsync(new RegisterOrganizationRequest()),
            "get" => client.GetAsync(FixedId),
            "update" => client.UpdateAsync(FixedId, new UpdateOrganizationRequest()),
            "suspend" => client.SuspendAsync(FixedId, new SuspendOrganizationRequest()),
            "reactivate" => client.ReactivateAsync(FixedId),
            "domains" => client.GetDomainsAsync(FixedId),
            "add-domain" => client.AddDomainAsync(FixedId, new AddOrganizationDomainRequest()),
            "remove-domain" => client.RemoveDomainAsync(FixedId, DomainId),
            "branding" => client.GetBrandingAsync(FixedId),
            "update-branding" => client.UpdateBrandingAsync(FixedId, new UpdateOrganizationBrandingRequest()),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        await call;

        // Assert
        handler.LastRequest!.Method.Method.Should().Be(method);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be(expectedPath);
    }

    private static HttpClient CreateHttpClient(RecordingHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var body = request.RequestUri!.AbsolutePath.EndsWith("/domains", StringComparison.Ordinal) && request.Method == HttpMethod.Get ? "[]" : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
