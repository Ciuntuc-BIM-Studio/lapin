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
        // The stairs' rules in the project's rule pack (docs/plan-user-extensibility.md §5.4).
        StairRuleSection.Register(r);
        r.Service<IRuleSection>(new Roofs.RoofRuleSection());
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
            new("sweep", "Turns through (°)", "angle", Min: 10, Max: 1440),
            new("railingSides", "Railing", "choice", Choices: ["none", "left", "right", "both"]),
            new("rotation", "Rotation (°)", "angle", Min: -360, Max: 360),
            new("noOpening", "No opening in the floor above", "bool"),
        ], "#c9b79c"));
        r.Geometry(IfcClass.Stair, (e, m, _) => StairBuilder.Geometry(e, m));
        r.Quantities(IfcClass.Stair, StairBuilder.Quantities);
        // Ramps (decision 252): types of kind "ramp" from the same configurator, placed and changed by the same commands.
        r.Class(new(IfcClass.RampType, "Ramp type", [], "#b9a68a", Kind: "type"));
        r.Class(new(IfcClass.Ramp, "Ramp",
        [
            new("width", "Width", "length", Min: 0.5, Max: 5),
            new("baseOffset", "Base offset", "length", Min: -5, Max: 5),
            new("topOffset", "Top offset", "length", Min: -5, Max: 5),
            new("rise", "Rise (own)", "length", Min: 0.01, Max: 10),
            new("slope", "Slope (fixed)", "number", Min: 0.005, Max: 0.5),
            new("landing", "Landing (fixed)", "length", Min: 0.3, Max: 5),
            new("turn", "Turn", "choice", Choices: ["left", "right"]),
            new("railingSides", "Railing", "choice", Choices: ["none", "left", "right", "both"]),
            new("rotation", "Rotation (°)", "angle", Min: -360, Max: 360),
            new("noOpening", "No opening in the floor above", "bool"),
        ], "#c9b79c"));
        r.Geometry(IfcClass.Ramp, (e, m, _) => RampBuilder.Geometry(e, m));
        r.Quantities(IfcClass.Ramp, RampBuilder.Quantities);
        r.Service<IDeletion>(new StairDeletion());
        r.Service<ITransformFollowers>(new StairLinks());

        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        r.Catalog("stairDefaults", new JsonObject
        {
            ["spec"] = StairSpec.Default().ToJson(),
            ["rules"] = new JsonObject
            {
                ["residential"] = JsonSerializer.SerializeToNode(StairRules.Residential, ArchitectureCatalogJson.Default.StairRules),
                ["public"] = JsonSerializer.SerializeToNode(StairRules.Public, ArchitectureCatalogJson.Default.StairRules),
                ["entrance"] = JsonSerializer.SerializeToNode(StairRules.Entrance, ArchitectureCatalogJson.Default.StairRules),
                ["ramp"] = JsonSerializer.SerializeToNode(StairRules.Ramp, ArchitectureCatalogJson.Default.StairRules),
            },
            ["library"] = new JsonArray(StairSpec.Library.Select(x => (JsonNode)new JsonObject { ["key"] = x.Key, ["spec"] = x.Value().ToJson() }).ToArray()),
            ["constructions"] = new JsonArray(StairSpec.Constructions.Select(x => (JsonNode)x).ToArray()),
            ["shapes"] = new JsonArray(StairSpec.Shapes.Select(x => (JsonNode)x).ToArray()),
            ["rampShapes"] = new JsonArray(StairSpec.RampShapes.Select(x => (JsonNode)x).ToArray()),
            ["ramp"] = StairSpec.Ramp().ToJson(),
            ["turns"] = new JsonArray(StairSpec.Turns.Select(x => (JsonNode)x).ToArray()),
            ["sides"] = new JsonArray(StairSpec.Sides.Select(x => (JsonNode)x).ToArray()),
            ["railingStyles"] = new JsonArray(StairSpec.RailingStyles.Select(x => (JsonNode)x).ToArray()),
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
            var type = RequireType(ctx.Model, (string?)a["id"]);
            var spec = StairSpec.From(a["spec"]) ?? throw new CommandException("The stair type's specification cannot be read.");
            if (spec.TypeClass != type.Class) throw new CommandException(spec.IsRamp ? "A stair type cannot become a ramp: make a new ramp type." : "A ramp type cannot become a stair: make a new stair type.");
            EnsureMaterials(ctx, spec);
            var before = StairSpec.From(type.Attrs["spec"]);
            var baseBefore = StairBuilder.DefaultBase(before?.Rise, before?.RiseFrom ?? "below");
            var baseAfter = StairBuilder.DefaultBase(spec.Rise, spec.RiseFrom);
            var attrs = type.CloneAttrs();
            attrs["spec"] = spec.ToJson();
            ctx.Replace(type.With(name: spec.Name, attrs: attrs, predefinedType: spec.PredefinedType));
            // Its stairs take the shape's IFC name and ask the floor above again; those climbing the type's own rise keep their
            // tops where they were when it comes up from below (entrance steps), their feet when it climbs from the level.
            foreach (var stair in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).ToList())
            {
                var own = stair.CloneAttrs();
                if (stair.NumOrNull("rise") == null && Math.Abs(baseBefore - baseAfter) > 1e-12)
                    own["baseOffset"] = spec.Rise == null ? 0.0 : stair.Num("baseOffset") - baseBefore + baseAfter;
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
            var type = (string?)a["typeId"] is { } tid ? RequireType(ctx.Model, tid)
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
            foreach (var k in new[] { "width", "risers", "riserHeight", "tread", "landing", "landingWidth", "landingRiser", "height", "rise", "topOffset", "sweep", "slope", "winders", "windersEnd", "treadsBetween" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            if ((string?)a["topStoreyId"] is { } top) attrs["topStoreyId"] = Spatial.Require(ctx.Model, top, IfcClass.BuildingStorey).Id;
            var spec = StairSpec.From(type.Attrs["spec"]) ?? StairSpec.Default();
            var ownRise = Json.Number(a["rise"]) is { } ar && ar > 0 ? ar : spec.Rise;
            attrs["baseOffset"] = Json.Number(a["baseOffset"]) ?? StairBuilder.DefaultBase(ownRise, spec.RiseFrom);
            var stair = new Entity(ctx.NewId(), spec.ElementClass, Name(ctx.Model, spec), attrs, spec.PredefinedType);
            ctx.Add(stair);
            Spatial.Contain(ctx, storeyId, stair.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, stair.Id);
            StairBuilder.EnsureOpening(ctx, stair);
            return new JsonObject { ["id"] = stair.Id };
        });

        // What the inspector changes: the type, the width, the turn, the offsets, what is fixed (null frees it), the opening.
        // A stair by sketch (Revit's "stair by sketch", users asked for it, 19.11.2026): {storeyId, typeId? | library?, boundary,
        // riser?, path?, flip?, topStoreyId?, baseOffset?, topOffset?} → {id}. The sketch in plan (each line [ax, ay, bx, by, rise?]):
        // the two sides, the risers across (none: spread as the rules ask), the path saying which way it climbs (StairSketch).
        r.Command("addSketchStair", "Stair by sketch", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            Spatial.Require(ctx.Model, storeyId, IfcClass.BuildingStorey);
            var type = (string?)a["typeId"] is { } tid ? RequireType(ctx.Model, tid)
                : (string?)a["library"] is { } key ? LibraryType(ctx, key) : DefaultType(ctx);
            var spec = StairSpec.From(type.Attrs["spec"]) ?? StairSpec.Default();
            if (spec.ElementClass != IfcClass.Stair) throw new CommandException("A stair by sketch takes a stair type.");
            var attrs = new JsonObject { ["at"] = new JsonArray(0.0, 0.0), ["rotation"] = 0.0, ["turn"] = "left", ["baseOffset"] = 0.0, ["topOffset"] = 0.0 };
            foreach (var k in new[] { "height", "rise", "topOffset", "risers" }) if (Json.Number(a[k]) is { } v) attrs[k] = v;
            if ((string?)a["topStoreyId"] is { } top) attrs["topStoreyId"] = Spatial.Require(ctx.Model, top, IfcClass.BuildingStorey).Id;
            var ownRise = Json.Number(a["rise"]) is { } ar && ar > 0 ? ar : spec.Rise;
            attrs["baseOffset"] = Json.Number(a["baseOffset"]) ?? StairBuilder.DefaultBase(ownRise, spec.RiseFrom);
            var stair = new Entity(ctx.NewId(), IfcClass.Stair, Name(ctx.Model, spec), attrs, "NOTDEFINED");
            ctx.Add(stair);
            Spatial.Contain(ctx, storeyId, stair.Id);
            ctx.Relate(IfcClass.RelDefinesByType, type.Id, stair.Id);
            var placed = WithSketch(ctx.Model, ctx.Model.Get(stair.Id)!, SketchArg(a));
            ctx.Replace(placed);
            StairBuilder.EnsureOpening(ctx, placed);
            return new JsonObject { ["id"] = stair.Id };
        });

        // {id, boundary, riser?, path?, flip?}: a stair's sketch drawn again (in plan); the stair keeps its id and GlobalId. {id, sketch: null}
        // takes the sketch away: the stair is its type's shape again, its foot where the sketch's was.
        r.Command("updateStairSketch", "Change stair sketch", (ctx, a) =>
        {
            var stair = RequireStair(ctx.Model, (string?)a["id"]);
            if (stair.Class != IfcClass.Stair) throw new CommandException("Only a stair is drawn by sketch.");
            if (a.ContainsKey("sketch") && a["sketch"] == null)
            {
                var attrs = stair.CloneAttrs();
                attrs.Remove(StairSketchData.Key);
                var plain = stair.With(attrs: attrs, predefinedType: StairBuilder.SpecOf(ctx.Model, stair).PredefinedType);
                ctx.Replace(plain);
                StairBuilder.EnsureOpening(ctx, plain);
                return null;
            }
            var updated = WithSketch(ctx.Model, stair, SketchArg(a));
            ctx.Replace(updated);
            StairBuilder.EnsureOpening(ctx, updated);
            return null;
        });

        // {id} → {boundary, riser, path, flip}: a stair's sketch in plan, to draw it again (null: drawn by its type's shape).
        r.Query("stairSketch", (q, a) =>
        {
            var stair = RequireStair(q.Model, (string?)a["id"]);
            if (StairSketchData.From(stair.Attrs[StairSketchData.Key]) is not { } local || Spatial.StoreyOf(q.Model, stair.Id) is not { } storey) return null;
            return StairSketch.ToWorld(local, StairBuilder.PlacementOf(stair, storey)).ToJson();
        });

        // {storeyId, typeId?, id?, boundary, riser?, path?, flip?} → {ok, error?, risers, riserHeight, tread, needed, checks}: what a sketch
        // would make, while it is drawn (the risers drawn, how many the rules ask for the height).
        r.Query("stairSketchCheck", (q, a) =>
        {
            var m = q.Model;
            var existing = (string?)a["id"] is { } sid ? m.Get(sid) : null;
            var storey = (existing != null ? Spatial.StoreyOf(m, existing.Id) : null)
                ?? ((string?)a["storeyId"] is { } st ? m.Get(st) : null) ?? Spatial.Storeys(m).FirstOrDefault();
            if (storey == null) return new JsonObject { ["ok"] = false, ["error"] = "The project has no storey." };
            var type = existing != null ? m.RelatingOf(existing.Id, IfcClass.RelDefinesByType)
                : (string?)a["typeId"] is { } tid ? m.Get(tid) : FirstStoreyType(m);
            var spec = (type != null ? StairSpec.From(type.Attrs["spec"]) : null) ?? StairSpec.Default();
            var height = existing != null ? StairBuilder.Height(m, existing, storey)
                : spec.Rise ?? (Spatial.Storeys(m).FirstOrDefault(s => s.Num("elevation") > storey.Num("elevation") + 1e-6)?.Num("elevation") ?? storey.Num("elevation") + storey.Num("height", 3)) - storey.Num("elevation");
            var needed = StairSolver.Resolve(spec with { Shape = "straight" }, height).Risers;
            try
            {
                var sketch = SketchArg(a);
                var r = StairSketch.Resolve(spec, height, existing != null ? StairBuilder.InputsOf(existing) : null, sketch);
                return new JsonObject
                {
                    ["ok"] = true, ["risers"] = r.Risers, ["riserHeight"] = r.RiserHeight, ["tread"] = r.Tread, ["width"] = r.Width, ["height"] = height, ["needed"] = needed,
                    ["drawn"] = sketch.Riser.Count > 0, ["landings"] = r.Landings.Count, ["rulesOk"] = r.Ok,
                    ["checks"] = new JsonArray(r.Checks.Select(c => (JsonNode)new JsonObject { ["key"] = c.Key, ["label"] = c.Label, ["value"] = c.Value, ["min"] = c.Min, ["max"] = c.Max, ["ok"] = c.Ok }).ToArray()),
                };
            }
            catch (CommandException e) { return new JsonObject { ["ok"] = false, ["error"] = e.Message, ["height"] = height, ["needed"] = needed }; }
        });

        r.Command("updateStair", "Change stair", (ctx, a) =>
        {
            var stair = RequireStair(ctx.Model, (string?)a["id"]);
            var attrs = stair.CloneAttrs();
            var riseBefore = StairBuilder.OwnRise(ctx.Model, stair);
            var fromLevel = StairBuilder.SpecOf(ctx.Model, stair).RiseFrom == "level";
            foreach (var k in new[] { "width", "baseOffset", "topOffset", "risers", "riserHeight", "tread", "landing", "landingWidth", "landingRiser", "height", "rotation", "rise", "sweep", "slope", "winders", "windersEnd", "treadsBetween" })
                if (a.ContainsKey(k)) { if (Json.Number(a[k]) is { } v) attrs[k] = v; else attrs.Remove(k); }
            // The railing's sides for this stair alone (null: the type's).
            if (a.ContainsKey("railingSides")) { if ((string?)a["railingSides"] is { } rs && StairSpec.Sides.Contains(rs)) attrs["railingSides"] = rs; else attrs.Remove("railingSides"); }
            // A rise of its own changed (and no base offset with it): the top stays where it was, at the porch or the door, and the foot moves;
            // climbing from its storey's level, the foot stays.
            if (a.ContainsKey("rise") && !a.ContainsKey("baseOffset") && !fromLevel && riseBefore is { } before && Json.Number(a["rise"]) is { } after && after > 0)
                attrs["baseOffset"] = stair.Num("baseOffset") + before - Math.Max(after, 0.05);
            if (a.ContainsKey("x") && a.ContainsKey("y")) { var at = Args.Point(a, "x", "y"); attrs["at"] = new JsonArray(at.X, at.Y); }
            if ((string?)a["turn"] is { } turn) attrs["turn"] = turn == "right" ? "right" : "left";
            if (a.ContainsKey("noOpening")) { if (a["noOpening"] is JsonValue nv && nv.TryGetValue<bool>(out var no) && no) attrs["noOpening"] = true; else attrs.Remove("noOpening"); }
            if (a.ContainsKey("topStoreyId")) { if ((string?)a["topStoreyId"] is { } top) attrs["topStoreyId"] = Spatial.Require(ctx.Model, top, IfcClass.BuildingStorey).Id; else attrs.Remove("topStoreyId"); }
            var predefined = stair.PredefinedType;
            if ((string?)a["typeId"] is { } tid)
            {
                var type = RequireType(ctx.Model, tid);
                var typeSpec = StairSpec.From(type.Attrs["spec"]) ?? StairSpec.Default();
                if (typeSpec.ElementClass != stair.Class) throw new CommandException(stair.Class == IfcClass.Ramp ? "A ramp takes a ramp type." : "A stair takes a stair type.");
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, stair.Id);
                predefined = typeSpec.PredefinedType;
                // Into entrance steps (a rise of their own) or out of them: the stair keeps its top at its storey's level, or starts from it again.
                if (!a.ContainsKey("baseOffset") && !attrs.ContainsKey("rise") && (riseBefore == null) != (typeSpec.Rise == null))
                    attrs["baseOffset"] = StairBuilder.DefaultBase(typeSpec.Rise, typeSpec.RiseFrom);
            }
            var updated = stair.With(attrs: attrs, predefinedType: predefined);
            ctx.Replace(updated);
            StairBuilder.EnsureOpening(ctx, updated);
            return null;
        });

        // One railing of a stair (forum topic 102), selected on its own as stairId#railing:left or …#railing:right:
        // {id (the railing's, or the stair's with side), side?, on?, kind?, height?, style?, profile?}; null frees a setting (the type's).
        // on: true puts the railing on that side back (railingSides), false takes it off; Delete on the railing does the same.
        r.Command("updateStairRailing", "Change stair railing", (ctx, a) =>
        {
            var id = (string?)a["id"] ?? throw new CommandException("Which stair's railing?");
            var (stairId, side) = StairBuilder.ParseRailingId(id) ?? (id, (string?)a["side"] ?? "");
            if (side is not ("left" or "right")) throw new CommandException("A stair's railing is on its left or its right.");
            var stair = ctx.Model.Get(stairId) is { Class: IfcClass.Stair } st ? st : throw new CommandException($"No stair „{stairId}”.");
            var attrs = stair.CloneAttrs();
            var all = attrs["railings"]?.DeepClone() as JsonObject ?? [];
            var own = all[side]?.DeepClone() as JsonObject ?? [];
            if (a.ContainsKey("kind")) { if ((string?)a["kind"] is { } kind && StairBuilder.RailingKinds.Contains(kind)) own["kind"] = kind; else own.Remove("kind"); }
            if (a.ContainsKey("height")) { if (Json.Number(a["height"]) is { } height) own["height"] = Math.Clamp(height, 0.3, 2.5); else own.Remove("height"); }
            if (a.ContainsKey("style")) { if ((string?)a["style"] is { } style && StairSpec.RailingStyles.Contains(style)) own["style"] = style; else own.Remove("style"); }
            if (a.ContainsKey("profile")) { if ((string?)a["profile"] is "round" or "square") own["profile"] = (string)a["profile"]!; else own.Remove("profile"); }
            if (own.Count > 0) all[side] = own; else all.Remove(side);
            if (all.Count > 0) attrs["railings"] = all; else attrs.Remove("railings");
            if (a["on"] is JsonValue onValue && onValue.TryGetValue<bool>(out var on)) attrs["railingSides"] = WithSide(StairBuilder.RailingOf(stair, StairBuilder.SpecOf(ctx.Model, stair)).Sides, side, on);
            ctx.Replace(stair.With(attrs: attrs));
            return null;
        });

        // The configurator's preview: a spec as it is being edited, the height and width to try it at.
        r.Query("stairPreview", (q, a) =>
        {
            var spec = StairSpec.From(a["spec"]) ?? throw new CommandException("The stair type's specification cannot be read.");
            var height = spec.Rise ?? Json.Number(a["height"]) ?? Spatial.Storeys(q.Model).FirstOrDefault()?.Num("height", 3) ?? 3;
            var place = new StairPlacement(new Vec2(0, 0), new Vec2(1, 0), (string?)a["turn"] != "right", 0);
            if (spec.IsRamp) return DescribeRamp(RampSolver.Resolve(spec, height, new RampInputs(Json.Number(a["width"])), place.TurnLeft), place);
            var r = StairSolver.Resolve(spec, height, new StairInputs(Json.Number(a["width"])), place.TurnLeft);
            return Describe(r, place, spec);
        });

        // The tool's preview: the stair a type would make on a storey, in its own frame (foot at the origin, climbing +X).
        r.Query("stairPlan", (q, a) =>
        {
            var storey = Spatial.Require(q.Model, (string?)a["storeyId"] ?? Spatial.Storeys(q.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            // A library type (the Entrance steps tool): the project's copy when it has one, else the library's own spec.
            var key = (string?)a["library"];
            var type = (string?)a["typeId"] is { } tid ? q.Model.Get(tid)
                : key != null ? Types(q.Model).FirstOrDefault(t => t.Str("library") == key)
                : FirstStoreyType(q.Model);
            var spec = (type != null ? StairSpec.From(type.Attrs["spec"]) : null)
                ?? (key != null && StairSpec.Library.TryGetValue(key, out var make) ? make() : StairSpec.Default());
            var above = Spatial.Storeys(q.Model).FirstOrDefault(s => s.Num("elevation") > storey.Num("elevation") + 1e-6);
            var height = spec.Rise ?? (above?.Num("elevation") ?? storey.Num("elevation") + storey.Num("height", 3)) - storey.Num("elevation");
            var left = (string?)a["turn"] != "right";
            if (spec.IsRamp) return DescribeRamp(RampSolver.Resolve(spec, height, new RampInputs(Json.Number(a["width"])), left), new StairPlacement(new Vec2(0, 0), new Vec2(1, 0), left, 0));
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
            var pts = (spec.IsRamp ? RampSolver.Resolve(spec, height, new RampInputs(Json.Number(a["width"])), turn == "left").Footprint()
                : StairSolver.Resolve(spec, height, new StairInputs(Json.Number(a["width"])), turn == "left").Footprint()).SelectMany(x => x).ToList();
            double x0 = pts.Min(p => p.X) - 1.5, x1 = pts.Max(p => p.X) + 1.5, y0 = pts.Min(p => p.Y) - 1.5, y1 = pts.Max(p => p.Y) + 1.5;
            if (spec.Rise == null || spec.RiseFrom == "level")
            {
                var upper = (string)Run("storey.create", new JsonObject { ["name"] = "Above", ["elevation"] = height, ["height"] = 3.0 })!["id"]!;
                Run("architecture.addSlab", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1)) });
            }
            var stair = new JsonObject { ["storeyId"] = ground.Id, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0, ["turn"] = turn, ["baseOffset"] = 0.0 };
            // A long ramp's section along its first flight: as far as the flight runs.
            if (spec.IsRamp) (x0, x1) = (Math.Min(x0, -1.5), Math.Max(x1, 1.5));
            if (Json.Number(a["width"]) is { } width) stair["width"] = width;
            Run("architecture.addStair", stair);
            var plan = (string)Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = ground.Id, ["scale"] = scale })!["id"]!;
            var section = (string)Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(x0 + 1, 0), ["end"] = new JsonArray(x1 - 1, 0), ["scale"] = scale })!["id"]!;
            return new JsonObject { ["plan"] = Views.Drawing(s.Reader, s.Model.Get(plan)!), ["section"] = Views.Drawing(s.Reader, s.Model.Get(section)!) };
        });

        // A stair as it is: its numbers and checks, for the inspector.
        r.Query("stairCheck", (q, a) =>
        {
            var stair = RequireStair(q.Model, (string?)a["id"]);
            JsonObject o;
            double from, to;
            if (stair.Class == IfcClass.Ramp)
            {
                if (RampBuilder.Resolve(q.Model, stair) is not var (rr, rp, _)) return null;
                o = DescribeRamp(rr, rp);
                (from, to) = (rp.Z0, rp.Z0 + rr.Height);
            }
            else
            {
                if (StairBuilder.Resolve(q.Model, stair) is not var (r, place, spec)) return null;
                o = Describe(r, place, spec);
                (from, to) = (place.Z0, place.Z0 + r.Height);
            }
            o["opening"] = StairBuilder.OpeningOf(q.Model, stair.Id)?.Id;
            var st = Spatial.StoreyOf(q.Model, stair.Id);
            o["topStorey"] = st != null ? StairBuilder.TopStorey(q.Model, stair, st)?.Id : null;
            o["ownRise"] = StairBuilder.OwnRise(q.Model, stair);
            o["fromLevel"] = from;
            o["toLevel"] = to;
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
                var l = fl.Left * (r.WidthOf(fl) / 2);
                var u = (i - 1) * r.Tread;
                risers.Add((JsonNode)Pts([place.World(fl.Start + fl.Dir * u - l), place.World(fl.Start + fl.Dir * u + l)]));
            }
        // The first flight's profile (u along, z up), finished surfaces.
        var profile = new JsonArray();
        // A spiral's or curved stair's profile is unrolled along its walking line.
        var f0 = r.Flights.Count > 0 ? r.Flights[0] : new Flight(r.Risers, new Vec2(0, 0), new Vec2(1, 0), 0);
        for (var i = 1; i <= f0.Risers; i++) { profile.Add((JsonNode)((i - 1) * r.Tread)); profile.Add((JsonNode)(i * r.RiserHeight)); if (i < f0.Risers) { profile.Add((JsonNode)(i * r.Tread)); profile.Add((JsonNode)(i * r.RiserHeight)); } }
        var checks = new JsonArray(r.Checks.Select(c => (JsonNode)new JsonObject { ["key"] = c.Key, ["label"] = c.Label, ["value"] = c.Value, ["min"] = c.Min, ["max"] = c.Max, ["ok"] = c.Ok }).ToArray());
        var top = place.World(r.Top.At);
        return new JsonObject
        {
            ["risers"] = r.Risers, ["riserHeight"] = r.RiserHeight, ["tread"] = r.Tread, ["step"] = r.Step, ["width"] = r.Width, ["height"] = r.Height,
            ["landing"] = r.LandingLength, ["flights"] = new JsonArray(r.Flights.Select(f => (JsonNode)f.Risers).ToArray()), ["winders"] = r.Winders.Count,
            // A turn by winders (report #109): how it turns, the winders at each corner, the straight treads between a U's.
            ["turn"] = spec.Turn, ["corners"] = r.Corners is { } cw ? new JsonArray(cw.Select(x => (JsonNode)x).ToArray()) : null, ["between"] = r.Between,
            ["run"] = r.Flights.Sum(f => (f.Risers - 1) * r.Tread) + (r.Arc?.Steps ?? 0) * r.Tread + (r.Sketch?.Length ?? 0),
            ["sketch"] = r.Sketch != null,
            ["sweep"] = r.Arc is { } arc ? Math.Abs(arc.Step * arc.Steps) * 180 / Math.PI : null,
            ["numbers"] = new JsonArray(StairMarks.StepCentres(r).Select(x => { var p = place.World(x.At); return (JsonNode)new JsonArray(p.X, p.Y, x.Number); }).ToArray()), ["ok"] = r.Ok, ["checks"] = checks, ["outlines"] = outlines, ["risersLines"] = risers,
            ["profile"] = profile, ["top"] = new JsonArray(top.X, top.Y), ["predefinedType"] = spec.PredefinedType, ["shape"] = spec.Shape, ["kind"] = "stair",
        };
    }

    /// <summary>A resolved ramp as JSON, in the stair's keys where they mean the same: the slope and run, the flights' lengths, the checks, the outlines.</summary>
    static JsonObject DescribeRamp(RampResolved r, StairPlacement place)
    {
        static JsonArray Pts(IEnumerable<Vec2> pts) => new(pts.SelectMany(p => new JsonNode[] { Math.Round(p.X, 4), Math.Round(p.Y, 4) }).ToArray());
        var outlines = new JsonArray();
        foreach (var fl in r.Flights) outlines.Add((JsonNode)new JsonObject { ["kind"] = "flight", ["points"] = Pts(fl.Outline().Select(p => place.World(p))) });
        foreach (var ld in r.Landings) outlines.Add((JsonNode)new JsonObject { ["kind"] = "landing", ["points"] = Pts(ld.Outline.Select(p => place.World(p))) });
        var checks = new JsonArray(r.Checks.Select(c => (JsonNode)new JsonObject { ["key"] = c.Key, ["label"] = c.Label, ["value"] = c.Value, ["min"] = c.Min, ["max"] = c.Max, ["ok"] = c.Ok }).ToArray());
        var top = place.World(r.Top.At);
        var f0 = r.Flights[0];
        return new JsonObject
        {
            ["kind"] = "ramp", ["slope"] = r.Slope, ["width"] = r.Width, ["height"] = r.Height, ["landing"] = r.LandingLength, ["run"] = r.Run,
            ["flights"] = new JsonArray(r.Flights.Select(f => (JsonNode)Math.Round(f.Length, 4)).ToArray()), ["ok"] = r.Ok, ["checks"] = checks, ["outlines"] = outlines,
            ["risers"] = 0, ["riserHeight"] = 0, ["tread"] = 0, ["step"] = 0, ["winders"] = 0, ["risersLines"] = new JsonArray(), ["numbers"] = new JsonArray(),
            ["profile"] = new JsonArray(0, f0.Z0, f0.Length, f0.Z1), ["top"] = new JsonArray(top.X, top.Y), ["predefinedType"] = r.Spec.PredefinedType, ["shape"] = r.Spec.Shape,
        };
    }

    /// <summary>The sketch a command was given, in plan: <c>boundary</c>, <c>riser</c>, <c>path</c>, <c>flip</c>.</summary>
    static StairSketchData SketchArg(JsonObject a) =>
        StairSketchData.From(a) ?? throw new CommandException("A stair by sketch needs its boundary: the two sides, each a list of lines [ax, ay, bx, by, rise?].");

    /// <summary>
    /// The stair with a sketch drawn in plan: checked (a sketch that makes no stair is refused, saying why), its foot where the
    /// sketch's first riser is, turned the way it climbs there, and the sketch kept in the stair's own frame.
    /// </summary>
    static Entity WithSketch(IModelView m, Entity stair, StairSketchData world)
    {
        var storey = Spatial.StoreyOf(m, stair.Id) ?? throw new CommandException("The stair stands on no storey.");
        var spec = StairBuilder.SpecOf(m, stair);
        var (at, dir) = StairSketch.Foot(spec, StairBuilder.Height(m, stair, storey), StairBuilder.InputsOf(stair), world);
        var attrs = stair.CloneAttrs();
        attrs["at"] = new JsonArray(at.X, at.Y);
        attrs["rotation"] = Math.Atan2(dir.Y, dir.X) * 180 / Math.PI;
        attrs[StairSketchData.Key] = StairSketch.ToLocal(world, at, dir).ToJson();
        return stair.With(attrs: attrs, predefinedType: "NOTDEFINED");
    }

    /// <summary>The railing's sides with one side put on or taken off: both less one is the other, one less itself is none.</summary>
    public static string WithSide(string sides, string side, bool on)
    {
        var (l, r) = (sides is "left" or "both", sides is "right" or "both");
        if (side == "left") l = on; else r = on;
        return l && r ? "both" : l ? "left" : r ? "right" : "none";
    }

    static string Name(IModelView m, StairSpec spec) => spec.IsRamp ? $"Ramp {Numbering.Next(m.OfClass(IfcClass.Ramp))}" : $"Stair {Numbering.Next(m.OfClass(IfcClass.Stair))}";

    /// <summary>The project's stair and ramp types.</summary>
    public static IEnumerable<Entity> Types(IModelView m) => m.OfClass(IfcClass.StairType).Concat(m.OfClass(IfcClass.RampType));

    static Entity RequireType(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: IfcClass.StairType or IfcClass.RampType } t ? t : throw new CommandException($"No stair or ramp type „{id}”.");

    static Entity RequireStair(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: IfcClass.Stair or IfcClass.Ramp } e ? e : throw new CommandException($"No stair or ramp „{id}”.");

    static Entity CreateType(ICommandContext ctx, StairSpec spec)
    {
        EnsureMaterials(ctx, spec);
        var type = new Entity(ctx.NewId(), spec.TypeClass, spec.Name, new JsonObject { ["spec"] = spec.ToJson() }, spec.PredefinedType);
        ctx.Add(type);
        return type;
    }

    /// <summary>The project's copy of a ready-made type (marked <c>library</c> with its key), made from the library the first time.</summary>
    public static Entity LibraryType(ICommandContext ctx, string? key)
    {
        if (key == null || !StairSpec.Library.TryGetValue(key, out var make)) throw new CommandException($"No stair type „{key}” in the library.");
        if (Types(ctx.Model).FirstOrDefault(t => t.Str("library") == key) is { } existing) return existing;
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

/// <summary>
/// A stair deleted takes the opening it asked of the floor above and the spaces furnished under it with it. One of its railings
/// deleted (selected on its own as stairId#railing:side, forum topic 102) leaves the stair without a railing on that side, and
/// without that side's own settings.
/// </summary>
sealed class StairDeletion : IDeletion
{
    public bool Delete(ICommandContext ctx, string id)
    {
        if (StairBuilder.ParseRailingId(id) is var (stairId, side))
        {
            if (ctx.Model.Get(stairId) is not { Class: IfcClass.Stair } stair) return false;
            var attrs = stair.CloneAttrs();
            attrs["railingSides"] = StairsModule.WithSide(StairBuilder.RailingOf(stair, StairBuilder.SpecOf(ctx.Model, stair)).Sides, side, false);
            if (attrs["railings"]?.DeepClone() is JsonObject all && all.Remove(side)) { if (all.Count > 0) attrs["railings"] = all; else attrs.Remove("railings"); }
            ctx.Replace(stair.With(attrs: attrs));
            return true;
        }
        if (ctx.Model.Get(id) is not { Class: IfcClass.Stair or IfcClass.Ramp }) return false;
        // Every opening it asked for (a floor copied without it once took a second).
        foreach (var opening in ctx.Model.OfClass(IfcClass.OpeningElement).Where(o => o.Str("stairId") == id).ToList()) ctx.Remove(opening.Id);
        // The space furnished under it goes with it (a tester's report, #71).
        foreach (var nook in UnderStairs.Of(ctx.Model, id).ToList()) ctx.Remove(nook.Id);
        ctx.Remove(id);
        return true;
    }
}
