namespace CarShowJudging.Core.Models;

public class Vehicle
{
    public int Id { get; set; }
    public int EntryNumber { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? RegisteredById { get; set; }
    public ApplicationUser? RegisteredBy { get; set; }
    public string? OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? PhotoUrl { get; set; }
    public string? RegistrationNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<VehicleClass> Classes { get; set; } = new List<VehicleClass>();
    public ICollection<Score> Scores { get; set; } = new List<Score>();
}
