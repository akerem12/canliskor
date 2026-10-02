namespace CanliSkor.Core.Time;

/// <summary>
/// The app's "local" calendar. We store UTC everywhere and only use this to decide
/// what "today" means and to present times to Turkish users.
/// </summary>
public static class IstanbulTime
{
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static DateTimeOffset ToIstanbul(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, TimeZone);

    /// <summary>The Istanbul calendar date an instant falls on.</summary>
    public static DateOnly DateOf(DateTimeOffset value) => DateOnly.FromDateTime(ToIstanbul(value).DateTime);

    public static DateOnly Today(TimeProvider timeProvider) => DateOf(timeProvider.GetUtcNow());
}
