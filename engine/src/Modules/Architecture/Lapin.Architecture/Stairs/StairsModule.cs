using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// Stairs in the architecture module: the type (IfcStairType, its spec edited
/// in the configurator), the stair (IfcStair: where it stands, how high it
/// climbs, what it fixes), the opening it asks of the floor above, and the
/// queries the configurator and the inspector draw from (docs/spec-stairs.md).
/// </summary>
public static class StairsModule
{
    public static void Register(IModuleRegistry r)
    {
        r.Class(new(IfcClass.StairType, "Stair type", [], "#b9a68a", Kind: "type"));
        r.Class(new(IfcClass.Stair, "Stair",
        [
            new("width", "Width", "length", Min: 0.5, Max: 5),
            new("baseOffset", "Base offset", "length", Min: -5, Max: 5),
            new("topOffset", "Top offset", "length", Min: -5, Max: 5),
            new("rise", "Rise (own)", "length", Min: 0.05, Max: 10),
            new("turn", "Turn", "choice", Choices: ["left", "right"]),
            new("risers", "Risers (fixed)", "number", Min: 1, Max: 60),
            new("riserHeight", "Riser (fixed)", "length", Min: 0.1, Max: 0.25),
            new("tread", "Tread (fixed)", "length", Min: 0.2, Max: 0.5),
            new("landing", "Landing (fixed)", "length", Min: 0.3, Max: 5),
            new("rotation", "Rotation (°)", "angle", Min: -360, Max: 360),
            new("noOpening", "No opening in the floor above", "bool"),
        ], "#c9b79c"));
        r.Geometry(IfcClass.Stair, (e, m, _) => StairBuilder.Geometry(e, m));
        r.Quantities(IfcClass.Stair, StairBuilder.Quantities);
        r.Service<IDeletion>(new StairDeletion());

        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        r.Catalog("stairDefaults", new JsonObject
        {
            ["spec"] = StairSpec.Default().ToJson(),
            ["rules"] = new JsonObject
            {
                ["residential"] = JsonSerializer.SerializeToNode(StairRules.Residential, ArchitectureCatalogJson.Default.StairRules),
                ["public"] = JsonSerializer.SerializeToNode(StairRules.Public, ArchitectureCatalogJson.Default.StairRules),
                ["entrance"] = JsonSerializer.SerializeToNode(StairRules.Entrance, ArchitectureCatalogJson.Default.StairRules),
            },
            ["library"] = new JsonArray(StairSpec.Library.Select(x => (JsonNode)new JsonObject { ["key"] = x.Key, ["spec"] = x.Value().ToJson() }).ToArray()),
            ["constructions"] = new JsonArray(StairSpec.Constructions.Select(x => (JsonNode)x).ToArray()),
            ["shapes"] = new JsonArray(StairSpec.Shapes.Select(x => (JsonNode)x).ToArray()),
            ["turns"] = new JsonArray(StairSpec.Turns.Select(x => (JsonNode)x).ToArray()),
            ["sides"] = new JsonArray(StairSpec.Sides.Select(x => (JsonNode)x).ToArray()),
        });

        r.Command("createStairType", "New stair type", (ctx, a) =>
        {
            var spec = StairSpec.From(a["spec"]) ?? throw new CommandException("The stair type's specification cannot be read.");
            return new JsonObject { ["id"] = CreateType(ctx, spec).Id };
        });

        // A ready-made type (Entrance steps): the project's copy, made the first time it is asked for.
        r.Command("stairTypeFromLibrary", "Stair type from the library", (ctx, a) =>
            new JsonObject { ["id"] = LibraryType(ctx, (string?)a["key"]).Id });

        r.Command("updateStairType", "Change stair type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.StairType);
            var spec = StairSpec.From(a["spec"]) ?? throw new CommandException("The stair type's specification cannot be read.");
            EnsureMaterials(ctx, spec);
            var riseBefore = StairSpec.From(type.Attrs["spec"])?.Rise;
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec.ToJson();
            ctx.Replace(type.With(name: spec.Name, attrs: attrs, predefinedType: spec.PredefinedType));
            // Its stairs take the shape's IFC name and ask the floor above again; those climbing the type's own rise keep their tops where they were.
            foreach (var stair in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).ToList())
            {
                var own = stair.CloneAttrs();
                if (stair.NumOrNull("rise") == null && riseBefore != spec.Rise)
                    own["baseOffset"] = (riseBefore, spec.Rise) switch
                    {
                        ({ } b, { } n) => stair.Num("baseOffset") + b - n,
                        (null, { } n) => -n,
                        _ => 0.0,
                    };
                var renamed = stair.With(attrs: own, predefinedType: spec.PredefinedType);
                ctx.Replace(renamed);
                StairBuilder.EnsureOpening(ctx, renamed);
            }
            return null;
        });

        // A stair: its foot (the centre of the first riser), the way it climbs (a second point, or an angle), which way it turns.
        // A stair with a rise of its own (entrance steps) arrives at its storey's level unless a base offset is given: from −rise up to ±0.00.
        r.Command("addStair", "Stair", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            Spatial.Require(ctx.Model, storeyId, IfcClass.BuildingStorey);
            var type = (string?)a["typeId"] is { } tid ? Spatial.Require(ctx.Model, tid, IfcClass.StairType)
                : (string?)a["library"] is { } key ? LibraryType(ctx, key) : DefaultType(ctx);
            var at = Args.Point(a, "x", "y");
            var rotation = Json.Number(a["rotation"]);
            if (rotation == null && a["x2"] != null)
            {
                var to = Args.Point(a, "x2", "y2");
                if ((to - at).Length < 1e-6) throw new CommandException("The stair needs a direction: a second point away from its foot.");
                rotation = Math.Atan2(to.Y - at.Y, to.X - at.X) * 180 / Math.PI;
            }
            var attrs = new JsonObject
            {
                ["at"] = new JsonArray(at.X, at.Y), ["rotation"] = rotation ?? 0, ["turn"] = (string?)a["turn"] == "right" ? "right" : "left",
                ["baseOffset"] = 0.0, ["topOffset"] = 0.0,
            };
            foreach (var k in new[] { "width", "risers", "riserHeight", "tread", "landing", "height", "rise", "topOffset" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            if ((string?)a["topStoreyId"] is { } top) attrs["topStoreyId"] = Spatial.Require(ctx.Model, top, IfcClass.BuildingStorey).Id;
            var spec = StairSpec.From(type.Attrs["spec"]) ?? StairSpec.Default();
            var ownRise = Json.Number(a["rise"]) is { } ar && ar > 0 ? ar : spec.Rise;
            attrs["baseOffset"] = Json.Number(a["baseOffset"]) ?? (ownRise is { } rise ? -rise : 0.0);
            var stair = new Entity(ctx.NewId(), IfcClass.Stair, Name(ctx.Model, spec), attrs, spec.PredefinedType);
            ctx.Add(stair);
            Spatial.Contain(ctx, storeyId, stair.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, stair.Id);
            StairBuilder.EnsureOpening(ctx, stair);
            return new JsonObject { ["id"] = stair.Id };
        });

        // What the inspector changes: the type, the width, the turn, the offsets, what is fixed (null frees it), the opening.
        r.Command("updateStair", "Change stair", (ctx, a) =>
        {
            var stair = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Stair);
            var attrs = stair.CloneAttrs();
            var riseBefore = StairBuilder.OwnRise(ctx.Model, stair);
            foreach (var k in new[] { "width", "baseOffset", "topOffset", "risers", "riserHeight", "tread", "landing", "height", "rotation", "rise" })
                if (a.ContainsKey(k)) { if (Json.Number(a[k]) is { } v) attrs[k] = v; else attrs.Remove(k); }
            // A rise of its own changed (and no base offset with it): the top stays where it was, at the porch or the door, and the foot moves.
            if (a.ContainsKey("rise") && !a.ContainsKey("baseOffset") && riseBefore is { } before && Json.Number(a["rise"]) is { } after && after > 0)
                attrs["baseOffset"] = stair.Num("baseOffset") + before - Math.Max(after, 0.05);
            if (a.ContainsKey("x") && a.ContainsKey("y")) { var at = Args.Point(a, "x", "y"); attrs["at"] = new JsonArray(at.X, at.Y); }
            if ((string?)a["turn"] is { } turn) attrs["turn"] = turn == "right" ? "right" : "left";
            if (a.ContainsKey("noOpening")) { if (a["noOpening"] is JsonValue nv && nv.TryGetValue<bool>(out var no) && no) attrs["noOpening"] = true; else attrs.Remove("noOpening"); }
            if (a.ContainsKey("topStoreyId")) { if ((string?)a["topStoreyId"] is { } top) attrs["topStoreyId"] = Spatial.Require(ctx.Model, top, IfcClass.BuildingStorey).Id; else attrs.Remove("topStoreyId"); }
            var predefined = stair.PredefinedType;
            if ((string?)a["typeId"] is { } tid)
            {
                var type = Spatial.Require(ctx.Model, tid, IfcClass.StairType);
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, stair.Id);
                var typeSpec = StairSpec.From(type.Attrs["spec"]) ?? StairSpec.Default();
                predefined = typeSpec.PredefinedType;
                // Into entrance steps (a rise of their own) or out of them: the stair keeps its top at its storey's level, or starts from it again.
                if (!a.ContainsKey("baseOffset") && !attrs.ContainsKey("rise") && (riseBefore == null) != (typeSpec.Rise == null))
                    attrs["baseOffset"] = typeSpec.Rise is { } typeRise ? -typeRise : 0.0;
            }
            var updated = stair.With(attrs: attrs, predefinedType: predefined);
            ctx.Replace(updated);
            StairBuilder.EnsureOpening(ctx, updated);
            return null;
        });

        // The configurator's preview: a spec as it is being edited, the height and width to try it at.
        r.Query("stairPreview", (q, a) =>
        {
            var spec = StairSpec.From(a["spec"]) ?? throw new CommandException("The stair type's specification cannot be read.");
            var height = spec.Rise ?? Json.Number(a["height"]) ?? Spatial.Storeys(q.Model).FirstOrDefault()?.Num("height", 3) ?? 3;
            var r = StairSolver.Resolve(spec, height, new StairInputs(Json.Number(a["width"])), (string?)a["turn"] != "right");
            return Describe(r, new StairPlacement(new Vec2(0, 0), new Vec2(1, 0), (string?)a["turn"] != "right", 0), spec);
        });

        // The tool's preview: the stair a type would make on a storey, in its own frame (foot at the origin, climbing +X).
        r.Query("stairPlan", (q, a) =>
        {
            var storey = Spatial.Require(q.Model, (string?)a["storeyId"] ?? Spatial.Storeys(q.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            // A library type (the Entrance steps tool): the project's copy when it has one, else the library's own spec.
            var key = (string?)a["library"];
            var type = (string?)a["typeId"] is { } tid ? q.Model.Get(tid)
                : key != null ? q.Model.OfClass(IfcClass.StairType).FirstOrDefault(t => t.Str("library") == key)
                : FirstStoreyType(q.Model);
            var spec = (type != null ? StairSpec.From(type.Attrs["spec"]) : null)
                ?? (key != null && StairSpec.Library.TryGetValue(key, out var make) ? make() : StairSpec.Default());
            var above = Spatial.Storeys(q.Model).FirstOrDefault(s => s.Num("elevation") > storey.Num("elevation") + 1e-6);
            var height = spec.Rise ?? (above?.Num("elevation") ?? storey.Num("elevation") + storey.Num("height", 3)) - storey.Num("elevation");
            var left = (string?)a["turn"] != "right";
            var r = StairSolver.Resolve(spec, height, new StairInputs(Json.Number(a["width"])), left);
            return Describe(r, new StairPlacement(new Vec2(0, 0), new Vec2(1, 0), left, 0), spec);
        });

        // The configurator's drawings: the stair the spec would make, built in a session of its own with a floor above, drawn by the
        // 2D generator as a plan (its symbol) and a section along the first flight, with the materials' hatches and the pens.
        r.Query("stairDrawing", (q, a) =>
        {
            var spec = StairSpec.From(a["spec"]) ?? throw new CommandException("The stair type's specification cannot be read.");
            // Entrance steps (a rise of their own) are drawn standing on the ground, with no floor above to open.
            var height = spec.Rise ?? Math.Max(0.3, Json.Number(a["height"]) ?? Spatial.Storeys(q.Model).FirstOrDefault()?.Num("height", 3) ?? 3);
            var turn = (string?)a["turn"] == "right" ? "right" : "left";
            var scale = Json.Number(a["scale"]) ?? 50;
            var catalog = q.Services as ModuleCatalog ?? throw new CommandException("No modules to draw with.");
            var s = new Session(catalog, new ProductManifest("stair-preview", "Stair preview", ["core", .. catalog.Modules.Select(m => m.Id)]));
            // One ground floor at ±0.00 (a new project's foundations would be the first storey, and its first floor the one climbed to).
            s.New(plain: true);
            JsonNode? Run(string command, JsonObject args) { s.Execute(command, args, out var result); return result; }
            var ground = Spatial.Storeys(s.Model).First();
            var type = (string)Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
            var r0 = StairSolver.Resolve(spec, height, new StairInputs(Json.Number(a["width"])), turn == "left");
            var pts = r0.Footprint().SelectMany(x => x).ToList();
            double x0 = pts.Min(p => p.X) - 1.5, x1 = pts.Max(p => p.X) + 1.5, y0 = pts.Min(p => p.Y) - 1.5, y1 = pts.Max(p => p.Y) + 1.5;
            if (spec.Rise == null)
            {
                var upper = (string)Run("storey.create", new JsonObject { ["name"] = "Above", ["elevation"] = height, ["height"] = 3.0 })!["id"]!;
                Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1)) });
            }
            var stair = new JsonObject { ["storeyId"] = ground.Id, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0, ["turn"] = turn, ["baseOffset"] = 0.0 };
            if (Json.Number(a["width"]) is { } width) stair["width"] = width;
            Run("architecture.addStair", stair);
            var plan = (string)Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = ground.Id, ["scale"] = scale })!["id"]!;
            var section = (string)Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(x0 + 1, 0), ["end"] = new JsonArray(x1 - 1, 0), ["scale"] = scale })!["id"]!;
            return new JsonObject { ["plan"] = Views.Drawing(s.Reader, s.Model.Get(plan)!), ["section"] = Views.Drawing(s.Reader, s.Model.Get(section)!) };
        });

        // A stair as it is: its numbers and checks, for the inspector.
        r.Query("stairCheck", (q, a) =>
        {
            var stair = Spatial.Require(q.Model, (string?)a["id"], IfcClass.Stair);
            if (StairBuilder.Resolve(q.Model, stair) is not var (r, place, spec)) return null;
            var o = Describe(r, place, spec);
            o["opening"] = StairBuilder.OpeningOf(q.Model, stair.Id)?.Id;
            var st = Spatial.StoreyOf(q.Model, stair.Id);
            o["topStorey"] = st != null ? StairBuilder.TopStorey(q.Model, stair, st)?.Id : null;
            o["ownRise"] = StairBuilder.OwnRise(q.Model, stair);
            o["fromLevel"] = place.Z0;
            o["toLevel"] = place.Z0 + r.Height;
            return o;
        });
    }

    /// <summary>The resolved stair as JSON: the numbers, the checks, the outlines in plan and the first flight's profile for a section.</summary>
    static JsonObject Describe(Resolved r, StairPlacement place, StairSpec spec)
    {
        static JsonArray Pts(IEnumerable<Vec2> pts) => new(pts.SelectMany(p => new JsonNode[] { Math.Round(p.X, 4), Math.Round(p.Y, 4) }).ToArray());
        var outlines = new JsonArray();
        foreach (var fl in r.Flights) outlines.Add((JsonNode)new JsonObject { ["kind"] = "flight", ["points"] = Pts(r.FlightOutline(fl).Select(p => place.World(p))), ["risers"] = fl.Risers });
        foreach (var ld in r.Landings) outlines.Add((JsonNode)new JsonObject { ["kind"] = "landing", ["points"] = Pts(ld.Outline.Select(p => place.World(p))) });
        foreach (var wd in r.Winders) outlines.Add((JsonNode)new JsonObject { ["kind"] = "winder", ["points"] = Pts(wd.Outline.Select(p => place.World(p))) });
        var risers = new JsonArray();
        foreach (var fl in r.Flights)
            for (var i = 1; i <= fl.Risers; i++)
            {
                var l = fl.Left * (r.Width / 2);
                var u = (i - 1) * r.Tread;
                risers.Add((JsonNode)Pts([place.World(fl.Start + fl.Dir * u - l), place.World(fl.Start + fl.Dir * u + l)]));
            }
        // The first flight's profile (u along, z up), finished surfaces.
        var profile = new JsonArray();
        var f0 = r.Flights[0];
        for (var i = 1; i <= f0.Risers; i++) { profile.Add((JsonNode)((i - 1) * r.Tread)); profile.Add((JsonNode)(i * r.RiserHeight)); if (i < f0.Risers) { profile.Add((JsonNode)(i * r.Tread)); profile.Add((JsonNode)(i * r.RiserHeight)); } }
        var checks = new JsonArray(r.Checks.Select(c => (JsonNode)new JsonObject { ["key"] = c.Key, ["label"] = c.Label, ["value"] = c.Value, ["min"] = c.Min, ["max"] = c.Max, ["ok"] = c.Ok }).ToArray());
        var top = place.World(r.Top.At);
        return new JsonObject
        {
            ["risers"] = r.Risers, ["riserHeight"] = r.RiserHeight, ["tread"] = r.Tread, ["step"] = r.Step, ["width"] = r.Width, ["height"] = r.Height,
            ["landing"] = r.LandingLength, ["flights"] = new JsonArray(r.Flights.Select(f => (JsonNode)f.Risers).ToArray()), ["winders"] = r.Winders.Count,
            ["run"] = r.Flights.Sum(f => (f.Risers - 1) * r.Tread), ["ok"] = r.Ok, ["checks"] = checks, ["outlines"] = outlines, ["risersLines"] = risers,
            ["profile"] = profile, ["top"] = new JsonArray(top.X, top.Y), ["predefinedType"] = spec.PredefinedType, ["shape"] = spec.Shape,
        };
    }

    static string Name(IModelView m, StairSpec spec) => $"Stair {m.OfClass(IfcClass.Stair).Count() + 1}";

    static Entity CreateType(ICommandContext ctx, StairSpec spec)
    {
        EnsureMaterials(ctx, spec);
        var type = new Entity(ctx.NewId(), IfcClass.StairType, spec.Name, new JsonObject { ["spec"] = spec.ToJson() }, spec.PredefinedType);
        ctx.Add(type);
        return type;
    }

    /// <summary>The project's copy of a ready-made type (marked <c>library</c> with its key), made from the library the first time.</summary>
    public static Entity LibraryType(ICommandContext ctx, string? key)
    {
        if (key == null || !StairSpec.Library.TryGetValue(key, out var make)) throw new CommandException($"No stair type „{key}” in the library.");
        if (ctx.Model.OfClass(IfcClass.StairType).FirstOrDefault(t => t.Str("library") == key) is { } existing) return existing;
        var type = CreateType(ctx, make());
        var attrs = type.CloneAttrs();
        attrs["library"] = key;
        var marked = type.With(attrs: attrs);
        ctx.Replace(marked);
        return marked;
    }

    /// <summary>The project's first stair type that climbs to a storey (not entrance steps), null when it has none.</summary>
    public static Entity? FirstStoreyType(IModelView m) =>
        m.OfClass(IfcClass.StairType).FirstOrDefault(t => StairSpec.From(t.Attrs["spec"])?.Rise == null);

    /// <summary>The project's first stair type that climbs to a storey, made from the default when it has none.</summary>
    public static Entity DefaultType(ICommandContext ctx) =>
        FirstStoreyType(ctx.Model) ?? CreateType(ctx, StairSpec.Default());

    static void EnsureMaterials(ICommandContext ctx, StairSpec spec)
    {
        Materials.Ensure(ctx, spec.Construction.Concrete);
        if (spec.Finish.TreadThickness > 0) Materials.Ensure(ctx, spec.Finish.TreadMaterial);
    }
}

/// <summary>A stair deleted takes the opening it asked of the floor above with it.</summary>
sealed class StairDeletion : IDeletion
{
    public bool Delete(ICommandContext ctx, string id)
    {
        if (ctx.Model.Get(id) is not { Class: IfcClass.Stair }) return false;
        if (StairBuilder.OpeningOf(ctx.Model, id) is { } opening) ctx.Remove(opening.Id);
        ctx.Remove(id);
        return true;
    }
}
