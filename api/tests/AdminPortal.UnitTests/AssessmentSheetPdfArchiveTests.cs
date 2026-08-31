using System.IO.Compression;
using System.Text;
using AdminPortal.Application.AssessmentSheets;

namespace AdminPortal.UnitTests;

public sealed class AssessmentSheetPdfArchiveTests
{
    [Fact]
    public void BuildZipWritesEveryFileAtItsPath()
    {
        var files = new List<(string Path, byte[] Content)>
        {
            ("khcn - S101.pdf", [1, 2, 3]),
            ("khcn - S102/page-001.png", [9, 9])
        };

        using var archive = OpenZip(AssessmentSheetPdfArchive.BuildZip(files, []));

        Assert.Equal(2, archive.Entries.Count);
        Assert.Equal([1, 2, 3], ReadEntry(archive, "khcn - S101.pdf"));
        Assert.Equal([9, 9], ReadEntry(archive, "khcn - S102/page-001.png"));
    }

    [Fact]
    public void BuildZipAddsSkipNoteFileOnlyWhenThereAreSkips()
    {
        using var withoutSkips = OpenZip(AssessmentSheetPdfArchive.BuildZip([("a.pdf", [0])], []));
        Assert.Null(withoutSkips.GetEntry("_bo-qua.txt"));

        using var withSkips = OpenZip(AssessmentSheetPdfArchive.BuildZip(
            [("a.pdf", [0])],
            ["KQ - S200: chưa có file PDF kết quả", "KQ - S201: Lỗi khi tải PDF từ Google Drive."]));

        var note = Encoding.UTF8.GetString(ReadEntry(withSkips, "_bo-qua.txt"));
        Assert.Contains("KQ - S200: chưa có file PDF kết quả", note);
        Assert.Contains("KQ - S201: Lỗi khi tải PDF từ Google Drive.", note);
    }

    private static ZipArchive OpenZip(byte[] bytes) =>
        new(new MemoryStream(bytes), ZipArchiveMode.Read);

    private static byte[] ReadEntry(ZipArchive archive, string path)
    {
        var entry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry(path));
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
