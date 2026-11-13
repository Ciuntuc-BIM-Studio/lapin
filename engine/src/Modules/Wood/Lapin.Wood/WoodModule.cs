using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Wood;

/// <summary>
/// The wood module (docs/plan-wood.md, decision 239), stage 1: timber-frame walls. A wall type with a frame spec builds its
/// core as members: the wall's body keeps its other layers and takes, in place of its core, every member of its frame
/// (<see cref="Framing"/>) as a part of its own and the insulation between them; each opening framed by its width band's
/// rule or its own (<c>woodFraming</c> on the opening). Commands set a type's frame and an opening's own rule; the
/// configurator's preview lays out a sample wall; the Timber schedule lists every member, the roofs' timber too.
/// </summary>
public sealed class WoodModule : ILapinModule, IGeometryFilter, IViewAnnotations
{
    public string Id => "wood";
    public string Name => "Wood";

    public const string OpeningKey = "woodFraming";
    /// <summary>A view that is a wall's frame elevation names the wall.</summary>
    public const string FrameViewKey = "woodFrame";

    public void Register(IModuleRegistry r)
    {
        r.Service<IGeometryFilter>(this);
        r.Service<IViewAnnotations>(this);
        r.Service<IScheduleSource>(new BoardSchedule());
        r.Service<IScheduleSource>(new CltSchedule());
        r.Service<IScheduleSource>(new PanelSchedule());

        // A joisted floor type (decision 244): {id (a floor type), spec (null: built of its layers)}.
        r.Command("setFloorJoists", "Floor joists", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.SlabType } t ? t : throw new CommandException("Joists are set on a floor type.");
            var attrs = type.CloneAttrs();
            if (a["spec"] is JsonObject o)
            {
                var spec = JoistSpec.From(o);
                if (spec.Problem() is { } problem) throw new CommandException(problem);
                attrs[FrameSpec.Key] = spec.ToJson();
            }
            else attrs.Remove(FrameSpec.Key);
            ctx.Replace(type.With(attrs: attrs));
            return null;
        });

        // The configurator's joist preview: {spec} → a 6 × 4 m floor with a stairwell, its members in plan.
        r.Query("joistPreview", (q, a) =>
        {
            var spec = JoistSpec.From(a["spec"]);
            if (spec.Problem() is { } problem) return new JsonObject { ["problem"] = problem };
            Vec2[] outline = [new(0, 0), new(6, 0), new(6, 4), new(0, 4)];
            Vec2[] well = [new(0.6, 0.6), new(3.4, 0.6), new(3.4, 1.6), new(0.6, 1.6)];
            var (_, depth) = FrameSpec.Size(spec.Section, (0.045, 0.22));
            var r = Joists.Layout(spec, outline, [well], -depth, 0);
            return new JsonObject
            {
                ["members"] = new JsonArray([.. r.Members.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["pts"] = new JsonArray([.. m.Outline.SelectMany(p => new[] { (JsonNode)Math.Round(p.X, 4), Math.Round(p.Y, 4) })]) })]),
                ["counts"] = new JsonObject([.. r.Members.GroupBy(m => m.Role).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))]),
                ["warnings"] = new JsonArray([.. r.Warnings.Select(w => (JsonNode)w)]),
            };
        });

        // A CLT roof (decision 246): {id (the roof), spec (null: its own rafters or trusses)}.
        r.Command("setRoofClt", "CLT roof", (ctx, a) =>
        {
            var roof = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Roof } x ? x : throw new CommandException("Pick a roof.");
            if (roof.Str("shape") is "flat" or "barrel") throw new CommandException("A CLT roof is laid on sloped planes: a flat roof is a CLT floor's type, a vault is not made of plates.");
            var attrs = roof.CloneAttrs();
            if (a["spec"] is JsonObject o)
            {
                var spec = CltSpec.From(o, CltSpec.Floor);
                if (spec.Problem() is { } problem) throw new CommandException(problem);
                attrs[CltSpec.RoofKey] = spec.ToJson();
            }
            else attrs.Remove(CltSpec.RoofKey);
            ctx.Replace(roof.With(attrs: attrs));
            return null;
        });

        // A CLT wall or floor type (decision 241): {id (a wall or slab type), spec (null: built of its layers)}.
        r.Command("setClt", "CLT", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.WallType or IfcClass.SlabType } t ? t : throw new CommandException("CLT is set on a wall or floor type.");
            var attrs = type.CloneAttrs();
            if (a["spec"] is JsonObject o)
            {
                var spec = CltSpec.From(o, type.Class == IfcClass.SlabType ? CltSpec.Floor : CltSpec.Wall);
                if (spec.Problem() is { } problem) throw new CommandException(problem);
                attrs[FrameSpec.Key] = spec.ToJson();
            }
            else attrs.Remove(FrameSpec.Key);
            ctx.Replace(type.With(attrs: attrs));
            return null;
        });

        // The configurator's CLT preview: {spec, length?, height?, openings?} → the joints of a sample wall, its rough openings, the warnings.
        r.Query("cltPreview", (q, a) =>
        {
            var spec = CltSpec.From(a["spec"], CltSpec.Wall);
            if (spec.Problem() is { } problem) return new JsonObject { ["problem"] = problem };
            var length = Json.Number(a["length"]) ?? 12;
            var height = Json.Number(a["height"]) ?? 2.7;
            var ros = new List<(string Id, double L, double R, double B, double T)> { ("Window", 1.4 - spec.Gap, 2.6 + spec.Gap, 0.9 - spec.Gap, 2.2 + spec.Gap), ("Door", 7.05 - spec.Gap, 7.95 + spec.Gap, 0, 2.1 + spec.Gap), ("Window 2", 9.4 - spec.Gap, 11.2 + spec.Gap, 0.9 - spec.Gap, 2.2 + spec.Gap) };
            var (joints, warnings) = Clt.Joints(spec, 0, length, ros.Select(o => (o.Id, o.L, o.R)).ToList());
            return new JsonObject
            {
                ["length"] = length, ["height"] = height, ["joints"] = new JsonArray([.. joints.Select(j => (JsonNode)Math.Round(j, 4))]),
                ["openings"] = new JsonArray([.. ros.Select(o => (JsonNode)new JsonArray(Math.Round(o.L, 4), Math.Round(o.R, 4), Math.Round(o.B, 4), Math.Round(o.T, 4)))]),
                ["warnings"] = new JsonArray([.. warnings.Select(w => (JsonNode)w)]), ["label"] = spec.Label,
                // Each opening's cutter as the router makes it (decision 246), (u, z) loops.
                ["cutters"] = new JsonArray([.. ros.SelectMany(o => Clt.Cutters(spec, o.L, o.R, o.B, o.T)).Select(loop => (JsonNode)new JsonArray([.. loop.SelectMany(p => new[] { (JsonNode)Math.Round(p.U, 4), Math.Round(p.Z, 4) })]))]),
            };
        });

        // A timber-frame wall's frame elevation (decision 240): a section along its axis through the middle of its core, every
        // member cut, at 1:20, with the frame's dimensions; {id (the wall)} → the view.
        r.Command("frameView", "Frame elevation", (ctx, a) =>
        {
            var wall = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Wall } w ? w : throw new CommandException("Pick a wall.");
            var wallType = Walls.TypeOf(ctx.Model, wall.Id);
            if (FrameSpec.Of(wallType) == null && CltSpec.Of(wallType) == null) throw new CommandException("The wall is not a timber frame or CLT: its type is built of layers.");
            var core = Walls.Section(ctx.Model, wall).First(b => b.Core);
            var attrs = new JsonObject { ["kind"] = "section", ["scale"] = 20, ["discipline"] = "Structure", [FrameViewKey] = wall.Id, ["depth"] = Math.Round((core.To - core.From) / 2 + 0.01, 4) };
            var (s, e) = (Walls.Start(wall), Walls.End(wall));
            SectionPaths.Set(ctx.Model, ctx.Services, attrs, new JsonObject { ["start"] = new JsonArray(s.X, s.Y), ["end"] = new JsonArray(e.X, e.Y) }, create: true);
            var v = new Entity(ctx.NewId(), Views.Class, $"Frame · {wall.Name}", attrs);
            ctx.Add(v);
            return new JsonObject { ["id"] = v.Id };
        });
        r.Service<IScheduleSource>(new TimberSchedule());

        // A wall type's frame: {id (the wall type), spec (null: not a timber frame)}.
        r.Command("setWallFrame", "Timber frame", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.WallType } t ? t : throw new CommandException("A timber frame is set on a wall type.");
            var attrs = type.CloneAttrs();
            if (a["spec"] is JsonObject o)
            {
                var spec = FrameSpec.From(o);
                if (spec.Problem() is { } problem) throw new CommandException(problem);
                attrs[FrameSpec.Key] = spec.ToJson();
            }
            else attrs.Remove(FrameSpec.Key);
            ctx.Replace(type.With(attrs: attrs));
            return null;
        });

        // An opening's own rule: {id (a door, a window or their opening), rule (null: the band's)}.
        r.Command("setOpeningFrame", "Opening framing", (ctx, a) =>
        {
            var opening = OpeningOf(ctx.Model, (string?)a["id"]) ?? throw new CommandException("Pick a door, a window or an opening.");
            var attrs = opening.CloneAttrs();
            if (a["rule"] is JsonObject o)
            {
                var wall = Openings.WallOf(ctx.Model, opening.Id);
                var spec = FrameSpec.Of(wall == null ? null : Walls.TypeOf(ctx.Model, wall.Id)) ?? FrameSpec.Default;
                var p = Openings.Place(ctx.Model, opening);
                var rule = OpeningRule.From(o, spec.RuleFor(p.Width + 2 * spec.GapSide));
                if ((spec with { Openings = [rule] }).Problem() is { } problem) throw new CommandException(problem);
                attrs[OpeningKey] = rule.ToJson();
            }
            else attrs.Remove(OpeningKey);
            ctx.Replace(opening.With(attrs: attrs));
            return null;
        });

        // The configurator's preview: {spec, length?, height?, openings?: [{along, sill, width, height, door}]} → the members and warnings of a sample wall.
        r.Query("preview", (q, a) =>
        {
            var spec = FrameSpec.From(a["spec"]);
            if (spec.Problem() is { } problem) return new JsonObject { ["problem"] = problem };
            var length = Json.Number(a["length"]) ?? 6;
            var height = Json.Number(a["height"]) ?? 2.7;
            var holes = a["openings"] is JsonArray arr
                ? arr.OfType<JsonObject>().Select((o, i) => new Framing.Hole($"Opening {i + 1}", Json.Number(o["along"]) ?? 1, Json.Number(o["sill"]) ?? 0.9, Json.Number(o["width"]) ?? 1.2, Json.Number(o["height"]) ?? 1.5, o["door"] is JsonValue dv && dv.TryGetValue<bool>(out var door) && door)).ToList()
                : [new Framing.Hole("Window", 1.7, 0.9, 1.2, 1.3, false), new Framing.Hole("Door", 4.3, 0, 0.9, 2.1, true)];
            var (_, d) = spec.Stud;
            var result = Framing.Panelize(spec, 0, length, _ => height, [], -d / 2, d / 2, holes, 0);
            return ToJson(result, length, height);
        });

        // A wall type's layers, first to last, for the configurator's boards: {id} → [{index, name, thickness, core}].
        r.Query("layers", (q, a) =>
        {
            var type = q.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.WallType } t ? t : throw new CommandException("Not a wall type.");
            var layers = WallTypes.LayersOf(q.Model, type.Id);
            var core = Lapin.Model.Layers.CoreIndex(layers);
            return new JsonArray([.. layers.Select((l, i) => (JsonNode)new JsonObject
            {
                ["index"] = i, ["name"] = l.Name ?? q.Model.Get(l.Material)?.Name ?? $"Layer {i + 1}", ["thickness"] = l.Thickness, ["core"] = i == core,
            })]);
        });

        // A wall's frame as built: its members by role, its warnings, its openings' rules.
        r.Query("wall", (q, a) =>
        {
            var wall = q.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Wall } w ? w : throw new CommandException("Not a wall.");
            if (CltSpec.Of(Walls.TypeOf(q.Model, wall.Id)) is { } clt)
                return new JsonObject { ["framed"] = false, ["clt"] = true, ["label"] = clt.Label, ["panels"] = q.Geometry(wall.Id)?.Parts.Count(p => p.Key?.StartsWith("clt:", StringComparison.Ordinal) == true) ?? 0 };
            if (Of(q.Model, wall, q.Geometry(wall.Id), q.Services) is not { } f) return new JsonObject { ["framed"] = false };
            var o = ToJson(f.Result, f.U1 - f.U0, f.Height);
            o["framed"] = true;
            return o;
        });

        // An opening's framing: the rule it takes (its band's, or its own) and what it gets.
        r.Query("opening", (q, a) =>
        {
            var opening = OpeningOf(q.Model, (string?)a["id"]) ?? throw new CommandException("Pick a door, a window or an opening.");
            var wall = Openings.WallOf(q.Model, opening.Id);
            if (wall == null || Of(q.Model, wall, q.Geometry(wall.Id), q.Services) is not { } f || !f.Result.Openings.TryGetValue(opening.Id, out var o)) return new JsonObject { ["framed"] = false };
            var mine = f.Result.Members.Where(m => m.Opening == opening.Id).GroupBy(m => m.Role).Select(g => (JsonNode)new JsonObject { ["role"] = g.Key, ["count"] = g.Count(), ["section"] = g.First().Section }).ToArray();
            return new JsonObject
            {
                ["framed"] = true, ["own"] = opening.Attrs[OpeningKey] != null, ["rule"] = o.Rule.ToJson(),
                ["roughWidth"] = Math.Round(o.U1 - o.U0, 4), ["roughHeight"] = Math.Round(o.Z1 - o.Z0, 4), ["members"] = new JsonArray(mine),
                ["warnings"] = new JsonArray([.. f.Result.Warnings.Where(w => w.StartsWith(opening.Id + ":")).Select(w => (JsonNode)w)]),
            };
        });
    }

    static Entity? OpeningOf(IModelView m, string? id) => m.Get(id ?? "") switch
    {
        { Class: IfcClass.OpeningElement } o => o,
        { } fill when Openings.OpeningOf(m, fill.Id) is { } o => o,
        _ => null,
    };

    static JsonObject ToJson(Framing.Result r, double length, double height) => new()
    {
        ["length"] = Math.Round(length, 4), ["height"] = Math.Round(height, 4),
        ["members"] = new JsonArray([.. r.Members.Select(m => (JsonNode)new JsonObject
        {
            ["role"] = m.Role, ["section"] = m.Section, ["opening"] = m.Opening,
            ["face"] = new JsonArray([.. m.Face.SelectMany(p => new[] { (JsonNode)Math.Round(p.U, 4), Math.Round(p.Z, 4) })]),
        })]),
        ["insulation"] = new JsonArray([.. r.Insulation.Select(c => (JsonNode)new JsonArray(Math.Round(c.U0, 4), Math.Round(c.U1, 4), Math.Round(c.Z0, 4), Math.Round(c.Z1Left, 4), Math.Round(c.Z1Right, 4)))]),
        ["counts"] = new JsonObject([.. r.Members.GroupBy(m => m.Role).Select(g => KeyValuePair.Create(g.Key, (JsonNode?)g.Count()))]),
        ["warnings"] = new JsonArray([.. r.Warnings.Select(w => (JsonNode)w)]),
        ["panels"] = new JsonArray([.. (r.Panels ?? []).Select(p => (JsonNode)new JsonArray(Math.Round(p.U0, 4), Math.Round(p.U1, 4)))]),
    };

    /// <summary>A prefabricated panel's mark (decision 245): the wall's name and the panel's number along it, "W3-P2".</summary>
    public static string PanelMark(Entity wall, int n) => $"{(string.IsNullOrWhiteSpace(wall.Name) ? wall.Id : wall.Name)}-P{n}";

    /// <summary>A wall's frame where it is: its spec, the result, and how its elevation maps into the model.</summary>
    public sealed record WallFrame(Entity Wall, FrameSpec Spec, Framing.Result Result, Vec2 Start, Vec2 Dir, double BaseZ, double U0, double U1, double Height)
    {
        public Vec3 At(double u, double z, double y) { var p = Start + Dir * u + Dir.Perp * y; return new Vec3(p.X, p.Y, BaseZ + z); }
    }

    /// <summary>
    /// A straight wall of a timber-frame type, framed: the core's run from its body (as its joins cut it), its top from
    /// the roof or body over it (else its height), its openings with their own rules. Null for any other wall.
    /// </summary>
    public static WallFrame? Of(IModelView m, Entity wall, ElementGeometry? g, IServices? services)
    {
        // The wall's body as the filter made it: the frame it was made with (its core is gone from it by then).
        if (g != null && Built.TryGetValue(g, out var made)) return made;
        if (wall.Class != IfcClass.Wall || FrameSpec.Of(Walls.TypeOf(m, wall.Id)) is not { } spec) return null;
        if (PathCurve.Of(wall) is { IsArc: true }) return null;
        var bands = Walls.Section(m, wall);
        if (!bands.Any(b => b.Core) || g == null) return null;
        var core = bands.First(b => b.Core);
        var start = Walls.Start(wall);
        var axis = Walls.End(wall) - start;
        if (axis.Length < 1e-6) return null;
        var dir = axis.Normalized();
        var baseZ = (Spatial.StoreyOf(m, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        var height = Walls.HeightOf(m, wall);
        // The core's run, from its part's bottom: at each end the inner one of the end face's corners (a mitre's or a butt's).
        var corePart = g.Parts.FirstOrDefault(p => p.Key == $"layer:{core.Index}");
        double u0 = 0, u1 = axis.Length;
        if (corePart != null)
        {
            var pts = corePart.Solid.Vertices.Select(v => (U: Vec2.Dot(v.Xy - start, dir), Z: v.Z - baseZ)).ToList();
            var low = pts.Min(p => p.Z);
            var bottom = pts.Where(p => p.Z < low + 1e-6).Select(p => p.U).ToList();
            var reach = (core.To - core.From) * 1.5 + 0.02;
            var (min, max) = (bottom.Min(), bottom.Max());
            u0 = bottom.Where(u => u < min + reach).Max();
            u1 = bottom.Where(u => u > max - reach).Min();
        }
        // The top: under a roof or a body, its profile; else the wall's height.
        var profile = WallTops.Profile(wall, new RoofTop.WallAxis(start, dir, axis.Length, baseZ, height, bands.Max(b => b.To) - bands.Min(b => b.From)), m, services);
        Func<double, double> top = _ => height;
        var bends = new List<double>();
        if (profile is { Count: >= 2 })
        {
            var pr = profile.OrderBy(p => p.U).ToList();
            top = u =>
            {
                if (u <= pr[0].U) return pr[0].V;
                for (var i = 0; i + 1 < pr.Count; i++)
                    if (u <= pr[i + 1].U) return pr[i + 1].U - pr[i].U < 1e-9 ? pr[i + 1].V : pr[i].V + (pr[i + 1].V - pr[i].V) * (u - pr[i].U) / (pr[i + 1].U - pr[i].U);
                return pr[^1].V;
            };
            bends.AddRange(pr.Select(p => p.U));
        }
        // Each opening keyed by its id, so its rule and members are found again; the warnings then name its door or window.
        var placed = Openings.PlacedOn(m, wall.Id);
        var holes = placed.Select(p => new Framing.Hole(
            p.Opening.Id, p.Along, p.Sill, p.Width, p.Height, p.Fill?.Class == IfcClass.Door || p.Sill <= 1e-6,
            p.Opening.Attrs[OpeningKey] is JsonObject own ? OpeningRule.From(own, spec.RuleFor(p.Width + 2 * spec.GapSide)) : null)).ToList();
        var origin = spec.LayoutFrom == "project" ? -Vec2.Dot(start, dir) : 0;
        var result = Framing.Panelize(spec, u0, u1, top, bends, core.From, core.To, holes, origin, Junctions(m, wall, start, dir, axis.Length, u0, u1, bands));
        var names = placed.ToDictionary(p => p.Opening.Id, p => p.Fill?.Name ?? "An opening");
        var warnings = result.Warnings.Select(w => names.Aggregate(w, (acc, kv) => acc.Replace(kv.Key + ":", kv.Value + ":"))).ToList();
        return new WallFrame(wall, spec, result with { Warnings = warnings }, start, dir, baseZ, u0, u1, profile is { Count: > 0 } ? profile.Max(p => p.V) : height);
    }

    /// <summary>
    /// Where the other walls of the storey stop against this one (decision 240): their core's band along it, at a corner
    /// (where this wall's core runs past the corner point, as its join made it) or in its run (a T), and on which side.
    /// </summary>
    static List<Framing.Junction> Junctions(IModelView m, Entity wall, Vec2 start, Vec2 dir, double length, double u0, double u1, IReadOnlyList<Walls.Band> bands)
    {
        var o = new List<Framing.Junction>();
        var storey = Spatial.StoreyOf(m, wall.Id)?.Id;
        var reach = Math.Max(Math.Abs(bands.Min(b => b.From)), Math.Abs(bands.Max(b => b.To))) + 0.02;
        // Only the walls near this one are read (report #125): a wall added elsewhere does not frame this one again. Sorted by the
        // place in their attributes, a core's half thickness allowed for at most a metre, then read through the model.
        const double Beyond = 1.05;
        var end = start + dir * length;
        var round = new Vec3(reach + Beyond, reach + Beyond, 0);
        var box = (new Vec3(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), 0) - round, new Vec3(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), 0) + round);
        bool Near(Vec2 p) { var s = Vec2.Dot(p - start, dir); return Math.Abs(Vec2.Cross(dir, p - start)) <= reach && s >= -Beyond && s <= length + Beyond; }
        foreach (var found in NearReads.Candidates(m, IfcClass.Wall, box))
        {
            if (found.Id == wall.Id || (!Near(Walls.Start(found)) && !Near(Walls.End(found)))) continue;
            if (m.Get(found.Id) is not { } other || Spatial.StoreyOf(m, other.Id)?.Id != storey || PathCurve.Of(other) is { IsArc: true }) continue;
            var (a, b) = (Walls.Start(other), Walls.End(other));
            var od = b - a;
            if (od.Length < 1e-6 || Math.Abs(Vec2.Cross(dir, od.Normalized())) < 0.3) continue;
            var obands = Walls.Section(m, other);
            if (!obands.Any(x => x.Core)) continue;
            var oc = obands.First(x => x.Core);
            var half = (oc.To - oc.From) / 2;
            foreach (var (p, q) in new[] { (a, b), (b, a) })
            {
                var s = Vec2.Dot(p - start, dir);
                if (Math.Abs(Vec2.Cross(dir, p - start)) > reach || s < -half - 0.05 || s > length + half + 0.05) continue;
                var left = Vec2.Cross(dir, q - p) > 0;
                var (j0, j1) = (s - half, s + half);
                if (s < half + 0.05)
                {
                    // At this wall's start: a corner only where this wall's core runs past the other's middle.
                    if (u0 < s - half / 2) o.Add(new Framing.Junction(j0, j1, true, 1, left));
                }
                else if (s > length - half - 0.05)
                {
                    if (u1 > s + half / 2) o.Add(new Framing.Junction(j0, j1, true, -1, left));
                }
                else o.Add(new Framing.Junction(j0, j1, false, 0, left));
            }
        }
        return o;
    }

    /// <summary>Each framed body the filter returns, with the frame it was built from (read back by the queries and the schedule).</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ElementGeometry, WallFrame> Built = new();

    /// <summary>The members of a timber-frame wall in place of its core, each a part; the insulation between them; the lintels a masonry rule would put in, gone.</summary>
    public ElementGeometry? Apply(Entity entity, ElementGeometry geometry, IModelView model, IServices services)
    {
        // CLT walls and floors (decision 241): their core in panels.
        if (entity.Class == IfcClass.Wall && CltSpec.Of(Walls.TypeOf(model, entity.Id)) is { } wallClt) return Clt.Wall(entity, geometry, model, services, wallClt) ?? geometry;
        if (entity.Class == IfcClass.Slab && CltSpec.Of(model.RelatingOf(entity.Id, IfcClass.RelDefinesByType)) is { } floorClt) return Clt.Floor(entity, geometry, model, services, floorClt) ?? geometry;
        if (entity.Class == IfcClass.Roof && CltSpec.OfRoof(entity) is { } roofClt) return Clt.Roof(entity, geometry, model, services, roofClt) ?? geometry;
        // Joisted floors (decision 244): their core as joists.
        if (entity.Class == IfcClass.Slab && Joists.Of(model, entity) is { } floor) return Joists.Apply(entity, geometry, model, services, floor);
        if (entity.Class != IfcClass.Wall || Of(model, entity, geometry, services) is not { } f) return geometry;
        var core = Walls.Section(model, entity).First(b => b.Core);
        var parts = geometry.Parts.Where(p => p.Key != $"layer:{core.Index}" && !(p.Key?.StartsWith("lintel", StringComparison.Ordinal) ?? false)).ToList();
        var counts = new Dictionary<string, int>();
        var timber = model.OfClass(IfcClass.Material).FirstOrDefault(x => x.Name.Contains("timber", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("wood", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("lemn", StringComparison.OrdinalIgnoreCase))?.Id;
        foreach (var mb in f.Result.Members)
        {
            var face = mb.Face.Select(p => f.At(p.U, p.Z, mb.Y0)).ToList();
            var across = f.At(0, 0, mb.Y1) - f.At(0, 0, mb.Y0);
            if (Prism.Extrude(face, null, across, mb.Role) is not { } body) continue;
            var i = counts[mb.Role] = counts.GetValueOrDefault(mb.Role) + 1;
            var (w, d) = FrameSpec.Size(mb.Section, (0, 0));
            List<(string, string)> props = [("Role", Label(mb.Role)), ("Section", Section(mb.Section)), ("Grade", f.Spec.Grade), ("Length", Math.Round(mb.Length, 3).ToString(CultureInfo.InvariantCulture))];
            if (mb.Panel > 0) props.Add(("Panel", PanelMark(entity, mb.Panel)));
            parts.Add(new GeometryPart($"{Label(mb.Role)} {Section(mb.Section)}", Colour(mb.Role), body, timber, $"wood:{mb.Role}:{i}")
            {
                PropertySet = "Lapin_Timber",
                Properties = props,
            });
        }
        var insulation = f.Result.Insulation.Select(c =>
            Prism.Extrude([f.At(c.U0, c.Z0, core.From), f.At(c.U1, c.Z0, core.From), f.At(c.U1, c.Z1Right, core.From), f.At(c.U0, c.Z1Left, core.From)], null, f.At(0, 0, core.To) - f.At(0, 0, core.From), "insulation")).OfType<Solid>().ToList();
        // The layers laid as sheets (decision 240): each sheet the layer's own body (its openings, joins and top already cut) within
        // a sheet's box, the joints upright on the studs' module, the sheets stacked where the wall is taller than one.
        var booleans = services.Get<ISolidBooleans>();
        var bands = Walls.Section(model, entity);
        foreach (var rule in f.Spec.Boards ?? [])
        {
            var band = bands.FirstOrDefault(b => b.Index == rule.Layer);
            var layerPart = parts.FirstOrDefault(p => p.Key == $"layer:{rule.Layer}");
            if (layerPart == null || booleans == null || band.Core) continue;
            var pts = layerPart.Solid.Vertices.Select(v => (U: Vec2.Dot(v.Xy - f.Start, f.Dir), Z: v.Z - f.BaseZ)).ToList();
            var (lu0, lu1, lz0, lz1) = (pts.Min(p => p.U), pts.Max(p => p.U), pts.Min(p => p.Z), pts.Max(p => p.Z));
            var origin = f.Spec.LayoutFrom == "project" ? -Vec2.Dot(f.Start, f.Dir) : 0;
            var first = origin + Math.Floor((lu0 - origin) / rule.Width) * rule.Width;
            var sheets = new List<GeometryPart>();
            var k = 0;
            for (var u = first; u < lu1 - 1e-6; u += rule.Width)
                for (var z = lz0; z < lz1 - 1e-6; z += rule.Height)
                {
                    var (a0, a1, z0, z1) = (Math.Max(u, lu0 - 0.01), Math.Min(u + rule.Width, lu1 + 0.01), z, Math.Min(z + rule.Height, lz1 + 0.01));
                    List<Vec3> face = [f.At(a0, z0, band.From - 0.01), f.At(a1, z0, band.From - 0.01), f.At(a1, z1, band.From - 0.01), f.At(a0, z1, band.From - 0.01)];
                    if (Prism.Extrude(face, null, f.At(0, 0, band.To + 0.01) - f.At(0, 0, band.From - 0.01), "sheet") is not { } box) continue;
                    Solid? piece;
                    try { piece = booleans.Intersection(layerPart.Solid, box, "sheet"); }
                    catch (InvalidOperationException) { piece = null; }
                    if (piece == null || Measure.Volume(piece) < 1e-7) continue;
                    List<(string, string)> props = [("Layer", band.Name), ("Sheet", $"{Math.Round(rule.Width * 1000)}x{Math.Round(rule.Height * 1000)} mm"), ("Area", Math.Round(Measure.Volume(piece) / Math.Max(1e-9, band.To - band.From), 3).ToString(CultureInfo.InvariantCulture))];
                    // On a prefabricated wall, the panel the sheet's middle stands on (decision 245).
                    var mid = (a0 + a1) / 2;
                    if (f.Result.Panels is { } ps && ps.FindIndex(p => mid >= p.U0 - 1e-9 && mid <= p.U1 + 1e-9) is var pi and >= 0) props.Add(("Panel", PanelMark(entity, pi + 1)));
                    sheets.Add(new GeometryPart($"{band.Name} {Math.Round(rule.Width * 1000)}×{Math.Round(rule.Height * 1000)}", layerPart.Color, piece, layerPart.Material, $"wood-board:{rule.Layer}:{++k}")
                    {
                        PropertySet = "Lapin_Board",
                        Properties = props,
                    });
                }
            if (sheets.Count == 0) continue;
            parts.Remove(layerPart);
            parts.AddRange(sheets);
        }
        // The insulation in the core layer's material: the stud zone is where the build-up has it.
        if (insulation.Count > 0) parts.Add(new GeometryPart("Insulation", "#e8d27a", Openings.Combine(insulation, "insulation"), core.Material, "wood-insulation"));
        var framed = geometry with { Parts = parts, Diagnostics = f.Result.Warnings.Count > 0 ? [.. geometry.Diagnostics ?? [], .. f.Result.Warnings] : geometry.Diagnostics };
        Built.AddOrUpdate(framed, f);
        return framed;
    }

    /// <summary>
    /// A frame elevation's dimensions (decision 240): under the wall, a chain from end to end through every rough opening's
    /// sides and, below it, the studs' module; each rough opening's size inside it.
    /// </summary>
    public IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.View.Str(FrameViewKey) is not { } wallId || c.Model.Get(wallId) is not { } wall || c.Geometry?.Invoke(wallId) is not { } g) yield break;
        if (CltSpec.Of(Walls.TypeOf(c.Model, wallId)) is { } clt)
        {
            if (CltMarks(c, wall, g, clt) is { } cm) yield return cm;
            yield break;
        }
        if (Of(c.Model, wall, g, c.Services) is not { } f) yield break;
        var core = Walls.Section(c.Model, wall).First(b => b.Core);
        var y = (core.From + core.To) / 2;
        Vec2 P(double u, double z) => c.Map(f.At(u, z, y));
        var k = c.Scale / 1000;
        var text = Lapin.Drawing.Standards.ResolveText(c.Book, []);
        var mk = new ViewMark(wallId);
        void Chain(IReadOnlyList<double> us, double z, string color)
        {
            var pts = us.Distinct().OrderBy(u => u).ToList();
            if (pts.Count < 2) return;
            mk.Line(P(pts[0], z), P(pts[^1], z), "DIMENSION", 0.18, color);
            foreach (var u in pts) mk.Line(P(u, z - 1.5 * k * 10), P(u, z + 1.5 * k * 10), "DIMENSION", 0.25, color);
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var mid = P((pts[i] + pts[i + 1]) / 2, z + 1.2 * k * 10);
                mk.Text(new PenText(mid, Math.Round((pts[i + 1] - pts[i]) * 1000).ToString(CultureInfo.InvariantCulture), text.Height, Anchor: "middle", Font: text.Font), color);
            }
        }
        var ros = f.Result.Openings.Values.ToList();
        Chain([f.U0, .. ros.SelectMany(o => new[] { o.U0, o.U1 }), f.U1], -0.25, "#000000");
        Chain([f.U0, .. f.Result.Members.Where(m => m.Role is "stud" or "cripple" or "king" or "end" or "corner" or "backer").Select(m => Math.Round((m.U0 + m.U1) / 2, 4)), f.U1], -0.5, "#555555");
        foreach (var o in ros)
            mk.Text(new PenText(P((o.U0 + o.U1) / 2, (o.Z0 + o.Z1) / 2), $"{Math.Round((o.U1 - o.U0) * 1000)} × {Math.Round((o.Z1 - o.Z0) * 1000)}", text.Height, Anchor: "middle", Font: text.Font), "#000000");
        // A prefabricated wall's panels (decision 245): their lengths in a chain above the wall, a joint line at each, each panel's mark.
        if (f.Result.Panels is { Count: > 1 } panels)
        {
            var over = f.Height + 0.3;
            Chain([.. panels.Select(p => p.U0), panels[^1].U1], over, "#0b5cad");
            foreach (var u in panels.Skip(1).Select(p => p.U0)) mk.Line(P(u, -0.1), P(u, f.Height + 0.15), "DIMENSION", 0.35, "#0b5cad");
            for (var i = 0; i < panels.Count; i++)
                mk.Text(new PenText(P((panels[i].U0 + panels[i].U1) / 2, over + 0.35), PanelMark(wall, i + 1), text.Height * 1.2, Anchor: "middle", Font: text.Font), "#0b5cad");
        }
        yield return mk;
    }

    /// <summary>
    /// A CLT wall's panel elevation (decision 246): under it, a chain through the panels' joints and, below, one through the
    /// rough openings' sides; each panel's mark over it; each rough opening's size inside it.
    /// </summary>
    static ViewMark? CltMarks(ViewContext c, Entity wall, ElementGeometry g, CltSpec spec)
    {
        var panels = g.Parts.Where(p => p.Key?.StartsWith("clt:", StringComparison.Ordinal) == true).ToList();
        if (panels.Count == 0) return null;
        var start = Walls.Start(wall);
        var dir = (Walls.End(wall) - start).Normalized();
        var core = Walls.Section(c.Model, wall).First(b => b.Core);
        var baseZ = (Spatial.StoreyOf(c.Model, wall.Id)?.Num("elevation") ?? 0) + wall.Num("baseOffset");
        var y = (core.From + core.To) / 2;
        Vec2 P(double u, double z) { var q = start + dir * u + dir.Perp * y; return c.Map(new Vec3(q.X, q.Y, baseZ + z)); }
        var k = c.Scale / 1000;
        var text = Lapin.Drawing.Standards.ResolveText(c.Book, []);
        var mk = new ViewMark(wall.Id);
        // Each panel's run at its middle ply: a half-lap's two halves overlap, the joint is between them.
        var runs = panels.Select(p =>
        {
            var us = p.Solid.Vertices.Select(v => Vec2.Dot(v.Xy - start, dir)).ToList();
            return (U0: us.Min(), U1: us.Max(), Z1: p.Solid.Vertices.Max(v => v.Z) - baseZ, Mark: (p.Properties ?? []).FirstOrDefault(x => x.Name == "Mark").Value ?? "");
        }).OrderBy(r => r.U0).ToList();
        var joints = new List<double>();
        for (var i = 0; i + 1 < runs.Count; i++) joints.Add(Math.Round((runs[i].U1 + runs[i + 1].U0) / 2, 4));
        var (u0, u1, top) = (runs[0].U0, runs[^1].U1, runs.Max(r => r.Z1));
        void Chain(IReadOnlyList<double> us, double z, string color)
        {
            var pts = us.Distinct().OrderBy(u => u).ToList();
            if (pts.Count < 2) return;
            mk.Line(P(pts[0], z), P(pts[^1], z), "DIMENSION", 0.18, color);
            foreach (var u in pts) mk.Line(P(u, z - 1.5 * k * 10), P(u, z + 1.5 * k * 10), "DIMENSION", 0.25, color);
            for (var i = 0; i + 1 < pts.Count; i++)
                mk.Text(new PenText(P((pts[i] + pts[i + 1]) / 2, z + 1.2 * k * 10), Math.Round((pts[i + 1] - pts[i]) * 1000).ToString(CultureInfo.InvariantCulture), text.Height, Anchor: "middle", Font: text.Font), color);
        }
        var ros = Openings.PlacedOn(c.Model, wall.Id).Select(p => (L: p.Along - p.Width / 2 - spec.Gap, R: p.Along + p.Width / 2 + spec.Gap, B: p.Fill?.Class == IfcClass.Door || p.Sill <= 1e-6 ? 0 : p.Sill - spec.Gap, T: p.Sill + p.Height + spec.Gap)).ToList();
        Chain([u0, .. joints, u1], -0.25, "#0b5cad");
        if (ros.Count > 0) Chain([u0, .. ros.SelectMany(o => new[] { o.L, o.R }), u1], -0.5, "#000000");
        foreach (var j in joints) mk.Line(P(j, -0.1), P(j, top + 0.15), "DIMENSION", 0.35, "#0b5cad");
        for (var i = 0; i < runs.Count; i++)
            mk.Text(new PenText(P((runs[i].U0 + runs[i].U1) / 2, top + 0.3), runs[i].Mark, text.Height * 1.2, Anchor: "middle", Font: text.Font), "#0b5cad");
        foreach (var o in ros)
            mk.Text(new PenText(P((o.L + o.R) / 2, (o.B + o.T) / 2), $"{Math.Round((o.R - o.L) * 1000)} × {Math.Round((o.T - o.B) * 1000)}", text.Height, Anchor: "middle", Font: text.Font), "#000000");
        return mk;
    }

    public static string Label(string role) => role switch
    {
        "plate" => "Plate", "end" => "End stud", "corner" => "Corner stud", "backer" => "Backer stud", "blocking" => "Blocking", "stud" => "Stud", "king" => "King stud", "jack" => "Jack stud", "header" => "Header",
        "sill" => "Sill", "cripple" => "Cripple", "noggin" => "Noggin", _ => role,
    };

    /// <summary>A section as the schedule writes it: "45x145 mm".</summary>
    public static string Section(string section)
    {
        var (w, d) = FrameSpec.Size(section, (0, 0));
        return $"{Math.Round(w * 1000)}x{Math.Round(d * 1000)} mm";
    }

    static string Colour(string role) => role switch { "header" or "sill" => "#a87a46", "plate" => "#b48650", _ => "#c99a64" };
}
