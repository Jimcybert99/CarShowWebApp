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
            existing.Condition = dto.Condition;
            existing.PaintAndBody = dto.PaintAndBody;
            existing.Interior = dto.Interior;
            existing.ShowAppeal = dto.ShowAppeal;
            existing.SuperCoolnessFactor = dto.SuperCoolnessFactor;
            existing.ScoredAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }

        var score = new Score
        {
            VehicleId = dto.VehicleId,
            JudgeId = dto.JudgeId,
            Condition = dto.Condition,
            PaintAndBody = dto.PaintAndBody,
            Interior = dto.Interior,
            ShowAppeal = dto.ShowAppeal,
            SuperCoolnessFactor = dto.SuperCoolnessFactor,
            ScoredAt = DateTimeOffset.UtcNow
        };

        _db.Scores.Add(score);
        await _db.SaveChangesAsync();
        return score;
    }

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
                    Condition = s.Condition,
                    PaintAndBody = s.PaintAndBody,
                    Interior = s.Interior,
                    ShowAppeal = s.ShowAppeal,
                    SuperCoolnessFactor = s.SuperCoolnessFactor,
                    ScoredAt = s.ScoredAt
                }).ToList()
            };

            if (scores.Count > 0)
            {
                row.AvgCondition = scores.Average(s => s.Condition);
                row.AvgPaintAndBody = scores.Average(s => s.PaintAndBody);
                row.AvgInterior = scores.Average(s => s.Interior);
                row.AvgShowAppeal = scores.Average(s => s.ShowAppeal);
                row.AvgSuperCoolnessFactor = scores.Average(s => s.SuperCoolnessFactor);
                row.OverallScore = scores.Average(s => s.Overall);
            }

            return row;
        }).ToList();

        rows = sortBy switch
        {
            "Condition" => rows.OrderByDescending(r => r.AvgCondition ?? -1).ToList(),
            "PaintAndBody" => rows.OrderByDescending(r => r.AvgPaintAndBody ?? -1).ToList(),
            "Interior" => rows.OrderByDescending(r => r.AvgInterior ?? -1).ToList(),
            "ShowAppeal" => rows.OrderByDescending(r => r.AvgShowAppeal ?? -1).ToList(),
            "SuperCoolnessFactor" => rows.OrderByDescending(r => r.AvgSuperCoolnessFactor ?? -1).ToList(),
            _ => rows.OrderByDescending(r => r.OverallScore ?? -1).ToList()
        };

        return rows;
    }
}
