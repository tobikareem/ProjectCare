namespace CarePath.Contracts.Common;

/// <summary>
/// Standard non-generic API response envelope for operations that return no data payload.
/// </summary>
/// <remarks>
/// PHI SAFETY: <see cref="Message"/> and <see cref="Errors"/> must never contain PHI.
/// IDOR SAFETY: for PHI resources, authorization failures are surfaced with not-found semantics
/// so responses never reveal whether a guessed identifier exists.
/// </remarks>
public class ApiResponse
{
    /// <summary>True when the operation completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Optional human-readable, PHI-free summary of the outcome.</summary>
    public string? Message { get; init; }

    /// <summary>Errors describing why the operation failed. Empty on success.</summary>
    public IReadOnlyList<ApiError> Errors { get; init; } = [];

    /// <summary>Correlation/trace identifier for support and audit correlation. Never contains PHI.</summary>
    public string? TraceId { get; init; }

    /// <summary>Creates a successful response.</summary>
    /// <param name="message">Optional PHI-free summary message.</param>
    /// <returns>A response with <see cref="Success"/> set to <c>true</c>.</returns>
    public static ApiResponse Ok(string? message = null) =>
        new() { Success = true, Message = message };

    /// <summary>Creates a failed response.</summary>
    /// <param name="errors">One or more PHI-free errors.</param>
    /// <returns>A response with <see cref="Success"/> set to <c>false</c>.</returns>
    public static ApiResponse Fail(params ApiError[] errors) =>
        new() { Success = false, Errors = errors };
}

/// <summary>
/// Standard generic API response envelope carrying a data payload.
/// </summary>
/// <typeparam name="T">Client-safe DTO type carried by the response. Never a Domain entity.</typeparam>
public class ApiResponse<T> : ApiResponse
{
    /// <summary>The payload. <c>null</c> (or default) when <see cref="ApiResponse.Success"/> is <c>false</c>.</summary>
    public T? Data { get; init; }

    /// <summary>Creates a successful response carrying <paramref name="data"/>.</summary>
    /// <param name="data">The client-safe payload.</param>
    /// <param name="message">Optional PHI-free summary message.</param>
    /// <returns>A response with <see cref="ApiResponse.Success"/> set to <c>true</c>.</returns>
    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    /// <summary>Creates a failed response with no payload.</summary>
    /// <param name="errors">One or more PHI-free errors.</param>
    /// <returns>A response with <see cref="ApiResponse.Success"/> set to <c>false</c>.</returns>
    public static new ApiResponse<T> Fail(params ApiError[] errors) =>
        new() { Success = false, Errors = errors };
}
