using CanliSkor.Core.Domain;

namespace CanliSkor.Infrastructure.Espn;

/// <summary>
/// Players ESPN files under somebody else's record, put right as the data comes in. ESPN then uses the wrong
/// record's id for the player everywhere (squad, line-ups, goals), so correcting by that id fixes every page and
/// notification at once; the profile keeps only the numbers for the player's own team, since the record's personal
/// details (age, height, other clubs) are the other person's. Remove an entry once ESPN has fixed its data.
/// </summary>
internal static class EspnCorrections
{
    /// <param name="PlayerId">ESPN's id, which belongs to someone else.</param>
    /// <param name="Team">The team the player really plays for.</param>
    internal sealed record Correction(string PlayerId, Team Team, string Name, string ShortName, PlayerPosition Position);

    private static readonly Dictionary<string, Correction> ByPlayerId = new Correction[]
    {
        // Seen 2026-10-17: Galatasaray's no. 8 is filed under Gabriel Gomes, a defender at FC Imabari (Japan).
        new("268478", new Team("432", "Galatasaray", "Galatasaray", "https://a.espncdn.com/i/teamlogos/soccer/500/432.png"),
            "Gabriel Sara", "G. Sara", PlayerPosition.Midfielder),
    }.ToDictionary(c => c.PlayerId);

    public static MatchDetail Apply(MatchDetail detail) => detail with
    {
        Events = [.. detail.Events.Select(Apply)],
        Lineups = detail.Lineups is { } lineups ? new MatchLineups(Apply(lineups.Home), Apply(lineups.Away)) : null,
    };

    public static Squad Apply(Squad squad) => squad with
    {
        // Another position may move the player to another group.
        Players = EspnRosterMapper.InSquadOrder(squad.Players.Select(p => ByPlayerId.TryGetValue(p.Id, out var c)
            ? p with { Name = c.Name, Position = c.Position, Age = null }
            : p)),
    };

    public static PlayerProfile Apply(PlayerProfile profile) => ByPlayerId.TryGetValue(profile.Id, out var c)
        ? profile with
        {
            Name = c.Name,
            Position = c.Position,
            Team = c.Team,
            Age = null,
            HeightCm = null,
            Competitions = [.. profile.Competitions.Where(s => s.TeamName == c.Team.Name)],
        }
        : profile;

    public static TeamLineup Apply(TeamLineup lineup) => lineup with
    {
        Rows = [.. lineup.Rows.Select(row => (IReadOnlyList<LineupPlayer>)[.. row.Select(Apply)])],
        Bench = [.. lineup.Bench.Select(Apply)],
    };

    private static LineupPlayer Apply(LineupPlayer player) => ByPlayerId.TryGetValue(player.Id, out var c)
        ? player with { Name = c.Name, ShortName = c.ShortName }
        : player;

    private static MatchEvent Apply(MatchEvent matchEvent) => matchEvent with
    {
        Player = matchEvent.PlayerId is { } id && ByPlayerId.TryGetValue(id, out var player) ? player.Name : matchEvent.Player,
        RelatedPlayer = matchEvent.RelatedPlayerId is { } related && ByPlayerId.TryGetValue(related, out var other) ? other.Name : matchEvent.RelatedPlayer,
    };
}
