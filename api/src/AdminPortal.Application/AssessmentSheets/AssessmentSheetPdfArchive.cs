using System.IO.Compression;

namespace AdminPortal.Application.AssessmentSheets;

/// <summary>
/// Dựng file zip cho Bulk Action "Tải PDF/ảnh" trên màn danh sách bảng đánh giá. Tải và gộp hoàn toàn
/// ở backend theo yêu cầu người dùng (link Google Drive không fetch trực tiếp từ trình duyệt được).
/// Render PDF → PNG bằng PDFium/SkiaSharp (PDFtoImage). Thuần, không I/O ngoài các call render.
/// </summary>
public static class AssessmentSheetPdfArchive
{
    /// <summary>
    /// Gộp <paramref name="files"/> (đường dẫn trong zip → bytes) thành một zip. Khi có
    /// <paramref name="skippedNotes"/> thì thêm file <c>_bo-qua.txt</c> liệt kê các dòng bị bỏ qua.
    /// </summary>
    public static byte[] BuildZip(
        IReadOnlyList<(string Path, byte[] Content)> files,
        IReadOnlyList<string> skippedNotes)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }

            if (skippedNotes.Count > 0)
            {
                var entry = archive.CreateEntry("_bo-qua.txt", CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open());
                writer.WriteLine("Các bảng đánh giá bị bỏ qua (không có file PDF hoặc tải lỗi):");
                foreach (var note in skippedNotes)
                    writer.WriteLine($"- {note}");
            }
        }

        return output.ToArray();
    }

    /// <summary>Từng trang của PDF thành PNG (dpi mặc định của PDFtoImage), đánh số từ 1.</summary>
    // CA1416: PDFtoImage gắn [SupportedOSPlatform] cho Windows/Linux/macOS — phủ đủ mọi môi trường chạy
    // của API (IIS Windows, hoặc Linux container). Assembly net10.0 không gắn platform nên analyzer báo
    // nhầm "reachable on all platforms"; tắt cục bộ tại đúng call site.
#pragma warning disable CA1416
    public static IReadOnlyList<byte[]> PdfToPngPages(byte[] pdf)
    {
        var pages = new List<byte[]>();
        var pageCount = PDFtoImage.Conversion.GetPageCount(pdf);
        for (var page = 0; page < pageCount; page++)
        {
            using var pngStream = new MemoryStream();
            PDFtoImage.Conversion.SavePng(pngStream, pdf, page);
            pages.Add(pngStream.ToArray());
        }

        return pages;
    }
#pragma warning restore CA1416
}
