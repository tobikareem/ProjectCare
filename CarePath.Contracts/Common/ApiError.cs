namespace CarePath.Contracts.Common;

/// <summary>
/// A single machine-readable error returned by the API.
/// </summary>
/// <remarks>
/// PHI SAFETY: <see cref="Code"/> and <see cref="Message"/> must never contain PHI
/// (patient names, dates of birth, diagnoses, addresses, document text, or field values
/// submitted by users). Messages describe the rule that failed, never the data that failed it.
/// </remarks>
/// <param name="Code">Stable, machine-readable error code (e.g., <c>"resource.not_found"</c>).</param>
/// <param name="Message">Human-readable, PHI-free description of the error.</param>
public sealed record ApiError(string Code, string Message);
