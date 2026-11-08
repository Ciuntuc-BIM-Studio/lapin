using System.Globalization;
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
