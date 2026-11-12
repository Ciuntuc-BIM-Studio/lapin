using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/// <summary>Where a stair stands: the foot of its first riser, the way the first flight climbs, which way it turns, and the finished floor it starts from.</summary>
public sealed record StairPlacement(Vec2 At, Vec2 Dir, bool TurnLeft, double Z0)
{
    public Vec2 World(Vec2 local) => At + Dir * local.X + Dir.Perp * local.Y;
    public Vec3 World(Vec2 local, double z) { var p = World(local); return new Vec3(p.X, p.Y, Z0 + z); }
}

/// <summary>
/// The stair's parts, plan symbol, quantities and the opening it asks of the
/// floor above, from the type's spec and the instance's attributes: <c>at</c>,
/// <c>rotation</c> (degrees), <c>turn</c>, <c>width</c>, <c>baseOffset</c>,
/// <c>topOffset</c>, <c>topStoreyId</c>, <c>height</c> (when no storey is
/// above), and what it fixes: <c>risers</c>, <c>riserHeight</c>, <c>tread</c>,
/// <c>landing</c>. Monolithic concrete: each flight a waist slab with its
/// steps, the landings slabs, winders stepped prisms; the finish on the
/// treads; the railing as posts and a bar.
/// </summary>
public static class StairBuilder
{
    public static StairSpec SpecOf(IModelView m, Entity stair) =>
        (m.RelatingOf(stair.Id, IfcClass.RelDefinesByType) is { } type ? StairSpec.From(type.Attrs["spec"]) : null) ?? StairSpec.Default();

    public static StairInputs InputsOf(Entity e)
    {
        static int? Whole(double? v) => v is { } x ? (int)Math.Round(x) : null;
        // A winding turn's corners (report #109): the winders at each and the straight treads between a U's.
        return new(e.NumOrNull("width"), Whole(e.NumOrNull("risers")), e.NumOrNull("riserHeight"), e.NumOrNull("tread"), e.NumOrNull("landing"), e.NumOrNull("sweep"),
            Whole(e.NumOrNull("winders")), Whole(e.NumOrNull("windersEnd")), Whole(e.NumOrNull("treadsBetween")));
    }

    /// <summary>The railing's sides for an instance: its own (<c>railingSides</c>), else the type's.</summary>
    public static StairRailing RailingOf(Entity stair, StairSpec spec) =>
        stair.Str("railingSides") is { } sides && StairSpec.Sides.Contains(sides) ? spec.Railing with { Sides = sides } : spec.Railing;

    /// <summary>
    /// The rise of its own the stair climbs, whatever the storeys: the
    /// instance's <c>rise</c>, else its type's (entrance steps); null when it
    /// climbs to a storey.
    /// </summary>
    public static double? OwnRise(IModelView m, Entity stair) =>
        stair.NumOrNull("rise") is { } r && r > 0 ? Math.Max(r, 0.05) : SpecOf(m, stair).Rise;

    /// <summary>Where a stair's base sits by default: a rise of its own from below ends at the storey's level (−rise), one from the level starts at it (0).</summary>
    public static double DefaultBase(double? rise, string riseFrom) => rise is { } r && riseFrom != "level" ? -r : 0.0;

    /// <summary>The storey the stair climbs to: the one named, else the first above its own; null when none, or when it has a rise of its own.</summary>
    public static Entity? TopStorey(IModelView m, Entity stair, Entity storey) =>
        OwnRise(m, stair) != null ? null
        : stair.Str("topStoreyId") is { } id && m.Get(id) is { Class: IfcClass.BuildingStorey } named ? named
        : Spatial.Storeys(m).FirstOrDefault(s => s.Num("elevation") > storey.Num("elevation") + 1e-6);

    /// <summary>
    /// The height to climb: its own rise when it has one (from its base, the
    /// storey's level and the base offset, up); else finished floor to
    /// finished floor with the offsets; without a storey above, the stair's
    /// own height, else the storey's.
    /// </summary>
    public static double Height(IModelView m, Entity stair, Entity storey) => Math.Min(Climb(m, stair, storey), MaxClimb);

    /// <summary>
    /// The most a stair or ramp climbs (m): a storey's level typed in the wrong unit (a tester's report, #73: 3000 m
    /// for 3000 mm) asked thousands of risers of the stair under it and froze the program. Past it, the stair stops
    /// there and says so (<see cref="TooHigh"/>).
    /// </summary>
    public const double MaxClimb = 50;

    /// <summary>The height asked of the stair, before <see cref="MaxClimb"/>.</summary>
    public static double Climb(IModelView m, Entity stair, Entity storey)
    {
        if (OwnRise(m, stair) is { } rise) return rise;
        var top = TopStorey(m, stair, storey)?.Num("elevation") ?? storey.Num("elevation") + (stair.NumOrNull("height") ?? storey.Num("height", 3));
        return top + stair.Num("topOffset") - (storey.Num("elevation") + stair.Num("baseOffset"));
    }

    /// <summary>The diagnostic of a stair or ramp asked to climb more than <see cref="MaxClimb"/>; null when it is not.</summary>
    public static string? TooHigh(IModelView m, Entity stair) =>
        Spatial.StoreyOf(m, stair.Id) is { } storey && Climb(m, stair, storey) is var c && c > MaxClimb
            ? $"It would climb {c:0.##} m to the level above; drawn to {MaxClimb:0} m. Check the storeys' levels."
            : null;

    public static StairPlacement PlacementOf(Entity stair, Entity storey)
    {
        var at = Args.Point(stair.Attrs["at"], "the stair's foot");
        var a = stair.Num("rotation") * Math.PI / 180;
        return new StairPlacement(at, new Vec2(Math.Cos(a), Math.Sin(a)), stair.Str("turn") != "right", storey.Num("elevation") + stair.Num("baseOffset"));
    }

    public static (Resolved Resolved, StairPlacement Place, StairSpec Spec)? Resolve(IModelView m, Entity stair)
    {
        if (Spatial.StoreyOf(m, stair.Id) is not { } storey) return null;
        var spec = SpecOf(m, stair);
        var place = PlacementOf(stair, storey);
        return (StairSolver.Resolve(spec, Height(m, stair, storey), InputsOf(stair), place.TurnLeft), place, spec);
    }

    // ─── Geometry ───────────────────────────────────────────────────────────

    public static ElementGeometry? Geometry(Entity stair, IModelView m)
    {
        if (Resolve(m, stair) is not var (r, place, spec)) return new ElementGeometry([], Diagnostics: ["The stair stands on no storey."]);
        var f = spec.Finish.TreadThickness;
        var waist = spec.Construction.Waist;
        var concrete = Materials.Find(m, spec.Construction.Concrete)?.Id;
        var finishMat = Materials.Find(m, spec.Finish.TreadMaterial)?.Id;
        var concreteColor = concrete != null ? Materials.ColorOf(m, concrete) : "#a8a8a8";
        var finishColor = finishMat != null ? Materials.ColorOf(m, finishMat) : "#d9d2c5";
        var parts = new List<GeometryPart>();
        var problems = new List<string>();
        var finish = new List<Solid>();
        var w = r.Width;
        var solidKind = spec.Construction.Kind == "solid";
        // Open treads (floating from a wall, or on stringers): each step a plate of the waist's thickness, no slab under them.
        var plates = spec.Construction.Kind is "treads" or "stringers";

        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            var fw = r.WidthOf(fl);
            // A flight of one riser has no tread: its step is the edge of what it climbs to (a porch, a landing).
            if (fl.Risers < 2) { if (r.Flights.Count == 1) problems.Add("One riser: the step is the edge of the floor it climbs to. Give it two risers or more for treads in front."); continue; }
            if (plates)
            {
                var treadSolids = new List<Solid>();
                for (var j = 1; j < fl.Risers; j++)
                {
                    var l = fl.Left * (fw / 2);
                    Vec2[] tp = [place.World(fl.Start + fl.Dir * ((j - 1) * r.Tread - spec.Finish.Nosing) - l), place.World(fl.Start + fl.Dir * (j * r.Tread) - l), place.World(fl.Start + fl.Dir * (j * r.Tread) + l), place.World(fl.Start + fl.Dir * ((j - 1) * r.Tread - spec.Finish.Nosing) + l)];
                    if (Prism.ExtrudeFootprint(tp, place.Z0 + fl.Z0 + j * r.RiserHeight - f - waist, waist, "tread") is { } t) treadSolids.Add(t);
                }
                if (spec.Construction.Kind == "stringers")
                    foreach (var sgn in new[] { -1.0, 1.0 })
                        if (Stringer(r, fl, place, sgn, f) is { } st) treadSolids.Add(st);
                if (treadSolids.Count > 0) parts.Add(new GeometryPart($"Flight {i + 1}", concreteColor, Openings.Combine(treadSolids, "flight"), concrete, $"flight:{i}"));
            }
            else if (FlightSolid(r, fl, place, f, waist, solidKind) is { } solid) parts.Add(new GeometryPart($"Flight {i + 1}", concreteColor, solid, concrete, $"flight:{i}"));
            else problems.Add($"Flight {i + 1} could not be built.");
            if (f > 0)
                for (var j = 1; j < fl.Risers; j++)
                {
                    var l = fl.Left * (fw / 2);
                    Vec2[] tread = [place.World(fl.Start + fl.Dir * ((j - 1) * r.Tread) - l), place.World(fl.Start + fl.Dir * (j * r.Tread) - l), place.World(fl.Start + fl.Dir * (j * r.Tread) + l), place.World(fl.Start + fl.Dir * ((j - 1) * r.Tread) + l)];
                    if (Prism.ExtrudeFootprint(tread, place.Z0 + fl.Z0 + j * r.RiserHeight - f, f, "finish") is { } s) finish.Add(s);
                }
        }
        var winders = new List<Solid>();
        foreach (var wd in r.Winders)
        {
            var outline = wd.Outline.Select(p => place.World(p)).ToArray();
            var top = place.Z0 + wd.Z - f;
            var bottom = plates ? top - waist : top - r.RiserHeight - waist;
            if (Prism.ExtrudeFootprint(outline, bottom, top - bottom, "winder") is { } s) winders.Add(s);
            if (f > 0 && Prism.ExtrudeFootprint(outline, top, f, "finish") is { } fs) finish.Add(fs);
        }
        if (winders.Count > 0) parts.Add(new GeometryPart(r.Arc != null ? "Treads" : "Winders", concreteColor, Openings.Combine(winders, "winders"), concrete, "winders"));
        // A spiral turns round a pole from the floor to the handrail's height over its top.
        if (spec.Shape == "spiral" && r.Arc is { } arc)
        {
            var c = place.World(arc.Centre);
            var radius = Math.Max(0.03, Math.Min(0.08, arc.Inner * 0.8));
            if (Prism.Cylinder(c, place.Z0, radius, r.Height + spec.Railing.Height, 16, "pole") is { } pole)
                parts.Add(new GeometryPart("Pole", "#5a5a5a", pole, null, "pole"));
        }
        for (var i = 0; i < r.Landings.Count; i++)
        {
            var ld = r.Landings[i];
            var outline = ld.Outline.Select(p => place.World(p)).ToArray();
            var t = spec.Construction.LandingThickness;
            if (Prism.ExtrudeFootprint(outline, place.Z0 + ld.Z - f - t, t, "landing") is { } s) parts.Add(new GeometryPart($"Landing {i + 1}", concreteColor, s, concrete, $"landing:{i}"));
            if (f > 0 && Prism.ExtrudeFootprint(outline, place.Z0 + ld.Z - f, f, "finish") is { } fs) finish.Add(fs);
        }
        if (finish.Count > 0) parts.Add(new GeometryPart("Tread finish", finishColor, Openings.Combine(finish, "finish"), finishMat, "finish"));
        var treads = r.Flights.Sum(x => x.Risers - 1) + r.Winders.Count;
        var rail = RailingOf(stair, spec);
        foreach (var side in new[] { "left", "right" })
            if (treads > 0 && (rail.Sides == side || rail.Sides == "both"))
            {
                var (solids, glass) = RailingSolids(r, place, rail, side == "left");
                if (solids.Count > 0) parts.Add(new GeometryPart($"Railing ({side})", rail.Color ?? "#4a4a4a", Openings.Combine(solids, "railing"), null, $"railing:{side}"));
                if (glass.Count > 0) parts.Add(new GeometryPart($"Railing glass ({side})", "#9cc8e8", Openings.Combine(glass, "glass"), null, $"railing-glass:{side}"));
            }

        var plan = Plan(r, place);
        var foot = r.Foot;
        Vec2[] snaps = [place.World(foot.At - foot.Left * (foot.Width / 2)), place.World(foot.At + foot.Left * (foot.Width / 2)), place.World(r.Top.At)];
        foreach (var c in r.Checks.Where(c => !c.Ok)) problems.Add($"{c.Label} {Fmt(c.Value)} is outside {Fmt(c.Min)} … {Fmt(c.Max)}.");
        if (TooHigh(m, stair) is { } high) problems.Add(high);
        return new ElementGeometry(parts, plan, snaps, problems.Count > 0 ? problems : null);
    }

    static string Fmt(double? v) => v is { } x ? (x >= 1 ? x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : (x * 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " mm") : "—";

    /// <summary>
    /// A flight as one solid: the stepped top, the vertical cut where it meets
    /// the landing (or the floor above), the soffit a waist under the step
    /// corners, the bearing on the floor; the concrete a finish below the
    /// finished surfaces. Solid, the steps are filled down to the flight's
    /// base instead (steps cast on the ground). The profile in the flight's
    /// plane, extruded across.
    /// </summary>
    public static Solid? FlightSolid(Resolved r, Flight fl, StairPlacement place, double finish, double waist, bool solid = false)
    {
        var (k, h, b) = (fl.Risers, r.RiserHeight, r.Tread);
        if (k < 2) return null;
        var wv = waist * Math.Sqrt(h * h + b * b) / b;
        var profile = new List<(double U, double Z)> { (0, -finish) };
        for (var i = 1; i <= k; i++)
        {
            profile.Add(((i - 1) * b, i * h - finish));
            if (i < k) profile.Add((i * b, i * h - finish));
        }
        var uEnd = (k - 1) * b;
        if (solid) profile.Add((uEnd, -finish));
        else
        {
            profile.Add((uEnd, (k - 1) * h - wv - finish));
            profile.Add((Math.Min(wv * b / h, Math.Max(uEnd - 0.01, 0)), -finish));
        }
        var right = fl.Start - fl.Left * (r.WidthOf(fl) / 2);
        var ring = profile.Select(p => place.World(right + fl.Dir * p.U, fl.Z0 + p.Z)).ToList();
        var across = place.World(fl.Left * r.WidthOf(fl), 0) - place.World(new Vec2(0, 0), 0);
        return Oriented(ring, across, "flight");
    }

    /// <summary>A stringer along one side of a flight (<paramref name="sign"/> −1 right, +1 left): a plate 40 mm thick, 250 mm deep, under the treads' nosing line.</summary>
    static Solid? Stringer(Resolved r, Flight fl, StairPlacement place, double sign, double finish)
    {
        const double t = 0.04, depth = 0.25;
        var (k, h, b) = (fl.Risers, r.RiserHeight, r.Tread);
        var drop = depth * Math.Sqrt(h * h + b * b) / b;
        var side = fl.Start + fl.Left * (sign * (r.WidthOf(fl) / 2 + (sign > 0 ? 0 : t)));
        var run = (k - 1) * b;
        // From the floor at the foot to the top tread, its top along the treads' fronts, its bottom a depth under.
        List<(double U, double Z)> profile = [(-b * 0.5, 0), (-b * 0.5, h * 0.5), (run, k * h - finish), (run, k * h - finish - drop), (drop * b / h - b * 0.5, 0)];
        var ring = profile.Select(p => place.World(side + fl.Dir * p.U, fl.Z0 + p.Z)).ToList();
        var across = place.World(fl.Left * -t, 0) - place.World(new Vec2(0, 0), 0);
        return Oriented(ring, across, "stringer");
    }

    /// <summary>A profile extruded so the solid comes out with positive volume, whichever way the ring runs.</summary>
    internal static Solid? Oriented(List<Vec3> ring, Vec3 across, string tag)
    {
        var s = Prism.Extrude(ring, null, across, tag);
        if (s == null || Measure.SignedVolume(s) >= 0) return s;
        ring.Reverse();
        return Prism.Extrude(ring, null, across, tag);
    }

    /// <summary>
    /// The railing's paths on one side: points along the nosing line with the surface under them, the
    /// bar over the flights, level over a landing, climbing over winders; around the outer edge of a turn,
    /// straight across the inner one; a spiral's or curved stair's along its arc, step by step; a split
    /// stair's on each flight's own side.
    /// </summary>
    static List<List<(Vec2 P, double Surface)>> RailingPaths(Resolved r, StairPlacement place, StairRailing rail, bool left)
    {
        var inset = Math.Clamp(rail.Inset, 0, 0.3);
        var sign = left ? 1 : -1;
        if (r.Arc is { } arc)
        {
            // Left as one climbs is the centre's side when it turns left.
            var inward = left == (arc.Step > 0);
            var radius = inward ? arc.Inner + inset : arc.Outer - inset;
            var p = new List<(Vec2, double)>();
            for (var k = 0; k <= arc.Steps; k++)
            {
                var a = arc.Angle(k);
                p.Add((arc.At(a, radius), (k + 1) * r.RiserHeight));
                if (k < arc.Steps) p.Add((arc.At((arc.Angle(k) + arc.Angle(k + 1)) / 2, radius), (k + 1) * r.RiserHeight));
            }
            return [p];
        }
        if (r.Spec.Shape == "split")
            return r.Flights.Select(fl =>
            {
                var side = fl.Left * (sign * (r.WidthOf(fl) / 2 - inset));
                var run = (fl.Risers - 1) * r.Tread;
                return new List<(Vec2, double)> { (fl.Start + side, fl.Z0 + r.RiserHeight), (fl.Start + fl.Dir * run + side, fl.Z0 + fl.Risers * r.RiserHeight) };
            }).Where(x => (x[1].Item1 - x[0].Item1).Length > 1e-6).ToList();
        var w = r.Width;
        var path = new List<(Vec2 P, double Surface)>();
        // A point on the path, once (a U's corners with no tread between them meet on one riser line).
        void Add(Vec2 p, double surface) { if (path.Count == 0 || (path[^1].P - p).Length > 1e-6) path.Add((p, surface)); }
        // A U turning at two corners of its own (report #109) turns twice as an L does, the short side's treads between.
        var quarter = r.Spec.Shape == "l" || (r.Spec.Shape == "u" && r.Flights.Count == 3);
        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            var side = fl.Left * (sign * (w / 2 - inset));
            var run = (fl.Risers - 1) * r.Tread;
            var zTop = fl.Z0 + fl.Risers * r.RiserHeight;
            Add(fl.Start + side, fl.Z0 + r.RiserHeight);
            var end = fl.Start + fl.Dir * run + side;
            Add(end, zTop);
            if (i + 1 < r.Flights.Count)
            {
                var next = r.Flights[i + 1];
                var start2 = next.Start + next.Left * (sign * (w / 2 - inset));
                var zTurn = next.Z0;
                // The turn's depth along the first flight: a landing's length, or the width when it turns in a square (winders too).
                var depth = !quarter && (r.Spec.Shape == "straight-landing" || (r.Spec.Shape == "u" && r.Winders.Count == 0)) ? r.LandingLength : w;
                var outer = r.Spec.Turns_ && left != place.TurnLeft;
                if (quarter && outer) Add(end + fl.Dir * (depth - inset), (zTop + zTurn) / 2);
                else if (r.Spec.Shape == "u" && outer)
                {
                    Add(end + fl.Dir * (depth - inset), zTop + (zTurn - zTop) / 3);
                    Add(start2 + fl.Dir * (depth - inset), zTop + (zTurn - zTop) * 2 / 3);
                }
                Add(start2, zTurn + r.RiserHeight);
            }
        }
        // The handrail runs on level past the first and the last step when asked.
        if (rail.Extension > 0 && path.Count >= 2)
        {
            var d0 = (path[0].P - path[1].P).Normalized();
            var d1 = (path[^1].P - path[^2].P).Normalized();
            path.Insert(0, (path[0].P + d0 * rail.Extension, path[0].Surface - r.RiserHeight));
            path.Add((path[^1].P + d1 * rail.Extension, path[^1].Surface));
        }
        return [path];
    }

    /// <summary>
    /// The top of the stair under a post (world <paramref name="at"/>, <paramref name="half"/> its half size; null where there is
    /// none): the highest of the tread, landing or winder under its centre and its corners, so a post over a nosing stands on the
    /// upper tread.
    /// </summary>
    static double? GroundUnder(Resolved r, StairPlacement place, Vec3 at, double half)
    {
        double? best = null;
        var c = new Vec2(at.X, at.Y);
        var k = half * 0.9;
        foreach (var w in (Vec2[])[c, c + new Vec2(-k, -k), c + new Vec2(k, -k), c + new Vec2(k, k), c + new Vec2(-k, k)])
        {
            var d = w - place.At;
            var p = new Vec2(Vec2.Dot(d, place.Dir), Vec2.Dot(d, place.Dir.Perp));
            if (SurfaceAt(r, p) is { } z && (best is not { } b || z > b)) best = z;
        }
        return best is { } top ? place.Z0 + top : null;
    }

    /// <summary>The finished surface of the stair at a point of its own frame (above its foot), or null outside it.</summary>
    static double? SurfaceAt(Resolved r, Vec2 p)
    {
        const double eps = 1e-6;
        double? best = null;
        void Take(double z) { if (best is not { } b || z > b) best = z; }
        // Behind the first riser the floor; just past the last the floor above (a little way only: a spiral's top may stand over its foot).
        var foot = r.Foot;
        if (Vec2.Dot(p - foot.At, foot.Dir) < eps && Math.Abs(Vec2.Dot(p - foot.At, foot.Left)) <= foot.Width / 2 + eps) Take(0);
        var (topAt, topDir) = r.Top;
        var past = Vec2.Dot(p - topAt, topDir);
        if (past > -eps && past < 0.3 && Math.Abs(Vec2.Dot(p - topAt, topDir.Perp)) <= r.Width / 2 + eps) Take(r.Height);
        foreach (var fl in r.Flights)
        {
            var d = p - fl.Start;
            var (u, v) = (Vec2.Dot(d, fl.Dir), Vec2.Dot(d, fl.Left));
            if (Math.Abs(v) > r.WidthOf(fl) / 2 + eps) continue;
            if (u < -eps || u > (fl.Risers - 1) * r.Tread + eps) continue;
            // Tread s + 1 lies between nosings s and s + 1; the last nosing is the floor above (or the landing).
            var s = Math.Clamp((int)Math.Floor(u / r.Tread + eps), 0, fl.Risers - 1);
            Take(fl.Z0 + (s + 1) * r.RiserHeight);
        }
        foreach (var ld in r.Landings) if (Polygon2.Contains(ld.Outline, p)) Take(ld.Z);
        foreach (var wd in r.Winders) if (Polygon2.Contains(wd.Outline, p)) Take(wd.Z);
        return best;
    }

    /// <summary>A round bar from a to b (an octagon swept along), <paramref name="d"/> across.</summary>
    internal static Solid? RoundBar(Vec3 a, Vec3 b, double d, string tag)
    {
        var axis = b - a;
        if (axis.Length < 1e-6) return null;
        var u = axis * (1 / axis.Length);
        var x = Math.Abs(u.Z) < 0.9 ? Vec3.Cross(u, new Vec3(0, 0, 1)) : Vec3.Cross(u, new Vec3(1, 0, 0));
        x = x * (1 / x.Length);
        var y = Vec3.Cross(u, x);
        var ring = Enumerable.Range(0, 8).Select(i => { var t = Math.PI * (i + 0.5) / 4; return a + x * (Math.Cos(t) * d / 2) + y * (Math.Sin(t) * d / 2); }).ToList();
        return Prism.Extrude(ring, null, axis, tag);
    }

    /// <summary>
    /// A railing's solids on one side, in its style: the handrail (square or round) along each path, then
    /// posts at their spacing (the bar style, glass and cables between them), balusters at most their
    /// spacing apart, or a solid panel; glass apart, to be drawn as glass.
    /// </summary>
    static (List<Solid> Metal, List<Solid> Glass) RailingSolids(Resolved r, StairPlacement place, StairRailing rail, bool left)
    {
        var metal = new List<Solid>();
        var glass = new List<Solid>();
        // Each path along its surface, built as any railing is (Railings, shared with the balconies').
        foreach (var path in RailingPaths(r, place, rail, left))
        {
            var (m, g) = Railings.Solids(path.Select(p => place.World(p.P, p.Surface)).ToList(), rail, (at, half) => GroundUnder(r, place, at, half));
            metal.AddRange(m);
            glass.AddRange(g);
        }
        return (metal, glass);
    }

    // ─── Plan ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The stair's plan lines, whole from the foot to the top (<see cref="StairPlan"/> with no cut): what a plan picks and
    /// snaps to, and what the interface draws while no drawing is asked for. A plan's drawing draws the stair from its
    /// marks instead (<see cref="StairMarks"/>), cut at the view's own cut plane (report #143), so these are of a kind
    /// the drawing leaves alone (<c>outline</c>).
    /// </summary>
    static List<PlanItem> Plan(Resolved r, StairPlacement place)
    {
        var symbol = StairPlan.Of(r, null);
        return symbol.Lines.Concat(symbol.Walk).Select(l => new PlanItem("outline", l.Closed, l.Points.Select(p => place.World(p)).ToArray())).ToList();
    }

    // ─── Quantities ─────────────────────────────────────────────────────────

    public static IEnumerable<QuantitySet> Quantities(Entity stair, ElementGeometry? g, IModelView m)
    {
        if (g == null || Resolve(m, stair) is not var (r, _, spec)) yield break;
        double Vol(string key) => g.Parts.Where(p => p.Key == key).Sum(p => Measure.Volume(p.Solid));
        var w = r.Width;
        var run = 0.0; var soffit = 0.0; var treads = 0.0;
        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            var length = (fl.Risers - 1) * r.Tread;
            run += length;
            // Solid on the ground, a flight has no soffit to form: its two sides instead.
            var fw = r.WidthOf(fl);
            soffit += spec.Construction.Kind == "solid" ? 2 * length * fl.Risers * r.RiserHeight / 2 : fw * length * Math.Sqrt(1 + r.RiserHeight * r.RiserHeight / (r.Tread * r.Tread));
            treads += (fl.Risers - 1) * r.Tread * fw;
            yield return new QuantitySet("Qto_StairFlightBaseQuantities",
            [
                new("Length", length, "LENGTH"), new("NumberOfRiser", fl.Risers, "COUNT"), new("NumberOfTreads", fl.Risers - 1, "COUNT"),
                new("RiserHeight", r.RiserHeight, "LENGTH"), new("TreadLength", r.Tread, "LENGTH"), new("GrossVolume", Vol($"flight:{i}"), "VOLUME"),
            ], $"flight:{i}", $"Flight {i + 1}", spec.Construction.Concrete);
        }
        var landingArea = r.Landings.Sum(l => Math.Abs(Polygon2.SignedArea(l.Outline)));
        var winderArea = r.Winders.Sum(x => Math.Abs(Polygon2.SignedArea(x.Outline)));
        var concrete = g.Parts.Where(p => p.Key != null && (p.Key.StartsWith("flight:", StringComparison.Ordinal) || p.Key == "winders" || p.Key.StartsWith("landing:", StringComparison.Ordinal))).Sum(p => Measure.Volume(p.Solid));
        var railing = 0.0;
        foreach (var fl in r.Flights) railing += (fl.Risers - 1) * Math.Sqrt(r.Tread * r.Tread + r.RiserHeight * r.RiserHeight);
        if (r.Arc is { } arc) { run += arc.Steps * r.Tread; railing += arc.Steps * Math.Sqrt(r.Tread * r.Tread + r.RiserHeight * r.RiserHeight); }
        var rail = RailingOf(stair, spec);
        var sides = rail.Sides == "both" ? 2 : rail.Sides == "none" ? 0 : 1;
        yield return new QuantitySet("Lapin_StairQuantities",
        [
            new("NumberOfRiser", r.Risers, "COUNT", "Risers"), new("RiserHeight", r.RiserHeight, "LENGTH", "Riser"), new("TreadLength", r.Tread, "LENGTH", "Tread"),
            new("Width", w, "LENGTH"), new("Height", r.Height, "LENGTH"), new("Length", run, "LENGTH", "Run in plan"),
            new("ConcreteVolume", concrete, "VOLUME", "Concrete"),
            new("FormworkArea", soffit + r.Risers * r.RiserHeight * w + landingArea + winderArea, "AREA", "Formwork"),
            new("FinishArea", treads + landingArea + winderArea, "AREA", "Tread finish"),
            new("RailingLength", railing * sides, "LENGTH", "Railing"),
        ]);
    }

    // ─── The opening in the floor above ─────────────────────────────────────

    /// <summary>What the stair (or ramp) asks of the floor above: the box, in plan, of everything that comes within the headroom of that floor's underside.</summary>
    public static IReadOnlyList<Vec2>? OpeningContour(IModelView m, Entity stair)
    {
        if (TopPoint(m, stair) is not { } topAt || Spatial.StoreyOf(m, stair.Id) is not { } storey || TopStorey(m, stair, storey) is not { } top) return null;
        var slab = SlabUnder(m, top, topAt);
        var thickness = slab != null ? Slabs.Section(m, slab).Sum(s => s.To - s.From) : 0.2;
        if (stair.Class == IfcClass.Ramp) return RampBuilder.OpeningContour(m, stair, thickness);
        if (Resolve(m, stair) is not var (r, place, spec)) return null;
        var limit = r.Height - thickness - spec.Rules.Headroom;
        var pts = new List<Vec2>();
        foreach (var fl in r.Flights)
            for (var i = 1; i <= fl.Risers; i++)
                if (fl.Z0 + i * r.RiserHeight > limit)
                {
                    var l = fl.Left * (r.WidthOf(fl) / 2);
                    var u0 = (i - 1) * r.Tread; var u1 = Math.Min(i, fl.Risers - 1) * r.Tread;
                    pts.AddRange([fl.Start + fl.Dir * u0 - l, fl.Start + fl.Dir * u0 + l, fl.Start + fl.Dir * u1 - l, fl.Start + fl.Dir * u1 + l]);
                }
        foreach (var ld in r.Landings) if (ld.Z > limit) pts.AddRange(ld.Outline);
        foreach (var wd in r.Winders) if (wd.Z > limit) pts.AddRange(wd.Outline);
        if (pts.Count < 3) return null;
        double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
        return [place.World(new Vec2(x0, y0)), place.World(new Vec2(x1, y0)), place.World(new Vec2(x1, y1)), place.World(new Vec2(x0, y1))];
    }

    /// <summary>Where a stair or a ramp arrives, in plan.</summary>
    static Vec2? TopPoint(IModelView m, Entity e) =>
        e.Class == IfcClass.Ramp
            ? RampBuilder.Resolve(m, e) is var (rr, rp, _) ? rp.World(rr.Top.At) : null
            : Resolve(m, e) is var (r, place, _) ? place.World(r.Top.At) : null;

    static Entity? SlabUnder(IModelView m, Entity storey, Vec2 at) =>
        m.OfClass(IfcClass.Slab).Where(s => Spatial.StoreyOf(m, s.Id)?.Id == storey.Id)
            .FirstOrDefault(s => Slabs.Contour(s) is { Count: >= 3 } c && Polygon2.Contains(c, at));

    /// <summary>The opening linked to the stair, if any: an IfcOpeningElement with <c>stairId</c>; its outline follows the stair.</summary>
    public static Entity? OpeningOf(IModelView m, string stairId) =>
        m.OfClass(IfcClass.OpeningElement).FirstOrDefault(o => o.Str("stairId") == stairId);

    /// <summary>The opening the floor above gets (or loses): made in the slab under the stair's top, unless the stair says <c>noOpening</c>; moved when the stair now ends under another slab.</summary>
    public static void EnsureOpening(ICommandContext ctx, Entity stair)
    {
        var m = ctx.Model;
        var existing = OpeningOf(m, stair.Id);
        Entity? slab = null;
        if (!stair.Bool("noOpening") && TopPoint(m, stair) is { } at && Spatial.StoreyOf(m, stair.Id) is { } storey && TopStorey(m, stair, storey) is { } top)
            slab = SlabUnder(m, top, at);
        if (slab == null || OpeningContour(m, stair) == null)
        {
            if (existing != null) ctx.Remove(existing.Id);
            return;
        }
        if (existing != null && m.RelatingOf(existing.Id, IfcClass.RelVoidsElement)?.Id == slab.Id) return;
        if (existing != null) ctx.Remove(existing.Id);
        var opening = new Entity(ctx.NewId(), IfcClass.OpeningElement, stair.Class == IfcClass.Ramp ? "Ramp opening" : "Stair opening", new JsonObject { ["stairId"] = stair.Id }, "OPENING");
        ctx.Add(opening);
        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, slab.Id, [opening.Id]));
    }
}

/// <summary>
/// How a plan draws its storey's stairs: the symbol cut at the view's own cut plane (report #143, <see cref="StairPlan"/>),
/// in the stairs' line styles (seen under the cut, overhead over it), and what it writes on them, as the type asks
/// (<see cref="StairPlanSymbols"/>; report #46, decision 190): each step's number in a circle beside the walking line, from
/// the first riser up (the last one on what it climbs to), "UP" at the foot of the arrow, and a label by the foot with the
/// risers, the riser and tread, and the width. Drawn by the engine as marks, so the plan on screen, a sheet and its PDF are one.
/// </summary>
public static class StairMarks
{
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan") yield break;
        var m = c.Model;
        var storeyId = c.View.Str("storeyId");
        var hidden = (c.View.Attrs["hiddenElements"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var graphics = VisibilityGraphics.Of(m, c.View);
        var style = Standards.TextOf(c.Book, Standards.DefaultRoomTag.TextStyle);
        // A site plan draws the stairs by their bodies, seen from above: no symbol, no cut.
        var site = SitePlans.Of(c.View) != null;
        double? cut = site ? null : Views.Range(m, c.View).Cut;
        var filters = c.Services?.All<IViewElementFilter>() ?? [];
        var stairs = m.OfClass(IfcClass.Stair).Where(s => !hidden.Contains(s.Id) && !graphics.Hides(s) && Spatial.StoreyOf(m, s.Id)?.Id == storeyId).ToList();
        if (stairs.Count == 0) yield break;
        var pens = new StairPens(c, graphics, stairs);
        foreach (var stair in stairs)
        {
            // A view's filter (a phase) may hide it, or draw it in its colour.
            var filtered = filters.Select(f => f.For(m, c.View, stair)).FirstOrDefault(x => x != null);
            if (filtered is { Hide: true }) continue;
            if (StairBuilder.Resolve(m, stair) is not var (r, place, spec)) continue;
            var symbols = spec.Symbols ?? new StairPlanSymbols();
            var mark = new ViewMark(stair.Id);
            Vec2 V(Vec2 local) => c.Map(place.World(local), place.Z0);
            var hc = cut - place.Z0;
            var plan = StairPlan.Of(r, hc, symbols.AboveCut, symbols.Break, 3 * c.Mm);
            if (!site)
            {
                var seen = pens.Of(stair, LineRole.Projection, filtered?.Color);
                var over = pens.Of(stair, LineRole.Overhead, filtered?.Color);
                foreach (var line in plan.Lines.Concat(plan.Walk))
                {
                    var (w, ink, dash) = line.Above ? over : seen;
                    mark.Polyline(line.Points.Select(V).ToList(), line.Closed, line.Above ? "STAIR-OVERHEAD" : "STAIR", w, ink, dash);
                }
                foreach (var br in plan.Breaks) mark.Polyline(br.Select(V).ToList(), false, "STAIR", seen.Width, seen.Color, null);
            }
            var color = style.Color ?? "#000000";
            var h = Math.Max(1.8, style.Height * 0.8);
            if (symbols.Numbers)
            {
                var (radius, gap) = NumberSize(r, h * c.Mm * 0.9, c.Mm);
                var nh = radius / (c.Mm * 0.9);
                foreach (var (at, n) in NumberSpots(r, radius, gap, plan.Paths))
                {
                    // Over the cut and not drawn: no number either.
                    if (!site && symbols.AboveCut == "hidden" && hc is { } top && n * r.RiserHeight > top + 1e-9) continue;
                    var p = V(at);
                    mark.Polyline(ViewMark.Circle(p, radius, 20), true, "STAIR-NUMBERS", Pens.Default.Hatch, color);
                    mark.Text(new PenText(p, n.ToString(System.Globalization.CultureInfo.InvariantCulture), nh, 0, "middle", "middle", false, style.Font), color);
                }
            }
            var foot = r.Foot;
            if (symbols.Up)
                mark.Text(new PenText(V(foot.At - foot.Dir * (r.Tread * 0.8)), "UP", h, 0, "middle", "middle", true, style.Font), color);
            if (symbols.Label)
            {
                var Mm = Schedules.ScheduleLayout.Mm;
                string[] lines = [$"{r.Risers} × {Mm(r.RiserHeight)}", $"{Mm(r.Tread)}", $"{Mm(r.Width)}"];
                string[] keys = ["Risers", "Tread", "Width"];
                var at = V(foot.At - foot.Left * (foot.Width / 2 + 0.25) - foot.Dir * 0.2);
                for (var i = 0; i < lines.Length; i++)
                    mark.Text(new PenText(at + new Vec2(0, -h * c.Mm * 1.5 * i), $"{keys[i]} {lines[i]}", h, 0, "end", "middle", i == 0, style.Font), color);
            }
            if (mark.Texts.Count > 0 || mark.Lines.Count > 0) yield return mark;
        }
    }

    /// <summary>
    /// A step number's circle (m), at most <paramref name="wanted"/>, and its gap from the walking line: small enough to
    /// fit in a tread (four tenths of it) and, beside the walking line, in half the narrowest flight.
    /// </summary>
    public static (double Radius, double Gap) NumberSize(Resolved r, double wanted, double gap)
    {
        var narrowest = r.Flights.Count > 0 ? Math.Min(r.Width, r.Flights.Min(r.WidthOf)) : r.Width;
        gap = Math.Min(gap, narrowest / 10);
        var radius = Math.Min(wanted, Math.Min(0.4 * r.Tread, (narrowest / 2 - gap) / 2.1));
        return (Math.Max(radius, 0.01), gap);
    }

    /// <summary>
    /// Where each step's number goes in the stair's frame (report #143): beside the walking line, not on it, its circle a
    /// <paramref name="gap"/> clear of it: a flight's on the outer side of the turn (a straight stair's on the right going
    /// up, <see cref="StairPlan.NumbersOnLeft"/>), a spiral's or curved stair's outwards, a winder's pushed off the line on its own side.
    /// </summary>
    public static List<(Vec2 At, int Number)> NumberSpots(Resolved r, double radius, double gap, IReadOnlyList<Vec2[]> paths)
    {
        var off = radius + gap;
        var onLeft = StairPlan.NumbersOnLeft(r);
        var o = new List<(Vec2, int)>();
        foreach (var (at, n) in StepCentres(r))
        {
            Vec2 p;
            if (r.Arc is { } arc)
            {
                var radial = (at - arc.Centre).Normalized();
                p = arc.Centre + radial * (arc.Walk + off);
            }
            else if (FlightOf(r, at) is { } fl)
            {
                // A split stair's return flights: each away from the middle.
                var side = r.Spec.Shape == "split" ? (Vec2.Dot(fl.Start - r.Flights[0].Start, fl.Left) is var d && Math.Abs(d) > 1e-6 ? (d > 0 ? fl.Left : fl.Left * -1) : fl.Left * -1)
                    : onLeft ? fl.Left : fl.Left * -1;
                p = at + side * off;
            }
            else p = at;
            // Whatever it stands on, never on the walking line: pushed off it, square to it.
            foreach (var path in paths)
                for (var i = 0; i + 1 < path.Length; i++)
                {
                    var (a, b) = (path[i], path[i + 1]);
                    var ab = b - a;
                    if (ab.Length < 1e-9) continue;
                    var t = Math.Clamp(Vec2.Dot(p - a, ab) / Vec2.Dot(ab, ab), 0, 1);
                    var q = a + ab * t;
                    var away = p - q;
                    if (away.Length >= off - 1e-9) continue;
                    var dir = away.Length > 1e-9 ? away.Normalized() : ab.Normalized().Perp * -1;
                    p = q + dir * off;
                }
            o.Add((p, n));
        }
        return o;
    }

    /// <summary>The flight a point on its centre line belongs to (a tread's middle, or the last number half a tread past the top); null on winders.</summary>
    static Flight? FlightOf(Resolved r, Vec2 at)
    {
        foreach (var fl in r.Flights)
        {
            var d = at - fl.Start;
            var (u, v) = (Vec2.Dot(d, fl.Dir), Vec2.Dot(d, fl.Left));
            if (Math.Abs(v) < 1e-6 && u > -1e-9 && u <= (fl.Risers - 0.5) * r.Tread + 1e-6) return fl;
        }
        return null;
    }

    /// <summary>Where each step's number goes, in the stair's frame: the middle of its tread (a winder's or a spiral's wedge too), numbered by its height; the last riser's on what it climbs to, half a tread on (a split stair's two return flights have the same numbers).</summary>
    public static List<(Vec2 At, int Number)> StepCentres(Resolved r)
    {
        var o = new List<(Vec2, int)>();
        int N(double z) => (int)Math.Round(z / r.RiserHeight);
        if (r.Arc is { } arc)
        {
            for (var k = 0; k < arc.Steps; k++) o.Add((arc.At((arc.Angle(k) + arc.Angle(k + 1)) / 2, arc.Walk), k + 1));
            o.Add((arc.At(arc.Angle(arc.Steps) + arc.Step / 2, arc.Walk), arc.Steps + 1));
            return o;
        }
        for (var f = 0; f < r.Flights.Count; f++)
        {
            var fl = r.Flights[f];
            // Before winders the flight's last riser lands on the first winder: its number is the winder's (a U's short side too, report #109).
            var last = r.Winders.Count > 0 && f < r.Flights.Count - 1 ? fl.Risers - 1 : fl.Risers;
            for (var i = 1; i <= last; i++) o.Add((fl.Start + fl.Dir * ((i - 0.5) * r.Tread), N(fl.Z0) + i));
        }
        foreach (var wd in r.Winders) o.Add((Centroid(wd.Outline), N(wd.Z)));
        return o;
    }

    static Vec2 Centroid(IReadOnlyList<Vec2> pts) => new(pts.Average(p => p.X), pts.Average(p => p.Y));
}

/// <summary>
/// The pens a plan draws its stairs' symbols with, as the drawing styles any element's lines (Pens): the view's element
/// and category overrides, then the project's object styles for stairs, the view's category colour and weight, a filter's
/// colour or halftone; else a seen line (0.25 mm, black) under the cut and an overhead one (dashed, grey) over it.
/// </summary>
sealed class StairPens
{
    readonly LineStyles.Resolver? styles;
    readonly CategoryGraphics? category;
    readonly VisibilityGraphics vg;

    public StairPens(ViewContext c, VisibilityGraphics vg, IEnumerable<Entity> stairs)
    {
        this.vg = vg;
        var project = Spatial.Project(c.Model);
        var graphics = GraphicsSets.Of(c.Model, c.View);
        var elements = graphics?["elements"]?.DeepClone() as JsonObject;
        foreach (var s in stairs)
            if (vg.LinesOf(s) is { } lines && !(elements?.ContainsKey(s.Id) ?? false)) (elements ??= [])[s.Id] = lines;
        styles = LineStyles.For(project?.Attrs["objectStyles"] as JsonObject, graphics?["categories"] as JsonObject, elements, LineStyles.ReadTable(project?.Attrs["penTable"]), c.Scale);
        category = CategoryGraphics.From(graphics?["categories"]?["stairs"]);
    }

    public (double Width, string Color, double[]? Dash) Of(Entity stair, string role, string? filterColor)
    {
        var styled = styles?.Invoke(role, stair.Class, stair.Id);
        var color = filterColor ?? vg.ColorOf(stair) ?? styled?.Color ?? category?.LineColor;
        if (role == LineRole.Overhead)
            return (styled?.WidthMm ?? Pens.Default.Seen * 0.7, color ?? "#888888", styled?.Dash is { Length: > 0 } od ? od : [2, 1]);
        var w = styled?.WidthMm ?? (Pens.WeightMm(category?.LineWeight) is { } mm ? Pens.SeenWeight(mm) : Pens.Default.Seen);
        return (w, color ?? "#000000", styled?.Dash is { Length: > 0 } sd ? sd : null);
    }
}
