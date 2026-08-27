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
            Paid = dto.Paid,
            RowNumber = dto.RowNumber,
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
            await using var photo = await ValidateAndBufferPhotoAsync(dto.PhotoStream, dto.PhotoFileName);
            var blobName = $"vehicles/{Guid.NewGuid()}{photo.Extension}";
            vehicle.PhotoUrl = await _blob.UploadAsync(photo.Stream, blobName, photo.ContentType);
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

    public async Task UpdateAsync(int vehicleId, VehicleUpdateDto dto, string requestingUserRole)
    {
        if (requestingUserRole is not ("Admin" or "SuperUser"))
            throw new UnauthorizedAccessException("Only an admin can edit this entry.");

        var vehicle = await _db.Vehicles.Include(v => v.Classes).FirstOrDefaultAsync(v => v.Id == vehicleId)
            ?? throw new KeyNotFoundException($"Vehicle {vehicleId} not found.");

        vehicle.OwnerName = dto.OwnerName;
        vehicle.Make = dto.Make;
        vehicle.Model = dto.Model;
        vehicle.Year = dto.Year;
        vehicle.RegistrationNote = string.IsNullOrWhiteSpace(dto.RegistrationNote) ? null : dto.RegistrationNote.Trim();
        vehicle.Paid = dto.Paid;
        vehicle.RowNumber = dto.RowNumber;

        vehicle.Classes = await _db.VehicleClasses
            .Where(c => dto.SelectedClassIds.Contains(c.Id))
            .ToListAsync();

        string? oldPhotoUrl = null;
        if (dto.PhotoStream is not null && dto.PhotoFileName is not null)
        {
            oldPhotoUrl = vehicle.PhotoUrl;
            await using var photo = await ValidateAndBufferPhotoAsync(dto.PhotoStream, dto.PhotoFileName);
            var blobName = $"vehicles/{Guid.NewGuid()}{photo.Extension}";
            vehicle.PhotoUrl = await _blob.UploadAsync(photo.Stream, blobName, photo.ContentType);
        }

        await _db.SaveChangesAsync();

        if (oldPhotoUrl is not null)
            await _blob.DeleteAsync(oldPhotoUrl);
    }

    // Photos are served back to any visitor via UseStaticFiles(), so a spoofed extension (e.g.
    // "photo.html" or "photo.svg" containing a <script>) would be stored and served as live,
    // same-origin executable content. The client's declared content-type and file extension are
    // untrusted, so both the extension and the actual file bytes (magic numbers) are checked
    // server-side before anything touches disk, and the whole file is buffered into memory (the
    // Razor components already cap uploads at 10MB) so the source stream can be fully consumed
    // and disposed here rather than left open for the caller to forget.
    // Each entry is a list of (offset, expected bytes) checks that must ALL match — WebP needs
    // two non-adjacent checks ("RIFF" at 0, "WEBP" at 8, with a 4-byte file-size field between
    // them that's ignored), while JPEG/PNG only need one. A signature list is intentionally not
    // "match any" — for WebP specifically, only checking the generic RIFF container marker would
    // also accept any other RIFF-based format (.wav, .avi, ...) renamed to .webp.
    private static readonly Dictionary<string, (int Offset, byte[] Bytes)[]> AllowedPhotoSignatures =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = [(0, [0xFF, 0xD8, 0xFF])],
            [".jpeg"] = [(0, [0xFF, 0xD8, 0xFF])],
            [".png"] = [(0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])],
            [".webp"] = [(0, [0x52, 0x49, 0x46, 0x46]), (8, [0x57, 0x45, 0x42, 0x50])], // "RIFF"…"WEBP"
        };

    private static async Task<ValidatedPhoto> ValidateAndBufferPhotoAsync(Stream source, string fileName)
    {
        // Wrapping the whole body (including the extension check) in `await using` guarantees
        // the caller's upload stream is disposed on every exit path, not just the happy one —
        // otherwise a rejected extension throws before the stream is ever touched, leaking it.
        await using (source)
        {
            var ext = Path.GetExtension(fileName);
            if (!AllowedPhotoSignatures.TryGetValue(ext, out var signatures))
                throw new InvalidOperationException("Only JPG, PNG, or WebP photos are supported.");

            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            buffer.Position = 0;

            var headerLength = signatures.Max(s => s.Offset + s.Bytes.Length);
            var header = new byte[headerLength];
            var read = await buffer.ReadAsync(header);
            buffer.Position = 0;

            var looksValid = signatures.All(sig =>
                read >= sig.Offset + sig.Bytes.Length &&
                header.AsSpan(sig.Offset, sig.Bytes.Length).SequenceEqual(sig.Bytes));
            if (!looksValid)
            {
                await buffer.DisposeAsync();
                throw new InvalidOperationException("The uploaded file doesn't look like a valid photo.");
            }

            var contentType = ext.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            return new ValidatedPhoto { Stream = buffer, Extension = ext, ContentType = contentType };
        }
    }

    private sealed class ValidatedPhoto : IAsyncDisposable
    {
        public required Stream Stream { get; init; }
        public required string Extension { get; init; }
        public required string ContentType { get; init; }
        public ValueTask DisposeAsync() => Stream.DisposeAsync();
    }

    // Deliberately narrow — any signed-in user who can already see a vehicle (My Vehicles, Judge
    // Entries, Admin Entries) is allowed to correct its note, but that's the only field this path
    // touches. Routing note edits through the full UpdateAsync/VehicleUpdateDto path would risk a
    // caller accidentally clobbering Make/Model/Classes/etc. with a partially-filled DTO.
    public async Task UpdateNoteAsync(int vehicleId, string? note)
    {
        var vehicle = await _db.Vehicles.FindAsync(vehicleId)
            ?? throw new KeyNotFoundException($"Vehicle {vehicleId} not found.");

        vehicle.RegistrationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await _db.SaveChangesAsync();
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

        var isAdmin = requestingUserRole is "Admin" or "SuperUser";
        if (!isAdmin && vehicle.RegisteredById != requestingUserId)
            throw new UnauthorizedAccessException("You can only delete your own entries.");

        _db.Vehicles.Remove(vehicle);
        await _db.SaveChangesAsync();
    }

    public Task<List<int>> GetUsedEntryNumbersAsync() =>
        _db.Vehicles.Select(v => v.EntryNumber).OrderBy(n => n).ToListAsync();
}
