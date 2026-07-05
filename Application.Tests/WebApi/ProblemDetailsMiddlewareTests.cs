using System.Net;
using System.Text.Json;
using CarePath.Application.Common.Exceptions;
using CarePath.Contracts.Common;
using CarePath.WebApi.Middleware;
using CarePath.WebApi.Validation;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

namespace CarePath.Application.Tests.WebApi;

public sealed class ProblemDetailsMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenPhiMissingOrDenied_ReturnsIdenticalNotFoundBodies()
    {
        // Arrange
        using var missingServer = CreateServer(_ => throw new ResourceNotFoundException(isPhiResource: true));
        using var deniedServer = CreateServer(_ => throw new ResourceAccessDeniedException("NoGrant", isPhiResource: true));

        // Act
        var missingResponse = await missingServer.CreateClient().GetAsync("/phi-resource");
        var deniedResponse = await deniedServer.CreateClient().GetAsync("/phi-resource");
        var missingBody = await missingResponse.Content.ReadAsStringAsync();
        var deniedBody = await deniedResponse.Content.ReadAsStringAsync();

        // Assert
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deniedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingBody.Should().Be(deniedBody);
        missingBody.Should().NotContain("NoGrant");

        var problem = DeserializeProblem(missingBody);
        problem.Title.Should().Be("Resource not found.");
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Detail.Should().BeNull();
        problem.TraceId.Should().Be("trace-denial-test");
        var response = JsonSerializer.Deserialize<ProblemDetailsResponse>(missingBody, JsonOptions())
            ?? throw new InvalidOperationException("Problem details response could not be deserialized.");
        response.Errors.Should().ContainSingle(error =>
            error.Code == "resource.not_found" &&
            error.Message == "Resource not found.");
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationFails_ReturnsValidationProblemWithoutAttemptedValues()
    {
        // Arrange
        const string attemptedValue = "Submitted Sensitive Value";
        using var server = CreateServer(_ => throw new ValidationException(new[]
        {
            new ValidationFailure("StartAt", "Start time is required.")
            {
                ErrorCode = "shift.start_required",
                AttemptedValue = attemptedValue,
            },
        }));

        // Act
        var response = await server.CreateClient().PostAsync("/validate", null);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotContain(attemptedValue);

        var problem = DeserializeProblem(body);
        problem.Title.Should().Be("Validation failed.");
        problem.ValidationErrors.Should().ContainSingle(error =>
            error.PropertyName == "StartAt" &&
            error.ErrorMessage == "Start time is required." &&
            error.ErrorCode == "shift.start_required");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionOccurs_ReturnsGenericProblemWithoutExceptionText()
    {
        // Arrange
        const string exceptionText = "Sensitive exception text";
        using var server = CreateServer(_ => throw new InvalidOperationException(exceptionText));

        // Act
        var response = await server.CreateClient().GetAsync("/throws");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.Should().NotContain(exceptionText);

        var problem = DeserializeProblem(body);
        problem.Title.Should().Be("An unexpected error occurred.");
        problem.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problem.Detail.Should().BeNull();
    }


    [Fact]
    public void Create_WhenModelStateContainsSensitiveValue_ReturnsGenericValidationMessage()
    {
        // Arrange
        const string sensitiveValue = "Sensitive submitted value";
        var actionContext = new ActionContext
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "trace-model-state-test",
            },
        };
        actionContext.ModelState.AddModelError("Field", $"The value \'{sensitiveValue}\' is not valid for Field.");

        // Act
        var result = InvalidModelStateProblemFactory.Create(actionContext);

        // Assert
        var problem = result.Value.Should().BeOfType<ApiProblemDetails>().Subject;
        problem.TraceId.Should().Be("trace-model-state-test");
        problem.ValidationErrors.Should().ContainSingle(error =>
            error.PropertyName == "Field" &&
            error.ErrorMessage == "The request is invalid.");
        problem.ValidationErrors.Select(error => error.ErrorMessage).Should().NotContain(message => message.Contains(sensitiveValue, StringComparison.Ordinal));
    }

    private static TestServer CreateServer(Action<HttpContext> endpoint)
    {
        var builder = new WebHostBuilder()
            .Configure(app =>
            {
                app.UseCarePathProblemDetails();
                app.Run(context =>
                {
                    context.TraceIdentifier = "trace-denial-test";
                    endpoint(context);
                    return Task.CompletedTask;
                });
            });

        return new TestServer(builder);
    }

    private static ApiProblemDetails DeserializeProblem(string body)
    {
        var options = JsonOptions();

        return JsonSerializer.Deserialize<ApiProblemDetails>(body, options)
            ?? throw new InvalidOperationException("Problem details response could not be deserialized.");
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
    };
}