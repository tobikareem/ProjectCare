using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CarePath.Infrastructure.Persistence.Converters;

/// <summary>
/// Converts <see cref="DateTime"/> values to UTC for storage and restores
/// <see cref="DateTimeKind.Utc"/> when values are read from SQL Server.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    /// <summary>Initializes a new UTC DateTime converter.</summary>
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
