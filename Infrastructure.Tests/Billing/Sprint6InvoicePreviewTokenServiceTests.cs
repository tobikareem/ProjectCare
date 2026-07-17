using CarePath.Application.Abstractions.Billing;
using CarePath.Infrastructure.Billing;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;

namespace CarePath.Infrastructure.Tests.Billing;

/// <summary>
/// Direct tests for the cryptographic preview token implementation (D-S6-18): round-trip
/// fidelity, opaque failure on garbage/tampered input, and cross-provider rejection.
/// </summary>
public sealed class Sprint6InvoicePreviewTokenServiceTests
{
    [Fact]
    public void ProtectThenUnprotect_RoundTripsTheFullFingerprint()
    {
        var service = new InvoicePreviewTokenService(new EphemeralDataProtectionProvider());
        var fingerprint = Fingerprint();

        var token = service.Protect(fingerprint, out var expiresAtUtc);
        var roundTripped = service.Unprotect(token);

        token.Should().NotBeNullOrWhiteSpace();
        token.Should().NotContain(fingerprint.ClientId.ToString("N"), "token contents must be opaque");
        expiresAtUtc.Should().BeAfter(DateTime.UtcNow);
        roundTripped.Should().NotBeNull();
        roundTripped!.ClientId.Should().Be(fingerprint.ClientId);
        roundTripped.ServiceType.Should().Be(fingerprint.ServiceType);
        roundTripped.PeriodStartUtc.Should().Be(fingerprint.PeriodStartUtc);
        roundTripped.PeriodEndUtc.Should().Be(fingerprint.PeriodEndUtc);
        roundTripped.EligibleShiftIds.Should().Equal(fingerprint.EligibleShiftIds);
        roundTripped.InputsHash.Should().Be(fingerprint.InputsHash);
        roundTripped.Subtotal.Should().Be(fingerprint.Subtotal);
        roundTripped.TotalBillableHours.Should().Be(fingerprint.TotalBillableHours);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-protected-payload")]
    public void Unprotect_ReturnsNullForMissingOrGarbageTokens(string? token)
    {
        var service = new InvoicePreviewTokenService(new EphemeralDataProtectionProvider());

        service.Unprotect(token!).Should().BeNull();
    }

    [Fact]
    public void Unprotect_ReturnsNullForTamperedTokens()
    {
        var service = new InvoicePreviewTokenService(new EphemeralDataProtectionProvider());
        var token = service.Protect(Fingerprint(), out _);
        var tampered = token[..^4] + "AAAA";

        service.Unprotect(tampered).Should().BeNull();
    }

    [Fact]
    public void Unprotect_ReturnsNullForTokensFromADifferentKeyRing()
    {
        var issuer = new InvoicePreviewTokenService(new EphemeralDataProtectionProvider());
        var verifier = new InvoicePreviewTokenService(new EphemeralDataProtectionProvider());
        var token = issuer.Protect(Fingerprint(), out _);

        verifier.Unprotect(token).Should().BeNull("a foreign key ring must not be able to mint or read tokens");
    }

    private static InvoicePreviewFingerprint Fingerprint() => new(
        Guid.NewGuid(),
        1,
        new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
        new[] { Guid.NewGuid(), Guid.NewGuid() },
        "ABCDEF0123456789",
        1234.56m,
        31.25m);
}
