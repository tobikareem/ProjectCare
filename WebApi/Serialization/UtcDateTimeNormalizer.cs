namespace CarePath.WebApi.Serialization;

/// <summary>
/// Normalizes <see cref="DateTime"/> values arriving at the API boundary to
/// <see cref="DateTimeKind.Utc"/>, matching the persistence-layer convention
/// (Infrastructure's UtcDateTimeConverter): values with an offset are converted to the
/// equivalent UTC instant; values without one are assumed to already be UTC.
/// </summary>
public static class UtcDateTimeNormalizer
{
    /// <summary>Returns the value as a <see cref="DateTimeKind.Utc"/> date/time.</summary>
    /// <param name="value">Value to normalize.</param>
    public static DateTime Normalize(DateTime value)
    {
        return value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
