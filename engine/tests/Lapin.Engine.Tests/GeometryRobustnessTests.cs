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
