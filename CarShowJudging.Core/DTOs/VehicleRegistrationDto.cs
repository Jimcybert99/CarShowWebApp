using System.ComponentModel.DataAnnotations;

namespace CarShowJudging.Core.DTOs;

public class VehicleRegistrationDto
{
    public int EntryNumber { get; set; }

    [Required(ErrorMessage = "Owner name is required.")]
    public string OwnerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Make is required.")]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = "Model is required.")]
    public string Model { get; set; } = string.Empty;

    [Range(1885, 2100, ErrorMessage = "Enter a valid vehicle year (1885–2100).")]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    public List<int> SelectedClassIds { get; set; } = new();
    public Stream? PhotoStream { get; set; }
    public string? PhotoFileName { get; set; }
}
