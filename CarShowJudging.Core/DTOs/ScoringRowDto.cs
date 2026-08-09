using CarShowJudging.Core.Constants;

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
    public double? AvgExterior { get; set; }
    public double? AvgInterior { get; set; }
    public double? AvgEngineBay { get; set; }
    public double? AvgCraftsmanship { get; set; }
    public double? AvgPresentation { get; set; }
    public double? OverallScore { get; set; }
    public List<JudgeScoreDto> JudgeScores { get; set; } = new();
    public List<string> ScoredByJudgeNames { get; set; } = new();
}

public class JudgeScoreDto
{
    public string JudgeName { get; set; } = string.Empty;
    public int Exterior { get; set; }
    public int Interior { get; set; }
    public int EngineBay { get; set; }
    public int Craftsmanship { get; set; }
    public int Presentation { get; set; }
    public double Overall =>
        Exterior * ScoreWeights.Exterior +
        Interior * ScoreWeights.Interior +
        EngineBay * ScoreWeights.EngineBay +
        Craftsmanship * ScoreWeights.Craftsmanship +
        Presentation * ScoreWeights.Presentation;
    public DateTimeOffset ScoredAt { get; set; }
}
