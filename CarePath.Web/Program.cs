using CarePath.Client.Api;
using CarePath.Client.Http;
using CarePath.Web;
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

builder.Services.AddTransient<AuthorizationMessageHandler>();
AddApiClient<CaregiversClient>(builder.Services, apiBaseUri);
AddApiClient<ClientsClient>(builder.Services, apiBaseUri);
AddApiClient<ShiftsClient>(builder.Services, apiBaseUri);
AddApiClient<VisitNotesClient>(builder.Services, apiBaseUri);
AddApiClient<BillingClient>(builder.Services, apiBaseUri);
AddApiClient<TransitionsClient>(builder.Services, apiBaseUri);

await builder.Build().RunAsync();

static IHttpClientBuilder AddApiClient<TClient>(IServiceCollection services, Uri apiBaseUri)
    where TClient : class
{
    return services
        .AddHttpClient<TClient>(client => client.BaseAddress = apiBaseUri)
        .AddHttpMessageHandler<AuthorizationMessageHandler>();
}
