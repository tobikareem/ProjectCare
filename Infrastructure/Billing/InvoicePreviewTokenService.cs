using System.Security.Cryptography;
using System.Text.Json;
using CarePath.Application.Abstractions.Billing;
using Microsoft.AspNetCore.DataProtection;

namespace CarePath.Infrastructure.Billing;

/// <summary>
/// Opaque preview token implementation (D-S6-18) built on ASP.NET Core time-limited data
/// protection: authenticated, encrypted, and expiring. Token contents are never inspectable
/// client-side, and every failure mode (missing, malformed, tampered, expired) collapses to
/// null so callers surface only the sanitized stale-preview conflict.
/// </summary>
public sealed class InvoicePreviewTokenService : IInvoicePreviewTokenService
{
    private const string Purpose = "CarePath.Billing.InvoicePreview.v1";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITimeLimitedDataProtector protector;

    /// <summary>Creates the service over the application data-protection provider.</summary>
    /// <param name="provider">Data protection provider.</param>
    public InvoicePreviewTokenService(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    /// <inheritdoc />
    public TimeSpan Lifetime => TokenLifetime;

    /// <inheritdoc />
    public string Protect(InvoicePreviewFingerprint fingerprint, out DateTime expiresAtUtc)
    {
        expiresAtUtc = DateTime.UtcNow.Add(TokenLifetime);
        var payload = JsonSerializer.Serialize(fingerprint, JsonOptions);
        return protector.Protect(payload, TokenLifetime);
    }

    /// <inheritdoc />
    public InvoicePreviewFingerprint? Unprotect(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var payload = protector.Unprotect(token);
            return JsonSerializer.Deserialize<InvoicePreviewFingerprint>(payload, JsonOptions);
        }
        catch (CryptographicException)
        {
            // Tampered, expired, or foreign token — indistinguishable by design.
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
