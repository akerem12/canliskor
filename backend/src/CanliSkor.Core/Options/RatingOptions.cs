using System.ComponentModel.DataAnnotations;
using CanliSkor.Core.Domain;

namespace CanliSkor.Core.Options;

/// <summary>
/// Weights of our own player ratings. A rating starts at <see cref="Base"/> and every weight is added per
/// occurrence, so penalties are negative numbers.
/// </summary>
public sealed class RatingOptions : IValidatableObject
{
    public const string SectionName = "Ratings";

    public double Base { get; set; } = 6.5;

    /// <summary>Ratings are clamped to <see cref="Min"/>..<see cref="Max"/>.</summary>
    public double Min { get; set; } = 3;

    public double Max { get; set; } = 10;

    /// <summary>Players with fewer minutes on the pitch get no rating: there is too little to judge.</summary>
    [Range(0, 120)]
    public int MinMinutes { get; set; } = 10;

    /// <summary>A goal is worth more the further back the scorer plays.</summary>
    public PositionWeights Goal { get; set; } = new() { Goalkeeper = 1.5, Defender = 1.4, Midfielder = 1.2, Forward = 1.0 };

    public double Assist { get; set; } = 0.8;

    /// <summary>Per shot on target that wasn't a goal.</summary>
    public double ShotOnTarget { get; set; } = 0.2;

    public double FoulSuffered { get; set; } = 0.1;

    public double FoulCommitted { get; set; } = -0.1;

    public double Offside { get; set; } = -0.1;

    public double YellowCard { get; set; } = -0.4;

    public double RedCard { get; set; } = -1.5;

    public double OwnGoal { get; set; } = -1.2;

    public double Save { get; set; } = 0.3;

    /// <summary>Per goal conceded while the player was on the pitch.</summary>
    public PositionWeights GoalConceded { get; set; } = new() { Goalkeeper = -0.4, Defender = -0.25, Midfielder = -0.1 };

    /// <summary>No goal conceded while on the pitch for at least <see cref="CleanSheetMinMinutes"/>.</summary>
    public PositionWeights CleanSheet { get; set; } = new() { Goalkeeper = 0.6, Defender = 0.5, Midfielder = 0.2 };

    [Range(0, 120)]
    public int CleanSheetMinMinutes { get; set; } = 60;

    /// <summary>Applied once the match is over.</summary>
    public double Win { get; set; } = 0.3;

    public double Loss { get; set; } = -0.3;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!(Min <= Base && Base <= Max))
        {
            yield return new ValidationResult(
                $"{nameof(Base)} must lie between {nameof(Min)} and {nameof(Max)}.", [nameof(Base), nameof(Min), nameof(Max)]);
        }
    }
}

public sealed class PositionWeights
{
    public double Goalkeeper { get; set; }

    public double Defender { get; set; }

    public double Midfielder { get; set; }

    public double Forward { get; set; }

    public double For(PlayerPosition position) => position switch
    {
        PlayerPosition.Goalkeeper => Goalkeeper,
        PlayerPosition.Defender => Defender,
        PlayerPosition.Midfielder => Midfielder,
        _ => Forward,
    };
}
