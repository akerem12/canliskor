using CanliSkor.Core.Polling;

namespace CanliSkor.Core.Tests.Polling;

public class MatchViewerRegistryTests
{
    private readonly MatchViewerRegistry _registry = new();

    [Fact]
    public void Match_with_several_viewers_is_listed_once()
    {
        _registry.Watch("a", "tur.1", "1");
        _registry.Watch("b", "tur.1", "1");
        _registry.Watch("b", "eng.1", "2");

        Assert.Equal([("eng.1", "2"), ("tur.1", "1")], _registry.Watched().Order());
    }

    [Fact]
    public void Match_stays_watched_until_its_last_viewer_leaves()
    {
        _registry.Watch("a", "tur.1", "1");
        _registry.Watch("b", "tur.1", "1");

        _registry.Unwatch("a", "tur.1", "1");
        Assert.Single(_registry.Watched());

        _registry.Unwatch("b", "tur.1", "1");
        Assert.Empty(_registry.Watched());
    }

    [Fact]
    public void Disconnecting_drops_everything_the_connection_watched()
    {
        _registry.Watch("a", "tur.1", "1");
        _registry.Watch("a", "tur.1", "2");

        _registry.Disconnect("a");

        Assert.Empty(_registry.Watched());
    }

    [Fact]
    public void Connection_can_only_watch_a_few_matches_at_once()
    {
        for (var i = 0; i < MatchViewerRegistry.MaxMatchesPerConnection; i++)
        {
            Assert.True(_registry.Watch("a", "tur.1", i.ToString()));
        }

        Assert.False(_registry.Watch("a", "tur.1", "one too many"));
        // Watching one of them again is fine, and so is another connection.
        Assert.True(_registry.Watch("a", "tur.1", "0"));
        Assert.True(_registry.Watch("b", "tur.1", "one too many"));
    }

    [Fact]
    public void Unwatching_something_never_watched_is_harmless()
    {
        _registry.Unwatch("a", "tur.1", "1");
        _registry.Disconnect("a");

        Assert.Empty(_registry.Watched());
    }
}
