namespace Bielu.BetterSearch.Abstractions.Query.SubQueries;

/// <summary>
/// A bound of a <see cref="DateRangeQuery"/>: one instant, the same on every provider and on every machine. A
/// <see cref="DateTimeOffset"/> keeps its instant, a <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/> or
/// <see cref="DateTimeKind.Local"/> is converted to UTC, and one with <see cref="DateTimeKind.Unspecified"/> is read
/// as UTC, never as the machine's local time.
/// </summary>
public readonly record struct DateRangeValue
{
    private DateRangeValue(DateTimeOffset instant) => Instant = instant.ToUniversalTime();

    /// <summary>The instant, with an offset of zero.</summary>
    public DateTimeOffset Instant { get; }

    /// <summary>The instant as a <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/>.</summary>
    public DateTime UtcDateTime => Instant.UtcDateTime;

    public static implicit operator DateRangeValue(DateTimeOffset value) => new(value);

    public static implicit operator DateRangeValue(DateTime value) => new(value.Kind == DateTimeKind.Unspecified
        ? new DateTimeOffset(value, TimeSpan.Zero)
        : new DateTimeOffset(value.ToUniversalTime()));
}
