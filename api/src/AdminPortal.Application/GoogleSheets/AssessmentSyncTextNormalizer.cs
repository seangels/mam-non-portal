namespace AdminPortal.Application.GoogleSheets;

/// <summary>
/// Chuẩn hoá text danh mục <c>Assessment</c> (Name, GroupLv1/2/3Name) khi đồng bộ từ Google Sheets.
/// Chỉ cắt whitespace ở hai đầu; giữ nguyên nội dung bên trong, bao gồm xuống dòng, space thừa và
/// dòng trống — tên/nhóm được phép nhiều dòng và phải giữ nguyên xi từ sync cho tới snapshot.
/// </summary>
public static class AssessmentSyncTextNormalizer
{
    public static string NormalizeRequiredName(string? value) =>
        NormalizeOptionalName(value) ?? string.Empty;

    public static string? NormalizeOptionalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
