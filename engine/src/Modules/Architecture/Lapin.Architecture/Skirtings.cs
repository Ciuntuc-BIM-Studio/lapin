using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A skirting board's type (a tester's report #61, decision 254): its <see cref="Profile"/> — "flush" (square, a tile's
/// cut edge), "bevelled" (MDF, a sloped top), "moulded" (wooden, an ogee at the top), "cove" (metal: a thin plate with a
/// curved foot), "pencil" (a half-round top) or "bullnose" (the top front edge rounded) — its height and thickness, its
/// material (a project material, by name) and colour.
/// </summary>
public sealed record SkirtingSpec(string Name, string Profile = "bevelled", double Height = 0.08, double Thickness = 0.012, string Material = "MDF", string Color = "#f2efe8")
{
    public static readonly string[] Profiles = ["flush", "bevelled", "moulded", "cove", "pencil", "bullnose"];
    public static SkirtingSpec Default(string name = "MDF 80 bevelled") => new(name);

    /// <summary>The ready-made types the Skirting types dialog offers.</summary>
    public static readonly SkirtingSpec[] Library =
    [
        new("MDF 80 bevelled", "bevelled", 0.08, 0.012, "MDF", "#f2efe8"),
        new("Oak 70 moulded", "moulded", 0.07, 0.015, "Oak", "#b98a55"),
        new("Tile 80 flush", "flush", 0.08, 0.008, "Ceramic tile", "#d9d4cc"),
        new("Aluminium 60 cove", "cove", 0.06, 0.01, "Aluminium", "#c4c8cc"),
        new("Pencil 50", "pencil", 0.05, 0.01, "MDF", "#f2efe8"),
        new("Bullnose 70", "bullnose", 0.07, 0.015, "Pine", "#d9b77e"),
    ];

    public JsonObject ToJson() => (JsonObject)JsonSerializer.SerializeToNode(this, ArchitectureJson.Default.SkirtingSpec)!;

    public static SkirtingSpec? From(JsonNode? node)
    {
        try
        {
            var s = node?.Deserialize(ArchitectureJson.Default.SkirtingSpec);
            if (s is null || s.Name is null) return null;
            return s with
            {
                Profile = Profiles.Contains(s.Profile) ? s.Profile : "flush",
                Height = Math.Clamp(s.Height, 0.04, 0.3), Thickness = Math.Clamp(s.Thickness, 0.005, 0.04),
                Material = string.IsNullOrWhiteSpace(s.Material) ? "MDF" : s.Material.Trim(),
                Color = string.IsNullOrWhiteSpace(s.Color) ? "#f2efe8" : s.Color,
            };
        }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// Skirtings (a tester's report #61, decision 254), the first tool of the Finishes tab: a <c>LapinSkirting</c> of a type
/// (<c>LapinSkirtingType</c>, its <see cref="SkirtingSpec"/>) whose profile is swept along a path in plan, mitred at the
/// corners, standing on its storey's floor (plus <c>baseOffset</c>). The path is a room's outline (<c>roomId</c>: it follows
/// the walls' faces as the room does, its islands too) or a line drawn along a wall's face (<c>points</c>, <c>closed</c>,
/// <c>side</c>: which side of the line the board lies on). Either way it is interrupted where a door (any opening down
/// to the floor) is in the wall the path runs along, unless <c>gaps</c> is false. Exported as IfcCovering SKIRTINGBOARD of an
/// IfcCoveringType SKIRTINGBOARD.
/// </summary>
public static class Skirtings
{
    public const string Class = "LapinSkirting";
    public const string TypeClass = "LapinSkirtingType";
    const string Predefined = "SKIRTINGBOARD";

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(TypeClass, "Skirting type", [], "#d8cbb4", Kind: "type"));
        r.Class(new(Class, "Skirting",
        [
            new("baseOffset", "Base offset", "length", Min: -1, Max: 5),
        ], "#c9b79a"));
        r.Geometry(Class, Geometry);
        r.Quantities(Class, Quantities);
        r.Service<IScheduleSource>(new SkirtingSchedule());
        r.Catalog("skirtings", new JsonObject
        {
            ["spec"] = SkirtingSpec.Default().ToJson(),
            ["library"] = new JsonArray(SkirtingSpec.Library.Select(s => (JsonNode)s.ToJson()).ToArray()),
            ["profiles"] = new JsonArray(SkirtingSpec.Profiles.Select(x => (JsonNode)x).ToArray()),
        });

        r.Command("createSkirtingType", "New skirting type", (ctx, a) =>
        {
            var spec = SkirtingSpec.From(a["spec"]) ?? throw new CommandException("The skirting type cannot be read.");
            return new JsonObject { ["id"] = CreateType(ctx, spec).Id };
        });
        r.Command("updateSkirtingType", "Change skirting type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], TypeClass);
            var spec = SkirtingSpec.From(a["spec"]) ?? throw new CommandException("The skirting type cannot be read.");
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec.ToJson();
            attrs["material"] = EnsureMaterial(ctx, spec).Id;
            ctx.Replace(type.With(name: spec.Name, attrs: attrs));
            return null;
        });
        // A library type in the project: the one of that name when there is one already, else made.
        r.Command("skirtingTypeFromLibrary", "Skirting type from the library", (ctx, a) =>
        {
            var name = (string?)a["name"] ?? throw new CommandException("Which library type?");
            var spec = SkirtingSpec.Library.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new CommandException($"The library has no skirting „{name}”.");
            var found = ctx.Model.OfClass(TypeClass).FirstOrDefault(t => string.Equals(t.Name, spec.Name, StringComparison.OrdinalIgnoreCase));
            return new JsonObject { ["id"] = (found ?? CreateType(ctx, spec)).Id, ["created"] = found == null };
        });

        // A skirting round a room ({roomId} or a point inside it, or inside walls without a room), or along a line drawn ({points, closed?, side?}).
        r.Command("addSkirting", "Skirting", (ctx, a) => new JsonObject { ["id"] = Add(ctx, a).Id });

        r.Command("updateSkirting", "Change skirting", (ctx, a) =>
        {
            var s = Spatial.Require(ctx.Model, (string?)a["id"], Class);
            var attrs = s.CloneAttrs();
            if (a["points"] is JsonArray) attrs["points"] = SectionPaths.PointsJson(CheckPoints(a["points"], attrs["closed"]?.GetValue<bool>() == true));
            if ((string?)a["side"] is { } side) attrs["side"] = side == "flip" ? (Side(s) > 0 ? "right" : "left") : side == "right" ? "right" : "left";
            if (Json.Number(a["baseOffset"]) is { } bo) attrs["baseOffset"] = Math.Clamp(bo, -1, 5);
            if (a["gaps"] is JsonValue g && g.TryGetValue<bool>(out var gaps)) attrs["gaps"] = gaps;
            if ((string?)a["typeId"] is { } tid) ctx.Relate(IfcClass.RelDefinesByType, Spatial.Require(ctx.Model, tid, TypeClass).Id, s.Id);
            ctx.Replace(s.With(attrs: attrs));
            return null;
        });
    }

    internal static Entity Add(ICommandContext ctx, JsonObject a)
    {
        var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        Spatial.Require(ctx.Model, storeyId, IfcClass.BuildingStorey);
        var attrs = new JsonObject { ["exportAs"] = "IfcCovering" };
        if ((string?)a["roomId"] is { } roomId)
        {
            var room = Spatial.Require(ctx.Model, roomId, Rooms.Class);
            storeyId = Spatial.StoreyOf(ctx.Model, room.Id)?.Id ?? storeyId;
            attrs["roomId"] = room.Id;
            Snapshot(ctx, room, attrs);
        }
        else if (a["points"] is JsonArray)
        {
            var closed = a["closed"] is JsonValue c && c.TryGetValue<bool>(out var cl) && cl;
            var pts = CheckPoints(a["points"], closed);
            attrs["points"] = SectionPaths.PointsJson(pts);
            attrs["closed"] = closed;
            attrs["side"] = (string?)a["side"] is "left" or "right" ? (string)a["side"]! : AutoSide(ctx.Model, storeyId, pts);
        }
        else
        {
            var at = Args.Point(a, "x", "y");
            var room = ctx.Model.OfClass(Rooms.Class).Where(x => Spatial.StoreyOf(ctx.Model, x.Id)?.Id == storeyId)
                .FirstOrDefault(x => Rooms.Of(x, ctx.Model, ctx.Query.Geometry)?.Holds(at) == true);
            if (room != null) { attrs["roomId"] = room.Id; Snapshot(ctx, room, attrs); }
            else
            {
                // No room there: the walls round the point, as a room would find them.
                var (region, problem) = RoomBounds.At(ctx.Model, ctx.Services, storeyId, at);
                if (region == null) throw new CommandException(problem ?? "Nothing closes round that point: click inside a room or inside walls, or draw the skirting along a wall.");
                attrs["points"] = SectionPaths.PointsJson(Polygon2.EnsureCcw(region.Outer));
                attrs["closed"] = true;
                attrs["side"] = "left";
            }
        }
        if (Json.Number(a["baseOffset"]) is { } bo) attrs["baseOffset"] = Math.Clamp(bo, -1, 5);
        var type = (string?)a["typeId"] is { } tid ? Spatial.Require(ctx.Model, tid, TypeClass) : DefaultType(ctx);
        var e = new Entity(ctx.NewId(), Class, $"Skirting {Numbering.Next(ctx.Model.OfClass(Class))}", attrs, Predefined);
        ctx.Add(e);
        Spatial.Contain(ctx, storeyId, e.Id);
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        return e;
    }

    /// <summary>A room's outline kept on its skirting, which keeps it when the room is gone.</summary>
    static void Snapshot(ICommandContext ctx, Entity room, JsonObject attrs)
    {
        if (Rooms.Of(room, ctx.Model, ctx.Query.Geometry) is not { } o) return;
        attrs["points"] = SectionPaths.PointsJson(Polygon2.EnsureCcw(o.Outer));
        attrs["closed"] = true;
        attrs["side"] = "left";
    }

    static List<Vec2> CheckPoints(JsonNode? n, bool closed)
    {
        var raw = (n as JsonArray ?? throw new CommandException("A skirting runs along two points at least."))
            .Select(p => p is JsonArray xy && xy.Count >= 2 && Json.Number(xy[0]) is { } x && Json.Number(xy[1]) is { } y ? new Vec2(x, y) : throw new CommandException("Each point is [x, y].")).ToList();
        var pts = new List<Vec2>();
        foreach (var p in raw) if (pts.Count == 0 || (p - pts[^1]).Length > 1e-3) pts.Add(p);
        if (closed && pts.Count > 2 && (pts[0] - pts[^1]).Length <= 1e-3) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < (closed ? 3 : 2)) throw new CommandException(closed ? "A closed skirting needs three points at least." : "A skirting runs along two points at least.");
        return pts;
    }

    /// <summary>The side of a drawn line the board lies on: away from the wall the line runs along (left unless the left is inside a wall).</summary>
    static string AutoSide(IModelView m, string storeyId, IReadOnlyList<Vec2> pts)
    {
        var (a, b) = (pts[0], pts[1]);
        var u = (b - a).Normalized();
        var mid = (a + b) * 0.5;
        return InsideWall(m, storeyId, mid + u.Perp * 0.02) && !InsideWall(m, storeyId, mid - u.Perp * 0.02) ? "right" : "left";
    }

    static bool InsideWall(IModelView m, string storeyId, Vec2 p)
    {
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, w.Id)?.Id != storeyId || PathCurve.Of(w) is { IsArc: true } || w.Attrs["start"] == null) continue;
            var f = Openings.FrameOf(m, w);
            var along = Vec2.Dot(p - f.Start, f.Dir);
            var y = Vec2.Dot(p - f.Start, f.Left);
            if (along >= -1e-3 && along <= PathCurve.Of(w).Length + 1e-3 && y >= f.YMin - 1e-4 && y <= f.YMax + 1e-4) return true;
        }
        return false;
    }

    // ------------------------------------------------------------ types

    public static SkirtingSpec SpecOf(IModelView m, Entity s) =>
        (m.RelatingOf(s.Id, IfcClass.RelDefinesByType) is { } t ? SkirtingSpec.From(t.Attrs["spec"]) : null) ?? SkirtingSpec.Default();

    static Entity CreateType(ICommandContext ctx, SkirtingSpec spec)
    {
        var material = EnsureMaterial(ctx, spec);
        var type = new Entity(ctx.NewId(), TypeClass, spec.Name, new JsonObject { ["spec"] = spec.ToJson(), ["exportAs"] = "IfcCoveringType", ["material"] = material.Id }, Predefined);
        ctx.Add(type);
        return type;
    }

    /// <summary>The project's skirting type of that name, else the library's brought in; null when neither has it.</summary>
    internal static Entity? TypeNamed(ICommandContext ctx, string name)
    {
        if (ctx.Model.OfClass(TypeClass).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) is { } found) return found;
        return SkirtingSpec.Library.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) is { } spec ? CreateType(ctx, spec) : null;
    }

    static Entity DefaultType(ICommandContext ctx) => ctx.Model.OfClass(TypeClass).FirstOrDefault() ?? CreateType(ctx, SkirtingSpec.Default());

    /// <summary>The type's material, made a finish (so a view style without finishes leaves it out) when the project had none of that name.</summary>
    static Entity EnsureMaterial(ICommandContext ctx, SkirtingSpec spec)
    {
        if (Materials.Find(ctx.Model, spec.Material) is { } found) return found;
        var made = Materials.Ensure(ctx, spec.Material, spec.Color);
        var attrs = made.CloneAttrs();
        attrs["category"] = "Finish";
        var finish = made.With(attrs: attrs);
        ctx.Replace(finish);
        return finish;
    }

    // ------------------------------------------------------------ the profile

    /// <summary>
    /// The board's section as a polygon anticlockwise in (d, z): d from the wall's face (0) out to the thickness, z from the
    /// floor up to the height; curves as short chords.
    /// </summary>
    public static List<Vec2> Profile(SkirtingSpec s)
    {
        double t = s.Thickness, h = s.Height;
        var o = new List<Vec2> { new(0, 0), new(t, 0) };
        void Arc(Vec2 c, double r, double from, double to, int n)
        {
            for (var i = 1; i <= n; i++)
            {
                var a = (from + (to - from) * i / n) * Math.PI / 180;
                o.Add(new Vec2(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)));
            }
        }
        switch (s.Profile)
        {
            case "bevelled":
            {
                // A sloped top, a third of the thickness left square at the back.
                var run = t * 0.65;
                o.Add(new Vec2(t, h - Math.Min(run * 1.5, h * 0.4)));
                o.Add(new Vec2(t - run, h));
                break;
            }
            case "moulded":
            {
                // An ogee from the plinth's face to a narrow top: x eases from t to 0.4 t over the top 40 % of the height.
                var z0 = h * 0.6;
                const int n = 10;
                o.Add(new Vec2(t, z0));
                for (var i = 1; i <= n; i++)
                {
                    var k = (double)i / n;
                    o.Add(new Vec2(t - (t - t * 0.4) * (0.5 - 0.5 * Math.Cos(Math.PI * k)), z0 + (h - z0) * k));
                }
                break;
            }
            case "cove":
            {
                // A thin plate at the back, its foot curving out to the floor.
                var plate = Math.Max(0.0015, t * 0.15);
                var r = t - plate;
                Arc(new Vec2(t, r), r, 270, 180, 8);
                o.Add(new Vec2(plate, h));
                break;
            }
            case "pencil":
                // A half-round top.
                o.Add(new Vec2(t, h - t / 2));
                Arc(new Vec2(t / 2, h - t / 2), t / 2, 0, 180, 10);
                return o;
            case "bullnose":
                // The top front edge rounded to the full thickness.
                o.Add(new Vec2(t, h - t));
                Arc(new Vec2(0, h - t), t, 0, 90, 8);
                o.RemoveAt(o.Count - 1);
                break;
            default:
                o.Add(new Vec2(t, h));
                break;
        }
        o.Add(new Vec2(0, h));
        return o;
    }

    /// <summary>The profile's area (m²).</summary>
    public static double Area(SkirtingSpec s) => Math.Abs(Polygon2.SignedArea(Profile(s)));

    // ------------------------------------------------------------ the path

    /// <summary>One continuous piece of a skirting: its line (on the wall's face), closed when it runs round without a break.</summary>
    public sealed record Run(IReadOnlyList<Vec2> Points, bool Closed)
    {
        public double Length => Closed ? Polygon2.Perimeter(Points) : Points.Zip(Points.Skip(1), (a, b) => (b - a).Length).Sum();
    }

    static int Side(Entity s) => s.Str("side") == "right" ? -1 : 1;

    static List<Vec2> StoredPoints(Entity s) => (s.Attrs["points"] as JsonArray)?.Select(Args.PointOf).ToList() ?? [];

    /// <summary>
    /// The lines the skirting follows, each with the side its board lies on (+1: left): a room's outline anticlockwise and
    /// its islands clockwise (the room on their left), else the line it stores.
    /// </summary>
    public static List<(IReadOnlyList<Vec2> Points, bool Closed, int Side)> Paths(Entity s, IModelView m, IServices? services)
    {
        var o = new List<(IReadOnlyList<Vec2>, bool, int)>();
        if (s.Str("roomId") is { } rid && m.Get(rid) is { Class: Rooms.Class } room && Rooms.Live(room, m, services) is { } outline)
        {
            o.Add((Polygon2.EnsureCcw(outline.Outer), true, 1));
            foreach (var island in outline.Islands) o.Add((Polygon2.EnsureCcw(island).Reverse().ToList(), true, 1));
            return o;
        }
        var pts = StoredPoints(s);
        var closed = s.Bool("closed");
        if (pts.Count >= (closed ? 3 : 2)) o.Add((pts, closed, Side(s)));
        return o;
    }

    /// <summary>
    /// Where the line passes a door: along each of its sides, the stretches in front of an opening that reaches down below the
    /// board's top (<paramref name="height"/>) in a wall of the storey running the same way, the side lying on or in that wall.
    /// Distances along the line from its start.
    /// </summary>
    public static List<(double S0, double S1)> Gaps(IModelView m, string? storeyId, IReadOnlyList<Vec2> pts, bool closed, double height)
    {
        var o = new List<(double, double)>();
        var openings = new List<(Openings.WallFrame F, Openings.Placed P)>();
        foreach (var w in m.OfClass(IfcClass.Wall))
        {
            if (Spatial.StoreyOf(m, w.Id)?.Id != storeyId || w.Attrs["start"] == null) continue;
            foreach (var p in Openings.PlacedOn(m, w.Id))
                if (p.Sill < height - 1e-6) openings.Add((Openings.FrameOf(m, w, p.Along), p));
        }
        if (openings.Count == 0) return o;
        var s = 0.0;
        var n = closed ? pts.Count : pts.Count - 1;
        for (var k = 0; k < n; k++)
        {
            var (a, b) = (pts[k], pts[(k + 1) % pts.Count]);
            var len = (b - a).Length;
            if (len < 1e-9) continue;
            var u = (b - a) * (1 / len);
            foreach (var (f, p) in openings)
            {
                if (Math.Abs(Vec2.Cross(u, f.Dir)) > 0.05) continue;
                var y = Vec2.Dot((a + b) * 0.5 - f.Start, f.Left);
                if (y < f.YMin - 0.02 || y > f.YMax + 0.02) continue;
                var c = f.Start + f.Dir * p.Along;
                var t0 = Vec2.Dot(c - f.Dir * (p.Width / 2) - a, u);
                var t1 = Vec2.Dot(c + f.Dir * (p.Width / 2) - a, u);
                if (t0 > t1) (t0, t1) = (t1, t0);
                (t0, t1) = (Math.Max(t0, 0), Math.Min(t1, len));
                if (t1 - t0 > 0.005) o.Add((s + t0, s + t1));
            }
            s += len;
        }
        return o;
    }

    /// <summary>The pieces a line leaves between its gaps (distances along it); the whole line when there are none.</summary>
    public static List<Run> Split(IReadOnlyList<Vec2> pts, bool closed, IEnumerable<(double S0, double S1)> gaps)
    {
        var cum = new List<double> { 0 };
        var n = closed ? pts.Count : pts.Count - 1;
        for (var k = 0; k < n; k++) cum.Add(cum[^1] + (pts[(k + 1) % pts.Count] - pts[k]).Length);
        var total = cum[^1];
        // The gaps in order, merged where they touch.
        var merged = new List<(double S0, double S1)>();
        foreach (var g in gaps.Select(g => (Math.Max(0, g.S0), Math.Min(total, g.S1))).Where(g => g.Item2 > g.Item1).OrderBy(g => g.Item1))
        {
            if (merged.Count > 0 && g.Item1 <= merged[^1].S1 + 1e-6) merged[^1] = (merged[^1].S0, Math.Max(merged[^1].S1, g.Item2));
            else merged.Add(g);
        }
        if (merged.Count == 0) return [new Run(pts, closed)];
        var spans = new List<(double S0, double S1)>();
        if (closed)
        {
            for (var i = 0; i + 1 < merged.Count; i++) spans.Add((merged[i].S1, merged[i + 1].S0));
            spans.Add((merged[^1].S1, total + merged[0].S0));
        }
        else
        {
            spans.Add((0, merged[0].S0));
            for (var i = 0; i + 1 < merged.Count; i++) spans.Add((merged[i].S1, merged[i + 1].S0));
            spans.Add((merged[^1].S1, total));
        }
        Vec2 At(double s)
        {
            if (closed) { s %= total; if (s < 0) s += total; }
            for (var k = 0; k < n; k++)
                if (s <= cum[k + 1] + 1e-12)
                {
                    var l = cum[k + 1] - cum[k];
                    return l < 1e-12 ? pts[k] : pts[k] + (pts[(k + 1) % pts.Count] - pts[k]) * ((s - cum[k]) / l);
                }
            return closed ? pts[0] : pts[^1];
        }
        var runs = new List<Run>();
        foreach (var (s0, s1) in spans)
        {
            if (s1 - s0 < 0.01) continue;
            var run = new List<Vec2> { At(s0) };
            // The corners strictly inside the span (twice round for a closed line's last span).
            for (var lap = 0; lap < (closed ? 2 : 1); lap++)
                for (var k = 0; k < pts.Count; k++)
                {
                    var sk = cum[k] + lap * total;
                    if (sk > s0 + 1e-6 && sk < s1 - 1e-6) run.Add(pts[k]);
                }
            run.Add(At(s1));
            runs.Add(new Run(run, false));
        }
        return runs;
    }

    /// <summary>The skirting's pieces: its lines split at the doors (unless <c>gaps</c> is false), with the side each board lies on.</summary>
    public static List<(Run Run, int Side)> Runs(Entity s, IModelView m, IServices? services)
    {
        var spec = SpecOf(m, s);
        var storey = Spatial.StoreyOf(m, s.Id)?.Id;
        var gaps = s.Attrs["gaps"] is not JsonValue g || !g.TryGetValue<bool>(out var on) || on;
        var o = new List<(Run, int)>();
        foreach (var (pts, closed, side) in Paths(s, m, services))
            foreach (var run in Split(pts, closed, gaps ? Gaps(m, storey, pts, closed, spec.Height) : []))
                o.Add((run, side));
        return o;
    }

    /// <summary>
    /// The profile swept along a run, its board on <paramref name="side"/> of the line (+1: left), mitred at each corner (the
    /// section there lies in the corner's bisecting plane), square at an open run's ends.
    /// </summary>
    public static Solid? Sweep(Run run, IReadOnlyList<Vec2> profile, int side, double z0)
    {
        var pts = run.Points;
        var count = pts.Count;
        if (count < (run.Closed ? 3 : 2)) return null;
        var segs = run.Closed ? count : count - 1;
        var normals = new Vec2[segs];
        for (var i = 0; i < segs; i++)
        {
            var d = pts[(i + 1) % count] - pts[i];
            if (d.Length < 1e-9) return null;
            normals[i] = d.Normalized().Perp * side;
        }
        var verts = new List<Vec3>();
        var rings = new List<int[]>();
        for (var i = 0; i < count; i++)
        {
            var incoming = run.Closed ? normals[(i - 1 + segs) % segs] : normals[Math.Max(i - 1, 0)];
            var outgoing = normals[Math.Min(i, segs - 1)];
            var dot = Vec2.Dot(incoming, outgoing);
            var mitre = dot < -0.95 ? incoming : (incoming + outgoing) * (1 / (1 + dot));
            rings.Add(profile.Select(q => { verts.Add((pts[i] + mitre * q.X).At(z0 + q.Y)); return verts.Count - 1; }).ToArray());
        }
        var faces = new List<FaceInput>();
        var np = profile.Count;
        for (var i = 0; i < segs; i++)
        {
            var (lo, hi) = (rings[i], rings[(i + 1) % count]);
            for (var k = 0; k < np; k++)
            {
                var j = (k + 1) % np;
                faces.Add(new FaceInput([lo[k], lo[j], hi[j], hi[k]], null, null, "side"));
            }
        }
        if (!run.Closed)
        {
            faces.Add(new FaceInput(rings[0].Reverse().ToArray(), null, null, "start"));
            faces.Add(new FaceInput(rings[^1], null, null, "end"));
        }
        var solid = SolidBuilder.Make(verts, faces, "skirting");
        return Measure.SignedVolume(solid) < 0 ? SolidBuilder.Flip(solid) : solid;
    }

    static double BaseZ(Entity s, IModelView m) => (Spatial.StoreyOf(m, s.Id)?.Num("elevation") ?? 0) + s.Num("baseOffset");

    public static ElementGeometry? Geometry(Entity s, IModelView m, IServices services)
    {
        var runs = Runs(s, m, services);
        if (runs.Count == 0) return new ElementGeometry([], Diagnostics: ["The skirting has no line: its room has no outline, or it was drawn without one."]);
        var spec = SpecOf(m, s);
        var profile = Profile(spec);
        var z = BaseZ(s, m);
        var solids = runs.Select(x => Sweep(x.Run, profile, x.Side, z)).OfType<Solid>().ToList();
        var parts = new List<GeometryPart>();
        if (solids.Count > 0) parts.Add(new GeometryPart("Skirting", spec.Color, Openings.Combine(solids, "skirting"), Materials.Find(m, spec.Material)?.Id, "skirting"));
        // In plan: its front edge, seen below the cut, and its line on the wall's face (what a click takes, and what it measures).
        var plan = new List<PlanItem>();
        foreach (var (run, side) in runs)
        {
            var line = run.Closed ? [.. run.Points, run.Points[0]] : run.Points.ToList();
            plan.Add(new PlanItem("hit", false, line));
            plan.Add(new PlanItem("symbol", false, Offset(line, run.Closed, spec.Thickness * side)));
        }
        return new ElementGeometry(parts, plan, runs.SelectMany(x => x.Run.Points).ToList());
    }

    /// <summary>A polyline offset to its left by <paramref name="d"/> (negative: right), mitred at its corners.</summary>
    static List<Vec2> Offset(IReadOnlyList<Vec2> line, bool closed, double d)
    {
        var o = new List<Vec2>();
        var count = line.Count;
        for (var i = 0; i < count; i++)
        {
            Vec2 N(int a, int b) => (line[b] - line[a]).Normalized().Perp;
            var hasIn = i > 0 || closed;
            var hasOut = i < count - 1 || closed;
            // A closed line repeats its first point last: its ends are one corner.
            var inN = hasIn ? N(i > 0 ? i - 1 : count - 2, i > 0 ? i : count - 1) : N(0, 1);
            var outN = hasOut ? N(i < count - 1 ? i : 0, i < count - 1 ? i + 1 : 1) : inN;
            var dot = Vec2.Dot(inN, outN);
            o.Add(line[i] + (dot < -0.95 ? inN : (inN + outN) * (1 / (1 + dot))) * d);
        }
        return o;
    }

    /// <summary>The skirting's length as built: its pieces' lines (the plan's "hit" items), the doors' gaps left out.</summary>
    public static double LengthOf(ElementGeometry? g) =>
        g?.Plan?.Where(p => p.Kind == "hit").Sum(p => p.Points.Zip(p.Points.Skip(1), (a, b) => (b - a).Length).Sum()) ?? 0;

    public static IEnumerable<QuantitySet> Quantities(Entity s, ElementGeometry? g, IModelView m)
    {
        if (g?.Plan == null) yield break;
        var spec = SpecOf(m, s);
        var length = LengthOf(g);
        var pieces = g.Plan.Count(p => p.Kind == "hit");
        yield return new QuantitySet("Qto_CoveringBaseQuantities", [new("Width", spec.Thickness, "LENGTH"), new("GrossArea", length * spec.Height, "AREA"), new("NetArea", length * spec.Height, "AREA")]);
        yield return new QuantitySet("Lapin_SkirtingQuantities",
        [
            new("Length", length, "LENGTH", "Length"), new("Height", spec.Height, "LENGTH", "Height"),
            new("Pieces", pieces, "COUNT", "Pieces"), new("Volume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Volume"),
        ]);
    }
}

/// <summary>The skirtings schedule (decision 254): by type, its profile, height and material, how many skirtings and pieces and their length.</summary>
public sealed class SkirtingSchedule : IScheduleSource
{
    public string Kind => "skirtings";
    public string Label => "Skirtings";

    public sealed record Row(string Type, string Profile, double Height, string Material, int Count, int Pieces, double Length);

    public static List<Row> Rows(IQueryContext q)
    {
        var m = q.Model;
        return m.OfClass(Skirtings.Class)
            .Select(s => (Spec: Skirtings.SpecOf(m, s), G: q.Geometry(s.Id)))
            .GroupBy(x => x.Spec.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new Row(g.Key, g.First().Spec.Profile, g.First().Spec.Height, g.First().Spec.Material, g.Count(),
                g.Sum(x => x.G?.Plan?.Count(p => p.Kind == "hit") ?? 0), g.Sum(x => Skirtings.LengthOf(x.G))))
            .ToList();
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = Rows(q);
        var grid = rows.Select(r => (IReadOnlyList<string>)[r.Type, r.Profile, N(r.Height * 1000, "0"), r.Material, r.Count.ToString(CultureInfo.InvariantCulture), r.Pieces.ToString(CultureInfo.InvariantCulture), N(r.Length)]).ToList();
        if (rows.Count > 0) grid.Add(["Total", "", "", "", rows.Sum(r => r.Count).ToString(CultureInfo.InvariantCulture), rows.Sum(r => r.Pieces).ToString(CultureInfo.InvariantCulture), N(rows.Sum(r => r.Length))]);
        return new ScheduleGrid(Label, ["Type", "Profile", "Height (mm)", "Material", "Skirtings", "Pieces", "Length (m)"], grid);
    }
}
