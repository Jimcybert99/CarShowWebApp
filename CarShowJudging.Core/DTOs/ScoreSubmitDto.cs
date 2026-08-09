namespace CarShowJudging.Core.DTOs;

public class ScoreSubmitDto
{
    public int VehicleId { get; set; }
    public string JudgeId { get; set; } = string.Empty;
    public int Exterior { get; set; }
    public int Interior { get; set; }
    public int EngineBay { get; set; }
    public int Craftsmanship { get; set; }
    public int Presentation { get; set; }
}
