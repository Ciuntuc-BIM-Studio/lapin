using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/*
 * Ramps (decision 252, report #53): a ramp is made by the stair configurator, a type of kind
 * "ramp" (IfcRampType), placed by the stair's commands as an IfcRamp. It climbs by a slope: the
 * run in plan is the height over the slope, split into flights no longer than the type's longest
 * run, with landings between them: on a straight line, round an L's corner, or back and forth in
 * lanes side by side (a switchback). Each flight is a sloping slab (or solid down to the ramp's
 * base), its finish on top, its handrails along both edges; the landings are slabs.
 */

/// <summary>A ramp's flight: where it starts (the middle of its foot), which way it climbs, its length in plan, the levels at its two ends and its width.</summary>
public sealed record RampFlight(Vec2 Start, Vec2 Dir, double Length, double Z0, double Z1, double Width)
{
    public Vec2 Left => Dir.Perp;
    public Vec2 End => Start + Dir * Length;
    public double Slope => Length > 1e-9 ? (Z1 - Z0) / Length : 0;
    public Vec2[] Outline()
    {
        var l = Left * (Width / 2);
        return [Start - l, End - l, End + l, Start + l];
    }
}

/// <summary>What the instance fixes on a ramp: its width, slope, landing length; null for the type's.</summary>
public sealed record RampInputs(double? Width = null, double? Slope = null, double? Landing = null);

/// <summary>A ramp worked out in its own frame (the middle of its foot at the origin, climbing +X, turning to +Y when it turns left), with its checks.</summary>
public sealed record RampResolved(StairSpec Spec, double Height, double Slope, double Width, double LandingLength,
    IReadOnlyList<RampFlight> Flights, IReadOnlyList<Landing> Landings, IReadOnlyList<StairCheck> Checks)
{
    public bool Ok => Checks.All(c => c.Ok);
    public double Run => Flights.Sum(f => f.Length);
    public (Vec2 At, Vec2 Dir) Top => (Flights[^1].End, Flights[^1].Dir);
    public IEnumerable<Vec2[]> Footprint() => Flights.Select(f => f.Outline()).Concat(Landings.Select(l => l.Outline));
}

public static class RampSolver
{
    public static RampResolved Resolve(StairSpec spec, double height, RampInputs? inputs = null, bool turnLeft = true)
    {
        inputs ??= new RampInputs();
        var rules = spec.Rules;
        height = Math.Max(height, 0.01);
        var slope = Math.Clamp(inputs.Slope ?? spec.Slope, 0.005, 0.5);
        var width = inputs.Width ?? spec.Width;
        var landing = inputs.Landing ?? (spec.LandingLength > 0 ? spec.LandingLength : Math.Max(1.5, width));
        var run = height / slope;
        var maxRun = spec.MaxRun > 0 ? spec.MaxRun : double.PositiveInfinity;
        var n = spec.Shape switch
        {
            "straight" => 1,
            "l" => 2,
            "u" => Math.Max(2, (int)Math.Ceiling(run / maxRun - 1e-9)),
            _ => Math.Max(1, (int)Math.Ceiling(run / maxRun - 1e-9)),
        };
        var length = run / n;
        var rise = height / n;
        var side = new Vec2(0, turnLeft ? 1.0 : -1.0);
        var flights = new List<RampFlight>();
        var landings = new List<Landing>();
        var w = width;
        var pos = new Vec2(0, 0);
        var dir = new Vec2(1, 0);
        for (var i = 0; i < n; i++)
        {
            var fl = new RampFlight(pos, dir, length, i * rise, (i + 1) * rise, w);
            flights.Add(fl);
            if (i == n - 1) break;
            var end = fl.End;
            var z = fl.Z1;
            switch (spec.Shape)
            {
                case "l":
                {
                    // A square corner as wide as the ramp, then on to the side it turns.
                    var across = side * (w / 2);
                    landings.Add(new Landing([end - across, end + dir * w - across, end + dir * w + across, end + across], z));
                    pos = end + dir * (w / 2) + side * (w / 2);
                    dir = side;
                    break;
                }
                case "u":
                {
                    // Across to the next lane, a gap beside this one, and back the way it came.
                    var near = side * (w / 2);
                    var far = side * (w / 2 + spec.Gap + w);
                    landings.Add(new Landing([end - near, end + dir * landing - near, end + dir * landing + far, end + far], z));
                    pos = end + side * (w + spec.Gap);
                    dir = -dir;
                    break;
                }
                default:
                {
                    var l = side * (w / 2);
                    landings.Add(new Landing([end - l, end + dir * landing - l, end + dir * landing + l, end + l], z));
                    pos = end + dir * landing;
                    break;
                }
            }
        }
        var checks = new List<StairCheck>
        {
            new("slope", "Slope", slope, null, rules.MaxSlope, slope <= rules.MaxSlope + 1e-9),
            new("width", "Width", width, rules.MinWidth, null, width >= rules.MinWidth - 1e-6),
        };
        if (spec.MaxRun > 0) checks.Add(new("run", "Flight length", length, null, spec.MaxRun, length <= spec.MaxRun + 1e-6));
        if (landings.Count > 0 && spec.Shape != "l") checks.Add(new("landing", "Landing", landing, Math.Min(1.5, width), null, landing >= Math.Min(1.5, width) - 1e-6));
        return new RampResolved(spec, height, slope, width, landing, flights, landings, checks);
    }
}

/// <summary>A ramp's parts, plan, quantities and the opening above, from its type and attributes (<c>slope</c>, <c>width</c>, <c>landing</c> beside the stair's).</summary>
public static class RampBuilder
{
    public static RampInputs InputsOf(Entity e) => new(e.NumOrNull("width"), e.NumOrNull("slope"), e.NumOrNull("landing"));

    public static (RampResolved Resolved, StairPlacement Place, StairSpec Spec)? Resolve(IModelView m, Entity ramp)
    {
        if (Spatial.StoreyOf(m, ramp.Id) is not { } storey) return null;
        var spec = StairBuilder.SpecOf(m, ramp);
        var place = StairBuilder.PlacementOf(ramp, storey);
        return (RampSolver.Resolve(spec, StairBuilder.Height(m, ramp, storey), InputsOf(ramp), place.TurnLeft), place, spec);
    }

    public static ElementGeometry? Geometry(Entity ramp, IModelView m)
    {
        if (Resolve(m, ramp) is not var (r, place, spec)) return new ElementGeometry([], Diagnostics: ["The ramp stands on no storey."]);
        var f = spec.Finish.TreadThickness;
        var concrete = Materials.Find(m, spec.Construction.Concrete)?.Id;
        var finishMat = Materials.Find(m, spec.Finish.TreadMaterial)?.Id;
        var concreteColor = concrete != null ? Materials.ColorOf(m, concrete) : "#a8a8a8";
        var finishColor = finishMat != null ? Materials.ColorOf(m, finishMat) : "#d9d2c5";
        var solid = spec.Construction.Kind == "solid";
        var parts = new List<GeometryPart>();
        var finish = new List<Solid>();
        var problems = new List<string>();
        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            // The slab's soffit a thickness under the surface, measured square to it; where it meets the base, the slab bears on it.
            var thick = spec.Construction.Waist * Math.Sqrt(1 + fl.Slope * fl.Slope);
            List<(double U, double Z)> profile = solid
                ? [(0, fl.Z0 - f), (fl.Length, fl.Z1 - f), (fl.Length, -f), (0, -f)]
                : [(0, fl.Z0 - f), (fl.Length, fl.Z1 - f), (fl.Length, fl.Z1 - f - thick), (0, fl.Z0 - f - thick)];
            if (solid && fl.Z0 < 1e-9) profile.RemoveAt(3);
            // The first flight's soffit stops where it reaches the base it starts on.
            if (!solid && i == 0 && fl.Slope > 1e-9) profile[3] = (Math.Min(thick / fl.Slope, fl.Length - 0.01), -f);
            if (Profile(fl, place, profile, "flight") is { } s) parts.Add(new GeometryPart($"Flight {i + 1}", concreteColor, s, concrete, $"flight:{i}"));
            else problems.Add($"Flight {i + 1} could not be built.");
            if (f > 0 && Profile(fl, place, [(0, fl.Z0 - f), (fl.Length, fl.Z1 - f), (fl.Length, fl.Z1), (0, fl.Z0)], "finish") is { } fs) finish.Add(fs);
        }
        for (var i = 0; i < r.Landings.Count; i++)
        {
            var ld = r.Landings[i];
            var outline = ld.Outline.Select(p => place.World(p)).ToArray();
            var t = solid ? ld.Z : spec.Construction.LandingThickness;
            if (t > 1e-6 && Prism.ExtrudeFootprint(outline, place.Z0 + ld.Z - f - t, t, "landing") is { } s) parts.Add(new GeometryPart($"Landing {i + 1}", concreteColor, s, concrete, $"landing:{i}"));
            if (f > 0 && Prism.ExtrudeFootprint(outline, place.Z0 + ld.Z - f, f, "finish") is { } fs) finish.Add(fs);
        }
        if (finish.Count > 0) parts.Add(new GeometryPart("Finish", finishColor, Openings.Combine(finish, "finish"), finishMat, "finish"));
        var rail = StairBuilder.RailingOf(ramp, spec);
        foreach (var sideName in new[] { "left", "right" })
            if (rail.Sides == sideName || rail.Sides == "both")
            {
                var metal = new List<Solid>();
                var glass = new List<Solid>();
                var path = RailingPath(r, place, rail, sideName == "left");
                var (ms, gs) = Railings.Solids(path.Select(p => place.World(p.P, p.Surface)).ToList(), rail);
                metal.AddRange(ms);
                glass.AddRange(gs);
                if (metal.Count > 0) parts.Add(new GeometryPart($"Railing ({sideName})", rail.Color ?? "#4a4a4a", Openings.Combine(metal, "railing"), null, $"railing:{sideName}"));
                if (glass.Count > 0) parts.Add(new GeometryPart($"Railing glass ({sideName})", "#9cc8e8", Openings.Combine(glass, "glass"), null, $"railing-glass:{sideName}"));
            }
        var foot = r.Flights[0];
        Vec2[] snaps = [place.World(foot.Start - foot.Left * (foot.Width / 2)), place.World(foot.Start + foot.Left * (foot.Width / 2)), place.World(r.Top.At)];
        foreach (var c in r.Checks.Where(c => !c.Ok)) problems.Add($"{c.Label} {Fmt(c)} is outside {(c.Min is { } lo ? Fmt(c, lo) : "—")} … {(c.Max is { } hi ? Fmt(c, hi) : "—")}.");
        if (StairBuilder.TooHigh(m, ramp) is { } high) problems.Add(high);
        return new ElementGeometry(parts, Plan(r, place), snaps, problems.Count > 0 ? problems : null);
    }

    static string Fmt(StairCheck c, double? v = null)
    {
        var x = v ?? c.Value;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return c.Key == "slope" ? (x * 100).ToString("0.0", inv) + " %" : x.ToString("0.00", inv) + " m";
    }

    /// <summary>A profile in the flight's vertical plane (u along it from its foot, z up from the ramp's base) extruded across its width.</summary>
    static Solid? Profile(RampFlight fl, StairPlacement place, List<(double U, double Z)> profile, string tag)
    {
        var right = fl.Start - fl.Left * (fl.Width / 2);
        var ring = profile.Select(p => place.World(right + fl.Dir * p.U, p.Z)).ToList();
        var across = place.World(fl.Left * fl.Width, 0) - place.World(new Vec2(0, 0), 0);
        return StairBuilder.Oriented(ring, across, tag);
    }

    /// <summary>
    /// A handrail's path on one side: along each flight's edge, over its surface; through a landing straight to
    /// the next flight on the inside of a turn, round the landing's far corners on the outside; on past the ends when asked.
    /// </summary>
    static List<(Vec2 P, double Surface)> RailingPath(RampResolved r, StairPlacement place, StairRailing rail, bool left)
    {
        var inset = Math.Clamp(rail.Inset, 0, 0.3);
        var sign = left ? 1.0 : -1.0;
        var turnSide = new Vec2(0, place.TurnLeft ? 1.0 : -1.0);
        var path = new List<(Vec2 P, double Surface)>();
        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            var off = fl.Left * (sign * (fl.Width / 2 - inset));
            path.Add((fl.Start + off, fl.Z0));
            path.Add((fl.End + off, fl.Z1));
            if (i + 1 >= r.Flights.Count || r.Spec.Shape is not ("l" or "u")) continue;
            // On the outside of the turn (the side away from where the ramp goes on), round the landing's far edge.
            if (Vec2.Dot(off, turnSide) >= 0) continue;
            var depth = r.Spec.Shape == "l" ? fl.Width : r.LandingLength;
            var next = r.Flights[i + 1];
            var nextOff = next.Left * (sign * (next.Width / 2 - inset));
            path.Add((fl.End + fl.Dir * (depth - inset) + off, fl.Z1));
            if (r.Spec.Shape == "u") path.Add((next.Start + fl.Dir * (depth - inset) + nextOff, fl.Z1));
        }
        if (rail.Extension > 0 && path.Count >= 2)
        {
            var d0 = (path[0].P - path[1].P).Normalized();
            var d1 = (path[^1].P - path[^2].P).Normalized();
            path.Insert(0, (path[0].P + d0 * rail.Extension, path[0].Surface));
            path.Add((path[^1].P + d1 * rail.Extension, path[^1].Surface));
        }
        return path;
    }

    /// <summary>The ramp in plan: each flight's outline, the landings, and the arrow up its middle through the turns.</summary>
    static List<PlanItem> Plan(RampResolved r, StairPlacement place)
    {
        var items = new List<PlanItem>();
        var arrow = new List<Vec2>();
        foreach (var fl in r.Flights)
        {
            items.Add(new PlanItem("symbol", true, fl.Outline().Select(p => place.World(p)).ToArray()));
            arrow.Add(place.World(arrow.Count == 0 ? fl.Start + fl.Dir * Math.Min(0.3, fl.Length / 4) : fl.Start));
            arrow.Add(place.World(fl.End));
        }
        foreach (var ld in r.Landings) items.Add(new PlanItem("symbol", true, ld.Outline.Select(p => place.World(p)).ToArray()));
        if (arrow.Count >= 2)
        {
            items.Add(new PlanItem("symbol", false, [.. arrow]));
            var tip = arrow[^1];
            var d = (arrow[^1] - arrow[^2]).Normalized();
            var size = Math.Min(0.3, r.Width * 0.25);
            items.Add(new PlanItem("symbol", false, [tip - d * size + d.Perp * (size * 0.5), tip, tip - d * size - d.Perp * (size * 0.5)]));
            var foot = arrow[0];
            items.Add(new PlanItem("symbol", true, [foot + new Vec2(0.04, 0), foot + new Vec2(0, 0.04), foot - new Vec2(0.04, 0), foot - new Vec2(0, 0.04)]));
        }
        return items;
    }

    public static IEnumerable<QuantitySet> Quantities(Entity ramp, ElementGeometry? g, IModelView m)
    {
        if (g == null || Resolve(m, ramp) is not var (r, _, spec)) yield break;
        double Vol(string key) => g.Parts.Where(p => p.Key == key).Sum(p => Measure.Volume(p.Solid));
        for (var i = 0; i < r.Flights.Count; i++)
        {
            var fl = r.Flights[i];
            yield return new QuantitySet("Qto_RampFlightBaseQuantities",
            [
                new("Length", fl.Length, "LENGTH"), new("Width", fl.Width, "LENGTH"), new("GrossVolume", Vol($"flight:{i}"), "VOLUME"),
            ], $"flight:{i}", $"Flight {i + 1}", spec.Construction.Concrete);
        }
        var surface = r.Flights.Sum(fl => fl.Length * Math.Sqrt(1 + fl.Slope * fl.Slope) * fl.Width) + r.Landings.Sum(l => Math.Abs(Polygon2.SignedArea(l.Outline)));
        var concrete = g.Parts.Where(p => p.Key != null && (p.Key.StartsWith("flight:", StringComparison.Ordinal) || p.Key.StartsWith("landing:", StringComparison.Ordinal))).Sum(p => Measure.Volume(p.Solid));
        var rail = StairBuilder.RailingOf(ramp, spec);
        var sides = rail.Sides == "both" ? 2 : rail.Sides == "none" ? 0 : 1;
        yield return new QuantitySet("Lapin_RampQuantities",
        [
            new("Width", r.Width, "LENGTH"), new("Height", r.Height, "LENGTH"), new("Slope", r.Slope * 100, "RATIO", "Slope (%)"),
            new("Length", r.Run, "LENGTH", "Run in plan"), new("NumberOfFlights", r.Flights.Count, "COUNT", "Flights"),
            new("ConcreteVolume", concrete, "VOLUME", "Concrete"), new("FinishArea", surface, "AREA", "Finish"),
            new("RailingLength", r.Flights.Sum(fl => fl.Length * Math.Sqrt(1 + fl.Slope * fl.Slope)) * sides, "LENGTH", "Railing"),
        ]);
    }

    /// <summary>What the ramp asks of the floor above: the box of what comes within the headroom of its underside.</summary>
    public static IReadOnlyList<Vec2>? OpeningContour(IModelView m, Entity ramp, double slabThickness)
    {
        if (Resolve(m, ramp) is not var (r, place, spec)) return null;
        var limit = r.Height - slabThickness - spec.Rules.Headroom;
        var pts = new List<Vec2>();
        foreach (var fl in r.Flights)
        {
            if (fl.Z1 <= limit) continue;
            var u0 = fl.Z0 >= limit || fl.Slope < 1e-9 ? 0 : (limit - fl.Z0) / fl.Slope;
            var l = fl.Left * (fl.Width / 2);
            pts.AddRange([fl.Start + fl.Dir * u0 - l, fl.Start + fl.Dir * u0 + l, fl.End - l, fl.End + l]);
        }
        foreach (var ld in r.Landings) if (ld.Z > limit) pts.AddRange(ld.Outline);
        if (pts.Count < 3) return null;
        double x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
        return [place.World(new Vec2(x0, y0)), place.World(new Vec2(x1, y0)), place.World(new Vec2(x1, y1)), place.World(new Vec2(x0, y1))];
    }
}

/// <summary>What a plan writes on its storey's ramps: the slope along each flight, in per cent.</summary>
public static class RampMarks
{
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.Kind != "plan") yield break;
        var m = c.Model;
        var storeyId = c.View.Str("storeyId");
        var hidden = (c.View.Attrs["hiddenElements"] as System.Text.Json.Nodes.JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        var graphics = VisibilityGraphics.Of(m, c.View);
        var style = Standards.TextOf(c.Book, Standards.DefaultRoomTag.TextStyle);
        foreach (var ramp in m.OfClass(IfcClass.Ramp))
        {
            if (hidden.Contains(ramp.Id) || graphics.Hides(ramp) || Spatial.StoreyOf(m, ramp.Id)?.Id != storeyId) continue;
            if (RampBuilder.Resolve(m, ramp) is not var (r, place, _)) continue;
            var mark = new ViewMark(ramp.Id);
            var color = style.Color ?? "#000000";
            var h = Math.Max(1.8, style.Height * 0.8);
            var text = (r.Slope * 100).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " %";
            foreach (var fl in r.Flights)
            {
                // Beside the arrow, along the flight, reading upright.
                var at = c.Map(place.World(fl.Start + fl.Dir * (fl.Length / 2) + fl.Left * Math.Min(0.25, fl.Width / 4)), place.Z0);
                var d = c.Map(place.World(fl.End), place.Z0) - c.Map(place.World(fl.Start), place.Z0);
                var angle = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
                if (angle > 90) angle -= 180; else if (angle < -90) angle += 180;
                mark.Text(new PenText(at, text, h, angle, "middle", "middle", false, style.Font), color);
            }
            if (mark.Texts.Count > 0) yield return mark;
        }
    }
}
