using CanliSkor.Core.Domain;
using CanliSkor.Core.Services;

namespace CanliSkor.Core.Tests.Services;

public class TeamSearchTests
{
    private static readonly League SuperLig = new("tur.1", "Turkish Super Lig");
    private static readonly League Champions = new("uefa.champions", "UEFA Champions League");
    private static readonly League Nations = new("uefa.nations", "UEFA Nations League");

    private static Team T(string id, string name) => new(id, name, name, null);

    private static readonly (League, IReadOnlyList<Team>)[] Leagues =
    [
        (SuperLig, [T("432", "Galatasaray"), T("1895", "Beşiktaş"), T("436", "Fenerbahçe"), T("7", "Istanbul Başakşehir")]),
        (Champions, [T("432", "Galatasaray"), T("83", "Barcelona"), T("2", "São Paulo")]),
        (Nations, [T("465", "Türkiye"), T("482", "Portugal"), T("9", "Senegal")]),
    ];

    private static IEnumerable<string> Names(string? query) => TeamSearch.Find(query, Leagues).Select(r => r.Team.Name);

    [Fact]
    public void Finds_teams_whose_name_contains_the_query_whatever_the_case()
    {
        Assert.Equal(["Barcelona"], Names("BARCE"));
        Assert.Equal(["Fenerbahçe"], Names("bah"));
    }

    [Fact]
    public void Ignores_accents_and_the_dotless_i()
    {
        Assert.Equal(["Beşiktaş"], Names("besiktas"));
        Assert.Equal(["São Paulo"], Names("sao"));
        Assert.Equal(["Türkiye"], Names("turk"));
        Assert.Equal(["Istanbul Başakşehir"], Names("ıstanbul basak"));
    }

    [Fact]
    public void Names_starting_with_the_query_come_first()
    {
        // "gal": Galatasaray starts with it; Portugal and Senegal only contain it.
        Assert.Equal(["Galatasaray", "Portugal", "Senegal"], Names("gal"));
    }

    [Fact]
    public void Team_in_several_leagues_is_listed_once_under_the_first()
    {
        var result = Assert.Single(TeamSearch.Find("galatasaray", Leagues));

        Assert.Equal(SuperLig, result.League);
        Assert.Equal("432", result.Team.Id);
    }

    [Fact]
    public void Club_is_listed_under_its_domestic_league_even_if_a_cup_comes_first()
    {
        (League, IReadOnlyList<Team>)[] cupFirst =
        [
            (Champions, [T("432", "Galatasaray")]),
            (Nations, [T("465", "Türkiye")]),
            (SuperLig, [T("432", "Galatasaray")]),
        ];

        Assert.Equal(SuperLig, Assert.Single(TeamSearch.Find("galata", cupFirst)).League);
        // No domestic league knows a national team: it stays where it was found.
        Assert.Equal(Nations, Assert.Single(TeamSearch.Find("turkiye", cupFirst)).League);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" g ")]
    public void Query_shorter_than_two_characters_finds_nothing(string? query)
    {
        Assert.Empty(TeamSearch.Find(query, Leagues));
    }

    [Fact]
    public void Returns_at_most_twenty_teams()
    {
        var many = Enumerable.Range(0, 50).Select(i => T(i.ToString(), $"Team {i:00}")).ToList();

        var results = TeamSearch.Find("team", [(SuperLig, many)]);

        Assert.Equal(TeamSearch.MaxResults, results.Count);
        Assert.Equal("Team 00", results[0].Team.Name);
    }
}
