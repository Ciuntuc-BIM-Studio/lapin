using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Architecture.Roofs;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>Architecture: layered walls and their types.</summary>
public sealed class ArchitectureModule : ILapinModule
{
    public string Id => "architecture";
    public string Name => "Architecture";

    public void Register(IModuleRegistry r)
    {
        r.Class(new(IfcClass.Wall, "Wall",
        [
            new("height", "Height", "length", Min: 0.01),
            new("baseOffset", "Base offset", "length"),
            new(PathCurve.Key, "Arc rise", "length"),
            new("cutPriority", "Cut priority", "number", Min: 0, Max: 100),
            new("roofTop", "Top under a roof", "choice", Choices: RoofTop.Modes),
        ], "#b5654a"));
        r.Class(new(IfcClass.WallType, "Wall type", [new("mark", "Mark", "text")], "#b5654a", Kind: "type"));
        r.Class(new(IfcClass.OpeningElement, "Opening", [new("width", "Width", "length", Min: 0.01), new("height", "Height", "length", Min: 0.01)], Kind: "element"));
        r.Class(new(IfcClass.Door, "Door", [], "#c8a27a"));
        r.Class(new(IfcClass.Window, "Window", [], "#9cc8e8"));
        r.Class(new(IfcClass.DoorType, "Door type", [], "#c8a27a", Kind: "type"));
        r.Class(new(IfcClass.WindowType, "Window type", [], "#9cc8e8", Kind: "type"));
        r.Service<IVoidShapes>(new VoidShapes());
        r.Service<IStoreyLevels>(new Levels());
        r.Geometry(IfcClass.Door, (fill, model, _) => Openings.FillGeometry(fill, model));
        r.Geometry(IfcClass.Window, (fill, model, _) => Openings.FillGeometry(fill, model));
        r.Quantities(IfcClass.Door, (fill, _, model) => FillQuantities(fill, model, "Qto_DoorBaseQuantities"));
        r.Quantities(IfcClass.Window, (fill, _, model) => FillQuantities(fill, model, "Qto_WindowBaseQuantities"));
        r.Catalog("joinery", new JsonObject
        {
            ["windowOperations"] = new JsonArray(Joinery.WindowOperations.Select(x => (JsonNode)x).ToArray()),
            ["doorOperations"] = new JsonArray(Joinery.DoorOperations.Select(x => (JsonNode)x).ToArray()),
            ["defaultDoor"] = JoinerySpecs.Door.ToJson(),
            ["defaultWindow"] = JoinerySpecs.Window.ToJson(),
        });

        // The configurator's preview: the body as boxes (front elevation in X–Z), and why the spec cannot be built.
        r.Query("joineryPreview", (q, a) =>
        {
            var spec = JoinerySpec.From(a["spec"]);
            if (spec == null) return new JsonObject { ["problem"] = "The configuration is unreadable." };
            var problem = Joinery.Problem(spec);
            var boxes = problem == null ? Joinery.Boxes(spec) : [];
            return new JsonObject
            {
                ["problem"] = problem,
                ["boxes"] = new JsonArray(boxes.Select(b => (JsonNode)new JsonObject
                {
                    ["role"] = b.Role, ["x0"] = b.Min.X, ["x1"] = b.Max.X, ["y0"] = b.Min.Y, ["y1"] = b.Max.Y, ["z0"] = b.Min.Z, ["z1"] = b.Max.Z,
                }).ToArray()),
                ["panels"] = new JsonArray(Joinery.Normalised(spec).Select(p =>
                {
                    var r = Joinery.PanelRect(spec, p);
                    return (JsonNode)new JsonObject { ["operation"] = p.Operation, ["x0"] = r.X0, ["x1"] = r.X1, ["z0"] = r.Z0, ["z1"] = r.Z1 };
                }).ToArray()),
                ["ifcType"] = spec.IsDoor ? Joinery.DoorOperation(spec).Type : Joinery.WindowPartitioning(spec).Type,
            };
        });

        // Floors.
        r.Class(new(IfcClass.Slab, "Floor", [new("offset", "Top offset", "length"), new("cutPriority", "Cut priority", "number", Min: 0, Max: 100)], "#a8a8a8"));
        // Cut priorities (the old Lapin's): a slab (60) over a wall (50) cuts its head.
        r.Cutter(IfcClass.Wall, 50, (wall, model) => Walls.Envelope(wall, model));
        r.Cutter(IfcClass.Slab, 60, (slab, model) => Slabs.Envelope(slab, model));
        r.Class(new(IfcClass.SlabType, "Floor type", [], "#a8a8a8", Kind: "type"));
        r.Geometry(IfcClass.Slab, (slab, model, services) => Slabs.Geometry(slab, model, services));
        r.Quantities(IfcClass.Slab, Slabs.Quantities);

        LintelProducts.Register(r);
        CurtainWalls.Register(r);
        Stairs.StairsModule.Register(r);

        r.Command("createSlabType", "New floor type", (ctx, a) =>
            new JsonObject { ["id"] = Slabs.CreateType(ctx, (string?)a["name"], LayerSets.Parse(a["layers"], "A floor type")).Id });

        r.Command("updateSlabType", "Change floor type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.SlabType);
            Slabs.UpdateType(ctx, type, (string?)a["name"], a["layers"] is null ? null : LayerSets.Parse(a["layers"], "A floor type"));
            return null;
        });

        r.Command("addSlab", "Floor", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var type = (string?)a["typeId"] is { } typeId ? Spatial.Require(ctx.Model, typeId, IfcClass.SlabType) : null;
            var slab = Slabs.Add(ctx, storeyId, Points(a), Json.Number(a["offset"]) ?? 0, type);
            return new JsonObject { ["id"] = slab.Id };
        });

        r.Command("updateSlab", "Change floor", (ctx, a) =>
        {
            var slab = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Slab);
            var attrs = slab.CloneAttrs();
            if (a["points"] != null || a["x1"] != null) attrs["contour"] = Slabs.ContourJson(Slabs.CheckContour(Points(a)));
            if (Json.Number(a["offset"]) is { } offset) attrs["offset"] = offset;
            ctx.Replace(slab.With(attrs: attrs));
            return null;
        });

        // An opening through a floor (a stairwell, a shaft): its outline in plan.
        r.Command("addSlabOpening", "Floor opening", (ctx, a) =>
        {
            var pts = Points(a);
            // No floor named: the one on the storey whose outline holds the opening's middle.
            var mid = new Vec2(pts.Average(p => p.X), pts.Average(p => p.Y));
            var slab = (string?)a["slabId"] is { } sid ? Spatial.Require(ctx.Model, sid, IfcClass.Slab)
                : ctx.Model.OfClass(IfcClass.Slab).FirstOrDefault(s => ((string?)a["storeyId"] == null || Spatial.StoreyOf(ctx.Model, s.Id)?.Id == (string?)a["storeyId"]) && Polygon2.Contains(Slabs.Contour(s), mid))
                  ?? throw new CommandException("Draw the opening inside a floor.");
            return new JsonObject { ["id"] = Slabs.AddOpening(ctx, slab, pts).Id };
        });

        r.Command("updateSlabOpening", "Change floor opening", (ctx, a) =>
        {
            var o = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.OpeningElement);
            if (o.Attrs["contour"] == null) throw new CommandException("Not a floor opening.");
            var attrs = o.CloneAttrs();
            attrs["contour"] = Slabs.ContourJson(Slabs.CheckContour(Points(a)));
            ctx.Replace(o.With(attrs: attrs));
            return null;
        });

        r.Command("setSlabType", "Change type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["typeId"], IfcClass.SlabType);
            foreach (var id in Ids(a)) Slabs.SetType(ctx, Spatial.Require(ctx.Model, id, IfcClass.Slab), type);
            return null;
        });

        // ------------------------------------------------------------ schedules
        new Schedules.ScheduleEntity().Register(r);

        // ------------------------------------------------------------ roofs
        r.Class(new(IfcClass.Roof, "Roof",
        [
            new("style", "Style", "choice", Choices: [.. RoofCatalog.Styles.Select(s => s.Id)], ReadOnly: true),
            new("shape", "Shape", "choice", Choices: RoofCatalog.Shapes),
            new("pitch", "Pitch (eaves)", "angle", Min: 1, Max: 85),
            new("overhang", "Eave overhang", "length", Min: 0, Max: 5),
            new("gableOverhang", "Gable overhang", "length", Min: 0, Max: 5),
            new("upperPitch", "Mansard: upper pitch", "angle", Min: 1, Max: 60),
            new("mansardInset", "Mansard: break inset", "length", Min: 0.2, Max: 5),
            new("ridgeDirection", "Ridge (gable roof)", "choice", Choices: ["auto", "x", "y"]),
            new("eaveOffset", "Wall plate over the storey's top", "length"),
            new("covering", "Covering", "choice", Choices: [.. RoofCatalog.Coverings.Select(c => c.Id)]),
            new("structure", "Structure", "choice", Choices: ["rafters", "none"]),
            new("cutPriority", "Cut priority", "number", Min: 0, Max: 100),
            new("rafterSection", "Rafters (cm)", "choice", Choices: Roof.RafterSections),
            new("rafterSpacing", "Rafter spacing", "length", Min: 0.3, Max: 2),
            new("purlins", "Purlins per slope", "number", Min: 0, Max: 3),
            new("gutterProfile", "Gutters", "choice", Choices: RoofCatalog.GutterProfiles),
            new("gutterMaterial", "Gutter material", "choice", Choices: RoofCatalog.GutterMaterials),
            new("snowGuards", "Snow guards", "choice", Choices: ["True", "False"]),
        ], "#b0563d"));
        r.Geometry(IfcClass.Roof, (roof, model, services) => RoofCuts.Geometry(roof, model, services));
        // A roof cuts walls and Studio solids by its covering, and is cut by slabs, beams, columns and footings.
        r.Cutter(IfcClass.Roof, 55, RoofCuts.Envelope);
        r.Quantities(IfcClass.Roof, (roof, geometry, model) => Roof.Quantities(roof, geometry, model));
        r.Catalog("roofStyles", System.Text.Json.JsonSerializer.SerializeToNode(RoofCatalog.Styles, ArchitectureCatalogJson.Default.IReadOnlyListStyle)!);
        r.Catalog("roofCoverings", System.Text.Json.JsonSerializer.SerializeToNode(RoofCatalog.Coverings, ArchitectureCatalogJson.Default.IReadOnlyListCovering)!);

        // A roof over the outline drawn (the wall line, at the top of its storey), in a style (default: the catalogue's first).
        r.Command("addRoof", "Roof", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var contour = Slabs.CheckContour(Points(a));
            var style = (string?)a["style"] is { } sid ? RoofCatalog.StyleOf(sid) ?? throw new CommandException($"There is no roof style „{sid}”.") : RoofCatalog.Styles[0];
            var roof = new Entity(ctx.NewId(), IfcClass.Roof, $"Roof {ctx.Model.OfClass(IfcClass.Roof).Count() + 1}", Roof.Attrs(style, contour), "NOTDEFINED");
            CheckRoof(ctx.Model, roof);
            ctx.Add(roof);
            Spatial.Contain(ctx, storeyId, roof.Id);
            return new JsonObject { ["id"] = roof.Id };
        });

        // Put a roof in a style: shape, pitch, overhangs, covering, gutters, snow guards; its per-edge overrides go.
        // A roof window or a dormer where it was clicked on a roof (the roof under the point when none is named); refused when it does not fit.
        r.Command("addRoofOpening", "Roof opening", (ctx, a) =>
        {
            var at = P(a, "x", "y");
            var roof = (string?)a["roofId"] is { } rid ? Spatial.Require(ctx.Model, rid, IfcClass.Roof)
                : ctx.Model.OfClass(IfcClass.Roof).FirstOrDefault(x => RoofTop.ShapeOf(x, ctx.Model) is { } sh && RoofOpenings.HostAt(sh, at) != null)
                  ?? throw new CommandException("Click on a slope of a roof.");
            var kind = (string?)a["kind"] == RoofOpening.Dormer ? RoofOpening.Dormer : RoofOpening.Window;
            var o = new JsonObject { ["id"] = ctx.NewId(), ["kind"] = kind, ["x"] = at.X, ["y"] = at.Y };
            foreach (var k in new[] { "width", "length", "wallHeight", "pitch", "overhang", "thickness" }) if (Json.Number(a[k]) is { } v) o[k] = v;
            if ((string?)a["dormerShape"] is "shed" or "gable") o["dormerShape"] = (string?)a["dormerShape"];
            var count = RoofOpenings.Of(roof).Count(x => x.Kind == kind) + 1;
            o["name"] = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : kind == RoofOpening.Window ? $"Roof window {count}" : $"Dormer {count}";
            WriteRoofOpenings(ctx, roof, [.. (roof.Attrs["openings"] as JsonArray)?.Select(x => x!.DeepClone()) ?? [], o], (string)o["id"]!);
            return new JsonObject { ["roofId"] = roof.Id, ["id"] = (string)o["id"]! };
        });

        r.Command("updateRoofOpening", "Change roof opening", (ctx, a) =>
        {
            var roof = Spatial.Require(ctx.Model, (string?)a["roofId"], IfcClass.Roof);
            var list = (roof.Attrs["openings"] as JsonArray)?.Select(x => (JsonObject)x!.DeepClone()).ToList() ?? [];
            var o = list.FirstOrDefault(x => (string?)x["id"] == (string?)a["id"]) ?? throw new CommandException("The roof has no such opening.");
            foreach (var k in new[] { "x", "y", "width", "length", "wallHeight", "pitch", "overhang", "thickness" }) if (Json.Number(a[k]) is { } v) o[k] = v;
            if ((string?)a["dormerShape"] is "shed" or "gable") o["dormerShape"] = (string?)a["dormerShape"];
            if (((string?)a["name"])?.Trim() is { Length: > 0 } n) o["name"] = n;
            WriteRoofOpenings(ctx, roof, [.. list], (string)o["id"]!);
            return null;
        });

        r.Command("removeRoofOpening", "Remove roof opening", (ctx, a) =>
        {
            var roof = Spatial.Require(ctx.Model, (string?)a["roofId"], IfcClass.Roof);
            var list = (roof.Attrs["openings"] as JsonArray)?.Select(x => x!.DeepClone()).Where(x => (string?)x["id"] != (string?)a["id"]).ToList() ?? [];
            var attrs = roof.CloneAttrs();
            if (list.Count > 0) attrs["openings"] = new JsonArray([.. list]); else attrs.Remove("openings");
            ctx.Replace(roof.With(attrs: attrs));
            return null;
        });

        r.Command("roofStyle", "Roof style", (ctx, a) =>
        {
            var roof = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Roof);
            var style = RoofCatalog.StyleOf((string?)a["style"]) ?? throw new CommandException("There is no such roof style.");
            // The style's fields replace the roof's; the per-edge overrides go. Everything else on the roof
            // (its openings, low edge, structure choices, phase) stays.
            var attrs = roof.CloneAttrs();
            attrs.Remove("edges");
            string[] own = ["structure", "rafterSection", "rafterSpacing", "purlins", "eaveOffset"];
            foreach (var (k, v) in Roof.Attrs(style, Roof.Contour(roof))) if (!own.Contains(k) || attrs[k] == null) attrs[k] = v?.DeepClone();
            if (style.Shape != "mansard") { attrs.Remove("upperPitch"); attrs.Remove("mansardInset"); }
            if (style.GutterColor.Length == 0) attrs.Remove("gutterColor");
            var next = roof.With(attrs: attrs);
            CheckRoof(ctx.Model, next);
            CheckRoofOpenings(ctx.Model, next);
            ctx.Replace(next);
            return null;
        });

        // The configurator's Apply, one step: the roof's fields and its per-edge rules
        // ("edges": one entry per edge, null for "as the roof"; null clears them all), its outline. Refused when it would not build.
        r.Command("updateRoof", "Change roof", (ctx, a) =>
        {
            var roof = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Roof);
            var d = ctx.Describe(IfcClass.Roof)!;
            var attrs = roof.CloneAttrs();
            foreach (var (k, v) in a["attrs"] as JsonObject ?? [])
            {
                var f = d.Fields.FirstOrDefault(x => x.Key == k && !x.ReadOnly) ?? (k is "lowEdge" or "gutterColor" ? new FieldDescriptor(k, k, k == "lowEdge" ? "number" : "color") : throw new CommandException($"„{k}” is not a roof field."));
                if (v == null) attrs.Remove(k); else attrs[k] = Fields.Check(f, v);
            }
            if (a["points"] != null || a["x1"] != null) attrs["contour"] = Slabs.ContourJson(Slabs.CheckContour(Points(a)));
            var corners = Slabs.Contour(roof.With(attrs: attrs)).Count;
            if (a.ContainsKey("edges"))
            {
                if (a["edges"] is JsonArray edges)
                {
                    if (edges.Count > corners) throw new CommandException($"The roof has {corners} edges, not {edges.Count}.");
                    var clean = new JsonArray();
                    foreach (var e in edges)
                    {
                        if (e is not JsonObject o) { clean.Add(null); continue; }
                        var c = new JsonObject();
                        if ((string?)o["kind"] is { } kind) c["kind"] = kind is "eave" or "gable" ? kind : throw new CommandException("An edge is an eave or a gable.");
                        if (Json.Number(o["pitch"]) is { } p) c["pitch"] = p;
                        if (Json.Number(o["overhang"]) is { } oh) c["overhang"] = oh;
                        clean.Add((JsonNode?)(c.Count > 0 ? c : null));
                    }
                    if (clean.Any(x => x != null)) attrs["edges"] = clean; else attrs.Remove("edges");
                }
                else attrs.Remove("edges");
            }
            // A roof shaped by hand is no longer exactly its style.
            if (a.ContainsKey("edges") || (a["attrs"] as JsonObject)?.Count > 0) attrs.Remove("style");
            var next = roof.With(attrs: attrs);
            CheckRoof(ctx.Model, next);
            ctx.Replace(next);
            return null;
        });

        r.Command("addDoor", "Door", (ctx, a) => AddFill(ctx, a, "door"));
        r.Command("addWindow", "Window", (ctx, a) => AddFill(ctx, a, "window"));

        r.Command("moveOpening", "Move opening", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Nothing to move.");
            Openings.Move(ctx, e, Json.Number(a["along"]), Json.Number(a["sill"]));
            return null;
        });

        r.Command("flipJoinery", "Flip", (ctx, a) =>
        {
            foreach (var id in Ids(a))
                if (ctx.Model.Get(id) is { Class: IfcClass.Door or IfcClass.Window } fill) Openings.Flip(ctx, fill, (string?)a["axis"] ?? "y");
            return null;
        });

        r.Command("createJoineryType", "New door or window type", (ctx, a) =>
        {
            var spec = JoinerySpec.From(a["spec"]) ?? throw new CommandException("The type's configuration is missing or unreadable.");
            return new JsonObject { ["id"] = Openings.CreateType(ctx, spec).Id };
        });

        r.Command("updateJoineryType", "Change door or window type", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.");
            Openings.UpdateType(ctx, type, JinerySpecOrThrow(a["spec"]));
            return null;
        });

        r.Command("setJoineryType", "Change type", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["typeId"] ?? "") is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.");
            foreach (var id in Ids(a))
                if (ctx.Model.Get(id) is { Class: IfcClass.Door or IfcClass.Window } fill) Openings.SetType(ctx, fill, type);
            return null;
        });

        r.Geometry(IfcClass.Wall, (wall, model, services) => Walls.Geometry(wall, model, services));
        r.PathElement(IfcClass.Wall, Walls.Frame);
        r.Quantities(IfcClass.Wall, (wall, geometry, model) => Walls.Quantities(wall, geometry, model));
        r.Catalog("defaultZones", new JsonArray(WallZones.Defaults("Brick").Select(z => (JsonNode)z).ToArray()));

        r.Command("createWallType", "New wall type", (ctx, a) =>
        {
            var type = WallTypes.Create(ctx, (string?)a["name"], WallTypes.ParseLayers(a["layers"]), (string?)a["mark"], a["zones"], a["lintels"]);
            return new JsonObject { ["id"] = type.Id };
        });

        r.Command("updateWallType", "Change wall type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.WallType);
            WallTypes.Update(ctx, type, (string?)a["name"], a["layers"] is null ? null : WallTypes.ParseLayers(a["layers"]), a["zones"], a["lintels"]);
            return null;
        });

        r.Command("addWall", "Wall", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var type = (string?)a["typeId"] is { } typeId ? Spatial.Require(ctx.Model, typeId, IfcClass.WallType) : null;
            var (p1, p2) = (P(a, "x1", "y1"), P(a, "x2", "y2"));
            var rise = Args.Rise(a, p1, p2);
            var unjoined = a["join"] is JsonValue j && j.TryGetValue<bool>(out var join) && !join;
            var wall = Walls.AddUnjoined(ctx, storeyId, p1, p2, Json.Number(a["height"]) ?? Walls.DefaultHeight, Json.Number(a["baseOffset"]) ?? 0, type);
            // A curved wall: the arc through its ends, rising off the chord (or through a third point), before it joins.
            if (rise != 0) { var attrs = wall.CloneAttrs(); attrs[PathCurve.Key] = rise; ctx.Replace(wall.With(attrs: attrs)); }
            if (!unjoined) Joins.Auto(ctx, Paths(ctx), wall.Id, Style(a["joinStyle"]) ?? JoinStyle.Butt);
            return new JsonObject { ["id"] = wall.Id };
        });

        r.Command("joinWalls", "Join walls", (ctx, a) =>
        {
            var w1 = Spatial.Require(ctx.Model, (string?)a["a"], IfcClass.Wall);
            var w2 = Spatial.Require(ctx.Model, (string?)a["b"], IfcClass.Wall);
            var priority = (string?)a["priority"] switch { "a" => 'a', "b" => 'b', _ => (char?)null };
            var join = Joins.Join(ctx, Paths(ctx), w1, w2, Style(a["style"]), priority);
            return new JsonObject { ["kind"] = join.Kind.ToString(), ["style"] = join.Style == JoinStyle.Mitre ? "mitre" : "butt" };
        });

        r.Command("unjoinWalls", "Unjoin walls", (ctx, a) =>
        {
            Joins.Unjoin(ctx, (string?)a["a"] ?? "", (string?)a["b"] ?? "");
            return null;
        });

        r.Command("restyleJoins", "Join style", (ctx, a) =>
        {
            var swap = a["swap"] is JsonValue v && v.TryGetValue<bool>(out var sw) && sw;
            var style = Style(a["style"]);
            if (style == null && !swap) throw new CommandException("Say how: a style (butt or mitre), or swap.");
            var n = Joins.Restyle(ctx, Paths(ctx), Ids(a), style, swap);
            if (n == 0) throw new CommandException("The selected walls have no corner to restyle.");
            return new JsonObject { ["corners"] = n };
        });

        // A wall's top under roofs: attached rises to the slopes' underside (from any storey), detached ignores roofs.
        r.Command("attachToRoof", "Attach to roof", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            var roofIds = a["roofIds"] is JsonArray rs ? rs.Select(x => (string?)x).OfType<string>().ToList() : [];
            foreach (var rid in roofIds) Spatial.Require(ctx.Model, rid, IfcClass.Roof);
            foreach (var id in ids)
            {
                var wall = Spatial.Require(ctx.Model, id, IfcClass.Wall);
                var attrs = wall.CloneAttrs();
                attrs["roofTop"] = "attach";
                if (roofIds.Count > 0) attrs["roofIds"] = new JsonArray(roofIds.Select(x => (JsonNode)x).ToArray()); else attrs.Remove("roofIds");
                ctx.Replace(wall.With(attrs: attrs));
            }
            return null;
        });
        r.Command("detachFromRoof", "Detach from roof", (ctx, a) =>
        {
            var mode = (string?)a["mode"] ?? "none";
            if (mode is not ("none" or "auto")) throw new CommandException("A detached wall either ignores roofs (none) or follows its storey's roof (auto).");
            foreach (var id in Args.Ids(a))
            {
                var wall = Spatial.Require(ctx.Model, id, IfcClass.Wall);
                var attrs = wall.CloneAttrs();
                attrs["roofTop"] = mode;
                attrs.Remove("roofIds");
                ctx.Replace(wall.With(attrs: attrs));
            }
            return null;
        });

        r.Command("updateWall", "Change wall", (ctx, a) =>
        {
            var wall = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Wall);
            var start = Walls.Start(wall);
            var end = Walls.End(wall);
            if (Json.Number(a["x1"]) is { } x1) start = start with { X = x1 };
            if (Json.Number(a["y1"]) is { } y1) start = start with { Y = y1 };
            if (Json.Number(a["x2"]) is { } x2) end = end with { X = x2 };
            if (Json.Number(a["y2"]) is { } y2) end = end with { Y = y2 };
            if ((end - start).Length < 1e-3) throw new CommandException("A wall needs two different points: it would have no length.");
            var height = Json.Number(a["height"]) ?? wall.Num("height", Walls.DefaultHeight);
            if (!(height > 0)) throw new CommandException("A wall's height must be positive.");
            var thickness = Json.Number(a["thickness"]) ?? wall.NumOrNull("thickness");
            if (thickness is <= 0) throw new CommandException("A wall's thickness must be positive.");
            var baseOffset = Json.Number(a["baseOffset"]) ?? wall.Num("baseOffset");
            var rise = a.ContainsKey(PathCurve.Key) || a["x3"] != null ? Args.Rise(a, start, end) : wall.NumOrNull(PathCurve.Key) ?? 0;
            // The openings must still fit the wall as it will be, along it and up it.
            var next = Walls.Attrs(start, end, height, baseOffset, thickness);
            if (rise != 0) next[PathCurve.Key] = rise;
            var after = wall.With(attrs: next);
            foreach (var o in Openings.PlacedOn(ctx.Model, wall.Id)) Openings.CheckFits(ctx.Model, after, o.Along, o.Sill, o.Width, o.Height, o.Opening.Id);
            // Only the wall's own fields change; what other modules wrote on it (its phase, its cut priority) stays.
            var attrs = wall.CloneAttrs();
            foreach (var (k, v) in Walls.Attrs(Walls.Start(wall), Walls.End(wall), height, baseOffset, thickness)) attrs[k] = v?.DeepClone();
            if (rise != 0) attrs[PathCurve.Key] = rise; else attrs.Remove(PathCurve.Key);
            ctx.Replace(wall.With(attrs: attrs));
            var moveJoined = a["moveJoinedEnds"] is not JsonValue mj || !mj.TryGetValue<bool>(out var keep) || keep;
            if ((start - Walls.Start(wall)).Length > 1e-12 || (end - Walls.End(wall)).Length > 1e-12)
                Joins.MoveEnds(ctx, Paths(ctx), ctx.Model.Get(wall.Id)!, start, end, moveJoined, a["detachJoinedEnds"] is JsonValue dj && dj.TryGetValue<bool>(out var detach) && detach);
            return null;
        });

        r.Command("setWallType", "Change type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["typeId"], IfcClass.WallType);
            foreach (var id in Ids(a)) Walls.SetType(ctx, Spatial.Require(ctx.Model, id, IfcClass.Wall), type);
            return null;
        });

        r.Command("flipWall", "Flip", (ctx, a) =>
        {
            var axis = (string?)a["axis"] ?? "y";
            foreach (var id in Ids(a)) Walls.Flip(ctx, Spatial.Require(ctx.Model, id, IfcClass.Wall), axis);
            return null;
        });
    }

    /// <summary>A contour: <c>points</c> as [[x, y], …], or the rectangle between (x1, y1) and (x2, y2).</summary>
    static void CheckRoof(IModelView m, Entity roof)
    {
        if (Roof.Resolve(roof, m) is { Flat: false } r && RoofSolver.Problem(r.Outline, r.Rules) is { } problem) throw new CommandException(problem);
    }

    static IReadOnlyList<Vec2> Points(JsonObject a) => Args.Points(a);

    /// <summary>A roof's openings written back; the one just set must fit (its reason when it does not).</summary>
    /// <summary>Refused when a roof window or dormer the roof already holds would not sit on the roof as it would become.</summary>
    static void CheckRoofOpenings(IModelView m, Entity next)
    {
        var openings = RoofOpenings.Of(next).ToList();
        if (openings.Count == 0) return;
        if (RoofTop.ShapeOf(next, m) is not { } shape) throw new CommandException("The roof holds roof windows or dormers, which a flat roof cannot take; remove them first.");
        var diags = new List<string>();
        var placed = RoofOpenings.Place(next, shape, m, diags);
        if (openings.FirstOrDefault(o => placed.All(p => p.Opening.Id != o.Id)) is { } lost)
            throw new CommandException(diags.FirstOrDefault(d => d.Contains($"„{lost.Name}”")) ?? $"„{lost.Name}” would no longer sit on the roof; move or remove it first.");
    }

    static void WriteRoofOpenings(ICommandContext ctx, Entity roof, JsonNode[] list, string checkId)
    {
        var attrs = roof.CloneAttrs();
        attrs["openings"] = new JsonArray(list);
        var next = roof.With(attrs: attrs);
        if (RoofTop.ShapeOf(next, ctx.Model) is not { } shape) throw new CommandException("A flat roof takes no roof windows or dormers.");
        var diags = new List<string>();
        var placed = RoofOpenings.Place(next, shape, ctx.Model, diags);
        if (placed.All(p => p.Opening.Id != checkId))
        {
            var name = RoofOpenings.Of(next).FirstOrDefault(o => o.Id == checkId)?.Name ?? "";
            throw new CommandException(diags.FirstOrDefault(d => d.Contains($"„{name}”")) ?? "The opening does not fit on the roof.");
        }
        ctx.Replace(next);
    }

    static JoinerySpec JinerySpecOrThrow(JsonNode? n) => JoinerySpec.From(n) ?? throw new CommandException("The type's configuration is missing or unreadable.");

    /// <summary>A door or window in the wall picked: where the click falls along it (or <c>along</c>), of the type given or the project's first.</summary>
    static JsonNode AddFill(ICommandContext ctx, JsonObject a, string kind)
    {
        var wall = Spatial.Require(ctx.Model, (string?)a["wallId"], IfcClass.Wall);
        var type = (string?)a["typeId"] is { } typeId
            ? ctx.Model.Get(typeId) is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.")
            : Openings.EnsureType(ctx, kind);
        var along = Json.Number(a["along"]) ?? Openings.AlongOf(wall, new Vec2(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0));
        var fill = Openings.AddFill(ctx, wall, type, along, Json.Number(a["sill"]));
        return new JsonObject { ["id"] = fill.Id };
    }

    static IEnumerable<QuantitySet> FillQuantities(Entity fill, IModelView m, string set)
    {
        if (Openings.SpecOf(m, fill.Id) is not { } spec) yield break;
        yield return new QuantitySet(set,
        [
            new("Width", spec.Width, "LENGTH"),
            new("Height", spec.Height, "LENGTH"),
            new("Area", spec.Width * spec.Height, "AREA"),
        ]);
    }

    static IPathElements Paths(ICommandContext ctx) => ctx.Services.Get<IPathElements>()!;

    static JoinStyle? Style(JsonNode? n) => Args.JoinStyle(n);

    static Vec2 P(JsonObject a, string x, string y) => Args.Point(a, x, y);

    static IEnumerable<string> Ids(JsonObject a) => Args.Ids(a);
}

/// <summary>Heights over the storey: a wall's base, a floor's top, a roof's wall plate (over the storey's top).</summary>
sealed class Levels : IStoreyLevels
{
    public IEnumerable<(string Key, bool FromTop)> LevelsOf(Entity e) => e.Class switch
    {
        IfcClass.Wall => [("baseOffset", false)],
        IfcClass.Slab => [("offset", false)],
        IfcClass.Roof => [("eaveOffset", true)],
        _ => [],
    };
}

/// <summary>The bodies of the openings in walls and floors, for the IFC export (IfcOpeningElement).</summary>
sealed class VoidShapes : IVoidShapes
{
    public Solid? Shape(Entity opening, IModelView m)
    {
        if (m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is not { } host) return null;
        return host.Class switch
        {
            IfcClass.Wall => Openings.CutterIn(m, host, Openings.Place(m, opening)),
            IfcClass.Slab => Slabs.OpeningSolid(m, host, opening),
            _ => null,
        };
    }
}

/// <summary>Roofs in the cut order: their body cut by higher priorities, their covering cutting lower ones.</summary>
static class RoofCuts
{
    /// <summary>A roof's body, cut by what has a higher cut priority.</summary>
    public static ElementGeometry? Geometry(Entity roof, IModelView m, IServices? services)
    {
        var g = Roof.Geometry(roof, m);
        if (g == null || g.Parts.Count == 0 || services == null) return g;
        var problems = new List<string>();
        var parts = CutPriorities.Apply(roof, m, services, g.Parts.ToList(), problems);
        return g with { Parts = parts, Diagnostics = problems.Count > 0 ? [.. g.Diagnostics ?? [], .. problems] : g.Diagnostics };
    }

    /// <summary>A roof's covering as what it cuts with, solved once per model revision (every wall around asks for it).</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Entity, Tuple<long, Solid?>> RoofEnvelopes = new();
    public static Solid? Envelope(Entity roof, IModelView m)
    {
        if (RoofEnvelopes.TryGetValue(roof, out var hit) && hit.Item1 == m.Revision) return hit.Item2;
        var covering = Roof.Geometry(roof, m)?.Parts.FirstOrDefault(p => p.Key == "covering")?.Solid;
        RoofEnvelopes.AddOrUpdate(roof, Tuple.Create(m.Revision, covering));
        return covering;
    }
}
