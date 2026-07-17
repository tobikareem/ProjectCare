using System.Security.Claims;
using Bunit;
using CarePath.Web.Layout;
using CarePath.Web.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CarePath.Web.Tests;

public sealed class NavigationAndErrorBoundaryTests
{
    [Theory]
    [InlineData("Admin", true, true, true, true, true, false, false)]
    [InlineData("Coordinator", true, true, true, false, false, false, false)]
    [InlineData("Clinician", false, true, false, false, false, false, false)]
    [InlineData("Caregiver", false, false, false, false, false, true, false)]
    [InlineData("Client", false, false, false, false, false, false, true)]
    public void NavMenu_ForRole_RendersOnlyAuthorizedDestinations(
        string role,
        bool showsOperations,
        bool showsClinicalWorkspace,
        bool showsBusiness,
        bool showsAnalytics,
        bool showsAdmin,
        bool showsMyClients,
        bool showsMyCaregivers)
    {
        // Arrange
        using var context = new BunitContext();
        context.Services.AddAuthorizationCore();
        context.Services.AddSingleton<IAuthorizationService, RoleAuthorizationService>();
        context.Services.AddSingleton<AuthenticationStateProvider>(new RoleAuthStateProvider(role));

        // Act
        var component = context.Render<CascadingAuthenticationState>(parameters =>
            parameters.AddChildContent<NavMenu>());

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Markup.Contains("href=\"schedule\"").Should().Be(showsOperations);
            component.Markup.Contains("href=\"caregivers\"").Should().Be(showsOperations);
            component.Markup.Contains("href=\"clients\"").Should().Be(showsClinicalWorkspace);
            component.Markup.Contains("href=\"visit-notes\"").Should().Be(showsClinicalWorkspace);
            component.Markup.Contains("href=\"billing\"").Should().Be(showsBusiness);
            component.Markup.Contains("href=\"compliance\"").Should().Be(showsBusiness);
            component.Markup.Contains("href=\"analytics\"").Should().Be(showsAnalytics);
            component.Markup.Contains("href=\"admin/users\"").Should().Be(showsAdmin);
            component.Markup.Contains("href=\"my-clients\"").Should().Be(showsMyClients);
            component.Markup.Contains("href=\"my-caregivers\"").Should().Be(showsMyCaregivers);
        });
    }

    [Fact]
    public void SanitizedErrorBoundary_WhenChildThrows_HidesExceptionDetails()
    {
        // Arrange
        using var context = new BunitContext();
        var logger = new CapturingLogger<SanitizedErrorBoundary>();
        context.Services.AddSingleton<ILogger<SanitizedErrorBoundary>>(logger);

        // Act
        var component = context.Render<SanitizedErrorBoundary>(parameters =>
            parameters.AddChildContent<ThrowOnceComponent>());

        // Assert
        component.WaitForAssertion(() =>
        {
            component.Find("[role='alert']").TextContent.Should().Contain("Something went wrong");
            component.Markup.Should().NotContain(ThrowOnceComponent.SensitiveExceptionText);
            component.Markup.Should().NotContain("InvalidOperationException");
            logger.Messages.Should().NotContain(message =>
                message.Contains(ThrowOnceComponent.SensitiveExceptionText, StringComparison.Ordinal));
        });

    }

    private sealed class RoleAuthStateProvider(string role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "Test User"), new Claim(ClaimTypes.Role, role)],
                "Test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private sealed class RoleAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            var authorized = requirements
                .OfType<RolesAuthorizationRequirement>()
                .All(requirement => requirement.AllowedRoles.Any(user.IsInRole));
            return Task.FromResult(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            string policyName) => Task.FromResult(AuthorizationResult.Failed());
    }

    private sealed class ThrowOnceComponent : ComponentBase
    {
        internal const string SensitiveExceptionText = "Jordan diagnosis must never render";
        private bool hasThrown;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (!hasThrown)
            {
                hasThrown = true;
                throw new InvalidOperationException(SensitiveExceptionText);
            }

            builder.AddContent(0, "Recovered page content");
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
