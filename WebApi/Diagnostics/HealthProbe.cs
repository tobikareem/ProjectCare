namespace CarePath.WebApi.Diagnostics;

/// <summary>
/// In-container health probe invoked as <c>dotnet CarePath.WebApi.dll --healthcheck</c>.
/// The chiseled runtime image contains no shell, curl, or wget, so Docker cannot probe the
/// endpoint with an external tool; running the already-present .NET runtime avoids adding
/// packages to the image purely to support a health check.
/// </summary>
public static class HealthProbe
{
    /// <summary>Command-line argument that switches the process into probe mode.</summary>
    public const string Argument = "--healthcheck";

    private const string DefaultPort = "8080";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Requests the readiness endpoint over loopback and reports the result as a process exit
    /// code, which is what Docker interprets as healthy or unhealthy.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the probe request.</param>
    /// <returns><c>0</c> when the endpoint reports healthy; <c>1</c> otherwise.</returns>
    public static async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        // Read the port the app was told to listen on rather than assuming one, so the probe
        // keeps working if the container's ASPNETCORE_HTTP_PORTS changes.
        var configuredPorts = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS");
        var port = string.IsNullOrWhiteSpace(configuredPorts)
            ? DefaultPort
            : configuredPorts.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        var url = $"http://127.0.0.1:{port}/health/ready";

        try
        {
            using var client = new HttpClient { Timeout = Timeout };
            using var response = await client.GetAsync(url, cancellationToken);

            Console.WriteLine($"{url} -> {(int)response.StatusCode}");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"{url} -> unreachable ({exception.GetType().Name})");
            return 1;
        }
    }
}
