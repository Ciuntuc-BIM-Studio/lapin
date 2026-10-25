using System.Text.Json.Nodes;
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

    public static StairInputs InputsOf(Entity e) => new(
        e.NumOrNull("width"), e.NumOrNull("risers") is { } n ? (int)Math.Round(n) : null, e.NumOrNull("riserHeight"), e.NumOrNull("tread"), e.NumOrNull("landing"));

    /// <summary>The storey the stair climbs to: the one named, else the first above its own; null when none.</summary>
    public static Entity? TopStorey(IModelView m, Entity stair, Entity storey) =>
        stair.Str("topStoreyId") is { } id && m.Get(id) is { Class: IfcClass.BuildingStorey } named ? named
        : Spatial.Storeys(m).FirstOrDefault(s => s.Num("elevation") > storey.Num("elevation") + 1e-6);

    /// <summary>The height to climb: finished floor to finished floor, with the offsets; without a storey above, the stair's own height, else the storey's.</summary>
    public static double Height(IModelView m, Entity stair, Entity storey)
    {
        var top = TopStorey(m, stair, storey)?.Num("elevation") ?? storey.Num("elevation") + (stair.NumOrNull("height") ?? storey.Num("height", 3));
        return top + stair.Num("topOffset") - (storey.Num("elevation") + stair.Num("baseOffset"));
    }

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

        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            if (FlightSolid(r, fl, place, f, waist) is { } solid) parts.Add(new GeometryPart($"Flight {i + 1}", concreteColor, solid, concrete, $"flight:{i}"));
            else problems.Add($"Flight {i + 1} could not be built.");
            if (f > 0)
                for (var j = 1; j < fl.Risers; j++)
                {
                    var l = fl.Left * (w / 2);
                    Vec2[] tread = [place.World(fl.Start + fl.Dir * ((j - 1) * r.Tread) - l), place.World(fl.Start + fl.Dir * (j * r.Tread) - l), place.World(fl.Start + fl.Dir * (j * r.Tread) + l), place.World(fl.Start + fl.Dir * ((j - 1) * r.Tread) + l)];
                    if (Prism.ExtrudeFootprint(tread, place.Z0 + fl.Z0 + j * r.RiserHeight - f, f, "finish") is { } s) finish.Add(s);
                }
        }
        var winders = new List<Solid>();
        foreach (var wd in r.Winders)
        {
            var outline = wd.Outline.Select(p => place.World(p)).ToArray();
            var top = place.Z0 + wd.Z - f;
            var bottom = top - r.RiserHeight - waist;
            if (Prism.ExtrudeFootprint(outline, bottom, top - bottom, "winder") is { } s) winders.Add(s);
            if (f > 0 && Prism.ExtrudeFootprint(outline, top, f, "finish") is { } fs) finish.Add(fs);
        }
        if (winders.Count > 0) parts.Add(new GeometryPart("Winders", concreteColor, Openings.Combine(winders, "winders"), concrete, "winders"));
        for (var i = 0; i < r.Landings.Count; i++)
        {
            var ld = r.Landings[i];
            var outline = ld.Outline.Select(p => place.World(p)).ToArray();
            var t = spec.Construction.LandingThickness;
            if (Prism.ExtrudeFootprint(outline, place.Z0 + ld.Z - f - t, t, "landing") is { } s) parts.Add(new GeometryPart($"Landing {i + 1}", concreteColor, s, concrete, $"landing:{i}"));
            if (f > 0 && Prism.ExtrudeFootprint(outline, place.Z0 + ld.Z - f, f, "finish") is { } fs) finish.Add(fs);
        }
        if (finish.Count > 0) parts.Add(new GeometryPart("Tread finish", finishColor, Openings.Combine(finish, "finish"), finishMat, "finish"));
        foreach (var side in new[] { "left", "right" })
            if (spec.Railing.Sides == side || spec.Railing.Sides == "both")
            {
                var solids = Railing(r, place, spec.Railing, side == "left");
                if (solids.Count > 0) parts.Add(new GeometryPart($"Railing ({side})", "#4a4a4a", Openings.Combine(solids, "railing"), null, $"railing:{side}"));
            }

        var plan = Plan(r, place);
        var foot = r.Flights[0];
        Vec2[] snaps = [place.World(foot.Start - foot.Left * (w / 2)), place.World(foot.Start + foot.Left * (w / 2)), place.World(r.Top.At)];
        foreach (var c in r.Checks.Where(c => !c.Ok)) problems.Add($"{c.Label} {Fmt(c.Value)} is outside {Fmt(c.Min)} … {Fmt(c.Max)}.");
        return new ElementGeometry(parts, plan, snaps, problems.Count > 0 ? problems : null);
    }

    static string Fmt(double? v) => v is { } x ? (x >= 1 ? x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : (x * 1000).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " mm") : "—";

    /// <summary>
    /// A flight as one solid: the stepped top, the vertical cut where it meets
    /// the landing (or the floor above), the soffit a waist under the step
    /// corners, the bearing on the floor; the concrete a finish below the
    /// finished surfaces. The profile in the flight's plane, extruded across.
    /// </summary>
    public static Solid? FlightSolid(Resolved r, Flight fl, StairPlacement place, double finish, double waist)
    {
        var (k, h, b) = (fl.Risers, r.RiserHeight, r.Tread);
        var wv = waist * Math.Sqrt(h * h + b * b) / b;
        var profile = new List<(double U, double Z)> { (0, -finish) };
        for (var i = 1; i <= k; i++)
        {
            profile.Add(((i - 1) * b, i * h - finish));
            if (i < k) profile.Add((i * b, i * h - finish));
        }
        var uEnd = (k - 1) * b;
        profile.Add((uEnd, (k - 1) * h - wv - finish));
        profile.Add((Math.Min(wv * b / h, Math.Max(uEnd - 0.01, 0)), -finish));
        var right = fl.Start - fl.Left * (r.Width / 2);
        var ring = profile.Select(p => place.World(right + fl.Dir * p.U, fl.Z0 + p.Z)).ToList();
        var across = place.World(fl.Left * r.Width, 0) - place.World(new Vec2(0, 0), 0);
        return Oriented(ring, across, "flight");
    }

    /// <summary>A profile extruded so the solid comes out with positive volume, whichever way the ring runs.</summary>
    static Solid? Oriented(List<Vec3> ring, Vec3 across, string tag)
    {
        var s = Prism.Extrude(ring, null, across, tag);
        if (s == null || Measure.SignedVolume(s) >= 0) return s;
        ring.Reverse();
        return Prism.Extrude(ring, null, across, tag);
    }

    /// <summary>
    /// The railing on one side: the bar over the nosing line along the flights,
    /// level over a landing, climbing over winders; around the outer edge of a
    /// turn, straight across the inner one; posts at the spacing, from the
    /// surface up to the bar.
    /// </summary>
    static List<Solid> Railing(Resolved r, StairPlacement place, StairRailing rail, bool left)
    {
        const double inset = 0.05;
        var w = r.Width;
        var sign = left ? 1 : -1;
        var path = new List<(Vec2 P, double Surface)>();
        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            var side = fl.Left * (sign * (w / 2 - inset));
            var run = (fl.Risers - 1) * r.Tread;
            var zTop = fl.Z0 + fl.Risers * r.RiserHeight;
            path.Add((fl.Start + side, fl.Z0 + r.RiserHeight));
            var end = fl.Start + fl.Dir * run + side;
            path.Add((end, zTop));
            if (i == 0 && r.Flights.Count == 2)
            {
                var next = r.Flights[1];
                var start2 = next.Start + next.Left * (sign * (w / 2 - inset));
                var zTurn = next.Z0;
                // The turn's depth along the first flight: a landing's length, or the width when it turns in a square (winders too).
                var depth = r.Spec.Shape == "straight-landing" || (r.Spec.Shape == "u" && r.Winders.Count == 0) ? r.LandingLength : w;
                var outer = r.Spec.Turns_ && left != place.TurnLeft;
                if (r.Spec.Shape == "l" && outer) path.Add((end + fl.Dir * (depth - inset), (zTop + zTurn) / 2));
                else if (r.Spec.Shape == "u" && outer)
                {
                    path.Add((end + fl.Dir * (depth - inset), zTop + (zTurn - zTop) / 3));
                    path.Add((start2 + fl.Dir * (depth - inset), zTop + (zTurn - zTop) * 2 / 3));
                }
                path.Add((start2, zTurn + r.RiserHeight));
            }
        }
        var solids = new List<Solid>();
        Vec3 Top((Vec2 P, double Surface) p) => place.World(p.P, p.Surface + rail.Height);
        for (var i = 0; i + 1 < path.Count; i++)
        {
            var (a, b) = (path[i], path[i + 1]);
            var len = (b.P - a.P).Length;
            if (len > 1e-6 && Prism.SweepBox(Top(a), Top(b), rail.BarSize, rail.BarSize, "rail") is { } bar) solids.Add(bar);
            var count = Math.Max(1, (int)Math.Ceiling(len / Math.Max(rail.PostSpacing, 0.3)));
            for (var j = 0; j <= count; j++)
            {
                if (j == count && i + 2 < path.Count) continue;
                var t = (double)j / count;
                var bottom = place.World(a.P + (b.P - a.P) * t, a.Surface + (b.Surface - a.Surface) * t);
                if (Prism.Box(new Vec3(bottom.X, bottom.Y, bottom.Z + rail.Height / 2), rail.PostSize, rail.PostSize, rail.Height, "post") is { } post) solids.Add(post);
            }
        }
        return solids;
    }

    // ─── Plan ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The stair as a plan draws it, whole, from the foot to the top whatever
    /// the plan's cut (the user reads the stair entire): every riser as a line
    /// across, the sides of each flight, the landings and winders, and the
    /// arrow of ascent along the centre line.
    /// </summary>
    static List<PlanItem> Plan(Resolved r, StairPlacement place)
    {
        var items = new List<PlanItem>();
        var w = r.Width;
        var arrow = new List<Vec2>();
        foreach (var fl in r.Flights)
        {
            var l = fl.Left * (w / 2);
            var run = (fl.Risers - 1) * r.Tread;
            for (var i = 1; i <= fl.Risers; i++)
            {
                var u = (i - 1) * r.Tread;
                items.Add(new PlanItem("symbol", false, [place.World(fl.Start + fl.Dir * u - l), place.World(fl.Start + fl.Dir * u + l)]));
            }
            items.Add(new PlanItem("symbol", false, [place.World(fl.Start - l), place.World(fl.Start + fl.Dir * run - l)]));
            items.Add(new PlanItem("symbol", false, [place.World(fl.Start + l), place.World(fl.Start + fl.Dir * run + l)]));
            arrow.Add(place.World(arrow.Count == 0 ? fl.Start + fl.Dir * (r.Tread / 2) : fl.Start));
            arrow.Add(place.World(fl.Start + fl.Dir * run));
        }
        foreach (var ld in r.Landings) items.Add(new PlanItem("symbol", true, ld.Outline.Select(p => place.World(p)).ToArray()));
        foreach (var wd in r.Winders) items.Add(new PlanItem("symbol", true, wd.Outline.Select(p => place.World(p)).ToArray()));
        if (arrow.Count >= 2)
        {
            // Through a turn the arrow goes from the end of one flight to the start of the next, over the landing or the winders.
            items.Add(new PlanItem("symbol", false, [.. arrow]));
            var tip = arrow[^1];
            var d = (arrow[^1] - arrow[^2]).Normalized();
            var size = Math.Min(0.25, w * 0.25);
            items.Add(new PlanItem("symbol", false, [tip - d * size + d.Perp * (size * 0.5), tip, tip - d * size - d.Perp * (size * 0.5)]));
            var foot = arrow[0];
            items.Add(new PlanItem("symbol", true, [foot + new Vec2(0.04, 0), foot + new Vec2(0, 0.04), foot - new Vec2(0.04, 0), foot - new Vec2(0, 0.04)]));
        }
        return items;
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
            soffit += w * length * Math.Sqrt(1 + r.RiserHeight * r.RiserHeight / (r.Tread * r.Tread));
            treads += (fl.Risers - 1) * r.Tread * w;
            yield return new QuantitySet("Qto_StairFlightBaseQuantities",
            [
                new("Length", length, "LENGTH"), new("NumberOfRiser", fl.Risers, "COUNT"), new("NumberOfTreads", fl.Risers - 1, "COUNT"),
                new("RiserHeight", r.RiserHeight, "LENGTH"), new("TreadLength", r.Tread, "LENGTH"), new("GrossVolume", Vol($"flight:{i}"), "VOLUME"),
            ], $"flight:{i}", $"Flight {i + 1}", spec.Construction.Concrete);
        }
        var landingArea = r.Landings.Sum(l => Area(l.Outline));
        var winderArea = r.Winders.Sum(x => Area(x.Outline));
        var concrete = g.Parts.Where(p => p.Key != null && (p.Key.StartsWith("flight:", StringComparison.Ordinal) || p.Key == "winders" || p.Key.StartsWith("landing:", StringComparison.Ordinal))).Sum(p => Measure.Volume(p.Solid));
        var railing = 0.0;
        foreach (var fl in r.Flights) railing += (fl.Risers - 1) * Math.Sqrt(r.Tread * r.Tread + r.RiserHeight * r.RiserHeight);
        var sides = spec.Railing.Sides == "both" ? 2 : spec.Railing.Sides == "none" ? 0 : 1;
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

    static double Area(IReadOnlyList<Vec2> poly)
    {
        var a = 0.0;
        for (var i = 0; i < poly.Count; i++) a += Vec2.Cross(poly[i], poly[(i + 1) % poly.Count]);
        return Math.Abs(a) / 2;
    }

    // ─── The opening in the floor above ─────────────────────────────────────

    /// <summary>What the stair asks of the floor above: the box, in plan, of everything that comes within the headroom of that floor's underside.</summary>
    public static IReadOnlyList<Vec2>? OpeningContour(IModelView m, Entity stair)
    {
        if (Resolve(m, stair) is not var (r, place, spec) || Spatial.StoreyOf(m, stair.Id) is not { } storey || TopStorey(m, stair, storey) is not { } top) return null;
        var slab = SlabUnder(m, top, place.World(r.Top.At));
        var thickness = slab != null ? Slabs.Section(m, slab).Sum(s => s.To - s.From) : 0.2;
        var limit = r.Height - thickness - spec.Rules.Headroom;
        var pts = new List<Vec2>();
        foreach (var fl in r.Flights)
            for (var i = 1; i <= fl.Risers; i++)
                if (fl.Z0 + i * r.RiserHeight > limit)
                {
                    var l = fl.Left * (r.Width / 2);
                    var u0 = (i - 1) * r.Tread; var u1 = Math.Min(i, fl.Risers - 1) * r.Tread;
                    pts.AddRange([fl.Start + fl.Dir * u0 - l, fl.Start + fl.Dir * u0 + l, fl.Start + fl.Dir * u1 - l, fl.Start + fl.Dir * u1 + l]);
                }
        foreach (var ld in r.Landings) if (ld.Z > limit) pts.AddRange(ld.Outline);
        foreach (var wd in r.Winders) if (wd.Z > limit) pts.AddRange(wd.Outline);
        if (pts.Count < 3) return null;
        double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
        return [place.World(new Vec2(x0, y0)), place.World(new Vec2(x1, y0)), place.World(new Vec2(x1, y1)), place.World(new Vec2(x0, y1))];
    }

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
        if (!stair.Bool("noOpening") && Resolve(m, stair) is var (r, place, _) && Spatial.StoreyOf(m, stair.Id) is { } storey && TopStorey(m, stair, storey) is { } top)
            slab = SlabUnder(m, top, place.World(r.Top.At));
        if (slab == null || OpeningContour(m, stair) == null)
        {
            if (existing != null) ctx.Remove(existing.Id);
            return;
        }
        if (existing != null && m.RelatingOf(existing.Id, IfcClass.RelVoidsElement)?.Id == slab.Id) return;
        if (existing != null) ctx.Remove(existing.Id);
        var opening = new Entity(ctx.NewId(), IfcClass.OpeningElement, "Stair opening", new JsonObject { ["stairId"] = stair.Id }, "OPENING");
        ctx.Add(opening);
        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, slab.Id, [opening.Id]));
    }
}
