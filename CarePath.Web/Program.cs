using CarePath.Client.Api;
using CarePath.Client.Http;
using CarePath.Web;
using CarePath.Web.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = builder.Configuration["Api:BaseAddress"];
if (!Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out var apiBaseUri))
{
    throw new InvalidOperationException("Api:BaseAddress must be configured in wwwroot/appsettings.json.");
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
AddApiClient<AuthClient>(builder.Services, apiBaseUri);
AddApiClient<CaregiversClient>(builder.Services, apiBaseUri);
AddApiClient<ClientsClient>(builder.Services, apiBaseUri);
AddApiClient<ShiftsClient>(builder.Services, apiBaseUri);
AddApiClient<VisitNotesClient>(builder.Services, apiBaseUri);
AddApiClient<BillingClient>(builder.Services, apiBaseUri);
AddApiClient<TransitionsClient>(builder.Services, apiBaseUri);
AddApiClient<AdminUsersClient>(builder.Services, apiBaseUri);

await builder.Build().RunAsync();

static IHttpClientBuilder AddApiClient<TClient>(IServiceCollection services, Uri apiBaseUri)
    where TClient : class
{
    return services
        .AddHttpClient<TClient>(client => client.BaseAddress = apiBaseUri)
        .AddHttpMessageHandler<AuthorizationMessageHandler>();
}
