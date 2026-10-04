using System.Text.Json;
using CanliSkor.Infrastructure.Espn.Dtos;

namespace CanliSkor.Infrastructure.Tests.Espn;

// ESPN sent {"didSub": false} instead of false for Portugal v Norway (uefa.nations, 2026-10-04), which made the whole match unreadable.
public class EspnSubstitutionFlagConverterTests
{
    private static EspnRosterEntry Read(string json) =>
        JsonSerializer.Deserialize<EspnRosterEntry>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void Plain_booleans_are_read()
    {
        var entry = Read("""{"starter":true,"subbedIn":false,"subbedOut":true}""");

        Assert.False(entry.SubbedIn);
        Assert.True(entry.SubbedOut);
    }

    [Fact]
    public void Objects_with_didSub_are_read()
    {
        var entry = Read("""{"starter":false,"subbedIn":{"didSub":true},"subbedOut":{"didSub":false},"jersey":"22"}""");

        Assert.True(entry.SubbedIn);
        Assert.False(entry.SubbedOut);
        Assert.Equal("22", entry.Jersey);
    }

    [Fact]
    public void Anything_else_counts_as_not_substituted()
    {
        var entry = Read("""{"subbedIn":{},"subbedOut":"yes","jersey":"7"}""");

        Assert.False(entry.SubbedIn);
        Assert.False(entry.SubbedOut);
        Assert.Equal("7", entry.Jersey);
    }
}
