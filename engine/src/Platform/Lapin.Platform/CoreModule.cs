using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The platform's own module, in every product: the spatial structure
/// (IfcProject, IfcSite, IfcBuilding, IfcBuildingStorey), the materials and
/// the commands every element answers to (rename, delete).
/// </summary>
public sealed class CoreModule : ILapinModule
{
    /// <summary>A storey's height from its base and top levels: the top must be above the base.</summary>
    static double TopToHeight(double @base, double top) =>
        top - @base > 1e-6 ? CheckHeight(Math.Round((top - @base) * 1e6) / 1e6) : throw new CommandException($"The top level ({top:0.00}) must be above the base level ({@base:0.00}).");

    /// <summary>
    /// The tallest a storey may be (m): a level typed in the wrong unit (a tester's report, #73) made storeys
    /// kilometres high, and what is built to them (stairs, columns with their bars) froze the program.
    /// </summary>
    public const double MaxStoreyHeight = 100;

    /// <summary>A storey's base level: a number, within ±10 km of ±0.00.</summary>
    static double CheckLevel(double z) =>
        double.IsFinite(z) && Math.Abs(z) <= 10_000 ? z : throw new CommandException($"A storey's level must be within ±10 km of ±0.00 (asked {z:0.##} m).");

    static double CheckHeight(double h)
    {
        if (!(h > 0)) throw new CommandException("A storey's height must be positive.");
        if (h > MaxStoreyHeight) throw new CommandException($"A storey is at most {MaxStoreyHeight:0} m high (asked {h:0.##} m). Check the unit the level was typed in.");
        return h;
    }

    public const string ModuleId = "core";
    public string Id => ModuleId;
    public string Name => "Core";

    static readonly HashSet<string> PlatformClasses =
    [
        IfcClass.Project, IfcClass.Site, IfcClass.Building, IfcClass.BuildingStorey,
        IfcClass.Material, IfcClass.MaterialLayerSet, IfcClass.MaterialLayerSetUsage, Views.Class, Sheets.Class, Links.Class, StoredBodies.SharedClass, IfcSource.RecordClass, Cde.CdeIssues.Class, Cde.Remarks.Remarks.Class, Documents.Documents.Class,
    ];

    public static bool Owns(string ifcClass) => PlatformClasses.Contains(ifcClass) || ifcClass.StartsWith("IfcRel", StringComparison.Ordinal);

    public void Register(IModuleRegistry r)
    {
        // Booleans for whatever cannot be built constructively (openings through walls); none: callers degrade with a diagnostic.
        if (Lapin.Booleans.ManifoldBooleans.Available) r.Service<Lapin.Geometry.ISolidBooleans>(new Lapin.Booleans.ManifoldBooleans());

        r.Class(new(IfcClass.BuildingStorey, "Storey",
            [new("elevation", "Elevation", "length"), new("height", "Height", "length", Min: 0.1)], "#6c5ce7", Kind: "spatial"));
        r.Class(new(IfcClass.Material, "Material", [new("color", "Colour", "color"), new("category", "Category", "text")], Kind: "resource"));
        DraftingResources.Register(r);
        // Line types of the project's own and the standards as a library (docs/plan-user-extensibility.md §3.6–3.7).
        StandardsLibrary.Register(r);
        // Rule packs: the norms the configurators use, as data (docs/plan-user-extensibility.md §5.4).
        RulePacks.Register(r);
        // Classifications, keynotes and shared parameter definitions (docs/plan-user-extensibility.md §7.3).
        Classifications.Register(r);
        ParamFormulas.Register(r);
        Images.Register(r);
        Views.Register(r);
        // The type configurators' preview, drawn as the drawings are (decision 529).
        TypePreviews.Register(r);
        r.Service<IProjectUpgrade>(new StoreyPlans.DefaultPlanUpgrade());
        r.Service<IProjectUpgrade>(new ViewDetail.LevelUpgrade());
        r.Service<IProjectUpgrade>(new MaterialHatchUpgrade());
        r.Service<IProjectUpgrade>(new ViewStyles.StructureUpgrade());
        // References move, turn, mirror, copy and rise like any element (decision 448).
        var references = new ReferencePlacement();
        r.Service<IElementPlacement>(references);
        r.Service<IElementRise>(references);
        // The doors and windows in a reference's kept openings go with it, and with its deletion (docs/spec-ifc.md, Golurile referințelor).
        var fills = new ReferenceFills();
        r.Service<IHostedPlacement>(fills);
        r.Service<IDeletion>(fills);
        // Sections and elevations moved in plan: a section with its cut, an elevation's marker alone (decision 460).
        r.Service<IElementPlacement>(new ViewMarkerPlacement());
        GraphicsSets.Register(r);
        ViewFilters.Register(r);
        BrowserSets.Register(r);
        ViewLinework.Register(r);
        Location.Register(r);
        DisplayQuality.Register(r);
        // Where the sun is for the project's place (docs/plan-visualisation.md): the 3D view's sun, a sun study's.
        Sun.Register(r);
        // A view's sun shadows, drawn as filled areas under elevations, sections and site plans.
        ViewShadows.Register(r);
        Groups.Register(r);
        Arrays.Register(r);
        Components.Register(r);
        // The wholes an element is a part of, for drill-down picking.
        Assemblies.Register(r);
        TypeMappings.Register(r);
        TypePacks.Register(r);
        TypeDefaults.Register(r);
        Sheets.Register(r);
        TitleBlocks.Register(r);
        // 2D symbols of the user's own (docs/plan-user-extensibility.md §3).
        Symbols.Register(r);
        SymbolSlots.Register(r);
        // The table import (docs/plan-user-extensibility.md §7.1) and its first target, property values.
        TableImport.Register(r);
        r.Service<TableTarget>(PropertyTable.Target);
        CutOrder.Register(r);
        StoreyMove.Register(r);
        StoreyCopy.Register(r);
        Links.Register(r);
        // Point clouds linked, indexed outside the model, streamed to the views (docs/point-clouds.md).
        PointCloudLinks.Register(r);
        // The project folder as a CDE: naming, checks, issues (docs/plan-cde.md); the remarks on the model exchanged as BCF.
        Cde.Cde.Register(r);
        Cde.Remarks.Remarks.Register(r);
        // The written documents filled from the project (docs/plan-documents.md), and the project's information they and the title blocks read.
        Documents.Documents.Register(r);
        // Openings as solid voids, and MEP checked against them (decision 289).
        Coordination.OpeningVoids.Register(r);
        // A body many references share (docs/plan-instancing.md): drawn by its instances, never on its own.
        r.Class(new(StoredBodies.SharedClass, "Shared body", [], "#888888", Kind: "resource", Template: false));
        // What foreign elements share in their file (a type, a system, a material), kept for the export.
        r.Class(new(IfcSource.RecordClass, "IFC record", [], "#888888", Kind: "resource", Template: false));
        ViewStyles.Register(r);
        Coordination.VoidDrawings.Register(r);
        ElementFaces.Register(r);
        Profiles.Register(r);
        ProfileLibraries.ProfileLibraryCommands.Register(r);
        Dependencies.Register(r);
        Analysis.JgExchange.Register(r);

        r.Command("storey.create", "New storey", (ctx, a) =>
        {
            var building = Spatial.EnsureBuilding(ctx);
            var storeys = Spatial.Storeys(ctx.Model).ToList();
            var top = storeys.LastOrDefault();
            var elevation = CheckLevel(Json.Number(a["elevation"]) ?? (top == null ? 0 : top.Num("elevation") + top.Num("height", 3)));
            var name = (string?)a["name"] ?? (storeys.Count == 0 ? "Ground floor" : $"Level {storeys.Count}");
            var storey = new Entity(ctx.NewId(), IfcClass.BuildingStorey, name, new JsonObject
            {
                ["elevation"] = elevation,
                ["height"] = Json.Number(a["top"]) is { } t ? TopToHeight(elevation, t) : CheckHeight(Json.Number(a["height"]) ?? 3.0),
            });
            ctx.Add(storey);
            ctx.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
            return new JsonObject { ["id"] = storey.Id };
        });

        r.Command("storey.update", "Change storey", (ctx, a) =>
        {
            var s = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.BuildingStorey);
            var attrs = s.CloneAttrs();
            // A storey lies between its base level (elevation) and its top level; its height is what lies between. Either may be below ±0.00.
            var (oldBase, oldTop) = (s.Num("elevation"), s.Num("elevation") + s.Num("height", 3));
            var keepTop = a["keepTop"] is JsonValue kt && kt.TryGetValue<bool>(out var k) && k;
            var @base = CheckLevel(Json.Number(a["elevation"]) ?? oldBase);
            attrs["elevation"] = @base;
            if (Json.Number(a["top"]) is { } top) attrs["height"] = TopToHeight(@base, top);
            else if (Json.Number(a["height"]) is { } h) attrs["height"] = CheckHeight(h);
            else if (keepTop) attrs["height"] = TopToHeight(@base, oldTop);
            ctx.Replace(s.With(name: (string?)a["name"], attrs: attrs));
            return null;
        });

        r.Command("element.rename", "Rename", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Nothing to rename.");
            // An element a module drives: its module first (docs/overrides.md, Driven elements).
            if (Driven.Before(ctx, new DrivenEdit("element.rename", [e.Id], a)).Handled.Contains(e.Id)) return null;
            var name = ((string?)a["name"])?.Trim();
            if (string.IsNullOrEmpty(name)) throw new CommandException("A name cannot be empty.");
            // A drafted shape's name is unique on its view (Studio finds its profiles and paths by it).
            if (e.PredefinedType == "DRAFT" && e.Str("viewId") is { } view
                && ctx.Model.OfClass(e.Class).Any(x => x.Id != e.Id && x.PredefinedType == "DRAFT" && x.Str("viewId") == view && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new CommandException($"Another shape on this view is called „{name}”.");
            ctx.Replace(e.With(name: name));
            return null;
        });

        // The user's own parameters on any element, drafted shape or Studio solid: a name, a kind, a value (exported as Lapin_Parameters).
        // With `pset`, in that property set: a standard one (Pset_DoorCommon) only with its own property names, held as their kind;
        // one of the user's never „Pset_…” (StandardPsets). On a type, `instance: true` makes it an instance default (TypeDefaults).
        r.Command("element.setParam", "Set parameter", (ctx, a) =>
        {
            var ids = Driven.Rest(Args.Ids(a), Driven.Before(ctx, new DrivenEdit("element.setParam", Args.Ids(a), a)));
            if (ids.Count == 0) throw new CommandException("Which element?");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A parameter needs a name.");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("Which element?");
                // A shared definition of that name gives its set and kind when the command gives none (docs/plan-user-extensibility.md §7.3).
                var shared = a["pset"] == null && a["kind"] == null ? ParamDefs.Find(ctx.Model, name) : null;
                var (pset, standardKind) = StandardPsets.Check(e.Class, (string?)a["pset"] ?? shared?.Pset, name);
                var list = e.Attrs["params"] is JsonArray old ? (JsonArray)old.DeepClone() : new JsonArray();
                var existing = list.OfType<JsonObject>().FirstOrDefault(p => Params.Same(p, pset, name));
                var kind = standardKind ?? (string?)a["kind"] ?? (string?)existing?["kind"] ?? shared?.Kind ?? ((string?)a[ParamFormulas.Key] is { Length: > 0 } ? "number" : null) ?? throw new CommandException($"A new parameter needs its kind ({string.Join(", ", Params.Kinds)}).");
                var options = a["options"] as JsonArray ?? existing?["options"] as JsonArray;
                // A formula instead of a value (docs/plan-user-extensibility.md §5.3): worked out on every ask, never stored.
                var formula = (string?)a[ParamFormulas.Key] is { Length: > 0 } fx ? ParamFormulas.Check(fx) : null;
                if (formula != null && kind is not ("number" or "length" or "area" or "angle" or "bool")) throw new CommandException("A formula gives a number: its parameter is a number, a length, an area, an angle or a yes/no.");
                var value = formula != null ? null : Params.Check(kind, a["value"], options);
                var entry = new JsonObject { ["name"] = name, ["kind"] = kind, ["value"] = value };
                if (formula != null) entry[ParamFormulas.Key] = formula;
                if (pset != null) entry["pset"] = pset;
                if (kind == "choice" && options != null) entry["options"] = options.DeepClone();
                // A type's instance default (TypeDefaults): `instance` given sets or clears it, else the parameter keeps what it was.
                var instance = a[Params.InstanceKey] is JsonValue iv && iv.TryGetValue<bool>(out var inst) ? inst : existing != null && Params.IsInstanceDefault(existing);
                if (instance) entry[Params.InstanceKey] = true;
                if (existing != null) list[list.IndexOf(existing)] = entry; else list.Add((JsonNode)entry);
                var attrs = e.CloneAttrs();
                attrs["params"] = list;
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
        r.Command("element.removeParam", "Remove parameter", (ctx, a) =>
        {
            var name = (string?)a["name"] ?? throw new CommandException("Which parameter?");
            var pset = (string?)a["pset"] is { Length: > 0 } ps && ps != StandardPsets.Own ? ps : null;
            foreach (var id in Driven.Rest(Args.Ids(a), Driven.Before(ctx, new DrivenEdit("element.removeParam", Args.Ids(a), a))))
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("Which element?");
                if (e.Attrs["params"] is not JsonArray old) continue;
                var list = new JsonArray(old.OfType<JsonObject>().Where(p => !Params.Same(p, pset, name)).Select(p => (JsonNode)p.DeepClone()).ToArray());
                var attrs = e.CloneAttrs();
                if (list.Count == 0) attrs.Remove("params"); else attrs["params"] = list;
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // The standard property sets the user's parameters may go into, with their properties (StandardPsets).
        r.Query("params.standardSets", (_, _) => StandardPsets.ToJson());

        r.Command("element.update", "Change", (ctx, a) =>
        {
            // One element, or several (ids): each takes the same values.
            var ids = Args.Ids(a).ToList();
            if (ids.Count == 0) throw new CommandException("Nothing to change.");
            var values = a["attrs"] as JsonObject ?? throw new CommandException("Nothing to change.");
            // What a module drives is its module's first: it may refuse a field, or set it in its own terms (docs/overrides.md, Driven elements).
            foreach (var id in Driven.Rest(ids, Driven.Before(ctx, new DrivenEdit("element.update", ids, a))))
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException("Nothing to change.");
                if (StoredBodies.Has(e)) throw new CommandException($"„{e.Name}” is a reference from an IFC file: it can be deleted, not changed.");
                var d = ctx.Describe(e.Class) ?? throw new CommandException($"{e.Class} cannot be changed here.");
                // A pinned element keeps where it stands; its other fields still change (decision 449).
                Pins.RefuseMove(e, values.Select(kv => kv.Key));
                var attrs = e.CloneAttrs();
                foreach (var (key, value) in values)
                {
                    var f = d.Fields.FirstOrDefault(x => x.Key == key) ?? throw new CommandException($"{d.Label} has no field „{key}”.");
                    if (f.ReadOnly) throw new CommandException($"{f.Label} cannot be changed.");
                    if (value is null) { attrs.Remove(key); continue; }
                    attrs[key] = Fields.Check(f, value);
                }
                ctx.Replace(e.With(attrs: attrs));
                // What follows the element (the runs on a node moved by its x) is told, in the same step.
                foreach (var u in ctx.Services.All<IElementUpdate>()) u.Updated(ctx, e, ctx.Model.Get(id)!);
            }
            return null;
        });

        // Move, copy, rotate or mirror elements and annotations in plan (one undo step); a copy returns the new ids.
        // Several commands as one edit (one undo step): a grip shared by two walls moves both ends; each step keeps its own rules.
        // `label` names the step ("Facades" for a script's batch; Session.LabelOf), else it is "Edit".
        r.Command("edit.batch", "Edit", (ctx, a) =>
        {
            var catalog = ctx.Services as ModuleCatalog ?? throw new CommandException("No commands to batch here.");
            if (a["steps"] is not JsonArray steps || steps.Count == 0) throw new CommandException("A batch needs its steps.");
            if (a["label"] is { } label && (label is not JsonValue lv || !lv.TryGetValue<string>(out _))) throw new CommandException("A batch's label is a text.");
            var results = new JsonArray();
            foreach (var step in steps)
            {
                var name = (string?)step?["command"] ?? throw new CommandException("A step needs its command.");
                if (name == "edit.batch") throw new CommandException("A batch cannot hold another batch.");
                var entry = catalog.Command(name) ?? throw new CommandException($"Unknown command „{name}”.");
                results.Add(entry.Handler(ctx, step!["args"] as JsonObject ?? [])?.DeepClone());
            }
            return results;
        });

        // Pinned where it is, or let go (decision 449): {ids, pinned}; a group's members with it. Any element, a reference too.
        r.Command("element.pin", "Pin", (ctx, a) =>
        {
            var ids = Args.Ids(a).ToList();
            if (ids.Count == 0) throw new CommandException("Select what to pin.");
            var pinned = a["pinned"] is not JsonValue pv || !pv.TryGetValue<bool>(out var pb) || pb;
            return new JsonObject { ["changed"] = Pins.Set(ctx, ids, pinned) };
        });

        r.Command("element.transform", "Transform", (ctx, a) =>
        {
            var t = ElementTransform.Run(ctx, a);
            var o = new JsonObject { ["ids"] = t.Made };
            // A 3D move's vertical part left out on what has no height of its own (docs/plan-3d-modelling.md).
            if (t.Kept.Count > 0) o["kept"] = new JsonArray(t.Kept.Select(x => (JsonNode)x).ToArray());
            return o;
        });

        // Copies in a row or round a centre (decision 448), or along a path, kept as an arrangement (decision 481, Arrays):
        // {ids, count (with the first), step: [x, y, z] | fit: [x, y, z]} or {kind: "polar", count, angle filled (degrees,
        // 360 a full turn), center: [x, y]} or {kind: "path", along | points, count | spacing, start, end, offset, orient,
        // flip, base}; each copy as element.transform copies, one undo step. Returns the copies and the array.
        r.Command("element.array", "Array", (ctx, a) => Arrays.Make(ctx, a));

        r.Command("element.delete", "Delete", (ctx, a) =>
        {
            // What a module drives (and the groups it owns) it deletes in its own terms, or refuses (docs/overrides.md, Driven elements).
            var driven = Driven.Before(ctx, new DrivenEdit("element.delete", Args.Ids(a), a));
            var given = Driven.Rest(Args.Ids(a), driven);
            // A group deleted goes with its members; a group its last member left goes too (docs/groups.md).
            var (ids, groups) = Groups.Expand(ctx.Model, given);
            ids = Driven.Rest(ids, driven);
            // What is pinned stays (decision 449).
            Pins.Refuse(ids.Concat(groups).Select(id => ctx.Model.Get(id)).OfType<Entity>());
            var groupsTouched = groups.Concat(ids.Select(id => Groups.Of(ctx.Model, id)?.Id).OfType<string>()).Distinct().ToList();
            // What a module's elements still use stays (an entourage kind with figures, its picture), whatever the order asked.
            var deleting = ids.ToHashSet();
            foreach (var id in deleting)
                if (ctx.Model.Get(id) is { } x) foreach (var d in ctx.Services.All<IDeletion>()) d.Check(ctx.Model, x, deleting);
            var bodies = new HashSet<string>();
            foreach (var id in ids.Distinct())
            {
                if (ctx.Model.Get(id) is not { } e)
                {
                    foreach (var d in ctx.Services.All<IDeletion>()) if (d.Delete(ctx, id)) break;
                    continue;
                }
                if (e.Class is IfcClass.Project or IfcClass.Site or IfcClass.Building) throw new CommandException($"The {e.Class} cannot be deleted.");
                if (e.Class == IfcClass.BuildingStorey && ctx.Model.RelatedOf(e.Id, IfcClass.RelContainedInSpatialStructure).Any())
                    throw new CommandException($"„{e.Name}” still has elements on it.");
                // A type in use stays: the elements built with it would be left without one.
                if (ctx.Describe(e.Class)?.Kind == "type" && ctx.Model.RelatedOf(e.Id, IfcClass.RelDefinesByType).Where(x => !ids.Contains(x.Id)).Count() is var n and > 0)
                    throw new CommandException($"„{e.Name}” is used by {n} element(s): give them another type first.");
                // A module may take its own element down with what hangs on it (a stair with the opening it asked of the floor above).
                if (ctx.Services.All<IDeletion>().Any(d => d.Delete(ctx, id))) continue;
                if (StoredBodies.InstanceOf(e) is var (body, _)) bodies.Add(body);
                ctx.Remove(id);
            }
            // A shared body its last instance left goes with it (docs/plan-instancing.md).
            if (bodies.Count > 0)
            {
                foreach (var e in ctx.Model.Entities)
                    if ((string?)e.Attrs[StoredBodies.BodyOf] is { } body) bodies.Remove(body);
                foreach (var body in bodies) ctx.Remove(body);
            }
            foreach (var g in groups) if (ctx.Model.Get(g) is { } x && !Groups.KeptForAbsent(ctx, x)) ctx.Remove(g);
            Groups.DropEmpty(ctx, groupsTouched);
            return null;
        });
    }
}

/// <summary>
/// element.transform's work: elements moved, copied, turned or mirrored in plan, with what they host, their tags and
/// their joins among themselves; Copy to storeys copies through it too (each copied id's copy, the groups' twins).
/// </summary>
public static class ElementTransform
{
    public sealed record Result(JsonArray Made, IReadOnlyDictionary<string, string> Copies, IReadOnlyDictionary<string, string> Groups)
    {
        /// <summary>What a move's <c>up</c> could not raise (it has no height of its own): the ids moved or copied.</summary>
        public IReadOnlyList<string> Kept { get; init; } = [];
    }

    public static Result Run(ICommandContext ctx, JsonObject a, Placement2D? given = null)
    {
        static Lapin.Geometry.Vec2 V(JsonNode? n, string what) => Args.Point(n, what);
        // A group selected is its members (docs/groups.md).
        var (ids, groups) = Groups.Expand(ctx.Model, Args.Ids(a));
        var op = (string?)a["op"] ?? "move";
        // A move's height (rise, or world's third value: an elevation's or a section's move in the model, decision 298).
        var world = a["world"] is JsonArray { Count: >= 2 } wv ? wv : null;
        // A 3D move's vertical part (docs/plan-3d-modelling.md): what has a height of its own goes up or down by it.
        var up = op is "move" or "copy" ? Json.Number(a["up"]) ?? 0 : 0;
        var rise = Json.Number(a["rise"]) ?? (world is { Count: >= 3 } ? Json.Number(world[2]) : null) ?? up;
        // A section's or an elevation's frame (decision 453): the command's points are the view's (x along it, y up), which
        // is what its drafted shapes take; the model's elements are carried by the same edit in the model instead.
        var frame = a["frame"] is JsonObject fo && Args.TryPoint3(fo["right"]) is { } fr && Args.TryPoint3(fo["up"]) is { } fu
            ? (Origin: Args.TryPoint3(fo["origin"]) ?? default, Right: fr, Up: fu) : ((Vec3 Origin, Vec3 Right, Vec3 Up)?)null;
        if (given == null && frame is { } fm && op is "move" or "copy" && world == null)
        {
            var by = V(a["by"], "displacement");
            var w = fm.Right * by.X + fm.Up * by.Y;
            world = new JsonArray(w.X, w.Y, w.Z);
            rise = Json.Number(a["rise"]) ?? w.Z;
        }
        var t = given ?? op switch
        {
            "move" or "copy" => Placement2D.Move(V(a["by"], "displacement"), rise),
            "rotate" => Placement2D.Rotate(V(a["center"], "centre"), (Json.Number(a["angle"]) ?? 0) * Math.PI / 180),
            "mirror" => (V(a["a"], "mirror line's start") - V(a["b"], "mirror line's end")).Length < 1e-9
                ? throw new CommandException("A mirror line needs two different points.") : Placement2D.Mirror(V(a["a"], "start"), V(a["b"], "end")),
            _ => throw new CommandException($"„{op}” is not move, copy, rotate or mirror."),
        };
        var copy = op == "copy" || (a["copy"] is JsonValue cv && cv.TryGetValue<bool>(out var cb) && cb);
        // What a module drives (its groups whole, or members alone) it moves or copies in its own terms, or refuses (docs/overrides.md, Driven elements).
        var driven = Driven.Before(ctx, new DrivenEdit("element.transform", Args.Ids(a), a) { Transform = t, Up = up, Copy = copy });
        if (driven.Handled.Count > 0)
        {
            (ids, groups) = Groups.Expand(ctx.Model, Driven.Rest(Args.Ids(a), driven));
            ids = Driven.Rest(ids, driven);
        }
        var all = ids.Select(id => ctx.Model.Get(id)).OfType<Entity>()
            .Where(e => e.Class is not (IfcClass.Project or IfcClass.Site or IfcClass.Building or IfcClass.BuildingStorey)).ToList();
        // The model's own edit when the view's is not it (a section's or an elevation's): a move along the view and up, a mirror
        // about a vertical line across the plane through it square to the view. A turn there would tip the elements over, and a
        // mirror about any other line turn them upside down: refused for the model's elements, drafted shapes still take them.
        Placement2D? inModel = null;
        double lift = 0;
        // A move given in the model alone (world, an older caller's) carries the elements by it too.
        if (given == null && (frame != null || world != null && op is "move" or "copy"))
        {
            var modelSelected = all.Any(e => !OnView(e)) || ids.Any(id => ctx.Model.Get(id) == null);
            switch (op)
            {
                case "move" or "copy":
                    inModel = Placement2D.Move(Args.PointOf(world), rise);
                    lift = world is { Count: >= 3 } ? Json.Number(world[2]) ?? 0 : 0;
                    break;
                case "rotate" when modelSelected:
                    throw new CommandException("Elements turn in a plan or in 3D: in a section or an elevation a turn would tip them over (drafted shapes turn here).");
                case "mirror" when modelSelected && frame is { } fm2:
                {
                    var (pa, pb) = (V(a["a"], "start"), V(a["b"], "end"));
                    var d = pb - pa;
                    var r = new Vec2(fm2.Right.X, fm2.Right.Y);
                    if (Math.Abs(d.X) > 1e-3 * d.Length || r.Length < 1e-9)
                        throw new CommandException("In a section or an elevation, elements mirror about a vertical line (across the plane through it, square to the view).");
                    // The view's x is measured along its right (a unit vector, level in a section or an elevation): the line's foot in plan.
                    var at = new Vec2(fm2.Origin.X, fm2.Origin.Y) + r * ((pa.X + pb.X) / 2);
                    inModel = Placement2D.Mirror(at, at + new Vec2(-r.Y, r.X));
                    break;
                }
            }
        }
        // How each one is carried: a drafted shape (or a bar drawn on the view) as the view's points say, the rest in the model.
        Placement2D Of(Entity e) => inModel != null && !OnView(e) ? inModel : t;
        // What is pinned is not moved, turned or mirrored; it is copied, and its copies are not pinned (decision 449).
        if (!copy) Pins.Refuse(all);
        // What a module places in a host (a door in its opening, the opening in its wall) is not moved on its own like the rest.
        var hosting = ctx.Services.All<IHostedPlacement>().ToList();
        string? HostOf(Entity e) => ctx.Model.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening
            ? ctx.Model.RelatingOf(opening.Id, IfcClass.RelVoidsElement)?.Id ?? opening.Id
            : ctx.Model.RelatingOf(e.Id, IfcClass.RelVoidsElement)?.Id ?? hosting.Select(h => h.HostOf(ctx.Model, e)).FirstOrDefault(x => x != null);
        var entities = all.Where(e => HostOf(e) == null).ToList();
        var moving = entities.Select(e => e.Id).ToHashSet();
        if (!copy) Components.Leaving(ctx, groups, moving);
        // One whose host moves too goes with it; one whose host stays is its module's to move in a host (report #82: MOVE
        // and COPY on a window slide it along its wall), once: an opening selected with its door is the door's.
        var hostedIds = all.Where(e => HostOf(e) is { } h && !moving.Contains(h)).Select(e => e.Id).ToHashSet();
        var hosted = all.Where(e => hostedIds.Contains(e.Id) && !ctx.Model.RelatedOf(e.Id, IfcClass.RelFillsElement).Any(f => hostedIds.Contains(f.Id))).ToList();
        var made = new JsonArray();
        var copies = new Dictionary<string, string>();
        var twins = new Dictionary<string, string>();
        // In a section or an elevation the view's edit is given in the model too: what is placed in a host moves by it.
        var inHost = inModel ?? t;
        // What follows the elements without being selected (their tags, report #93) is told before and after.
        var selected = all.Select(e => e.Id).ToHashSet();
        var followers = ctx.Services.All<ITransformFollowers>().Select(f => (f, State: f.Before(ctx, moving, selected))).ToList();
        foreach (var e in entities)
        {
            if (ctx.Model.Get(e.Id) is not { } now) continue;
            if (copy) { var c = Placement.Copy(ctx, now, Of(now)); made.Add((JsonNode)c); copies[e.Id] = c; }
            else Placement.Transform(ctx, now, Of(now), moving);
        }
        foreach (var own in ctx.Services.All<IHostedPlacement>())
            if (hosted.Where(e => own.Handles(ctx.Model, e)).ToList() is { Count: > 0 } mine)
                foreach (var c in own.Transform(ctx, mine, inHost, copy)) made.Add((JsonNode)c);
        foreach (var (f, state) in followers) f.After(ctx, state, t, inHost, moving, selected, copies);
        // Elements copied together keep their joins: a corner or a T between two of them joins their copies the same way.
        if (copy)
            foreach (var r in copies.Keys.SelectMany(id => ctx.Model.RelationsOf(id, IfcClass.RelConnectsPathElements)).DistinctBy(r => r.Id).Where(r => copies.ContainsKey(r.Relating) && r.Related.All(copies.ContainsKey)).ToList())
                ctx.AddRelation(new Relation(ctx.NewId(), r.Class, copies[r.Relating], r.Related.Select(x => copies[x]).ToList(), r.Attrs?.DeepClone() as JsonObject));
        // A group copied: its members' copies make a group of their own, with its name.
        if (copy)
            foreach (var g in groups)
                if (ctx.Model.Get(g) is { } group && Groups.Make(ctx, group.Name, Groups.MembersOf(ctx.Model, g).Where(copies.ContainsKey).Select(x => copies[x]).ToList()) is { } twin)
                {
                    made.Add((JsonNode)twin.Id);
                    twins[g] = twin.Id;
                    Components.CopiedInstance(ctx, group, twin, inModel ?? t, up + lift);
                }
        // Parts selected on their own (a grid's axis): their module carries them.
        // One whose element goes as a whole goes with it, once.
        foreach (var id in ids.Distinct().Where(id => ctx.Model.Get(id) == null && !(id.IndexOf('#') is var h and > 0 && moving.Contains(id[..h]))))
            foreach (var part in ctx.Services.All<IPartPlacement>())
                if (part.Transform(ctx, id, inModel ?? t, copy) is { } after)
                {
                    if (copy) made.Add((JsonNode)after);
                    break;
                }
        var kept = new List<string>();
        foreach (var e in entities)
        {
            // A section's or an elevation's move up raises the model's elements by their offset from the level, not what lies on the view.
            var dz = up + (OnView(e) ? 0 : lift);
            if (Math.Abs(dz) <= 1e-9) continue;
            var id = copy ? copies.GetValueOrDefault(e.Id) : e.Id;
            if (id == null || ctx.Model.Get(id) is not { } now) continue;
            if (!ElementRise.Lift(ctx, now, dz)) kept.Add(id);
        }
        // An instance moved as a whole says where it stands now (docs/components.md).
        if (!copy) foreach (var g in groups) Components.MovedInstance(ctx, g, inModel ?? t, up + lift);
        if (copy) Pins.Unpinned(ctx, made.Select(x => (string?)x).OfType<string>());
        foreach (var id in driven.Made) made.Add((JsonNode)id);
        return new Result(made, copies, twins) { Kept = kept };
    }

    /// <summary>What lies on a view and takes the view's points (a drafted shape, a dimension, a bar drawn on it), not the model's.</summary>
    public static bool OnView(Entity e) => e.Class == IfcClass.Annotation || e.Str("viewId") is { Length: > 0 };
}

/// <summary>The spatial structure: one project, site and building; the storeys, lowest first.</summary>
public static class Spatial
{
    public static Entity? Project(IModelView m) => m.OfClass(IfcClass.Project).FirstOrDefault();
    public static Entity? Building(IModelView m) => m.OfClass(IfcClass.Building).FirstOrDefault();

    public static IEnumerable<Entity> Storeys(IModelView m) =>
        m.OfClass(IfcClass.BuildingStorey).OrderBy(s => s.Num("elevation")).ThenBy(s => m.Order(s.Id));

    /// <summary>The storey an element stands on.</summary>
    public static Entity? StoreyOf(IModelView m, string elementId) => m.RelatingOf(elementId, IfcClass.RelContainedInSpatialStructure);

    /// <summary>
    /// The storey an element is drawn on: its own, or for an opening (never contained itself, as IFC has it) the storey of
    /// the element it voids, so a plan shows and picks a wall's bare opening on the wall's storey only (decision 312).
    /// </summary>
    public static Entity? ShownOn(IModelView m, Entity e) =>
        StoreyOf(m, e.Id) ?? (e.Class == IfcClass.OpeningElement && m.RelatingOf(e.Id, IfcClass.RelVoidsElement) is { } host ? StoreyOf(m, host.Id) : null);

    /// <summary>The spatial parent: an element's storey, a storey's building, a building's site.</summary>
    public static Entity? ParentOf(IModelView m, string id) =>
        StoreyOf(m, id) ?? m.RelatingOf(id, IfcClass.RelAggregates);

    public static Entity Require(IModelView m, string? id, string ifcClass) =>
        id != null && m.Get(id) is { } e && e.Class == ifcClass ? e : throw new CommandException($"There is no {ifcClass} {id}.");

    /// <summary>The project, site and building, made when the model has none yet.</summary>
    public static Entity EnsureBuilding(IEdit edit) => EnsureBuilding(edit, null);

    /// <summary>
    /// The project, site and building, made when the model has none yet; with <paramref name="seed"/> (a project made from
    /// an IFC file) under the file's GlobalIds and names, their aggregations remembering the file's GlobalIds too.
    /// </summary>
    public static Entity EnsureBuilding(IEdit edit, SpatialSeed? seed)
    {
        if (Building(edit.Model) is { } b) return b;
        var project = Project(edit.Model);
        if (project == null)
        {
            project = new Entity(IdOf(edit, seed?.Project.Id), IfcClass.Project, seed?.Project.Name ?? "Project", new JsonObject { ["lengthUnit"] = "METRE", [ViewDetail.RevKey] = 1, [MaterialHatchUpgrade.RevKey] = MaterialHatchUpgrade.Rev, [ViewStyles.StructureRevKey] = ViewStyles.StructureRev });
            edit.Add(project);
        }
        var site = edit.Model.OfClass(IfcClass.Site).FirstOrDefault();
        if (site == null)
        {
            site = new Entity(IdOf(edit, seed?.Site.Id), IfcClass.Site, seed?.Site.Name ?? "Site", []);
            edit.Add(site);
            Aggregate(edit, project.Id, site.Id, seed?.Site.Aggregation);
        }
        var building = new Entity(IdOf(edit, seed?.Building.Id), IfcClass.Building, seed?.Building.Name ?? "Building", []);
        edit.Add(building);
        Aggregate(edit, site.Id, building.Id, seed?.Building.Aggregation);
        return building;
    }

    /// <summary>
    /// The key of a relationship's attributes holding the GlobalId the IFC file gave it, which the export writes again
    /// (a spatial aggregation or containment Lapin otherwise names after its owner).
    /// </summary>
    public const string FileGlobalId = "globalId";

    /// <summary>The GlobalId a relationship came with from an IFC file (<see cref="FileGlobalId"/>), or null.</summary>
    public static string? FileGlobalIdOf(Relation? r) =>
        r?.Attrs?[FileGlobalId] is JsonValue v && v.TryGetValue<string>(out var g) && GlobalId.IsValid(g) ? g : null;

    /// <summary>The file's id when it is a GlobalId the model does not hold yet, else a new one.</summary>
    static string IdOf(IEdit edit, string? id) =>
        id != null && GlobalId.IsValid(id) && edit.Model.Get(id) == null && edit.Model.GetRelation(id) == null ? id : edit.NewId();

    /// <summary><paramref name="part"/> aggregated into <paramref name="whole"/>; a new aggregation remembers the file's GlobalId.</summary>
    static void Aggregate(IEdit edit, string whole, string part, string? globalId)
    {
        var r = edit.Relate(IfcClass.RelAggregates, whole, part);
        if (globalId != null && GlobalId.IsValid(globalId) && FileGlobalIdOf(r) == null)
        {
            var attrs = r.Attrs?.DeepClone() as JsonObject ?? [];
            attrs[FileGlobalId] = globalId;
            edit.ReplaceRelation(r with { Attrs = attrs });
        }
    }

    /// <summary>A storey made under the building as a seed gives it (its GlobalId, name, base level and height).</summary>
    public static Entity AddStorey(IEdit edit, Entity building, string? id, string name, double elevation, double height, string? aggregation = null)
    {
        var storey = new Entity(IdOf(edit, id), IfcClass.BuildingStorey, name, new JsonObject { ["elevation"] = elevation, ["height"] = height });
        edit.Add(storey);
        Aggregate(edit, building.Id, storey.Id, aggregation);
        return storey;
    }

    /// <summary>
    /// The lowest storey, made when the model has none (an import into an empty project): the product's first storey,
    /// as the session starts one (Session.StartEmpty), else a ground floor.
    /// </summary>
    public static Entity EnsureStorey(IEdit edit, ProductManifest? product = null)
    {
        if (Storeys(edit.Model).FirstOrDefault() is { } s) return s;
        var building = EnsureBuilding(edit);
        var (name, elevation, height) = product?.Storeys is { Length: > 0 } own ? (own[0].Name, own[0].Elevation, own[0].Height) : ("Ground floor", 0.0, 3.0);
        var storey = new Entity(edit.NewId(), IfcClass.BuildingStorey, name, new JsonObject { ["elevation"] = elevation, ["height"] = height });
        edit.Add(storey);
        edit.Relate(IfcClass.RelAggregates, building.Id, storey.Id);
        return storey;
    }

    /// <summary>Places <paramref name="elementId"/> on <paramref name="storeyId"/>.</summary>
    public static void Contain(IEdit edit, string storeyId, string elementId)
    {
        Require(edit.Model, storeyId, IfcClass.BuildingStorey);
        edit.Relate(IfcClass.RelContainedInSpatialStructure, storeyId, elementId);
    }
}

/// <summary>
/// A project's spatial structure as an IFC file gives it, for a new project made from the file (an IFC opened as a
/// project): the project, site and building with their GlobalIds and names, and the storeys bottom up; each part's
/// <c>Aggregation</c> is the GlobalId of the file's IfcRelAggregates that holds it (docs/spec-ifc.md, Structura spațială).
/// </summary>
public sealed record SpatialSeed(SpatialSeed.Part Project, SpatialSeed.Part Site, SpatialSeed.Part Building, IReadOnlyList<SpatialSeed.Level> Storeys)
{
    public sealed record Part(string? Id, string? Name, string? Aggregation = null);
    public sealed record Level(string? Id, string Name, double Elevation, double Height, string? Aggregation = null);
}

/// <summary>The project's materials (IfcMaterial), found by name.</summary>
public static class Materials
{
    public static Entity? Find(IModelView m, string name) =>
        m.OfClass(IfcClass.Material).FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>The material named <paramref name="name"/>, made when the project has none.</summary>
    public static Entity Ensure(IEdit edit, string name, string? color = null)
    {
        if (Find(edit.Model, name) is { } found) return found;
        var m = new Entity(edit.NewId(), IfcClass.Material, name, new JsonObject { ["color"] = color ?? DefaultColor(name) });
        edit.Add(m);
        return m;
    }

    /// <summary>The material categories the interface offers (IfcMaterial.Category; any other text is kept as given).</summary>
    public static readonly string[] Categories = ["Structure", "Finish", "Insulation", "Membrane", "Other"];

    /// <summary>
    /// Whether a material is a finish, which a view style without finishes
    /// leaves out: its category says so; a material without a category is
    /// one when its name is a finish's (plaster, render, paint, tiles, boards,
    /// floor coverings, in English or Romanian).
    /// </summary>
    public static bool IsFinish(Entity material)
    {
        if (material.Str("category") is { Length: > 0 } c) return string.Equals(c.Trim(), "Finish", StringComparison.OrdinalIgnoreCase);
        var n = material.Name.ToLowerInvariant();
        string[] finishes = ["plaster", "render", "paint", "tile", "gypsum", "plasterboard", "drywall", "parquet", "laminate", "carpet", "skim", "finish",
            "tencu", "glet", "vopsea", "faian", "gresie", "parchet", "rigips", "gips-carton", "finisaj", "zugr"];
        return finishes.Any(n.Contains);
    }

    public static string ColorOf(IModelView m, string? materialId) =>
        materialId != null && m.Get(materialId) is { } mat ? mat.Str("color") ?? DefaultColor(mat.Name) : "#c9ced6";

    /// <summary>A colour from the material's name, until the project gives it one.</summary>
    public static string DefaultColor(string name)
    {
        var n = MaterialLook.Fold(name);
        bool Has(params string[] w) => w.Any(x => n.Contains(x, StringComparison.Ordinal));
        if (Has("insulation", "xps", "eps", "wool")) return "#f2c46d";
        if (Has("glass", "glazing", "sticla", "geam")) return "#9cc8e8";
        if (Has("brick", "caramid")) return "#b5654a";
        if (Has("concrete", "beton")) return "#a8a8a8";
        if (Has("plaster", "render", "tencu")) return "#e8e2d6";
        // Parquet and laminate are wood floors (report #76: they came out grey).
        if (Has("parquet", "parchet", "laminat")) return "#b98a5a";
        if (Has("timber", "wood", "lemn", "oak", "stejar", "plank")) return "#c08a4f";
        if (Has("aac", "bca")) return "#d9d6cf";
        if (Has("steel", "otel", "inox", "alumin", "metal", "zinc")) return "#9a9fa6";
        return "#c9ced6";
    }
}

/// <summary>A value checked against the field that holds it.</summary>
public static class Fields
{
    public static JsonNode Check(FieldDescriptor f, JsonNode value)
    {
        switch (f.Kind)
        {
            case "number" or "length" or "angle":
            {
                var n = Json.Number(value) ?? throw new CommandException($"{f.Label} must be a number.");
                if (!double.IsFinite(n)) throw new CommandException($"{f.Label} must be a number.");
                if (f.Min is { } min && n < min) throw new CommandException($"{f.Label} must be at least {min}.");
                if (f.Max is { } max && n > max) throw new CommandException($"{f.Label} must be at most {max}.");
                return n;
            }
            case "bool":
                return value is JsonValue v && v.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{f.Label} is yes or no.");
            case "choice":
            {
                var c = (string?)value;
                return c != null && (f.Choices ?? []).Contains(c) ? c : throw new CommandException($"{f.Label} must be one of {string.Join(", ", f.Choices ?? [])}.");
            }
            case "color":
            {
                var c = (string?)value;
                return c is { Length: 7 } && c[0] == '#' && c[1..].All(Uri.IsHexDigit) ? c : throw new CommandException($"{f.Label} is a colour like #a8a8a8.");
            }
            default:
                return (string?)value ?? throw new CommandException($"{f.Label} must be text.");
        }
    }

}
