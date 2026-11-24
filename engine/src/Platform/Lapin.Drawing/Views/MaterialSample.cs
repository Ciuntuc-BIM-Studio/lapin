using Lapin.Geometry;

namespace Lapin.Drawing.Views;

/// <summary>
/// A material drawn on a sample by the drawings' own path (decision 678): a band, or a strip of three layers with the
/// material between two plain neighbours, cut as a wall's layers are (<see cref="MaterialDrawing.Apply"/> then
/// <see cref="Pens.Of"/>), so what the material configurator previews is what a view, a sheet and the PDF print; and a
/// legend's swatch, filled and hatched the same way.
/// </summary>
public static class MaterialSample
{
    /// <summary>The class the sample's bands are cut as (a wall's layers: they separate with the layer pen).</summary>
    public const string Class = "IfcWall";
    const string Element = "sample";

    /// <summary>The neighbours' material in a strip: no hatch, no fill.</summary>
    static readonly ResolvedMaterial Plain = new("", null, 1, null, null, false, false, null, "own");

    /// <summary>
    /// The sample's pens, view coordinates in metres (y up, from 0,0 at the bottom left): a band <paramref name="widthMm"/> by
    /// <paramref name="heightMm"/> paper millimetres at 1:<paramref name="scale"/>; with <paramref name="strip"/> between two
    /// neighbours half as thick. An elevation's sample is a face it sees: its outline a seen line, no cut.
    /// </summary>
    public static (List<Pen> Pens, (Vec2 Min, Vec2 Max) Bounds) Draw(ResolvedMaterial look, string kind, double scale, double widthMm = 60, double heightMm = 15, bool strip = true,
        Pens.Options? options = null)
    {
        var u = scale / 1000;
        var (w, h) = (widthMm * u, heightMm * u);
        var seen = kind == "elevation";
        var bands = new List<(double Y0, double Y1, ResolvedMaterial G, string Key)>();
        if (strip && !seen)
        {
            var n = h / 2;
            bands.Add((0, n, Plain, "layer:0"));
            bands.Add((n, n + h, look, "layer:1"));
            bands.Add((n + h, n + h + n, Plain, "layer:2"));
        }
        else bands.Add((0, h, look, "layer:0"));
        var cuts = new List<DPolygon>();
        var lines = new List<DLine>();
        var role = seen ? LineRole.Projection : LineRole.Cut;
        foreach (var (y0, y1, g, key) in bands)
        {
            Vec2[] loop = [new(0, y0), new(w, y0), new(w, y1), new(0, y1)];
            cuts.Add(new DPolygon([loop], Element, Class, key, ReferenceEquals(g, Plain) ? "plain" : "material"));
            for (var i = 0; i < 4; i++)
            {
                var (a, b) = (loop[i], loop[(i + 1) % 4]);
                // One line between two bands, not two.
                if (lines.Any(l => (l.A - b).Length < 1e-9 && (l.B - a).Length < 1e-9 || (l.A - a).Length < 1e-9 && (l.B - b).Length < 1e-9)) continue;
                lines.Add(new DLine(a, b, role, Element, Class));
            }
        }
        var drawing = new ViewDrawing(cuts, lines);
        var materials = MaterialDrawing.Apply(drawing, p => p.Material == "plain" ? Plain : look);
        var o = (options ?? new Pens.Options()) with { Scale = scale, HiddenLines = false };
        var pens = Pens.Of(drawing, materials, o);
        return (pens, (new Vec2(0, 0), new Vec2(w, bands[^1].Y1)));
    }

    /// <summary>
    /// A legend's swatch: the box filled and hatched as the material is (its fill, background hatch and hatch, each in its
    /// colour and weight), its hatch laid from the box's corner, at <paramref name="unitsPerMm"/> drawing units per paper millimetre.
    /// No outline: the legend draws its own.
    /// </summary>
    /// <param name="viewScale">
    /// The scale of the view the swatch stands for (its denominator; 0: none): the material hatched inside the box as that view
    /// hatches it, a real-size pattern's courses as many as the view shows on so much paper (a surface legend's swatch, forum
    /// topic 255); none, at the box's own scale as before.
    /// </param>
    public static List<Pen> Swatch(ResolvedMaterial look, IReadOnlyList<Vec2> box, double unitsPerMm, IReadOnlyList<Hatch.HatchPattern>? patterns = null, int maxSegments = 4000, double viewScale = 0)
    {
        var own = unitsPerMm * 1000;
        if (viewScale > 0 && own > 0 && Math.Abs(viewScale / own - 1) > 1e-9)
        {
            // The box made as large as the view's model would be on so much paper, hatched at the view's scale, brought back.
            var k = viewScale / own;
            var at = box[0];
            var pens = Swatch(look, box.Select(p => at + (p - at) * k).ToList(), unitsPerMm * k, patterns, maxSegments);
            Vec2 Back(Vec2 p) => at + (p - at) * (1 / k);
            var back = new List<Pen>();
            foreach (var pen in pens)
            {
                var b = new Pen(pen.Layer, pen.Stroke, pen.Width, pen.Fill, pen.Dash);
                b.Segments.AddRange(pen.Segments.Select(sg => (Back(sg.A), Back(sg.B))));
                b.Loops.AddRange(pen.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(Back).ToArray()));
                back.Add(b);
            }
            return back;
        }
        var loop = box.ToArray();
        var drawing = new ViewDrawing([new DPolygon([loop], Element, Class, null, "material")], []);
        var materials = MaterialDrawing.Apply(drawing, _ => look);
        // From the box's corner, as the legend always laid its hatch.
        for (var i = 0; i < materials.Hatches.Count; i++)
            if (materials.Hatches[i].Origin == null) materials.Hatches[i] = materials.Hatches[i] with { Origin = loop[0] };
        return Pens.Of(drawing, materials, new Pens.Options(Scale: unitsPerMm * 1000, Patterns: patterns, MaxHatchSegments: maxSegments));
    }
}
