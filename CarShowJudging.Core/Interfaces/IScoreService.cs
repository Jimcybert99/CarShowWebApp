using CarShowJudging.Core.DTOs;
using CarShowJudging.Core.Models;

namespace CarShowJudging.Core.Interfaces;

public interface IScoreService
{
    Task<Score> SubmitAsync(ScoreSubmitDto dto);
    Task<Score?> GetByVehicleAndJudgeAsync(int vehicleId, string judgeId);
    Task<List<ScoringRowDto>> GetScoringRowsAsync(int? classId, string? sortBy);
    Task<List<Score>> GetScoresForVehicleAsync(int vehicleId);
}
