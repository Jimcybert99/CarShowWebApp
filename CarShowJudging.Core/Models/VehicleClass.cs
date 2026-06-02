namespace CarShowJudging.Core.Models;

public class VehicleClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
