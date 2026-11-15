using System.IO.Compression;
using System.Text;
using Lapin.Platform.Cde.Remarks;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, P21: a BCF zip with two file names that differ only by case (or one name written twice) threw out of
/// the reader as an engine error. Both are kept now; a reference finds its file as written first, else ignoring case.
/// </summary>
public class BcfNameCaseTests
{
    static byte[] Zip(params (string Name, string Text)[] files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, text) in files)
            {
                using var s = zip.CreateEntry(name).Open();
                s.Write(Encoding.UTF8.GetBytes(text));
            }
        return ms.ToArray();
    }

    static string Markup(string guid, string title, string viewpoint) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Markup>
          <Topic Guid="{guid}"><Title>{title}</Title><CreationDate>2026-11-15T10:00:00Z</CreationDate><CreationAuthor>ionut</CreationAuthor></Topic>
          <Viewpoints Guid="{guid[..^1]}9"><Viewpoint>{viewpoint}</Viewpoint></Viewpoints>
        </Markup>
        """;

    static string Viewpoint(string ifcGuid) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <VisualizationInfo><Components><Selection><Component IfcGuid="{ifcGuid}" /></Selection></Components></VisualizationInfo>
        """;

    [Fact]
    public void Two_names_that_differ_only_by_case_are_both_read_each_reference_finding_its_own()
    {
        const string a = "a1b2c3d4-0000-4000-8000-000000000001", b = "A1B2C3D4-0000-4000-8000-000000000002";
        var bytes = Zip(
            ("bcf.version", """<?xml version="1.0"?><Version VersionId="2.1" />"""),
            ($"{a}/markup.bcf", Markup(a, "First", "viewpoint.bcfv")),
            ($"{a}/viewpoint.bcfv", Viewpoint("1111111111111111111111")),
            ($"{a}/Viewpoint.bcfv", Viewpoint("2222222222222222222222")),
            ($"{b}/markup.bcf", Markup(b, "Second", "VIEWPOINT.BCFV")),
            ($"{b}/viewpoint.bcfv", Viewpoint("3333333333333333333333")),
            ($"{b}/viewpoint.bcfv", Viewpoint("4444444444444444444444")));
        var file = BcfReader.Read(bytes);
        Assert.Equal(["First", "Second"], file.Topics.Select(t => t.Title).Order(StringComparer.Ordinal));
        // Written as referenced: that file, not the one in another case.
        Assert.Equal(["1111111111111111111111"], file.Topics.Single(t => t.Title == "First").Viewpoints.Single().Selection);
        // Referenced in another case: found ignoring it, the first of the name written twice.
        Assert.Equal(["3333333333333333333333"], file.Topics.Single(t => t.Title == "Second").Viewpoints.Single().Selection);
    }
}
