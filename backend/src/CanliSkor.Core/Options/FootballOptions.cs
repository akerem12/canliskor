using System.ComponentModel.DataAnnotations;

namespace CanliSkor.Core.Options;

public sealed class FootballOptions
{
    public const string SectionName = "Football";

    /// <summary>League codes to follow, in display order, e.g. "tur.1".</summary>
    [MinLength(1)]
    public List<string> Leagues { get; set; } = [];
}
