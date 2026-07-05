namespace CarePath.Contracts.Common;

/// <summary>
/// A single field-level validation failure produced by FluentValidation at the Application boundary.
/// </summary>
/// <remarks>
/// PHI SAFETY: this contract deliberately has no <c>AttemptedValue</c> member. Echoing submitted
/// values back to clients (or into logs) can leak PHI. Only the property name, a PHI-free message,
/// and a stable error code are transported.
/// </remarks>
/// <param name="PropertyName">Name of the request property that failed validation.</param>
/// <param name="ErrorMessage">Human-readable, PHI-free description of the validation rule that failed.</param>
/// <param name="ErrorCode">Stable, machine-readable validation code (e.g., <c>"shift.end_before_start"</c>). Optional.</param>
public sealed record ValidationError(string PropertyName, string ErrorMessage, string? ErrorCode = null);
