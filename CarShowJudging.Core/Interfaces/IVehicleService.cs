using CarShowJudging.Core.DTOs;
using CarShowJudging.Core.Models;

namespace CarShowJudging.Core.Interfaces;

public interface IVehicleService
{
    Task<Vehicle> RegisterAsync(VehicleRegistrationDto dto, string registeredById);
    Task<List<Vehicle>> GetAllAsync();
    Task<List<Vehicle>> GetByOwnerAsync(string ownerId);
    Task<Vehicle?> GetByIdAsync(int id);
    Task DeleteAsync(int vehicleId, string requestingUserId, string requestingUserRole);
    Task<List<int>> GetUsedEntryNumbersAsync();
}
