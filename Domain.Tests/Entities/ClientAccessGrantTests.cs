using CarePath.Domain.Entities.Identity;
using CarePath.Domain.Enumerations;
using FluentAssertions;

namespace CarePath.Domain.Tests.Entities;

public class ClientAccessGrantTests
{
    [Fact]
    public void AccessScope_DefaultsToPatientFacing()
    {
        // Arrange
        var grant = new ClientAccessGrant();

        // Act
        var scope = grant.AccessScope;

        // Assert
        scope.Should().Be(AccessScope.PatientFacing);
    }

    [Fact]
    public void GrantedAtUtc_DefaultsToUtcTimestamp()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var grant = new ClientAccessGrant();
        var after = DateTime.UtcNow;

        // Assert
        grant.GrantedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        grant.GrantedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void IsRevoked_WhenRevokedAtUtcIsNull_ReturnsFalse()
    {
        // Arrange
        var grant = new ClientAccessGrant { RevokedAtUtc = null };

        // Act
        var isRevoked = grant.IsRevoked;

        // Assert
        isRevoked.Should().BeFalse();
    }

    [Fact]
    public void IsRevoked_WhenRevokedAtUtcHasValue_ReturnsTrue()
    {
        // Arrange
        var grant = new ClientAccessGrant { RevokedAtUtc = DateTime.UtcNow };

        // Act
        var isRevoked = grant.IsRevoked;

        // Assert
        isRevoked.Should().BeTrue();
    }

    [Theory]
    [InlineData(AccessScope.PatientFacing, 1)]
    [InlineData(AccessScope.Full, 2)]
    public void AccessScope_NumericValuesRemainStable(AccessScope scope, int expectedValue)
    {
        // Act
        var numericValue = (int)scope;

        // Assert
        numericValue.Should().Be(expectedValue);
    }
}
