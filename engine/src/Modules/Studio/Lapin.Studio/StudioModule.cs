using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Studio (the old Lapin's, grown into a geometry editor: docs/plan-studio-editor.md):
/// solids from sketches drawn on a plan, a section, an elevation or a work
/// plane (any face picked in 3D), an extrusion (straight, symmetric, tapered,
/// or up to a face), a sweep along a path (twisted, scaled, rising into a
/// helix), a loft, a revolution, or a primitive placed with a click; each
/// rebuilt whenever its sketch changes, then the steps it was given done in
/// order (push/pull, chamfer, fillet, shell, slice, arrays, 3D placement:
/// <see cref="Features"/>); and live booleans between any elements
/// (IfcRelInterferesElements): a target less its tools, or united or
/// intersected with them (the tools then hidden, kept editable).
/// </summary>
public sealed partial class StudioModule : ILapinModule, IGeometryFilter
{
    public string Id => "studio";
    public string Name => "Studio";

    /// <summary>The classes a Studio solid can be (those no other module builds); others, the stair among them since architecture builds stairs, are written as a proxy with <c>exportAs</c>.</summary>
    public static readonly string[] Classes =
        [IfcClass.BuildingElementProxy, "IfcMember", "IfcPlate", "IfcCovering", "IfcRailing", "IfcRamp", "IfcChimney", "IfcFurnishingElement"];

    /// <summary>A Studio solid's type (shared by the solids that take it); exported as the type of the solid's IFC class (IfcMemberType, IfcSlabType…).</summary>
    public const string TypeClass = "LapinSolidType";

    /// <summary>What a Studio solid can be, as the inspector offers it: its IFC class (written as itself, or as a proxy that exports as it) and that class's predefined types (IFC4).</summary>
    public static readonly (string Class, string Label, string[] Predefined)[] Offered =
    [
        (IfcClass.BuildingElementProxy, "Proxy (generic)", ["COMPLEX", "ELEMENT", "PARTIAL", "PROVISIONFORVOID", "PROVISIONFORSPACE"]),
        ("IfcMember", "Member", ["BRACE", "CHORD", "COLLAR", "MEMBER", "MULLION", "PLATE", "POST", "PURLIN", "RAFTER", "STRINGER", "STRUT", "STUD"]),
        ("IfcPlate", "Plate", ["CURTAIN_PANEL", "SHEET"]),
        ("IfcCovering", "Covering", ["CEILING", "FLOORING", "CLADDING", "ROOFING", "MOLDING", "SKIRTINGBOARD", "INSULATION", "MEMBRANE", "SLEEVING", "WRAPPING"]),
        ("IfcRailing", "Railing", ["HANDRAIL", "GUARDRAIL", "BALUSTRADE"]),
        ("IfcStair", "Stair", ["STRAIGHT_RUN_STAIR", "TWO_STRAIGHT_RUN_STAIR", "QUARTER_WINDING_STAIR", "QUARTER_TURN_STAIR", "HALF_WINDING_STAIR", "HALF_TURN_STAIR", "SPIRAL_STAIR", "CURVED_RUN_STAIR"]),
        ("IfcRamp", "Ramp", ["STRAIGHT_RUN_RAMP", "TWO_STRAIGHT_RUN_RAMP", "QUARTER_TURN_RAMP", "TWO_QUARTER_TURN_RAMP", "HALF_TURN_RAMP", "SPIRAL_RAMP"]),
        ("IfcChimney", "Chimney", []),
        ("IfcFurnishingElement", "Furniture", []),
        (IfcClass.Wall, "Wall", ["MOVABLE", "PARAPET", "PARTITIONING", "PLUMBINGWALL", "SHEAR", "SOLIDWALL", "STANDARD", "POLYGONAL", "ELEMENTEDWALL"]),
        (IfcClass.Slab, "Slab", ["FLOOR", "ROOF", "LANDING", "BASESLAB", "PAVING"]),
        (IfcClass.Column, "Column", ["COLUMN", "PILASTER"]),
        (IfcClass.Beam, "Beam", ["BEAM", "JOIST", "HOLLOWCORE", "LINTEL", "SPANDREL", "T_BEAM"]),
        (IfcClass.Roof, "Roof", ["FLAT_ROOF", "SHED_ROOF", "GABLE_ROOF", "HIP_ROOF", "MANSARD_ROOF", "DOME_ROOF", "FREEFORM"]),
        (IfcClass.Footing, "Footing", ["CAISSON_FOUNDATION", "FOOTING_BEAM", "PAD_FOOTING", "PILE_CAP", "STRIP_FOOTING"]),
    ];

    /// <summary>The words EXTRUDE takes for a class, as in the old Lapin.</summary>
    public static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PROXY"] = IfcClass.BuildingElementProxy, ["MEMBER"] = "IfcMember", ["PLATE"] = "IfcPlate", ["COVERING"] = "IfcCovering",
        ["RAILING"] = "IfcRailing", ["STAIR"] = "IfcStair", ["RAMP"] = "IfcRamp", ["CHIMNEY"] = "IfcChimney", ["FURNITURE"] = "IfcFurnishingElement",
        ["WALL"] = IfcClass.Wall, ["SLAB"] = IfcClass.Slab, ["FLOOR"] = IfcClass.Slab, ["COLUMN"] = IfcClass.Column, ["BEAM"] = IfcClass.Beam,
        ["ROOF"] = IfcClass.Roof, ["FOOTING"] = IfcClass.Footing,
    };

    /// <summary>
    /// The operations a solid is made by: from a sketch (extrude, sweep, revolve, loft), without one (a primitive;
    /// a prism, a face pushed or pulled on an element Studio does not build; a body, an element's or a mesh file's
    /// triangles made into a Studio solid).
    /// </summary>
    public static readonly string[] Operations = ["extrude", "sweep", "revolve", "loft", "primitive", "prism", "body"];

    public void Register(IModuleRegistry r)
    {
        foreach (var cls in Classes)
        {
            r.Class(new(cls, cls == IfcClass.BuildingElementProxy ? "Solid" : Offered.FirstOrDefault(o => o.Class == cls).Label ?? cls[3..],
            [
                new("depth", "Depth", "length"),
                new("angle", "Angle (°)", "number", Min: -360, Max: 360),
                new("offset", "Offset from the sketch", "length"),
                new("place", "Profile on the path", "choice", Choices: Solids.Places),
                new("placeOffset", "Gap from the path", "length"),
                new("symmetric", "Both ways from the sketch", "bool"),
                new("taper", "Taper (°)", "number", Min: -89, Max: 89),
                new("twist", "Twist along the path (°)", "number", Min: -3600, Max: 3600),
                new("scaleEnd", "Scale at the end", "number", Min: 0.01, Max: 100),
                new("rise", "Rise along the path", "length"),
                new("turns", "Turns (a rising ring)", "number", Min: 0.1, Max: 100),
                new("smooth", "Smooth through the profiles", "bool"),
                new("shape", "Primitive", "choice", Choices: Solids.Primitives, ReadOnly: true),
                new("width", "Width", "length", Min: 0),
                new("length", "Length", "length", Min: 0),
                new("radius", "Radius", "length", Min: 0),
                new("radius2", "Second radius", "length", Min: 0),
                new("rotation", "Rotation (°)", "number", Min: -360, Max: 360),
                new("cutPriority", "Cut priority", "number", Min: 0, Max: 100),
                new("exportAs", "Export as", "text"),
                // Rings, boxes and wall strips (Thick).
                new("thickness", "Thickness", "length", Min: 0.001),
                new("height", "Height (negative: away)", "length"),
                new("justify", "Thickness lies", "choice", Choices: Thick.Justifications),
                new("bottom", "Bottom", "bool"),
                new("bottomThickness", "Bottom thickness", "length", Min: 0.001),
                new("top", "Top", "bool"),
                new("topThickness", "Top thickness", "length", Min: 0.001),
            ], "#b9b2a6"));
            r.Geometry(cls, Geometry);
            r.Quantities(cls, Quantities);
            // Studio solids give way to the building's elements; furniture to everything.
            r.Cutter(cls, cls == "IfcFurnishingElement" ? 10 : 40, (e, m) => !IsVoid(e) && Geometry(e, m, null)?.Parts is [{ } only] ? only.Solid : null);
        }
        r.Class(new(TypeClass, "Solid type", [new("description", "Description", "text")], "#b9b2a6", Kind: "type"));
        // A box's walls with its bottom and top: an assembly (IfcElementAssembly) of separate elements, without a body of its own.
        r.Class(new(Boxes.AssemblyClass, "Assembly", [], "#b9b2a6"));
        r.Service<IGeometryFilter>(this);
        r.Service<IElementPlacement>(new StudioPlacement());
        // An import's references made editable on request (the import's third way, docs/spec-ifc.md).
        r.Service<IReferenceEditor>(new ReferenceSolids());
        r.Service<IElementUpdate>(new Boxes());
        r.Service<IDeletion>(new Boxes());

        r.Command("extrude", "Extrude", (ctx, a) => Create(ctx, a, "extrude"));
        r.Command("sweep", "Sweep", (ctx, a) => Create(ctx, a, "sweep"));
        r.Command("revolve", "Revolve", (ctx, a) => Create(ctx, a, "revolve"));
        r.Command("loft", "Loft", (ctx, a) => Create(ctx, a, "loft"));
        // Thick solids from a drafted shape: {profileId, thickness, height, justify, class?; a box's bottom, top and their thicknesses}.
        r.Command("ring", "Ring", (ctx, a) => CreateThick(ctx, a, Thick.Ring));
        r.Command("box", "Box", (ctx, a) => CreateThick(ctx, a, Thick.Box));
        r.Command("wall", "Wall strip", (ctx, a) => CreateThick(ctx, a, Thick.Wall));
        r.Command("primitive", "Primitive", Primitive);
        RegisterEdits(r);

        // A solid's class (and its predefined type) changed: written again under the new class, same id, same sketch, same type and booleans.
        r.Command("setClass", "Change class", (ctx, a) =>
        {
            foreach (var id in a["ids"] is JsonArray ? Args.Ids(a) : [(string?)a["id"] ?? ""])
            {
                var e = Solid(ctx.Model, id);
                var (cls, exportAs) = a.ContainsKey("class") ? ClassOf((string?)a["class"]) : (e.Class, e.Str("exportAs"));
                var attrs = e.CloneAttrs();
                if (exportAs != null) attrs["exportAs"] = exportAs; else attrs.Remove("exportAs");
                // A new class drops a predefined type it does not have, unless one is given.
                var pre = a.ContainsKey("predefinedType") ? ((string?)a["predefinedType"])?.Trim().ToUpperInvariant() is { Length: > 0 } p ? p : null
                    : (exportAs ?? cls) == (e.Str("exportAs") ?? e.Class) ? e.PredefinedType : null;
                if (pre != null && !System.Text.RegularExpressions.Regex.IsMatch(pre, "^[A-Z0-9_]+$")) throw new CommandException($"„{pre}” is not a predefined type (capitals, digits and _).");
                var storey = Spatial.StoreyOf(ctx.Model, e.Id)?.Id;
                var keep = ctx.Model.Relations.Where(x => x.Touches(e.Id) && x.Class is IfcClass.RelInterferesElements or IfcClass.RelDefinesByType or IfcClass.RelAggregates).ToList();
                ctx.Remove(e.Id);
                ctx.Add(new Entity(e.Id, cls, e.Name, attrs, pre));
                if (storey != null) Spatial.Contain(ctx, storey, e.Id);
                foreach (var x in keep)
                {
                    if (x.Class is IfcClass.RelDefinesByType or IfcClass.RelAggregates) { if (x.Relating != e.Id && ctx.Model.Get(x.Relating) != null) ctx.Relate(x.Class, x.Relating, e.Id); continue; }
                    if (ctx.Model.Get(x.Relating) != null && x.Related.All(r2 => ctx.Model.Get(r2) != null)) ctx.AddRelation(x with { Id = ctx.NewId() });
                }
            }
            return null;
        });

        // Solids given a type (an existing one, or a new one by name), or none.
        r.Command("setType", "Solid type", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            Entity? type = null;
            if ((string?)a["typeId"] is { } tid) type = Spatial.Require(ctx.Model, tid, TypeClass);
            else if (((string?)a["name"])?.Trim() is { Length: > 0 } name)
            {
                type = ctx.Model.OfClass(TypeClass).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
                if (type == null) { type = new Entity(ctx.NewId(), TypeClass, name, new JsonObject()); ctx.Add(type); }
            }
            foreach (var id in ids)
            {
                Solid(ctx.Model, id);
                ctx.Unrelate(IfcClass.RelDefinesByType, id);
                if (type != null) ctx.Relate(IfcClass.RelDefinesByType, type.Id, id);
            }
            return type == null ? null : new JsonObject { ["id"] = type.Id };
        });

        r.Query("studio.classes", (_, _) => new JsonArray(Offered.Select(o => (JsonNode)new JsonObject
        {
            ["class"] = o.Class, ["label"] = o.Label, ["predefined"] = new JsonArray(o.Predefined.Select(p => (JsonNode)p).ToArray()),
        }).ToArray()));

        // ------------------------------------------------------------ booleans

        // The first is the target, the others its tools: subtract, union or intersect, live.
        r.Command("boolean", "Boolean", (ctx, a) =>
        {
            var op = ((string?)a["op"])?.ToLowerInvariant() switch { "subtract" => "Subtract", "union" => "Union", "intersect" or "intersection" => "Intersection", var x => throw new CommandException($"„{x}” is not subtract, union or intersect.") };
            var target = ctx.Model.Get((string?)a["target"] ?? "") ?? throw new CommandException("Select the target first, then the elements to combine with it.");
            var tools = (a["tools"] as JsonArray)?.Select(x => (string?)x).OfType<string>().Distinct().Where(t => t != target.Id).ToList() ?? [];
            if (tools.Count == 0) throw new CommandException("Select the target first, then the elements to combine with it.");
            var rels = Rels(ctx.Model).ToList();
            if (op != "Subtract" && rels.Any(x => x.Related.Contains(target.Id) && TypeOf(x) != "Subtract")) throw new CommandException("The target is already another element's operand.");
            var added = 0;
            foreach (var t in tools)
            {
                if (ctx.Model.Get(t) is not { } tool) throw new CommandException($"There is no {t}.");
                if (ctx.Model.Get(t) is null || Spatial.StoreyOf(ctx.Model, t) == null) throw new CommandException($"„{tool.Name}” is not an element.");
                if (op != "Subtract" && rels.Any(x => x.Related.Contains(t) && TypeOf(x) != "Subtract")) throw new CommandException($"„{tool.Name}” already takes part in a union or an intersection.");
                if (rels.Any(x => x.Relating == target.Id && x.Related.Contains(t)))
                {
                    if (op == "Subtract" && rels.Any(x => x.Relating == target.Id && x.Related.Contains(t) && TypeOf(x) == "Subtract")) continue;
                    throw new CommandException($"„{tool.Name}” already works on the target.");
                }
                if (rels.Any(x => x.Relating == t && x.Related.Contains(target.Id))) throw new CommandException($"„{tool.Name}” already works on the target.");
                ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelInterferesElements, target.Id, [t], new JsonObject { ["interferenceType"] = op, ["name"] = $"Lapin {op.ToLowerInvariant()}" }));
                added++;
            }
            return new JsonObject { ["added"] = added };
        });

        // The operations the selected elements take part in (as target or tool) removed: each is whole again.
        r.Command("removeBoolean", "Remove operation", (ctx, a) =>
        {
            var ids = (a["ids"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
            var gone = Rels(ctx.Model).Where(x => ids.Contains(x.Relating) || x.Related.Any(ids.Contains)).ToList();
            foreach (var x in gone) ctx.RemoveRelation(x.Id);
            return new JsonObject { ["removed"] = gone.Count };
        });

        // A union's or intersection's tools shown for editing, or hidden again.
        r.Command("operands", "Show operands", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Select a united or intersected element.");
            if (!Rels(ctx.Model).Any(x => x.Relating == e.Id && TypeOf(x) != "Subtract")) throw new CommandException("Select a united or intersected element: its operands are shown for editing, or hidden again.");
            var attrs = e.CloneAttrs();
            var show = a["show"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : !(e.Attrs["showOperands"] is JsonValue sv && sv.TryGetValue<bool>(out var sb) && sb);
            if (show) attrs["showOperands"] = true; else attrs.Remove("showOperands");
            ctx.Replace(e.With(attrs: attrs));
            return new JsonObject { ["shown"] = show };
        });

        // What the properties card shows of a Studio solid: its operation, sketch and steps.
        r.Query("studio.feature", (q, a) =>
        {
            if (q.Model.Get((string?)a["id"] ?? "") is not { } e || e.Str("operation") is not { } op) return null;
            // A box's bottom or top: its box's sketch.
            var source = op == Thick.BoxSlab ? q.Model.Get(e.Str("box") ?? "") : e;
            var profile = q.Model.Get(source?.Str("profile") ?? "");
            var view = profile != null ? Views.Find(q.Model, profile.Str("viewId") ?? "") : null;
            var outline = profile != null ? Sketches.OutlineOf(q.Model, profile) : null;
            var holes = profile != null ? Sketches.HoleOutlines(q.Model, profile, ProfileIds(q.Model)) : [];
            var sketch = (outline?.Ids ?? (profile != null ? [profile.Id] : [])).Concat(new[] { e.Str("guide"), e.Str("endProfile"), e.Str("rail") }.OfType<string>())
                .Concat(holes.SelectMany(h => h.Ids)).Distinct().Select(x => (JsonNode)x).ToArray();
            var steps = new JsonArray();
            foreach (var f in (e.Attrs["features"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var step = (JsonObject)f.DeepClone();
                step["label"] = Features.Label(f);
                steps.Add((JsonNode)step);
            }
            var built = q.Geometry(e.Id);
            return new JsonObject
            {
                ["operation"] = op, ["view"] = view?.Name, ["viewId"] = view?.Id, ["viewKind"] = view?.Str("kind"), ["profile"] = profile?.Id, ["holes"] = holes.Count,
                ["sketch"] = new JsonArray(sketch),
                ["area"] = outline == null ? null : Math.Abs(Polygon2.SignedArea(outline.Loop)) - holes.Sum(h => Math.Abs(Polygon2.SignedArea(h.Loop))),
                ["guide"] = e.Str("guide") is { } g ? (q.Model.Get(g) is { } ge ? ge.Name : "missing") : null,
                ["upTo"] = e.Attrs["upTo"] is JsonObject ut && q.Model.Get((string?)ut["id"] ?? "") is { } target ? target.Name : e.Attrs["upTo"] is JsonObject ? "missing" : null,
                ["features"] = steps,
                ["volume"] = built?.Parts.Sum(pt => Measure.Volume(pt.Solid)),
                ["problems"] = built?.Diagnostics is { Count: > 0 } d ? new JsonArray(d.Select(x => (JsonNode)x).ToArray()) : null,
            };
        });
    }

    // ------------------------------------------------------------ solids

    static Entity Solid(IModelView m, string? id) =>
        m.Get(id ?? "") is { } e && e.Str("operation") != null ? e : throw new CommandException("Not a Studio solid.");

    static (string Class, string? ExportAs) ClassOf(string? word)
    {
        if (string.IsNullOrWhiteSpace(word)) return (IfcClass.BuildingElementProxy, null);
        var cls = Words.TryGetValue(word.Trim(), out var c) ? c : word.Trim().StartsWith("Ifc", StringComparison.OrdinalIgnoreCase) ? word.Trim() : throw new CommandException($"„{word}” is not a class Studio knows.");
        var known = Classes.FirstOrDefault(x => string.Equals(x, cls, StringComparison.OrdinalIgnoreCase));
        return known != null ? (known, null) : (IfcClass.BuildingElementProxy, cls);
    }

    /// <summary>Every drafted shape a solid uses as a profile: its own, the one it ends with, a loft's.</summary>
    static HashSet<string> ProfileIds(IModelView m) =>
        Classes.SelectMany(m.OfClass).SelectMany(e => new[] { e.Str("profile"), e.Str("endProfile") }
            .Concat((e.Attrs["profiles"] as JsonArray)?.Select(x => (string?)x) ?? [])).OfType<string>().ToHashSet();

    static Entity ClosedDraft(IModelView m, string? id, string what)
    {
        var d = Drafts.Require(m, id, editable: false);
        if (Sketches.OutlineOf(m, d) is not { } outline) throw new CommandException($"The {what} must be closed: a closed polyline, a circle, or lines and arcs meeting end to end.");
        if (outline.Ids.Any(ProfileIds(m).Contains)) throw new CommandException($"That outline is a solid's profile already.");
        return d;
    }

    /// <summary>The plane a drafted shape lies on, for a new solid: an elevation drawn on for the first time takes a plane of its own here.</summary>
    static SketchPlane PlaneFor(ICommandContext ctx, Entity draft)
    {
        var view = Views.Find(ctx.Model, draft.Str("viewId") ?? "") ?? throw new CommandException("The sketch's view is gone.");
        if (view.Str("kind") == "elevation" && Views.ElevationPlane(view) == null) view = Views.FixElevationPlane(ctx, view);
        return Sketches.PlaneOf(ctx.Model, view, ctx.Services, Sketches.MidX(Drafts.ShapeOf(draft)))
            ?? throw new CommandException("Solids are sketched on a plan, a section, an elevation or a reference plane.");
    }

    static JsonNode? CreateThick(ICommandContext ctx, JsonObject a, string op)
    {
        var profile = Drafts.Require(ctx.Model, (string?)a["profileId"], editable: false);
        var shape = Drafts.ShapeOf(profile);
        if (Thick.ShapeProblem(op, shape) is { } bad) throw new CommandException(bad);
        if (ProfileIds(ctx.Model).Contains(profile.Id)) throw new CommandException("That shape is a solid's sketch already.");
        var thickness = Json.Number(a["thickness"]) ?? 0.2;
        var height = Json.Number(a["height"]) ?? 3;
        if (!(thickness > 0)) throw new CommandException("The thickness must be positive.");
        if (Math.Abs(height) < 1e-6) throw new CommandException("The solid needs a height (negative: away from you).");
        var justify = (string?)a["justify"] ?? Thick.DefaultJustify(op);
        if (Thick.JustifyProblem(op, justify) is { } jp) throw new CommandException(jp);
        if (Thick.Build(shape!, thickness, justify).Problem is { } problem) throw new CommandException(problem);
        var plane = PlaneFor(ctx, profile);
        var (cls, exportAs) = ClassOf((string?)a["class"] ?? "WALL");
        var attrs = new JsonObject { ["operation"] = op, ["profile"] = profile.Id, ["thickness"] = thickness, ["height"] = height, ["justify"] = justify };
        if (exportAs != null) attrs["exportAs"] = exportAs;
        if (Json.Number(a["offset"]) is { } off) attrs["offset"] = off;
        if (op == Thick.Box)
        {
            static bool B(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
            attrs["bottom"] = B(a["bottom"]);
            attrs["top"] = B(a["top"]);
            attrs["bottomThickness"] = Json.Number(a["bottomThickness"]) ?? thickness;
            attrs["topThickness"] = Json.Number(a["topThickness"]) ?? thickness;
            if ((bool)attrs["bottom"]! || (bool)attrs["top"]!) Boxes.CheckSlabs(height, attrs);
        }
        var word = op == Thick.Wall ? "Wall strip" : char.ToUpperInvariant(op[0]) + op[1..];
        var taken = ctx.Model.Entities.Where(x => x.Str("operation") == op).Select(x => x.Name).ToHashSet();
        var k = 1;
        while (taken.Contains($"{word} {k}")) k++;
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"{word} {k}";
        var e = new Entity(ctx.NewId(), cls, name, attrs);
        ctx.Add(e);
        var storey = (string?)a["storeyId"] ?? plane.StoreyId ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        Spatial.Contain(ctx, storey, e.Id);
        if (op == Thick.Box) Boxes.Reconcile(ctx, ctx.Model.Get(e.Id)!);
        var o = new JsonObject { ["id"] = e.Id };
        if (op == Thick.Box)
        {
            o["slabs"] = new JsonArray(Boxes.SlabsOf(ctx.Model, e.Id).Select(s => (JsonNode)s.Id).ToArray());
            if (Boxes.AssemblyOf(ctx.Model, e.Id) is { } asm) o["assembly"] = asm.Id;
        }
        return o;
    }

    /// <summary>The plane the solid's sketch lies on now, or why there is none.</summary>
    static (SketchPlane? Plane, string? Problem) PlaneOfSketch(IModelView m, IServices? services, Entity draft)
    {
        var view = Views.Find(m, draft.Str("viewId") ?? "");
        if (view == null) return (null, "The solid's sketch view is gone.");
        var plane = Sketches.PlaneOf(m, view, services, Sketches.MidX(Drafts.ShapeOf(draft)));
        return plane == null ? (null, view.Str("kind") == "elevation" ? "The elevation the solid is sketched on has no sketch plane: give it one (its Sketch plane depth)." : "The solid's sketch view lies on no plane.") : (plane, null);
    }

    /// <summary>A ring, box, wall strip or a box's bottom or top: the band of the shape, extruded along the plane's normal.</summary>
    static ElementGeometry? ThickGeometry(Entity e, IModelView m, IServices? services)
    {
        ElementGeometry Fail(string why) => new([], Diagnostics: [why]);
        var op = e.Str("operation")!;
        var owner = op == Thick.BoxSlab ? m.Get(e.Str("box") ?? "") : e;
        if (owner == null) return Fail("The box this bottom or top belongs to is gone.");
        if (m.Get(owner.Str("profile") ?? "") is not { } profile || Drafts.ShapeOf(profile) is not { } shape) return Fail("The solid's sketch is gone: draw its shape again.");
        if (Thick.ShapeProblem(owner.Str("operation")!, shape) is { } bad) return Fail(bad);
        var (plane, problem) = PlaneOfSketch(m, services, profile);
        if (plane == null) return Fail(problem!);
        var t = owner.NumOrNull("thickness") ?? 0.2;
        var h = owner.NumOrNull("height") ?? 3;
        var band = Thick.Build(shape, t, owner.Str("justify") ?? Thick.DefaultJustify(owner.Str("operation")!));
        if (band.Problem != null) return Fail(band.Problem);
        var shift = plane.Normal * owner.Num("offset");
        List<Vec3> W(IReadOnlyList<Vec2> l, double up = 0) => l.Select(p => plane.World(p) + shift + plane.Normal * up).ToList();
        Solid? solid;
        IReadOnlyList<Vec2> outline;
        IReadOnlyList<IReadOnlyList<Vec2>> holes;
        if (op == Thick.BoxSlab)
        {
            if (band.Inner == null) return Fail("The box has no inside for a bottom or top.");
            var bottom = e.Str("side") != "top";
            var st = owner.NumOrNull(bottom ? "bottomThickness" : "topThickness") ?? t;
            var sign = Math.Sign(h);
            if (!(st > 0) || st >= Math.Abs(h)) return Fail($"The {(bottom ? "bottom" : "top")} ({st:0.###} m) is as thick as the box is high.");
            solid = Solids.Extrude(W(band.Inner, bottom ? 0 : h - sign * st), [], plane.Normal * (sign * st));
            (outline, holes) = (band.Inner, []);
        }
        else
        {
            solid = Solids.Extrude(W(band.Outer), band.Holes.Select(x => (IReadOnlyList<Vec3>)W(x)).ToList(), plane.Normal * h);
            (outline, holes) = (band.Outer, band.Holes);
        }
        if (solid == null) return Fail("The solid could not be built from its sketch.");
        var plan = new List<PlanItem>();
        if (Math.Abs(plane.Normal.Z) > 0.999)
        {
            plan.Add(new PlanItem("outline", true, outline));
            plan.AddRange(holes.Select(x => new PlanItem("outline", true, x)));
        }
        else if (Polygon2.ConvexHull(solid.Vertices.Select(v => new Vec2(v.X, v.Y))) is { Length: >= 3 } hull) plan.Add(new PlanItem("outline", true, hull));
        var material = e.Str("material");
        var parts = new List<GeometryPart> { new(e.Name, material != null ? Materials.ColorOf(m, material) : "#b9b2a6", solid, material, "body") };
        var problems = new List<string>();
        parts = CutPriorities.Apply(e, m, services, parts, problems);
        return new ElementGeometry(parts, plan, plan.SelectMany(p => p.Points).ToList(), problems.Count > 0 ? problems : null);
    }

    /// <summary>
    /// A solid measured as the class it is written as: always its volume and
    /// surface; a member, beam or column its length (along its path, or its
    /// depth) and section; a slab, plate or covering its area and thickness;
    /// a wall its length, height and width; a railing its length.
    /// </summary>
    static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var cls = e.Str("exportAs") ?? e.Class;
        var volume = g.Parts.Sum(p => Measure.Volume(p.Solid));
        var list = new List<Quantity>
        {
            new("NetVolume", volume, "VOLUME", "Net volume"),
            new("GrossSurfaceArea", g.Parts.Sum(p => Measure.SurfaceArea(p.Solid)), "AREA", "Surface area"),
        };
        var (section, along, width, height) = Measure2(e, m);
        switch (cls)
        {
            case "IfcMember" or IfcClass.Beam or IfcClass.Column or IfcClass.Footing:
                if (along > 0) list.Add(new("Length", along, "LENGTH", "Length"));
                if (section > 0) list.Add(new("CrossSectionArea", section, "AREA", "Section area"));
                break;
            case IfcClass.Slab or "IfcPlate" or "IfcCovering" or IfcClass.Roof:
            {
                var thick = e.Str("operation") switch
                {
                    "extrude" => Math.Abs(e.Num("depth")),
                    Thick.BoxSlab => SlabThickness(e, m),
                    _ => height,
                };
                if (thick > 0) list.Add(new("Width", thick, "LENGTH", "Thickness"));
                if (thick > 0) list.Add(new("NetArea", volume / thick, "AREA", "Area"));
                break;
            }
            case IfcClass.Wall:
                if (along > 0) list.Add(new("Length", along, "LENGTH", "Length"));
                if (height > 0) list.Add(new("Height", height, "LENGTH", "Height"));
                if (width > 0) list.Add(new("Width", width, "LENGTH", "Width"));
                break;
            case "IfcRailing":
                if (along > 0) list.Add(new("Length", along, "LENGTH", "Length"));
                break;
        }
        yield return new QuantitySet($"Qto_{(cls.StartsWith("Ifc") ? cls[3..] : cls)}BaseQuantities", list);
    }

    /// <summary>A solid's section area, its run (path length, or extrusion depth), and its profile's width and height.</summary>
    /// <summary>A box's bottom or top: how thick its box says it is.</summary>
    static double SlabThickness(Entity e, IModelView m) =>
        m.Get(e.Str("box") ?? "") is { } box ? box.NumOrNull(e.Str("side") == "top" ? "topThickness" : "bottomThickness") ?? box.NumOrNull("thickness") ?? 0.2 : 0;

    static (double Section, double Along, double Width, double Height) Measure2(Entity e, IModelView m)
    {
        // A ring, box or wall strip: along its middle line, its thickness wide, its height high, its band the section.
        if (Thick.Is(e.Str("operation")))
        {
            if (m.Get(e.Str("profile") ?? "") is not { } d || Drafts.ShapeOf(d) is not { } s) return (0, 0, 0, 0);
            var t = e.NumOrNull("thickness") ?? 0.2;
            var band = Thick.Build(s, t, e.Str("justify") ?? Thick.DefaultJustify(e.Str("operation")!));
            return band.Problem != null ? (0, 0, 0, 0) : (t * Math.Abs(e.NumOrNull("height") ?? 3), band.Length, t, Math.Abs(e.NumOrNull("height") ?? 3));
        }
        if (e.Str("operation") == Thick.BoxSlab) return (0, 0, 0, SlabThickness(e, m));
        var profile = m.Get(e.Str("profile") ?? "");
        if (e.Str("operation") == "primitive")
            return (0, e.Num("height"), Math.Max(e.Num("width"), 2 * e.Num("radius")), e.Num("height"));
        var outer = profile != null ? Sketches.OutlineOf(m, profile)?.Loop : null;
        if (outer == null) return (0, 0, 0, 0);
        var holes = Sketches.Holes(m, profile!, ProfileIds(m));
        var section = Math.Abs(Polygon2.SignedArea(outer)) - holes.Sum(h => Math.Abs(Polygon2.SignedArea(h.Loop)));
        var (w, h) = (outer.Max(p => p.X) - outer.Min(p => p.X), outer.Max(p => p.Y) - outer.Min(p => p.Y));
        var along = e.Str("operation") switch
        {
            "extrude" => Math.Abs(e.Num("depth")),
            "sweep" when m.Get(e.Str("guide") ?? "") is { } gd && Drafts.ShapeOf(gd) is { } gs => Geo.Polylines(gs)[0] is var pl ? pl.Zip(pl.Skip(1), (p, q) => (q - p).Length).Sum() : 0,
            _ => 0,
        };
        return (section, along, w, h);
    }

    // ------------------------------------------------------------ live booleans

    static IEnumerable<Relation> Rels(IModelView m) => m.Relations.Where(x => x.Class == IfcClass.RelInterferesElements);
    static string TypeOf(Relation r) => (string?)r.Attrs?["interferenceType"] ?? "Subtract";

    /// <summary>A Studio solid that only cuts (an extrusion made to cut, a face pushed into an element): never drawn itself.</summary>
    public static bool IsVoid(Entity e) => e.Str("operation") != null && e.Attrs["void"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public ElementGeometry? Apply(Entity e, ElementGeometry g, IModelView m, IServices services)
    {
        var rels = m.RelationsOf(e.Id).Where(x => x.Class == IfcClass.RelInterferesElements).ToList();
        if (IsVoid(e)) return g with { Parts = [], Plan = null };
        if (rels.Count == 0) return null;
        // A union's or intersection's tool is drawn inside its target, unless the target shows its operands.
        var asTool = rels.FirstOrDefault(x => x.Related.Contains(e.Id) && TypeOf(x) != "Subtract");
        if (asTool != null && m.Get(asTool.Relating) is { } owner && !(owner.Attrs["showOperands"] is JsonValue sv && sv.TryGetValue<bool>(out var show) && show))
            return g with { Parts = [], Plan = null };
        var mine = rels.Where(x => x.Relating == e.Id).ToList();
        if (mine.Count == 0 || g.Parts.Count == 0) return null;
        if (services.Get<ISolidBooleans>() is not { } booleans) return g with { Diagnostics = [.. g.Diagnostics ?? [], "Booleans need the boolean library (manifold), missing here."] };
        var producers = services.Get<IGeometryProducers>();
        List<Solid> ToolSolids(Relation x) => x.Related.Select(m.Get).OfType<Entity>()
            .SelectMany(t => producers?.Geometry(t.Class)?.Invoke(t, m, services)?.Parts.Select(p => p.Solid) ?? []).ToList();
        var parts = g.Parts.ToList();
        var problems = new List<string>();
        foreach (var x in mine.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var tools = ToolSolids(x);
            if (tools.Count == 0) continue;
            try
            {
                switch (TypeOf(x))
                {
                    case "Union":
                    {
                        var u = booleans.Union([.. parts.Select(p => p.Solid), .. tools], "studio");
                        if (u != null) parts = [parts[0] with { Solid = u, Key = "body" }];
                        break;
                    }
                    case "Intersection":
                    {
                        var self = parts.Count == 1 ? parts[0].Solid : booleans.Union(parts.Select(p => p.Solid).ToList(), "studio");
                        var other = tools.Count == 1 ? tools[0] : booleans.Union(tools, "studio");
                        var i = self != null && other != null ? booleans.Intersection(self, other, "studio") : null;
                        parts = i != null ? [parts[0] with { Solid = i, Key = "body" }] : [];
                        break;
                    }
                    default:
                        parts = parts.Select(p => booleans.Difference(p.Solid, tools, p.Solid.Tag) is { } d ? p with { Solid = d } : null).OfType<GeometryPart>().ToList();
                        break;
                }
            }
            catch (InvalidOperationException ex) { problems.Add($"The boolean could not be done: {ex.Message}"); }
        }
        return g with { Parts = parts, Diagnostics = problems.Count > 0 ? [.. g.Diagnostics ?? [], .. problems] : g.Diagnostics };
    }
}
