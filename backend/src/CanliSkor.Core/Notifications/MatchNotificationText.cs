using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// The words of the notifications, in the subscriber's language. They are written here and not in the browser
/// because they are shown while the site is closed. Team names are the provider's (English) spelling.
/// </summary>
public static class MatchNotificationText
{
    public static PushNotification KickoffReminder(Match match, TimeSpan untilKickoff, string language)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(untilKickoff.TotalMinutes));
        var (title, body) = language == "tr"
            ? ($"Maç hatırlatması: {Teams(match, " - ")}",
                $"Maç {minutes} dakika sonra başlıyor! Maç öncesi istatistikler ve oranlar için dokunun.")
            : ($"Match Reminder: {Teams(match, " vs ")}",
                $"Kickoff in {minutes} {(minutes == 1 ? "minute" : "minutes")}! Tap to view pre-match stats and odds.");

        return new PushNotification(title, body, MatchUrl(match), $"{match.Id}:reminder", untilKickoff);
    }

    public static PushNotification LineupsAnnounced(Match match, TimeSpan untilKickoff, string language)
    {
        var (title, body) = language == "tr"
            ? ($"Kadrolar açıklandı: {Teams(match, " - ")}", "İlk 11'ler belli oldu! Dizilişlere ve öne çıkan oyunculara göz atın.")
            : ($"Lineups Confirmed: {Teams(match, " vs ")}", "Official starting XIs are out! Check out the formations and key players.");

        return new PushNotification(title, body, $"{MatchUrl(match)}&tab=lineups", $"{match.Id}:lineups", untilKickoff);
    }

    private static string Teams(Match match, string separator) => $"{match.HomeTeam.ShortName}{separator}{match.AwayTeam.ShortName}";

    // The same address the site itself gives a match page (frontend/src/route.ts).
    private static string MatchUrl(Match match) =>
        $"/?league={Uri.EscapeDataString(match.LeagueCode)}&match={Uri.EscapeDataString(match.Id)}";
}
