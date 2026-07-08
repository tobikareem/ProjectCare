using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarePath.WebApi.Serialization;

/// <summary>
/// JSON converter that reads every <see cref="DateTime"/> as UTC — ISO 8601 values without an
/// offset are assumed UTC, values with an offset are converted — and writes every value with a
/// Zulu suffix. Application-layer validators require <see cref="DateTimeKind.Utc"/>, so this
/// keeps clients that send offset-less dates (date pickers, plain JSON bodies) valid.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!reader.TryGetDateTime(out var value))
        {
            throw new JsonException("The JSON value is not a valid ISO 8601 date/time.");
        }

        return UtcDateTimeNormalizer.Normalize(value);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(UtcDateTimeNormalizer.Normalize(value));
    }
}
