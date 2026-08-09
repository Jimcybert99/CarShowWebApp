using CarShowJudging.Core.Constants;

namespace CarShowJudging.Core.Models;

public class Score
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public string JudgeId { get; set; } = string.Empty;
    public ApplicationUser? Judge { get; set; }
    public int Exterior { get; set; }
    public int Interior { get; set; }
    public int EngineBay { get; set; }
    public int Craftsmanship { get; set; }
    public int Presentation { get; set; }
    public DateTimeOffset ScoredAt { get; set; } = DateTimeOffset.UtcNow;

    public double Overall =>
        Exterior * ScoreWeights.Exterior +
        Interior * ScoreWeights.Interior +
        EngineBay * ScoreWeights.EngineBay +
        Craftsmanship * ScoreWeights.Craftsmanship +
        Presentation * ScoreWeights.Presentation;
}
