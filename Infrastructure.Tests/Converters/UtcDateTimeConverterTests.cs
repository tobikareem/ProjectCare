using CarePath.Infrastructure.Persistence.Converters;
using FluentAssertions;

namespace CarePath.Infrastructure.Tests.Converters;

public class UtcDateTimeConverterTests
{
    [Fact]
    public void ConvertFromProvider_WhenDateTimeIsRead_RestoresUtcKind()
    {
        // Arrange
        var converter = new UtcDateTimeConverter();
        var databaseValue = new DateTime(2026, 6, 27, 10, 30, 0, DateTimeKind.Unspecified);

        // Act
        var result = (DateTime)converter.ConvertFromProvider(databaseValue)!;

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(DateTime.SpecifyKind(databaseValue, DateTimeKind.Utc));
    }

    [Fact]
    public void ConvertToProvider_WhenDateTimeIsLocal_StoresUtcValue()
    {
        // Arrange
        var converter = new UtcDateTimeConverter();
        var localValue = new DateTime(2026, 6, 27, 10, 30, 0, DateTimeKind.Local);

        // Act
        var result = (DateTime)converter.ConvertToProvider(localValue)!;

        // Assert
        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(localValue.ToUniversalTime());
    }

    [Fact]
    public void ConvertToProvider_WhenDateTimeIsAlreadyUtc_ValueIsUnchanged()
    {
        // Arrange
        var converter = new UtcDateTimeConverter();
        var utcValue = new DateTime(2026, 6, 27, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var result = (DateTime)converter.ConvertToProvider(utcValue)!;

        // Assert
        result.Should().Be(utcValue);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ConvertToProvider_WhenDateTimeKindIsUnspecified_TreatsAsUtcWithoutShifting()
    {
        // Arrange
        var converter = new UtcDateTimeConverter();
        var unspecifiedValue = new DateTime(2026, 6, 27, 10, 30, 0, DateTimeKind.Unspecified);

        // Act
        var result = (DateTime)converter.ConvertToProvider(unspecifiedValue)!;

        // Assert - assumed UTC, so the clock value must not be offset-shifted
        result.Should().Be(DateTime.SpecifyKind(unspecifiedValue, DateTimeKind.Utc));
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void NullableConvertToProvider_WhenValueIsUnspecified_TreatsAsUtcWithoutShifting()
    {
        // Arrange
        var converter = new NullableUtcDateTimeConverter();
        DateTime? unspecifiedValue = new DateTime(2026, 6, 27, 10, 30, 0, DateTimeKind.Unspecified);

        // Act
        var result = (DateTime?)converter.ConvertToProvider(unspecifiedValue);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be(DateTime.SpecifyKind(unspecifiedValue.Value, DateTimeKind.Utc));
        result.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void NullableConvertFromProvider_WhenValueIsNull_ReturnsNull()
    {
        // Arrange
        var converter = new NullableUtcDateTimeConverter();

        // Act
        var result = converter.ConvertFromProvider(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NullableConvertFromProvider_WhenValueIsNonNull_RestoresUtcKind()
    {
        // Arrange
        var converter = new NullableUtcDateTimeConverter();
        var databaseValue = new DateTime(2026, 6, 27, 10, 30, 0, DateTimeKind.Unspecified);

        // Act
        var result = (DateTime?)converter.ConvertFromProvider(databaseValue);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Kind.Should().Be(DateTimeKind.Utc);
        result.Value.Should().Be(DateTime.SpecifyKind(databaseValue, DateTimeKind.Utc));
    }
}
