using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// Foreign IFC columns, beams and pad footings rebuilt natively
/// (docs/spec-ifc.md, Recunoașterea). A column: a vertical prism with a
/// rectangular, regular polygonal or round base, its section and rotation from
/// the base, its base offset from the storey and its height its own where its
/// type's is another. A beam: a prism swept horizontally, its section from the
/// profile (a rectangle in its plane, else a reference), its axis from the
/// sweep; or swept vertically, its axis along the long side of the footprint;
/// of a box's prisms the one whose section is its file type's, else the
/// longest. A footing: a vertical prism on a rectangle, one step. Each keeps
/// the file's GlobalId as its id. The columns and beams of one file type share
/// a native type while their sections are the same (a column's rectangle
/// turned a quarter is the same); one of another section takes a type of its
/// own, named after the file's with its section.
/// </summary>
public sealed class ForeignMembers : IIfcRecognizer
{
    public string? Recognize(IEdit edit, ForeignElement e, RecognitionContext ctx) => e.IfcClass switch
    {
        IfcClass.Column => Column(edit, e, ctx),
        IfcClass.Beam or "IfcMember" => Beam(edit, e, ctx),
        IfcClass.Footing => Footing(edit, e, ctx),
        _ => null,
    };

    /// <summary>
    /// The beams joined as the file has them (<see cref="FoundJoins"/>, as the walls are): a join only where it leaves both
    /// on their footprints, the free ends square where the file's bodies end.
    /// </summary>
    public void Finish(IEdit edit, RecognitionContext ctx)
    {
        if (ctx.Services.Get<IPathElements>() is not { } paths) return;
        var beams = ctx.Made.Where(id => edit.Model.Get(id)?.Class == IfcClass.Beam).ToList();
        try { FoundJoins.Make(edit, paths, beams, ctx.Footprints); }
        catch (CommandException ex) { ctx.Notes.Add($"Beams not joined: {ex.Message}"); }
    }

    /// <summary>
    /// How near a chorded circle's area (as Lapin draws it) must be to the file's polygon to be read as round: the fidelity
    /// gate's default volume tolerance (docs/spec-ifc.md, Poarta de fidelitate), so a round column is never one it fails.
    /// </summary>
    const double RoundTolerance = 0.005;

    /// <summary>A column's section in plan, where it stands and how it is turned; null when it is none Lapin has.</summary>
    static (Section Section, Vec2 At, double Rotation)? ColumnSection(IReadOnlyList<Vec2> plan)
    {
        if (Footprints.Rectangle(plan) is { } r) return (Section.Rectangle(Round(r.Width), Round(r.Depth)), r.Centre, Math.Round(r.RotationDeg, 3));
        var area = Math.Abs(Polygon2.SignedArea(plan));
        bool Fits(Section c) => Math.Abs(Math.Abs(Polygon2.SignedArea(c.Outline())) - area) <= RoundTolerance * area;
        // A regular polygon: a faceted round column when Lapin's circle has its area, else the polygon itself (an octagonal post).
        if (Footprints.RegularPolygon(plan) is { } g)
        {
            var circle = Section.Circle(Round(2 * g.Radius));
            if (g.Sides >= 8 && Fits(circle)) return (circle, g.Centre, 0);
            if (g.Sides <= 64) return (Section.Polygon(Round(2 * g.Radius), g.Sides), g.Centre, Math.Round(g.RotationDeg, 3));
        }
        if (Footprints.Circle(plan) is { } c && Section.Circle(Round(c.Diameter)) is var round && Fits(round)) return (round, c.Centre, 0);
        return null;
    }

    /// <summary>Whether two sections are one (to 0.1 mm).</summary>
    static bool Same(Section? a, Section b) => a != null && a.Kind == b.Kind && a.Sides == b.Sides
        && Math.Abs(a.Width - b.Width) < 1e-4 && Math.Abs(a.Depth - b.Depth) < 1e-4 && Math.Abs(a.Web - b.Web) < 1e-4 && Math.Abs(a.Flange - b.Flange) < 1e-4;

    string? Column(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        if (!One(e, ctx, p => p.Vertical, "a column whose body is not one vertical prism", out var prism)) return null;
        if (prism.Holes.Count > 0) { ctx.Skip("a column with holes in its section"); return null; }
        if (ColumnSection(Footprints.Plan(prism.Outer)) is not { } read)
        {
            ctx.Skip("a column whose section is not a rectangle, a regular polygon or a circle");
            return null;
        }
        var (section, at, rotation) = read;
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a column without a storey"); return null; }
        var height = Round(prism.Height);
        // Its file type's section, or that rectangle turned a quarter (the file's first edge the other side).
        var turned = section.Kind == "rectangle" ? Section.Rectangle(section.Depth, section.Width) : null;
        var type = Type(edit, ctx, IfcClass.ColumnType, e, $"column:{section.Label}:{height}:{e.Material}", $"Column {section.Label}", section.Label,
            name => Columns.CreateType(edit, name, section, height, e.Material ?? (section.IsProfile ? "Steel" : "Reinforced concrete")),
            t => Section.From(t.Attrs["section"]) is var ts && (Same(ts, section) || turned != null && Same(ts, turned)));
        if (turned != null && !Same(Section.From(type.Attrs["section"]), section)) rotation = Math.Round(((rotation + 90) % 360 + 360) % 360, 3);
        // Where the file has it: its base as an offset from its storey, and its own height where its type's (the first of
        // its file type's columns) is another.
        var attrs = new JsonObject { ["at"] = Place.Json2(at), ["rotation"] = rotation };
        var baseOffset = Math.Round(prism.BaseZ - storey.Num("elevation"), 6);
        if (Math.Abs(baseOffset) > 1e-6) attrs["baseOffset"] = baseOffset;
        if (Math.Abs((type.NumOrNull("height") ?? 0) - height) > 1e-6) attrs["height"] = height;
        var column = new Entity(e.GlobalId, IfcClass.Column, e.Name ?? type.Name, attrs, "COLUMN");
        edit.Add(column);
        Spatial.Contain(edit, storey.Id, column.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, column.Id);
        ctx.Made.Add(column.Id);
        return column.Id;
    }

    string? Beam(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        static bool Lying(ShapePrism p) => p.Horizontal || p.Vertical;
        if (!One(e, ctx, Lying, "a beam whose body is not one prism", out var prism)) return null;
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a beam without a storey"); return null; }
        var key = TypeKey(IfcClass.BeamType, e);
        // A box is three prisms: the one whose section is its file type's (a short stub of a long beam's type), else the longest.
        if (key != null && ctx.TypeIds.TryGetValue(key, out var known) && Section.From(edit.Model.Get(known)?.Attrs["section"]) is { } typed)
        {
            foreach (var p in e.Prisms.Where(Lying).OrderByDescending(p => p.Height))
                if (ReadBeam(p) is { } read && Same(typed, Section.Rectangle(read.Width, read.Depth))) { prism = p; break; }
        }
        if (ReadBeam(prism, ctx) is not { } beamRead) return null;
        var (start, end, width, depth, top, footprint) = beamRead;
        var section = Section.Rectangle(width, depth);
        var type = Type(edit, ctx, IfcClass.BeamType, e, $"beam:{section.Label}:{e.Material}", $"Beam {section.Label}", section.Label,
            name => Beams.CreateType(edit, name, section, e.Material ?? "Reinforced concrete"), t => Same(Section.From(t.Attrs["section"]), section));
        var topOffset = Math.Round(top - storey.Num("elevation") - storey.Num("height", 3), 6);
        var beam = new Entity(e.GlobalId, IfcClass.Beam, e.Name ?? type.Name, new JsonObject { ["start"] = Place.Json2(start), ["end"] = Place.Json2(end), ["topOffset"] = topOffset }, "BEAM");
        edit.Add(beam);
        Spatial.Contain(edit, storey.Id, beam.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, beam.Id);
        if (footprint.Count >= 3) ctx.Footprints[beam.Id] = footprint;
        ctx.Made.Add(beam.Id);
        return beam.Id;
    }

    /// <summary>
    /// A beam read from one prism: its axis in plan, its section (width across, depth up), its top, and its footprint in plan
    /// (for the joins); null, with the reason when <paramref name="ctx"/> is given, when it is no beam Lapin has.
    /// </summary>
    static (Vec2 Start, Vec2 End, double Width, double Depth, double Top, IReadOnlyList<Vec2> Footprint)? ReadBeam(ShapePrism prism, RecognitionContext? ctx = null)
    {
        Vec2 start, end; double width, depth, top;
        IReadOnlyList<Vec2> footprint;
        if (prism.Horizontal)
        {
            // The profile stands in a vertical plane: width across the sweep in plan, depth up it.
            var dir = new Vec2(prism.Sweep.X, prism.Sweep.Y).Normalized();
            var across = new Vec2(-dir.Y, dir.X);
            var origin = new Vec2(prism.Outer[0].X, prism.Outer[0].Y);
            double xMin = double.MaxValue, xMax = double.MinValue, zMin = double.MaxValue, zMax = double.MinValue;
            foreach (var p in prism.Outer)
            {
                var x = Vec2.Dot(new Vec2(p.X, p.Y) - origin, across);
                xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x); zMin = Math.Min(zMin, p.Z); zMax = Math.Max(zMax, p.Z);
            }
            width = Round(xMax - xMin); depth = Round(zMax - zMin); top = zMax;
            // A native beam is a horizontal rectangle swept along its axis: a profile that is no rectangle in its plane (a T,
            // a haunch, or a sloping beam's parallelogram or turned rectangle read across its sweep) would come in as its bounding box.
            Vec2 InPlane(Vec3 p) => new(Vec2.Dot(new Vec2(p.X, p.Y) - origin, across), p.Z);
            var area = Math.Abs(Polygon2.SignedArea([.. prism.Outer.Select(InPlane)])) - prism.Holes.Sum(h => Math.Abs(Polygon2.SignedArea([.. h.Select(InPlane)])));
            if (area < 0.99 * (xMax - xMin) * (zMax - zMin)) { ctx?.Skip("a beam whose profile is not a rectangle (a shaped section, or a sloping beam)"); return null; }
            var centre = origin + across * ((xMin + xMax) / 2);
            start = centre; end = centre + dir * prism.Height;
            var (a0, a1) = (centre + across * ((xMin - xMax) / 2), centre + across * ((xMax - xMin) / 2));
            footprint = [a0, a1, a1 + dir * prism.Height, a0 + dir * prism.Height];
        }
        else
        {
            var plan = Footprints.Plan(prism.Outer);
            if (Footprints.Axis(plan) is not { } axis || axis.Coverage < 0.5 || axis.Thickness > (axis.End - axis.Start).Length) { ctx?.Skip("a beam whose footprint has no axis"); return null; }
            (start, end) = (axis.Start, axis.End);
            width = Round(axis.Thickness); depth = Round(prism.Height); top = prism.TopZ;
            footprint = plan;
        }
        if (!(width > 0 && depth > 0) || (end - start).Length < 1e-3) { ctx?.Skip("a beam without a section"); return null; }
        return (start, end, width, depth, top, footprint);
    }

    /// <summary>
    /// A long rectangle: a strip foundation along its middle (decision 214). Its section stays the file's rectangle: a core
    /// as wide as it on a footing of the same width, half its height each, the bottom where the file has it.
    /// </summary>
    string? StripFooting(IEdit edit, ForeignElement e, ShapePrism prism, (Vec2 Centre, double Width, double Depth, double RotationDeg) r, Entity storey, RecognitionContext ctx)
    {
        var angle = r.RotationDeg * Math.PI / 180;
        var along = new Vec2(Math.Cos(angle), Math.Sin(angle));
        var (length, width) = r.Width >= r.Depth ? (r.Width, r.Depth) : (r.Depth, r.Width);
        if (r.Width < r.Depth) along = along.Perp;
        var height = Round(prism.Height);
        var bottom = Math.Round(prism.BaseZ - storey.Num("elevation"), 6);
        var material = e.Material ?? "Reinforced concrete";
        var type = Type(edit, ctx, IfcClass.FootingType, e, $"strip:{Round(width)}:{height}:{bottom}", $"SF {width * 100:0}×{height * 100:0}",
            name => StripFootings.CreateType(edit, name, [new LayerSpec(material, Round(width), Core: true)], height / 2, Round(width), height / 2, bottom, material));
        var (start, end) = (r.Centre - along * (length / 2), r.Centre + along * (length / 2));
        var footing = new Entity(e.GlobalId, IfcClass.Footing, e.Name ?? type.Name, new JsonObject { ["start"] = Place.Json2(start), ["end"] = Place.Json2(end) }, "STRIP_FOOTING");
        edit.Add(footing);
        Spatial.Contain(edit, storey.Id, footing.Id);
        StripFootings.SetType(edit, footing, type);
        ctx.Made.Add(footing.Id);
        return footing.Id;
    }

    string? Footing(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        if (!One(e, ctx, p => p.Vertical, "a footing whose body is not one vertical prism", out var prism)) return null;
        var plan = Footprints.Plan(prism.Outer);
        if (Footprints.Rectangle(plan) is not { } r) { ctx.Skip("a footing that is not a rectangle"); return null; }
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a footing without a storey"); return null; }
        if (Math.Max(r.Width, r.Depth) > 4 * Math.Min(r.Width, r.Depth)) return StripFooting(edit, e, prism, r, storey, ctx);
        var height = Round(prism.Height);
        var bottom = Math.Round(prism.BaseZ - storey.Num("elevation"), 6);
        var step = new PadStep(Round(r.Width), Round(r.Depth), height);
        var type = Type(edit, ctx, IfcClass.FootingType, e, $"pad:{step.Length}:{step.Width}:{step.Height}:{bottom}", $"PF {step.Length * 100:0}×{step.Width * 100:0}",
            name => PadFootings.CreateType(edit, name, [step], bottom, e.Material ?? "Reinforced concrete"));
        var footing = new Entity(e.GlobalId, IfcClass.Footing, e.Name ?? type.Name, new JsonObject { ["at"] = Place.Json2(r.Centre), ["rotation"] = Math.Round(r.RotationDeg, 3) }, "PAD_FOOTING");
        edit.Add(footing);
        Spatial.Contain(edit, storey.Id, footing.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, footing.Id);
        ctx.Made.Add(footing.Id);
        return footing.Id;
    }

    /// <summary>The key of the element's file type among the types this import made, when it has one.</summary>
    static string? TypeKey(string cls, ForeignElement e) => e.TypeName is { Length: > 0 } tn ? $"{cls}:type:{tn}" : null;

    /// <summary>
    /// The native type for the element: its file type's (the first made for it) when it <paramref name="fits"/>, else one
    /// of the file type's for this element's section (<paramref name="label"/>, named after both); without a file type, one
    /// by <paramref name="key"/> (what makes it).
    /// </summary>
    static Entity Type(IEdit edit, RecognitionContext ctx, string cls, ForeignElement e, string key, string fallbackName, string label, Func<string, Entity> create, Func<Entity, bool> fits)
    {
        var name = fallbackName;
        if (TypeKey(cls, e) is { } typed)
        {
            name = e.TypeName!;
            key = typed;
            if (ctx.TypeIds.TryGetValue(key, out var first) && edit.Model.Get(first) is { } t && !fits(t)) { key = $"{typed}:{label}"; name = $"{e.TypeName} {label}"; }
        }
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var type = create(name);
        ctx.CreatedTypes.Add(type.Id);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    static Entity Type(IEdit edit, RecognitionContext ctx, string cls, ForeignElement e, string key, string fallbackName, Func<string, Entity> create) =>
        Type(edit, ctx, cls, e, key, fallbackName, "", create, _ => true);

    static double Round(double v) => Math.Round(v, 4);

    /// <summary>The box a prism fills: its base and its base swept.</summary>
    static (Vec3 Min, Vec3 Max) Span(ShapePrism p)
    {
        var pts = p.Outer.Concat(p.Outer.Select(q => q + p.Sweep)).ToList();
        return (new Vec3(pts.Min(q => q.X), pts.Min(q => q.Y), pts.Min(q => q.Z)), new Vec3(pts.Max(q => q.X), pts.Max(q => q.Y), pts.Max(q => q.Z)));
    }

    static bool One(ForeignElement e, RecognitionContext ctx, Func<ShapePrism, bool> wanted, string reason, out ShapePrism prism)
    {
        prism = null!;
        if (e.Prisms.Count == 0) { ctx.Skip(e.ShapeNote is { } why ? $"{reason.Split(" whose")[0]}: {why}" : reason); return false; }
        // A B-rep's prisms (a box's three) all span its box: prisms spanning different ones are of several B-reps.
        var breps = e.Prisms.Where(p => p.Source == "brep").Select(Span).ToList();
        var oneBrep = breps.All(b => (b.Min - breps[0].Min).Length <= 1e-3 && (b.Max - breps[0].Max).Length <= 1e-3);
        var distinct = e.Prisms.Count(p => p.Source != "brep") + (breps.Count == 0 ? 0 : oneBrep ? 1 : 2);
        if (distinct > 1) { ctx.Skip($"{reason.Split(" whose")[0]} made of several bodies"); return false; }
        var candidates = e.Prisms.Where(wanted).ToList();
        if (candidates.Count == 0) { ctx.Skip(reason); return false; }
        // A box gives three prisms: for a column the vertical one; for a beam the longest sweep.
        prism = candidates.OrderByDescending(p => wanted(p) && p.Vertical && e.IfcClass != IfcClass.Beam ? 1 : 0).ThenByDescending(p => p.Height).First();
        return true;
    }
}
