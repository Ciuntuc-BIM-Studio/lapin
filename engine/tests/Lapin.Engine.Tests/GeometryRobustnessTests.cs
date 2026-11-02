using System.Text;
using Lapin.Drawing.Export;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// The geometry and drawing layers against what breaks them: fills that share an edge, images too big for an int,
/// texts beyond the DXF's code page, many dashed lines.
/// </summary>
public class GeometryRobustnessTests
{
    static double Area(IEnumerable<(Vec2 A, Vec2 B, Vec2 C)> tris) => tris.Sum(t => Math.Abs(Vec2.Cross(t.B - t.A, t.C - t.A)) / 2);

    static List<IReadOnlyList<Vec2>> Rects(params (double X0, double Y0, double X1, double Y1)[] rs) =>
        [.. rs.Select(r => (IReadOnlyList<Vec2>)[new(r.X0, r.Y0), new(r.X1, r.Y0), new(r.X1, r.Y1), new(r.X0, r.Y1)])];

    /// <summary>
    /// Two areas of one pen that share an edge (abutting walls of one material) are both filled in the DXF, as the
    /// PDF fills them: one's vertex on the other's boundary made it a hole of it, and both fills were lost. A hole and
    /// an island in it stay even-odd.
    /// </summary>
    [Fact]
    public void Dxf_fills_that_share_an_edge_are_both_filled()
    {
        // B starts on A's right edge; C shares A's top edge and starts on its corner.
        var loops = Rects((0, 0, 1, 1), (1, 0.5, 2, 1.5), (0, 1, 1, 2));
        Assert.Equal(3.0, Area(DxfWriter.Triangles(loops)), 9);
        var reversed = Rects((1, 0.5, 2, 1.5), (0, 1, 1, 2), (0, 0, 1, 1));
        Assert.Equal(3.0, Area(DxfWriter.Triangles(reversed)), 9);
        // B to the left of A, starting on A's left edge: a ray from there crosses A's right edge once, so it was "inside" A.
        List<IReadOnlyList<Vec2>> left = [Rects((0, 0, 1, 1))[0], [new(0, 0.5), new(0, 1.5), new(-1, 1.5), new(-1, 0.5)]];
        Assert.Equal(2.0, Area(DxfWriter.Triangles(left)), 9);
        // A frame with an island: 16 − 4 + 1.
        var nested = Rects((0, 0, 4, 4), (1, 1, 3, 3), (1.5, 1.5, 2.5, 2.5));
        Assert.Equal(13.0, Area(DxfWriter.Triangles(nested)), 9);
        // A hole that touches its outer loop's edge from inside is still a hole.
        var notch = Rects((0, 0, 4, 4), (0, 1, 1, 2));
        Assert.Equal(15.0, Area(DxfWriter.Triangles(notch)), 9);
    }

    [Fact]
    public void An_interior_point_lies_inside_even_when_vertices_touch_others()
    {
        Vec2[] l = [new(0, 0), new(10, 0), new(10, 0.001), new(0, 0.001)];
        Assert.True(Polygon2.Contains(l, Polygon2.InteriorPoint(l)));
        Vec2[] cw = [new(0, 0), new(0, 1), new(1, 1), new(1, 0)];
        Assert.True(Polygon2.Contains(cw, Polygon2.InteriorPoint(cw)));
        Vec2[] ell = [new(0, 0), new(2, 0), new(2, 1), new(1, 1), new(1, 2), new(0, 2)];
        Assert.True(Polygon2.Contains(ell, Polygon2.InteriorPoint(ell)));
    }

    /// <summary>
    /// A section through solids at site coordinates (half a million metres off the origin) closes every loop: the two
    /// faces sharing a cut edge find the very same crossing point, whichever way they walk it.
    /// </summary>
    [Fact]
    public void Sections_at_site_coordinates_close_their_loops()
    {
        var open = 0;
        for (var k = 0; k < 500; k++)
        {
            var c = new Vec2(512_345.678 + k * 0.137, 701_234.321 - k * 0.091);
            var solid = Prism.Cylinder(c, 101.3 + k * 0.01, 0.3 + k * 0.001, 3, 48)!;
            var a = 0.1 + k * 0.031;
            var dir = new Vec3(Math.Cos(a), Math.Sin(a), 0.02 * Math.Sin(k));
            var right = Vec3.Cross(dir, Vec3.UnitZ).Normalized();
            var view = new ViewFrame(new Vec3(c.X, c.Y, 102.7) + dir * 0.01 * Math.Cos(k), right, Vec3.Cross(right, dir).Normalized(), dir.Normalized());
            if (HiddenLines.Section(solid, view).Count != 1) open++;
        }
        Assert.Equal(0, open);
    }

    /// <summary>An outline with a doubled point offsets as the outline without it: every corner mitred, no notch.</summary>
    [Fact]
    public void An_offset_takes_a_repeated_vertex()
    {
        Vec2[] square = [new(0, 0), new(4, 0), new(4, 4), new(0, 4)];
        Vec2[] doubled = [new(0, 0), new(4, 0), new(4, 0), new(4, 4), new(0, 4)];
        var a = Polygon2.Offset(square, [1.0]);
        var b = Polygon2.Offset(doubled, [1.0]);
        Assert.Equal(5, b.Length);
        Assert.Equal([new Vec2(-1, -1), new Vec2(5, -1), new Vec2(5, -1), new Vec2(5, 5), new Vec2(-1, 5)], b.Select(p => new Vec2(Math.Round(p.X, 9), Math.Round(p.Y, 9))));
        Assert.Equal(a[1], b[2]);
    }

    /// <summary>Cut lines a kilometre long split at the corners standing on them, without walking every cell of their bounds.</summary>
    [Fact]
    public void Long_diagonal_cut_lines_split_quickly()
    {
        var lines = new List<Lapin.Drawing.Views.DLine>();
        for (var k = 0; k < 40; k++)
            lines.Add(new(new Vec2(k, 0), new Vec2(k + 1000, 1000), Lapin.Drawing.LineRole.Cut, "e", "IfcWall"));
        // A cut area's corner on the first line, halfway along it.
        Lapin.Drawing.Views.DPolygon cut = new([[new(500, 500), new(510, 500), new(510, 510)]], "f", "IfcSlab", null, null);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var split = Lapin.Drawing.Views.Generator.SplitAtCorners(lines, [cut]);
        clock.Stop();
        Assert.Equal(43, split.Count); // the first line split at two corners, the eleventh (x − 10 = y) at one
        Assert.Contains(split, l => l.B == new Vec2(500, 500));
        Assert.Contains(split, l => l.A == new Vec2(510, 510) || l.B == new Vec2(510, 510));
        Assert.True(clock.ElapsedMilliseconds < 2000, $"{clock.ElapsedMilliseconds} ms");
    }

    /// <summary>A face of many vertices (a terrain's boundary, a hatch's outline) is triangulated in well under a second: n − 2 triangles covering its area.</summary>
    [Fact]
    public void A_face_of_thousands_of_vertices_triangulates_quickly()
    {
        const int n = 6000;
        // A star-like ring: every other vertex pulled in, so half are reflex.
        var pts = Enumerable.Range(0, n).Select(i =>
        {
            var a = 2 * Math.PI * i / n;
            var r = i % 2 == 0 ? 100.0 : 97.0;
            return new Vec3(r * Math.Cos(a), r * Math.Sin(a), 0);
        }).ToList();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var tris = Triangulator.Face(pts, [.. Enumerable.Range(0, n)], [], Vec3.UnitZ).ToList();
        clock.Stop();
        Assert.Equal(n - 2, tris.Count);
        var area = tris.Sum(t => Vec3.Cross(pts[t.B] - pts[t.A], pts[t.C] - pts[t.A]).Z / 2);
        var ring = pts.Select(p => new Vec2(p.X, p.Y)).ToArray();
        Assert.Equal(Polygon2.SignedArea(ring), area, 6);
        Assert.True(clock.ElapsedMilliseconds < 3000, $"{clock.ElapsedMilliseconds} ms");
    }

    static byte[] Chunk(string kind, byte[] data)
    {
        var o = new List<byte>();
        o.AddRange([(byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length]);
        o.AddRange(Encoding.ASCII.GetBytes(kind));
        o.AddRange(data);
        o.AddRange([0, 0, 0, 0]); // the CRC, not checked
        return [.. o];
    }

    /// <summary>A PNG whose decoded size passes an int (an orthophoto of 30,000 × 30,000 RGBA) is not read: null, never an OverflowException out of the PDF export.</summary>
    [Fact]
    public void An_image_too_big_to_decode_is_left_out()
    {
        static byte[] Png(int w, int h, byte[] idat) =>
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            .. Chunk("IHDR", [(byte)(w >> 24), (byte)(w >> 16), (byte)(w >> 8), (byte)w, (byte)(h >> 24), (byte)(h >> 16), (byte)(h >> 8), (byte)h, 8, 6, 0, 0, 0]),
            .. Chunk("IDAT", idat),
            .. Chunk("IEND", []),
        ];
        Assert.Null(Raster.Read(Png(30_000, 30_000, [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01])));
        // A small one whose data ends early: null too.
        Assert.Null(Raster.Read(Png(4, 4, [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01])));
    }

    /// <summary>
    /// Texts in windows-1252: Romanian letters as \U+XXXX, what the code page has beyond Latin-1 (€) as itself, an
    /// emoji as one replacement, never as its two surrogates; a dash pattern used by many lines is one line type.
    /// </summary>
    [Fact]
    public void Dxf_texts_and_line_types()
    {
        var w = new DxfWriter();
        var layer = w.Layer("A", "#000000", 0.25, null);
        w.Text(new Vec2(0, 0), "Bucătărie € 🍳 é", 2.5, layer);
        for (var i = 0; i < 1000; i++) w.Line(new Vec2(i, 0), new Vec2(i, 1), layer, null, null, [1.0, 0.5]);
        w.Line(new Vec2(0, 0), new Vec2(0, 1), layer, null, null, [2.0, 0.5]);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = Encoding.GetEncoding(1252).GetString(w.Bytes("test"));
        Assert.Contains("Buc\\U+0103t\\U+0103rie € \\U+FFFD é", text);
        Assert.DoesNotContain("\\U+D83C", text);
        Assert.Contains("LAPIN1", text);
        Assert.Contains("LAPIN2", text);
        Assert.DoesNotContain("LAPIN3", text);
    }
}
