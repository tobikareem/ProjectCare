using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class CaregiverCertificationTests
{
    [Fact]
    public void IsExpired_ReturnsFalse_WhenExpirationDateIsInFuture()
    {
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CNA,
            ExpirationDate = DateTime.UtcNow.AddDays(60)
        };
        cert.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpirationDateIsInPast()
    {
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CNA,
            ExpirationDate = DateTime.UtcNow.AddDays(-1)
        };
        cert.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenExpirationDateIsYesterday()
    {
        // Yesterday at midnight is unambiguously before UtcNow regardless of time-of-day
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CNA,
            ExpirationDate = DateTime.UtcNow.Date.AddDays(-1)
        };
        cert.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpiringSoon_ReturnsFalse_WhenCertIsAlreadyExpired()
    {
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CNA,
            ExpirationDate = DateTime.UtcNow.AddDays(-1)
        };
        cert.IsExpiringSoon.Should().BeFalse();
    }

    [Fact]
    public void IsExpiringSoon_ReturnsFalse_WhenExpirationIsMoreThan30DaysAway()
    {
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CNA,
            ExpirationDate = DateTime.UtcNow.AddDays(60)
        };
        cert.IsExpiringSoon.Should().BeFalse();
    }

    [Fact]
    public void IsExpiringSoon_ReturnsTrue_WhenExpirationIsWithin30Days()
    {
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CNA,
            ExpirationDate = DateTime.UtcNow.AddDays(15)
        };
        cert.IsExpiringSoon.Should().BeTrue();
    }

    [Fact]
    public void IsExpiringSoon_ReturnsTrue_WhenExpirationIsIn1Day()
    {
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CNA,
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };
        cert.IsExpiringSoon.Should().BeTrue();
    }

    [Fact]
    public void IsExpiringSoon_ReturnsFalse_WhenExpirationIsMoreThan30DaysAway_Boundary()
    {
        // Use 31 days rather than exactly 30 to avoid timing races between the
        // two separate DateTime.UtcNow calls (one setting ExpirationDate, one
        // inside the property). An exact 30-day boundary test is non-deterministic.
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CNA,
            ExpirationDate = DateTime.UtcNow.AddDays(31)
        };
        cert.IsExpiringSoon.Should().BeFalse();
    }

    [Fact]
    public void CertificationNumber_IsNullable()
    {
        var cert = new CaregiverCertification
        {
            Type = CertificationType.CPR,
            CertificationNumber = null
        };
        cert.CertificationNumber.Should().BeNull();
    }

    [Fact]
    public void IssuingAuthority_IsNullable()
    {
        var cert = new CaregiverCertification
        {
            Type = CertificationType.Dementia,
            IssuingAuthority = null
        };
        cert.IssuingAuthority.Should().BeNull();
    }
}
