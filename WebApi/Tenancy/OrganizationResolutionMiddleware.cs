using CarePath.Application.Abstractions.Audit;
using CarePath.Application.Abstractions.Tenancy;
using CarePath.Application.Platform;
using CarePath.Domain.Enumerations;
using CarePath.Infrastructure.ControlPlane;
using CarePath.WebApi.Security;
using Microsoft.Extensions.Options;

namespace CarePath.WebApi.Tenancy;

/// <summary>
/// Resolves the organization from the request host before authentication (ADR 0003 §8.2 step 1)
/// and fails closed: an unresolved host gets the generic 401 (or, for the anonymous branding
/// route, continues so the controller returns the standard 404) and never reaches
/// authentication or a tenant lookup. A control-plane outage yields 503 with no detail.
/// </summary>
public sealed class OrganizationResolutionMiddleware
{
    private const string HealthPathPrefix = "/health";
    private const string BrandingPath = "/api/organization/branding";

    private static readonly System.Text.Json.JsonSerializerOptions ServiceUnavailableJsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

    private readonly RequestDelegate _next;
    private readonly ILogger<OrganizationResolutionMiddleware> _logger;

    /// <summary>Initializes the middleware.</summary>
    /// <param name="next">Next delegate.</param>
    /// <param name="logger">Logger; records reason codes only, never the host.</param>
    public OrganizationResolutionMiddleware(RequestDelegate next, ILogger<OrganizationResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Resolves the host and either continues or writes the fail-closed response.</summary>
    /// <param name="context">Current HTTP context.</param>
    /// <param name="organizationContext">Scoped holder to populate.</param>
    /// <param name="resolver">Authoritative control-plane resolver.</param>
    /// <param name="auditLogger">Platform audit logger for denied resolutions.</param>
    /// <param name="options">Control-plane options (platform hosts).</param>
    public async Task InvokeAsync(
        HttpContext context,
        OrganizationContext organizationContext,
        IOrganizationHostResolver resolver,
        IPlatformAuditLogger auditLogger,
        IOptions<ControlPlaneOptions> options)
    {
        if (context.Request.Path.StartsWithSegments(HealthPathPrefix))
        {
            await _next(context);
            return;
        }

        var normalizedHost = HostNameNormalizer.Normalize(context.Request.Host.Value);
        if (normalizedHost is not null && IsPlatformHost(normalizedHost, options.Value))
        {
            organizationContext.SetPlatform();
            await _next(context);
            return;
        }

        HostResolution resolution;
        if (normalizedHost is null)
        {
            resolution = HostResolution.Denied(HostDenialReason.MalformedHost);
        }
        else
        {
            try
            {
                resolution = await resolver.ResolveAsync(normalizedHost, context.RequestAborted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Never fall back to single-tenant behavior when the control plane is unreachable.
                _logger.LogError("Host resolution failed closed: control plane unavailable ({ExceptionType}).", exception.GetType().Name);
                await WriteServiceUnavailableAsync(context);
                return;
            }
        }

        if (resolution.IsResolved)
        {
            organizationContext.SetTenant(resolution.Organization!);
            await _next(context);
            return;
        }

        _logger.LogInformation("Host resolution denied: {Reason}.", resolution.DenialReason);
        await RecordDenialAsync(auditLogger, context, normalizedHost);

        if (IsAnonymousBrandingRequest(context))
        {
            // The branding controller turns the unresolved context into the standard 404 body.
            await _next(context);
            return;
        }

        await UnauthorizedResponseWriter.WriteAsync(context);
    }

    private static bool IsPlatformHost(string normalizedHost, ControlPlaneOptions options)
    {
        foreach (var configured in options.PlatformHostNames)
        {
            if (HostNameNormalizer.Normalize(configured) == normalizedHost)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAnonymousBrandingRequest(HttpContext context) =>
        HttpMethods.IsGet(context.Request.Method)
        && context.Request.Path.Value?.TrimEnd('/').Equals(BrandingPath, StringComparison.OrdinalIgnoreCase) == true;

    private async Task RecordDenialAsync(IPlatformAuditLogger auditLogger, HttpContext context, string? normalizedHost)
    {
        try
        {
            await auditLogger.LogAsync(
                new PlatformAuditEntry(
                    ActorPlatformUserId: null,
                    OrganizationId: null,
                    PlatformAuditAction.HostResolutionDenied,
                    EntityType: "Host",
                    EntityId: null,
                    CorrelationId: context.TraceIdentifier,
                    HostHash: normalizedHost is null ? null : HostHash.Compute(normalizedHost)),
                context.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            // Audit must not turn a denied anonymous request into a 500; the denial itself is still logged above.
            _logger.LogWarning("Host denial audit write failed ({ExceptionType}).", exception.GetType().Name);
        }
    }

    private static async Task WriteServiceUnavailableAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new Middleware.ProblemDetailsResponse
        {
            Type = "about:blank",
            Title = "Service unavailable.",
            Status = StatusCodes.Status503ServiceUnavailable,
            Detail = null,
            Instance = null,
            TraceId = null,
            ValidationErrors = [],
            Errors = [],
        }, ServiceUnavailableJsonOptions, CancellationToken.None);
    }
}
