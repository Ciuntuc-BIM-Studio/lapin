using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// What the roof configurator shows of a roof (decision 231): its fields as they stand, every edge's kind, the plan (the wall
/// line, the eaves, ridges, hips and valleys), a section across the main ridge through its framing (the covering, the wall
/// plates, the truss or couple nearest the middle with its webs and collar tie, the purlins and ridge beam cut), and a summary
/// of what it takes. Read-only: the configurator sends the draft, the model is not touched.
/// </summary>
public static class RoofPreview
{
    static JsonArray Pt(Vec2 p) => new(Math.Round(p.X, 4), Math.Round(p.Y, 4));
    static JsonArray Pts(IEnumerable<Vec2> ps) => new(ps.Select(p => (JsonNode)Pt(p)).ToArray());
    static string N(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);

    public static JsonObject Of(Entity roof, IModelView m)
    {
        var o = new JsonObject { ["attrs"] = roof.Attrs.DeepClone() };
        if (Roof.Resolve(roof, m) is not { } r) { o["problem"] = "A roof needs three corners at least."; return o; }
        o["outline"] = Pts(r.Outline);
        o["kinds"] = new JsonArray(r.Rules.Select(x => (JsonNode)(x.Kind == EdgeKind.Gable ? "gable" : "eave")).ToArray());
        o["eaves"] = Pts(Roof.EaveOutline(r));
        if (Roof.Problem(r) is { } problem) { o["problem"] = problem; return o; }
        if (r.Flat)
        {
            o["summary"] = new JsonArray(Row("Covering", $"{N(Math.Abs(Polygon2.SignedArea(Roof.EaveOutline(r))), "0.0")} m²"));
            return o;
        }
        var (shape, why) = Roof.Solve(r);
        if (shape == null) { o["problem"] = why ?? "The roof does not build."; return o; }
        o["lines"] = new JsonArray(shape.Lines.Where(l => l.Kind != LineKind.Eave).Select(l => (JsonNode)new JsonObject
        {
            ["kind"] = l.Kind.ToString().ToLowerInvariant(), ["a"] = Pt(l.A.Xy), ["b"] = Pt(l.B.Xy),
        }).ToArray());
        var holes = RoofOpenings.Place(roof, shape, m, []);
        var members = RoofParts.Framing(r, shape, holes);
        o["section"] = Section(r, shape, members);

        var summary = new JsonArray(
            Row("Covering", $"{N(shape.Planes.Sum(p => VecMath.NewellNormal(p.Points).Length), "0.0")} m² · {r.Covering.Id}"),
            Row("Height over the plates", $"{N(shape.Height - r.BaseZ, "0.00")} m"));
        if (members.Count > 0)
        {
            var trusses = members.Where(x => x.Truss >= 0).Select(x => x.Truss).Distinct().Count();
            if (trusses > 0) summary.Add((JsonNode)Row("Trusses", $"{trusses}"));
            foreach (var role in new[] { "rafter", "collar", "purlin", "ridge", "hip", "valley", "trimmer" })
                if (members.Where(x => x.Role == role).ToList() is { Count: > 0 } ms)
                    summary.Add((JsonNode)Row(RoofParts.RoleLabel(role) + "s", role is "rafter" or "collar" ? $"{ms.Count} · {N(ms.Sum(x => x.Length), "0.0")} m" : $"{N(ms.Sum(x => x.Length), "0.0")} m"));
            summary.Add((JsonNode)Row("Timber", $"{N(members.Sum(x => x.Volume), "0.00")} m³ · {r.Grade}"));
        }
        var gutters = RoofParts.Gutters(r, shape);
        if (gutters.Count > 0) summary.Add((JsonNode)Row("Gutters", $"{N(gutters.Sum(x => x.Length), "0.0")} m · {RoofParts.Downpipes(r, shape, gutters).Count} downpipes"));
        o["summary"] = summary;
        return o;
    }

    static JsonArray Row(string label, string value) => new(label, value);

    /// <summary>
    /// A vertical section across the main ridge (or the longest eave) through the outline's middle: x across, z up. The covering
    /// over the slopes it crosses, the walls under the plates, the members a cut there meets: of the rafters and chords that stand
    /// parallel to it, those nearest on each side with their truss or collar tie; the level members it crosses, cut.
    /// </summary>
    static JsonObject Section(Roof.Resolved r, RoofShape shape, List<RoofParts.Member> members)
    {
        var ridge = shape.Lines.Where(l => l.Kind == LineKind.Ridge && l.Length > 0.1).MaxBy(l => l.Length);
        var eave = Enumerable.Range(0, r.Outline.Count).Where(i => r.Rules[i].Kind == EdgeKind.Eave)
            .Select(i => r.Outline[(i + 1) % r.Outline.Count] - r.Outline[i]).DefaultIfEmpty(r.Outline[1] - r.Outline[0]).MaxBy(d => d.Length);
        var axis = ridge != null ? (ridge.B - ridge.A).Xy.Normalized() : eave.Normalized();
        var n = new Vec2(-axis.Y, axis.X);
        var c = ridge != null ? (ridge.A.Xy + ridge.B.Xy) * 0.5 : r.Outline.Aggregate(new Vec2(0, 0), (s, p) => s + p) * (1.0 / r.Outline.Count);
        double X(Vec3 p) => Vec2.Dot(p.Xy - c, n);
        double Along(Vec3 p) => Vec2.Dot(p.Xy - c, axis);
        var items = new JsonArray();
        void Add(string role, IEnumerable<(double X, double Z)> pts) =>
            items.Add((JsonNode)new JsonObject { ["role"] = role, ["pts"] = new JsonArray(pts.SelectMany(p => new[] { (JsonNode)Math.Round(p.X, 4), Math.Round(p.Z, 4) }).ToArray()) });

        // The covering (or the deck) over each slope the cut crosses: from the structure's top to the covering's.
        foreach (var plane in shape.Planes)
        {
            var z = RoofSolver.PlaneZ(plane.Points);
            foreach (var (k0, k1) in RoofParts.Crossings(plane.Points.Select(q => q.Xy).ToList(), c, n))
            {
                double Zk(double k) => z(c + n * k);
                var sec = Math.Sqrt(1 + Math.Pow((Zk(k1) - Zk(k0)) / (k1 - k0), 2));
                var (lo, hi) = r.Framed ? (r.StructureDepth * sec, r.Thickness * sec) : (0, r.Thickness * sec);
                Add(r.Framed ? "covering" : "deck", [(k0, Zk(k0) + lo), (k1, Zk(k1) + lo), (k1, Zk(k1) + hi), (k0, Zk(k0) + hi)]);
            }
        }
        // The walls under the wall line, a storey's top's worth down.
        foreach (var (w0, w1) in RoofParts.Crossings(r.Outline, c, n))
            foreach (var w in new[] { w0, w1 })
                Add("wall", [(w - 0.15, r.PlateZ - 0.8), (w + 0.15, r.PlateZ - 0.8), (w + 0.15, r.PlateZ), (w - 0.15, r.PlateZ)]);

        // Rafters and chords parallel to the cut: the nearest on each side (rising one way or the other), with what goes with them.
        var parallel = members.Where(x => x.Profile != null && Math.Abs(Vec2.Dot(x.Side.Xy.Normalized(), axis)) > 0.99).ToList();
        var chosen = new List<RoofParts.Member>();
        // The one nearest the middle (the lower of two as near), then on each side the nearest to it.
        var at = parallel.Count == 0 ? 0 : parallel.Select(x => Along(x.A)).OrderBy(v => Math.Round(Math.Abs(v), 6)).ThenBy(v => v).First();
        foreach (var side in parallel.GroupBy(x => Math.Sign((x.B.Z - x.A.Z) * Vec2.Dot((x.B - x.A).Xy, n))))
        {
            var d = side.Min(x => Math.Abs(Along(x.A) - at));
            chosen.AddRange(side.Where(x => Math.Abs(Math.Abs(Along(x.A) - at) - d) < 0.01));
        }
        var trusses = chosen.Where(x => x.Truss >= 0).Select(x => x.Truss).ToHashSet();
        var near = chosen.Select(x => Along(x.A)).ToList();
        foreach (var x in chosen) Add(x.Role, x.Profile!.Select(p => (X(p), p.Z)));
        foreach (var x in members.Where(x => x.Profile == null && (x.Truss >= 0 && trusses.Contains(x.Truss) || x.Role == "collar" && near.Any(a => Math.Abs(Along(x.A) - a) < x.Width * 1.5 + 0.01))))
        {
            // A web, a bottom chord or a collar tie in the plane: its outline, as deep as its section.
            var (a, b) = ((X(x.A), x.A.Z), (X(x.B), x.B.Z));
            var len = Math.Sqrt(Math.Pow(b.Item1 - a.Item1, 2) + Math.Pow(b.Item2 - a.Item2, 2));
            if (len < 1e-6) continue;
            var (px, pz) = (-(b.Item2 - a.Item2) / len * x.Depth / 2, (b.Item1 - a.Item1) / len * x.Depth / 2);
            Add(x.Role, [(a.Item1 + px, a.Item2 + pz), (b.Item1 + px, b.Item2 + pz), (b.Item1 - px, b.Item2 - pz), (a.Item1 - px, a.Item2 - pz)]);
        }
        // Level members the cut crosses (wall plates, purlins, the ridge beam, trimmers): their section.
        foreach (var x in members.Where(x => x.Profile == null && x.Truss < 0 && x.Role != "collar"))
        {
            var (sa, sb) = (Along(x.A), Along(x.B));
            if (sa * sb > 0 || Math.Abs(sb - sa) < 1e-9) continue;
            var t = sa / (sa - sb);
            var p = x.A + (x.B - x.A) * t;
            var dir = (x.B - x.A).Normalized();
            if (Math.Abs(dir.Z) > 0.3) continue;
            var (cx, cz) = (X(p), p.Z);
            Add(x.Role, [(cx - x.Width / 2, cz - x.Depth / 2), (cx + x.Width / 2, cz - x.Depth / 2), (cx + x.Width / 2, cz + x.Depth / 2), (cx - x.Width / 2, cz + x.Depth / 2)]);
        }
        var xs = items.SelectMany(i => ((JsonArray)i!["pts"]!).Where((_, k) => k % 2 == 0).Select(v => (double)v!)).DefaultIfEmpty(0).ToList();
        var zs = items.SelectMany(i => ((JsonArray)i!["pts"]!).Where((_, k) => k % 2 == 1).Select(v => (double)v!)).DefaultIfEmpty(0).ToList();
        var walls = RoofParts.Crossings(r.Outline, c, n);
        return new JsonObject
        {
            ["items"] = items,
            ["box"] = new JsonArray(xs.Min(), zs.Min(), xs.Max(), zs.Max()),
            ["span"] = walls.Count > 0 ? Math.Round(walls[^1].T1 - walls[0].T0, 3) : null,
            ["walls"] = new JsonArray(walls.SelectMany(w => new[] { (JsonNode)Math.Round(w.T0, 4), Math.Round(w.T1, 4) }).ToArray()),
            ["plate"] = Math.Round(r.BaseZ, 4),
            ["top"] = Math.Round(shape.Height, 4),
        };
    }
}
