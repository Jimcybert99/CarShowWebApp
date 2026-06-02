namespace CarShowJudging.Core.DTOs;

public class ScoringRowDto
{
    public int VehicleId { get; set; }
    public int EntryNumber { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? PhotoUrl { get; set; }
    public List<string> ClassNames { get; set; } = new();
    public double? AvgCondition { get; set; }
    public double? AvgPaintAndBody { get; set; }
    public double? AvgInterior { get; set; }
    public double? AvgShowAppeal { get; set; }
    public double? AvgSuperCoolnessFactor { get; set; }
    public double? OverallScore { get; set; }
    public List<JudgeScoreDto> JudgeScores { get; set; } = new();
    public List<string> ScoredByJudgeNames { get; set; } = new();
}

public class JudgeScoreDto
{
    public string JudgeName { get; set; } = string.Empty;
    public int Condition { get; set; }
    public int PaintAndBody { get; set; }
    public int Interior { get; set; }
    public int ShowAppeal { get; set; }
    public int SuperCoolnessFactor { get; set; }
    public double Overall => (Condition + PaintAndBody + Interior + ShowAppeal + SuperCoolnessFactor) / 5.0;
    public DateTimeOffset ScoredAt { get; set; }
}
