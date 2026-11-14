using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>A kind of cable carrier: a ladder, a perforated or solid tray, trunking, conduit; its IFC4 predefined type, its indicative fill limit (%) and inside bend radius (m).</summary>
public sealed record CarrierKind(string Key, string Label, string Predefined, double FillLimit, double BendRadius)
{
    public bool Round => Key == "conduit";
}

/// <summary>What a carrier is built from: its kind and size (its type's, or its own), the sheet's thickness, the inside bend radius, the fill limit.</summary>
public sealed record CarrierSpec(CarrierKind Kind, double Width, double Height, double Diameter, double Thickness, double BendRadius, double FillLimit)
{
    /// <summary>Across in plan, and up: a conduit's diameter both ways.</summary>
    public double Across => Kind.Round ? Diameter : Width;
    public double Up => Kind.Round ? Diameter : Height;

    /// <summary>The cross-section cables may fill (m²): inside the walls, under a trunking's lid; inside a conduit's bore.</summary>
    public double Usable => Kind.Round
        ? Math.PI * Math.Pow(Math.Max(0, Diameter / 2 - Thickness), 2)
        : Math.Max(0, Width - 2 * Thickness) * Math.Max(0, Height - (Kind.Key == "trunking" ? 2 : 1) * Thickness);

    public string SizeLabel => Kind.Round ? $"Ø{Mm(Diameter)}" : $"{Mm(Width)}×{Mm(Height)}";
    internal static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);
}

/// <summary>A built-in carrier type: indicative, as everything Lapin ships (the user's own types are theirs).</summary>
public sealed record CarrierPreset(string Key, string Name, string Kind, double Width, double Height, double Diameter = 0)
{
    public JsonObject ToJson() => new() { ["key"] = Key, ["name"] = Name, ["kind"] = Kind, ["width"] = Width, ["height"] = Height, ["diameter"] = Diameter, ["indicative"] = true };
}

/// <summary>
/// Cable carriers (testers' report #147, docs/user/mep.md, Cable trays and trunking): cable ladders, perforated and solid
/// trays, trunking with its lid and conduit, each an <c>IfcCableCarrierSegment</c> of an <c>IfcCableCarrierSegmentType</c>
/// that holds its kind and size. A carrier is drawn as the runs are: a polyline in plan on a storey at an elevation, its
/// legs laid at 45° steps; <c>levels</c> (one per point, null: the elevation) say the height each point goes on from, so
/// a change of level is a vertical leg at that point. Its ends may join another carrier (<c>IfcRelConnectsPorts</c>, as
/// runs do): inside its length a tee (a cross where two meet it across from each other), at its end a reducer when the
/// sizes differ. Bends, tees, crosses and reducers are the carrier's fittings, worked out from it (never stored) and
/// written to IFC as <c>IfcCableCarrierFitting</c>s aggregated in it. A cable run says which carrier it lies in
/// (<c>carrier</c>); the carrier's fill is the cables' cross-sections over its usable area.
/// </summary>
public static class CableCarriers
{
    public const string Class = "IfcCableCarrierSegment";
    public const string TypeClass = "IfcCableCarrierSegmentType";
    public const string Connects = Runs.Connects;

    public static readonly IReadOnlyList<CarrierKind> Kinds =
    [
        new("ladder", "Cable ladder", "CABLELADDERSEGMENT", 50, 0.15),
        new("perforated", "Perforated cable tray", "CABLETRAYSEGMENT", 50, 0.1),
        new("solid", "Solid cable tray", "CABLETRAYSEGMENT", 50, 0.1),
        new("trunking", "Trunking", "CABLETRUNKINGSEGMENT", 45, 0.05),
        new("conduit", "Conduit", "CONDUITSEGMENT", 40, 0.06),
    ];

    public static CarrierKind Kind(string? key) => Kinds.FirstOrDefault(k => k.Key == key) ?? Kinds[1];

    /// <summary>The built-in types, indicative.</summary>
    public static readonly IReadOnlyList<CarrierPreset> Presets =
    [
        new("tray100x60", "Perforated tray 100×60", "perforated", 0.1, 0.06),
        new("tray200x60", "Perforated tray 200×60", "perforated", 0.2, 0.06),
        new("tray300x60", "Perforated tray 300×60", "perforated", 0.3, 0.06),
        new("tray400x100", "Perforated tray 400×100", "perforated", 0.4, 0.1),
        new("solid200x60", "Solid tray 200×60", "solid", 0.2, 0.06),
        new("ladder300x100", "Cable ladder 300×100", "ladder", 0.3, 0.1),
        new("ladder600x100", "Cable ladder 600×100", "ladder", 0.6, 0.1),
        new("trunking40x40", "Trunking 40×40", "trunking", 0.04, 0.04),
        new("trunking60x40", "Trunking 60×40", "trunking", 0.06, 0.04),
        new("trunking100x60", "Trunking 100×60", "trunking", 0.1, 0.06),
        new("conduit20", "Conduit Ø20", "conduit", 0, 0, 0.02),
        new("conduit25", "Conduit Ø25", "conduit", 0, 0, 0.025),
        new("conduit32", "Conduit Ø32", "conduit", 0, 0, 0.032),
    ];

    public static CarrierPreset? Preset(string? key) => Presets.FirstOrDefault(p => p.Key == key || p.Name == key);

    public static bool IsCarrier(Entity e) => e.Class == Class && e.Attrs.ContainsKey("points");
    public static IEnumerable<Entity> All(IModelView m) => m.OfClass(Class).Where(IsCarrier);
    public static List<Vec2> Points(Entity e) => Args.StoredPoints(e.Attrs["points"]);

    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { Class: TypeClass } t ? t : null;

    /// <summary>The carrier's kind and size: its own values over its type's, the kind's defaults under both.</summary>
    public static CarrierSpec SpecOf(IModelView m, Entity e)
    {
        var t = e.Class == TypeClass ? e : TypeOf(m, e);
        var kind = Kind(t?.Str("kind") ?? e.Str("kind"));
        double V(string k, double d) => (e.Class == TypeClass ? null : e.NumOrNull(k)) ?? t?.NumOrNull(k) ?? d;
        var diameter = V("diameter", 0.025);
        return new CarrierSpec(kind, V("width", 0.2), V("height", 0.06), diameter, V("thickness", kind.Round ? 0.0015 : 0.0012),
            V("bendRadius", kind.Round ? Math.Max(kind.BendRadius, 3 * diameter) : kind.BendRadius), V("fillLimit", kind.FillLimit));
    }

    public static double Elevation(Entity e) => e.NumOrNull("elevation") ?? 2.4;

    /// <summary>Each point's level above the storey: its own, else the carrier's elevation.</summary>
    public static List<double> Levels(Entity e)
    {
        var pts = Points(e);
        var own = e.Attrs["levels"] as JsonArray;
        var el = Elevation(e);
        return [.. pts.Select((_, i) => own != null && own.Count == pts.Count && Json.Number(own[i]) is { } v ? v : el)];
    }

    // ─── Laying ─────────────────────────────────────────────────────────────

    /// <summary>The direction of a leg laid at the nearest 45° step.</summary>
    public static Vec2 Step45(Vec2 d)
    {
        if (d.Length < 1e-12) return d;
        var a = Math.Round(Math.Atan2(d.Y, d.X) / (Math.PI / 4)) * (Math.PI / 4);
        return new Vec2(Math.Cos(a), Math.Sin(a));
    }

    /// <summary>The points laid: each leg at the nearest 45° step, as long as the drawn leg is along it (horizontal legs turn by 45° or 90°).</summary>
    public static List<Vec2> Lay(IReadOnlyList<Vec2> pts)
    {
        var o = new List<Vec2>(pts.Count);
        for (var i = 0; i < pts.Count; i++)
        {
            if (i == 0) { o.Add(pts[0]); continue; }
            var d = pts[i] - pts[i - 1];
            var u = Step45(d);
            var p = o[^1] + u * Vec2.Dot(d, u);
            o.Add(new Vec2(Math.Round(p.X, 6), Math.Round(p.Y, 6)));
        }
        return o;
    }

    // ─── Connections ────────────────────────────────────────────────────────

    public sealed record CarrierEnd(string CarrierId, double Station);

    public static Relation? Connection(IModelView m, string id, string end) =>
        m.RelationsOf(id, Connects).FirstOrDefault(r => r.Relating == id && (string?)r.Attrs?["end"] == end);

    public static CarrierEnd? EndOf(IModelView m, Entity e, string end) =>
        Connection(m, e.Id, end) is { Related.Count: > 0 } c && m.Get(c.Related[0]) is { } other && IsCarrier(other) ? new CarrierEnd(other.Id, Json.Number(c.Attrs?["station"]) ?? 0) : null;

    /// <summary>The carriers whose ends join this one, at their stations.</summary>
    public static IEnumerable<(Entity Branch, string End, double Station)> BranchesOf(IModelView m, string id) =>
        m.RelationsOf(id, Connects).Where(r => r.Related.Contains(id) && r.Relating != id)
            .Select(r => (m.Get(r.Relating), (string?)r.Attrs?["end"] ?? "start", Json.Number(r.Attrs?["station"]) ?? 0))
            .Where(x => x.Item1 != null && IsCarrier(x.Item1!)).Select(x => (x.Item1!, x.Item2, x.Item3));

    /// <summary>Whether a station is inside the carrier's length (a tee), not at an end (a reducer or a coupling).</summary>
    public static bool Inside(Entity trunk, double station) => station > 1e-3 && station < Pipes.Length(Points(trunk)) - 1e-3;

    static bool Leads(IModelView m, Entity from, string target)
    {
        var seen = new HashSet<string>();
        var todo = new Stack<Entity>([from]);
        while (todo.TryPop(out var c))
        {
            if (!seen.Add(c.Id)) continue;
            foreach (var end in new[] { "start", "end" })
                if (EndOf(m, c, end) is { } x)
                {
                    if (x.CarrierId == target) return true;
                    if (m.Get(x.CarrierId) is { } n) todo.Push(n);
                }
        }
        return false;
    }

    public static void Connect(ICommandContext ctx, Entity e, string end, Entity trunk, double? station, Vec2? at)
    {
        if (end is not ("start" or "end")) throw new CommandException("A carrier connects at its start or its end.");
        if (trunk.Id == e.Id) throw new CommandException("A carrier cannot join itself.");
        if (!IsCarrier(trunk)) throw new CommandException("A carrier joins another cable carrier.");
        if (Leads(ctx.Model, trunk, e.Id)) throw new CommandException($"„{trunk.Name}” is already joined to „{e.Name}”: the carriers would close a loop.");
        var tp = Points(trunk);
        var mine = Points(e);
        var st = station ?? Pipes.Nearest(tp, at ?? (end == "start" ? mine[0] : mine[^1])).Station;
        var length = Pipes.Length(tp);
        // An end within 50 mm of the trunk's own end meets it there (end to end).
        if (st < 0.05) st = 0; else if (st > length - 0.05) st = length;
        Disconnect(ctx, e, end);
        ctx.AddRelation(new Relation(ctx.NewId(), Connects, e.Id, [trunk.Id], new JsonObject { ["end"] = end, ["station"] = Math.Round(Math.Clamp(st, 0, length), 5) }));
    }

    public static void Disconnect(ICommandContext ctx, Entity e, string end)
    {
        if (Connection(ctx.Model, e.Id, end) is { } c) ctx.RemoveRelation(c.Id);
    }

    // ─── The centreline ─────────────────────────────────────────────────────

    /// <summary>Where a trunk is at a station: in plan, and its level there (above its storey).</summary>
    public static (Vec2 At, double Level, Vec2 Dir) OnTrunk(Entity trunk, double station)
    {
        var pts = Points(trunk);
        var (at, leg, dir) = Pipes.AtStation(pts, station);
        var levels = Levels(trunk);
        var level = station >= Pipes.Length(pts) - 1e-6 ? levels[^1] : levels[Math.Min(leg, levels.Count - 1)];
        return (at, level, dir);
    }

    /// <summary>The carrier's points in plan and their levels as built: an end on another carrier moved onto it, at its level (and the leg next to it).</summary>
    public static (List<Vec2> Points, List<double> Levels) Followed(IModelView m, Entity e)
    {
        var pts = Points(e);
        var lv = Levels(e);
        if (pts.Count < 2) return (pts, lv);
        foreach (var end in new[] { "start", "end" })
        {
            if (EndOf(m, e, end) is not { } c || m.Get(c.CarrierId) is not { } trunk) continue;
            var (at, level, _) = OnTrunk(trunk, c.Station);
            if (end == "start") { pts[0] = at; lv[0] = level; }
            else { pts[^1] = at; lv[^1] = level; lv[^2] = level; }
        }
        return (pts, lv);
    }

    /// <summary>
    /// The centreline in space: each leg level at the level of the point it leaves, a vertical leg wherever the level
    /// changes (at the point that changes it); points on one spot and legs in line merged.
    /// </summary>
    public static List<Vec3> Centreline(IModelView m, Entity e)
    {
        var z0 = Nodes.StoreyZ(m, e);
        var (pts, lv) = Followed(m, e);
        var o = new List<Vec3>();
        for (var i = 0; i < pts.Count; i++)
        {
            if (i > 0) o.Add(new Vec3(pts[i].X, pts[i].Y, z0 + lv[i - 1]));
            o.Add(new Vec3(pts[i].X, pts[i].Y, z0 + lv[i]));
        }
        return Clean(o);
    }

    internal static List<Vec3> Clean(List<Vec3> line)
    {
        var o = new List<Vec3>();
        foreach (var p in line)
        {
            if (o.Count > 0 && (p - o[^1]).Length < 1e-6) continue;
            if (o.Count >= 2)
            {
                var a = (o[^1] - o[^2]).Normalized();
                var b = (p - o[^1]).Normalized();
                if (Vec3.Dot(a, b) > 1 - 1e-9) { o[^1] = p; continue; }
            }
            o.Add(p);
        }
        return o;
    }

    public static double Length(IReadOnlyList<Vec3> line)
    {
        double l = 0;
        for (var i = 1; i < line.Count; i++) l += (line[i] - line[i - 1]).Length;
        return l;
    }

    // ─── Fill ───────────────────────────────────────────────────────────────

    /// <summary>The cable runs that lie in the carrier.</summary>
    public static IEnumerable<Entity> Cables(IModelView m, string carrierId) =>
        Runs.All(m).Where(r => r.Class == "IfcCableSegment" && r.Str("carrier") == carrierId);

    public sealed record Fill(double Cables, double Usable, double Percent, double Limit, int Count)
    {
        public bool Over => Percent > Limit + 1e-9;
    }

    /// <summary>The cables' cross-sections (each run's size its cable's diameter, a pair two) over the carrier's usable area, in percent.</summary>
    public static Fill FillOf(IModelView m, Entity carrier)
    {
        var s = SpecOf(m, carrier);
        double area = 0; var n = 0;
        foreach (var run in Cables(m, carrier.Id))
        {
            var rs = Runs.SettingOf(m, run);
            var one = Math.PI * rs.Size * rs.Size / 4;
            var k = rs.Pair ? 2 : 1;
            area += one * k; n += k;
        }
        var pct = s.Usable > 1e-12 ? 100 * area / s.Usable : 0;
        return new Fill(area, s.Usable, Math.Round(pct, 1), s.FillLimit, n);
    }

    // ─── Types ──────────────────────────────────────────────────────────────

    static readonly string[] SpecKeys = ["width", "height", "diameter", "thickness", "bendRadius", "fillLimit"];

    static string Unique(IModelView m, string name)
    {
        var taken = m.OfClass(TypeClass).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var i = 2; ; i++) if (!taken.Contains($"{name} {i}")) return $"{name} {i}";
    }

    static JsonObject TypeAttrs(CarrierKind kind, JsonObject a)
    {
        var o = new JsonObject { ["kind"] = kind.Key };
        foreach (var k in SpecKeys) if (Json.Number(a[k]) is { } v) o[k] = Positive(k, v);
        return o;
    }

    static double Positive(string key, double v)
    {
        if (key == "fillLimit") return v is > 0 and <= 100 ? v : throw new CommandException("A fill limit is a percentage over 0 and up to 100.");
        if (key == "bendRadius") return v >= 0 ? v : throw new CommandException("A bend radius is not negative.");
        if (!(v > 0) || v > 5) throw new CommandException($"A carrier's {key} is over 0 and under 5 m.");
        return v;
    }

    public static Entity CreateType(IEdit edit, string? kindKey, string? name, JsonObject a, bool indicative = false)
    {
        var kind = Kinds.FirstOrDefault(k => k.Key == kindKey) ?? throw new CommandException($"A cable carrier is one of {string.Join(", ", Kinds.Select(k => k.Key))}.");
        var attrs = TypeAttrs(kind, a);
        if (indicative) attrs["indicative"] = true;
        var t0 = new Entity("", TypeClass, "", attrs);
        var spec = SpecOf(edit.Model, t0);
        var type = new Entity(edit.NewId(), TypeClass, Unique(edit.Model, string.IsNullOrWhiteSpace(name) ? $"{kind.Label} {spec.SizeLabel}" : name.Trim()), attrs, kind.Predefined);
        edit.Add(type);
        return type;
    }

    static Entity FromPreset(IEdit edit, CarrierPreset p) =>
        edit.Model.OfClass(TypeClass).FirstOrDefault(t => t.Str("preset") == p.Key) is { } had ? had : AddPreset(edit, p);

    static Entity AddPreset(IEdit edit, CarrierPreset p)
    {
        var a = new JsonObject();
        if (p.Width > 0) a["width"] = p.Width;
        if (p.Height > 0) a["height"] = p.Height;
        if (p.Diameter > 0) a["diameter"] = p.Diameter;
        var t = CreateType(edit, p.Kind, p.Name, a, indicative: true);
        var attrs = t.CloneAttrs();
        attrs["preset"] = p.Key;
        edit.Replace(t.With(attrs: attrs));
        return edit.Model.Get(t.Id)!;
    }

    static Entity RequireType(IModelView m, string? id) => m.Get(id ?? "") is { Class: TypeClass } t ? t : throw new CommandException("There is no such cable carrier type.");

    /// <summary>The type asked for: by id, a built-in one (made once), else the project's first of the kind, else the kind's first built-in.</summary>
    public static Entity TypeFor(IEdit edit, JsonObject a)
    {
        if ((string?)a["typeId"] is { } id) return RequireType(edit.Model, id);
        if ((string?)a["preset"] is { } pk) return FromPreset(edit, Preset(pk) ?? throw new CommandException($"There is no built-in carrier type „{pk}”."));
        var kind = (string?)a["kind"] ?? "perforated";
        if (!Kinds.Any(k => k.Key == kind)) throw new CommandException($"A cable carrier is one of {string.Join(", ", Kinds.Select(k => k.Key))}.");
        return edit.Model.OfClass(TypeClass).FirstOrDefault(t => t.Str("kind") == kind) ?? FromPreset(edit, Presets.First(p => p.Kind == kind));
    }

    // ─── Commands ───────────────────────────────────────────────────────────

    static List<Vec2> PointsArg(JsonNode? n)
    {
        var pts = (n as JsonArray)?.Select((p, i) => Args.Point(p, $"point {i + 1} of the carrier")).ToList() ?? [];
        if (pts.Count < 2) throw new CommandException("A carrier needs at least two points [[x, y], …].");
        return pts;
    }

    static JsonArray? LevelsArg(JsonNode? n, int count)
    {
        if (n is not JsonArray a) return null;
        if (a.Count != count) throw new CommandException($"One level per point ({count}), or null.");
        return a.Any(x => Json.Number(x) != null) ? new JsonArray([.. a.Select(x => Json.Number(x) is { } v ? (JsonNode)Math.Round(v, 4) : null)]) : null;
    }

    static void ConnectSpec(ICommandContext ctx, Entity e, string end, JsonNode? spec)
    {
        if (spec is not JsonObject o || (string?)o["carrierId"] is not { } cid) return;
        var trunk = ctx.Model.Get(cid) ?? throw new CommandException("There is no such carrier.");
        Connect(ctx, ctx.Model.Get(e.Id)!, end, trunk, Json.Number(o["station"]), Args.TryPoint(o["at"]));
    }

    public static Entity Draw(ICommandContext ctx, string storeyId, JsonObject a)
    {
        var type = TypeFor(ctx, a);
        var pts = Lay(PointsArg(a["points"]));
        var kind = Kind(type.Str("kind"));
        var attrs = new JsonObject { ["points"] = Runs.J(pts), ["elevation"] = Math.Round(Json.Number(a["elevation"]) ?? 2.4, 4), ["kind"] = kind.Key };
        if (LevelsArg(a["levels"], pts.Count) is { } lv) attrs["levels"] = lv;
        foreach (var k in new[] { "width", "height", "diameter" }) if (Json.Number(a[k]) is { } v) attrs[k] = Positive(k, v);
        var n = All(ctx.Model).Count() + 1;
        var e = new Entity(ctx.NewId(), Class, (string?)a["name"] is { Length: > 0 } name ? name : $"{kind.Label} {n}", attrs, kind.Predefined);
        ctx.Add(e);
        Spatial.Contain(ctx, storeyId, e.Id);
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        ConnectSpec(ctx, e, "start", a["start"]);
        ConnectSpec(ctx, e, "end", a["end"]);
        return ctx.Model.Get(e.Id)!;
    }

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { } e && IsCarrier(e) ? e : throw new CommandException("Which cable carrier?");

    public static readonly FieldDescriptor[] Fields =
    [
        new("elevation", "Elevation", "length"),
        new("width", "Width", "length", Min: 0.01, ShowIf: FieldDescriptor.When("kind", "!conduit")),
        new("height", "Height", "length", Min: 0.01, ShowIf: FieldDescriptor.When("kind", "!conduit")),
        new("diameter", "Diameter", "length", Min: 0.005, ShowIf: FieldDescriptor.When("kind", "conduit")),
    ];

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Cable carrier", Fields, "#8d99a6"));
        r.Class(new(TypeClass, "Cable carrier type", [], "#8d99a6", Kind: "type"));
        r.Geometry(Class, (e, m, _) => IsCarrier(e) ? CarrierBodies.Geometry(e, m) : null);
        r.Quantities(Class, (e, g, m) => IsCarrier(e) ? Quantities(e, m) : []);
        r.Cutter(Class, 70, null);
        Generator.SymbolicInPlan.Add(Class);
        r.Service<IScheduleSource>(new CarrierSchedule());
        r.Service<IIfcNetwork>(new CarrierNetwork());
        var placement = new CarrierPlacement();
        r.Service<IElementPlacement>(placement);
        r.Catalog("carrierKinds", new JsonArray([.. Kinds.Select(k => (JsonNode)new JsonObject
        {
            ["key"] = k.Key, ["label"] = k.Label, ["predefined"] = k.Predefined, ["fillLimit"] = k.FillLimit, ["bendRadius"] = k.BendRadius,
        })]));
        r.Catalog("carrierPresets", new JsonArray([.. Presets.Select(p => (JsonNode)p.ToJson())]));

        // A carrier drawn: {storeyId?, points, typeId | preset | kind, elevation?, levels?, width?, height?, diameter?, start?/end?: {carrierId, station? | at}} → {id}.
        r.Command("drawCarrier", "Cable carrier", (ctx, a) =>
        {
            var storey = (string?)a["storeyId"] is { } sid ? Spatial.Require(ctx.Model, sid, IfcClass.BuildingStorey) : Spatial.Storeys(ctx.Model).FirstOrDefault() ?? throw new CommandException("The project has no storey.");
            return new JsonObject { ["id"] = Draw(ctx, storey.Id, a).Id };
        });

        // New points (and levels): laid again at 45° steps; the levels follow the points unless given.
        r.Command("setCarrierPoints", "Move cable carrier", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var pts = Lay(PointsArg(a["points"]));
            var attrs = e.CloneAttrs();
            attrs["points"] = Runs.J(pts);
            var given = LevelsArg(a["levels"], pts.Count);
            var old = e.Attrs["levels"] is JsonArray ol && ol.Count == Points(e).Count ? ol.Select(x => Json.Number(x)).ToList() : null;
            var levels = given ?? Runs.RemapLevels(Points(e), old, pts, Elevation(e));
            if (levels != null && levels.Any(x => x != null)) attrs["levels"] = levels; else attrs.Remove("levels");
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // The levels of its points (null: back to the elevation); a change of level is a vertical leg at the point.
        r.Command("setCarrierLevels", "Carrier levels", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            if (LevelsArg(a["levels"], Points(e).Count) is { } lv) attrs["levels"] = lv; else attrs.Remove("levels");
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        r.Command("connectCarrier", "Connect carrier", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var to = a["to"] as JsonObject ?? a;
            var trunk = Require(ctx.Model, (string?)to["carrierId"]);
            Connect(ctx, e, (string?)a["end"] ?? "end", trunk, Json.Number(to["station"]), Args.TryPoint(to["at"]));
            return null;
        });

        r.Command("disconnectCarrier", "Disconnect carrier", (ctx, a) =>
        {
            Disconnect(ctx, Require(ctx.Model, (string?)a["id"]), (string?)a["end"] ?? "end");
            return null;
        });

        // Types: {preset} or {kind, name?, width, height, diameter, thickness, bendRadius, fillLimit} → {id}.
        r.Command("createCarrierType", "New cable carrier type", (ctx, a) =>
        {
            if ((string?)a["preset"] is { } pk)
            {
                var p = Preset(pk) ?? throw new CommandException($"There is no built-in carrier type „{pk}”.");
                return new JsonObject { ["id"] = AddPreset(ctx, p).Id };
            }
            return new JsonObject { ["id"] = CreateType(ctx, (string?)a["kind"], (string?)a["name"], a).Id };
        });

        // A type changed ({id, name?, kind?, width?…}): its carriers follow, being built from it; it is the user's own from then on.
        r.Command("updateCarrierType", "Change cable carrier type", (ctx, a) =>
        {
            var t = RequireType(ctx.Model, (string?)a["id"]);
            var attrs = t.CloneAttrs();
            var kind = Kind(t.Str("kind"));
            if ((string?)a["kind"] is { } k) { kind = Kinds.FirstOrDefault(x => x.Key == k) ?? throw new CommandException($"A cable carrier is one of {string.Join(", ", Kinds.Select(x => x.Key))}."); attrs["kind"] = k; }
            foreach (var key in SpecKeys) if (Json.Number(a[key]) is { } v) attrs[key] = Positive(key, v);
            attrs.Remove("indicative"); attrs.Remove("preset");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : t.Name;
            ctx.Replace(t.With(name: name, attrs: attrs, predefinedType: kind.Predefined));
            foreach (var e in ctx.Model.RelatedOf(t.Id, IfcClass.RelDefinesByType).Where(IsCarrier).ToList())
            {
                var ea = e.CloneAttrs();
                ea["kind"] = kind.Key;
                ctx.Replace(e.With(attrs: ea, predefinedType: kind.Predefined));
            }
            return null;
        });

        r.Command("setCarrierType", "Change type", (ctx, a) =>
        {
            var t = RequireType(ctx.Model, (string?)a["typeId"]);
            var kind = Kind(t.Str("kind"));
            foreach (var id in Args.Ids(a))
            {
                var e = Require(ctx.Model, id);
                ctx.Relate(IfcClass.RelDefinesByType, t.Id, e.Id);
                // The type's size from now on: its own width, height and diameter go.
                var attrs = e.CloneAttrs();
                foreach (var k in new[] { "width", "height", "diameter" }) attrs.Remove(k);
                attrs["kind"] = kind.Key;
                ctx.Replace(e.With(attrs: attrs, predefinedType: kind.Predefined));
            }
            return null;
        });

        // Cable runs laid in a carrier ({ids, carrierId}; null takes them out).
        r.Command("setCableCarrier", "Lay in carrier", (ctx, a) =>
        {
            var carrier = (string?)a["carrierId"] is { } cid ? Require(ctx.Model, cid) : null;
            foreach (var id in Args.Ids(a))
            {
                var run = ctx.Model.Get(id) ?? throw new CommandException("There is no such run.");
                if (!Runs.IsRun(run) || run.Class != "IfcCableSegment") throw new CommandException($"„{run.Name}” is not a cable run: only cables lie in a carrier.");
                var attrs = run.CloneAttrs();
                if (carrier != null) attrs["carrier"] = carrier.Id; else attrs.Remove("carrier");
                ctx.Replace(run.With(attrs: attrs));
            }
            return null;
        });

        // A carrier's fill: {id} → {percent, limit, over, cables (m²), usable (m²), count, cableIds}.
        r.Query("carrierFill", (q, a) =>
        {
            var e = Require(q.Model, (string?)a["id"]);
            var f = FillOf(q.Model, e);
            return new JsonObject
            {
                ["percent"] = f.Percent, ["limit"] = f.Limit, ["over"] = f.Over, ["cables"] = f.Cables, ["usable"] = f.Usable, ["count"] = f.Count,
                ["cableIds"] = new JsonArray([.. Cables(q.Model, e.Id).Select(x => (JsonNode)x.Id)]),
                ["size"] = SpecOf(q.Model, e).SizeLabel, ["kind"] = SpecOf(q.Model, e).Kind.Key,
            };
        });
    }

    public static IEnumerable<QuantitySet> Quantities(Entity e, IModelView m)
    {
        var s = SpecOf(m, e);
        var line = Centreline(m, e);
        var f = CarrierBodies.FittingsOf(m, e);
        var q = new List<Quantity> { new("Length", Length(line), "LENGTH") };
        if (s.Kind.Round) q.Add(new("Diameter", s.Diameter, "LENGTH"));
        else { q.Add(new("Width", s.Width, "LENGTH")); q.Add(new("Height", s.Height, "LENGTH")); }
        q.Add(new("Bends", f.Bends, "COUNT")); q.Add(new("Tees", f.Tees, "COUNT"));
        q.Add(new("Crosses", f.Crosses, "COUNT")); q.Add(new("Reducers", f.Reducers, "COUNT"));
        yield return new QuantitySet("Lapin_CableCarrier", q);
    }
}

/// <summary>Carriers moved, turned, mirrored and copied: their points carried; what their ends are on stays.</summary>
sealed class CarrierPlacement : IElementPlacement
{
    public bool Handles(Entity e) => CableCarriers.IsCarrier(e);

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        var attrs = e.CloneAttrs();
        attrs["points"] = Runs.J(CableCarriers.Points(e).Select(t.Point).ToList());
        ctx.Replace(e.With(attrs: attrs));
    }
}

/// <summary>The carriers' ports in the IFC export (CABLECARRIER, at both ends and at each tee) and their connections.</summary>
sealed class CarrierNetwork : IIfcNetwork
{
    public IEnumerable<IfcPort> PortsOf(IModelView m, Entity e)
    {
        if (!CableCarriers.IsCarrier(e)) yield break;
        var line = CableCarriers.Centreline(m, e);
        if (line.Count < 2) yield break;
        yield return new IfcPort("start", line[0], (line[0] - line[1]).Normalized(), "SOURCEANDSINK", "CABLECARRIER", "ELECTRICAL");
        yield return new IfcPort("end", line[^1], (line[^1] - line[^2]).Normalized(), "SOURCEANDSINK", "CABLECARRIER", "ELECTRICAL");
        var z0 = Nodes.StoreyZ(m, e);
        foreach (var (branch, end, station) in CableCarriers.BranchesOf(m, e.Id))
        {
            if (!CableCarriers.Inside(e, station)) continue;
            var (at, level, dir) = CableCarriers.OnTrunk(e, station);
            var bp = CableCarriers.Points(branch);
            var toward = bp.Count >= 2 ? (end == "start" ? bp[1] - bp[0] : bp[^2] - bp[^1]) : new Vec2(-dir.Y, dir.X);
            yield return new IfcPort("tee:" + branch.Id, new Vec3(at.X, at.Y, z0 + level), new Vec3(toward.X, toward.Y, 0).Normalized(), "SOURCEANDSINK", "CABLECARRIER", "ELECTRICAL");
        }
    }

    public IEnumerable<IfcPortConnection> Connections(IModelView m)
    {
        foreach (var c in CableCarriers.All(m))
            foreach (var end in new[] { "start", "end" })
                if (CableCarriers.EndOf(m, c, end) is { } x && m.Get(x.CarrierId) is { } trunk)
                {
                    var length = Pipes.Length(CableCarriers.Points(trunk));
                    var port = CableCarriers.Inside(trunk, x.Station) ? "tee:" + c.Id : x.Station < length / 2 ? "start" : "end";
                    yield return new IfcPortConnection(c.Id, end, trunk.Id, port);
                }
    }

    public IEnumerable<IfcSystemGroup> Systems(IModelView m) => [];
}

/// <summary>The carriers schedule: each carrier's type, kind, size, length and fittings, and its fill; totals by type at the end.</summary>
public sealed class CarrierSchedule : IScheduleSource
{
    public string Kind => "mep-carriers";
    public string Label => "Cable carriers schedule";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = new List<IReadOnlyList<string>>();
        var byType = new Dictionary<string, (double Length, int Bends, int Tees, int Crosses, int Reducers)>();
        foreach (var c in CableCarriers.All(m).OrderBy(c => CableCarriers.TypeOf(m, c)?.Name).ThenBy(c => c.Name))
        {
            var s = CableCarriers.SpecOf(m, c);
            var type = CableCarriers.TypeOf(m, c)?.Name ?? "";
            var length = CableCarriers.Length(CableCarriers.Centreline(m, c));
            var f = CarrierBodies.FittingsOf(m, c);
            var fill = CableCarriers.FillOf(m, c);
            rows.Add([c.Name, type, s.Kind.Label, s.SizeLabel, Spatial.StoreyOf(m, c.Id)?.Name ?? "", N(length), f.Bends.ToString(CultureInfo.InvariantCulture),
                f.Tees.ToString(CultureInfo.InvariantCulture), f.Crosses.ToString(CultureInfo.InvariantCulture), f.Reducers.ToString(CultureInfo.InvariantCulture),
                fill.Count > 0 ? N(fill.Percent, "0.#") + (fill.Over ? " !" : "") : ""]);
            var t = byType.GetValueOrDefault(type + " " + s.SizeLabel);
            byType[type + " " + s.SizeLabel] = (t.Length + length, t.Bends + f.Bends, t.Tees + f.Tees, t.Crosses + f.Crosses, t.Reducers + f.Reducers);
        }
        foreach (var (k, t) in byType.OrderBy(x => x.Key))
            rows.Add(["Total", k.Trim(), "", "", "", N(t.Length), t.Bends.ToString(CultureInfo.InvariantCulture), t.Tees.ToString(CultureInfo.InvariantCulture),
                t.Crosses.ToString(CultureInfo.InvariantCulture), t.Reducers.ToString(CultureInfo.InvariantCulture), ""]);
        return new ScheduleGrid(Label, ["Name", "Type", "Kind", "Size (mm)", "Storey", "Length (m)", "Bends", "Tees", "Crosses", "Reducers", "Fill (%)"], rows);
    }
}
