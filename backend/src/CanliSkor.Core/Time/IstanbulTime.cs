namespace CanliSkor.Core.Time;

/// <summary>
/// The app's "local" calendar. We store UTC everywhere and only use this to decide
/// what "today" means and to present times to Turkish users.
/// </summary>
public static class IstanbulTime
{
    public static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static DateTimeOffset ToIstanbul(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, TimeZone);

    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(ToIstanbul(timeProvider.GetUtcNow()).DateTime);
}
