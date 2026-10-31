using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// The bars in space drawn for the 3D view on request (decision 221): not bodies in the model (those are the premium's
/// IfcReinforcingBar), but a drawing the interface asks for with <c>rebar.bodies</c> — every bar laid in space (a bar drawn
/// by hand with its copies, a type's stirrups at their stations, a column's bars) as a hexagonal tube of its diameter along
/// its axis, the bends as a few segments on the real arc, the triangles grouped by colour (the diameter's). The arrays go
/// out as the protocol's binary buffers, not JSON numbers. Over <see cref="MaxTriangles"/> the rest is left out and the
/// reply says so.
/// </summary>
public static class RebarBodies
{
    public const int MaxTriangles = 2_000_000;
    /// <summary>A tube's sides: a hexagon reads as round at a bar's size and costs twelve triangles a segment.</summary>
    public const int Sides = 6;
    /// <summary>A bend's arc is sampled at this angle or finer, three segments at least.</summary>
    public const double ArcStepDeg = 30;

    /// <summary>One colour's triangles: the vertices (xyz, metres) and the triangle indices into them.</summary>
    public sealed record Group(string Color, float[] Positions, int[] Indices)
    {
        public int Triangles => Indices.Length / 3;
    }

    public sealed record Result(IReadOnlyList<Group> Groups, int Triangles, bool Truncated)
    {
        public JsonObject ToJson() => new()
        {
            ["groups"] = new JsonArray(Groups.Select(g => (JsonNode)new JsonObject
            {
                ["color"] = g.Color,
                ["positions"] = JsonValue.Create(g.Positions, RebarJson.Default.SingleArray),
                ["indices"] = JsonValue.Create(g.Indices, RebarJson.Default.Int32Array),
            }).ToArray()),
            ["triangles"] = Triangles,
            ["truncated"] = Truncated,
        };
    }

    /// <summary>A bar's axis in space with its diameter and colour.</summary>
    public sealed record Chain(IReadOnlyList<Vec3> Points, double Diameter, string Color);

    public static void Register(IModuleRegistry r) =>
        // {ids?: [element ids]}: the bars of these elements (a bar, a beam, a column) alone; all of them otherwise.
        r.Query("bodies", (q, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet();
            return Build(Chains(q.Model, q.Services, q.Geometry, RebarSettings.Of(q.Model), ids)).ToJson();
        });

    /// <summary>
    /// Every bar in space as a chain on its axis: the hand-drawn bars' copies (as <see cref="Rebar3D.Bars"/> lays them,
    /// but the arcs sampled coarser), each type's stirrups bent at every station, each column's bars.
    /// </summary>
    public static IEnumerable<Chain> Chains(IModelView m, IServices? services, Func<string, ElementGeometry?>? geometry, RebarSettings st, ISet<string>? ids = null)
    {
        string Color(double d) => st.ColorByDiameter ? RebarShapes.ColorOf(d) : "#000000";
        foreach (var e in m.OfClass(Rebars.Class))
        {
            // A bar put in section elsewhere (not in the schedule) stands for one already drawn.
            if (e.Bool("excludeSchedule") || (ids != null && !ids.Contains(e.Id))) continue;
            var s = Rebars.SpecOf(e);
            foreach (var chain in Placed(m, services, geometry, e, s, st)) yield return new Chain(chain, s.Diameter, Color(s.Diameter));
        }
        foreach (var e in m.Entities)
        {
            if (e.Class is not (IfcClass.Beam or IfcClass.Column) || (ids != null && !ids.Contains(e.Id))) continue;
            if (TypeStirrups.Of(m, e) is { } laid)
            {
                var d = laid.Spec.Diameter;
                var spec = new RebarSpec("stirrup", new Dictionary<string, double>(), null, d, null, null);
                foreach (var station in laid.Stations)
                    foreach (var loop in laid.LoopsAt(station))
                        yield return new Chain(Stirrup(loop, spec), d, Color(d));
            }
            if (ColumnBars.Of(m, e) is { } bars)
                foreach (var bar in bars.Bars) yield return new Chain(bar, bars.Spec.Diameter, Color(bars.Spec.Diameter));
        }
    }

    /// <summary>A hand-drawn bar's copies in space: its chain on its view's plane, at each depth and array place (as <see cref="Rebar3D.Bars"/>), a dot run along the view's direction.</summary>
    static IEnumerable<Vec3[]> Placed(IModelView m, IServices? services, Func<string, ElementGeometry?>? geometry, Entity e, RebarSpec s, RebarSettings st)
    {
        var view = Views.Find(m, e.Str("viewId") ?? "");
        if (Rebar3D.FrameOf(m, services, view) is not { } frame) yield break;
        var at = Rebars.At(e);
        var chain = s.Shape == "dot" ? [at] : Sample(RebarShapes.Segments(s).Select(x => RebarShapes.Place(s, at, x)).ToList());
        if (chain.Count == 0) yield break;
        var half = s.Shape == "dot" ? s["length"] / 2 : 0;
        var (nx, dx, ny, dy) = Rebars.Array(e);
        foreach (var depth in Rebar3D.Depths(m, geometry, e, frame, st))
            for (var ix = 0; ix < nx; ix++)
                for (var iy = 0; iy < ny; iy++)
                {
                    var off = new Vec2(ix * dx, iy * dy);
                    Vec3 P(Vec2 p, double along) => frame.Origin + frame.Right * (p.X + off.X) + frame.Up * (p.Y + off.Y) + frame.Dir * (depth + along);
                    yield return half > 0 ? [P(chain[0], -half), P(chain[0], half)] : chain.Select(p => P(p, 0)).ToArray();
                }
    }

    /// <summary>
    /// A stirrup loop bent in space: the shape's chain (its corners rounded, its hooks) mapped onto the loop's plane by its
    /// first corner and two sides, as the 2D drawing maps it (an affine map, so a diamond's rhombus bends as drawn).
    /// </summary>
    public static Vec3[] Stirrup(TypeStirrups.Loop loop, RebarSpec spec)
    {
        var (c0, c1, c3) = (loop.Corners[0], loop.Corners[1], loop.Corners[3]);
        var (u, v) = ((c1 - c0) * (1 / loop.A), (c3 - c0) * (1 / loop.B));
        var s = spec with { Params = new Dictionary<string, double> { ["a"] = loop.A, ["b"] = loop.B } };
        return Sample(RebarShapes.Segments(s)).Select(p => c0 + u * p.X + v * p.Y).ToArray();
    }

    /// <summary>The segments as one chain, an arc as <see cref="ArcStepDeg"/> steps, three at least (coarser than the drawing's).</summary>
    public static List<Vec2> Sample(IReadOnlyList<Seg> segs)
    {
        var o = new List<Vec2>();
        void Add(Vec2 p) { if (o.Count == 0 || (o[^1] - p).Length > 1e-9) o.Add(p); }
        foreach (var seg in segs)
            switch (seg)
            {
                case SegLine l: Add(l.A); Add(l.B); break;
                case SegArc a:
                    var n = Math.Max(3, (int)Math.Ceiling(Math.Abs(a.Sweep) / (ArcStepDeg * Math.PI / 180)));
                    for (var k = 0; k <= n; k++) Add(a.At((double)k / n));
                    break;
            }
        return o;
    }

    /// <summary>The chains as tubes, grouped by colour, until the cap is reached (the chain that would pass it, and the rest, are left out).</summary>
    public static Result Build(IEnumerable<Chain> chains, int cap = MaxTriangles)
    {
        var groups = new Dictionary<string, (List<float> Positions, List<int> Indices)>();
        var order = new List<string>();
        var triangles = 0;
        var truncated = false;
        foreach (var chain in chains)
        {
            var pts = Distinct(chain.Points);
            if (pts.Count < 2) continue;
            var cost = TrianglesOf(pts.Count);
            if (triangles + cost > cap) { truncated = true; break; }
            if (!groups.TryGetValue(chain.Color, out var g)) { g = groups[chain.Color] = ([], []); order.Add(chain.Color); }
            Tube(pts, chain.Diameter / 2, g.Positions, g.Indices);
            triangles += cost;
        }
        return new Result(order.Select(c => new Group(c, groups[c].Positions.ToArray(), groups[c].Indices.ToArray())).ToList(), triangles, truncated);
    }

    /// <summary>A tube's triangles for a chain of so many points: the sides between the rings and the two end caps.</summary>
    public static int TrianglesOf(int points) => 2 * Sides * (points - 1) + 2 * (Sides - 2);

    static List<Vec3> Distinct(IReadOnlyList<Vec3> pts)
    {
        var o = new List<Vec3>(pts.Count);
        foreach (var p in pts) if (o.Count == 0 || (o[^1] - p).Length > 1e-9) o.Add(p);
        return o;
    }

    /// <summary>
    /// A hexagonal tube along a chain: a ring at each point, in the plane that halves the angle there, its frame carried
    /// along the chain so the tube does not twist; the sides between the rings, a fan at each end. Appended to the lists,
    /// the indices counted from the vertices already there.
    /// </summary>
    public static void Tube(IReadOnlyList<Vec3> pts, double radius, List<float> positions, List<int> indices)
    {
        var n = pts.Count;
        if (n < 2) return;
        var dirs = new Vec3[n - 1];
        for (var i = 0; i < n - 1; i++) dirs[i] = (pts[i + 1] - pts[i]).Normalized();
        var normal = Vec3.Cross(dirs[0], Math.Abs(dirs[0].Z) > 0.9 ? new Vec3(1, 0, 0) : Vec3.UnitZ).Normalized();
        var first = positions.Count / 3;
        for (var i = 0; i < n; i++)
        {
            var t = i == 0 ? dirs[0] : i == n - 1 ? dirs[n - 2] : dirs[i - 1] + dirs[i];
            if (t.Length < 1e-9) t = dirs[i]; // Back on itself: the ring across the leg.
            t = t.Normalized();
            var carried = normal - t * Vec3.Dot(normal, t);
            normal = carried.Length > 1e-9 ? carried.Normalized() : Vec3.Cross(t, Math.Abs(t.Z) > 0.9 ? new Vec3(1, 0, 0) : Vec3.UnitZ).Normalized();
            var binormal = Vec3.Cross(t, normal);
            for (var k = 0; k < Sides; k++)
            {
                var a = 2 * Math.PI * k / Sides;
                var p = pts[i] + normal * (radius * Math.Cos(a)) + binormal * (radius * Math.Sin(a));
                positions.Add((float)p.X); positions.Add((float)p.Y); positions.Add((float)p.Z);
            }
        }
        // The sides, outward (the rings turn counter-clockwise about the direction).
        for (var i = 0; i < n - 1; i++)
            for (var k = 0; k < Sides; k++)
            {
                var (a, b) = (first + i * Sides + k, first + i * Sides + (k + 1) % Sides);
                var (c, d) = (a + Sides, b + Sides);
                indices.Add(a); indices.Add(b); indices.Add(d);
                indices.Add(a); indices.Add(d); indices.Add(c);
            }
        // The caps: the first looks back along the chain, the last ahead.
        for (var k = 1; k < Sides - 1; k++) { indices.Add(first); indices.Add(first + k + 1); indices.Add(first + k); }
        var last = first + (n - 1) * Sides;
        for (var k = 1; k < Sides - 1; k++) { indices.Add(last); indices.Add(last + k); indices.Add(last + k + 1); }
    }
}

/// <summary>The bodies' arrays as JSON values: the host's frame writes them as its binary buffers (float32, int32), a trimmed engine without reflection too.</summary>
[JsonSerializable(typeof(float[]))]
[JsonSerializable(typeof(int[]))]
internal partial class RebarJson : JsonSerializerContext;
