namespace CarePath.Contracts.Common;

/// <summary>
/// RFC 7807 problem-details contract used for non-success HTTP responses, including
/// validation failures. Defined here as a dependency-free POCO so Blazor WebAssembly and
/// MAUI clients can deserialize it without referencing ASP.NET Core.
/// </summary>
/// <remarks>
/// PHI SAFETY: <see cref="Title"/>, <see cref="Detail"/>, <see cref="Instance"/>, and all
/// validation errors must be PHI-free. <see cref="Instance"/> must never embed patient
/// identifiers in a form that leaks PHI (route templates only, per the no-PHI-in-URLs rule).
/// </remarks>
public class ApiProblemDetails
{
    /// <summary>URI reference identifying the problem type. Defaults to <c>about:blank</c>.</summary>
    public string Type { get; init; } = "about:blank";

    /// <summary>Short, PHI-free summary of the problem type.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>HTTP status code for this occurrence of the problem.</summary>
    public int Status { get; init; }

    /// <summary>Optional PHI-free explanation specific to this occurrence.</summary>
    public string? Detail { get; init; }

    /// <summary>Optional URI reference for this occurrence. Must not leak PHI.</summary>
    public string? Instance { get; init; }

    /// <summary>Correlation/trace identifier for support and audit correlation.</summary>
    public string? TraceId { get; init; }

    /// <summary>Field-level validation failures. Empty when the problem is not a validation failure.</summary>
    public IReadOnlyList<ValidationError> ValidationErrors { get; init; } = [];
}
