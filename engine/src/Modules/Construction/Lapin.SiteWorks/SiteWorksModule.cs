using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.SiteWorks;

/// <summary>
/// Site works (docs/plan-site-works.md): facade scaffolding and formwork, each
/// from a system type its configurator sets, each created and struck in the
/// execution phase (temporary), laid out from the model as it is now.
/// </summary>
public sealed class SiteWorksModule : ILapinModule
{
    public string Id => "sitework";
    public string Name => "Site works";

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Scaffolds.Class, "Scaffold",
        [
            new("side", "Scaffold on the", "choice", Choices: ["left", "right"]), new("top", "Work top (fixed)", "length"), new("height", "Height (no walls)", "length", Min: 1),
            new("baseOffset", "Base offset", "length"), new("onStorey", "Stands on the storey", "bool"),
        ], Scaffolds.Color));
        r.Class(new(Scaffolds.TypeClass, "Scaffold type", [], Scaffolds.Color, Kind: "type"));
        r.Class(new(Formwork.Class, "Formwork", [new("direction", "Primary beams' direction", "angle")], Formwork.Color));
        r.Class(new(Formwork.TypeClass, "Formwork type", [], Formwork.Color, Kind: "type"));
        r.Geometry(Scaffolds.Class, (e, m, s) => Scaffolds.Geometry(e, m, s));
        r.Quantities(Scaffolds.Class, Scaffolds.Quantities);
        r.Geometry(Formwork.Class, (e, m, s) => Formwork.Geometry(e, m, s));
        r.Quantities(Formwork.Class, Formwork.Quantities);
        r.Service<IScheduleSource>(new FormworkSchedule());
        r.Service<IScheduleSource>(new ScaffoldSchedule());
        r.Catalog("scaffoldDefault", Scaffolds.DefaultSpec());
        r.Catalog("slabFormworkDefault", Formwork.DefaultSpec("slab"));
        r.Catalog("beamFormworkDefault", Formwork.DefaultSpec("beam"));
        r.Catalog("uprightFormworkDefault", Formwork.DefaultSpec("upright"));

        // ------------------------------------------------------------ types
        r.Command("createScaffoldType", "New scaffold type", (ctx, a) =>
            new JsonObject { ["id"] = CreateType(ctx, Scaffolds.TypeClass, (string?)a["name"] ?? "Facade scaffold", null, a["spec"] as JsonObject).Id });
        r.Command("createFormworkType", "New formwork type", (ctx, a) =>
        {
            var kind = Formwork.Kinds.Contains((string?)a["kind"]) ? (string)a["kind"]! : throw new CommandException("A formwork type is for floors (slab), beams (beam) or walls, columns and footings (upright).");
            return new JsonObject { ["id"] = CreateType(ctx, Formwork.TypeClass, (string?)a["name"] ?? KindName(kind), kind, a["spec"] as JsonObject).Id };
        });
        foreach (var (command, cls) in new[] { ("updateScaffoldType", Scaffolds.TypeClass), ("updateFormworkType", Formwork.TypeClass) })
            r.Command(command, "Change type", (ctx, a) =>
            {
                var t = Spatial.Require(ctx.Model, (string?)a["id"], cls);
                var attrs = t.CloneAttrs();
                if (a["spec"] is JsonObject spec) attrs["spec"] = Clean(cls, t.Str("kind"), spec);
                var name = ((string?)a["name"])?.Trim();
                if (name is { Length: 0 }) throw new CommandException("A type's name cannot be empty.");
                ctx.Replace(t.With(name: name ?? t.Name, attrs: attrs));
                return null;
            });

        // ------------------------------------------------------------ scaffolds
        r.Command("addScaffold", "Scaffold", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var pts = Args.Points(a);
            if (pts.Count < 2) throw new CommandException("A scaffold needs a line of two points at least, along the facade.");
            var attrs = new JsonObject { ["path"] = Scaffolds.PathJson(pts), ["side"] = (string?)a["side"] == "right" ? "right" : "left", ["exportAs"] = "IfcElementAssembly", ["resource"] = "SCAFFOLD" };
            if (Bool(a, "closed") && pts.Count >= 3) attrs["closed"] = true;
            foreach (var k in new[] { "top", "height", "baseOffset" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            var e = Place(ctx, Scaffolds.Class, "Scaffold", attrs, "SCAFFOLD", storeyId, Type(ctx, Scaffolds.TypeClass, null, (string?)a["typeId"]));
            return new JsonObject { ["id"] = e.Id };
        });

        // Along the outer faces of walls, followed as they change: up to their tops, on the lowest one's storey.
        r.Command("scaffoldFromWalls", "Scaffold on walls", (ctx, a) =>
        {
            var walls = Ids(a, "wallIds").Select(id => ctx.Model.Get(id)).Where(w => w?.Class == IfcClass.Wall).Cast<Entity>().ToList();
            if (walls.Count == 0) throw new CommandException("Select the walls the scaffold goes along.");
            if (Scaffolds.FromWalls(walls, ctx.Model, ctx.Services) is not { } line) throw new CommandException("The walls have no faces to follow (curved walls are not followed yet).");
            var storey = walls.Select(w => Spatial.StoreyOf(ctx.Model, w.Id)).OfType<Entity>().MinBy(s => s.Num("elevation"));
            var attrs = new JsonObject
            {
                ["walls"] = new JsonArray(walls.Select(w => (JsonNode)w.Id).ToArray()), ["path"] = Scaffolds.PathJson(line.Points), ["side"] = "left", ["exportAs"] = "IfcElementAssembly", ["resource"] = "SCAFFOLD",
            };
            if (line.Closed) attrs["closed"] = true;
            if (Json.Number(a["top"]) is { } top) attrs["top"] = top;
            var e = Place(ctx, Scaffolds.Class, "Scaffold", attrs, "SCAFFOLD", storey?.Id ?? Spatial.Storeys(ctx.Model).First().Id, Type(ctx, Scaffolds.TypeClass, null, (string?)a["typeId"]));
            return new JsonObject { ["id"] = e.Id, ["closed"] = line.Closed };
        });

        r.Command("updateScaffold", "Change scaffold", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], Scaffolds.Class);
            var attrs = e.CloneAttrs();
            if (a["points"] != null)
            {
                var pts = Args.Points(a);
                if (pts.Count < 2) throw new CommandException("A scaffold needs a line of two points at least.");
                attrs["path"] = Scaffolds.PathJson(pts);
                attrs.Remove("walls");
            }
            if (a["closed"] != null) { if (Bool(a, "closed")) attrs["closed"] = true; else attrs.Remove("closed"); }
            if ((string?)a["side"] is { } side) attrs["side"] = side == "right" ? "right" : "left";
            if (a["onStorey"] != null) { if (Bool(a, "onStorey")) attrs["onStorey"] = true; else attrs.Remove("onStorey"); }
            foreach (var k in new[] { "top", "height", "baseOffset" })
            {
                if (a[k] is null && a.ContainsKey(k)) attrs.Remove(k);
                else if (Json.Number(a[k]) is { } v) attrs[k] = v;
            }
            if (Bool(a, "detach")) attrs.Remove("walls");
            ctx.Replace(e.With(attrs: attrs));
            if ((string?)a["typeId"] is { } typeId) ctx.Relate(IfcClass.RelDefinesByType, Spatial.Require(ctx.Model, typeId, Scaffolds.TypeClass).Id, e.Id);
            return null;
        });

        // ------------------------------------------------------------ formwork
        // On each host (a floor or a beam): all its formable faces, or those named; added to its formwork if it has one.
        r.Command("addFormwork", "Formwork", (ctx, a) =>
        {
            var hosts = Ids(a, "hostIds").Select(id => ctx.Model.Get(id)).OfType<Entity>().Where(h => Formwork.KindFor(h.Class) != null).ToList();
            if (hosts.Count == 0) throw new CommandException("Select the floors, beams, walls, columns or footings to form.");
            var faces = (a["faces"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList();
            var ids = new JsonArray();
            foreach (var h in hosts)
            {
                var kind = Formwork.KindFor(h.Class)!;
                if (faces != null && faces.Count > 0)
                {
                    var known = ElementFaces.Of(ctx.Services, h, ctx.Model).Select(f => f.Role).ToHashSet();
                    known.UnionWith(kind == "upright" ? ["Auto", "Sides", "Zones", "Ends"] : ["Edges"]);
                    if (faces.FirstOrDefault(f => !known.Contains(f)) is { } bad) throw new CommandException($"{h.Name} has no face {bad}.");
                }
                var existing = ctx.Model.OfClass(Formwork.Class).FirstOrDefault(f => f.Str("host") == h.Id);
                if (existing != null)
                {
                    var attrs = existing.CloneAttrs();
                    var now = faces == null ? null : Formwork.FacesOf(existing).Union(faces).ToList();
                    if (now == null) attrs.Remove("faces"); else attrs["faces"] = new JsonArray(now.Select(x => (JsonNode)x).ToArray());
                    ctx.Replace(existing.With(attrs: attrs));
                    ids.Add((JsonNode)existing.Id);
                    continue;
                }
                var o = new JsonObject { ["host"] = h.Id, ["kind"] = kind, ["exportAs"] = "IfcElementAssembly", ["resource"] = "FORMWORK" };
                if (faces is { Count: > 0 }) o["faces"] = new JsonArray(faces.Select(x => (JsonNode)x).ToArray());
                var storey = (kind == "upright" ? null : StoreyUnder(ctx, h)) ?? Spatial.StoreyOf(ctx.Model, h.Id)?.Id ?? Spatial.Storeys(ctx.Model).First().Id;
                var e = Place(ctx, Formwork.Class, $"Formwork · {h.Name}", o, "FORMWORK", storey, Type(ctx, Formwork.TypeClass, kind, (string?)a["typeId"]));
                ids.Add((JsonNode)e.Id);
            }
            return new JsonObject { ["ids"] = ids };
        });

        r.Command("updateFormwork", "Change formwork", (ctx, a) =>
        {
            var e = Spatial.Require(ctx.Model, (string?)a["id"], Formwork.Class);
            var attrs = e.CloneAttrs();
            if (a["faces"] is JsonArray faces)
            {
                var l = faces.Select(x => (string?)x).OfType<string>().Distinct().ToList();
                if (l.Count == 0) throw new CommandException("A formwork forms one face at least; delete it to take it away.");
                attrs["faces"] = new JsonArray(l.Select(x => (JsonNode)x).ToArray());
            }
            if (a["direction"] is null && a.ContainsKey("direction")) attrs.Remove("direction");
            else if (Json.Number(a["direction"]) is { } d) attrs["direction"] = d;
            ctx.Replace(e.With(attrs: attrs));
            if ((string?)a["typeId"] is { } typeId)
            {
                var t = Spatial.Require(ctx.Model, typeId, Formwork.TypeClass);
                if ((t.Str("kind") ?? "slab") != Formwork.KindOf(e)) throw new CommandException($"{t.Name} is a type for {KindWhat(t.Str("kind"))}.");
                ctx.Relate(IfcClass.RelDefinesByType, t.Id, e.Id);
            }
            return null;
        });

        // A plan of a storey in the execution phase, cut a metre up, the props cut: seen from below (look "up", the default), the formwork
        // over them as seen lines up to the level above (a reflected ceiling plan); seen from above ("down"), drawn overhead, dashed.
        r.Command("formworkPlan", "Formwork plan", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var look = (string?)a["look"] ?? "up";
            if (look is not ("up" or "down")) throw new CommandException("A formwork plan is seen from below (up) or from above (down).");
            var attrs = new JsonObject { ["kind"] = "plan", ["scale"] = 50, ["storeyId"] = storey.Id, ["cutHeight"] = 1.0 };
            if (look == "up") attrs["look"] = "up";
            if (ctx.Services.Get<IPhaseService>()?.EnsureExecutionPhase(ctx) is { } phase) attrs["phaseId"] = phase;
            var v = new Entity(ctx.NewId(), Views.Class, $"Formwork plan · {storey.Name}{(look == "up" ? " (from below)" : "")}", attrs);
            ctx.Add(v);
            return new JsonObject { ["id"] = v.Id };
        });

        // ------------------------------------------------------------ configurators
        r.Query("sitework.scaffoldPreview", (q, a) => Scaffolds.Preview(a["spec"] as JsonObject, Json.Number(a["length"]) ?? 10, Json.Number(a["height"]) ?? 6));
        r.Query("sitework.formworkPreview", (q, a) => Formwork.Preview((string?)a["kind"] is "beam" or "upright" ? (string)a["kind"]! : "slab", a["spec"] as JsonObject,
            Json.Number(a["thickness"]) ?? 0.2, Json.Number(a["width"]) ?? 0.3, Json.Number(a["height"]) ?? 2.8, Json.Number(a["depth"]) ?? 0.5));
    }

    /// <summary>The storey a formwork stands on: the highest whose level is under the host's lowest face (a floor's soffit, a beam's bottom), so the plan of the storey below draws its props.</summary>
    static string? StoreyUnder(ICommandContext ctx, Entity host)
    {
        var faces = ElementFaces.Of(ctx.Services, host, ctx.Model).Where(f => !f.IsEdge).ToList();
        if (faces.Count == 0) return null;
        var bottom = faces.SelectMany(f => f.Outline).Min(p => p.Z);
        return Spatial.Storeys(ctx.Model).Where(s => s.Num("elevation") <= bottom - 0.01).MaxBy(s => s.Num("elevation"))?.Id;
    }

    static string KindName(string kind) => kind switch { "beam" => "Beam formwork", "upright" => "Wall and column formwork", _ => "Floor formwork" };
    static string KindWhat(string? kind) => kind switch { "beam" => "beams", "upright" => "walls, columns and footings", _ => "floors" };

    static bool Bool(JsonObject a, string key) => a[key] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    static IEnumerable<string> Ids(JsonObject a, string key) => (a[key] as JsonArray)?.Select(x => (string?)x).OfType<string>() ?? [];

    /// <summary>A spec as it is kept: only the keys the kind knows, over nothing (the defaults fill the rest when read).</summary>
    static JsonObject Clean(string cls, string? kind, JsonObject spec)
    {
        var known = cls == Scaffolds.TypeClass ? Scaffolds.DefaultSpec() : Formwork.DefaultSpec(kind ?? "slab");
        var o = new JsonObject();
        foreach (var (k, v) in spec) if (known.ContainsKey(k)) o[k] = v?.DeepClone();
        return o;
    }

    static Entity CreateType(ICommandContext ctx, string cls, string name, string? kind, JsonObject? spec)
    {
        if (name.Trim().Length == 0) throw new CommandException("A type's name cannot be empty.");
        var attrs = new JsonObject { ["spec"] = Clean(cls, kind, spec ?? new JsonObject()) };
        if (kind != null) attrs["kind"] = kind;
        var t = new Entity(ctx.NewId(), cls, name.Trim(), attrs, "USERDEFINED");
        ctx.Add(t);
        return t;
    }

    /// <summary>The type asked for, else the project's first of the kind, else a generic one made now.</summary>
    static Entity Type(ICommandContext ctx, string cls, string? kind, string? id)
    {
        if (id != null)
        {
            var t = Spatial.Require(ctx.Model, id, cls);
            if (kind != null && (t.Str("kind") ?? "slab") != kind) throw new CommandException($"{t.Name} is not a type for {KindWhat(kind)}.");
            return t;
        }
        return ctx.Model.OfClass(cls).FirstOrDefault(t => kind == null || t.Str("kind") == kind)
            ?? CreateType(ctx, cls, cls == Scaffolds.TypeClass ? "Facade scaffold (generic)" : $"{KindName(kind ?? "slab")} (generic)", kind, null);
    }

    /// <summary>A temporary element: on its storey, of its type, created and struck in the execution phase.</summary>
    static Entity Place(ICommandContext ctx, string cls, string name, JsonObject attrs, string predefined, string storeyId, Entity type)
    {
        if (ctx.Services.Get<IPhaseService>()?.EnsureExecutionPhase(ctx) is { } phase)
        {
            attrs["phaseCreated"] = phase;
            attrs["phaseDemolished"] = phase;
        }
        var n = ctx.Model.OfClass(cls).Count() + 1;
        var e = new Entity(ctx.NewId(), cls, n == 1 ? name : $"{name} {n}", attrs, predefined);
        ctx.Add(e);
        Spatial.Contain(ctx, storeyId, e.Id);
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
        return e;
    }

    internal static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    internal static string Q(IReadOnlyList<QuantitySet> sets, string name) => sets.SelectMany(s => s.Quantities).FirstOrDefault(q => q.Name == name) is { } q ? N(q.Value) : "";
}

/// <summary>
/// The formwork schedule: each formwork with its host, its type and deviz
/// article, its contact areas and its props; then, per type, the total, the
/// set it needs and what to buy. The set is the most cast at once: by
/// execution phase when the formwork is spread over several, else by storey
/// (the work going storey by storey); what to buy is that set, or the total
/// over the type's reuses when that is more.
/// </summary>
public sealed class FormworkSchedule : IScheduleSource
{
    public string Kind => "formwork";
    public string Label => "Formwork schedule";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var rows = new List<IReadOnlyList<string>>();
        var all = new List<(Entity E, Entity? Type, double Contact)>();
        double props = 0;
        foreach (var e in q.Model.OfClass(Formwork.Class).OrderBy(e => e.Name))
        {
            var sets = Formwork.Quantities(e, q.Geometry(e.Id), q.Model).ToList();
            double Sum(string n) => sets.SelectMany(s => s.Quantities).Where(x => x.Name == n).Sum(x => x.Value);
            var kind = Formwork.KindOf(e);
            var type = Formwork.TypeOf(q.Model, e);
            var (code, text) = Formwork.ArticleOf(q.Model, e);
            var article = string.Join(" ", new[] { code, text }.Where(x => x.Length > 0));
            var (first, second) = kind switch { "beam" => ("BottomArea", "SideArea"), "upright" => ("", "ContactArea"), _ => ("SoffitArea", "EdgeArea") };
            rows.Add([e.Name, Formwork.HostOf(q.Model, e)?.Name ?? "—", type?.Name ?? "", article, first.Length > 0 ? SiteWorksModule.N(Sum(first)) : "", SiteWorksModule.N(Sum(second)),
                SiteWorksModule.N(Sum("ContactArea")), SiteWorksModule.N(Sum("Props")), "", ""]);
            all.Add((e, type, Sum("ContactArea")));
            props += Sum("Props");
        }
        if (rows.Count == 0) return new ScheduleGrid(Label, Columns, rows);
        rows.Add(["Total", "", "", "", "", "", SiteWorksModule.N(all.Sum(x => x.Contact)), SiteWorksModule.N(props), "", ""]);
        foreach (var byType in all.GroupBy(x => x.Type?.Id ?? "").OrderBy(g => g.First().Type?.Name))
        {
            var type = byType.First().Type;
            var total = byType.Sum(x => x.Contact);
            var phases = byType.Select(x => x.E.Str("phaseCreated")).Distinct().Count();
            var set = byType.GroupBy(x => phases > 1 ? x.E.Str("phaseCreated") ?? "" : Spatial.StoreyOf(q.Model, x.E.Id)?.Id ?? "").Max(g => g.Sum(x => x.Contact));
            var reuses = Math.Max(1, (int)(Json.Number((type?.Attrs["spec"] as JsonObject)?["reuses"]) ?? (Formwork.DefaultSpec(type?.Str("kind") ?? "slab")["reuses"] is JsonValue r && r.TryGetValue<int>(out var n) ? n : 1)));
            var buy = Math.Max(set, total / reuses);
            rows.Add([$"Type: {type?.Name ?? "—"}", phases > 1 ? "set by phase" : "set by storey", type?.Name ?? "", "", "", "", SiteWorksModule.N(total), "", SiteWorksModule.N(set), SiteWorksModule.N(buy)]);
        }
        return new ScheduleGrid(Label, Columns, rows);
    }

    static readonly string[] Columns = ["Formwork", "On", "Type", "Article", "Soffit / bottom (m²)", "Edges / sides (m²)", "Contact (m²)", "Props", "Set (m²)", "To buy (m²)"];
}

/// <summary>The scaffold schedule: each scaffold with its length, height, area, bays, lifts, decks and anchors, and the totals.</summary>
public sealed class ScaffoldSchedule : IScheduleSource
{
    public string Kind => "scaffold";
    public string Label => "Scaffold schedule";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var rows = new List<IReadOnlyList<string>>();
        double area = 0;
        foreach (var e in q.Model.OfClass(Scaffolds.Class).OrderBy(e => e.Name))
        {
            var sets = Scaffolds.Quantities(e, q.Geometry(e.Id), q.Model).ToList();
            rows.Add([e.Name, Scaffolds.TypeOf(q.Model, e)?.Name ?? "", SiteWorksModule.Q(sets, "Length"), SiteWorksModule.Q(sets, "Height"), SiteWorksModule.Q(sets, "Area"),
                SiteWorksModule.Q(sets, "Bays"), SiteWorksModule.Q(sets, "Lifts"), SiteWorksModule.Q(sets, "DeckBoards"), SiteWorksModule.Q(sets, "Anchors")]);
            area += sets.SelectMany(s => s.Quantities).Where(x => x.Name == "Area").Sum(x => x.Value);
        }
        if (rows.Count > 0) rows.Add(["Total", "", "", "", SiteWorksModule.N(area), "", "", "", ""]);
        return new ScheduleGrid(Label, ["Scaffold", "Type", "Length (m)", "Height (m)", "Area (m²)", "Bays", "Lifts", "Deck boards", "Anchors"], rows);
    }
}
