using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bunit;
using CarePath.Client.Api;
using CarePath.Client.Http;
using CarePath.Client.UI.Theming;
using CarePath.Contracts.Common;
using CarePath.Contracts.Enumerations;
using CarePath.Contracts.Organization;
using CarePath.Web.Auth;
using CarePath.Web.Pages;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarePath.Web.Tests;

/// <summary>CP-04 TASK-097: the login page renders the organization the host resolved to, and falls back silently.</summary>
public sealed class LoginBrandingTests
{
    [Fact]
    public void Login_WhenBrandingResolves_RendersOrganizationNameMonogramAndTokenStyle()
    {
        // Arrange
        using var context = new BunitContext();
        var handler = new BrandingHandler(new OrganizationBrandingDto
        {
            DisplayName = "Bright Care Partners",
            Slug = "brightcare",
            PrimaryColorToken = BrandingColorToken.Teal700,
            AccentColorToken = BrandingColorToken.Green,
        });
        AddServices(context, handler);

        // Act
        var component = context.Render<Login>();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("Bright Care Partners");
            component.Find(".auth-brand-mark").TextContent.Should().Be("BC");
            component.Find(".auth-card").GetAttribute("style").Should().Be("--brand-primary: var(--teal-700); --brand-accent: var(--green);");
            component.Find(".auth-card").GetAttribute("style").Should().NotContainAny("#", "rgb");
        });
        handler.Requests.Should().ContainSingle(path => path == "api/organization/branding");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public void Login_WhenBrandingIsUnavailable_FallsBackToNeutralBrandAndStillSignsIn(HttpStatusCode statusCode)
    {
        // Arrange
        using var context = new BunitContext();
        var handler = new BrandingHandler(null, statusCode);
        AddServices(context, handler);

        // Act
        var component = context.Render<Login>();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Should().Contain("CarePath");
            component.Find(".auth-brand-mark").TextContent.Should().Be("CA");
            component.Find(".auth-card").GetAttribute("style").Should().Be("--brand-primary: var(--teal-900); --brand-accent: var(--orange);");
            component.Find("button[type='submit']").Should().NotBeNull();
            component.Markup.Should().NotContain("Not found");
        });
    }

    [Theory]
    [InlineData("html")]
    [InlineData("throw")]
    public void Login_WhenBrandingResponseIsNotJsonOrTransportFails_FallsBackToNeutralBrand(string mode)
    {
        // Arrange — a static host answering /api with index.html, or no network at all.
        using var context = new BunitContext();
        var handler = new BrandingHandler(null, HttpStatusCode.OK) { Mode = mode };
        AddServices(context, handler);

        // Act
        var component = context.Render<Login>();

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Find(".auth-brand-mark").TextContent.Should().Be("CA");
            component.Find("button[type='submit']").Should().NotBeNull();
        });
    }

    [Fact]
    public void BrandStyle_WhenPrimaryIsALightTint_UsesAContrastSafeToken()
    {
        BrandingTokens.BrandStyle(BrandingColorToken.Teal100, BrandingColorToken.Amber)
            .Should().Be("--brand-primary: var(--teal-700); --brand-accent: var(--amber);");
    }

    [Theory]
    [InlineData("Bright Care", "BC")]
    [InlineData("A", "A")]
    [InlineData("42 Care", "4C")]
    [InlineData("helpinghands", "HE")]
    [InlineData("CarePath", "CA")]
    [InlineData("  ", "CP")]
    [InlineData("Émile & Co", "MC")]
    [InlineData("&&&", "CP")]
    public void Monogram_FromDisplayName_UsesAsciiInitialsOnly(string? name, string expected)
    {
        BrandingTokens.Monogram(name).Should().Be(expected);
    }

    private static void AddServices(BunitContext context, BrandingHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/") };
        context.Services.AddSingleton(new AuthClient(httpClient));
        context.Services.AddSingleton(new OrganizationBrandingClient(httpClient));
        context.Services.AddSingleton(new TokenAuthenticationStateProvider(new InMemoryAccessTokenProvider()));
    }

    private sealed class BrandingHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly OrganizationBrandingDto? _branding;
        private readonly HttpStatusCode _failure;

        public BrandingHandler(OrganizationBrandingDto? branding, HttpStatusCode failure = HttpStatusCode.OK)
        {
            _branding = branding;
            _failure = failure;
        }

        public List<string> Requests { get; } = [];

        public string? Mode { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty);
            if (Mode == "throw")
            {
                throw new HttpRequestException("offline");
            }

            if (Mode == "html")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<!doctype html><html></html>", System.Text.Encoding.UTF8, "text/html") });
            }

            if (_branding is not null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_branding, options: JsonOptions) });
            }

            var problem = new ApiProblemDetails { Title = "Not found", Status = (int)_failure };
            return Task.FromResult(new HttpResponseMessage(_failure) { Content = JsonContent.Create(problem, options: JsonOptions) });
        }
    }
}
