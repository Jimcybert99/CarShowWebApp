using CarShowJudging.Core.DTOs;
using ClosedXML.Excel;

namespace CarShowJudging.Web.Services;

// Builds the Admin/Scores "Download Spreadsheet" export. Takes whatever the page already has
// on screen (already filtered by class and sorted per the leaderboard's own controls) and
// serializes it as-is — this class does no filtering or sorting of its own.
public static class ScoreSpreadsheetExporter
{
    private static readonly Dictionary<string, string> SortLabels = new()
    {
        ["Exterior"] = "Exterior",
        ["Interior"] = "Interior",
        ["EngineBay"] = "EngineBay",
        ["Craftsmanship"] = "Craftsmanship",
        ["Presentation"] = "Presentation",
    };

    public static byte[] BuildWorkbook(IReadOnlyList<ScoringRowDto> rows, string? className, string? sortBy)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(BuildLabel(className, sortBy)));

        string[] headers =
        [
            "#", "Year", "Make", "Model", "Owner", "Classes",
            "Exterior (30%)", "Interior (20%)", "Engine Bay (15%)", "Craftsmanship (20%)", "Presentation (15%)", "Overall"
        ];
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Font.FontColor = XLColor.White;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#212529");

        var r = 2;
        foreach (var row in rows)
        {
            sheet.Cell(r, 1).Value = row.EntryNumber;
            sheet.Cell(r, 2).Value = row.Year;
            sheet.Cell(r, 3).Value = row.Make;
            sheet.Cell(r, 4).Value = row.Model;
            sheet.Cell(r, 5).Value = row.OwnerName;
            sheet.Cell(r, 6).Value = string.Join(", ", row.ClassNames);
            SetScoreCell(sheet.Cell(r, 7), row.AvgExterior);
            SetScoreCell(sheet.Cell(r, 8), row.AvgInterior);
            SetScoreCell(sheet.Cell(r, 9), row.AvgEngineBay);
            SetScoreCell(sheet.Cell(r, 10), row.AvgCraftsmanship);
            SetScoreCell(sheet.Cell(r, 11), row.AvgPresentation);
            var overallCell = sheet.Cell(r, 12);
            SetScoreCell(overallCell, row.OverallScore);
            overallCell.Style.Font.Bold = true;
            r++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName(string? className, string? sortBy)
    {
        var classLabel = SanitizeFileNamePart(string.IsNullOrEmpty(className) ? "AllClasses" : className);
        var sortLabel = sortBy is not null && SortLabels.TryGetValue(sortBy, out var label) ? label : "Overall";
        return $"ScoringLeaderboard_{classLabel}_By{sortLabel}_{DateTime.Now:yyyy-MM-dd}.xlsx";
    }

    private static string BuildLabel(string? className, string? sortBy)
    {
        var classLabel = string.IsNullOrEmpty(className) ? "AllClasses" : className;
        var sortLabel = sortBy is not null && SortLabels.TryGetValue(sortBy, out var label) ? label : "Overall";
        return $"{classLabel} - By {sortLabel}";
    }

    private static void SetScoreCell(IXLCell cell, double? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
            cell.Style.NumberFormat.Format = "0.0";
        }
        else
        {
            cell.Value = "—";
        }
    }

    // Excel worksheet names can't contain : \ / ? * [ ] and are capped at 31 characters.
    private static string SanitizeSheetName(string name)
    {
        var cleaned = string.Concat(name.Select(c => "\\/?*[]:".Contains(c) ? '-' : c));
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    private static string SanitizeFileNamePart(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) || c == ' ' ? '-' : c));
    }
}
