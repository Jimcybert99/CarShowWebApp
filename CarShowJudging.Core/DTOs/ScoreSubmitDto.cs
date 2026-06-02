namespace CarShowJudging.Core.DTOs;

public class ScoreSubmitDto
{
    public int VehicleId { get; set; }
    public string JudgeId { get; set; } = string.Empty;
    public int Condition { get; set; }
    public int PaintAndBody { get; set; }
    public int Interior { get; set; }
    public int ShowAppeal { get; set; }
    public int SuperCoolnessFactor { get; set; }
}
