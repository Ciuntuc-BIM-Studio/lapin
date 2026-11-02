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

        // A timber-frame wall's frame elevation (decision 240): a section along its axis through the middle of its core, every
        // member cut, at 1:20, with the frame's dimensions; {id (the wall)} → the view.
        r.Command("frameView", "Frame elevation", (ctx, a) =>
        {
            var wall = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.Wall } w ? w : throw new CommandException("Pick a wall.");
            if (FrameSpec.Of(Walls.TypeOf(ctx.Model, wall.Id)) == null) throw new CommandException("The wall is not a timber frame: its type is built of layers.");
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
            var result = Framing.Layout(spec, 0, length, _ => height, [], -d / 2, d / 2, holes, 0);
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
    };

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
        var height = wall.Num("height", Walls.DefaultHeight);
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
        var result = Framing.Layout(spec, u0, u1, top, bends, core.From, core.To, holes, origin, Junctions(m, wall, start, dir, axis.Length, u0, u1, bands));
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
        foreach (var other in m.OfClass(IfcClass.Wall))
        {
            if (other.Id == wall.Id || Spatial.StoreyOf(m, other.Id)?.Id != storey || PathCurve.Of(other) is { IsArc: true }) continue;
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
            parts.Add(new GeometryPart($"{Label(mb.Role)} {Section(mb.Section)}", Colour(mb.Role), body, timber, $"wood:{mb.Role}:{i}")
            {
                PropertySet = "Lapin_Timber",
                Properties = [("Role", Label(mb.Role)), ("Section", Section(mb.Section)), ("Grade", f.Spec.Grade), ("Length", Math.Round(mb.Length, 3).ToString(CultureInfo.InvariantCulture))],
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
                    sheets.Add(new GeometryPart($"{band.Name} {Math.Round(rule.Width * 1000)}×{Math.Round(rule.Height * 1000)}", layerPart.Color, piece, layerPart.Material, $"wood-board:{rule.Layer}:{++k}")
                    {
                        PropertySet = "Lapin_Board",
                        Properties = [("Layer", band.Name), ("Sheet", $"{Math.Round(rule.Width * 1000)}x{Math.Round(rule.Height * 1000)} mm"), ("Area", Math.Round(Measure.Volume(piece) / Math.Max(1e-9, band.To - band.From), 3).ToString(CultureInfo.InvariantCulture))],
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
        yield return mk;
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
