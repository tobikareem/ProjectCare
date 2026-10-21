using CarePath.Client.Api;
using CarePath.Client.Http;
using CarePath.Web;
using CarePath.Web.Auth;
using CarePath.Web.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// CP-04: an empty Api:BaseAddress means same-origin — the API is served from the host the app was
// loaded from, so the organization the server resolves is the one the user navigated to.
var apiBaseAddress = builder.Configuration["Api:BaseAddress"];
Uri apiBaseUri;
if (string.IsNullOrWhiteSpace(apiBaseAddress))
{
    apiBaseUri = new Uri(builder.HostEnvironment.BaseAddress, UriKind.Absolute);
}
else if (!Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out apiBaseUri!))
{
    throw new InvalidOperationException("Api:BaseAddress must be an absolute URL, or empty/whitespace for same-origin, in wwwroot/appsettings.json.");
}

builder.Services.AddSingleton<InMemoryAccessTokenProvider>();
builder.Services.AddSingleton<IAccessTokenProvider>(services =>
    services.GetRequiredService<InMemoryAccessTokenProvider>());
builder.Services.AddTransient<AuthorizationMessageHandler>();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<TokenAuthenticationStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(services =>
    services.GetRequiredService<TokenAuthenticationStateProvider>());
builder.Services.AddScoped<BillingReconciliationNavigationState>();
AddApiClient<AuthClient>(builder.Services, apiBaseUri);
AddApiClient<CaregiversClient>(builder.Services, apiBaseUri);
AddApiClient<ClientsClient>(builder.Services, apiBaseUri);
AddApiClient<ShiftsClient>(builder.Services, apiBaseUri);
AddApiClient<VisitNotesClient>(builder.Services, apiBaseUri);
AddApiClient<BillingClient>(builder.Services, apiBaseUri);
AddApiClient<TransitionsClient>(builder.Services, apiBaseUri);
AddApiClient<AdminUsersClient>(builder.Services, apiBaseUri);
// Anonymous by contract (FR-012): no authorization handler, so a stale session token is never
// presented to the public branding endpoint.
builder.Services.AddHttpClient<OrganizationBrandingClient>(client => client.BaseAddress = apiBaseUri);
AddApiClient<OrganizationMembershipsClient>(builder.Services, apiBaseUri);
AddApiClient<PlatformOrganizationsClient>(builder.Services, apiBaseUri);

await builder.Build().RunAsync();

static IHttpClientBuilder AddApiClient<TClient>(IServiceCollection services, Uri apiBaseUri)
    where TClient : class
{
    return services
        .AddHttpClient<TClient>(client => client.BaseAddress = apiBaseUri)
        .AddHttpMessageHandler<AuthorizationMessageHandler>();
}
