using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarePath.Contracts.Common;

namespace CarePath.Client;

/// <summary>
/// Base class for typed API clients. Wraps <see cref="HttpClient"/>, deserializes success
/// payloads, and maps non-success responses (including <see cref="ApiProblemDetails"/> bodies
/// produced by the WebApi problem-details middleware) into failed <see cref="ApiResponse{T}"/>
/// results — callers never handle raw HTTP or exceptions for expected failures.
/// </summary>
/// <remarks>
/// PHI SAFETY: error mapping surfaces only the PHI-free <c>Title</c>, stable error codes, and
/// <c>TraceId</c> from the server. This class never logs response bodies.
/// IDOR NOTE: per the Sprint 3 denial-mapping contract, the server returns identical 404s for
/// missing and denied PHI resources, so clients receive <c>resource.not_found</c> for both.
/// </remarks>
public abstract class ApiClientBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The configured HTTP client (base address and auth handler supplied by DI).</summary>
    protected HttpClient Http { get; }

    /// <summary>
    /// Creates the client.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with the API base address.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="httpClient"/> is null.</exception>
    protected ApiClientBase(HttpClient httpClient) =>
        Http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <summary>Sends a GET and maps the response.</summary>
    /// <typeparam name="T">Expected payload type.</typeparam>
    /// <param name="requestUri">Relative request URI. Must never embed PHI values.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mapped response envelope.</returns>
    protected async Task<ApiResponse<T>> GetAsync<T>(
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        return await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a POST with a JSON body and maps the response payload.</summary>
    /// <typeparam name="TRequest">Request body type.</typeparam>
    /// <typeparam name="TResponse">Expected payload type.</typeparam>
    /// <param name="requestUri">Relative request URI. Must never embed PHI values.</param>
    /// <param name="payload">Request body.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mapped response envelope.</returns>
    protected async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
        string requestUri,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http
            .PostAsJsonAsync(requestUri, payload, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a POST with a JSON body for operations that return no payload.</summary>
    /// <typeparam name="TRequest">Request body type.</typeparam>
    /// <param name="requestUri">Relative request URI. Must never embed PHI values.</param>
    /// <param name="payload">Request body.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mapped response envelope.</returns>
    protected async Task<ApiResponse> PostAsync<TRequest>(
        string requestUri,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http
            .PostAsJsonAsync(requestUri, payload, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new ApiResponse { Success = true };
        }

        var (errors, traceId) = await MapErrorsAsync(response, cancellationToken).ConfigureAwait(false);
        return new ApiResponse { Success = false, Errors = errors, TraceId = traceId };
    }

    /// <summary>Sends a body-less POST for trigger-style operations (e.g., starting extraction).</summary>
    /// <param name="requestUri">Relative request URI. Must never embed PHI values.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mapped response envelope.</returns>
    protected async Task<ApiResponse> PostAsync(
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http
            .PostAsync(requestUri, content: null, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new ApiResponse { Success = true };
        }

        var (errors, traceId) = await MapErrorsAsync(response, cancellationToken).ConfigureAwait(false);
        return new ApiResponse { Success = false, Errors = errors, TraceId = traceId };
    }

    /// <summary>Sends a DELETE for operations that return no payload (e.g., soft revoke).</summary>
    /// <param name="requestUri">Relative request URI. Must never embed PHI values.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mapped response envelope.</returns>
    protected async Task<ApiResponse> DeleteAsync(
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.DeleteAsync(requestUri, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new ApiResponse { Success = true };
        }

        var (errors, traceId) = await MapErrorsAsync(response, cancellationToken).ConfigureAwait(false);
        return new ApiResponse { Success = false, Errors = errors, TraceId = traceId };
    }

    /// <summary>Sends multipart form data (file uploads) and maps the response payload.</summary>
    /// <typeparam name="TResponse">Expected payload type.</typeparam>
    /// <param name="requestUri">Relative request URI. Must never embed PHI values.</param>
    /// <param name="content">The multipart content. Caller retains ownership of streams.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mapped response envelope.</returns>
    protected async Task<ApiResponse<TResponse>> PostMultipartAsync<TResponse>(
        string requestUri,
        MultipartFormDataContent content,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);
        return await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a PUT with a JSON body and maps the response payload.</summary>
    /// <typeparam name="TRequest">Request body type.</typeparam>
    /// <typeparam name="TResponse">Expected payload type.</typeparam>
    /// <param name="requestUri">Relative request URI. Must never embed PHI values.</param>
    /// <param name="payload">Request body.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mapped response envelope.</returns>
    protected async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(
        string requestUri,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http
            .PutAsJsonAsync(requestUri, payload, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return await ReadAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ApiResponse<T>> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content
                .ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return data is null
                ? new ApiResponse<T>
                {
                    Success = false,
                    Errors = [new ApiError("response.empty", "The server returned an empty response.")]
                }
                : new ApiResponse<T> { Success = true, Data = data };
        }

        var (errors, traceId) = await MapErrorsAsync(response, cancellationToken).ConfigureAwait(false);
        return new ApiResponse<T> { Success = false, Errors = errors, TraceId = traceId };
    }

    private static async Task<(IReadOnlyList<ApiError> Errors, string? TraceId)> MapErrorsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ApiProblemDetails? problem = null;

        try
        {
            problem = await response.Content
                .ReadFromJsonAsync<ApiProblemDetails>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Non-JSON error body (proxy, gateway); fall through to the status-code mapping.
        }

        if (problem is { ValidationErrors.Count: > 0 })
        {
            var validationErrors = problem.ValidationErrors
                .Select(v => new ApiError(
                    v.ErrorCode ?? "validation.failed",
                    $"{v.PropertyName}: {v.ErrorMessage}"))
                .ToArray();

            return (validationErrors, problem.TraceId);
        }

        var code = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "request.invalid",
            HttpStatusCode.Unauthorized => "auth.unauthenticated",
            HttpStatusCode.Forbidden => "auth.forbidden",
            HttpStatusCode.NotFound => "resource.not_found",
            HttpStatusCode.Conflict => "resource.conflict",
            _ => "server.error"
        };

        var message = string.IsNullOrWhiteSpace(problem?.Title)
            ? "The request could not be completed."
            : problem.Title;

        return ([new ApiError(code, message)], problem?.TraceId);
    }
}
