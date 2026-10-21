using CarePath.Application.Platform;
using FluentAssertions;

namespace CarePath.Application.Tests.Platform;

public sealed class HostNameNormalizerTests
{
    [Theory]
    [InlineData("BrightCare.CarePathHealth.com", "brightcare.carepathhealth.com")]
    [InlineData("brightcare.carepathhealth.com:443", "brightcare.carepathhealth.com")]
    [InlineData("brightcare.carepathhealth.com.", "brightcare.carepathhealth.com")]
    [InlineData("  LOCALHOST:5240 ", "localhost")]
    [InlineData("carepath.localhost", "carepath.localhost")]
    public void Normalize_WhenHostIsValid_LowercasesAndStripsPortAndTrailingDot(string input, string expected)
    {
        // Act
        var normalized = HostNameNormalizer.Normalize(input);

        // Assert
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(":443")]
    [InlineData("bright..care.test")]
    [InlineData("-brightcare.test")]
    [InlineData("brightcare-.test")]
    [InlineData("bright_care.test")]
    [InlineData("brightcäre.test")]
    [InlineData("[::1]:5240")]
    public void Normalize_WhenHostIsMalformed_ReturnsNull(string? input)
    {
        // Act
        var normalized = HostNameNormalizer.Normalize(input);

        // Assert
        normalized.Should().BeNull();
    }

    [Fact]
    public void Normalize_WhenHostExceedsMaxLength_ReturnsNull()
    {
        // Arrange
        var label = new string('a', 63);
        var tooLong = string.Join('.', Enumerable.Repeat(label, 5));

        // Act
        var normalized = HostNameNormalizer.Normalize(tooLong);

        // Assert
        tooLong.Length.Should().BeGreaterThan(HostNameNormalizer.MaxLength);
        normalized.Should().BeNull();
    }

    [Fact]
    public void Normalize_WhenLabelExceeds63Characters_ReturnsNull()
    {
        // Arrange
        var host = new string('a', 64) + ".test";

        // Act / Assert
        HostNameNormalizer.Normalize(host).Should().BeNull();
    }
}
