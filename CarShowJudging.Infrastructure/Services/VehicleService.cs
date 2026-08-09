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

    // Registration only ever issues a single SaveChangesAsync call, which EF Core already wraps
    // in its own atomic transaction — an explicit BeginTransactionAsync here added no safety and
    // was the likely source of "connection is already in a transaction" failures under load.
    public async Task<Vehicle> RegisterAsync(VehicleRegistrationDto dto, string registeredById)
    {
        if (dto.EntryNumber > 0 && await _db.Vehicles.AnyAsync(v => v.EntryNumber == dto.EntryNumber))
            throw new InvalidOperationException($"Entry number {dto.EntryNumber} is already in use.");

        var vehicle = new Vehicle
        {
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
            vehicle.Classes = await _db.VehicleClasses
                .Where(c => dto.SelectedClassIds.Contains(c.Id))
                .ToListAsync();
        }

        if (dto.PhotoStream is not null && dto.PhotoFileName is not null)
        {
            var ext = Path.GetExtension(dto.PhotoFileName);
            var blobName = $"vehicles/{Guid.NewGuid()}{ext}";
            vehicle.PhotoUrl = await _blob.UploadAsync(dto.PhotoStream, blobName, "image/jpeg");
        }

        _db.Vehicles.Add(vehicle);

        if (dto.EntryNumber > 0)
        {
            vehicle.EntryNumber = dto.EntryNumber;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsEntryNumberConflict(ex))
            {
                throw new InvalidOperationException($"Entry number {dto.EntryNumber} is already in use.");
            }
            return vehicle;
        }

        // Auto-assign path: two concurrent registrations can both read the same "next" number
        // before either commits, so retry a few times against the unique index instead of trusting
        // a single read-then-write.
        for (var attempt = 1; ; attempt++)
        {
            vehicle.EntryNumber = (await _db.Vehicles.MaxAsync(v => (int?)v.EntryNumber) ?? 0) + 1;
            try
            {
                await _db.SaveChangesAsync();
                return vehicle;
            }
            catch (DbUpdateException ex) when (IsEntryNumberConflict(ex) && attempt < 5)
            {
                // Someone else grabbed this number first — loop around and recompute.
            }
        }
    }

    public async Task UpdateAsync(int vehicleId, VehicleUpdateDto dto)
    {
        var vehicle = await _db.Vehicles.Include(v => v.Classes).FirstOrDefaultAsync(v => v.Id == vehicleId)
            ?? throw new KeyNotFoundException($"Vehicle {vehicleId} not found.");

        vehicle.OwnerName = dto.OwnerName;
        vehicle.Make = dto.Make;
        vehicle.Model = dto.Model;
        vehicle.Year = dto.Year;
        vehicle.RegistrationNote = string.IsNullOrWhiteSpace(dto.RegistrationNote) ? null : dto.RegistrationNote.Trim();

        vehicle.Classes = await _db.VehicleClasses
            .Where(c => dto.SelectedClassIds.Contains(c.Id))
            .ToListAsync();

        string? oldPhotoUrl = null;
        if (dto.PhotoStream is not null && dto.PhotoFileName is not null)
        {
            oldPhotoUrl = vehicle.PhotoUrl;
            var ext = Path.GetExtension(dto.PhotoFileName);
            var blobName = $"vehicles/{Guid.NewGuid()}{ext}";
            vehicle.PhotoUrl = await _blob.UploadAsync(dto.PhotoStream, blobName, "image/jpeg");
        }

        await _db.SaveChangesAsync();

        if (oldPhotoUrl is not null)
            await _blob.DeleteAsync(oldPhotoUrl);
    }

    private static bool IsEntryNumberConflict(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true
        && ex.InnerException.Message.Contains("EntryNumber", StringComparison.OrdinalIgnoreCase);

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

        var isAdminOrJudge = requestingUserRole is "Admin" or "Judge" or "SuperUser";
        if (!isAdminOrJudge && vehicle.RegisteredById != requestingUserId)
            throw new UnauthorizedAccessException("You can only delete your own entries.");

        _db.Vehicles.Remove(vehicle);
        await _db.SaveChangesAsync();
    }

    public Task<List<int>> GetUsedEntryNumbersAsync() =>
        _db.Vehicles.Select(v => v.EntryNumber).OrderBy(n => n).ToListAsync();
}
