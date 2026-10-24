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

    /// <summary>The words EXTRUDE takes for a class, as in the old Lapin.</summary>
    public static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PROXY"] = IfcClass.BuildingElementProxy, ["MEMBER"] = "IfcMember", ["PLATE"] = "IfcPlate", ["COVERING"] = "IfcCovering",
        ["RAILING"] = "IfcRailing", ["STAIR"] = "IfcStair", ["RAMP"] = "IfcRamp", ["CHIMNEY"] = "IfcChimney", ["FURNITURE"] = "IfcFurnishingElement",
        ["WALL"] = IfcClass.Wall, ["SLAB"] = IfcClass.Slab, ["FLOOR"] = IfcClass.Slab, ["COLUMN"] = IfcClass.Column, ["BEAM"] = IfcClass.Beam,
        ["ROOF"] = IfcClass.Roof, ["FOOTING"] = IfcClass.Footing,
    };

    public static readonly string[] Operations = ["extrude", "sweep", "revolve"];

    public void Register(IModuleRegistry r)
    {
        foreach (var cls in Classes)
        {
            r.Class(new(cls, cls == IfcClass.BuildingElementProxy ? "Solid" : cls[3..],
            [
                new("depth", "Depth", "length"),
                new("angle", "Angle (°)", "number", Min: -360, Max: 360),
                new("offset", "Offset from the sketch", "length"),
                new("exportAs", "Export as", "text"),
            ], "#b9b2a6"));
            r.Geometry(cls, Geometry);
            r.Quantities(cls, Quantities);
        }
        r.Service<IGeometryFilter>(this);
        r.Service<IElementPlacement>(new StudioPlacement());

        r.Command("extrude", "Extrude", (ctx, a) => Create(ctx, a, "extrude"));
        r.Command("sweep", "Sweep", (ctx, a) => Create(ctx, a, "sweep"));
        r.Command("revolve", "Revolve", (ctx, a) => Create(ctx, a, "revolve"));

        // A solid's class changed: written again under the new one, same id, same sketch.
        r.Command("setClass", "Change class", (ctx, a) =>
        {
            var e = Solid(ctx.Model, (string?)a["id"]);
            var (cls, exportAs) = ClassOf((string?)a["class"]);
            var attrs = e.CloneAttrs();
            if (exportAs != null) attrs["exportAs"] = exportAs; else attrs.Remove("exportAs");
            var storey = Spatial.StoreyOf(ctx.Model, e.Id)?.Id;
            var rels = ctx.Model.Relations.Where(x => x.Touches(e.Id) && x.Class == IfcClass.RelInterferesElements).ToList();
            ctx.Remove(e.Id);
            ctx.Add(new Entity(e.Id, cls, e.Name, attrs, e.PredefinedType));
            if (storey != null) Spatial.Contain(ctx, storey, e.Id);
            foreach (var x in rels) if (ctx.Model.Get(x.Relating) != null && x.Related.All(id => ctx.Model.Get(id) != null)) ctx.AddRelation(x with { Id = ctx.NewId() });
            return null;
        });

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

    static HashSet<string> ProfileIds(IModelView m) =>
        Classes.SelectMany(m.OfClass).Select(e => e.Str("profile")).OfType<string>().ToHashSet();

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

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices services)
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
                solid = Solids.Sweep(outer2, holes2, path, plane.Normal);
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
        return new ElementGeometry([new GeometryPart(e.Name, material != null ? Materials.ColorOf(m, material) : "#b9b2a6", solid, material, "body")], plan, plan.SelectMany(p => p.Points).ToList());
    }

    static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        yield return new QuantitySet($"Qto_{(e.Class.StartsWith("Ifc") ? e.Class[3..] : e.Class)}BaseQuantities",
        [
            new("NetVolume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Net volume"),
            new("OuterSurfaceArea", g.Parts.Sum(p => Measure.SurfaceArea(p.Solid)), "AREA", "Surface area"),
        ]);
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
