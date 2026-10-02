using System.ComponentModel.DataAnnotations;

namespace CanliSkor.Infrastructure.Espn;

public sealed class EspnOptions
{
    public const string SectionName = "Espn";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://site.api.espn.com/apis/site/v2/sports/soccer/";
}
