using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CarePath.Infrastructure.Persistence.Converters;

/// <summary>
/// Converts nullable <see cref="DateTime"/> values to UTC for storage and restores
/// <see cref="DateTimeKind.Utc"/> when values are read from SQL Server.
/// </summary>
public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    /// <summary>Initializes a new nullable UTC DateTime converter.</summary>
    public NullableUtcDateTimeConverter()
        : base(
            value => value.HasValue
                ? value.Value.Kind == DateTimeKind.Local
                    ? value.Value.ToUniversalTime()
                    : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : null,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null)
    {
    }
}
