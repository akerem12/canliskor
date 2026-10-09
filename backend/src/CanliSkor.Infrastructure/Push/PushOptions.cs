using System.ComponentModel.DataAnnotations;

namespace CanliSkor.Infrastructure.Push;

public sealed class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>
    /// Any long random text, kept out of the repository (on Render: the <c>Push__Secret</c> environment variable).
    /// The notification keys are derived from it; see <see cref="VapidKeys.FromSecret"/> for what happens without one.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>Who the push services can contact about our messages: an https or mailto address.</summary>
    [Required]
    public string Subject { get; set; } = "https://canliskor.onrender.com";
}
