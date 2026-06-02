namespace CarShowJudging.Core.DTOs;

public class VehicleRegistrationDto
{
    public string OwnerName { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; } = DateTime.UtcNow.Year;
    public List<int> SelectedClassIds { get; set; } = new();
    public Stream? PhotoStream { get; set; }
    public string? PhotoFileName { get; set; }
}
