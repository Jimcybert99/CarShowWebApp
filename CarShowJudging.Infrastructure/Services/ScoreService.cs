using CarShowJudging.Core.DTOs;
using CarShowJudging.Core.Interfaces;
using CarShowJudging.Core.Models;
using CarShowJudging.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarShowJudging.Infrastructure.Services;

public class ScoreService : IScoreService
{
    private readonly AppDbContext _db;

    public ScoreService(AppDbContext db) => _db = db;

    public async Task<Score> SubmitAsync(ScoreSubmitDto dto)
    {
        var existing = await _db.Scores
            .FirstOrDefaultAsync(s => s.VehicleId == dto.VehicleId && s.JudgeId == dto.JudgeId);

        if (existing is not null)
        {
            existing.Exterior = dto.Exterior;
            existing.Interior = dto.Interior;
            existing.EngineBay = dto.EngineBay;
            existing.Craftsmanship = dto.Craftsmanship;
            existing.Presentation = dto.Presentation;
            existing.ScoredAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }

        var score = new Score
        {
            VehicleId = dto.VehicleId,
            JudgeId = dto.JudgeId,
            Exterior = dto.Exterior,
            Interior = dto.Interior,
            EngineBay = dto.EngineBay,
            Craftsmanship = dto.Craftsmanship,
            Presentation = dto.Presentation,
            ScoredAt = DateTimeOffset.UtcNow
        };

        _db.Scores.Add(score);
        try
        {
            await _db.SaveChangesAsync();
            return score;
        }
        catch (DbUpdateException ex) when (IsScoreConflict(ex))
        {
            // A concurrent submission from the same judge for the same vehicle (e.g. a double
            // click) won the race and inserted first — fall back to updating that row instead.
            _db.Entry(score).State = EntityState.Detached;
            var winner = await _db.Scores
                .FirstAsync(s => s.VehicleId == dto.VehicleId && s.JudgeId == dto.JudgeId);
            winner.Exterior = dto.Exterior;
            winner.Interior = dto.Interior;
            winner.EngineBay = dto.EngineBay;
            winner.Craftsmanship = dto.Craftsmanship;
            winner.Presentation = dto.Presentation;
            winner.ScoredAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return winner;
        }
    }

    private static bool IsScoreConflict(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true
        && ex.InnerException.Message.Contains("Scores.VehicleId", StringComparison.OrdinalIgnoreCase);

    public Task<Score?> GetByVehicleAndJudgeAsync(int vehicleId, string judgeId) =>
        _db.Scores.FirstOrDefaultAsync(s => s.VehicleId == vehicleId && s.JudgeId == judgeId);

    public Task<List<Score>> GetScoresForVehicleAsync(int vehicleId) =>
        _db.Scores
            .Include(s => s.Judge)
            .Where(s => s.VehicleId == vehicleId)
            .ToListAsync();

    public async Task<List<ScoringRowDto>> GetScoringRowsAsync(int? classId, string? sortBy)
    {
        var query = _db.Vehicles
            .Include(v => v.Classes)
            .Include(v => v.Scores).ThenInclude(s => s.Judge)
            .AsQueryable();

        if (classId.HasValue)
            query = query.Where(v => v.Classes.Any(c => c.Id == classId.Value));

        var vehicles = await query.ToListAsync();

        var rows = vehicles.Select(v =>
        {
            var scores = v.Scores.ToList();
            var row = new ScoringRowDto
            {
                VehicleId = v.Id,
                EntryNumber = v.EntryNumber,
                OwnerName = v.OwnerName,
                Make = v.Make,
                Model = v.Model,
                Year = v.Year,
                PhotoUrl = v.PhotoUrl,
                ClassNames = v.Classes.Select(c => c.Name).ToList(),
                ScoredByJudgeNames = scores.Select(s => s.Judge?.DisplayName ?? s.Judge?.UserName ?? "Judge").ToList(),
                JudgeScores = scores.Select(s => new JudgeScoreDto
                {
                    JudgeName = s.Judge?.DisplayName ?? s.Judge?.UserName ?? "Judge",
                    Exterior = s.Exterior,
                    Interior = s.Interior,
                    EngineBay = s.EngineBay,
                    Craftsmanship = s.Craftsmanship,
                    Presentation = s.Presentation,
                    ScoredAt = s.ScoredAt
                }).ToList()
            };

            if (scores.Count > 0)
            {
                row.AvgExterior = scores.Average(s => s.Exterior);
                row.AvgInterior = scores.Average(s => s.Interior);
                row.AvgEngineBay = scores.Average(s => s.EngineBay);
                row.AvgCraftsmanship = scores.Average(s => s.Craftsmanship);
                row.AvgPresentation = scores.Average(s => s.Presentation);
                row.OverallScore = scores.Average(s => s.Overall);
            }

            return row;
        }).ToList();

        rows = sortBy switch
        {
            "Exterior" => rows.OrderByDescending(r => r.AvgExterior ?? -1).ToList(),
            "Interior" => rows.OrderByDescending(r => r.AvgInterior ?? -1).ToList(),
            "EngineBay" => rows.OrderByDescending(r => r.AvgEngineBay ?? -1).ToList(),
            "Craftsmanship" => rows.OrderByDescending(r => r.AvgCraftsmanship ?? -1).ToList(),
            "Presentation" => rows.OrderByDescending(r => r.AvgPresentation ?? -1).ToList(),
            _ => rows.OrderByDescending(r => r.OverallScore ?? -1).ToList()
        };

        return rows;
    }
}
