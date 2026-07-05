using CarePath.Contracts.Common;
using Microsoft.AspNetCore.Mvc;

namespace CarePath.WebApi.Validation;

public static class InvalidModelStateProblemFactory
{
    public static BadRequestObjectResult Create(ActionContext context)
    {
        var validationErrors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value?.Errors.Select(_ => new ValidationError(
                entry.Key,
                "The request is invalid.",
                null)) ?? [])
            .ToArray();

        var problem = new ApiProblemDetails
        {
            Type = "about:blank",
            Title = "Validation failed.",
            Status = StatusCodes.Status400BadRequest,
            TraceId = context.HttpContext.TraceIdentifier,
            ValidationErrors = validationErrors,
        };

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }
}