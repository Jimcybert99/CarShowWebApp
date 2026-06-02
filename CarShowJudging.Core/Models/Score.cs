namespace CarShowJudging.Core.Models;

public class Score
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public string JudgeId { get; set; } = string.Empty;
    public ApplicationUser? Judge { get; set; }
    public int Condition { get; set; }
    public int PaintAndBody { get; set; }
    public int Interior { get; set; }
    public int ShowAppeal { get; set; }
    public int SuperCoolnessFactor { get; set; }
    public DateTimeOffset ScoredAt { get; set; } = DateTimeOffset.UtcNow;

    public double Overall =>
        (Condition + PaintAndBody + Interior + ShowAppeal + SuperCoolnessFactor) / 5.0;
}
