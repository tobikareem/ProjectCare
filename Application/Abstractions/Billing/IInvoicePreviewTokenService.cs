namespace CarePath.Application.Abstractions.Billing;

/// <summary>
/// The selection and totals a preview token is bound to (D-S6-18). Serialized inside the
/// opaque token payload — never exposed to clients in readable form.
/// </summary>
/// <param name="ClientId">Previewed client.</param>
/// <param name="ServiceType">Previewed service line (domain numeric value).</param>
/// <param name="PeriodStartUtc">Period start (UTC, inclusive).</param>
/// <param name="PeriodEndUtc">Period end (UTC, exclusive).</param>
/// <param name="EligibleShiftIds">Sorted eligible shift IDs at preview time.</param>
/// <param name="InputsHash">Fingerprint of billable inputs (<see cref="BillingMath.ComputeInputsHash"/>).</param>
/// <param name="Subtotal">Previewed subtotal (sum of rounded lines).</param>
/// <param name="TotalBillableHours">Previewed total billable hours.</param>
public sealed record InvoicePreviewFingerprint(
    Guid ClientId,
    int ServiceType,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    IReadOnlyList<Guid> EligibleShiftIds,
    string InputsHash,
    decimal Subtotal,
    decimal TotalBillableHours);

/// <summary>
/// Opaque, expiring, tamper-protected preview token service (D-S6-18). Tokens are
/// time-limited and authenticated; any expiry, tampering, or payload mismatch surfaces only
/// as the sanitized <c>invoice.preview_stale</c> conflict.
/// </summary>
public interface IInvoicePreviewTokenService
{
    /// <summary>Token lifetime from issuance.</summary>
    TimeSpan Lifetime { get; }

    /// <summary>Protects a fingerprint into an opaque token and reports its UTC expiry.</summary>
    string Protect(InvoicePreviewFingerprint fingerprint, out DateTime expiresAtUtc);

    /// <summary>
    /// Unprotects a token. Returns null for missing, malformed, tampered, or expired tokens —
    /// callers must map null to the sanitized stale-preview conflict without detail.
    /// </summary>
    InvoicePreviewFingerprint? Unprotect(string token);
}
