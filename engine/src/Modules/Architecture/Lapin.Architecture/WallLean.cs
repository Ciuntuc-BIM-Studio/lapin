using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A leaning wall (docs/plan-wall-lean-taper.md, stage L1; in-app report #191). A straight wall's <see cref="Key"/> (degrees
/// from vertical, + towards its left face; its type's when it has none of its own, 0 when neither has one) tilts it in its own
/// plane by the map <c>(x, y, z) → (x, y/cosθ + (z − pivot)·tanθ, z)</c> in its frame: its faces stay planes, each layer
/// keeps its thickness measured normal to the faces, every horizontal section is its base footprint moved across, its height
/// stays vertical. <see cref="FromKey"/> says which level stays put (<c>base</c>, the default: the wall stays on its footing;
/// <c>top</c>; <c>axis</c>, half its height). <see cref="CutKey"/>: its doors and windows lean with it (<c>wall</c>, the
/// default) or stand plumb (<c>vertical</c>); their holes are the same either way, the sill and head level.
/// The wall's joins are worked out at its base and at its top (<see cref="At"/>: every leaning wall's layers as they are at
/// that height, <see cref="Sectioned"/>) and its layers lofted between the two, so two walls meet where their faces' planes
/// cross. Lapin's design intent, written in Lapin_Intent; the IFC body an oblique extrusion where it is one. Absent (every wall
/// made before, every import of a plumb wall), nothing changes: the wall is the prism it was.
/// Stage 1 does not lean (and says so, the wall standing plumb): a curved wall, one with its own top profile, one rising to a
/// roof or attached to one, one whose layers have extents of their own or go on into the floors, one wrapping its free ends.
/// </summary>
public static class WallLean
{
    public const string Key = "lean", FromKey = "leanFrom", CutKey = "openingCut";
    public const string Base = "base", Top = "top", Axis = "axis";
    public const string Wall = "wall", Vertical = "vertical";
    public static readonly string[] Froms = [Base, Top, Axis];
    public static readonly string[] Cuts = [Wall, Vertical];
    /// <summary>The steepest lean, degrees either way.</summary>
    public const double Max = 60;

    /// <summary>A wall's lean as it is built: its tangent and cosine, and the height over its base that stays put.</summary>
    public sealed record Lean(double Degrees, double Tan, double Cos, double Pivot)
    {
        /// <summary>How far across (to the left) the wall's section is moved at <paramref name="h"/> over its base.</summary>
        public double Shift(double h) => (h - Pivot) * Tan;
        /// <summary>A band edge at <paramref name="y"/> across the axis as it is at <paramref name="h"/> over the base.</summary>
        public double At(double y, double h) => y / Cos + Shift(h);
    }

    /// <summary>The lean set: the wall's own, else its type's, else 0 (degrees).</summary>
    public static double Angle(IModelView m, Entity wall) =>
        wall.NumOrNull(Key) ?? (Walls.TypeOf(m, wall.Id)?.NumOrNull(Key) ?? 0);

    /// <summary>Which level stays put: the wall's own, else its type's, else the base.</summary>
    public static string FromOf(IModelView m, Entity wall) =>
        wall.Str(FromKey) is { } own && Froms.Contains(own) ? own : Walls.TypeOf(m, wall.Id)?.Str(FromKey) is { } t && Froms.Contains(t) ? t : Base;

    /// <summary>How its openings stand: the wall's own, else its type's, else with the wall.</summary>
    public static string CutOf(IModelView m, Entity wall) =>
        wall.Str(CutKey) is { } own && Cuts.Contains(own) ? own : Walls.TypeOf(m, wall.Id)?.Str(CutKey) is { } t && Cuts.Contains(t) ? t : Wall;

    /// <summary>The lean the wall is built with; null for a plumb wall, or one stage 1 cannot lean (<see cref="Refusal"/>).</summary>
    public static Lean? Of(IModelView m, Entity wall)
    {
        if (wall.Class != IfcClass.Wall) return null;
        var deg = Angle(m, wall);
        if (Math.Abs(deg) < 1e-9 || Math.Abs(deg) > Max + 1e-9) return null;
        if (ambient is { } a && a.Cache.TryGetValue(wall.Id, out var known)) return known;
        Lean? o = null;
        // Worked out with no level in force: what the refusal reads of the wall is the wall as it is.
        var saved = ambient;
        ambient = null;
        try
        {
            if (Refusal(m, wall, null) == null)
            {
                var r = deg * Math.PI / 180;
                var height = Walls.HeightOf(m, wall);
                var pivot = FromOf(m, wall) switch { Top => height, Axis => height / 2, _ => 0 };
                o = new Lean(deg, Math.Tan(r), Math.Cos(r), pivot);
            }
        }
        finally { ambient = saved; }
        if (ambient is { } b) b.Cache[wall.Id] = o;
        return o;
    }

    /// <summary>Why stage 1 cannot lean this wall; null when it can (or it has no lean).</summary>
    public static string? Refusal(IModelView m, Entity wall, IServices? services)
    {
        if (PathCurve.Of(wall).IsArc) return "A curved wall cannot lean yet (leaning walls, stage 1): it stands plumb.";
        if (wall.Attrs[WallTops.TopProfileKey] is JsonArray { Count: >= 2 }) return "A wall with a top profile of its own cannot lean yet: it stands plumb.";
        if (WallEndWraps.Of(m, wall) != WallEndWraps.None) return "A wall that wraps a finish round its free ends cannot lean yet: it stands plumb (Wrap at free ends: none).";
        var bands = Walls.SectionPlumb(m, wall);
        if (LayerExtents.Effective(wall, m).Count > 0 || bands.Any(b => b.ExtendDown || b.ExtendUp))
            return "A wall whose layers have extents of their own, or go on into the floors, cannot lean yet: it stands plumb.";
        var (a, b) = (Walls.Start(wall), Walls.End(wall));
        if ((b - a).Length < 1e-9) return null;
        if (wall.Str("roofTop") == "attach") return "A wall attached to a roof or a body cannot lean yet: it stands plumb (Top under a roof: none).";
        var axis = new RoofTop.WallAxis(a, (b - a).Normalized(), (b - a).Length, Spatial.Base(m, wall), Walls.HeightOf(m, wall), bands.Max(x => x.To) - bands.Min(x => x.From));
        if (WallTops.Profile(wall, axis, m, services) != null) return "A wall that rises to a roof cannot lean yet: it stands plumb (Top under a roof: none).";
        return null;
    }

    /// <summary>The problem a wall with a lean stage 1 cannot build shows; null for every other wall.</summary>
    public static string? Problem(IModelView m, Entity wall, IServices? services)
    {
        var deg = Angle(m, wall);
        if (Math.Abs(deg) < 1e-9) return null;
        if (Math.Abs(deg) > Max + 1e-9) return $"A wall leans at most {Max}°: it stands plumb.";
        return Refusal(m, wall, services);
    }

    /// <summary>Whether the wall is built by lofting: it leans, or a wall joined to it does (its joined ends follow that wall's faces).</summary>
    public static bool Touches(IModelView m, Entity wall)
    {
        if (Math.Abs(Angle(m, wall)) > 1e-9) return Of(m, wall) != null;
        // A plumb wall: lofted only when a leaning wall is joined to it (and it has nothing stage 1 cannot loft).
        var leaning = false;
        foreach (var r in Joins.Of(m, wall.Id))
            if (m.Get(Joins.Other(r, wall.Id)) is { } other && Math.Abs(Angle(m, other)) > 1e-9 && Of(m, other) != null) { leaning = true; break; }
        return leaning && Refusal(m, wall, null) == null;
    }

    // ------------------------------------------------------------ the level the layouts are worked out at

    sealed class Level(double z)
    {
        public readonly double Z = z;
        public readonly Dictionary<string, Lean?> Cache = [];
    }

    [ThreadStatic] static Level? ambient;

    /// <summary>
    /// While the scope lasts, every leaning wall's section (<see cref="Walls.Section"/>) is as it is at the absolute height
    /// <paramref name="z"/>: its bands across the axis as wide as they are horizontally (thickness / cosθ) and moved across by its
    /// lean there. What the joins work out in it is the wall's layout at that height.
    /// </summary>
    public static IDisposable At(double z)
    {
        var saved = ambient;
        ambient = new Level(z);
        return new Restore(saved);
    }

    sealed class Restore(Level? saved) : IDisposable
    {
        public void Dispose() => ambient = saved;
    }

    /// <summary>The wall's bands as the level in force has them (<see cref="At"/>); as they are with none, or for a plumb wall.</summary>
    internal static IReadOnlyList<Walls.Band> Sectioned(IModelView m, Entity wall, IReadOnlyList<Walls.Band> bands)
    {
        if (ambient is not { } level || Of(m, wall) is not { } lean) return bands;
        var h = level.Z - Spatial.Base(m, wall);
        return [.. bands.Select(b => b with { From = lean.At(b.From, h), To = lean.At(b.To, h) })];
    }

    // ------------------------------------------------------------ the map

    /// <summary>
    /// The map that leans what a plumb wall's frame holds (its openings' boxes, its lintels, its doors and windows): a point at
    /// <c>y</c> across the axis through <paramref name="origin"/> and <c>h</c> over <paramref name="baseZ"/> goes across to
    /// <c>y/cosθ + (h − pivot)·tanθ</c>; along the axis and up it stays.
    /// </summary>
    public static Func<Vec3, Vec3> Map(Lean lean, Vec2 origin, Vec2 dir, double baseZ)
    {
        var left = dir.Perp;
        return p =>
        {
            var y = Vec2.Dot(p.Xy - origin, left);
            var d = lean.At(y, p.Z - baseZ) - y;
            return new Vec3(p.X + left.X * d, p.Y + left.Y * d, p.Z);
        };
    }

    /// <summary>The same map in plan, at <paramref name="h"/> over the base.</summary>
    public static Func<Vec2, Vec2> MapAt(Lean lean, Vec2 origin, Vec2 dir, double h)
    {
        var left = dir.Perp;
        return p =>
        {
            var y = Vec2.Dot(p - origin, left);
            return p + left * (lean.At(y, h) - y);
        };
    }

    public static Solid Mapped(Solid s, Func<Vec3, Vec3> map) => SolidBuilder.Transform(s, map);

    // ------------------------------------------------------------ the loft

    /// <summary>
    /// The solid between a footprint at <paramref name="z0"/> and the same footprint's vertices moved at <paramref name="z1"/>
    /// (the same count, in the same order): its caps "bottom" and "top", its sides "side", each a plane where its four corners are
    /// on one, else two triangles. Null for a footprint of no area.
    /// </summary>
    public static Solid? Loft(IReadOnlyList<Vec2> bottom, double z0, IReadOnlyList<Vec2> top, double z1, string? tag)
    {
        if (bottom.Count < 3 || bottom.Count != top.Count || !(z1 - z0 > 1e-9)) return null;
        var area = Polygon2.SignedArea(bottom);
        if (Math.Abs(area) < Tolerance.Area) return null;
        var n = bottom.Count;
        var (lo, hi) = area > 0 ? (bottom, top) : ((IReadOnlyList<Vec2>)[.. bottom.Reverse()], (IReadOnlyList<Vec2>)[.. top.Reverse()]);
        var verts = new List<Vec3>(2 * n);
        foreach (var p in lo) verts.Add(p.At(z0));
        foreach (var p in hi) verts.Add(p.At(z1));
        var faces = new List<FaceInput>
        {
            new([.. Enumerable.Range(0, n).Reverse()], null, new Vec3(0, 0, -1), "bottom"),
            new([.. Enumerable.Range(n, n)], null, new Vec3(0, 0, 1), "top"),
        };
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            var (a, b, c, d) = (i, j, n + j, n + i);
            var up = verts[d] - verts[a];
            if ((verts[b] - verts[a]).Length < 1e-12) up = verts[c] - verts[b];
            var outward = Vec3.Cross(verts[b] - verts[a], up);
            if (outward.Length < 1e-15) outward = Vec3.Cross(verts[c] - verts[d], verts[c] - verts[b]);
            var normal = VecMath.NewellNormal([verts[a], verts[b], verts[c], verts[d]]);
            var unit = normal.Length > 0 ? normal * (1 / normal.Length) : outward;
            var flat = new[] { a, b, c, d }.All(k => Math.Abs(Vec3.Dot(verts[k] - verts[a], unit)) < 1e-9);
            if (flat) faces.Add(new FaceInput([a, b, c, d], null, outward, "side"));
            else
            {
                faces.Add(new FaceInput([a, b, c], null, Vec3.Cross(verts[b] - verts[a], verts[c] - verts[a]), "side"));
                faces.Add(new FaceInput([a, c, d], null, Vec3.Cross(verts[c] - verts[a], verts[d] - verts[a]), "side"));
            }
        }
        var s = SolidBuilder.Make(verts, faces, tag);
        return Measure.SignedVolume(s) < 0 ? SolidBuilder.Flip(s) : s;
    }

    // ------------------------------------------------------------ doors and windows

    /// <summary>
    /// A door's or window's body and plan in a leaning wall: leaning with it (<see cref="Wall"/>: the same map as its hole), or
    /// plumb (<see cref="Vertical"/>: moved across to where the wall's middle is at its mid-height). As it is in a plumb wall.
    /// </summary>
    public static ElementGeometry? Follow(IModelView m, Entity fill, ElementGeometry? g, double? planCut = null)
    {
        if (g == null || Openings.OpeningOf(m, fill.Id) is not { } opening || Openings.WallOf(m, opening.Id) is not { Class: IfcClass.Wall } wall) return g;
        if (CurtainCells.SeatOf(m, fill) != null || Of(m, wall) is not { } lean) return g;
        var (origin, dir) = (Walls.Start(wall), (Walls.End(wall) - Walls.Start(wall)).Normalized());
        var z = Spatial.Base(m, wall);
        var p = Openings.Place(m, opening);
        var f = Openings.FrameOf(m, wall, p.Along);
        var cut = (planCut ?? Openings.PlanCut) - wall.Num("baseOffset");
        Func<Vec3, Vec3> body;
        Func<Vec2, Vec2> plan;
        if (CutOf(m, wall) == Vertical)
        {
            var mid = p.FillSill + p.Height / 2;
            var d = dir.Perp * (lean.At(f.Centre, mid) - f.Centre);
            body = q => new Vec3(q.X + d.X, q.Y + d.Y, q.Z);
            plan = q => q + d;
        }
        else
        {
            body = Map(lean, origin, dir, z);
            plan = MapAt(lean, origin, dir, cut);
        }
        return g with
        {
            Parts = [.. g.Parts.Select(x => x with { Solid = Mapped(x.Solid, body) })],
            Plan = g.Plan?.Select(x => x with { Points = [.. x.Points.Select(plan)] }).ToList(),
            Snaps = g.Snaps?.Select(plan).ToList(),
        };
    }

    /// <summary>A plan's symbols of a door or window (<see cref="Openings.FillPlanAt"/>) moved as its body is, at that plan's cut.</summary>
    public static IReadOnlyList<PlanItem>? FollowPlan(IModelView m, Entity fill, IReadOnlyList<PlanItem>? items, double cut) =>
        items == null ? null : Follow(m, fill, new ElementGeometry([], items), cut)?.Plan;

    /// <summary>A lean given in a command (degrees, ±<see cref="Max"/>) or null to clear it.</summary>
    public static double? Parse(JsonNode? n, string what = "Lean")
    {
        if (n is null) return null;
        var v = Json.Number(n) ?? throw new CommandException($"{what} is an angle in degrees from vertical.");
        if (!double.IsFinite(v) || Math.Abs(v) > Max) throw new CommandException($"{what} is at most {Max}° either way.");
        return v;
    }

    /// <summary>Sets (or, for null, clears) a type's or a wall's lean settings from a command's arguments (those given only).</summary>
    /// <remarks>The arguments by their names (<c>lean</c>, <c>leanFrom</c>, <c>openingCut</c>), as the command catalogue reads them.</remarks>
    public static void Set(JsonObject attrs, JsonObject a)
    {
        if (a.ContainsKey("lean"))
        {
            if (Parse(a["lean"]) is { } v) attrs[Key] = v;
            else attrs.Remove(Key);
        }
        if (a.ContainsKey("leanFrom")) Choose(attrs, FromKey, a["leanFrom"], Froms, "Lean from");
        if (a.ContainsKey("openingCut")) Choose(attrs, CutKey, a["openingCut"], Cuts, "Openings in a leaning wall");
    }

    static void Choose(JsonObject attrs, string key, JsonNode? n, string[] values, string what)
    {
        if (n is null) { attrs.Remove(key); return; }
        var s = n is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
        if (s == null || !values.Contains(s)) throw new CommandException($"{what} is one of {string.Join(", ", values)}.");
        attrs[key] = s;
    }

    /// <summary>Whether a command's arguments set any of the lean settings.</summary>
    public static bool Given(JsonObject a) => a.ContainsKey("lean") || a.ContainsKey("leanFrom") || a.ContainsKey("openingCut");
}

/// <summary>
/// A leaning wall of one layer as IFC's constructive solid (plan-wall-lean-taper, L1): its base footprint swept along its lean,
/// an IfcExtrudedAreaSolid with an oblique ExtrudedDirection (exact, as Revit and ArchiCAD read one); its layer set usage stays.
/// The export keeps the B-rep where that is not the wall's body (its joins cut its ends, it has layers or lintels).
/// </summary>
sealed class LeaningWallBodies : IIfcSweptBodies
{
    public IfcSweptBody? BodyOf(IModelView m, Entity e)
    {
        if (e.Class != IfcClass.Wall || WallLean.Of(m, e) is not { } lean) return null;
        var (a, b) = (Walls.Start(e), Walls.End(e));
        var length = (b - a).Length;
        if (length < 1e-9) return null;
        var bands = Walls.SectionPlumb(m, e);
        var dir = (b - a).Normalized();
        var left = dir.Perp;
        var (y0, y1) = (lean.At(bands.Min(x => x.From), 0), lean.At(bands.Max(x => x.To), 0));
        var sweep = new Vec3(left.X * lean.Tan, left.Y * lean.Tan, 1);
        var reach = sweep.Length;
        var body = new IfcSweep([new Vec2(0, y0), new Vec2(length, y0), new Vec2(length, y1), new Vec2(0, y1)], [], a.At(Spatial.Base(m, e)), new Vec3(dir.X, dir.Y, 0), new Vec3(0, 0, 1),
            Walls.HeightOf(m, e) * reach) { Direction = sweep * (1 / reach) };
        return new IfcSweptBody(body, [], []);
    }
}
