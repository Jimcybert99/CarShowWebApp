namespace CarShowJudging.Core.Models;

public class SiteNote
{
    public int Id { get; set; }
    public string? PageContext { get; set; }
    public int? VehicleId { get; set; }
    public int? ParentNoteId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsImportant { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EditedAt { get; set; }

    public Vehicle? Vehicle { get; set; }
    public SiteNote? Parent { get; set; }
    public ICollection<SiteNote> Replies { get; set; } = new List<SiteNote>();
}
