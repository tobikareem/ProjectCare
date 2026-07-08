using System.Text.Json;
using CarePath.WebApi.Serialization;
using FluentAssertions;

namespace CarePath.Application.Tests.WebApi;

public sealed class UtcDateTimeJsonConverterTests
{
    private sealed class DatePayload
    {
        public DateTime Timestamp { get; init; }

        public DateTime? OptionalTimestamp { get; init; }
    }

    [Fact]
    public void Read_WhenValueHasNoOffset_AssumesUtcWithoutShiftingWallClock()
    {
        var payload = Deserialize("""{"timestamp":"2026-01-15T00:00:00"}""");

        payload.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        payload.Timestamp.Should().Be(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Read_WhenValueHasZuluSuffix_KeepsUtcInstant()
    {
        var payload = Deserialize("""{"timestamp":"2026-01-15T10:30:00Z"}""");

        payload.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        payload.Timestamp.Should().Be(new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Read_WhenValueHasOffset_ConvertsInstantToUtc()
    {
        var payload = Deserialize("""{"timestamp":"2026-01-15T10:00:00+02:00"}""");

        payload.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
        payload.Timestamp.Should().Be(new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Read_WhenNullableValueIsNull_ReturnsNull()
    {
        var payload = Deserialize("""{"timestamp":"2026-01-15T00:00:00","optionalTimestamp":null}""");

        payload.OptionalTimestamp.Should().BeNull();
    }

    [Fact]
    public void Read_WhenNullableValueHasNoOffset_AssumesUtc()
    {
        var payload = Deserialize("""{"timestamp":"2026-01-15T00:00:00","optionalTimestamp":"2027-01-15T00:00:00"}""");

        payload.OptionalTimestamp.Should().Be(new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        payload.OptionalTimestamp!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Read_WhenValueIsNotADate_ThrowsJsonException()
    {
        var act = () => Deserialize("""{"timestamp":"not-a-date"}""");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Write_WhenKindIsUnspecified_SerializesWithZuluSuffix()
    {
        var payload = new DatePayload
        {
            Timestamp = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified),
        };

        var json = JsonSerializer.Serialize(payload, CreateOptions());

        json.Should().Contain("\"2026-01-15T10:00:00Z\"");
    }

    [Fact]
    public void Write_WhenKindIsLocal_RoundTripsToSameUtcInstant()
    {
        var utcInstant = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var payload = new DatePayload { Timestamp = utcInstant.ToLocalTime() };

        var json = JsonSerializer.Serialize(payload, CreateOptions());
        var roundTripped = JsonSerializer.Deserialize<DatePayload>(json, CreateOptions())!;

        roundTripped.Timestamp.Should().Be(utcInstant);
        roundTripped.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Normalize_WhenKindIsUnspecified_AssumesUtc()
    {
        var normalized = UtcDateTimeNormalizer.Normalize(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Unspecified));

        normalized.Kind.Should().Be(DateTimeKind.Utc);
        normalized.Should().Be(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Normalize_WhenKindIsLocal_ConvertsToUtcInstant()
    {
        var utcInstant = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var normalized = UtcDateTimeNormalizer.Normalize(utcInstant.ToLocalTime());

        normalized.Kind.Should().Be(DateTimeKind.Utc);
        normalized.Should().Be(utcInstant);
    }

    [Fact]
    public void Normalize_WhenKindIsUtc_ReturnsValueUnchanged()
    {
        var utcInstant = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var normalized = UtcDateTimeNormalizer.Normalize(utcInstant);

        normalized.Should().Be(utcInstant);
        normalized.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static DatePayload Deserialize(string json)
    {
        return JsonSerializer.Deserialize<DatePayload>(json, CreateOptions())!;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new UtcDateTimeJsonConverter());
        return options;
    }
}
