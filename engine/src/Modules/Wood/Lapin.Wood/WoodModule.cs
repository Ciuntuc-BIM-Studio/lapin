using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
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
public sealed class WoodModule : ILapinModule, IGeometryFilter
{
    public string Id => "wood";
    public string Name => "Wood";

    public const string OpeningKey = "woodFraming";

    public void Register(IModuleRegistry r)
    {
        r.Service<IGeometryFilter>(this);
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
        var result = Framing.Layout(spec, u0, u1, top, bends, core.From, core.To, holes, origin);
        var names = placed.ToDictionary(p => p.Opening.Id, p => p.Fill?.Name ?? "An opening");
        var warnings = result.Warnings.Select(w => names.Aggregate(w, (acc, kv) => acc.Replace(kv.Key + ":", kv.Value + ":"))).ToList();
        return new WallFrame(wall, spec, result with { Warnings = warnings }, start, dir, baseZ, u0, u1, profile is { Count: > 0 } ? profile.Max(p => p.V) : height);
    }

    /// <summary>The members of a timber-frame wall in place of its core, each a part; the insulation between them; the lintels a masonry rule would put in, gone.</summary>
    public ElementGeometry? Apply(Entity entity, ElementGeometry geometry, IModelView model, IServices services)
    {
        if (entity.Class != IfcClass.Wall || Of(model, entity, geometry, services) is not { } f) return geometry;
        var core = Walls.Section(model, entity).First(b => b.Core);
        var parts = geometry.Parts.Where(p => p.Key != $"layer:{core.Index}" && !(p.Key?.StartsWith("lintel", StringComparison.Ordinal) ?? false)).ToList();
        var counts = new Dictionary<string, int>();
        foreach (var mb in f.Result.Members)
        {
            var face = mb.Face.Select(p => f.At(p.U, p.Z, mb.Y0)).ToList();
            var across = f.At(0, 0, mb.Y1) - f.At(0, 0, mb.Y0);
            if (Prism.Extrude(face, null, across, mb.Role) is not { } body) continue;
            var i = counts[mb.Role] = counts.GetValueOrDefault(mb.Role) + 1;
            var (w, d) = FrameSpec.Size(mb.Section, (0, 0));
            parts.Add(new GeometryPart($"{Label(mb.Role)} {Section(mb.Section)}", Colour(mb.Role), body, null, $"wood:{mb.Role}:{i}")
            {
                PropertySet = "Lapin_Timber",
                Properties = [("Role", Label(mb.Role)), ("Section", Section(mb.Section)), ("Grade", f.Spec.Grade), ("Length", Math.Round(mb.Length, 3).ToString(CultureInfo.InvariantCulture))],
            });
        }
        var insulation = f.Result.Insulation.Select(c =>
            Prism.Extrude([f.At(c.U0, c.Z0, core.From), f.At(c.U1, c.Z0, core.From), f.At(c.U1, c.Z1Right, core.From), f.At(c.U0, c.Z1Left, core.From)], null, f.At(0, 0, core.To) - f.At(0, 0, core.From), "insulation")).OfType<Solid>().ToList();
        if (insulation.Count > 0) parts.Add(new GeometryPart("Insulation", "#e8d27a", Openings.Combine(insulation, "insulation"), null, "wood-insulation"));
        return geometry with { Parts = parts, Diagnostics = f.Result.Warnings.Count > 0 ? [.. geometry.Diagnostics ?? [], .. f.Result.Warnings] : geometry.Diagnostics };
    }

    public static string Label(string role) => role switch
    {
        "plate" => "Plate", "end" => "End stud", "stud" => "Stud", "king" => "King stud", "jack" => "Jack stud", "header" => "Header",
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
