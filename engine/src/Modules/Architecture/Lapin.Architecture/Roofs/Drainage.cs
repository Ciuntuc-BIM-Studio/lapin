using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// A roof's gutters and downpipes made parametric (decision 235, as ArchiCAD's): the gutter is always tied to its eave edge,
/// outside it so it takes the water, with its profile and size, its offset out, its drop under the covering's edge, its fall
/// to the outlets, its end caps, and switched on or off per eave edge (<c>edges[i].gutter</c>). The downpipes are the roof's
/// automatic ones (one per 10 m, in to the wall at 45°, then down: the starting point, and how old roofs open) until
/// <c>architecture.makeDownpipes</c> makes them elements of their own (<c>LapinDownpipe</c>): each tied to an eave edge's gutter
/// at a station along it, its route a chain of segments each with its length and its angle from the vertical (towards the
/// wall), the last one to the ground unless given, its diameter and the shoe at its foot.
/// </summary>
public static class Drainage
{
    public const string DownpipeClass = "LapinDownpipe";
    public const double DefaultDiameter = 0.1;

    /// <summary>A gutter's parameters, from its roof.</summary>
    public sealed record Spec(bool Box, double Width, double Offset, double Drop, double Fall, bool Ends)
    {
        public double R => Width / 2;
        public static Spec Of(Entity roof) => new(
            roof.Str("gutterProfile") == "box", Math.Clamp(roof.NumOrNull("gutterWidth") ?? 0.125, 0.06, 0.4), roof.NumOrNull("gutterOffset") ?? 0,
            roof.NumOrNull("gutterDrop") ?? 0, Math.Clamp(roof.NumOrNull("gutterFall") ?? 0, 0, 20) / 1000, roof.Str("gutterEnds") != "False");
    }

    /// <summary>One segment of a downpipe's route: its length (null: down to the ground) and its angle from the vertical, towards the wall.</summary>
    public sealed record Leg(double? Length, double Angle);

    public static List<Leg> RouteOf(Entity pipe) =>
        (pipe.Attrs["route"] as JsonArray)?.OfType<JsonObject>().Select(o => new Leg(Json.Number(o["length"]), Json.Number(o["angle"]) ?? 0)).ToList() ?? [];

    public static JsonArray RouteJson(IEnumerable<Leg> legs) =>
        new([.. legs.Select(l => { var o = new JsonObject { ["angle"] = Math.Round(l.Angle, 3) }; if (l.Length is { } len) o["length"] = Math.Round(len, 4); return (JsonNode)o; })]);

    /// <summary>The automatic route from an outlet: in to the wall at 45° (by how far the outlet stands out of it), then down to the ground.</summary>
    public static List<Leg> DefaultRoute(double outFromWall) =>
        outFromWall > 0.05 ? [new Leg(Math.Round(outFromWall * Math.Sqrt(2), 4), 45), new Leg(null, 0)] : [new Leg(null, 0)];

    /// <summary>The outline edge an eave line runs along: parallel to it, as far out as its overhang.</summary>
    public static int EdgeOf(Roof.Resolved r, RoofLine l)
    {
        var n = r.Outline.Count;
        var d = (l.B - l.A).Xy.Normalized();
        var mid = ((l.A + l.B) * 0.5).Xy;
        var best = -1;
        var bestErr = double.PositiveInfinity;
        for (var i = 0; i < n; i++)
        {
            if (r.Rules[i].Kind != EdgeKind.Eave) continue;
            var (a, b) = (r.Outline[i], r.Outline[(i + 1) % n]);
            var e = (b - a).Normalized();
            if (Math.Abs(Vec2.Cross(d, e)) > 0.02) continue;
            var err = Math.Abs(Math.Abs(Vec2.Cross(e, mid - a)) - r.Rules[i].Overhang);
            if (err < bestErr) { bestErr = err; best = i; }
        }
        return best;
    }

    /// <summary>Whether an eave edge has its gutter (on unless its rule says not).</summary>
    public static bool HasGutter(Entity roof, int edge) =>
        !((roof.Attrs["edges"] as JsonArray) is { } arr && edge >= 0 && edge < arr.Count && arr[edge] is JsonObject o && o["gutter"] is JsonValue v && v.TryGetValue<bool>(out var on) && !on);

    /// <summary>The roof's downpipes as stations along its gutters: the automatic ones, or its elements (none when it says none).</summary>
    public static List<(int Gutter, double Station, Entity? Element)> Outlets(IModelView m, Roof.Resolved r, IReadOnlyList<RoofParts.Gutter> gutters)
    {
        var o = new List<(int, double, Entity?)>();
        var mode = r.Roof.Str("downpipes") ?? "auto";
        if (mode == "none") return o;
        if (mode == "elements")
        {
            foreach (var p in m.OfClass(DownpipeClass).Where(p => p.Str("roofId") == r.Roof.Id))
            {
                var gi = gutters.ToList().FindIndex(g => g.Edge == (int)p.Num("edge"));
                if (gi >= 0) o.Add((gi, Math.Clamp(p.Num("station"), 0, gutters[gi].Length), p));
            }
            return o;
        }
        for (var gi = 0; gi < gutters.Count; gi++)
        {
            var g = gutters[gi];
            var n = Math.Max(1, (int)Math.Ceiling(g.Length / 10));
            for (var k = 0; k < n; k++)
                o.Add((gi, n == 1 ? g.Length - 0.3 : 0.3 + (g.Length - 0.6) * (n - 1 - k) / (n - 1), null));
        }
        return o;
    }

    /// <summary>
    /// How far below its top a gutter's bottom lies at a station: level with no fall; else falling towards the nearest outlet,
    /// the high points halfway between outlets (or at the ends without one), each outlet as low as its longer side asks.
    /// </summary>
    public static Func<double, double> FallOf(double length, IReadOnlyList<double> outlets, double fall)
    {
        if (fall <= 0 || outlets.Count == 0) return _ => 0;
        var os = outlets.OrderBy(x => x).ToList();
        // Breakpoints: highs and lows, with their depths.
        var pts = new List<(double S, double D)>();
        double HighBefore(int i) => i == 0 ? 0 : (os[i - 1] + os[i]) / 2;
        double HighAfter(int i) => i == os.Count - 1 ? length : (os[i] + os[i + 1]) / 2;
        var depth = os.Select((s, i) => fall * Math.Max(s - HighBefore(i), HighAfter(i) - s)).ToList();
        for (var i = 0; i < os.Count; i++)
        {
            var hb = HighBefore(i);
            if (i == 0) pts.Add((hb, depth[0] - fall * (os[0] - hb)));
            pts.Add((os[i], depth[i]));
            var ha = HighAfter(i);
            // A high point between two outlets: as high as the shallower slope from either side lets it be.
            var dh = i == os.Count - 1 ? depth[i] - fall * (ha - os[i]) : Math.Max(depth[i] - fall * (ha - os[i]), depth[i + 1] - fall * (os[i + 1] - ha));
            pts.Add((ha, dh));
        }
        return s =>
        {
            if (s <= pts[0].S) return pts[0].D;
            for (var i = 0; i + 1 < pts.Count; i++)
                if (s <= pts[i + 1].S) { var (a, b) = (pts[i], pts[i + 1]); return b.S - a.S < 1e-9 ? b.D : a.D + (b.D - a.D) * (s - a.S) / (b.S - a.S); }
            return pts[^1].D;
        };
    }

    /// <summary>A gutter's body: its channel in pieces between its high and low points (falling), and its end caps.</summary>
    public static List<Solid> Gutter(RoofParts.Gutter g, Spec spec, IReadOnlyList<double> outlets, string tag)
    {
        var o = new List<Solid>();
        var depth = FallOf(g.Length, outlets, spec.Fall);
        var dir = (g.B - g.A) * (1 / g.Length);
        var stations = new SortedSet<double> { 0, g.Length };
        if (spec.Fall > 0) foreach (var s in outlets) { stations.Add(Math.Clamp(s, 0, g.Length)); }
        if (spec.Fall > 0 && outlets.Count > 0)
        {
            var os = outlets.OrderBy(x => x).ToList();
            for (var i = 0; i + 1 < os.Count; i++) stations.Add((os[i] + os[i + 1]) / 2);
        }
        var list = stations.ToList();
        for (var i = 0; i + 1 < list.Count; i++)
        {
            var (s0, s1) = (list[i], list[i + 1]);
            if (s1 - s0 < 1e-4) continue;
            var a = g.A + dir * s0 - new Vec3(0, 0, depth(s0));
            var b = g.A + dir * s1 - new Vec3(0, 0, depth(s1));
            if (RoofParts.Channel(g with { A = a, B = b }, spec.Box, tag, spec.R) is { } c) o.Add(c);
        }
        if (spec.Ends)
            foreach (var (s, inward) in new[] { (0.0, 1.0), (g.Length, -1.0) })
            {
                var at = g.A + dir * s - new Vec3(0, 0, depth(s));
                if (RoofParts.Cap(g with { A = at, B = at + dir * inward }, spec.Box, spec.R, tag) is { } cap) o.Add(cap);
            }
        return o;
    }

    /// <summary>A downpipe's chain of points: from under its outlet, leg by leg (towards the wall, at its angle from the vertical), the last one to the ground unless its length is given; then the shoe, out from the wall.</summary>
    public static List<Vec3> Chain(Vec3 outlet, Vec2 outward, IReadOnlyList<Leg> legs, Func<Vec2, double> ground, bool shoe, double radius)
    {
        var pts = new List<Vec3> { outlet with { Z = outlet.Z - 0.05 } };
        var p = pts[0];
        var inward = new Vec3(-outward.X, -outward.Y, 0);
        for (var i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            var t = leg.Angle * Math.PI / 180;
            var d = inward * Math.Sin(t) + new Vec3(0, 0, -Math.Cos(t));
            var len = leg.Length ?? (Math.Cos(t) > 1e-6 ? Math.Max(0, (p.Z - ground(p.Xy) - (shoe ? 0.25 * Math.Cos(Math.PI / 4) : 0)) / Math.Cos(t)) : 0);
            if (len < 1e-4) continue;
            p += d * len;
            pts.Add(p);
        }
        if (shoe && pts.Count > 1)
        {
            // The shoe: a short 45° kick out from the wall at the foot.
            p += (new Vec3(outward.X, outward.Y, 0) * Math.Sin(Math.PI / 4) + new Vec3(0, 0, -Math.Cos(Math.PI / 4))) * 0.25;
            pts.Add(p);
        }
        return pts;
    }

    public static List<Solid> PipeSolids(IReadOnlyList<Vec3> chain, double radius, string tag)
    {
        var o = new List<Solid>();
        for (var i = 0; i + 1 < chain.Count; i++) if (RoofParts.Tube(chain[i], chain[i + 1], radius, 0, tag) is { } s) o.Add(s);
        // The bends: a short ball of the pipe's size where two legs meet.
        return o;
    }

    /// <summary>A downpipe element's place: its roof's gutter on its edge, the outlet at its station, its chain; null when the roof or the gutter is gone.</summary>
    public sealed record Placed(Entity Pipe, Entity Roof, RoofParts.Gutter Gutter, double Station, Vec3 Outlet, List<Vec3> Chain, double Diameter);

    /// <summary>
    /// The building's ground floor level: the storey at or below ±0.00 closest to it (a new project's Ground floor, not its
    /// Foundations at −1.50 under it), else the lowest storey, else 0.
    /// </summary>
    public static double GroundFloor(IModelView m)
    {
        var levels = Spatial.Storeys(m).Select(s => s.Num("elevation")).ToList();
        if (levels.Count == 0) return 0;
        var under = levels.Where(z => z <= 1e-6).ToList();
        return under.Count > 0 ? under.Max() : levels.Min();
    }

    /// <summary>The ground a downpipe reaches at a point: the terrain under it when the project has one (<see cref="IGround"/>), else the ground floor.</summary>
    public static Func<Vec2, double> GroundAt(IModelView m, IServices? services)
    {
        var floor = GroundFloor(m);
        var terrain = services?.Get<IGround>();
        return terrain == null ? _ => floor : p => terrain.ElevationAt(m, p.X, p.Y) ?? floor;
    }

    /// <summary>A roof's gutters and its downpipes' stations, solved once per roof and revision: each of its pipes and every drawing asks.</summary>
    public sealed record Drain(Roof.Resolved R, List<RoofParts.Gutter> Gutters, List<(int Gutter, double Station, Entity? Element)> Outlets);
    static readonly RevisionCache<Drain?> Drains = new();

    public static Drain? DrainOf(Entity roof, IModelView m) => Drains.Get(roof, m, v =>
    {
        if (Roof.Resolve(roof, v) is not { } r || RoofTop.ShapeOf(roof, v) is not { } shape) return null;
        var gutters = RoofParts.Gutters(r, shape);
        return new Drain(r, gutters, Outlets(v, r, gutters));
    });

    public static Placed? Place(IModelView m, Entity pipe, IServices? services = null)
    {
        if (m.Get(pipe.Str("roofId") ?? "") is not { Class: IfcClass.Roof } roof || DrainOf(roof, m) is not { } drain) return null;
        var gutters = drain.Gutters;
        var g = gutters.FirstOrDefault(x => x.Edge == (int)pipe.Num("edge"));
        if (g == null) return null;
        var station = Math.Clamp(pipe.Num("station"), 0, g.Length);
        var spec = Spec.Of(roof);
        var outlets = drain.Outlets.Where(x => gutters[x.Gutter] == g).Select(x => x.Station).ToList();
        var outlet = OutletAt(g, station, outlets, spec);
        var d = pipe.NumOrNull("diameter") ?? DefaultDiameter;
        var chain = Chain(outlet, g.Out, RouteOf(pipe), GroundAt(m, services), pipe.Str("shoe") != "False", d / 2);
        return new Placed(pipe, roof, g, station, outlet, chain, d);
    }

    /// <summary>A chain's length along its legs.</summary>
    public static double LengthOf(IReadOnlyList<Vec3> chain) => chain.Zip(chain.Skip(1), (a, b) => (b - a).Length).Sum();

    /// <summary>
    /// A downpipe's part, its length kept on it: the length is measured to the ground it was built to (the terrain, known to
    /// geometry but not to quantities), and the quantities read it back from what was built.
    /// </summary>
    public static GeometryPart PipePart(string name, string color, Solid solid, string key, IReadOnlyList<Vec3> chain) =>
        new(name, color, solid, null, key) { Properties = [("Length", LengthOf(chain).ToString("0.0###", System.Globalization.CultureInfo.InvariantCulture))], PropertySet = "Lapin_Downpipe" };

    /// <summary>The length a downpipe part was built with; null when it says none.</summary>
    public static double? BuiltLength(GeometryPart part) =>
        part.Properties?.FirstOrDefault(x => x.Name == "Length").Value is { } v && double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var len) ? len : null;

    // ------------------------------------------------------------ the downpipe as an element

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(DownpipeClass, "Downpipe",
        [
            new("station", "Station along the gutter", "length", Min: 0),
            new("diameter", "Diameter", "length", Min: 0.04, Max: 0.3),
            new("shoe", "Shoe at the foot", "choice", Choices: ["True", "False"]),
        ], "#6b7178"));
        r.Geometry(DownpipeClass, Geometry);
        r.Quantities(DownpipeClass, Quantities);

        // A downpipe on a roof's gutter: {roofId, edge, station, diameter?, route?: [{length?, angle}], shoe?}; the roof's
        // downpipes become elements (its automatic ones no longer drawn) if they were not.
        r.Command("addDownpipe", "Downpipe", (ctx, a) =>
        {
            // Clicked in plan ({x, y}): the nearest gutter of the roof named, or of any roof, within a metre, at the point along it.
            Entity roof;
            int edge;
            double? at = null;
            if (Json.Number(a["x"]) is { } px && Json.Number(a["y"]) is { } py && a["edge"] == null)
            {
                var p = new Vec2(px, py);
                var best = ctx.Model.OfClass(IfcClass.Roof).Where(x => (string?)a["roofId"] is not { } rid || x.Id == rid)
                    .SelectMany(x => Roof.Resolve(x, ctx.Model) is { } rx && Roof.Solve(rx).Shape is { } sx ? RoofParts.Gutters(rx, sx).Select(g => (Roof: x, G: g)) : [])
                    .Select(x => { var (a2, b2) = (x.G.A.Xy, x.G.B.Xy); var t = Math.Clamp(Vec2.Dot(p - a2, b2 - a2) / Math.Max(1e-12, Vec2.Dot(b2 - a2, b2 - a2)), 0, 1); return (x.Roof, x.G, T: t * x.G.Length, D: (a2 + (b2 - a2) * t - p).Length); })
                    .OrderBy(x => x.D).FirstOrDefault();
                if (best.Roof == null || best.D > 1.0) throw new CommandException("Click on a roof's gutter.");
                (roof, edge, at) = (best.Roof, best.G.Edge, best.T);
            }
            else
            {
                roof = Spatial.Require(ctx.Model, (string?)a["roofId"], IfcClass.Roof);
                edge = (int)(Json.Number(a["edge"]) ?? -1);
            }
            var (rr, gutters) = GuttersOf(ctx.Model, roof);
            if (edge < 0 && gutters.Count > 0) edge = gutters[0].Edge;
            var g = gutters.FirstOrDefault(x => x.Edge == edge) ?? throw new CommandException("That edge of the roof has no gutter.");
            var station = Math.Clamp(at ?? Json.Number(a["station"]) ?? g.Length / 2, 0, g.Length);
            if (roof.Str("downpipes") != "elements") MakeElements(ctx, roof);
            var route = a["route"] is JsonArray rt ? CheckRoute(rt) : DefaultRoute(OutFromWall(rr, g, Spec.Of(roof)));
            var id = Add(ctx, roof, edge, station, route, Json.Number(a["diameter"]), a["shoe"] is JsonValue sv && sv.TryGetValue<bool>(out var shoe) ? shoe : true);
            return new JsonObject { ["id"] = id };
        });

        // A downpipe's station, diameter, route or shoe: {id, station?, diameter?, route?, shoe?, edge?}.
        r.Command("updateDownpipe", "Change downpipe", (ctx, a) =>
        {
            var pipe = ctx.Model.Get((string?)a["id"] ?? "") is { Class: DownpipeClass } x ? x : throw new CommandException("Not a downpipe.");
            var attrs = pipe.CloneAttrs();
            if (Json.Number(a["edge"]) is { } edge) attrs["edge"] = (int)edge;
            if (Json.Number(a["station"]) is { } st) attrs["station"] = Math.Max(0, Math.Round(st, 4));
            if (Json.Number(a["diameter"]) is { } d) attrs["diameter"] = d >= 0.04 && d <= 0.3 ? d : throw new CommandException("A downpipe is 40 to 300 mm across.");
            if (a["route"] is JsonArray rt) attrs["route"] = RouteJson(CheckRoute(rt));
            if (a["shoe"] is JsonValue sv && sv.TryGetValue<bool>(out var shoe)) attrs["shoe"] = shoe ? "True" : "False";
            var next = pipe.With(attrs: attrs);
            if (Place(ctx.Model, next) is null) throw new CommandException("That edge of the roof has no gutter.");
            ctx.Replace(next);
            return null;
        });

        // The roof's automatic downpipes made elements of their own, where they stand, one step.
        r.Command("makeDownpipes", "Make downpipes editable", (ctx, a) =>
        {
            var roof = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Roof);
            if (roof.Str("downpipes") == "elements") throw new CommandException("The roof's downpipes are elements already.");
            return new JsonObject { ["ids"] = new JsonArray([.. MakeElements(ctx, roof).Select(x => (JsonNode)x)]) };
        });
    }

    static (Roof.Resolved R, List<RoofParts.Gutter> Gutters) GuttersOf(IModelView m, Entity roof)
    {
        var r = Roof.Resolve(roof, m) ?? throw new CommandException("The roof has no outline.");
        var shape = Roof.Solve(r).Shape ?? throw new CommandException("The roof does not build.");
        return (r, RoofParts.Gutters(r, shape));
    }

    static List<Leg> CheckRoute(JsonArray rt)
    {
        var legs = rt.OfType<JsonObject>().Select(o => new Leg(Json.Number(o["length"]), Json.Number(o["angle"]) ?? 0)).ToList();
        if (legs.Count == 0 || legs.Count > 12) throw new CommandException("A downpipe's route has 1 to 12 segments.");
        if (legs.Any(l => l.Angle < -90 || l.Angle > 90)) throw new CommandException("A segment's angle from the vertical is between -90° and 90°.");
        if (legs.Any(l => l.Length is { } len && (len < 0.01 || len > 100))) throw new CommandException("A segment is 1 cm to 100 m long.");
        return legs;
    }

    static string Add(ICommandContext ctx, Entity roof, int edge, double station, List<Leg> route, double? diameter, bool shoe)
    {
        var n = ctx.Model.OfClass(DownpipeClass).Count() + 1;
        var attrs = new JsonObject
        {
            ["roofId"] = roof.Id, ["edge"] = edge, ["station"] = Math.Round(station, 4), ["route"] = RouteJson(route), ["shoe"] = shoe ? "True" : "False",
            ["exportAs"] = "IfcPipeSegment", ["objectType"] = "Downpipe",
        };
        if (diameter is { } d) attrs["diameter"] = d;
        var e = new Entity(ctx.NewId(), DownpipeClass, $"Downpipe {n}", attrs, "RIGIDSEGMENT");
        ctx.Add(e);
        if (Spatial.StoreyOf(ctx.Model, roof.Id) is { } storey) Spatial.Contain(ctx, storey.Id, e.Id);
        return e.Id;
    }

    /// <summary>The automatic downpipes as elements, where they stand, with the same route; the roof's downpipes are elements from then on.</summary>
    static List<string> MakeElements(ICommandContext ctx, Entity roof)
    {
        var (r, gutters) = GuttersOf(ctx.Model, roof);
        var spec = Spec.Of(roof);
        var ids = new List<string>();
        foreach (var (gi, station, element) in Outlets(ctx.Model, r, gutters))
            if (element == null) ids.Add(Add(ctx, roof, gutters[gi].Edge, station, DefaultRoute(OutFromWall(r, gutters[gi], spec)), null, false));
        var attrs = roof.CloneAttrs();
        attrs["downpipes"] = "elements";
        ctx.Replace(roof.With(attrs: attrs));
        return ids;
    }

    public static ElementGeometry? Geometry(Entity pipe, IModelView m, IServices? services)
    {
        if (Place(m, pipe, services) is not { } p) return null;
        var solids = PipeSolids(p.Chain, p.Diameter / 2, "downpipe");
        if (solids.Count == 0) return null;
        var foot = p.Chain[^1].Xy;
        var circle = Enumerable.Range(0, 16).Select(k => foot + new Vec2(Math.Cos(k * Math.PI / 8), Math.Sin(k * Math.PI / 8)) * (p.Diameter / 2)).ToArray();
        var plan = new List<PlanItem> { new("symbol", true, circle) };
        // Its course in plan, where it leaves the wall line.
        for (var i = 0; i + 1 < p.Chain.Count; i++)
            if ((p.Chain[i + 1].Xy - p.Chain[i].Xy).Length > 0.01) plan.Add(new PlanItem("symbol", false, [p.Chain[i].Xy, p.Chain[i + 1].Xy]));
        var color = p.Roof.Str("gutterColor") is { Length: > 0 } c ? c : "#6b7178";
        return new ElementGeometry([PipePart(pipe.Name, color, Openings.Combine(solids, "downpipe"), "downpipe", p.Chain)], plan, [foot, p.Outlet.Xy]);
    }

    static IEnumerable<QuantitySet> Quantities(Entity pipe, ElementGeometry? built, IModelView m)
    {
        if (Place(m, pipe) is not { } p) yield break;
        var length = built?.Parts.Select(BuiltLength).FirstOrDefault(x => x != null) ?? LengthOf(p.Chain);
        yield return new QuantitySet("Qto_PipeSegmentBaseQuantities",
        [
            new("Length", length, "LENGTH", $"Downpipe Ø{Math.Round(p.Diameter * 1000)}"),
            new("OuterSurfaceArea", Math.PI * p.Diameter * length, "AREA", "Outside"),
        ]);
        yield return new QuantitySet("Lapin_DownpipeQuantities", [new("Bends", Math.Max(0, p.Chain.Count - 2), "COUNT", "Bends")]);
    }

    /// <summary>A downpipe as a view sees its gutter: the run's ends and the station in the view's coordinates, for the grip that slides it along (plans and sections).</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        foreach (var pipe in c.Model.OfClass(DownpipeClass))
        {
            if (Place(c.Model, pipe) is not { } p) continue;
            if (c.Kind == "plan" && Spatial.StoreyOf(c.Model, pipe.Id)?.Id is { } sid && c.View.Str("storeyId") is { } vs && vs != sid) continue;
            var (a, b, o) = (c.Map(p.Gutter.A), c.Map(p.Gutter.B), c.Map(p.Outlet));
            var mk = new ViewMark(pipe.Id + ":gutter") { Data = new JsonObject
            {
                ["a"] = new JsonArray(Math.Round(a.X, 5), Math.Round(a.Y, 5)), ["b"] = new JsonArray(Math.Round(b.X, 5), Math.Round(b.Y, 5)),
                ["outlet"] = new JsonArray(Math.Round(o.X, 5), Math.Round(o.Y, 5)), ["length"] = Math.Round(p.Gutter.Length, 5), ["station"] = Math.Round(p.Station, 5),
            } };
            mk.Snaps.Add(o);
            yield return mk;
        }
    }

    /// <summary>Where a downpipe leaves its gutter: at the station, under the channel's middle, as low as the gutter's fall has it there.</summary>
    public static Vec3 OutletAt(RoofParts.Gutter g, double station, IReadOnlyList<double> stationsOnIt, Spec spec)
    {
        var dir = (g.B - g.A) * (1 / g.Length);
        return g.A + dir * station + new Vec3(g.Out.X, g.Out.Y, 0) * spec.R - new Vec3(0, 0, FallOf(g.Length, stationsOnIt, spec.Fall)(station) + spec.R);
    }

    /// <summary>How far an outlet stands out of the wall below it (the eave's overhang, the gutter's offset and half its width).</summary>
    public static double OutFromWall(Roof.Resolved r, RoofParts.Gutter g, Spec spec) =>
        (g.Edge >= 0 ? r.Rules[g.Edge].Overhang : 0.5) + spec.Offset + spec.R - 0.1;
}

/// <summary>A roof deleted takes its downpipe elements with it: without their gutter they are nowhere.</summary>
sealed class RoofDeletion : IDeletion
{
    public bool Delete(ICommandContext ctx, string id)
    {
        if (ctx.Model.Get(id) is not { Class: IfcClass.Roof }) return false;
        var pipes = ctx.Model.OfClass(Drainage.DownpipeClass).Where(p => p.Str("roofId") == id).Select(p => p.Id).ToList();
        if (pipes.Count == 0) return false;
        foreach (var p in pipes) ctx.Remove(p);
        ctx.Remove(id);
        return true;
    }
}
