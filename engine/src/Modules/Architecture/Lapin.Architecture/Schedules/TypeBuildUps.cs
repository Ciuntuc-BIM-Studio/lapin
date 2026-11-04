using System.Globalization;
using Lapin.Drawing;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Schedules;

/// <summary>
/// The type assemblies schedule's drawings (reports #65–#67): a card a wall or floor type, after the table, its
/// build-up cut to the schedule's scale — a wall as a plan cuts it, a floor as a section does, layer 1 on top —
/// each layer filled and hatched by its material as the section draws it, the core's faces drawn heavier, the total
/// dimensioned, and each layer named as the table names it (its number or name, material, thickness). In paper mm.
/// </summary>
public static class TypeBuildUps
{
    /// <summary>What a type assemblies schedule keeps in its <c>drawings</c> to draw the build-ups.</summary>
    public const string Drawing = "buildup";

    const double StripW = 40, Head = 9, DimLeft = 7, LabelGap = 6, LabelRow = 3.4, LabelText = 2.2, Pad = 3, MinW = 70;
    const double CutPen = 0.35, LayerPen = 0.18, ThinPen = 0.13;

    static string Mm(double m) => Math.Round(m * 1000, 1).ToString("0.#", CultureInfo.InvariantCulture);

    sealed class Sink
    {
        public readonly Dictionary<string, Pen> Pens = [];
        Pen Get(string key, Func<Pen> make) => Pens.TryGetValue(key, out var p) ? p : Pens[key] = make();
        public void Line(Vec2 a, Vec2 b, double width, string layer = "SCHEDULE-SYMBOLS") =>
            Get($"{layer}:{width}", () => new Pen(layer, "#000000", width, null, null)).Segments.Add((a, b));
        public void Fill(IReadOnlyList<Vec2> loop, string colour) => Get($"f:{colour}", () => new Pen("SCHEDULE-FILL", null, 0, colour, null)).Loops.Add(loop);
        public void Hatch(Vec2 a, Vec2 b) => Get("hatch", () => new Pen("SCHEDULE-HATCH", "#000000", ThinPen, null, null)).Segments.Add((a, b));
        public void Text(Vec2 at, string text, double size, bool bold = false, string anchor = "start", double angle = 0) =>
            Get("TEXT", () => new Pen("TEXT", "#000000", 0, null, null)).Texts.Add(new PenText(at, text, size, angle, anchor, "bottom", bold));
    }

    /// <summary>One card's content: the type's kind, mark and name, its layers' labels, thicknesses, materials and cores.</summary>
    public sealed record Card(string Kind, string Name, string Mark, IReadOnlyList<(string Label, string Material, double Thickness, bool Core, Entity? MaterialEntity)> Layers);

    /// <summary>The cards, in the table's order (every wall and floor type with layers).</summary>
    public static List<Card> Cards(IModelView m) => TypeExchange.Types(m).Select(x =>
    {
        var layers = LayerSets.LayersOf(m, x.Type.Id);
        return new Card(x.Kind, x.Type.Name, x.Type.Str("mark") ?? "", layers.Select((l, i) =>
        {
            var mat = m.Get(l.Material);
            return (l.Name is { Length: > 0 } n ? n : (i + 1).ToString(CultureInfo.InvariantCulture), mat?.Name ?? "", l.Thickness, l.Core, mat);
        }).ToList());
    }).Where(c => c.Layers.Count > 0 && c.Layers.Sum(l => l.Thickness) > 0).ToList();

    /// <summary>The cards laid out in bands no wider than <paramref name="maxWidth"/>, each band's pens from its own top.</summary>
    public static List<ScheduleBand> Bands(IModelView m, double scale = 50, double maxWidth = 380)
    {
        var cards = Cards(m);
        if (cards.Count == 0) return [];
        var k = 1000 / scale;
        var patterns = DraftingResources.Patterns(m);
        string LabelOf((string Label, string Material, double Thickness, bool Core, Entity? MaterialEntity) l) => $"{l.Label}  {l.Material}  {Mm(l.Thickness)}";
        double CardW(Card c) => Math.Max(MinW, DimLeft + StripW + LabelGap + c.Layers.Max(l => LabelOf(l).Length) * LabelText * 0.58 + Pad);
        double BodyH(Card c) => Math.Max(c.Layers.Sum(l => l.Thickness) * k, c.Layers.Count * LabelRow);

        var groups = new List<List<Card>> { new() };
        var used = 0.0;
        foreach (var c in cards)
        {
            if (groups[^1].Count > 0 && used + CardW(c) > maxWidth) { groups.Add([]); used = 0; }
            groups[^1].Add(c);
            used += CardW(c);
        }
        var bands = new List<ScheduleBand>();
        foreach (var g in groups)
        {
            var s = new Sink();
            var h = Head + g.Max(BodyH) + 2 * Pad + 4;
            var x = 0.0;
            foreach (var c in g)
            {
                var w = CardW(c);
                // The card's frame and its heading: the kind, the mark, the name.
                var corners = new[] { new Vec2(x, 0), new Vec2(x + w, 0), new Vec2(x + w, h), new Vec2(x, h) };
                for (var i = 0; i < 4; i++) s.Line(corners[i], corners[(i + 1) % 4], i == 0 || i == 2 ? CutPen : LayerPen, "SCHEDULE");
                var title = string.Join(" · ", new[] { c.Kind == "floor" ? "Floor" : "Wall", c.Mark, c.Name }.Where(t => t.Length > 0));
                s.Text(new Vec2(x + Pad, 6), title, 2.8, bold: true);
                s.Line(new Vec2(x, Head), new Vec2(x + w, Head), LayerPen, "SCHEDULE");

                // The strip, layer 1 on top, centred in the body's height.
                var total = c.Layers.Sum(l => l.Thickness) * k;
                var body = h - Head - 2 * Pad;
                var top = Head + Pad + (body - total) / 2;
                var (x0, x1) = (x + DimLeft, x + DimLeft + StripW);
                var y = top;
                var bounds = new List<double> { y };
                foreach (var l in c.Layers)
                {
                    var t = l.Thickness * k;
                    IReadOnlyList<Vec2> loop = [new(x0, y), new(x1, y), new(x1, y + t), new(x0, y + t)];
                    var look = MaterialGraphics.Resolve(l.MaterialEntity, "section", l.MaterialEntity == null ? l.Material : null);
                    var pattern = look.Hatch is { } hn ? HatchLibrary.Find(hn, patterns) : null;
                    if (pattern is { Solid: true }) s.Fill(loop, "#2b2b2b");
                    else
                    {
                        if (look.Fill is { Length: > 0 } f) s.Fill(loop, f);
                        if (pattern != null && t > 0.05)
                        {
                            var segs = BattHatch.Is(pattern)
                                ? BattHatch.Segments([loop], 0, 4000)
                                : HatchFill.Segments([loop], pattern, new HatchOptions(look.HatchScale, look.HatchAngle, new Vec2(x0, y), 4000)).Segments;
                            foreach (var seg in segs) s.Hatch(seg.A, seg.B);
                        }
                    }
                    y += t;
                    bounds.Add(y);
                }
                // The cut's outer faces and the core's heavier, the layers' joints lighter; the strip's ends broken off thin.
                for (var i = 0; i < bounds.Count; i++)
                {
                    var heavy = i == 0 || i == bounds.Count - 1 || (i > 0 && c.Layers[i - 1].Core) != (i < c.Layers.Count && c.Layers[i].Core);
                    s.Line(new Vec2(x0, bounds[i]), new Vec2(x1, bounds[i]), heavy ? CutPen : LayerPen);
                }
                s.Line(new Vec2(x0, top), new Vec2(x0, y), ThinPen);
                s.Line(new Vec2(x1, top), new Vec2(x1, y), ThinPen);

                // The total, dimensioned on the left, a tick at every joint.
                var dx = x + DimLeft - 3;
                s.Line(new Vec2(dx, top), new Vec2(dx, y), ThinPen);
                foreach (var b in bounds) s.Line(new Vec2(dx - 0.8, b + 0.8), new Vec2(dx + 0.8, b - 0.8), CutPen * 0.7);
                s.Text(new Vec2(dx - 0.8, (top + y) / 2), Mm(c.Layers.Sum(l => l.Thickness)), 2, anchor: "middle", angle: 90);

                // Each layer named beside it, a leader from its middle; the names kept a row apart, centred on the strip.
                var n = c.Layers.Count;
                var labelsTop = (top + y) / 2 - n * LabelRow / 2;
                for (var i = 0; i < n; i++)
                {
                    var mid = (bounds[i] + bounds[i + 1]) / 2;
                    var ly = labelsTop + (i + 0.5) * LabelRow;
                    var lx = x1 + LabelGap;
                    s.Line(new Vec2(x1 - 3, mid), new Vec2(x1 + 1.5, mid), ThinPen);
                    s.Line(new Vec2(x1 + 1.5, mid), new Vec2(lx - 1, ly), ThinPen);
                    s.Text(new Vec2(lx, ly + LabelText * 0.4), LabelOf(c.Layers[i]), LabelText, bold: c.Layers[i].Core);
                }
                x += w;
            }
            bands.Add(new ScheduleBand(x, h, s.Pens.Values.ToList()));
        }
        return bands;
    }
}
