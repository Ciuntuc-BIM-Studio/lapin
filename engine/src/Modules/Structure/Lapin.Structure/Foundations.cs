using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>A footprint along an axis between two lateral offsets, its ends cut by its joins.</summary>
static class Strips
{
    /// <summary>The band from <paramref name="y0"/> to <paramref name="y1"/> across the axis, cut at each end straight across the body (yMin … yMax).</summary>
    public static Vec2[] Footprint(JoinWall f, (Vec2 Point, EndCut Cut)?[] ends, double y0, double y1)
    {
        var start = ends[0]?.Point ?? f.Start;
        var end = ends[1]?.Point ?? f.End;
        var sc = ends[0]?.Cut ?? EndCut.Square;
        var ec = ends[1]?.Cut ?? EndCut.Square;
        var dir = (end - start).Normalized();
        var left = dir.Perp;
        var length = (end - start).Length;
        double At(EndCut c, double y) => f.YMax - f.YMin < 1e-12 ? c.Right : c.Right + (c.Left - c.Right) * (y - f.YMin) / (f.YMax - f.YMin);
        Vec2 P(double x, double y) => start + dir * x + left * y;
        return [P(-At(sc, y0), y0), P(length + At(ec, y0), y0), P(length + At(ec, y1), y1), P(-At(sc, y1), y1)];
    }

    /// <summary>The band from <paramref name="y0"/> to <paramref name="y1"/>, each end cut as its own join says, across the body that join was worked out for.</summary>
    public static Vec2[] Footprint(Vec2 axisStart, Vec2 axisEnd, End? start, End? end, double y0, double y1)
    {
        var a = start?.Point ?? axisStart;
        var b = end?.Point ?? axisEnd;
        var dir = (b - a).Normalized();
        var left = dir.Perp;
        var length = (b - a).Length;
        static double At(End? e, double y)
        {
            if (e is not { } x) return 0;
            return x.YMax - x.YMin < 1e-12 ? x.Cut.Right : x.Cut.Right + (x.Cut.Left - x.Cut.Right) * (y - x.YMin) / (x.YMax - x.YMin);
        }
        Vec2 P(double x, double y) => a + dir * x + left * y;
        return [P(-At(start, y0), y0), P(length + At(end, y0), y0), P(length + At(end, y1), y1), P(-At(start, y1), y1)];
    }

    /// <summary>One end as a join leaves it: where the axis stops, the cut there, the body the cut is across, the kind of join.</summary>
    public readonly record struct End(Vec2 Point, EndCut Cut, double YMin, double YMax, JoinKind Kind);

    /// <summary>
    /// Each end's join worked out with the bodies <paramref name="frameFor"/>
    /// gives (all the layers, or the cores only) and the style given (else the
    /// join's own).
    /// </summary>
    public static End?[] Ends(IModelView m, Entity e, IPathElements paths, List<string> problems, Func<Entity, JoinWall?> frameFor, JoinStyle? style = null)
    {
        var ends = new End?[2];
        foreach (var r in Joins.Of(m, e.Id))
        {
            if (m.Get(r.Relating) is not { } a || r.Related.Count == 0 || m.Get(r.Related[0]) is not { } b || !paths.IsPath(a.Class) || a.Class != b.Class) continue;
            if (frameFor(a) is not { } fa || frameFor(b) is not { } fb) continue;
            WallJoin join;
            try { join = WallJoins.Compute(fa, fb, new JoinOptions(Priority: 'a', Style: style ?? Joins.StyleOf(r))); }
            catch (CommandException ex) { problems.Add(ex.Message); continue; }
            var side = r.Relating == e.Id ? join.A : join.B;
            var i = side.Connection switch { Connection.AtStart => 0, Connection.AtEnd => 1, _ => -1 };
            if (i < 0 || ends[i] != null) continue;
            ends[i] = i == 0
                ? new End(side.Wall.Start, side.Wall.StartCut ?? EndCut.Square, side.Wall.YMin, side.Wall.YMax, join.Kind)
                : new End(side.Wall.End, side.Wall.EndCut ?? EndCut.Square, side.Wall.YMin, side.Wall.YMax, join.Kind);
        }
        return ends;
    }

    /// <summary>The same band along a curved axis: its ends where the joins put them (along the tangent there), cut across the body.</summary>
    public static List<Vec2> Footprint(PathCurve c, JoinWall f, (Vec2 Point, EndCut Cut)?[] ends, double y0, double y1)
    {
        var (from, to) = c.Ends(ends[0], ends[1], f.YMin, f.YMax);
        return c.Band(from, to, y0, y1);
    }

    /// <summary>Where a profile swept along a curved axis starts and ends: where its joints leave the axis.</summary>
    public static (double S0, double S1) Reach(PathCurve c, JoinWall f, (Vec2 Point, EndCut Cut)?[] ends)
    {
        var (from, to) = c.Ends(ends[0], ends[1], f.YMin, f.YMax);
        return (from(0), to(0));
    }
}

/// <summary>
/// Strip foundations (the old Lapin's): a foundation wall (the stem),
/// layered like a wall with its reinforced concrete core on the axis, on a
/// strip footing centred under it. One IfcFooting STRIP_FOOTING along its
/// axis, of an IfcFootingType that carries the stem's layer set and the
/// footing's size and level. Strip foundations join as walls do; the
/// footing joins as its stem does, with its own width.
/// </summary>
public static class StripFootings
{
    /// <summary>The old Lapin's default: protection board, XPS, waterproofing, then the 300 mm core; a 1.4 m stem on a 600 × 400 footing at −1.80.</summary>
    public static readonly IReadOnlyList<LayerSpec> DefaultLayers =
    [
        new("Protection board", 0.02, Color: "#8c8c8c"),
        new("XPS insulation", 0.1, Color: "#f2c46d"),
        new("Waterproofing", 0.005, Color: "#303030"),
        new("Reinforced concrete", 0.3, Core: true, Color: "#a8a8a8"),
    ];

    public static bool IsStrip(Entity type) => type.Str("kind") == "strip";

    public static Entity CreateType(IEdit edit, string? name, IReadOnlyList<LayerSpec> layers, double stemHeight = 1.4, double footingWidth = 0.6, double footingHeight = 0.4, double bottom = -1.8, string concrete = "Reinforced concrete")
    {
        LayerSets.Validate(layers, "A strip foundation");
        if (!(stemHeight > 0 && footingWidth > 0 && footingHeight > 0)) throw new CommandException("The stem's height and the footing's width and height must be positive.");
        var core = layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!;
        var type = new Entity(edit.NewId(), IfcClass.FootingType, string.IsNullOrWhiteSpace(name) ? $"SF {core.Thickness * 100:0}" : name.Trim(), new JsonObject
        {
            ["kind"] = "strip", ["stemHeight"] = stemHeight, ["footingWidth"] = footingWidth, ["footingHeight"] = footingHeight, ["bottom"] = bottom,
            ["concrete"] = Materials.Ensure(edit, concrete).Id,
        }, "STRIP_FOOTING");
        edit.Add(type);
        LayerSets.Attach(edit, type, layers);
        return type;
    }

    public static Entity DefaultType(IEdit edit) => CreateType(edit, null, DefaultLayers);

    public static Entity Add(IEdit edit, IPathElements paths, Entity storey, Vec2 start, Vec2 end, Entity type, JoinStyle style, double rise = 0)
    {
        if ((end - start).Length < 1e-3) throw new CommandException("A strip foundation needs two different points.");
        var f = new Entity(edit.NewId(), IfcClass.Footing, type.Name, new JsonObject { ["start"] = Place.Json2(start), ["end"] = Place.Json2(end) }, "STRIP_FOOTING");
        if (rise != 0) f.Attrs[PathCurve.Key] = rise;
        edit.Add(f);
        Spatial.Contain(edit, storey.Id, f.Id);
        SetType(edit, f, type);
        Joins.Auto(edit, paths, f.Id, style);
        return edit.Model.Get(f.Id)!;
    }

    public static void SetType(IEdit edit, Entity footing, Entity type)
    {
        edit.Relate(IfcClass.RelDefinesByType, type.Id, footing.Id);
        LayerSets.LayOut(edit, footing, type, "AXIS2", Layers.CoreOffset);
    }

    /// <summary>The stem's layer bands across the axis (core centred on it), first to last.</summary>
    static IReadOnlyList<(double From, double To, string Material, string Name, int Index)> Bands(IModelView m, Entity footing)
    {
        return LayerSets.BandsOf(m, footing.Id)?.Select(b => (b.From, b.To, b.Material, b.Name, b.Index)).ToList() ?? [];
    }

    /// <summary>The stem as joins see it: its axis and its layers' extent.</summary>
    public static JoinWall? Frame(Entity footing, IModelView m)
    {
        var bands = Bands(m, footing);
        var (s, e) = (Joins.Start(footing), Joins.End(footing));
        if (bands.Count == 0 || (e - s).Length < 1e-3) return null;
        return new JoinWall(s, e, bands.Min(b => b.From), bands.Max(b => b.To));
    }

    /// <summary>The stem's core alone, as the cores' joins see it.</summary>
    static JoinWall? CoreFrame(Entity footing, IModelView m)
    {
        var bands = LayerSets.BandsOf(m, footing.Id);
        var (s, e) = (Joins.Start(footing), Joins.End(footing));
        if (bands is not { Count: > 0 } || (e - s).Length < 1e-3) return null;
        var core = bands.FirstOrDefault(b => b.Core);
        if (core.To - core.From <= 1e-9) core = bands.MaxBy(b => b.To - b.From);
        return new JoinWall(s, e, core.From, core.To);
    }

    /// <summary>The footing as its joins cut it: centred on the axis, its own width.</summary>
    static JoinWall? FootingFrame(Entity footing, IModelView m)
    {
        if (Place.TypeOf(m, footing.Id) is not { } type || !IsStrip(type)) return null;
        var w = type.Num("footingWidth", 0.6);
        return new JoinWall(Joins.Start(footing), Joins.End(footing), -w / 2, w / 2);
    }

    public static Solid? Envelope(Entity footing, IModelView m)
    {
        if (Place.TypeOf(m, footing.Id) is not { } type || !IsStrip(type) || FootingFrame(footing, m) is not { } f) return null;
        var left = (f.End - f.Start).Normalized().Perp;
        var y0 = Math.Min(f.YMin, Frame(footing, m)?.YMin ?? 0);
        var y1 = Math.Max(f.YMax, Frame(footing, m)?.YMax ?? 0);
        var c = PathCurve.Of(footing);
        IReadOnlyList<Vec2> fp = c.IsArc ? c.Band(_ => 0, _ => c.Length, y0, y1) : [f.Start + left * y0, f.End + left * y0, f.End + left * y1, f.Start + left * y1];
        var bottom = Place.Elevation(m, footing) + type.Num("bottom", -1.8);
        return Prism.ExtrudeFootprint(fp, bottom, type.Num("footingHeight", 0.4) + type.Num("stemHeight", 1.4), "footing");
    }

    public static ElementGeometry? Geometry(Entity footing, IModelView m, IServices? services)
    {
        if (Place.TypeOf(m, footing.Id) is not { } type || !IsStrip(type)) return new ElementGeometry([], Diagnostics: ["A strip foundation needs its type."]);
        var paths = services?.Get<IPathElements>();
        var problems = new List<string>();
        var bottom = Place.Elevation(m, footing) + type.Num("bottom", -1.8);
        var fh = type.Num("footingHeight", 0.4);
        var sh = type.Num("stemHeight", 1.4);
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        // Straight, or along its arc.
        var c = PathCurve.Of(footing);
        IReadOnlyList<Vec2> Band(JoinWall f, (Vec2 Point, EndCut Cut)?[] ends, double y0, double y1) => c.IsArc ? Strips.Footprint(c, f, ends, y0, y1) : Strips.Footprint(f, ends, y0, y1);
        if (Frame(footing, m) is { } stem)
        {
            var ends = paths != null ? Joins.EndsOf(m, footing, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
            // Round an L corner the layers outside the core go on mitred, whatever the style; the cores join as it says.
            Strips.End?[]? layered = null, cores = null;
            if (paths != null && !c.IsArc)
            {
                var all = Strips.Ends(m, footing, paths, [], e => Frame(e, m));
                if (all.Any(x => x is { Kind: JoinKind.L }))
                {
                    layered = Strips.Ends(m, footing, paths, [], e => Frame(e, m), JoinStyle.Mitre);
                    cores = Strips.Ends(m, footing, paths, [], e => CoreFrame(e, m));
                    for (var k = 0; k < 2; k++)
                        if (all[k] is not { Kind: JoinKind.L }) { layered[k] = all[k]; cores[k] = all[k]; }
                }
            }
            var coreIndex = LayerSets.BandsOf(m, footing.Id)?.FirstOrDefault(b => b.Core) is { } cb && cb.To - cb.From > 1e-9 ? cb.Index : -1;
            foreach (var (from, to, material, name, index) in Bands(m, footing))
            {
                var fp = layered != null && cores != null
                    ? Strips.Footprint(stem.Start, stem.End, (index == coreIndex ? cores : layered)[0], (index == coreIndex ? cores : layered)[1], from, to)
                    : Band(stem, ends, from, to);
                if (Prism.ExtrudeFootprint(fp, bottom + fh, sh, name) is { } solid) parts.Add(new GeometryPart(name, Materials.ColorOf(m, material), solid, material, $"layer:{index}"));
                plan.Add(new PlanItem("outline", true, fp));
            }
        }
        if (FootingFrame(footing, m) is { } ff)
        {
            // The footing joins as its stem does, with its own width.
            var ends = paths != null ? Joins.EndsOf(m, footing, paths, problems, e => FootingFrame(e, m)) : new (Vec2 Point, EndCut Cut)?[2];
            var fp = Band(ff, ends, ff.YMin, ff.YMax);
            var concrete = type.Str("concrete");
            if (Prism.ExtrudeFootprint(fp, bottom, fh, "footing") is { } solid) parts.Add(new GeometryPart("Footing", Materials.ColorOf(m, concrete), solid, concrete, "footing"));
            plan.Add(new PlanItem("outline", true, fp));
        }
        parts = CutPriorities.Apply(footing, m, services, parts, problems);
        IReadOnlyList<Vec2> snaps = c.IsArc ? [c.Start, c.End, c.PointAt(c.Length / 2)] : [Joins.Start(footing), Joins.End(footing)];
        return new ElementGeometry(parts, plan, snaps, problems.Count > 0 ? problems : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity footing, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0 || Place.TypeOf(m, footing.Id) is not { } type) yield break;
        var length = PathCurve.Of(footing).Length;
        var fh = type.Num("footingHeight", 0.4);
        yield return new QuantitySet("Qto_FootingBaseQuantities",
        [
            new("Length", length, "LENGTH"),
            new("NetVolume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Net volume"),
        ]);
        foreach (var part in g.Parts)
        {
            var v = Measure.Volume(part.Solid);
            if (part.Key == "footing")
                yield return new QuantitySet("Qto_FootingBaseQuantities",
                [
                    new("Width", type.Num("footingWidth", 0.6), "LENGTH"),
                    new("Height", fh, "LENGTH"),
                    new("Area", fh > 0 ? v / fh : 0, "AREA", "Footprint"),
                    new("NetVolume", v, "VOLUME", "Net volume"),
                ], "footing", "Footing", part.Material);
            else
                yield return new QuantitySet("Lapin_StemLayerQuantities", [new("NetVolume", v, "VOLUME", "Net volume")], part.Key, part.Name, part.Material);
        }
    }
}

/// <summary>A step of a pad footing, bottom up: its length (along its direction), width and height.</summary>
public sealed record PadStep(double Length, double Width, double Height);

/// <summary>Stepped pad footings (the old Lapin's): steps from the bottom up, each centred on the footing's point, turned with it.</summary>
public static class PadFootings
{
    public static readonly IReadOnlyList<PadStep> DefaultSteps = [new(1.8, 1.8, 0.5), new(1, 1, 0.4)];

    public static bool IsPad(Entity type) => type.Str("kind") == "pad";

    public static IReadOnlyList<PadStep> Steps(Entity type) =>
        type.Attrs["steps"] is JsonArray arr
            ? arr.OfType<JsonObject>().Select(s => new PadStep(Json.Number(s["length"]) ?? 0, Json.Number(s["width"]) ?? 0, Json.Number(s["height"]) ?? 0)).ToList()
            : DefaultSteps;

    public static Entity CreateType(IEdit edit, string? name, IReadOnlyList<PadStep> steps, double bottom = -1.8, string concrete = "Reinforced concrete")
    {
        if (steps.Count == 0) throw new CommandException("A pad footing needs at least one step.");
        if (steps.Any(s => !(s.Length > 0 && s.Width > 0 && s.Height > 0))) throw new CommandException("Every step needs a length, a width and a height.");
        var type = new Entity(edit.NewId(), IfcClass.FootingType, string.IsNullOrWhiteSpace(name) ? $"PF {steps[0].Length * 100:0}×{steps[0].Width * 100:0}" : name.Trim(), new JsonObject
        {
            ["kind"] = "pad",
            ["steps"] = new JsonArray(steps.Select(s => (JsonNode)new JsonObject { ["length"] = s.Length, ["width"] = s.Width, ["height"] = s.Height }).ToArray()),
            ["bottom"] = bottom,
            ["concrete"] = Materials.Ensure(edit, concrete).Id,
        }, "PAD_FOOTING");
        edit.Add(type);
        return type;
    }

    public static Entity DefaultType(IEdit edit) => CreateType(edit, null, DefaultSteps);

    static IEnumerable<(PadStep Step, double Z)> Stack(Entity type, double bottom)
    {
        var z = bottom;
        foreach (var s in Steps(type)) { yield return (s, z); z += s.Height; }
    }

    public static Solid? Envelope(Entity pad, IModelView m)
    {
        if (Place.TypeOf(m, pad.Id) is not { } type || !IsPad(type)) return null;
        var steps = Steps(type);
        var bottom = Place.Elevation(m, pad) + type.Num("bottom", -1.8);
        var widest = steps.MaxBy(s => s.Length * s.Width)!;
        return Prism.ExtrudeFootprint(Place.Placed(Section.Rectangle(widest.Length, widest.Width).Outline(), Place.Point(pad), pad.Num("rotation")), bottom, steps.Sum(s => s.Height), "pad");
    }

    public static ElementGeometry? Geometry(Entity pad, IModelView m, IServices? services)
    {
        if (Place.TypeOf(m, pad.Id) is not { } type || !IsPad(type)) return new ElementGeometry([], Diagnostics: ["A pad footing needs its type."]);
        var concrete = type.Str("concrete");
        var at = Place.Point(pad);
        var parts = new List<GeometryPart>();
        var plan = new List<PlanItem>();
        var i = 0;
        foreach (var (step, z) in Stack(type, Place.Elevation(m, pad) + type.Num("bottom", -1.8)))
        {
            var fp = Place.Placed(Section.Rectangle(step.Length, step.Width).Outline(), at, pad.Num("rotation"));
            if (Prism.ExtrudeFootprint(fp, z, step.Height, "step") is { } solid) parts.Add(new GeometryPart($"Step {i + 1}", Materials.ColorOf(m, concrete), solid, concrete, $"step:{i}"));
            plan.Add(new PlanItem("outline", true, fp));
            i++;
        }
        var problems = new List<string>();
        parts = CutPriorities.Apply(pad, m, services, parts, problems);
        return new ElementGeometry(parts, plan, [at], problems.Count > 0 ? problems : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity pad, ElementGeometry? g, IModelView m)
    {
        if (g == null || Place.TypeOf(m, pad.Id) is not { } type || !IsPad(type)) yield break;
        var steps = Steps(type);
        // Sides of every step, the part of each step's top the next leaves open, and the top.
        var surface = 0.0;
        for (var i = 0; i < steps.Count; i++)
        {
            var s = steps[i];
            surface += 2 * (s.Length + s.Width) * s.Height;
            surface += s.Length * s.Width - (i + 1 < steps.Count ? steps[i + 1].Length * steps[i + 1].Width : 0);
        }
        yield return new QuantitySet("Qto_FootingBaseQuantities",
        [
            new("Height", steps.Sum(s => s.Height), "LENGTH"),
            new("OuterSurfaceArea", surface, "AREA", "Outer surface"),
            new("NetVolume", g.Parts.Sum(p => Measure.Volume(p.Solid)), "VOLUME", "Net volume"),
        ]);
    }
}
