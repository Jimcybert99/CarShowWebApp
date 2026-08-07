using CarShowJudging.Core.DTOs;
using CarShowJudging.Core.Interfaces;
using CarShowJudging.Core.Models;
using CarShowJudging.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarShowJudging.Infrastructure.Services;

public class VehicleService : IVehicleService
{
    private readonly AppDbContext _db;
    private readonly IBlobStorageService _blob;

    public VehicleService(AppDbContext db, IBlobStorageService blob)
    {
        _db = db;
        _blob = blob;
    }

    public async Task<Vehicle> RegisterAsync(VehicleRegistrationDto dto, string registeredById)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        int entryNumber;
        if (dto.EntryNumber > 0)
        {
            var taken = await _db.Vehicles.AnyAsync(v => v.EntryNumber == dto.EntryNumber);
            if (taken)
                throw new InvalidOperationException($"Entry number {dto.EntryNumber} is already in use.");
            entryNumber = dto.EntryNumber;
        }
        else
        {
            entryNumber = (await _db.Vehicles.MaxAsync(v => (int?)v.EntryNumber) ?? 0) + 1;
        }

        var vehicle = new Vehicle
        {
            EntryNumber = entryNumber,
            OwnerName = dto.OwnerName,
            RegisteredById = registeredById,
            Make = dto.Make,
            Model = dto.Model,
            Year = dto.Year,
            RegistrationNote = string.IsNullOrWhiteSpace(dto.RegistrationNote) ? null : dto.RegistrationNote.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (dto.SelectedClassIds.Count > 0)
        {
            var classes = await _db.VehicleClasses
                .Where(c => dto.SelectedClassIds.Contains(c.Id))
                .ToListAsync();
            vehicle.Classes = classes;
        }

        if (dto.PhotoStream is not null && dto.PhotoFileName is not null)
        {
            var ext = Path.GetExtension(dto.PhotoFileName);
            var blobName = $"vehicles/{Guid.NewGuid()}{ext}";
            vehicle.PhotoUrl = await _blob.UploadAsync(dto.PhotoStream, blobName, "image/jpeg");
        }

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return vehicle;
    }

    public Task<List<Vehicle>> GetAllAsync() =>
        _db.Vehicles
            .Include(v => v.Classes)
            .Include(v => v.Scores).ThenInclude(s => s.Judge)
            .OrderBy(v => v.EntryNumber)
            .ToListAsync();

    public Task<List<Vehicle>> GetByOwnerAsync(string ownerId) =>
        _db.Vehicles
            .Include(v => v.Classes)
            .Where(v => v.RegisteredById == ownerId)
            .OrderBy(v => v.EntryNumber)
            .ToListAsync();

    public Task<Vehicle?> GetByIdAsync(int id) =>
        _db.Vehicles
            .Include(v => v.Classes)
            .Include(v => v.Scores).ThenInclude(s => s.Judge)
            .FirstOrDefaultAsync(v => v.Id == id);

    public async Task DeleteAsync(int vehicleId, string requestingUserId, string requestingUserRole)
    {
        var vehicle = await _db.Vehicles.FindAsync(vehicleId)
            ?? throw new KeyNotFoundException($"Vehicle {vehicleId} not found.");

        var isAdminOrJudge = requestingUserRole is "Admin" or "Judge";
        if (!isAdminOrJudge && vehicle.RegisteredById != requestingUserId)
            throw new UnauthorizedAccessException("You can only delete your own entries.");

        _db.Vehicles.Remove(vehicle);
        await _db.SaveChangesAsync();
    }

    public Task<List<int>> GetUsedEntryNumbersAsync() =>
        _db.Vehicles.Select(v => v.EntryNumber).OrderBy(n => n).ToListAsync();
}
