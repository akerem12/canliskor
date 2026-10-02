using System.ComponentModel.DataAnnotations;

namespace CanliSkor.Core.Options;

public sealed class PollingOptions
{
    public const string SectionName = "Polling";

    /// <summary>Delay between polls while any match is in play.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:10:00")]
    public TimeSpan LiveInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum delay between polls when nothing is live (also how often fixtures are refreshed).</summary>
    [Range(typeof(TimeSpan), "00:01:00", "06:00:00")]
    public TimeSpan IdleInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How long before a kickoff we switch to live polling, so the first minutes aren't missed.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "01:00:00")]
    public TimeSpan KickoffLeadTime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Upper bound on the next delay after a failed fetch, so we recover quickly once the provider is back.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "01:00:00")]
    public TimeSpan ErrorRetryInterval { get; set; } = TimeSpan.FromMinutes(1);
}
