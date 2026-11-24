using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>
/// A received open-web joist made native (docs/plan-steel.md, Grinzile cu zăbrele; decision 827): a beam or member whose names
/// say it is a joist (a family or type "K-Series Bar Joist", "Joist", "Open web", a type named as a joist series is, "16K2",
/// "18LH04", "24KCS3") and whose body is two pairs of chords, each pair two straight angles back to back (Revit's bar joists:
/// their webs only drawn, as lines beside the chords). Read from the body: its axis, depth, the chords' angles and gap, the
/// top chord's drop under the bearing, the bottom chord's setback, the seats; its webs are the lines its program drew with it,
/// kept as they are (no pattern is fitted over them), so the native joist's webs are lines too where the file's are. Nothing is
/// built from what the body does not show; the fidelity gate then measures the native body against the received one, as any
/// recognised element's, and what differs comes back the reference it was.
/// </summary>
public sealed class JoistRecognizer : IBodyRecognizer
{
    /// <summary>A joist series' type name: depth (inches), series, size; Revit's and the joist makers' (16K2, 18LH04, 24KCS3, 52DLH13).</summary>
    static readonly Regex Series = new(@"^\s*\d{1,3}\s*(K|KCS|LH|DLH|SLH)\s*\d{1,3}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly string[] Words = ["joist", "open web", "open-web", "k-series", "bar joist"];

    public bool Takes(string ifcClass) => ifcClass is IfcClass.Beam or "IfcMember";

    public bool Takes(ForeignBody body)
    {
        IEnumerable<string?> Names()
        {
            yield return body.Name;
            yield return body.TypeName;
            yield return body.ObjectType;
            if (body.Properties["Speckle"] is JsonObject sp)
                foreach (var k in new[] { "family", "type" }) if (sp[k] is JsonValue v && v.TryGetValue<string>(out var s)) yield return s;
        }
        return Names().OfType<string>().Any(n => Series.IsMatch(n) || Words.Any(w => n.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    const double Tol = 5e-4;

    public string? Recognize(IEdit edit, ForeignBody body, RecognitionContext ctx)
    {
        string? No(string why) { ctx.Skip("a joist " + why); return null; }
        var storey = edit.Model.Get(body.StoreyId);
        if (storey == null) return No("without a storey");
        // The triangles welded (0.01 mm) and split into the pieces they make.
        var index = new Dictionary<(long, long, long), int>();
        var pts = new List<Vec3>();
        var tris = new List<(int, int, int)>();
        foreach (var s in body.Solids)
        {
            var map = s.Vertices.Select(v =>
            {
                var k = ((long)Math.Round(v.X * 1e5), (long)Math.Round(v.Y * 1e5), (long)Math.Round(v.Z * 1e5));
                if (!index.TryGetValue(k, out var i)) { index[k] = i = pts.Count; pts.Add(v); }
                return i;
            }).ToArray();
            tris.AddRange(Measure.Triangles(s).Select(t => (map[t.A], map[t.B], map[t.C])));
        }
        if (pts.Count < 8) return No("with no body");
        var parent = Enumerable.Range(0, pts.Count).ToArray();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        foreach (var (a, b, c) in tris) { parent[Find(b)] = Find(a); parent[Find(c)] = Find(a); }
        var used = tris.SelectMany(t => new[] { t.Item1, t.Item2, t.Item3 }).ToHashSet();
        var pieces = used.GroupBy(Find).Select(g => g.ToList()).ToList();
        if (pieces.Count != 4) return No($"whose body is not two pairs of chords ({pieces.Count} pieces: its webs solids, or plates and bridging with it)");

        // Its axis: along the longer side of the plan's least box.
        var hull = Polygon2.ConvexHull(used.Select(i => pts[i].Xy));
        if (hull.Length < 3) return No("with no extent in plan");
        var (along, best) = (new Vec2(1, 0), double.MaxValue);
        for (var i = 0; i < hull.Length; i++)
        {
            var e = hull[(i + 1) % hull.Length] - hull[i];
            if (e.Length < 1e-9) continue;
            var d = e.Normalized();
            var n = new Vec2(-d.Y, d.X);
            var (l, w) = (hull.Max(p => Vec2.Dot(p, d)) - hull.Min(p => Vec2.Dot(p, d)), hull.Max(p => Vec2.Dot(p, n)) - hull.Min(p => Vec2.Dot(p, n)));
            if (l * w < best - 1e-12) (best, along) = (l * w, l >= w ? d : n);
        }
        // One way for every joist: east, else north.
        if (along.X < -1e-9 || Math.Abs(along.X) <= 1e-9 && along.Y < 0) along = along * -1;
        var across = new Vec2(-along.Y, along.X);
        double U(Vec3 p) => Vec2.Dot(p.Xy, along);
        double V(Vec3 p) => Vec2.Dot(p.Xy, across);
        var all = used.Select(i => pts[i]).ToList();
        var (u0, u1) = (all.Min(U), all.Max(U));
        var vc = (all.Min(V) + all.Max(V)) / 2;
        var top = all.Max(p => p.Z);
        var bottom = all.Min(p => p.Z);
        var length = u1 - u0;
        if (length < 0.5 || top - bottom > length) return No("shorter than it is deep");

        // The pieces: two over the middle height (the top chord's angles), two under it, one each side.
        var mid = (top + bottom) / 2;
        var sorted = pieces.Select(p => (Pts: p.Select(i => pts[i]).ToList(), Upper: p.Average(i => pts[i].Z) > mid, Left: p.Average(i => V(pts[i])) > vc)).ToList();
        if (sorted.Count(x => x.Upper) != 2 || sorted.Count(x => x.Upper && x.Left) != 1 || sorted.Count(x => !x.Upper && x.Left) != 1) return No("whose pieces are not a pair of chords over a pair");

        // Each angle's section, where it is a plain angle (the station nearest its middle), and the same at every station between its
        // ends (a straight angle); in (w, z): w out from the joist's middle, z down from its top.
        (double U0, double U1, double Gap, JoistChord Chord, double ChordTop, List<(double U, List<Vec2> Section)> Stations)? Angle(List<Vec3> p)
        {
            var side = p.Average(V) > vc ? 1 : -1;
            var stations = new List<(double U, List<Vec2> Section)>();
            foreach (var q in p.OrderBy(U))
            {
                var w = new Vec2(side * (V(q) - vc), q.Z - top);
                if (stations.Count > 0 && Math.Abs(stations[^1].U - U(q)) < Tol) stations[^1].Section.Add(w);
                else stations.Add((U(q), [w]));
            }
            foreach (var st in stations) { var d = st.Section.Distinct(new Near()).ToList(); st.Section.Clear(); st.Section.AddRange(d); }
            if (stations.Count < 2) return null;
            var (a0, a1) = (stations[0].U, stations[^1].U);
            var plain = stations.OrderBy(st => Math.Abs(st.U - (a0 + a1) / 2)).First().Section;
            if (Read(plain) is not { } read) return null;
            return (a0, a1, read.Gap, read.Chord, read.Top, stations);
        }
        var angles = sorted.Select(x => (x.Upper, x.Left, Read: Angle(x.Pts))).ToList();
        if (angles.Any(a => a.Read == null)) return No("whose chords are not angles (each a pair of plain angles back to back)");
        var tops = angles.Where(a => a.Upper).Select(a => a.Read!.Value).ToList();
        var bottoms = angles.Where(a => !a.Upper).Select(a => a.Read!.Value).ToList();
        bool Same(JoistChord a, JoistChord b) => a.Flange == b.Flange && Math.Abs(a.Leg - b.Leg) < Tol && Math.Abs(a.Height - b.Height) < Tol && Math.Abs(a.Thickness - b.Thickness) < Tol;
        foreach (var pair in new[] { tops, bottoms })
            if (!Same(pair[0].Chord, pair[1].Chord) || Math.Abs(pair[0].ChordTop - pair[1].ChordTop) > Tol || Math.Abs(pair[0].U0 - pair[1].U0) > Tol || Math.Abs(pair[0].U1 - pair[1].U1) > Tol)
                return No("whose chords' two angles are not each other's mirror");
        var gap = tops[0].Gap;
        if (angles.Any(a => Math.Abs(a.Read!.Value.Gap - gap) > Tol)) return No("whose chords' angles are not as far apart (top and bottom)");
        var (topChord, bottomChord) = (tops[0].Chord, bottoms[0].Chord);
        if (Math.Abs(tops[0].U0 - u0) > Tol || Math.Abs(tops[0].U1 - u1) > Tol) return No("whose top chord does not run its whole length");
        var setback = bottoms[0].U0 - u0;
        if (Math.Abs(u1 - bottoms[0].U1 - setback) > 1e-3) return No("whose bottom chord is not centred on it");
        var depth = top - bottom;
        if (Math.Abs(bottoms[0].ChordTop - (-depth + bottomChord.Height)) > Tol) return No("whose bottom chord is not at its bottom");
        // Straight angles: every station between the bottom chord's ends the plain section; the top chord's too, but its ends' (the seats).
        foreach (var (read, isTop) in new[] { (bottoms[0], false), (bottoms[1], false), (tops[0], true), (tops[1], true) })
        {
            var inner = isTop ? read.Stations.Skip(1).SkipLast(1) : read.Stations;
            var plain = Out(read.Chord.Outline(read.ChordTop), read.Gap / 2);
            if (inner.Any(st => !SameSection(st.Section, plain))) return No("whose chords are not straight (an angle bent, notched or of another section along it)");
        }
        // The seats: at each end of the top chord, what stands above it (to the bearing) and under it; their length to the next station.
        var drop = -tops[0].ChordTop;
        double seatDepth = 0, seatLength = 0;
        var ends = new[] { tops[0].Stations[0], tops[0].Stations[^1] };
        var plainTop = Out(topChord.Outline(tops[0].ChordTop), gap / 2);
        if (ends.Any(st => !SameSection(st.Section, plainTop)))
        {
            seatDepth = -ends.SelectMany(st => st.Section).Min(q => q.Y);
            if (ends.Any(st => st.Section.Max(q => q.Y) < -Tol)) return No("whose seats do not reach its bearing");
            var st = tops[0].Stations;
            seatLength = st.Count > 2 ? Math.Min(st[1].U - st[0].U, st[^1].U - st[^2].U) : 0;
            if (st.Count > 2 && Math.Abs(st[1].U - st[0].U - (st[^1].U - st[^2].U)) > 1e-3) return No("whose seats are not as long at both ends");
        }
        var start = new Vec3(along.X * u0 + across.X * vc, along.Y * u0 + across.Y * vc, top);
        var end = start + new Vec3(along.X, along.Y, 0) * length;
        // The webs its file drew, along it and down from its top: the lines in its plane, within its length and depth.
        var webs = new List<(double U0, double Z0, double U1, double Z1)>();
        var reach = gap / 2 + Math.Max(topChord.Leg, bottomChord.Leg);
        foreach (var (a, b) in body.Lines)
        {
            if (Math.Abs(V(a) - vc) > reach || Math.Abs(V(b) - vc) > reach) continue;
            if (Math.Min(U(a), U(b)) < u0 - Tol || Math.Max(U(a), U(b)) > u1 + Tol) continue;
            if (Math.Min(a.Z, b.Z) < bottom - Tol || Math.Max(a.Z, b.Z) > top + Tol) continue;
            if ((b - a).Length < 1e-4) continue;
            webs.Add((U(a) - u0, a.Z - top, U(b) - u0, b.Z - top));
        }
        // Its webs the file's lines: the type's panel and web size say nothing of them (its own pattern's, should its webs be reset).
        var basis = JoistSpec.Presets[JoistSpec.DefaultPreset];
        var spec = new JoistSpec(Round(depth), topChord, bottomChord, Round(gap), Round(drop), Round(setback), Round(seatDepth), Round(seatLength), "lines", basis.WebSize, basis.WebThickness,
            basis.Panel, false);
        if (spec.Problem() is { } problem) return No($"of rules no joist has ({problem})");
        var type = TypeFor(edit, ctx, body, spec);
        var id = OpenWebJoists.Add(edit, storey, type, start, end, body.GlobalId, webs);
        if (body.Name is { Length: > 0 } name) edit.Replace(edit.Model.Get(id)!.With(name: name));
        ctx.Made.Add(id);
        return id;
    }

    /// <summary>A plain angle's section read from its corners (w out from the joist's middle, z down from its top): six corners, its web against the gap, its flange out.</summary>
    static (double Gap, JoistChord Chord, double Top)? Read(List<Vec2> section)
    {
        if (section.Count != 6) return null;
        var (w0, w1, z0, z1) = (section.Min(q => q.X), section.Max(q => q.X), section.Min(q => q.Y), section.Max(q => q.Y));
        bool Has(double w, double z) => section.Any(q => Math.Abs(q.X - w) < Tol && Math.Abs(q.Y - z) < Tol);
        if (!Has(w0, z0) || !Has(w0, z1)) return null;
        var flange = !Has(w1, z1) && Has(w1, z0) ? "bottom" : !Has(w1, z0) && Has(w1, z1) ? "top" : null;
        if (flange == null) return null;
        var inner = section.Where(q => q.X > w0 + Tol && q.X < w1 - Tol && q.Y > z0 + Tol && q.Y < z1 - Tol).ToList();
        if (inner.Count != 1) return null;
        var tWeb = inner[0].X - w0;
        var tFlange = flange == "bottom" ? inner[0].Y - z0 : z1 - inner[0].Y;
        if (Math.Abs(tWeb - tFlange) > Tol || tWeb <= 0) return null;
        var chord = new JoistChord(Round(w1 - w0), Round(z1 - z0), Round((tWeb + tFlange) / 2), flange);
        // Its outline as the joist would draw it must be these corners.
        if (!SameSection(section, Out(chord.Outline(z1), w0))) return null;
        return (Round(2 * w0), chord, z1);
    }

    /// <summary>An angle's outline (w from the gap's face) moved out to stand <paramref name="by"/> from the joist's middle.</summary>
    static List<Vec2> Out(IReadOnlyList<Vec2> outline, double by) => [.. outline.Select(q => new Vec2(q.X + by, q.Y))];

    static bool SameSection(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b) =>
        a.Count == b.Count && a.All(p => b.Any(q => (p - q).Length < 1e-3)) && b.All(q => a.Any(p => (p - q).Length < 1e-3));

    sealed class Near : IEqualityComparer<Vec2>
    {
        public bool Equals(Vec2 a, Vec2 b) => (a - b).Length < Tol;
        public int GetHashCode(Vec2 v) => 0;
    }

    static double Round(double v) => Math.Round(v, 5);

    /// <summary>
    /// The joist type for these rules: the file type's (named after it) while its rules are these, else one of its own for them
    /// ("16K2 (2)"); without a file type, one by its depth. Kept for the import, so its joists of one type share one.
    /// </summary>
    static Entity TypeFor(IEdit edit, RecognitionContext ctx, ForeignBody body, JoistSpec spec)
    {
        var basis = body.TypeName is { Length: > 0 } tn ? tn : $"Joist {spec.Depth * 1000:0}";
        var rules = spec.ToJson();
        // The same rules to 0.5 mm (a reference's corners are kept to 0.1 mm): its joists differ from the type by less than the gate's 1 mm, which judges each.
        bool Same(JsonObject other) => rules.All(kv => other[kv.Key] is var o && (Json.Number(kv.Value) is { } a && Json.Number(o) is { } b ? Math.Abs(a - b) <= Tol : JsonNode.DeepEquals(kv.Value, o)));
        for (var n = 1; ; n++)
        {
            var (key, name) = n == 1 ? ($"joist:{basis}", basis) : ($"joist:{basis}:{n}", $"{basis} ({n})");
            if (ctx.TypeIds.TryGetValue(key, out var known) && edit.Model.Get(known) is { } t)
            {
                if (Same(JoistSpec.Of(t.Attrs).ToJson())) return t;
                continue;
            }
            var type = OpenWebJoists.CreateType(edit, name, spec);
            ctx.CreatedTypes.Add(type.Id);
            ctx.TypeIds[key] = type.Id;
            return type;
        }
    }
}
