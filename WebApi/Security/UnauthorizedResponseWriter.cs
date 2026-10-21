using System.Text.Json;
using System.Text.Json.Serialization;
using CarePath.Contracts.Common;
using CarePath.WebApi.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Security;

/// <summary>
/// The single generic 401 body. Used by the login/refresh failures, the organization resolution
/// middleware, and the bearer challenge, so unknown-host, suspended-organization,
/// wrong-organization, and invalid-credential responses are byte-identical (requirement §4.4).
/// </summary>
public static class UnauthorizedResponseWriter
{
    /// <summary>Error code carried by every generic 401.</summary>
    public const string Code = "auth.invalid_credentials";

    private const string Message = "Invalid credentials.";
    private const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Builds the generic body. Never includes a trace id or any request-specific value.</summary>
    public static ProblemDetailsResponse CreateBody() => new()
    {
        Type = "about:blank",
        Title = "Unauthorized.",
        Status = StatusCodes.Status401Unauthorized,
        Detail = null,
        Instance = null,
        TraceId = null,
        ValidationErrors = [],
        Errors = [new ApiError(Code, Message)],
    };

    /// <summary>Writes the generic 401 to the response. No-op when the response has already started.</summary>
    /// <param name="context">Current HTTP context.</param>
    public static async Task WriteAsync(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        // Status and content type only: Clear() would also drop headers CORS already set, turning
        // a deliberate 401 into an opaque browser error.
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = ContentType;
        await context.Response.WriteAsJsonAsync(CreateBody(), JsonOptions, CancellationToken.None);
    }

    /// <summary>An action result that writes the generic 401 through <see cref="WriteAsync"/>.</summary>
    public static ActionResult Result() => new GenericUnauthorizedResult();

    private sealed class GenericUnauthorizedResult : ActionResult
    {
        public override Task ExecuteResultAsync(ActionContext context) => WriteAsync(context.HttpContext);
    }
}
