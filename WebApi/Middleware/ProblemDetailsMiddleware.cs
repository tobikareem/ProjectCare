using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Common;
using FluentValidation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarePath.WebApi.Middleware;

public sealed class ProblemDetailsMiddleware
{
    private const string ResourceNotFoundCode = "resource.not_found";
    private static readonly JsonSerializerOptions ProblemJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly RequestDelegate next;
    private readonly ILogger<ProblemDetailsMiddleware> logger;

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException exception)
        {
            await WriteValidationFailureAsync(context, exception);
        }
        catch (ResourceNotFoundException exception)
        {
            await WriteNotFoundAsync(context, exception.IsPhiResource);
        }
        catch (ResourceAccessDeniedException exception)
        {
            await WriteAccessDeniedAsync(context, exception.IsPhiResource);
        }
        catch (ResourceConflictException exception)
        {
            await WriteConflictAsync(context, exception);
        }
        catch (Exception)
        {
            var traceId = GetTraceId(context);
            logger.LogError("Unhandled exception while processing request. TraceId: {TraceId}", traceId);
            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                detail: null,
                validationErrors: [],
                errors: []);
        }
    }

    private static Task WriteAccessDeniedAsync(HttpContext context, bool isPhiResource)
    {
        return isPhiResource
            ? WriteNotFoundAsync(context, isPhiResource)
            : WriteProblemAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden.",
                detail: null,
                validationErrors: [],
                errors: []);
    }

    private static Task WriteNotFoundAsync(HttpContext context, bool isPhiResource)
    {
        _ = isPhiResource;
        return WriteProblemAsync(
            context,
            StatusCodes.Status404NotFound,
            "Resource not found.",
            detail: null,
            validationErrors: [],
            errors: [new ApiError(ResourceNotFoundCode, "Resource not found.")],
            includeTraceId: false);
    }


    private static Task WriteConflictAsync(HttpContext context, ResourceConflictException exception)
    {
        return WriteProblemAsync(
            context,
            StatusCodes.Status409Conflict,
            "Conflict.",
            detail: null,
            validationErrors: [],
            errors: [new ApiError(exception.Code, exception.Message)]);
    }
    private static Task WriteValidationFailureAsync(HttpContext context, ValidationException exception)
    {
        var validationErrors = exception.Errors
            .Select(error => new ValidationError(
                error.PropertyName,
                "The request is invalid.",
                string.IsNullOrWhiteSpace(error.ErrorCode) ? null : error.ErrorCode))
            .ToArray();

        return WriteProblemAsync(
            context,
            StatusCodes.Status400BadRequest,
            "Validation failed.",
            detail: null,
            validationErrors,
            errors: []);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title,
        string? detail,
        IReadOnlyList<ValidationError> validationErrors,
        IReadOnlyList<ApiError> errors,
        bool includeTraceId = true)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetailsResponse
        {
            Type = "about:blank",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = null,
            TraceId = includeTraceId ? GetTraceId(context) : null,
            ValidationErrors = validationErrors,
            Errors = errors,
        };

        await context.Response.WriteAsJsonAsync(problem, ProblemJsonOptions);
    }

    private static string GetTraceId(HttpContext context) => context.TraceIdentifier;
}
