using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.AssessmentSheets;

public sealed partial class AssessmentSheetService
{
    private sealed class ExcelImportRow
    {
        public int RowNumber { get; init; }
        public string? AssessmentCode { get; init; }
        public string? StudentCode { get; init; }
        public string? StudentName { get; init; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? DueDate { get; set; }
        public string? PlanGrade { get; init; }
        public string? PlanNote { get; init; }
        public string? NormalizedAssessmentCode { get; init; }
        public string? NormalizedStudentCode { get; init; }
        public string Action { get; set; } = "Invalid";
        public bool IsDuplicate { get; set; }
        public Student? Student { get; set; }
        public Assessment? Assessment { get; set; }
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];
    }

    private sealed class ExcelImportGroup(
        ExcelImportGroupKey key,
        List<ExcelImportRow> rows,
        Student student)
    {
        public ExcelImportGroupKey Key { get; } = key;
        public List<ExcelImportRow> Rows { get; } = rows;
        public Student Student { get; } = student;
        public DateTimeOffset StartDate => Key.StartDate;
        public DateTimeOffset DueDate => Key.DueDate;
        public Guid? ExistingSheetId { get; set; }
    }

    private sealed record ExcelImportGroupKey(Guid StudentId, DateTimeOffset StartDate, DateTimeOffset DueDate);

    private sealed class ExcelImportPlan(
        string fileName,
        List<ExcelImportRow> rows,
        List<ExcelImportGroup> groups)
    {
        public string FileName { get; } = fileName;
        public List<ExcelImportRow> Rows { get; } = rows;
        public List<ExcelImportGroup> Groups { get; } = groups;
        public bool CanImport => Rows.Count > 0 && Rows.All(x => x.Errors.Count == 0);
        public int ValidRowCount => Rows.Count(x => x.Errors.Count == 0 && !x.IsDuplicate);
        public int SkippedDuplicateRowCount => Rows.Count(x => x.IsDuplicate);
        public IReadOnlyList<string> Warnings => Rows
            .SelectMany(x => x.Warnings.Select(warning => $"Dòng {x.RowNumber}: {warning}"))
            .ToList();
    }
}
