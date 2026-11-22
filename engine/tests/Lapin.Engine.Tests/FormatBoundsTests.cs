using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Mesh.Bos;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Damaged or crafted files are refused with a reason, never by filling the memory or with an internal error (audit
/// 22.11.2026, G19): Thrift counts, Parquet pages, a .bos's zip entries, a .lapin's parts, column kinds a newer Lapin wrote.
/// </summary>
public sealed class FormatBoundsTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-bounds-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    [Fact]
    public void A_thrift_map_longer_than_its_bytes_is_refused()
    {
        // Field 1, a map: 100 000 000 entries (varint), i32 → i32, then nothing.
        byte[] data = [0x1B, 0x80, 0xC2, 0xD7, 0x2F, 0x55, 0x00];
        Assert.Throws<InvalidDataException>(() => new Thrift.Reader(data, 0).ReadStruct());
    }

    [Fact]
    public void A_thrift_map_of_booleans_reads_a_byte_for_each()
    {
        // Field 1, a map of two entries bool → bool: each element is a byte (1 true, 2 false).
        byte[] data = [0x1B, 0x02, 0x11, 0x01, 0x02, 0x02, 0x01, 0x00];
        var o = new Thrift.Reader(data, 0).ReadStruct();
        Assert.Equal(new[] { true, false, false, true }, o.Items(1).Cast<bool>());
    }

    [Fact]
    public void Thrift_lists_nested_without_end_are_refused()
    {
        // Field 1, a list of one list of one list…: 0x19 then 0x19 repeated.
        var data = Enumerable.Repeat((byte)0x19, 100_000).ToArray();
        Assert.Throws<InvalidDataException>(() => new Thrift.Reader(data, 0).ReadStruct());
    }

    [Fact]
    public void A_parquet_page_claiming_more_values_than_its_bytes_is_refused()
    {
        var file = Craft(pageRows: 500_000_000, chunkRows: 500_000_000);
        Assert.True(file.Length < 300);
        var e = Assert.Throws<InvalidDataException>(() => Parquet.Read(file));
        Assert.Contains("claims", e.Message);
    }

    [Fact]
    public void A_parquet_page_with_more_rows_than_its_chunk_is_refused()
    {
        var e = Assert.Throws<InvalidDataException>(() => Parquet.Read(Craft(pageRows: 1000, chunkRows: 1)));
        Assert.Contains("claims", e.Message);
    }

    [Fact]
    public void A_parquet_data_page_without_its_header_is_refused()
    {
        var e = Assert.Throws<InvalidDataException>(() => Parquet.Read(Craft(pageRows: 1, chunkRows: 1, header: false)));
        Assert.Contains("no header", e.Message);
    }

    [Fact]
    public void A_crafted_parquet_still_reads_when_it_is_whole()
    {
        var col = Assert.Single(Parquet.Read(Craft(pageRows: 1, chunkRows: 1)));
        Assert.Equal(new[] { 7 }, col.Ints!);
    }

    [Fact]
    public void A_bos_with_a_damaged_table_is_not_a_valid_bos_not_an_internal_error()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var s = zip.CreateEntry("Entities.parquet").Open()) s.Write(Craft(pageRows: 1, chunkRows: 1, header: false));
        }
        var e = Assert.Throws<CommandException>(() => BosImport.Read(ms.ToArray(), "x.bos"));
        Assert.Contains("is not a BIM Open Schema file", e.Message);
        Assert.Contains("Entities.parquet", e.Message);
    }

    [Fact]
    public void A_lapin_part_larger_than_the_manifest_says_is_refused_before_it_is_inflated()
    {
        var path = Path.Combine(_dir, "bomb.lapin");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var s = zip.CreateEntry("manifest.json").Open())
                s.Write("""{"format":"lapin-project","version":3,"app":"x","product":"x","modules":[],"classes":{},"parts":[{"path":"project/document.json","module":"core","size":2,"sha256":"44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a"}]}"""u8);
            // 64 MB of zeros, a few dozen kilobytes once deflated.
            using var b = zip.CreateEntry("project/document.json", CompressionLevel.SmallestSize).Open();
            var zeros = new byte[1 << 20];
            for (var i = 0; i < 64; i++) b.Write(zeros);
        }
        Assert.True(new FileInfo(path).Length < 1 << 20);
        var e = Assert.Throws<InvalidDataException>(() => LapinPackage.Read(path));
        Assert.Contains("damaged", e.Message);
    }

    [Fact]
    public void A_column_kind_this_build_does_not_know_was_saved_by_a_newer_lapin()
    {
        var bytes = ArrowTables.WriteEntities([new Entity("a", "IfcWall", "W", new JsonObject { ["height"] = 3.0 }, null)], _ => 0);
        // The schema is in the stream's head and again in the file's footer: every copy says the new kind.
        var changed = 0;
        for (int at; (at = IndexOf(bytes, "Number"u8.ToArray())) >= 0; changed++) "Vector"u8.CopyTo(bytes.AsSpan(at));
        Assert.True(changed > 0);
        var e = Assert.Throws<InvalidDataException>(() => ArrowTables.ReadEntities(bytes));
        Assert.Contains("newer Lapin", e.Message);
    }

    [Fact]
    public void Two_storeys_whose_ids_differ_by_a_slash_keep_entries_of_their_own()
    {
        // Audit 22.11.2026: '/' and '\\' both became '_', so „a/b” and „a_b” were one entry and the project could not be read back.
        var f = new Fixture();
        // Any two entities stand for two storeys' elements; the rest for a third whose id holds the escape itself.
        var ids = f.Model.Entities.Select(e => e.Id).Take(2).ToList();
        Assert.Equal(2, ids.Count);
        string? StoreyOf(string id) => id == ids[0] ? "a/b" : id == ids[1] ? "a_b" : "a%2Fb";
        var build = LapinPackage.Build(f.Model, _ => "core", StoreyOf, "x", "x", []);
        var paths = build.Manifest.Parts.Select(p => p.Path).ToList();
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.Ordinal).Count());
        var path = Path.Combine(_dir, "slashes.lapin");
        LapinPackage.Write(path, build);
        var back = LapinPackage.Read(path);
        Assert.Equal(f.Model.Entities.Count(), back.Entities.Count);
    }

    [Fact]
    public void A_manifest_listing_a_part_twice_is_damaged_not_an_internal_error()
    {
        var path = Path.Combine(_dir, "twice.lapin");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var s = zip.CreateEntry("manifest.json").Open())
                s.Write("""{"format":"lapin-project","version":3,"app":"x","product":"x","modules":[],"classes":{},"parts":[{"path":"project/document.json","module":"core","size":2,"sha256":"44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a"},{"path":"project/document.json","module":"core","size":2,"sha256":"44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a"}]}"""u8);
            using (var s = zip.CreateEntry("project/document.json").Open()) s.Write("{}"u8);
        }
        var e = Assert.Throws<InvalidDataException>(() => LapinPackage.Read(path));
        Assert.Contains("twice", e.Message);
        Assert.Same(PackageForeign.None, PackageForeign.Of(File.ReadAllBytes(path)));
    }

    static int IndexOf(byte[] hay, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= hay.Length; i++) if (hay.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        return -1;
    }

    /// <summary>
    /// A Parquet file of one required INT32 column „a” holding 7, its data page saying <paramref name="pageRows"/> rows and
    /// its chunk <paramref name="chunkRows"/>; without <paramref name="header"/>, the page header lacks its data page header.
    /// </summary>
    static byte[] Craft(long pageRows, long chunkRows, bool header = true)
    {
        var o = new MemoryStream();
        o.Write("PAR1"u8);
        var raw = BitConverter.GetBytes(7);
        var ph = new Thrift.Writer().Int(1, 0).Int(2, raw.Length).Int(3, raw.Length);
        if (header) ph.Begin(5).Int(1, pageRows).Int(2, 0).Int(3, 3).Int(4, 3).End();
        var head = ph.Finish().ToArray();
        var offset = o.Position;
        o.Write(head);
        o.Write(raw);
        var w = new Thrift.Writer().Int(1, 1);
        w.BeginList(2, Thrift.TStruct, 2);
        w.BeginElement().Text(4, "schema").Int(5, 1).End();
        w.BeginElement().Int(1, 1).Int(3, 0).Text(4, "a").End();
        w.Long(3, chunkRows);
        w.BeginList(4, Thrift.TStruct, 1).BeginElement();
        w.BeginList(1, Thrift.TStruct, 1);
        w.BeginElement().Long(2, offset).Begin(3).Int(1, 1);
        w.BeginList(2, Thrift.TI32, 1).ListInt(0);
        w.BeginList(3, Thrift.TBinary, 1).ListText("a");
        w.Int(4, 0).Long(5, chunkRows).Long(6, head.Length + raw.Length).Long(7, head.Length + raw.Length).Long(9, offset).End().End();
        w.Long(2, head.Length + raw.Length).Long(3, chunkRows).End();
        var meta = w.Finish().ToArray();
        o.Write(meta);
        o.Write(BitConverter.GetBytes(meta.Length));
        o.Write("PAR1"u8);
        return o.ToArray();
    }
}
