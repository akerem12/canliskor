using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Notifications;

/// <summary>
/// The words of the notifications, in the subscriber's language: short, the score in the title, the minute and
/// the player in the body. They are written here and not in the browser because they are shown while the site is
/// closed. Team names are the provider's (English) spelling.
/// </summary>
public static class MatchNotificationText
{
    /// <summary>A goal or a card is old news after this; a phone that was off that long isn't sent it any more.</summary>
    public static readonly TimeSpan LiveTimeToLive = TimeSpan.FromMinutes(15);

    /// <param name="icon">The crest to show: the subscriber's own team's, if it plays.</param>
    public static PushNotification KickoffReminder(Match match, TimeSpan untilKickoff, string language, string? icon)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(untilKickoff.TotalMinutes));
        var title = language == "tr"
            ? $"⏰ Maça {minutes} Dakika Kaldı"
            : $"⏰ Kick-off in {minutes} {(minutes == 1 ? "minute" : "minutes")}";

        return new PushNotification(title, Teams(match), MatchUrl(match), $"{match.Id}:reminder", untilKickoff, icon);
    }

    public static PushNotification LineupsAnnounced(Match match, TimeSpan untilKickoff, string language, string? icon)
    {
        var title = language == "tr" ? "📋 İlk 11'ler Açıklandı" : "📋 Line-ups are out";

        return new PushNotification(title, Teams(match), $"{MatchUrl(match)}&tab=lineups", $"{match.Id}:lineups", untilKickoff, icon);
    }

    public static PushNotification Kickoff(Match match, string language, string? icon)
    {
        var (title, body) = language == "tr"
            ? ($"Maç Başladı: {Scoreline(match, new Score(0, 0))}", "Karşılaşmada ilk düdük çaldı.")
            : ($"Kick-off: {Scoreline(match, new Score(0, 0))}", "The match is under way.");

        return new PushNotification(title, body, MatchUrl(match), $"{match.Id}:kickoff", LiveTimeToLive, icon);
    }

    public static PushNotification HalfTime(Match match, string language, string? icon)
    {
        var score = Scoreline(match, match.Score ?? new Score(0, 0));
        var (title, body) = language == "tr"
            ? ($"Devre Arası: {score}", "İlk yarı sona erdi.")
            : ($"Half-time: {score}", "The first half is over.");

        return new PushNotification(title, body, MatchUrl(match), $"{match.Id}:half-time", LiveTimeToLive, icon);
    }

    public static PushNotification SecondHalf(Match match, string language, string? icon)
    {
        var score = Scoreline(match, match.Score ?? new Score(0, 0));
        var (title, body) = language == "tr"
            ? ($"İkinci Yarı Başladı: {score}", "Karşılaşmada ikinci yarı başladı.")
            : ($"Second half: {score}", "The second half is under way.");

        return new PushNotification(title, body, MatchUrl(match), $"{match.Id}:second-half", LiveTimeToLive, icon);
    }

    public static PushNotification FullTime(Match match, string language, string? icon)
    {
        var score = Scoreline(match, match.Score ?? new Score(0, 0));
        var (title, body) = language == "tr"
            ? ($"Maç Sonu: {score}", "Karşılaşma sona erdi.")
            : ($"Full-time: {score}", "The match is over.");

        return new PushNotification(title, body, MatchUrl(match), $"{match.Id}:full-time", LiveTimeToLive, icon);
    }

    /// <summary>A goal, a penalty given or missed, or a red card, with the crest of the team it is about.</summary>
    /// <param name="score">The score right after the event.</param>
    /// <param name="key">Tells the event apart from the others of the match (see <see cref="MatchNotifier"/>).</param>
    /// <returns>Null for events that aren't notified (yellow cards, substitutions).</returns>
    public static PushNotification? Event(Match match, MatchEvent matchEvent, Score score, string key, string language)
    {
        var tr = language == "tr";
        var team = matchEvent.Side == TeamSide.Home ? match.HomeTeam : match.AwayTeam;
        var opponent = matchEvent.Side == TeamSide.Home ? match.AwayTeam : match.HomeTeam;
        var scoreline = Scoreline(match, score);

        (string Title, string Body)? text = matchEvent.Type switch
        {
            MatchEventType.Goal => (Goal(tr, scoreline), Who(matchEvent, team)),
            MatchEventType.PenaltyGoal => (Goal(tr, scoreline), $"{Who(matchEvent, team)} {(tr ? "(P)" : "(pen)")}"),
            // Counted for the team it helps, but scored by a player of the other one.
            MatchEventType.OwnGoal => (Goal(tr, scoreline), $"{Who(matchEvent, opponent)} {(tr ? "(K.K.)" : "(OG)")}"),
            MatchEventType.PenaltyMissed => (tr ? $"❌ Penaltı Kaçtı! {scoreline}" : $"❌ Penalty missed! {scoreline}", Who(matchEvent, team)),
            MatchEventType.RedCard => (tr ? $"🟥 Kırmızı Kart! {team.ShortName}" : $"🟥 Red card! {team.ShortName}",
                $"{Minute(matchEvent)}{matchEvent.Player ?? team.ShortName} ({scoreline})"),
            MatchEventType.PenaltyAwarded => (tr ? $"⚠️ Penaltı! {scoreline}" : $"⚠️ Penalty! {scoreline}",
                tr ? $"{Minute(matchEvent)}{team.ShortName} penaltı kazandı" : $"{Minute(matchEvent)}Penalty to {team.ShortName}"),
            _ => null,
        };

        return text is { } t
            ? new PushNotification(t.Title, t.Body, MatchUrl(match), $"{match.Id}:event:{key}", LiveTimeToLive, team.LogoUrl)
            : null;
    }

    /// <summary>A goal that was announced and then taken back (VAR, offside): the corrected score, and the change.</summary>
    /// <param name="side">The team the goal counted for.</param>
    /// <param name="goal">The goal as it was announced; null if it was announced without its scorer.</param>
    /// <param name="score">The score now, without it.</param>
    public static PushNotification GoalDisallowed(Match match, TeamSide side, MatchEvent? goal, Score score, string key, string language)
    {
        var tr = language == "tr";
        var team = side == TeamSide.Home ? match.HomeTeam : match.AwayTeam;
        var scorerTeam = goal?.Type == MatchEventType.OwnGoal ? (side == TeamSide.Home ? match.AwayTeam : match.HomeTeam) : team;
        var before = side == TeamSide.Home ? score with { Home = score.Home + 1 } : score with { Away = score.Away + 1 };

        var title = tr ? $"🚫 GOL İPTAL! {Scoreline(match, score)}" : $"🚫 GOAL DISALLOWED! {Scoreline(match, score)}";
        var who = goal is null ? team.ShortName : Who(goal, scorerTeam);
        var body = $"{who} · {before.Home} - {before.Away} → {score.Home} - {score.Away}";

        return new PushNotification(title, body, MatchUrl(match), $"{match.Id}:disallowed:{key}", LiveTimeToLive, team.LogoUrl);
    }

    /// <summary>A goal the score shows but the provider hasn't said who scored.</summary>
    public static PushNotification GoalWithoutScorer(Match match, TeamSide side, Score score, string key, string language)
    {
        var team = side == TeamSide.Home ? match.HomeTeam : match.AwayTeam;
        var title = Goal(language == "tr", Scoreline(match, score));

        return new PushNotification(title, team.ShortName, MatchUrl(match), $"{match.Id}:event:{key}", LiveTimeToLive, team.LogoUrl);
    }

    private static string Goal(bool tr, string scoreline) => tr ? $"⚽ GOL! {scoreline}" : $"⚽ GOAL! {scoreline}";

    /// <summary>"57' Mauro Icardi (Galatasaray)"; just the team if the player isn't known.</summary>
    private static string Who(MatchEvent matchEvent, Team team) =>
        matchEvent.Player is null ? $"{Minute(matchEvent)}{team.ShortName}" : $"{Minute(matchEvent)}{matchEvent.Player} ({team.ShortName})";

    // The provider's clock already ends in "'", e.g. "57'" or "45'+2'".
    private static string Minute(MatchEvent matchEvent) => string.IsNullOrEmpty(matchEvent.Clock) ? "" : $"{matchEvent.Clock} ";

    private static string Teams(Match match) => $"{match.HomeTeam.ShortName} - {match.AwayTeam.ShortName}";

    private static string Scoreline(Match match, Score score) =>
        $"{match.HomeTeam.ShortName} {score.Home} - {score.Away} {match.AwayTeam.ShortName}";

    // The same address the site itself gives a match page (frontend/src/route.ts).
    private static string MatchUrl(Match match) =>
        $"/?league={Uri.EscapeDataString(match.LeagueCode)}&match={Uri.EscapeDataString(match.Id)}";
}
