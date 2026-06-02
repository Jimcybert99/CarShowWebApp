using CarShowJudging.Core.Interfaces;
using CarShowJudging.Core.Models;
using CarShowJudging.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarShowJudging.Infrastructure.Services;

public class ClassService : IClassService
{
    private readonly AppDbContext _db;

    public ClassService(AppDbContext db) => _db = db;

    public Task<List<VehicleClass>> GetAllAsync() =>
        _db.VehicleClasses.OrderBy(c => c.Name).ToListAsync();

    public async Task<VehicleClass> AddAsync(string name)
    {
        var cls = new VehicleClass { Name = name };
        _db.VehicleClasses.Add(cls);
        await _db.SaveChangesAsync();
        return cls;
    }

    public async Task RemoveAsync(int id)
    {
        var cls = await _db.VehicleClasses.FindAsync(id);
        if (cls is not null)
        {
            _db.VehicleClasses.Remove(cls);
            await _db.SaveChangesAsync();
        }
    }
}
