using CarShowJudging.Core.Interfaces;
using CarShowJudging.Core.Models;
using CarShowJudging.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CarShowJudging.Infrastructure.Services;

public class NoteService : INoteService
{
    private readonly AppDbContext _db;

    public NoteService(AppDbContext db) => _db = db;

    public Task<List<SiteNote>> GetPageNotesAsync(string pageContext) =>
        _db.SiteNotes
            .Where(n => n.PageContext == pageContext && n.ParentNoteId == null)
            .Include(n => n.Replies)
            .OrderBy(n => n.Id)
            .ToListAsync();

    public Task<List<SiteNote>> GetVehicleNotesAsync(int vehicleId) =>
        _db.SiteNotes
            .Where(n => n.VehicleId == vehicleId && n.ParentNoteId == null)
            .Include(n => n.Replies)
            .OrderBy(n => n.Id)
            .ToListAsync();

    public async Task<(int count, bool hasImportant)> GetStatsAsync(string? pageContext, IEnumerable<int>? vehicleIds)
    {
        var idList = vehicleIds?.ToList();

        var topLevel = await _db.SiteNotes
            .Where(n => n.ParentNoteId == null &&
                        ((pageContext != null && n.PageContext == pageContext) ||
                         (idList != null && idList.Count > 0 && n.VehicleId.HasValue && idList.Contains(n.VehicleId.Value))))
            .Select(n => new { n.Id, n.IsImportant })
            .ToListAsync();

        if (topLevel.Count == 0) return (0, false);

        var topIds = topLevel.Select(n => n.Id).ToList();
        var replies = await _db.SiteNotes
            .Where(n => n.ParentNoteId.HasValue && topIds.Contains(n.ParentNoteId.Value))
            .Select(n => n.IsImportant)
            .ToListAsync();

        return (topLevel.Count + replies.Count,
                topLevel.Any(n => n.IsImportant) || replies.Any(i => i));
    }

    public async Task<Dictionary<int, (int count, bool hasImportant)>> GetPerVehicleStatsAsync(IEnumerable<int> vehicleIds)
    {
        var ids = vehicleIds.ToList();
        var result = ids.ToDictionary(id => id, _ => (0, false));

        var topLevel = await _db.SiteNotes
            .Where(n => n.ParentNoteId == null && n.VehicleId.HasValue && ids.Contains(n.VehicleId.Value))
            .Select(n => new { n.Id, VehicleId = n.VehicleId!.Value, n.IsImportant })
            .ToListAsync();

        if (topLevel.Count == 0) return result;

        var topIds = topLevel.Select(n => n.Id).ToList();
        var replies = await _db.SiteNotes
            .Where(n => n.ParentNoteId.HasValue && topIds.Contains(n.ParentNoteId.Value))
            .Select(n => new { n.ParentNoteId, n.IsImportant })
            .ToListAsync();

        var parentLookup = topLevel.ToDictionary(n => n.Id, n => n.VehicleId);
        foreach (var grp in topLevel.GroupBy(n => n.VehicleId))
        {
            result[grp.Key] = (grp.Count(), grp.Any(n => n.IsImportant));
        }
        foreach (var r in replies)
        {
            if (r.ParentNoteId.HasValue && parentLookup.TryGetValue(r.ParentNoteId.Value, out var vid))
            {
                var (c, h) = result[vid];
                result[vid] = (c + 1, h || r.IsImportant);
            }
        }

        return result;
    }

    public async Task<SiteNote> AddAsync(string? pageContext, int? vehicleId, int? parentNoteId,
        string authorId, string authorDisplayName, string content, bool isImportant)
    {
        var note = new SiteNote
        {
            PageContext = pageContext,
            VehicleId = vehicleId,
            ParentNoteId = parentNoteId,
            AuthorId = authorId,
            AuthorDisplayName = authorDisplayName,
            Content = content,
            IsImportant = isImportant,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.SiteNotes.Add(note);
        await _db.SaveChangesAsync();
        return note;
    }

    public async Task<SiteNote> EditAsync(int noteId, string requestingUserId, bool isAdmin,
        string newContent, bool isImportant)
    {
        if (string.IsNullOrEmpty(requestingUserId))
            throw new UnauthorizedAccessException("You must be signed in to edit notes.");

        var note = await _db.SiteNotes.FindAsync(noteId)
            ?? throw new KeyNotFoundException($"Note {noteId} not found.");

        if (!isAdmin && note.AuthorId != requestingUserId)
            throw new UnauthorizedAccessException("You can only edit your own notes.");

        note.Content = newContent;
        note.IsImportant = isImportant;
        note.EditedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return note;
    }

    public async Task DeleteAsync(int noteId, string requestingUserId, bool isAdmin)
    {
        if (string.IsNullOrEmpty(requestingUserId))
            throw new UnauthorizedAccessException("You must be signed in to delete notes.");

        var note = await _db.SiteNotes
            .Include(n => n.Replies)
            .FirstOrDefaultAsync(n => n.Id == noteId)
            ?? throw new KeyNotFoundException($"Note {noteId} not found.");

        if (!isAdmin && note.AuthorId != requestingUserId)
            throw new UnauthorizedAccessException("You can only delete your own notes.");

        _db.SiteNotes.RemoveRange(note.Replies);
        _db.SiteNotes.Remove(note);
        await _db.SaveChangesAsync();
    }
}
