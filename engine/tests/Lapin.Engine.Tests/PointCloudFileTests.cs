using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Lapin.PointClouds;

namespace Lapin.Engine.Tests;

/// <summary>
/// The readers against files written by other programs (scripts/pointclouds/make-fixtures.py: LAZ by laspy with lazrs,
/// E57 by pye57 on libE57Format), each with the values it was written with: LAZ point formats 1, 3, 6, 7 and 8, fixed and
/// variable chunks, extra bytes; E57 with two scans, one posed.
/// </summary>
public class PointCloudFileTests
{
    static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "pointclouds", name);

    static List<double[]> Expected(string name) => File.ReadAllLines(Fixture(name + ".expected.csv"))
        .Select(l => l.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToList();

    static List<(double X, double Y, double Z, ushort I, byte C, ushort R, ushort G, ushort B)> Read(IPointReader r)
    {
        var o = new List<(double, double, double, ushort, byte, ushort, ushort, ushort)>();
        foreach (var b in r.Read(1000))
            for (var i = 0; i < b.Count; i++) o.Add((b.X[i], b.Y[i], b.Z[i], b.Intensity[i], b.Class[i], b.R[i], b.G[i], b.B[i]));
        return o;
    }

    [Theory]
    [InlineData("f1.laz", 1)]
    [InlineData("f3.laz", 3)]
    [InlineData("f6.laz", 6)]
    [InlineData("f7.laz", 7)]
    [InlineData("f8.laz", 8)]
    public void A_laz_file_decodes_to_the_points_it_was_written_with(string name, int format)
    {
        var r = PointReaders.Open(new FileSource(Fixture(name)));
        Assert.StartsWith("LAZ", r.Header.Format);
        Assert.Contains($"point format {format}", r.Header.Format);
        var pts = Read(r);
        Assert.Equal(3000, pts.Count);
        var want = Expected(name);
        for (var k = 0; k < want.Count; k++)
        {
            var (p, w) = (pts[k * 7], want[k]);
            Assert.Equal(w[0], p.X, 3);
            Assert.Equal(w[1], p.Y, 3);
            Assert.Equal(w[2], p.Z, 3);
            Assert.Equal((ushort)w[3], p.I);
            Assert.Equal((byte)w[4], p.C);
            Assert.Equal((ushort)w[5], p.R);
            Assert.Equal((ushort)w[6], p.G);
            Assert.Equal((ushort)w[7], p.B);
        }
    }
}

public class E57FileTests
{
    static void Near(double want, double got) => Assert.True(Math.Abs(want - got) < 1e-3, $"{got} is not {want}");
    static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "pointclouds", name);

    [Fact]
    public void An_e57_file_with_three_scans_reads_posed_cartesian_bit_packed_and_spherical_points_without_its_invalid_ones()
    {
        var r = PointReaders.Open(new FileSource(Fixture("three-scans.e57")));
        Assert.Equal("E57 1.0, 3 scans", r.Header.Format);
        Assert.Equal(1200 + 600 + 500, r.Header.Count);
        Assert.True(r.Header.HasColor && r.Header.HasIntensity);
        var pts = new List<(double X, double Y, double Z, ushort I, ushort R, ushort G, ushort B)>();
        foreach (var b in r.Read(333))
            for (var i = 0; i < b.Count; i++) pts.Add((b.X[i], b.Y[i], b.Z[i], b.Intensity[i], b.R[i], b.G[i], b.B[i]));
        // Scan two's every tenth point and scan three's every 25th are invalid (state 2): left out.
        Assert.Equal(1200 + 540 + 480, pts.Count);
        var want = File.ReadAllLines(Fixture("three-scans.e57.expected.csv")).Select(l => l.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToList();
        for (var k = 0; k < want.Count; k++)
        {
            var (p, w) = (pts[k * 7], want[k]);
            Near(w[0], p.X);
            Near(w[1], p.Y);
            Near(w[2], p.Z);
            Assert.InRange(p.I, w[3] - 2, w[3] + 2);
            Assert.Equal((ushort)w[5], p.R);
            Assert.Equal((ushort)w[7], p.B);
        }
        // Scan three: spherical, its range a scaled integer, intensity on 12 bits.
        var sph = File.ReadAllLines(Fixture("three-scans.e57.spherical.csv")).Select(l => l.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToList();
        Assert.Equal(480, sph.Count);
        for (var k = 0; k < sph.Count; k++)
        {
            var p = pts[1200 + 540 + k];
            Near(sph[k][0], p.X);
            Near(sph[k][1], p.Y);
            Near(sph[k][2], p.Z);
            Assert.InRange(p.I, sph[k][3] / 4095 * 65535 - 1, sph[k][3] / 4095 * 65535 + 1);
        }
    }

    [Fact]
    public void A_damaged_e57_page_is_refused_by_its_checksum()
    {
        var bytes = File.ReadAllBytes(Fixture("three-scans.e57"));
        bytes[1024 + 100] ^= 0x5A;
        Assert.Throws<PointCloudException>(() => { var r = PointReaders.Open(new BytesSource("x.e57", bytes)); foreach (var _ in r.Read()) { } });
    }
}

/// <summary>
/// Damaged E57, PTS and CSV files are refused with a <see cref="PointCloudException"/> that says why, never a .NET
/// exception (audit 22.11.2026): an E57 scan without its fileOffset or recordCount, a bytestream past its packet; a PTS
/// count of more than 19 digits; a CSV heading naming more columns than its lines have.
/// </summary>
public class PointCloudDamageTests
{
    static void Refused(string name, byte[] bytes, string says)
    {
        var e = Assert.Throws<PointCloudException>(() => { var r = PointReaders.Open(new BytesSource(name, bytes)); foreach (var _ in r.Read()) { } });
        Assert.Contains(says, e.Message);
    }

    /// <summary>
    /// A one-scan E57 (pages of 1024 bytes): its XML at logical 48, its points' section at logical 2040 (physical 2048), a
    /// data packet right after the section's 32-byte header with three bytestreams of <paramref name="lengths"/>.
    /// </summary>
    static byte[] E57(string pointsAttributes, ushort[] lengths)
    {
        const int page = 1024, data = 1020;
        static long Physical(long logical) => logical / data * page + logical % data;
        var logical = new byte[data * 4];
        var xml = Encoding.UTF8.GetBytes(
            $"""<?xml version="1.0"?><e57Root type="Structure" xmlns="http://www.astm.org/COMMIT/E57/2010-e57-v1.0"><data3D type="Vector"><vectorChild type="Structure"><points type="CompressedVector" {pointsAttributes}><prototype type="Structure"><cartesianX type="Float" precision="single"/><cartesianY type="Float" precision="single"/><cartesianZ type="Float" precision="single"/></prototype></points></vectorChild></data3D></e57Root>""");
        "ASTM-E57"u8.CopyTo(logical);
        BinaryPrimitives.WriteUInt32LittleEndian(logical.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(logical.AsSpan(16), page * 4);
        BinaryPrimitives.WriteUInt64LittleEndian(logical.AsSpan(24), (ulong)Physical(48));
        BinaryPrimitives.WriteUInt64LittleEndian(logical.AsSpan(32), (ulong)xml.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(logical.AsSpan(40), page);
        xml.CopyTo(logical.AsSpan(48));
        const int section = 2040;
        logical[section] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(logical.AsSpan(section + 16), (ulong)Physical(section + 32));
        var packet = logical.AsSpan(section + 32);
        packet[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(packet[2..], 6 + 6 + 12 - 1);
        BinaryPrimitives.WriteUInt16LittleEndian(packet[4..], 3);
        for (var k = 0; k < 3; k++) BinaryPrimitives.WriteUInt16LittleEndian(packet[(6 + 2 * k)..], lengths[k]);
        var file = new byte[page * 4];
        for (var p = 0; p < 4; p++)
        {
            logical.AsSpan(p * data, data).CopyTo(file.AsSpan(p * page));
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(p * page + data), E57Reader.Crc32C(file.AsSpan(p * page, data)));
        }
        return file;
    }

    [Fact]
    public void A_whole_crafted_e57_reads_its_point()
    {
        var r = PointReaders.Open(new BytesSource("x.e57", E57("""fileOffset="2048" recordCount="1" """, [4, 4, 4])));
        Assert.Equal(1, r.Read().Sum(b => b.Count));
    }

    [Fact]
    public void An_e57_scan_without_its_file_offset_or_record_count_is_refused_by_name()
    {
        Refused("x.e57", E57("""recordCount="1" """, [4, 4, 4]), "fileOffset");
        Refused("x.e57", E57("""fileOffset="2048" """, [4, 4, 4]), "recordCount");
        Refused("x.e57", E57("""fileOffset="999999" recordCount="1" """, [4, 4, 4]), "past the end");
    }

    [Fact]
    public void An_e57_bytestream_longer_than_its_packet_is_refused()
    {
        Refused("x.e57", E57("""fileOffset="2048" recordCount="1" """, [4, 4, 400]), "runs past its data packet");
    }

    [Fact]
    public void A_pts_count_of_more_than_nineteen_digits_is_refused()
    {
        Refused("x.pts", "123456789012345678901234\n1 2 3 0 10 20 30\n"u8.ToArray(), "too large a number of points");
    }

    [Fact]
    public void A_csv_heading_with_more_columns_than_its_lines_is_refused()
    {
        Refused("x.csv", "x,y,z,intensity,red,green,blue\n1,2,3\n4,5,6\n"u8.ToArray(), "its lines have 3");
    }
}
