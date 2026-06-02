using CarShowJudging.Core.Models;

namespace CarShowJudging.Core.Interfaces;

public interface IClassService
{
    Task<List<VehicleClass>> GetAllAsync();
    Task<VehicleClass> AddAsync(string name);
    Task RemoveAsync(int id);
}
