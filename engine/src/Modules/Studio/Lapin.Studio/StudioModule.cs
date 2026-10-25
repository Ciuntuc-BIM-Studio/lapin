using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Studio (the old Lapin's): solids from sketches drawn on a plan or a
/// section, an extrusion, a sweep along a path or a revolution about an
/// axis, each rebuilt whenever its sketch changes; and live booleans between
/// any elements (IfcRelInterferesElements): a target less its tools, or united
/// or intersected with them (the tools then hidden, kept editable).
/// </summary>
public sealed class StudioModule : ILapinModule, IGeometryFilter
{
    public string Id => "studio";
    public string Name => "Studio";

    /// <summary>The classes a Studio solid can be (those no other module builds); others are written as a proxy with <c>exportAs</c>.</summary>
    public static readonly string[] Classes =
        [IfcClass.BuildingElementProxy, "IfcMember", "IfcPlate", "IfcCovering", "IfcRailing", "IfcStair", "IfcRamp", "IfcChimney", "IfcFurnishingElement"];

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

    public static readonly string[] Operations = ["extrude", "sweep", "revolve", "loft"];

    public void Register(IModuleRegistry r)
    {
        foreach (var cls in Classes)
        {
            r.Class(new(cls, cls == IfcClass.BuildingElementProxy ? "Solid" : cls[3..],
            [
                new("depth", "Depth", "length"),
                new("angle", "Angle (°)", "number", Min: -360, Max: 360),
                new("offset", "Offset from the sketch", "length"),
                new("place", "Profile on the path", "choice", Choices: Solids.Places),
                new("placeOffset", "Gap from the path", "length"),
                new("cutPriority", "Cut priority", "number", Min: 0, Max: 100),
                new("exportAs", "Export as", "text"),
            ], "#b9b2a6"));
            r.Geometry(cls, Geometry);
            r.Quantities(cls, Quantities);
            // Studio solids give way to the building's elements; furniture to everything.
            r.Cutter(cls, cls == "IfcFurnishingElement" ? 10 : 40, (e, m) => Geometry(e, m, null)?.Parts is [{ } only] ? only.Solid : null);
        }
        r.Class(new(TypeClass, "Solid type", [new("description", "Description", "text")], "#b9b2a6", Kind: "type"));
        r.Service<IGeometryFilter>(this);
        r.Service<IElementPlacement>(new StudioPlacement());

        r.Command("extrude", "Extrude", (ctx, a) => Create(ctx, a, "extrude"));
        r.Command("sweep", "Sweep", (ctx, a) => Create(ctx, a, "sweep"));
        r.Command("revolve", "Revolve", (ctx, a) => Create(ctx, a, "revolve"));
        r.Command("loft", "Loft", (ctx, a) => Create(ctx, a, "loft"));

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
                var keep = ctx.Model.Relations.Where(x => x.Touches(e.Id) && x.Class is IfcClass.RelInterferesElements or IfcClass.RelDefinesByType).ToList();
                ctx.Remove(e.Id);
                ctx.Add(new Entity(e.Id, cls, e.Name, attrs, pre));
                if (storey != null) Spatial.Contain(ctx, storey, e.Id);
                foreach (var x in keep)
                {
                    if (x.Class == IfcClass.RelDefinesByType) { if (ctx.Model.Get(x.Relating) != null) ctx.Relate(IfcClass.RelDefinesByType, x.Relating, e.Id); continue; }
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

        // What the properties card shows of a Studio solid.
        r.Query("studio.feature", (q, a) =>
        {
            if (q.Model.Get((string?)a["id"] ?? "") is not { } e || e.Str("operation") is not { } op) return null;
            var profile = q.Model.Get(e.Str("profile") ?? "");
            var view = profile != null ? Views.Find(q.Model, profile.Str("viewId") ?? "") : null;
            var holes = profile != null ? Sketches.Holes(q.Model, profile, ProfileIds(q.Model)) : [];
            var outer = Sketches.Loop(profile != null ? Drafts.ShapeOf(profile) : null);
            return new JsonObject
            {
                ["operation"] = op, ["view"] = view?.Name, ["viewId"] = view?.Id, ["profile"] = profile?.Id, ["holes"] = holes.Count,
                ["sketch"] = new JsonArray(new[] { profile?.Id, e.Str("guide") }.Concat(holes.Select(h => h.Id)).OfType<string>().Select(x => (JsonNode)x).ToArray()),
                ["area"] = outer == null ? null : Math.Abs(Polygon2.SignedArea(outer)) - holes.Sum(h => Math.Abs(Polygon2.SignedArea(h.Loop))),
                ["guide"] = e.Str("guide") is { } g ? (q.Model.Get(g) is { } ge ? ge.Name : "missing") : null,
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
        if (Sketches.Loop(Drafts.ShapeOf(d)) == null) throw new CommandException($"The {what} must be a closed polyline or a circle.");
        if (ProfileIds(m).Contains(d.Id)) throw new CommandException($"That outline is a solid's profile already.");
        return d;
    }

    static JsonNode? Create(ICommandContext ctx, JsonObject a, string op)
    {
        var profile = Drafts.Require(ctx.Model, (string?)a["profileId"], editable: false);
        if (Sketches.Loop(Drafts.ShapeOf(profile)) == null) throw new CommandException("The profile must be a closed polyline or a circle.");
        if (ProfileIds(ctx.Model).Contains(profile.Id)) throw new CommandException("That outline is a solid's profile already.");
        var view = Views.Find(ctx.Model, profile.Str("viewId") ?? "") ?? throw new CommandException("The sketch's view is gone.");
        var plane = Sketches.PlaneOf(ctx.Model, view) ?? throw new CommandException("Solids are sketched on a plan or a section.");
        var (cls, exportAs) = ClassOf((string?)a["class"]);
        var attrs = new JsonObject { ["operation"] = op, ["profile"] = profile.Id };
        if (exportAs != null) attrs["exportAs"] = exportAs;
        switch (op)
        {
            case "extrude":
            {
                var depth = Json.Number(a["depth"]) ?? 1;
                if (Math.Abs(depth) < 1e-6) throw new CommandException("An extrusion needs a depth.");
                attrs["depth"] = depth;
                if (Json.Number(a["offset"]) is { } off) attrs["offset"] = off;
                break;
            }
            case "sweep":
            {
                var path = Drafts.Require(ctx.Model, (string?)a["pathId"], editable: false);
                if (Drafts.ShapeOf(path) is not (Line or Polyline or Arc or Circle)) throw new CommandException("A path is a line, a polyline, an arc or a circle.");
                if (path.Str("viewId") != profile.Str("viewId")) throw new CommandException("The path is drawn on the profile's view.");
                attrs["guide"] = path.Id;
                // A profile it ends with (the profile becomes it on the way), or a second rail it stretches to.
                if ((string?)a["endProfileId"] is { } endId)
                {
                    var end = ClosedDraft(ctx.Model, endId, "end profile");
                    if (end.Id == profile.Id) throw new CommandException("The end profile is another outline than the start one.");
                    if (end.Str("viewId") != profile.Str("viewId")) throw new CommandException("The end profile is drawn on the profile's view.");
                    attrs["endProfile"] = end.Id;
                }
                if ((string?)a["railId"] is { } railId)
                {
                    var rail = Drafts.Require(ctx.Model, railId, editable: false);
                    if (Drafts.ShapeOf(rail) is not (Line or Polyline or Arc)) throw new CommandException("A rail is a line, a polyline or an arc.");
                    if (rail.Id == path.Id) throw new CommandException("The second rail is another line than the path.");
                    if (rail.Str("viewId") != profile.Str("viewId")) throw new CommandException("The rail is drawn on the profile's view.");
                    attrs["rail"] = rail.Id;
                }
                if (attrs["endProfile"] != null && attrs["rail"] != null) throw new CommandException("A sweep ends with another profile or follows a second rail, not both.");
                if ((string?)a["place"] is { } place)
                {
                    if (!Solids.Places.Contains(place)) throw new CommandException($"„{place}” is not a place on the path ({string.Join(", ", Solids.Places)}).");
                    attrs["place"] = place;
                }
                if (Json.Number(a["placeOffset"]) is { } gap) attrs["placeOffset"] = gap;
                break;
            }
            case "loft":
            {
                // The profiles in order, each on its own view (a plan of another level, a section further on).
                var ids = (a["profileIds"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToList() ?? [];
                if (ids.Count < 2) throw new CommandException("A loft needs two profiles at least.");
                if (ids[0] != profile.Id) ids.Insert(0, profile.Id);
                var all = ids.Select((id, i) => i == 0 ? profile : ClosedDraft(ctx.Model, id, "profile")).ToList();
                if (all.Select(x => x.Id).Distinct().Count() != all.Count) throw new CommandException("A profile comes once in a loft.");
                foreach (var d in all) if (Views.Find(ctx.Model, d.Str("viewId") ?? "") is not { } v || Sketches.PlaneOf(ctx.Model, v) == null) throw new CommandException("Each profile of a loft is drawn on a plan or a section.");
                attrs["profiles"] = new JsonArray(all.Select(x => (JsonNode)x.Id).ToArray());
                break;
            }
            case "revolve":
            {
                var axis = Drafts.Require(ctx.Model, (string?)a["axisId"], editable: false);
                if (Drafts.ShapeOf(axis) is not Line) throw new CommandException("The axis is a line.");
                if (axis.Str("viewId") != profile.Str("viewId")) throw new CommandException("The axis is drawn on the profile's view.");
                attrs["guide"] = axis.Id;
                attrs["angle"] = Json.Number(a["angle"]) ?? 360;
                break;
            }
        }
        var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : $"{char.ToUpperInvariant(op[0])}{op[1..]} {ctx.Model.OfClass(cls).Count() + 1}";
        var e = new Entity(ctx.NewId(), cls, name, attrs);
        ctx.Add(e);
        var storey = (string?)a["storeyId"] ?? plane.StoreyId ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
        Spatial.Contain(ctx, storey, e.Id);
        return new JsonObject { ["id"] = e.Id };
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices? services)
    {
        var problems = new List<string>();
        ElementGeometry Fail(string why) => new([], Diagnostics: [why]);
        var profile = m.Get(e.Str("profile") ?? "");
        if (profile == null) return Fail("The solid's sketch is gone: draw its outline again.");
        var outer2 = Sketches.Loop(Drafts.ShapeOf(profile));
        if (outer2 == null) return Fail("The solid's outline is no longer closed.");
        var view = Views.Find(m, profile.Str("viewId") ?? "");
        var plane = view == null ? null : Sketches.PlaneOf(m, view);
        if (plane == null) return Fail("The solid's sketch view is gone.");
        var holes2 = Sketches.Holes(m, profile, ProfileIds(m)).Select(h => h.Loop).ToList();
        var shift = plane.Normal * e.Num("offset");
        List<Vec3> W(IReadOnlyList<Vec2> l) => l.Select(p => plane.World(p) + shift).ToList();
        Solid? solid = null;
        switch (e.Str("operation"))
        {
            case "extrude":
                solid = Solids.Extrude(W(outer2), holes2.Select(h => (IReadOnlyList<Vec3>)W(h)).ToList(), plane.Normal * (e.NumOrNull("depth") ?? 1));
                break;
            case "sweep":
            {
                if (m.Get(e.Str("guide") ?? "") is not { } g || Drafts.ShapeOf(g) is not { } gs) return Fail("The path is missing: draw it again to rebuild.");
                var path = Geo.Polylines(gs, Math.PI / 24)[0].Select(p => plane.World(p) + shift).ToList();
                Solids.SweepResult swept;
                if (e.Str("endProfile") is { } endId)
                {
                    if (m.Get(endId) is not { } endDraft || Sketches.Loop(Drafts.ShapeOf(endDraft)) is not { } endLoop) return Fail("The end profile is missing or no longer closed: draw it again to rebuild.");
                    swept = Solids.SweepMorph(outer2, endLoop, path, plane.Normal);
                }
                else if (e.Str("rail") is { } railId)
                {
                    if (m.Get(railId) is not { } railDraft || Drafts.ShapeOf(railDraft) is not { } rs) return Fail("The second rail is missing: draw it again to rebuild.");
                    var rail = Geo.Polylines(rs, Math.PI / 24)[0].Select(p => plane.World(p) + shift).ToList();
                    swept = Solids.SweepRails(outer2, holes2, path, rail, plane.Normal);
                }
                else swept = Solids.SweepPlaced(outer2, holes2, path, plane.Normal, e.Str("place") ?? "centre", e.Num("placeOffset"));
                if (swept.Problem != null) return Fail(swept.Problem);
                solid = swept.Solid;
                break;
            }
            case "loft":
            {
                var loops = new List<IReadOnlyList<Vec2>>();
                var worlds = new List<Func<Vec2, Vec3>>();
                foreach (var id in (e.Attrs["profiles"] as JsonArray)?.Select(x => (string?)x).OfType<string>() ?? [])
                {
                    if (m.Get(id) is not { } d || Sketches.Loop(Drafts.ShapeOf(d)) is not { } loop) return Fail("A profile of the loft is missing or no longer closed.");
                    if (Views.Find(m, d.Str("viewId") ?? "") is not { } v || Sketches.PlaneOf(m, v) is not { } pl) return Fail("A profile's view is gone.");
                    loops.Add(loop);
                    worlds.Add(q => pl.World(q) + shift);
                }
                var lofted = Solids.LoftThrough(loops, worlds);
                if (lofted.Problem != null) return Fail(lofted.Problem);
                solid = lofted.Solid;
                break;
            }
            case "revolve":
            {
                if (m.Get(e.Str("guide") ?? "") is not { } g || Drafts.ShapeOf(g) is not Line ax) return Fail("The axis is missing: draw it again to rebuild.");
                var (a3, b3) = (plane.World(ax.A) + shift, plane.World(ax.B) + shift);
                solid = Solids.Revolve(W(outer2), holes2.Select(h => (IReadOnlyList<Vec3>)W(h)).ToList(), a3, b3 - a3, e.NumOrNull("angle") ?? 360);
                break;
            }
        }
        if (solid == null) return Fail("The solid could not be built from its sketch.");
        var material = e.Str("material");
        // In plan: the outline of what it covers (its sketch when drawn in plan, else its shadow's hull).
        var plan = new List<PlanItem>();
        if (e.Str("operation") == "extrude" && Math.Abs(plane.Normal.Z) > 0.999)
        {
            plan.Add(new PlanItem("outline", true, outer2));
            plan.AddRange(holes2.Select(h => new PlanItem("outline", true, h)));
        }
        else if (Polygon2.ConvexHull(solid.Vertices.Select(v => new Vec2(v.X, v.Y))) is { Length: >= 3 } hull) plan.Add(new PlanItem("outline", true, hull));
        var parts = new List<GeometryPart> { new(e.Name, material != null ? Materials.ColorOf(m, material) : "#b9b2a6", solid, material, "body") };
        // Cut by what ranks above it (walls through a pavement…); its envelope for others is the uncut solid.
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
                var thick = e.Str("operation") == "extrude" ? Math.Abs(e.Num("depth")) : height;
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
    static (double Section, double Along, double Width, double Height) Measure2(Entity e, IModelView m)
    {
        var profile = m.Get(e.Str("profile") ?? "");
        var outer = Sketches.Loop(profile != null ? Drafts.ShapeOf(profile) : null);
        if (outer == null) return (0, 0, 0, 0);
        var holes = Sketches.Holes(m, profile!, ProfileIds(m));
        var section = Math.Abs(Polygon2.SignedArea(outer)) - holes.Sum(h => Math.Abs(Polygon2.SignedArea(h.Loop)));
        var (w, h) = (outer.Max(p => p.X) - outer.Min(p => p.X), outer.Max(p => p.Y) - outer.Min(p => p.Y));
        var along = e.Str("operation") switch
        {
            "extrude" => Math.Abs(e.Num("depth")),
            "sweep" when m.Get(e.Str("guide") ?? "") is { } gd && Drafts.ShapeOf(gd) is { } gs => Geo.Polylines(gs, Math.PI / 24)[0] is var pl ? pl.Zip(pl.Skip(1), (p, q) => (q - p).Length).Sum() : 0,
            _ => 0,
        };
        return (section, along, w, h);
    }

    // ------------------------------------------------------------ live booleans

    static IEnumerable<Relation> Rels(IModelView m) => m.Relations.Where(x => x.Class == IfcClass.RelInterferesElements);
    static string TypeOf(Relation r) => (string?)r.Attrs?["interferenceType"] ?? "Subtract";

    public ElementGeometry? Apply(Entity e, ElementGeometry g, IModelView m, IServices services)
    {
        var rels = m.RelationsOf(e.Id).Where(x => x.Class == IfcClass.RelInterferesElements).ToList();
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
