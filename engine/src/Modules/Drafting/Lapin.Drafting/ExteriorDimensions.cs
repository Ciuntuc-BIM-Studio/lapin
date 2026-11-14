using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A building's exterior dimensions on a plan, made at once (forum topic 78), beside the rooms' (decision 249). The plan's
/// cut finds the facades: a face of a wall is outside when, a hair in front of it, a ray escapes the building (the
/// openings' boxes close the walls' gaps, so a ray never leaves a room through a window); the outside faces in line one
/// after another, their gaps an opening's, are one facade. Each facade gets parallel strings outside it, in the order the
/// user's rules give (<see cref="DimensionRules"/>), from the wall outward:
/// <list type="bullet">
/// <item>details: the facade's corners and every opening's jambs;</item>
/// <item>openings: from the facade's start to each opening's centre, to its end (the object spacing);</item>
/// <item>axes: from the start to every grid axis that crosses the facade, to the end;</item>
/// <item>rooms: the thicknesses of the walls that meet the facade from inside and the distances between them;</item>
/// <item>overall: the whole facade.</item>
/// </list>
/// Every point holds on what it measures (a wall's face or axis, an opening's place in its wall, a grid axis), so the
/// strings follow the model as any dimension does. A facade dimensioned again has its strings made anew; the user's own
/// dimensions stay.
/// </summary>
public static class ExteriorDimensions
{
    /// <summary>The attribute that says which string of a facade a dimension is (<see cref="DimensionRules.ExteriorKinds"/>).</summary>
    public const string Key = "exteriorString";
    /// <summary>The attribute listing the walls of the facade a string dimensions.</summary>
    public const string WallsKey = "facadeWalls";

    /// <summary>The shortest face (m) a facade is made of; the shortest facade dimensioned.</summary>
    const double MinEdge = 0.05, MinRun = 0.5;
    /// <summary>Points this close (m) along a string are one.</summary>
    const double Same = 1e-3;

    /// <summary>One facade: its line (points <c>O + U·s</c>, outside towards <c>N</c>), from <c>S0</c> to <c>S1</c>, its walls and how deep they are.</summary>
    public sealed record Facade(Vec2 O, Vec2 U, Vec2 N, double S0, double S1, HashSet<string> Walls, double Depth)
    {
        public Vec2 At(double s) => O + U * s;
        public double C => Vec2.Dot(O, N);
    }

    sealed record Seg(Vec2 A, Vec2 B, string Element, string? Key, object Loop);

    static bool Blocks(string cls) => cls is IfcClass.Wall or IfcClass.Column;

    /// <summary>
    /// Whether a part of an element's cut makes its faces: any part, or measured to the core its core's (an element drawn
    /// without its core, or with no layers: every part).
    /// </summary>
    static bool Measures(IModelView m, ViewDrawing d, DPolygon p, string measureTo, Dictionary<string, string?> cores)
    {
        if (measureTo != "core") return true;
        if (!cores.TryGetValue(p.Element, out var key))
        {
            key = LayerSets.BandsOf(m, p.Element)?.Where(b => b.Core).Select(b => $"layer:{b.Index}").FirstOrDefault();
            if (key != null && !d.Cuts.Any(q => q.Element == p.Element && q.Key == key)) key = null;
            cores[p.Element] = key;
        }
        return key == null || p.Key == key;
    }

    /// <summary>The facades on <paramref name="d"/> (a plan): of <paramref name="wallIds"/>' faces, or every wall's.</summary>
    public static List<Facade> Facades(IModelView m, ViewDrawing d, IReadOnlyList<WallOpening> openings, string measureTo, IReadOnlyCollection<string>? wallIds)
    {
        var cores = new Dictionary<string, string?>();
        bool Measured(DPolygon p) => Measures(m, d, p, measureTo, cores);

        var segs = new List<Seg>();
        foreach (var poly in d.Cuts.Where(p => Blocks(p.Class)))
            foreach (var l in poly.Loops)
                for (var i = 0; i < l.Length; i++) segs.Add(new Seg(l[i], l[(i + 1) % l.Length], poly.Element, poly.Key, l));
        // The openings' boxes in plan, closing the walls' gaps.
        var boxes = openings.Select(o => (O: o, Box: Box(o))).Where(b => b.Box.Count == 4).ToList();
        foreach (var (o, box) in boxes)
            for (var i = 0; i < 4; i++) segs.Add(new Seg(box[i], box[(i + 1) % 4], o.OpeningId, null, box));

        bool Escapes(Vec2 p, Vec2 dir, Seg self)
        {
            foreach (var s in segs)
            {
                if (ReferenceEquals(s.Loop, self.Loop)) continue;
                // Measured to the core: the wall's own finishes do not hide it.
                if (measureTo == "core" && s.Element == self.Element && s.Key != self.Key) continue;
                if (Ray(p, dir, s.A, s.B)) return false;
            }
            return true;
        }

        var outside = new List<(Seg S, Vec2 N)>();
        foreach (var poly in d.Cuts.Where(p => p.Class == IfcClass.Wall && (wallIds == null || wallIds.Contains(p.Element)) && Measured(p)))
            foreach (var l in poly.Loops)
            {
                var ccw = Polygon2.SignedArea(l) > 0;
                for (var i = 0; i < l.Length; i++)
                {
                    var (a, b) = (l[i], l[(i + 1) % l.Length]);
                    var e = b - a;
                    if (e.Length < MinEdge) continue;
                    var u = e.Normalized();
                    var n = ccw ? new Vec2(u.Y, -u.X) : new Vec2(-u.Y, u.X);
                    var self = new Seg(a, b, poly.Element, poly.Key, l);
                    var votes = 0;
                    foreach (var t in new[] { 0.25, 0.5, 0.75 })
                    {
                        var p = a + e * t + n * 1e-4;
                        if (new[] { 0.0, 35, -35, 70, -70 }.Any(deg => Escapes(p, Turn(n, deg), self))) votes++;
                    }
                    if (votes >= 2) outside.Add((self, n));
                }
            }

        // Faces in line, outside the same way, one after another (a gap an opening's): one facade.
        var o2 = new List<Facade>();
        var groups = new List<List<(Seg S, Vec2 N)>>();
        foreach (var f in outside)
        {
            var g = groups.FirstOrDefault(g => Vec2.Dot(g[0].N, f.N) > 0.9999 && Math.Abs(Vec2.Dot(f.S.A - g[0].S.A, g[0].N)) < 0.002);
            if (g == null) groups.Add([f]); else g.Add(f);
        }
        foreach (var g in groups)
        {
            var n = g[0].N;
            var u = new Vec2(-n.Y, n.X);
            var c = Vec2.Dot(g[0].S.A, n);
            var spans = g.Select(f => { var (sa, sb) = (Vec2.Dot(f.S.A, u), Vec2.Dot(f.S.B, u)); return (Lo: Math.Min(sa, sb), Hi: Math.Max(sa, sb), f.S.Element); }).OrderBy(x => x.Lo).ToList();
            bool Bridged(double lo, double hi) => boxes.Any(b =>
            {
                var ss = b.Box.Select(q => Vec2.Dot(q, u)).ToList();
                var ns = b.Box.Select(q => Vec2.Dot(q, n)).ToList();
                return ss.Min() <= lo + 0.05 && ss.Max() >= hi - 0.05 && ns.Max() >= c - 0.05 && ns.Min() <= c + 0.05;
            });
            var runs = new List<(double Lo, double Hi, HashSet<string> Walls)>();
            foreach (var sp in spans)
            {
                if (runs.Count > 0 && (sp.Lo <= runs[^1].Hi + 0.002 || Bridged(runs[^1].Hi, sp.Lo)))
                {
                    runs[^1] = (runs[^1].Lo, Math.Max(runs[^1].Hi, sp.Hi), runs[^1].Walls);
                    runs[^1].Walls.Add(sp.Element);
                }
                else runs.Add((sp.Lo, sp.Hi, [sp.Element]));
            }
            foreach (var (lo, hi, walls) in runs)
            {
                if (hi - lo < MinRun) continue;
                // How deep the facade's walls are: the farthest face of theirs along it, inside.
                var depth = 0.0;
                foreach (var poly in d.Cuts.Where(p => walls.Contains(p.Element) && Measured(p)))
                    foreach (var l in poly.Loops)
                        for (var i = 0; i < l.Length; i++)
                        {
                            var (a, b) = (l[i], l[(i + 1) % l.Length]);
                            if ((b - a).Length < 0.1 || Math.Abs(Vec2.Cross((b - a).Normalized(), u)) > 0.01) continue;
                            depth = Math.Max(depth, c - Vec2.Dot(a, n));
                        }
                o2.Add(new Facade(n * c, u, n, lo, hi, walls, depth));
            }
        }
        return o2;
    }

    static Vec2 Turn(Vec2 v, double deg)
    {
        var r = deg * Math.PI / 180;
        return new Vec2(v.X * Math.Cos(r) - v.Y * Math.Sin(r), v.X * Math.Sin(r) + v.Y * Math.Cos(r));
    }

    /// <summary>Whether a ray from <paramref name="p"/> along <paramref name="dir"/> meets the segment.</summary>
    static bool Ray(Vec2 p, Vec2 dir, Vec2 a, Vec2 b)
    {
        var e = b - a;
        var den = Vec2.Cross(dir, e);
        if (Math.Abs(den) < 1e-12) return false;
        var t = Vec2.Cross(a - p, e) / den;
        var s = Vec2.Cross(a - p, dir) / den;
        return t > 1e-9 && s >= -1e-9 && s <= 1 + 1e-9;
    }

    /// <summary>An opening's box in plan: its width along the wall by the wall's thickness (empty when it has none).</summary>
    static List<Vec2> Box(WallOpening o)
    {
        var w = new Vec2(o.Right.X - o.Left.X, o.Right.Y - o.Left.Y);
        if (w.Length < 1e-6 || o.Corners.Count == 0) return [];
        var u = w.Normalized();
        var n = u.Perp;
        var pts = o.Corners.Select(q => new Vec2(q.X, q.Y)).ToList();
        var (t0, t1) = (pts.Min(q => Vec2.Dot(q, u)), pts.Max(q => Vec2.Dot(q, u)));
        var (n0, n1) = (pts.Min(q => Vec2.Dot(q, n)), pts.Max(q => Vec2.Dot(q, n)));
        if (n1 - n0 < 1e-6) return [];
        return [u * t0 + n * n0, u * t1 + n * n0, u * t1 + n * n1, u * t0 + n * n1];
    }

    /// <summary>A wall (or column) the facade's inside meets: its faces along the facade (with their directions), its axis there.</summary>
    sealed record Crosser(string Element, double Lo, Vec2 LoDir, double Hi, Vec2 HiDir, double? Axis);

    /// <summary>What meets the facade from inside: crossing a line just inside its walls' inner faces, in order along it.</summary>
    static List<Crosser> Crossers(IModelView m, ViewDrawing d, Facade f, string measureTo)
    {
        var probe = f.C - f.Depth - 0.02;
        var hits = new Dictionary<string, List<(double S, Vec2 Dir)>>();
        var cores = new Dictionary<string, string?>();
        foreach (var poly in d.Cuts.Where(p => Blocks(p.Class) && !f.Walls.Contains(p.Element)))
        {
            if (!Measures(m, d, poly, measureTo, cores)) continue;
            foreach (var l in poly.Loops)
                for (var i = 0; i < l.Length; i++)
                {
                    var (a, b) = (l[i], l[(i + 1) % l.Length]);
                    var (da, db) = (Vec2.Dot(a, f.N) - probe, Vec2.Dot(b, f.N) - probe);
                    if (da * db > 0 || Math.Abs(da - db) < 1e-12) continue;
                    var x = a + (b - a) * (da / (da - db));
                    (hits.TryGetValue(poly.Element, out var h) ? h : hits[poly.Element] = []).Add((Vec2.Dot(x, f.U), (b - a).Normalized()));
                }
        }
        var o = new List<Crosser>();
        foreach (var (el, h) in hits)
        {
            var (lo, hi) = (h.MinBy(x => x.S), h.MaxBy(x => x.S));
            if (hi.S < f.S0 - 0.01 || lo.S > f.S1 + 0.01 || hi.S - lo.S < 1e-3) continue;
            double? axis = null;
            if (m.Get(el) is { } e && Dimensions.Frame(e) is { } fr && e.Attrs["end"] is JsonArray)
            {
                var den = Vec2.Dot(fr.U, f.N);
                if (Math.Abs(den) > 0.1) axis = Vec2.Dot(fr.O + fr.U * ((f.C - Vec2.Dot(fr.O, f.N)) / den), f.U);
            }
            o.Add(new Crosser(el, lo.S, lo.Dir, hi.S, hi.Dir, axis));
        }
        return o.OrderBy(c => c.Lo).ToList();
    }

    /// <summary>A string's points as they are gathered: where along the facade, the point, what it holds on, and which wins when two fall together (lower first).</summary>
    sealed class Points(Facade f)
    {
        readonly List<(double S, Vec2 P, DimEndRef? Ref, int Rank)> pts = [];
        public void Add(double s, Vec2 p, DimEndRef? r, int rank = 1)
        {
            if (s < f.S0 - Same || s > f.S1 + Same) return;
            pts.Add((s, p, r, rank));
        }
        public List<(Vec2 P, DimEndRef? Ref)> Done()
        {
            var o = new List<(double S, Vec2 P, DimEndRef? Ref, int Rank)>();
            foreach (var x in pts.OrderBy(x => x.S).ThenBy(x => x.Rank))
            {
                if (o.Count > 0 && x.S - o[^1].S < Same) { if (x.Rank < o[^1].Rank) o[^1] = x; continue; }
                o.Add(x);
            }
            return o.Select(x => (x.P, x.Ref)).ToList();
        }
    }

    /// <summary>The facades' strings on the plan; how many facades and strings. The strings this made before for those walls go first.</summary>
    public static (int Facades, int Strings) Add(ICommandContext ctx, Entity view, IReadOnlyCollection<string>? wallIds, DimensionRules rules, ViewDrawing? drawing, double tolerance)
    {
        var m = ctx.Model;
        foreach (var old in m.OfClass(IfcClass.Annotation).Where(a => a.PredefinedType == "DIMENSION" && a.Str("viewId") == view.Id && a.Str(Key) != null
                     && (wallIds == null || (a.Attrs[WallsKey] as JsonArray ?? []).Any(w => wallIds.Contains((string?)w ?? "")))).ToList())
            ctx.Remove(old.Id);
        if (drawing == null) return (0, 0);
        var storey = view.Str("storeyId");
        var openings = ctx.Services.All<IWallOpenings>().SelectMany(s => s.Openings(m)).Where(o => storey == null || o.StoreyId == null || o.StoreyId == storey).ToList();
        var facades = Facades(m, drawing, openings, rules.MeasureTo, wallIds);
        var scale = view.NumOrNull("scale") ?? 100;
        var grids = m.OfClass(IfcClass.Grid).SelectMany(g => Grids.Axes(g).Select(ax => (Grid: g.Id, Ax: ax))).ToList();
        var (made, count) = (0, 0);
        foreach (var f in facades)
        {
            var crossers = rules.Has("rooms") || rules.MeasureTo == "axes" ? Crossers(m, drawing, f, rules.MeasureTo) : [];
            (Vec2 P, DimEndRef? Ref) Corner(double s) { var p = f.At(s); return (p, Dimensions.Associate(m, "plan", drawing, p, tolerance, f.U)); }
            (Vec2 P, DimEndRef? Ref) End(bool first)
            {
                var s = first ? f.S0 : f.S1;
                if (rules.MeasureTo == "axes")
                {
                    // The wall closing the facade at that end, by its axis.
                    var c = first ? crossers.FirstOrDefault() : crossers.LastOrDefault();
                    if (c is { Axis: { } ax } && (first ? c.Lo <= f.S0 + 0.01 : c.Hi >= f.S1 - 0.01) && Dimensions.HoldOnAxis(m, c.Element, f.At(ax)) is { } r) return (f.At(ax), r);
                }
                return Corner(s);
            }
            void Ends(Points pts, bool corners)
            {
                var (a, b) = corners ? (Corner(f.S0), Corner(f.S1)) : (End(true), End(false));
                pts.Add(Vec2.Dot(a.P, f.U), a.P, a.Ref, 0);
                pts.Add(Vec2.Dot(b.P, f.U), b.P, b.Ref, 0);
            }
            var mine = openings.Where(o => f.Walls.Contains(o.WallId) && o.Width >= rules.MinOpening - 1e-9).Select(o =>
            {
                var (l, r) = (new Vec2(o.Left.X, o.Left.Y), new Vec2(o.Right.X, o.Right.Y));
                return (O: o, Sl: Vec2.Dot(l, f.U), Sr: Vec2.Dot(r, f.U));
            }).Where(x => (x.Sl + x.Sr) / 2 > f.S0 && (x.Sl + x.Sr) / 2 < f.S1).ToList();

            var strings = new List<(string Kind, List<(Vec2 P, DimEndRef? Ref)> Pts)>();
            foreach (var kind in rules.Order)
            {
                var pts = new Points(f);
                switch (kind)
                {
                    case "details":
                        Ends(pts, true);
                        foreach (var (o, sl, sr) in mine)
                            foreach (var s in new[] { sl, sr })
                                pts.Add(s, f.At(s), Dimensions.HoldOnOpening(m, o.OpeningId, f.At(s), f.N));
                        break;
                    case "openings":
                        Ends(pts, false);
                        foreach (var (o, sl, sr) in mine)
                        {
                            var p = f.At((sl + sr) / 2);
                            pts.Add((sl + sr) / 2, p, Dimensions.HoldOnOpening(m, o.OpeningId, p));
                        }
                        break;
                    case "axes":
                        Ends(pts, false);
                        foreach (var (grid, ax) in grids)
                        {
                            var den = Vec2.Dot(ax.B - ax.A, f.N);
                            if ((ax.B - ax.A).Length < 1e-9 || Math.Abs(den) / (ax.B - ax.A).Length < 0.1) continue;
                            var p = ax.A + (ax.B - ax.A) * ((f.C - Vec2.Dot(ax.A, f.N)) / den);
                            pts.Add(Vec2.Dot(p, f.U), p, Dimensions.HoldOnGrid(grid, ax, p), 0);
                        }
                        break;
                    case "rooms":
                        Ends(pts, false);
                        foreach (var c in crossers)
                        {
                            if (rules.MeasureTo == "axes" && c.Axis is { } ax) { pts.Add(ax, f.At(ax), Dimensions.HoldOnAxis(m, c.Element, f.At(ax))); continue; }
                            pts.Add(c.Lo, f.At(c.Lo), Dimensions.HoldOn(m, "plan", drawing, c.Element, f.At(c.Lo), c.LoDir));
                            pts.Add(c.Hi, f.At(c.Hi), Dimensions.HoldOn(m, "plan", drawing, c.Element, f.At(c.Hi), c.HiDir));
                        }
                        break;
                    case "overall":
                        Ends(pts, false);
                        break;
                }
                var done = pts.Done();
                // A string that says no more than the whole facade is left out (the overall string says it).
                if (done.Count < 2 || (kind != "overall" && done.Count < 3)) continue;
                strings.Add((kind, done));
            }
            for (var k = 0; k < strings.Count; k++)
            {
                var (kind, pts) = strings[k];
                var at = f.At((f.S0 + f.S1) / 2) + f.N * ((rules.Offset + k * rules.Spacing) * scale / 1000);
                var attrs = new JsonObject
                {
                    ["viewId"] = view.Id, ["at"] = new JsonArray(at.X, at.Y), [Key] = kind,
                    [WallsKey] = new JsonArray(f.Walls.Order(StringComparer.Ordinal).Select(w => (JsonNode)JsonValue.Create(w)!).ToArray()),
                };
                if (rules.DimStyle != null) attrs["dimStyle"] = rules.DimStyle;
                Dimensions.Write(attrs, pts);
                ctx.Add(new Entity(ctx.NewId(), IfcClass.Annotation, "Exterior dimension", attrs, "DIMENSION"));
            }
            if (strings.Count > 0) made++;
            count += strings.Count;
        }
        return (made, count);
    }
}
