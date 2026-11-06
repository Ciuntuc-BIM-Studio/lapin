using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Transport;

/// <summary>
/// The transport module (docs/plan-elevators.md, testers' reports #54 and #97): lifts as <c>IfcTransportElement</c>
/// ELEVATOR of an <c>IfcTransportElementType</c>. A lift stands on its lowest stop's storey (<c>at</c>: the middle of its
/// shaft, <c>rotation</c>: its doors on its local −y) and runs up to <c>topStoreyId</c>, stopping on every storey between
/// but those in <c>unserved</c>; everything else (drive, load, car, doors, pit, headroom, machine room, shaft walls) is
/// its own attributes, set by a style (the catalogue's twelve kinds, or the project's own) and changed by hand. Its
/// shaft opens the floors it passes through (IfcOpeningElements of theirs, kept in step with it), its plans show it on
/// every storey it reaches, the Lifts schedule lists it, IFC writes it with Pset_TransportElementCommon.
/// </summary>
public sealed class TransportModule : ILapinModule
{
    public string Id => "transport";
    public string Name => "Transport";

    public const string Class = "IfcTransportElement";
    public const string TypeClass = "IfcTransportElementType";
    public const string Predefined = "ELEVATOR";
    public const string ShaftMaterial = "Reinforced concrete", GlassMaterial = "Glass", SteelMaterial = "Stainless steel", WeightMaterial = "Steel";
    /// <summary>The project's own lift styles, on the project.</summary>
    public const string StylesKey = "elevatorStyles";
    public const string OwnPrefix = "own:";
    /// <summary>The attribute an opening a lift made in a floor names it by.</summary>
    public const string OpeningKey = "elevatorId";

    static readonly Dictionary<string, string> MaterialColors = new()
    {
        [ShaftMaterial] = "#b8b4ad", [GlassMaterial] = "#bcd9e6", [SteelMaterial] = "#aab0b6", [WeightMaterial] = "#5d646c",
    };

    static IReadOnlyList<IReadOnlyDictionary<string, string[]>> When(string key, params string[] values) => FieldDescriptor.When(key, values);

    public static readonly IReadOnlyList<FieldDescriptor> Fields =
    [
        new("drive", "Drive", "choice", Choices: ElevatorSpec.Drives),
        new("load", "Rated load (kg)", "number", Min: 50, Max: 10000),
        new("persons", "Persons", "number", Min: 1, Max: 200),
        new("speed", "Speed (m/s)", "number", Min: 0.05, Max: 10),
        new("cabinWidth", "Car width", "length", Min: 0.6, Max: 4),
        new("cabinDepth", "Car depth", "length", Min: 0.6, Max: 6),
        new("cabinHeight", "Car height", "length", Min: 1.9, Max: 4),
        new("glass", "Glass car walls", "bool"),
        new("entrances", "Entrances", "choice", Choices: ElevatorSpec.Entrances),
        new("doorWidth", "Door width", "length", Min: 0.5, Max: 3),
        new("doorHeight", "Door height", "length", Min: 1.8, Max: 3.5),
        new("doorOpening", "Door opening", "choice", Choices: ElevatorSpec.Openings),
        new("doorPanels", "Door panels", "number", Min: 1, Max: 4),
        new("counterweight", "Counterweight side", "choice", Choices: ElevatorSpec.Sides, ShowIf: When("drive", "!hydraulic")),
        new("jackSide", "Jack side", "choice", Choices: ["left", "right"], ShowIf: When("drive", "hydraulic")),
        new("accessible", "Accessible (EN 81-70)", "choice", Choices: ElevatorSpec.AccessibleTypes),
        new("pit", "Pit depth", "length", Min: 0.05, Max: 4),
        new("headroom", "Headroom over the top stop", "length", Min: 2.2, Max: 8),
        new("machineRoom", "Machine room", "choice", Choices: ElevatorSpec.MachineRooms, ShowIf: When("drive", "traction", "hydraulic")),
        new("shaftGlass", "Glass shaft", "bool"),
        new("shaftThickness", "Shaft wall thickness", "length", Min: 0.05, Max: 0.6, ShowIf: [new Dictionary<string, string[]> { ["shaftWallTypeId"] = [""], ["shaftGlass"] = ["!true"] }]),
        new("fireFighting", "Firefighters lift (EN 81-72)", "bool"),
        new("fireExit", "Evacuation lift (fire exit)", "bool"),
        new("rotation", "Rotation (°)", "angle", Min: -360, Max: 360),
    ];

    /// <summary>What a lift's own section sets, besides its fields: the storeys, the stops it skips, its shaft's wall type, its style.</summary>
    static readonly string[] OwnKeys = ["topStoreyId", "unserved", "shaftWallTypeId", "style", "kind", "at"];

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Elevator", Fields, "#9aa3ad"));
        r.Class(new(TypeClass, "Elevator type", [], "#9aa3ad", Kind: "type"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Quantities(Class, Quantities);
        r.Catalog("styles", new JsonArray([.. ElevatorStyles.All.Select(s => (JsonNode)s.ToJson())]));
        r.Catalog("en81", new JsonObject
        {
            ["maxArea"] = new JsonArray([.. En81.MaxArea.Select(x => (JsonNode)new JsonArray(x.Load, x.Area))]),
            ["minAreaForPersons"] = new JsonArray([.. En81.MinAreaForPersons.Select(x => (JsonNode)x)]),
        });
        var follow = new ElevatorFollowers();
        r.Service<IElementPlacement>(follow);
        r.Service<ITransformFollowers>(follow);
        r.Service<IElementUpdate>(follow);
        r.Service<IDeletion>(follow);
        r.Service<IStoreySpans>(new ElevatorSpans());
        r.Service<IIfcElementSets>(new ElevatorSets());
        r.Service<IScheduleSource>(new ElevatorSchedule());

        // A lift at a point: {storeyId, x, y, rotation?, style? (a catalogue's or the project's), topStoreyId? (the highest storey), attrs?} → {id}.
        r.Command("place", "Elevator", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var p = Args.Point(a, "x", "y");
            var top = (string?)a["topStoreyId"] is { } tid ? Spatial.Require(ctx.Model, tid, IfcClass.BuildingStorey) : Spatial.Storeys(ctx.Model).Last();
            var attrs = new JsonObject { ["at"] = new JsonArray(R(p.X), R(p.Y)), ["rotation"] = Json.Number(a["rotation"]) ?? 0, ["topStoreyId"] = top.Id };
            ApplyStyle(ctx.Model, attrs, (string?)a["style"] ?? "passenger-medium");
            if (a["attrs"] is JsonObject given) Merge(ctx, attrs, given, null);
            Normalize(attrs, null);
            EnsureMaterials(ctx);
            var e = new Entity(ctx.NewId(), Class, "Elevator", attrs, Predefined);
            ctx.Add(e);
            Spatial.Contain(ctx, storey.Id, e.Id);
            Retype(ctx, e.Id);
            Sync(ctx, e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        // The configurator's Apply: {id, style?, attrs?, bottomStoreyId?} — a style's values first, then those set by hand (null: the default again). One step.
        r.Command("update", "Change elevator", (ctx, a) =>
        {
            Configure(ctx, Require(ctx.Model, (string?)a["id"]), a);
            return null;
        });

        // The floors' openings made again for every lift (or those asked): a floor drawn after its lift opens for it.
        r.Command("syncOpenings", "Shaft openings", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            foreach (var e in ctx.Model.OfClass(Class).Where(x => ids.Count == 0 || ids.Contains(x.Id)).ToList()) Sync(ctx, e.Id);
            return null;
        });

        // A project style from a lift as configured: {id, name, styleId? (an own style saved again: its lifts follow), style?, attrs?} → {styleId}.
        r.Command("saveStyle", "Save elevator style", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            Configure(ctx, e, a);
            e = ctx.Model.Get(e.Id)!;
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("Name the style.");
            var styles = OwnStyles(ctx.Model);
            var id = (string?)a["styleId"] is { } sid && styles.Any(s => (string?)s["id"] == sid) ? sid : OwnPrefix + ctx.NewId();
            var snap = new JsonObject { ["id"] = id, ["label"] = name, ["kind"] = e.Str("kind") ?? "passenger-medium", ["attrs"] = Snapshot(e) };
            var list = new JsonArray([.. styles.Where(s => (string?)s["id"] != id).Select(s => (JsonNode)s.DeepClone()), snap]);
            WriteStyles(ctx, list);
            // Its lifts follow the style saved again; this one says its style.
            foreach (var x in ctx.Model.OfClass(Class).Where(x => x.Id == e.Id || x.Str("style") == id).ToList())
            {
                var attrs = x.CloneAttrs();
                if (x.Id != e.Id) ApplyStyle(ctx.Model, attrs, id);
                attrs["style"] = id;
                ctx.Replace(x.With(attrs: attrs));
                Retype(ctx, x.Id);
                Sync(ctx, x.Id);
            }
            return new JsonObject { ["styleId"] = id };
        });

        r.Command("renameStyle", "Rename elevator style", (ctx, a) =>
        {
            var id = (string?)a["styleId"];
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("Name the style.");
            var styles = OwnStyles(ctx.Model);
            if (!styles.Any(s => (string?)s["id"] == id)) throw new CommandException("There is no such elevator style.");
            WriteStyles(ctx, new JsonArray([.. styles.Select(s => { var c = (JsonObject)s.DeepClone(); if ((string?)c["id"] == id) c["label"] = name; return (JsonNode)c; })]));
            foreach (var x in ctx.Model.OfClass(Class).Where(x => x.Str("style") == id).ToList()) Retype(ctx, x.Id);
            return null;
        });

        // A project style deleted: its lifts keep how they are, their kind the catalogue's they came from.
        r.Command("deleteStyle", "Delete elevator style", (ctx, a) =>
        {
            var id = (string?)a["styleId"];
            var styles = OwnStyles(ctx.Model);
            if (!styles.Any(s => (string?)s["id"] == id)) throw new CommandException("There is no such elevator style.");
            WriteStyles(ctx, new JsonArray([.. styles.Where(s => (string?)s["id"] != id).Select(s => (JsonNode)s.DeepClone())]));
            foreach (var x in ctx.Model.OfClass(Class).Where(x => x.Str("style") == id).ToList())
            {
                var attrs = x.CloneAttrs();
                attrs.Remove("style");
                ctx.Replace(x.With(attrs: attrs));
                Retype(ctx, x.Id);
            }
            return null;
        });

        // The configurator's preview of a draft: {id, style?, attrs?} → {attrs, plan, section, summary, checks, suggested, problem?}.
        r.Query("preview", (q, a) =>
        {
            var e = Require(q.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            if ((string?)a["style"] is { } style) ApplyStyle(q.Model, attrs, style);
            if (a["attrs"] is JsonObject given) Merge(null, attrs, given, q.Model);
            Normalize(attrs, e.Attrs);
            var draft = e.With(attrs: attrs);
            var layout = ElevatorLayout.Resolve(q.Model, draft, out var problem);
            if (layout == null) return new JsonObject { ["problem"] = problem };
            return Preview(q.Model, layout, attrs);
        });
    }

    static double R(double v) => Math.Round(v, 5);
    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);
    static string M2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    static Entity Require(IModelView m, string? id) => m.Get(id ?? "") is { Class: Class } e ? e : throw new CommandException("Pick an elevator.");

    public static Entity? TypeOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { Class: TypeClass } t ? t : null;

    static void EnsureMaterials(IEdit edit)
    {
        foreach (var (name, color) in MaterialColors) Materials.Ensure(edit, name, color);
    }

    // ---- Styles ----

    public static List<JsonObject> OwnStyles(IModelView m) => (Spatial.Project(m)?.Attrs[StylesKey] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    static void WriteStyles(ICommandContext ctx, JsonArray list)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The project is missing.");
        var attrs = project.CloneAttrs();
        if (list.Count == 0) attrs.Remove(StylesKey); else attrs[StylesKey] = list;
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>The style's fields of a lift, as a project style keeps them.</summary>
    static JsonObject Snapshot(Entity e)
    {
        var o = new JsonObject();
        foreach (var k in ElevatorStyles.Fields.Append("jackSide").Append("shaftWallTypeId")) if (e.Attrs[k] is { } v) o[k] = v.DeepClone();
        return o;
    }

    /// <summary>A style put on a lift's attributes: its fields replace the lift's; the lift says its style and kind.</summary>
    public static void ApplyStyle(IModelView m, JsonObject attrs, string id)
    {
        JsonObject values;
        string kind;
        if (id.StartsWith(OwnPrefix, StringComparison.Ordinal))
        {
            var own = OwnStyles(m).FirstOrDefault(s => (string?)s["id"] == id) ?? throw new CommandException("There is no such elevator style.");
            values = own["attrs"] as JsonObject ?? [];
            kind = (string?)own["kind"] ?? "passenger-medium";
        }
        else
        {
            var s = ElevatorStyles.Find(id) ?? throw new CommandException($"An elevator style is one of {string.Join(", ", ElevatorStyles.All.Select(x => x.Id))}, or the project's own.");
            values = s.Values;
            kind = s.Id;
        }
        foreach (var k in ElevatorStyles.Fields.Append("jackSide").Append("shaftWallTypeId")) attrs.Remove(k);
        foreach (var (k, v) in values) attrs[k] = v?.DeepClone();
        attrs["style"] = id;
        attrs["kind"] = kind;
    }

    /// <summary>Values set by hand merged in, each checked as its field says (null: back to the default).</summary>
    static void Merge(ICommandContext? ctx, JsonObject attrs, JsonObject given, IModelView? model)
    {
        var m = ctx?.Model ?? model!;
        foreach (var (k, v) in given)
        {
            if (k == "topStoreyId")
            {
                if (v is null) attrs.Remove(k); else attrs[k] = Spatial.Require(m, (string?)v, IfcClass.BuildingStorey).Id;
                continue;
            }
            if (k == "unserved")
            {
                if (v is not JsonArray arr || arr.Count == 0) attrs.Remove(k);
                else attrs[k] = new JsonArray([.. arr.Select(x => (string?)x).OfType<string>().Distinct().Select(x => (JsonNode)x)]);
                continue;
            }
            if (k == "shaftWallTypeId")
            {
                if (v is null || (string?)v is not { Length: > 0 } wt) attrs.Remove(k);
                else attrs[k] = m.Get(wt) is { Class: IfcClass.WallType } ? wt : throw new CommandException("The shaft's walls take a wall type.");
                continue;
            }
            if (k == "at") { attrs[k] = new JsonArray(R(Args.Point(v, "at").X), R(Args.Point(v, "at").Y)); continue; }
            var f = Fields.FirstOrDefault(x => x.Key == k) ?? throw new CommandException($"An elevator has no field „{k}”.");
            if (v is null) attrs.Remove(k); else attrs[k] = Lapin.Platform.Fields.Check(f, v);
        }
    }

    /// <summary>
    /// What follows a change: another drive takes its own machine room, pit and headroom (those left as the old drive's
    /// defaults); a speed changed moves a pit and headroom left at their suggested values; a hydraulic lift's jack is at a side.
    /// </summary>
    public static void Normalize(JsonObject attrs, JsonObject? before)
    {
        var now = new ElevatorSpec(attrs);
        var old = before != null ? new ElevatorSpec(before) : null;
        if (old != null && old.Drive != now.Drive)
            attrs["machineRoom"] = now.Drive switch { "traction" => "top", "hydraulic" => "beside", _ => "none" };
        if (old != null && (old.Drive != now.Drive || Math.Abs(old.Speed - now.Speed) > 1e-9))
        {
            if (Math.Abs(old.Pit - ElevatorSpec.SuggestedPit(old.Drive, old.Speed)) < 1e-6) attrs["pit"] = ElevatorSpec.SuggestedPit(now.Drive, now.Speed);
            if (Math.Abs(old.Headroom - ElevatorSpec.SuggestedHeadroom(old.Drive, old.Speed)) < 1e-6) attrs["headroom"] = ElevatorSpec.SuggestedHeadroom(now.Drive, now.Speed);
        }
        if (now.Drive is "mrl" or "home") attrs["machineRoom"] = "none";
        if (now.Drive != "hydraulic" && (string?)attrs["machineRoom"] == "beside") attrs["machineRoom"] = "top";
        // A hydraulic lift's jack stands where its counterweight would: the jack side is that, at a side.
        if (now.Drive == "hydraulic")
        {
            var side = attrs["jackSide"] is JsonValue jv && jv.TryGetValue<string>(out var js) && js is "left" or "right" ? js : (string?)attrs["counterweight"] is "right" ? "right" : "left";
            attrs["jackSide"] = side;
            attrs["counterweight"] = side;
        }
        // Every field written out, so the properties show (and their conditions read) what the lift is.
        var s = new ElevatorSpec(attrs);
        attrs["drive"] = s.Drive; attrs["load"] = s.Load; attrs["speed"] = s.Speed; attrs["persons"] = s.Persons;
        attrs["cabinWidth"] = s.CabinWidth; attrs["cabinDepth"] = s.CabinDepth; attrs["cabinHeight"] = s.CabinHeight;
        attrs["entrances"] = s.Entrance; attrs["doorWidth"] = s.DoorWidth; attrs["doorHeight"] = s.DoorHeight;
        attrs["doorOpening"] = s.DoorOpening; attrs["doorPanels"] = s.DoorPanels; attrs["counterweight"] = s.Counterweight;
        attrs["accessible"] = s.Accessible; attrs["pit"] = s.Pit; attrs["headroom"] = s.Headroom; attrs["machineRoom"] = s.MachineRoom;
        attrs["shaftThickness"] = s.ShaftThickness;
        foreach (var b in new[] { "glass", "shaftGlass", "fireFighting", "fireExit" }) attrs[b] = attrs[b] is JsonValue bv && bv.TryGetValue<bool>(out var bb) && bb;
    }

    /// <summary>A lift configured: a style, values by hand, its storeys; its type, its floors' openings after it.</summary>
    static void Configure(ICommandContext ctx, Entity e, JsonObject a)
    {
        var attrs = e.CloneAttrs();
        if ((string?)a["style"] is { } style) ApplyStyle(ctx.Model, attrs, style);
        if (a["attrs"] is JsonObject given) Merge(ctx, attrs, given, null);
        Normalize(attrs, e.Attrs);
        var draft = e.With(attrs: attrs);
        if (ElevatorLayout.Resolve(ctx.Model, draft, out var problem) == null) throw new CommandException(problem ?? "The elevator cannot be built so.");
        EnsureMaterials(ctx);
        ctx.Replace(draft);
        if ((string?)a["bottomStoreyId"] is { } bottom && bottom != Spatial.StoreyOf(ctx.Model, e.Id)?.Id)
            Spatial.Contain(ctx, Spatial.Require(ctx.Model, bottom, IfcClass.BuildingStorey).Id, e.Id);
        Retype(ctx, e.Id);
        Sync(ctx, e.Id);
    }

    // ---- Types ----

    /// <summary>The label of a lift's kind: its project style's name, else its catalogue kind's.</summary>
    public static string KindLabel(IModelView m, Entity e)
    {
        if (e.Str("style") is { } sid && sid.StartsWith(OwnPrefix, StringComparison.Ordinal) && OwnStyles(m).FirstOrDefault(s => (string?)s["id"] == sid) is { } own)
            return (string?)own["label"] ?? "Elevator";
        return ElevatorStyles.Find(e.Str("kind"))?.Label ?? "Elevator";
    }

    /// <summary>
    /// The lift's type: one per kind, rated load and car, made when the project has none (Medium passenger 630 kg, 1100 × 1400),
    /// so lifts alike share it and the cost module's articles on it price them all. A type no lift uses any more and that
    /// carries nothing of the user's goes.
    /// </summary>
    public static void Retype(ICommandContext ctx, string id)
    {
        var e = ctx.Model.Get(id)!;
        var s = new ElevatorSpec(e.Attrs);
        var label = KindLabel(ctx.Model, e);
        var key = $"{label}|{s.Drive}|{s.Load}|{Mm(s.CabinWidth)}x{Mm(s.CabinDepth)}";
        var name = $"{label} {s.Load.ToString(CultureInfo.InvariantCulture)} kg, {Mm(s.CabinWidth)} × {Mm(s.CabinDepth)}";
        var old = TypeOf(ctx.Model, e);
        if (old?.Str("key") == key) return;
        var type = ctx.Model.OfClass(TypeClass).FirstOrDefault(t => t.Str("key") == key);
        if (type == null)
        {
            type = new Entity(ctx.NewId(), TypeClass, name, new JsonObject { ["key"] = key, ["kind"] = e.Str("kind") ?? "" }, Predefined);
            ctx.Add(type);
        }
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        if (old == null || e.Name == old.Name || e.Name == "Elevator") ctx.Replace(ctx.Model.Get(id)!.With(name: type.Name));
        if (old != null && old.Str("key") != null && old.Attrs["articles"] == null && !ctx.Model.RelatedOf(old.Id, IfcClass.RelDefinesByType).Any()) ctx.Remove(old.Id);
    }

    // ---- The floors' openings ----

    public static IEnumerable<Entity> OpeningsOf(IModelView m, string id) => m.OfClass(IfcClass.OpeningElement).Where(o => o.Str(OpeningKey) == id);

    /// <summary>The floors a lift's shaft passes through: those whose top lies between its pit and its cap, under its footprint.</summary>
    public static List<Entity> FloorsCut(IModelView m, ElevatorLayout l)
    {
        var outer = l.Outer;
        var o = new List<Entity>();
        foreach (var slab in m.OfClass(IfcClass.Slab))
        {
            var top = Slabs.Top(m, slab);
            if (top <= l.ZPit + 0.01 || top >= l.ZHead - 0.01) continue;
            var contour = Slabs.Contour(slab);
            if (contour.Count >= 3 && Overlaps(outer, contour)) o.Add(slab);
        }
        return o;
    }

    static bool Overlaps(IReadOnlyList<Vec2> a, IReadOnlyList<Vec2> b)
    {
        Vec2[] ra = [.. a], rb = [.. b];
        if (a.Any(p => Polygon2.Inside([rb], p)) || b.Any(p => Polygon2.Inside([ra], p))) return true;
        for (var i = 0; i < a.Count; i++)
            for (var j = 0; j < b.Count; j++)
                if (Cross(a[i], a[(i + 1) % a.Count], b[j], b[(j + 1) % b.Count])) return true;
        return false;
    }

    static bool Cross(Vec2 p1, Vec2 p2, Vec2 q1, Vec2 q2)
    {
        double O(Vec2 a, Vec2 b, Vec2 c) => Vec2.Cross(b - a, c - a);
        var (d1, d2, d3, d4) = (O(q1, q2, p1), O(q1, q2, p2), O(p1, p2, q1), O(p1, p2, q2));
        return d1 * d2 < 0 && d3 * d4 < 0;
    }

    /// <summary>The openings a lift's shaft asks of the floors it passes through, made, moved or removed as it is now.</summary>
    public static void Sync(ICommandContext ctx, string id)
    {
        var m = ctx.Model;
        var existing = OpeningsOf(m, id).ToList();
        var e = m.Get(id);
        var layout = e != null ? ElevatorLayout.Resolve(m, e, out _) : null;
        var floors = layout != null ? FloorsCut(m, layout) : [];
        var contour = layout != null ? Slabs.ContourJson(layout.Outer.Select(p => new Vec2(R(p.X), R(p.Y)))) : null;
        foreach (var o in existing)
        {
            var host = m.RelatingOf(o.Id, IfcClass.RelVoidsElement);
            if (host == null || !floors.Any(f => f.Id == host.Id)) { ctx.Remove(o.Id); continue; }
            floors.RemoveAll(f => f.Id == host.Id);
            if (contour != null && o.Attrs["contour"]?.ToJsonString() != contour.ToJsonString())
            {
                var attrs = o.CloneAttrs();
                attrs["contour"] = contour.DeepClone();
                ctx.Replace(o.With(attrs: attrs));
            }
        }
        foreach (var slab in floors)
        {
            var opening = Slabs.AddOpening(ctx, slab, layout!.Outer);
            var attrs = opening.CloneAttrs();
            attrs[OpeningKey] = id;
            ctx.Replace(opening.With(name: "Elevator shaft", attrs: attrs));
        }
    }

    // ---- The body ----

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var l = ElevatorLayout.Resolve(m, e, out var problem);
        if (l == null) return new ElementGeometry([], null, null, [problem ?? "The elevator cannot be built."]);
        var parts = new List<GeometryPart>();
        foreach (var (key, name, solids, role) in l.Bodies())
        {
            var (matName, fallback) = role switch
            {
                "shaft" => (null, MaterialColors[ShaftMaterial]),
                "glass" => (GlassMaterial, MaterialColors[GlassMaterial]),
                "metal" => (WeightMaterial, MaterialColors[WeightMaterial]),
                _ => (SteelMaterial, MaterialColors[SteelMaterial]),
            };
            var matId = role == "shaft" ? key == "machineRoom" ? Materials.Find(m, ShaftMaterial)?.Id ?? l.WallMaterial : l.WallMaterial : Materials.Find(m, matName!)?.Id;
            var color = matId != null && m.Get(matId) is { } mat ? mat.Str("color") ?? fallback : fallback;
            parts.Add(new GeometryPart(name, color, Combine(solids), matId, key) { CutInPlan = role == "shaft" });
        }
        var plan = l.PlanOn(Spatial.StoreyOf(m, e.Id)?.Id ?? l.Stops[0].Storey.Id);
        List<Vec2> snaps = [l.At, .. l.Outer, .. l.Inner];
        return new ElementGeometry(parts, plan, snaps);
    }

    static Solid Combine(IReadOnlyList<Solid> solids)
    {
        if (solids.Count == 1) return solids[0];
        var vertices = new List<Vec3>();
        var faces = new List<Face>();
        foreach (var s in solids)
        {
            var at = vertices.Count;
            vertices.AddRange(s.Vertices);
            faces.AddRange(s.Faces.Select(f => f with { Outer = f.Outer.Select(i => i + at).ToArray(), Holes = f.Holes.Select(h => h.Select(i => i + at).ToArray()).ToArray() }));
        }
        return new Solid([.. vertices], [.. faces], solids[0].Tag);
    }

    /// <summary>What a lift measures (Lapin_LiftQuantities): one lift, its stops, the stops it serves, its travel, its shaft's height and footprint.</summary>
    static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (ElevatorLayout.Resolve(m, e, out _) is not { } l) yield break;
        yield return new QuantitySet("Lapin_LiftQuantities",
        [
            new Quantity("Count", 1, "COUNT", "Elevators"),
            new Quantity("Stops", l.Stops.Count, "COUNT", "Stops"),
            new Quantity("ServedStops", l.Stops.Count(s => s.Served), "COUNT", "Stops served"),
            new Quantity("TravelHeight", R(l.Travel), "LENGTH", "Travel"),
            new Quantity("ShaftHeight", R(l.ZHead + l.Tc - l.ZBottom), "LENGTH", "Shaft height"),
            new Quantity("ShaftArea", R(l.OuterW * l.OuterD), "AREA", "Shaft footprint"),
        ]);
    }

    // ---- The configurator's preview ----

    static JsonArray Pts(IEnumerable<Vec2> pts) => new([.. pts.SelectMany(p => new[] { (JsonNode)R(p.X), R(p.Y) })]);

    static JsonObject Preview(IModelView m, ElevatorLayout lay, JsonObject attrs)
    {
        // Drawn in the lift's own frame: its doors down the page.
        var l = new ElevatorLayout
        {
            Spec = lay.Spec, At = new Vec2(0, 0), Rotation = 0, Wi = lay.Wi, Di = lay.Di, T = lay.T, Tp = lay.Tp, Tc = lay.Tc, Cx = lay.Cx, Cy0 = lay.Cy0, Cy1 = lay.Cy1,
            Dx0 = lay.Dx0, Dx1 = lay.Dx1, Stops = lay.Stops, WallMaterial = lay.WallMaterial,
        };
        var s = l.Spec;
        var plan = new JsonArray();
        // The shaft's walls in plan, with the landing door's opening.
        var (x0, x1, y0, y1, t) = (-l.Wi / 2, l.Wi / 2, -l.Di / 2, l.Di / 2, l.T);
        void Wall(double a0, double b0, double a1, double b1) { if (a1 - a0 > 1e-6 && b1 - b0 > 1e-6) plan.Add((JsonNode)new JsonObject { ["kind"] = "wall", ["closed"] = true, ["points"] = Pts(l.Rect(a0, b0, a1, b1)) }); }
        Wall(x0 - t, y0 - t, l.Dx0, y0); Wall(l.Dx1, y0 - t, x1 + t, y0);
        if (s.Through) { Wall(x0 - t, y1, l.Dx0, y1 + t); Wall(l.Dx1, y1, x1 + t, y1 + t); } else Wall(x0 - t, y1, x1 + t, y1 + t);
        Wall(x0 - t, y0, x0, y1); Wall(x1, y0, x1 + t, y1);
        if (l.BesideRoom is { } br) plan.Add((JsonNode)new JsonObject { ["kind"] = "room", ["closed"] = true, ["points"] = Pts(l.Rect(br.X0, br.Y0, br.X1, br.Y1)) });
        foreach (var i in l.PlanOn(l.Stops[0].Storey.Id))
            plan.Add((JsonNode)new JsonObject { ["kind"] = i.Kind, ["closed"] = i.Closed, ["points"] = Pts(i.Points) });

        // A section through the car, front to back: the shaft from its pit floor to its cap, the stops, the car at the bottom.
        var section = new JsonArray();
        void Box(string role, double a0, double a1, double z0, double z1) { if (a1 - a0 > 1e-6 && z1 - z0 > 1e-6) section.Add((JsonNode)new JsonObject { ["role"] = role, ["y0"] = R(a0), ["y1"] = R(a1), ["z0"] = R(z0 - l.ZB), ["z1"] = R(z1 - l.ZB) }); }
        Box("shaft", y0 - t, y1 + t, l.ZPit - l.Tp, l.ZPit);
        Box("shaft", y0 - t, y1 + t, l.ZHead, l.ZHead + l.Tc);
        void WallZ(double a0, double a1, bool holes)
        {
            var z = l.ZPit;
            foreach (var st in l.Stops.Where(x => x.Served && holes))
            {
                Box("shaft", a0, a1, z, st.Z);
                Box("door", a0 + (a1 - a0) * 0.6, a1, st.Z, st.Z + s.DoorHeight);
                z = st.Z + s.DoorHeight;
            }
            Box("shaft", a0, a1, z, l.ZHead);
        }
        WallZ(y0 - t, y0, true);
        WallZ(y1, y1 + t, s.Through);
        if (s.MachineRoom == "top")
        {
            var b0 = l.ZHead + l.Tc;
            Box("machine", y0 - t, y0 - t + 0.2, b0, b0 + ElevatorLayout.MachineRoomHeight);
            Box("machine", y1 + t - 0.2, y1 + t, b0, b0 + ElevatorLayout.MachineRoomHeight);
            Box("machine", y0 - t, y1 + t, b0 + ElevatorLayout.MachineRoomHeight, b0 + ElevatorLayout.MachineRoomHeight + 0.2);
        }
        Box("cabin", l.Cy0 - 0.04, l.Cy1 + 0.04, l.ZB - 0.1, l.ZB + s.CabinHeight + 0.1);
        if (s.Counterweight == "back" && s.Drive != "hydraulic")
        {
            var (_, wy0, _, wy1) = l.Counterweight;
            var h = Math.Clamp(l.Travel * 0.3, 1.0, 2.5);
            Box("weight", wy0, wy1, l.ZHead - 0.4 - h, l.ZHead - 0.4);
        }
        var levels = new JsonArray([.. l.Stops.Select(x => (JsonNode)new JsonObject { ["name"] = x.Storey.Name, ["z"] = R(x.Z - l.ZB), ["served"] = x.Served, ["id"] = x.Storey.Id })]);

        var area = s.CabinArea;
        var max = En81.AreaForLoad(s.Load);
        var summary = new JsonArray
        {
            Row("Shaft, clear", $"{Mm(l.Wi)} × {Mm(l.Di)} mm"),
            Row("Shaft, outside", $"{Mm(l.OuterW)} × {Mm(l.OuterD)} mm"),
            Row("Car area", $"{M2(area)} m²"),
            Row("Allowed by EN 81-20 for the load", $"{M2(max)} m²"),
            Row("Rated load for this car", $"{En81.LoadForArea(area).ToString(CultureInfo.InvariantCulture)} kg"),
            Row("Persons it carries", En81.Persons(Math.Max(s.Load, En81.LoadForArea(area)), area).ToString(CultureInfo.InvariantCulture)),
            Row("Stops served", $"{l.Stops.Count(x => x.Served)} / {l.Stops.Count}"),
            Row("Travel", $"{M2(l.Travel)} m"),
            Row("Pit", $"{Mm(s.Pit)} mm"),
            Row("Headroom", $"{Mm(s.Headroom)} mm"),
            Row("Top of the shaft over the top stop", $"+{M2(l.ZTop - l.ZT)} m"),
            Row("Floors opened", FloorsCut(m, lay).Count.ToString(CultureInfo.InvariantCulture)),
        };
        var suggested = En81.LoadForArea(area);
        return new JsonObject
        {
            ["attrs"] = attrs.DeepClone(),
            ["plan"] = plan,
            ["section"] = new JsonObject { ["items"] = section, ["levels"] = levels, ["top"] = R(l.ZTop - l.ZB), ["bottom"] = R(l.ZBottom - l.ZB), ["width"] = R(l.OuterD) },
            ["summary"] = summary,
            ["checks"] = new JsonArray([.. s.Checks(l.Travel).Select(x => (JsonNode)x)]),
            ["suggested"] = new JsonObject { ["load"] = Math.Max(suggested, s.Load), ["persons"] = En81.Persons(Math.Max(suggested, s.Load), area) },
        };
    }

    static JsonNode Row(string k, string v) => new JsonArray(k, v);
}

/// <summary>
/// A lift moved, turned, mirrored or copied (its other hand under a mirror: the counterweight on the other side, the
/// doors still on its front); its floors' openings follow once everything has moved; deleted, it takes them with it;
/// changed in Properties, its type and openings follow.
/// </summary>
sealed class ElevatorFollowers : IElementPlacement, ITransformFollowers, IElementUpdate, IDeletion
{
    public bool Handles(Entity e) => e.Class == TransportModule.Class;

    static JsonObject Mirrored(JsonObject attrs)
    {
        attrs["rotation"] = Math.Round((((Json.Number(attrs["rotation"]) ?? 0) + 180) % 360 + 360) % 360, 6);
        foreach (var k in new[] { "counterweight", "jackSide" })
            if ((string?)attrs[k] is "left" or "right") attrs[k] = (string?)attrs[k] == "left" ? "right" : "left";
        return attrs;
    }

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        var attrs = Placement.Carry(e, t);
        ctx.Replace(e.With(attrs: t.Mirrored ? Mirrored(attrs) : attrs));
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        var id = Placement.DeepCopy(ctx, e, t);
        if (t.Mirrored && ctx.Model.Get(id) is { } copy) ctx.Replace(copy.With(attrs: Mirrored(copy.CloneAttrs())));
        return id;
    }

    public object? Before(ICommandContext ctx, IReadOnlySet<string> moving, IReadOnlySet<string> selected) => null;

    public void After(ICommandContext ctx, object? state, Placement2D t, Placement2D plan, IReadOnlySet<string> moving, IReadOnlySet<string> selected, IReadOnlyDictionary<string, string> copies)
    {
        // A lift or a floor moved: every lift's openings as they now stand (a few lifts; nothing written when nothing changed).
        var m = ctx.Model;
        bool Touched(string id) => m.Get(id) is { Class: TransportModule.Class or IfcClass.Slab };
        if (!moving.Any(Touched) && !copies.Values.Any(Touched)) return;
        foreach (var e in m.OfClass(TransportModule.Class).ToList()) TransportModule.Sync(ctx, e.Id);
    }

    public void Updated(ICommandContext ctx, Entity before, Entity after)
    {
        if (after.Class == IfcClass.Slab)
        {
            foreach (var e in ctx.Model.OfClass(TransportModule.Class).ToList()) TransportModule.Sync(ctx, e.Id);
            return;
        }
        if (after.Class != TransportModule.Class) return;
        var attrs = after.CloneAttrs();
        TransportModule.Normalize(attrs, before.Attrs);
        if (attrs.ToJsonString() != after.Attrs.ToJsonString()) ctx.Replace(after.With(attrs: attrs));
        TransportModule.Retype(ctx, after.Id);
        TransportModule.Sync(ctx, after.Id);
    }

    public bool Delete(ICommandContext ctx, string id)
    {
        if (ctx.Model.Get(id) is not { Class: TransportModule.Class } e) return false;
        foreach (var o in TransportModule.OpeningsOf(ctx.Model, id).ToList()) ctx.Remove(o.Id);
        var type = TransportModule.TypeOf(ctx.Model, e);
        ctx.Remove(id);
        if (type != null && type.Str("key") != null && type.Attrs["articles"] == null && !ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Any()) ctx.Remove(type.Id);
        return true;
    }
}

/// <summary>A lift drawn by the plans of every storey its shaft reaches, its car and doors where it stops.</summary>
sealed class ElevatorSpans : IStoreySpans
{
    public bool Handles(Entity e) => e.Class == TransportModule.Class;

    public bool On(IModelView m, Entity e, Entity storey) =>
        ElevatorLayout.Resolve(m, e, out _) is { } l && l.Crosses(storey.Num("elevation"));

    public IReadOnlyList<PlanItem>? PlanOn(IModelView m, Entity e, Entity storey) =>
        ElevatorLayout.Resolve(m, e, out _) is { } l ? l.PlanOn(storey.Id) : null;
}

/// <summary>A lift's IFC sets with IFC's own names: Pset_TransportElementCommon (its capacity, a fire exit) and Pset_TransportElementElevator (a firefighters lift, its car's clear size).</summary>
sealed class ElevatorSets : IIfcElementSets
{
    public IEnumerable<IfcStandardSet> Sets(IModelView model, Entity element)
    {
        if (element.Class != TransportModule.Class) yield break;
        var s = new ElevatorSpec(element.Attrs);
        yield return new IfcStandardSet("Pset_TransportElementCommon",
        [
            new("CapacityPeople", "IFCCOUNTMEASURE", s.Persons),
            new("CapacityWeight", "IFCMASSMEASURE", s.Load),
            new("FireExit", "IFCBOOLEAN", s.FireExit),
        ]);
        yield return new IfcStandardSet("Pset_TransportElementElevator",
        [
            new("FireFightingLift", "IFCBOOLEAN", s.FireFighting),
            new("ClearWidth", "IFCPOSITIVELENGTHMEASURE", s.CabinWidth),
            new("ClearDepth", "IFCPOSITIVELENGTHMEASURE", s.CabinDepth),
            new("ClearHeight", "IFCPOSITIVELENGTHMEASURE", s.CabinHeight),
        ]);
    }
}

/// <summary>The Lifts schedule: a row per lift, its kind, drive, load and persons, car, doors, stops, travel, speed and shaft.</summary>
public sealed class ElevatorSchedule : IScheduleSource
{
    public string Kind => "elevators";
    public string Label => "Elevator schedule";

    static string Mm(double v) => Math.Round(v * 1000).ToString(CultureInfo.InvariantCulture);
    static string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        foreach (var e in m.OfClass(TransportModule.Class).OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            if (ElevatorLayout.Resolve(m, e, out _) is not { } l) continue;
            var s = l.Spec;
            rows.Add([
                e.Name, TransportModule.KindLabel(m, e), s.Drive, N(s.Load), s.Persons.ToString(CultureInfo.InvariantCulture),
                $"{Mm(s.CabinWidth)} × {Mm(s.CabinDepth)} × {Mm(s.CabinHeight)}", $"{Mm(s.DoorWidth)} × {Mm(s.DoorHeight)} {s.DoorOpening}",
                $"{l.Stops.Count(x => x.Served)}/{l.Stops.Count}", $"{l.Stops[0].Storey.Name} – {l.Stops[^1].Storey.Name}", N(l.Travel, "0.00"), N(s.Speed),
                $"{Mm(l.Wi)} × {Mm(l.Di)}",
            ]);
            keys.Add(e.Id);
        }
        if (rows.Count > 0) { rows.Add(["Total", rows.Count.ToString(CultureInfo.InvariantCulture), "", "", "", "", "", "", "", "", "", ""]); keys.Add(null); }
        return new ScheduleGrid(Label, ["Elevator", "Kind", "Drive", "Rated load (kg)", "Persons", "Car (mm)", "Doors (mm)", "Stops", "Storeys", "Travel (m)", "Speed (m/s)", "Shaft, clear (mm)"], rows, keys);
    }
}
