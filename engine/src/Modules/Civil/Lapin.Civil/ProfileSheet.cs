using System.Globalization;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A profile drawing on a sheet (T3): the long profile of a line or its cross sections, drawn by the engine as vectors from the
/// same data the profile editor shows (<see cref="LineProfiles"/>), so the sheet, its PDF and its DXF print what the editor
/// shows. The long profile: the level grid with its datum, the existing ground dashed, the design ground thin, the line's own
/// profile heavy with its points (PVIs) and vertical curves named, and under it a band with the stations, both grounds and the
/// cut or fill; its horizontal scale 1:N and its heights exaggerated (1×, 2×, 5×, 10×), cut into pages when wider than the
/// frame. The cross sections: one small graph each, both grounds across, the line's point marked, the cross line where one was
/// edited, laid in rows and pages.
/// </summary>
public sealed class ProfileSheet : ISheetContent
{
    public const string ExistingColor = "#6b6b6b", DesignColor = "#2e7d32", LineColor = "#c0392b";

    public bool Handles(Entity e) => e.Class == LineProfiles.ViewClass;

    static string F(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);

    /// <summary>The smallest step of <paramref name="steps"/> at least <paramref name="mm"/> apart on paper at <paramref name="k"/> mm a metre.</summary>
    static double Step(double k, double mm, double[] steps) => steps.FirstOrDefault(s => s * k >= mm, steps[^1]);

    static readonly double[] StationSteps = [1, 2, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000];
    static readonly double[] LevelSteps = [0.1, 0.2, 0.25, 0.5, 1, 2, 2.5, 5, 10, 20, 50, 100];

    public IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight)
    {
        var text = new Pen("TEXT", "#000000", 0, null, null);
        if (q.Model.Get(e.Str("line") ?? "") is not { } lineEntity || !LineProfiles.Has(lineEntity))
        {
            text.Texts.Add(new PenText(new Vec2(0, 6), e.Name, 4, Anchor: "start", Bold: true));
            text.Texts.Add(new PenText(new Vec2(0, 12), "The line this profile was drawn along is gone.", 2.5, Anchor: "start"));
            return [(120, 16, [text])];
        }
        LineProfiles.Line l;
        try { l = LineProfiles.Of(q.Model, lineEntity); }
        catch (CommandException err)
        {
            text.Texts.Add(new PenText(new Vec2(0, 6), e.Name, 4, Anchor: "start", Bold: true));
            text.Texts.Add(new PenText(new Vec2(0, 12), err.Message, 2.5, Anchor: "start"));
            return [(120, 16, [text])];
        }
        var scale = Math.Max(1, e.Num("scale") is var s && s > 0 ? s : 500);
        var ex = Math.Max(1, e.Num("exaggeration") is var x && x > 0 ? x : 1);
        return e.Str("kind") == "cross"
            ? Cross(q, e, l, scale, ex, Math.Max(60, maxWidth), maxHeight)
            : Long(q, e, l, scale, ex, Math.Max(80, maxWidth));
    }

    /// <summary>A ground sampled along a line as (station, z) runs, gaps where it is off the ground.</summary>
    static List<List<(double S, double Z)>> Runs(IEnumerable<(double S, double? Z)> samples)
    {
        var runs = new List<List<(double, double)>>();
        List<(double, double)>? run = null;
        foreach (var (st, z) in samples)
        {
            if (z is not { } h) { run = null; continue; }
            if (run == null) { run = []; runs.Add(run); }
            run.Add((st, h));
        }
        return runs;
    }

    static double? At(IReadOnlyList<(double S, double? Z)> samples, double s)
    {
        for (var i = 1; i < samples.Count; i++)
            if (s <= samples[i].S + 1e-9 && s >= samples[i - 1].S - 1e-9)
            {
                if (samples[i - 1].Z is not { } a || samples[i].Z is not { } b) return null;
                var d = samples[i].S - samples[i - 1].S;
                return d < 1e-12 ? b : a + (b - a) * (s - samples[i - 1].S) / d;
            }
        return null;
    }

    public static IReadOnlyList<(double W, double H, List<Pen> Pens)> Long(IQueryContext q, Entity view, LineProfiles.Line l, double scale, double ex, double maxWidth)
    {
        var k = 1000 / scale;
        var kv = k * ex;
        var existing = l.Terrain is { } t ? LineProfiles.Sample(Terrains.Existing(t), l.Path) : [];
        var design = l.Terrain is { } t2 ? LineProfiles.Sample(Terrains.Of(t2), l.Path) : [];
        var zs = existing.Concat(design).Select(p => p.Z).OfType<double>().Concat(l.Laid.Select(p => p.Z)).Concat(l.Points.Select(p => p.Z)).Where(double.IsFinite).ToList();
        if (zs.Count == 0) zs = [0, 1];
        var levelStep = Step(kv, 8, LevelSteps);
        var datum = Math.Floor((zs.Min() - levelStep) / levelStep) * levelStep;
        var top = Math.Ceiling((zs.Max() + levelStep) / levelStep) * levelStep;
        var stStep = Step(k, 18, StationSteps);
        const double Left = 24, Right = 6, Title = 14, Row = 6;
        var gh = (top - datum) * kv;
        var bandTop = Title + gh + 4;
        var h = bandTop + 4 * Row + 2;
        // Cut into pages by station when wider than the frame.
        var usable = Math.Max(40, maxWidth - Left - Right);
        var span = usable / k;
        var pages = Math.Max(1, (int)Math.Ceiling(l.Length / span - 1e-9));
        var o = new List<(double, double, List<Pen>)>();
        for (var pg = 0; pg < pages; pg++)
        {
            var s0 = pg * span;
            var s1 = Math.Min(l.Length, s0 + span);
            var w = Left + (s1 - s0) * k + Right;
            var text = new Pen("TEXT", "#000000", 0, null, null);
            var axis = new Pen("PROFILE-AXIS", "#000000", 0.25, null, null);
            var grid = new Pen("PROFILE-GRID", "#cccccc", 0.1, null, null);
            var ex1 = new Pen("PROFILE-EXISTING", ExistingColor, 0.25, null, [1.5, 1]);
            var de1 = new Pen("PROFILE-DESIGN", DesignColor, 0.25, null, null);
            var red = new Pen("PROFILE-LINE", LineColor, 0.5, null, null);
            var pvi = new Pen("PROFILE-POINT", LineColor, 0.25, null, null);
            double X(double st) => Left + (st - s0) * k;
            double Y(double z) => Title + gh - (z - datum) * kv;
            void T(double xx, double yy, string s, double size, string anchor = "start", bool bold = false) => text.Texts.Add(new PenText(new Vec2(xx, yy), s, size, Anchor: anchor, Bold: bold));
            T(0, 5, $"{view.Name}", 4, bold: true);
            T(0, 10, $"Long profile · {l.Entity.Name} · H 1:{F(scale, "0")} · V 1:{F(scale / ex, "0.#")} ({F(ex, "0.#")}×) · datum {F(datum)}" + (pages > 1 ? $" · {pg + 1}/{pages}" : ""), 2.5);
            // The grid: levels and stations.
            for (var z = datum; z <= top + 1e-9; z += levelStep)
            {
                grid.Segments.Add((new Vec2(Left, Y(z)), new Vec2(X(s1), Y(z))));
                T(Left - 1.5, Y(z) + 0.8, F(z, levelStep < 1 ? "0.00" : "0.0"), 2.2, "end");
            }
            var first = Math.Ceiling((s0 + l.Start) / stStep - 1e-9) * stStep - l.Start;
            var marks = new List<double>();
            for (var st = first; st <= s1 + 1e-9; st += stStep) marks.Add(st);
            foreach (var st in marks) grid.Segments.Add((new Vec2(X(st), Title), new Vec2(X(st), Title + gh)));
            axis.Segments.Add((new Vec2(Left, Title), new Vec2(Left, Title + gh)));
            axis.Segments.Add((new Vec2(Left, Title + gh), new Vec2(X(s1), Title + gh)));
            void Draw(Pen pen, IReadOnlyList<(double S, double Z)> pts)
            {
                for (var i = 0; i + 1 < pts.Count; i++)
                {
                    var (a, b) = (pts[i], pts[i + 1]);
                    if (b.S < s0 || a.S > s1 || b.S - a.S < 1e-12) continue;
                    double Z(double st) => a.Z + (b.Z - a.Z) * (st - a.S) / (b.S - a.S);
                    var (u, v) = (Math.Max(a.S, s0), Math.Min(b.S, s1));
                    pen.Segments.Add((new Vec2(X(u), Y(Z(u))), new Vec2(X(v), Y(Z(v)))));
                }
            }
            foreach (var run in Runs(existing)) Draw(ex1, run);
            foreach (var run in Runs(design)) Draw(de1, run);
            Draw(red, l.Laid);
            // The points, their stations and heights, and their vertical curves.
            foreach (var p in l.Points.Where(p => p.Station >= s0 - 1e-9 && p.Station <= s1 + 1e-9 && double.IsFinite(p.Z)))
            {
                var c = new Vec2(X(p.Station), Y(p.Z));
                pvi.Curves.Add(PenCurve.Circle(c, p.Designed ? 0.8 : 0.5));
                var label = $"{Chain.Label(p.Station + l.Start)}  {F(p.Z)}" + (p.Curve > 0 ? $"  L={F(p.Curve, "0.##")}" : "");
                T(c.X, c.Y - 2, label, 2, "middle");
                if (p.Curve > 0)
                {
                    pvi.Segments.Add((new Vec2(X(Math.Max(s0, p.Station - p.Curve / 2)), Title + gh + 1), new Vec2(X(Math.Min(s1, p.Station + p.Curve / 2)), Title + gh + 1)));
                }
            }
            // Grades between designed points.
            for (var i = 0; i + 1 < l.Points.Count; i++)
            {
                var (a, b) = (l.Points[i], l.Points[i + 1]);
                if (!a.Designed || !b.Designed || b.Station - a.Station < 1e-6 || !double.IsFinite(a.Z) || !double.IsFinite(b.Z)) continue;
                var mid = (a.Station + b.Station) / 2;
                if (mid < s0 || mid > s1) continue;
                var g = (b.Z - a.Z) / (b.Station - a.Station);
                T(X(mid), Y((a.Z + b.Z) / 2) + 3.5, $"{F(g * 100, "0.00")} %", 2, "middle");
            }
            // The band: stations, both grounds, the line, cut (−) or fill (+).
            string[] rows = ["Station", "Existing", "Design", "Fill / cut"];
            for (var r = 0; r <= rows.Length; r++) axis.Segments.Add((new Vec2(0, bandTop + r * Row), new Vec2(X(s1), bandTop + r * Row)));
            axis.Segments.Add((new Vec2(Left, bandTop), new Vec2(Left, bandTop + rows.Length * Row)));
            for (var r = 0; r < rows.Length; r++) T(1, bandTop + r * Row + 4, rows[r], 2.2);
            var at = marks.Concat(l.Points.Where(p => p.Designed).Select(p => p.Station)).Where(st => st >= s0 - 1e-9 && st <= s1 + 1e-9).Distinct().Order().ToList();
            var lastX = double.NegativeInfinity;
            foreach (var st in at)
            {
                var xx = X(st);
                if (xx - lastX < 9) continue;
                lastX = xx;
                grid.Segments.Add((new Vec2(xx, bandTop), new Vec2(xx, bandTop + rows.Length * Row)));
                var ez = At(existing, st);
                var lz = l.ZAt(st);
                T(xx, bandTop + 4, Chain.Label(st + l.Start), 1.8, "middle");
                if (ez is { } e1) T(xx, bandTop + Row + 4, F(e1), 1.8, "middle");
                if (lz is { } z1) T(xx, bandTop + 2 * Row + 4, F(z1), 1.8, "middle");
                if (ez is { } e2 && lz is { } z2) T(xx, bandTop + 3 * Row + 4, (z2 - e2 >= 0 ? "+" : "") + F(z2 - e2), 1.8, "middle");
            }
            o.Add((w, h, [grid, axis, ex1, de1, red, pvi, text]));
        }
        return o;
    }

    public static IReadOnlyList<(double W, double H, List<Pen> Pens)> Cross(IQueryContext q, Entity view, LineProfiles.Line l, double scale, double ex, double maxWidth, double? maxHeight)
    {
        var k = 1000 / scale;
        var kv = k * ex;
        var width = Math.Max(0.5, view.Num("width") is var wv && wv > 0 ? wv : 10);
        var stations = LineProfiles.ViewStations(l, view.Attrs);
        const double Left = 16, Pad = 6, Title = 6, Foot = 6;
        var sections = stations.Select(st => (St: st, J: LineProfiles.Cross(q.Model, l, st, width))).ToList();
        static List<(double S, double? Z)> Read(System.Text.Json.Nodes.JsonNode? n) =>
            [.. ((n as System.Text.Json.Nodes.JsonArray) ?? []).OfType<System.Text.Json.Nodes.JsonArray>().Select(p => (Json.Number(p[0]) ?? 0, Json.Number(p[1])))];
        var all = sections.SelectMany(x => Read(x.J["existing"]).Concat(Read(x.J["design"])).Concat(Read(x.J["line"]))).Select(p => p.Z).OfType<double>().ToList();
        var levelStep = Step(kv, 6, LevelSteps);
        // Each section's own height range, all as tall as the tallest.
        var tall = sections.Select(x =>
        {
            var z = Read(x.J["existing"]).Concat(Read(x.J["design"])).Concat(Read(x.J["line"])).Select(p => p.Z).OfType<double>().ToList();
            return z.Count == 0 ? levelStep : z.Max() - z.Min();
        }).DefaultIfEmpty(1).Max() + 2 * levelStep;
        var bw = Left + 2 * width * k + Pad;
        var bh = Title + tall * kv + Foot;
        var cols = Math.Max(1, (int)Math.Floor((maxWidth + Pad) / bw));
        var rowsPerPage = Math.Max(1, maxHeight is { } mh ? (int)Math.Floor((mh - 8) / bh) : 1000);
        var perPage = cols * rowsPerPage;
        var pages = new List<(double, double, List<Pen>)>();
        if (sections.Count == 0)
        {
            var tx = new Pen("TEXT", "#000000", 0, null, null);
            tx.Texts.Add(new PenText(new Vec2(0, 5), view.Name, 4, Anchor: "start", Bold: true));
            tx.Texts.Add(new PenText(new Vec2(0, 11), "No stations: give an interval or pick stations.", 2.5, Anchor: "start"));
            return [(120, 14, [tx])];
        }
        for (var start = 0; start < sections.Count; start += perPage)
        {
            var chunk = sections.Skip(start).Take(perPage).ToList();
            var text = new Pen("TEXT", "#000000", 0, null, null);
            var axis = new Pen("PROFILE-AXIS", "#000000", 0.25, null, null);
            var grid = new Pen("PROFILE-GRID", "#cccccc", 0.1, null, null);
            var ex1 = new Pen("PROFILE-EXISTING", ExistingColor, 0.25, null, [1.5, 1]);
            var de1 = new Pen("PROFILE-DESIGN", DesignColor, 0.25, null, null);
            var red = new Pen("PROFILE-LINE", LineColor, 0.5, null, null);
            var mark = new Pen("PROFILE-POINT", LineColor, 0.25, null, null);
            const double Head = 8;
            text.Texts.Add(new PenText(new Vec2(0, 5), $"{view.Name} · 1:{F(scale, "0")}" + (ex > 1 ? $" · heights {F(ex, "0.#")}×" : ""), 3, Anchor: "start", Bold: true));
            for (var i = 0; i < chunk.Count; i++)
            {
                var (st, j) = chunk[i];
                var (cx, cy) = (i % cols * bw, Head + i / cols * bh);
                var ex0 = Read(j["existing"]);
                var de0 = Read(j["design"]);
                var ln = Read(j["line"]);
                var z = ex0.Concat(de0).Concat(ln).Select(p => p.Z).OfType<double>().ToList();
                var lz = Json.Number(j["z"]);
                if (lz is { } zz) z.Add(zz);
                if (z.Count == 0) z = [0];
                var datum = Math.Floor((z.Min() - levelStep) / levelStep) * levelStep;
                var gh = tall * kv;
                double X(double off) => cx + Left + (off + width) * k;
                double Y(double h) => cy + Title + gh - (h - datum) * kv;
                text.Texts.Add(new PenText(new Vec2(X(0), cy + 4), Chain.Label(st), 2.5, Anchor: "middle", Bold: true));
                for (var h = datum; h <= datum + tall + 1e-9; h += levelStep)
                {
                    grid.Segments.Add((new Vec2(X(-width), Y(h)), new Vec2(X(width), Y(h))));
                    text.Texts.Add(new PenText(new Vec2(X(-width) - 1, Y(h) + 0.7), F(h, levelStep < 1 ? "0.00" : "0.0"), 1.8, Anchor: "end"));
                }
                axis.Segments.Add((new Vec2(X(-width), Y(datum)), new Vec2(X(width), Y(datum))));
                axis.Segments.Add((new Vec2(X(0), Y(datum)), new Vec2(X(0), Y(datum + tall))));
                foreach (var off in new[] { -width, 0, width })
                    text.Texts.Add(new PenText(new Vec2(X(off), Y(datum) + 3.2), (off > 0 ? "+" : "") + F(off, "0.0"), 1.8, Anchor: "middle"));
                void Draw(Pen pen, List<(double S, double Z)> pts)
                {
                    for (var a = 0; a + 1 < pts.Count; a++) pen.Segments.Add((new Vec2(X(pts[a].S), Y(pts[a].Z)), new Vec2(X(pts[a + 1].S), Y(pts[a + 1].Z))));
                }
                foreach (var run in Runs(ex0)) Draw(ex1, run);
                foreach (var run in Runs(de0)) Draw(de1, run);
                foreach (var run in Runs(ln)) Draw(red, run);
                if (lz is { } at) mark.Curves.Add(PenCurve.Circle(new Vec2(X(0), Y(at)), 0.8));
            }
            var rows = (chunk.Count + cols - 1) / cols;
            pages.Add((Math.Min(cols, chunk.Count) * bw, Head + rows * bh, [grid, axis, ex1, de1, red, mark, text]));
        }
        return pages;
    }
}
