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
using Microsoft.Extensions.Hosting;

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
        var missingResponse = await missingServer.GetTestClient().GetAsync("/phi-resource");
        var deniedResponse = await deniedServer.GetTestClient().GetAsync("/phi-resource");
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
        problem.TraceId.Should().BeNull();
        var response = JsonSerializer.Deserialize<ProblemDetailsResponse>(missingBody, JsonOptions())
            ?? throw new InvalidOperationException("Problem details response could not be deserialized.");
        response.Errors.Should().ContainSingle(error =>
            error.Code == "resource.not_found" &&
            error.Message == "Resource not found.");
    }

    [Fact]
    public async Task InvokeAsync_WhenTransitionsPhiMissingOrDenied_ReturnsByteIdenticalNotFoundBodiesWithoutTraceId()
    {
        // Arrange
        using var missingServer = CreateServer(_ => throw new ResourceNotFoundException(isPhiResource: true));
        using var deniedServer = CreateServer(_ => throw new ResourceAccessDeniedException("ResourceUnavailable", isPhiResource: true));

        // Act
        var missingResponse = await missingServer.GetTestClient().GetAsync($"/api/transitions/plans/{Guid.NewGuid():D}");
        var deniedResponse = await deniedServer.GetTestClient().GetAsync($"/api/transitions/plans/{Guid.NewGuid():D}");
        var missingBody = await missingResponse.Content.ReadAsByteArrayAsync();
        var deniedBody = await deniedResponse.Content.ReadAsByteArrayAsync();
        var bodyText = System.Text.Encoding.UTF8.GetString(missingBody);

        // Assert
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        deniedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingBody.Should().Equal(deniedBody);
        bodyText.Contains("TraceId", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        bodyText.Contains("ResourceUnavailable", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenValidationFails_DoesNotEchoSubmittedValues()
    {
        // Arrange
        const string attemptedValue = "Latitude 999.123";
        using var server = CreateServer(_ => throw new ValidationException(new[]
        {
            new ValidationFailure("Latitude", $"The value {attemptedValue} is not valid for Latitude.")
            {
                ErrorCode = "gps.invalid",
            },
        }));

        // Act
        var response = await server.GetTestClient().PostAsync("/validate", null);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotContain(attemptedValue);

        var problem = DeserializeProblem(body);
        problem.Title.Should().Be("Validation failed.");
        problem.ValidationErrors.Should().ContainSingle(error =>
            error.PropertyName == "Latitude" &&
            error.ErrorMessage == "The request is invalid." &&
            error.ErrorCode == "gps.invalid");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionOccurs_ReturnsGenericProblemWithoutExceptionText()
    {
        // Arrange
        const string exceptionText = "Sensitive exception text";
        using var server = CreateServer(_ => throw new InvalidOperationException(exceptionText));

        // Act
        var response = await server.GetTestClient().GetAsync("/throws");
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
    public async Task InvokeAsync_WhenResourceConflictOccurs_ReturnsPhiFreeConflictProblem()
    {
        // Arrange
        using var server = CreateServer(_ => throw new ResourceConflictException("invoice.duplicate", "An invoice already exists for the requested billing period."));

        // Act
        var response = await server.GetTestClient().PostAsync("/invoices", null);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.Should().NotContain("Test Client A");

        var problem = JsonSerializer.Deserialize<ProblemDetailsResponse>(body, JsonOptions())
            ?? throw new InvalidOperationException("Problem details response could not be deserialized.");
        problem.Title.Should().Be("Conflict.");
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Detail.Should().BeNull();
        problem.Errors.Should().ContainSingle(error =>
            error.Code == "invoice.duplicate" &&
            error.Message == "An invoice already exists for the requested billing period.");
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

    private static IHost CreateServer(Action<HttpContext> endpoint)
    {
        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseCarePathProblemDetails();
                    app.Run(context =>
                    {
                        context.TraceIdentifier = Guid.NewGuid().ToString("N");
                        endpoint(context);
                        return Task.CompletedTask;
                    });
                });
            })
            .Start();
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
