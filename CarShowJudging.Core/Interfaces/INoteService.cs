using CarShowJudging.Core.Models;

namespace CarShowJudging.Core.Interfaces;

public interface INoteService
{
    Task<List<SiteNote>> GetPageNotesAsync(string pageContext);
    Task<List<SiteNote>> GetVehicleNotesAsync(int vehicleId);
    Task<(int count, bool hasImportant)> GetStatsAsync(string? pageContext, IEnumerable<int>? vehicleIds);
    Task<Dictionary<int, (int count, bool hasImportant)>> GetPerVehicleStatsAsync(IEnumerable<int> vehicleIds);
    Task<SiteNote> AddAsync(string? pageContext, int? vehicleId, int? parentNoteId,
        string authorId, string authorDisplayName, string content, bool isImportant);
    Task<SiteNote> EditAsync(int noteId, string requestingUserId, string newContent, bool isImportant);
    Task DeleteAsync(int noteId, string requestingUserId, bool isAdmin);
}
