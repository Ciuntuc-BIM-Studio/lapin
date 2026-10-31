using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// Foreign IFC columns, beams and pad footings rebuilt natively
/// (docs/spec-ifc.md, Recunoașterea). A column: a vertical prism with a
/// rectangular or round base, its section and rotation from the base. A
/// beam: a prism swept horizontally, its section from the profile, its axis
/// from the sweep; or swept vertically, its axis along the long side of the
/// footprint. A footing: a vertical prism on a rectangle, one step. Each
/// keeps the file's GlobalId as its id.
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

    public void Finish(IEdit edit, RecognitionContext ctx)
    {
        if (ctx.Services.Get<IPathElements>() is not { } paths) return;
        foreach (var id in ctx.Made.Where(id => edit.Model.Get(id)?.Class == IfcClass.Beam).ToList())
        {
            try { Joins.Auto(edit, paths, id); }
            catch (CommandException ex) { ctx.Notes.Add($"Beam {id}: {ex.Message}"); }
        }
    }

    string? Column(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        if (!One(e, ctx, p => p.Vertical, "a column whose body is not one vertical prism", out var prism)) return null;
        var plan = Footprints.Plan(prism.Outer);
        Section section; Vec2 at; double rotation = 0;
        if (Footprints.Rectangle(plan) is { } r) { section = Section.Rectangle(Round(r.Width), Round(r.Depth)); at = r.Centre; rotation = Math.Round(r.RotationDeg, 3); }
        else if (Footprints.Circle(plan) is { } c) { section = Section.Circle(Round(c.Diameter)); at = c.Centre; }
        else { ctx.Skip("a column whose section is not a rectangle or a circle"); return null; }
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a column without a storey"); return null; }
        var height = Round(prism.Height);
        var type = Type(edit, ctx, IfcClass.ColumnType, e, $"column:{section.Label}:{height}:{e.Material}", $"Column {section.Label}",
            name => Columns.CreateType(edit, name, section, height, e.Material ?? (section.IsProfile ? "Steel" : "Reinforced concrete")));
        var column = new Entity(e.GlobalId, IfcClass.Column, e.Name ?? type.Name, new JsonObject { ["at"] = Place.Json2(at), ["rotation"] = rotation }, "COLUMN");
        edit.Add(column);
        Spatial.Contain(edit, storey.Id, column.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, column.Id);
        if (Math.Abs(prism.BaseZ - storey.Num("elevation")) > 0.01) ctx.Notes.Add($"Column {e.Name ?? e.GlobalId}: its base is {prism.BaseZ - storey.Num("elevation"):+0.00;-0.00} m from the storey; it stands on the storey.");
        ctx.Made.Add(column.Id);
        return column.Id;
    }

    string? Beam(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        if (!One(e, ctx, p => p.Horizontal || p.Vertical, "a beam whose body is not one prism", out var prism)) return null;
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a beam without a storey"); return null; }
        Vec2 start, end; double width, depth, top;
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
            var centre = origin + across * ((xMin + xMax) / 2);
            start = centre; end = centre + dir * prism.Height;
        }
        else
        {
            if (Footprints.Axis(Footprints.Plan(prism.Outer)) is not { } axis || axis.Coverage < 0.5 || axis.Thickness > (axis.End - axis.Start).Length) { ctx.Skip("a beam whose footprint has no axis"); return null; }
            (start, end) = (axis.Start, axis.End);
            width = Round(axis.Thickness); depth = Round(prism.Height); top = prism.TopZ;
        }
        if (!(width > 0 && depth > 0) || (end - start).Length < 1e-3) { ctx.Skip("a beam without a section"); return null; }
        var section = Section.Rectangle(width, depth);
        var type = Type(edit, ctx, IfcClass.BeamType, e, $"beam:{section.Label}:{e.Material}", $"Beam {section.Label}",
            name => Beams.CreateType(edit, name, section, e.Material ?? "Reinforced concrete"));
        var topOffset = Math.Round(top - storey.Num("elevation") - storey.Num("height", 3), 6);
        var beam = new Entity(e.GlobalId, IfcClass.Beam, e.Name ?? type.Name, new JsonObject { ["start"] = Place.Json2(start), ["end"] = Place.Json2(end), ["topOffset"] = topOffset }, "BEAM");
        edit.Add(beam);
        Spatial.Contain(edit, storey.Id, beam.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, beam.Id);
        ctx.Made.Add(beam.Id);
        return beam.Id;
    }

    /// <summary>
    /// A long rectangle: a strip foundation along its middle (decision 210). Its section stays the file's rectangle: a core
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

    static Entity Type(IEdit edit, RecognitionContext ctx, string cls, ForeignElement e, string key, string fallbackName, Func<string, Entity> create)
    {
        key = e.TypeName is { Length: > 0 } tn ? $"{cls}:type:{tn}" : key;
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var name = e.TypeName is { Length: > 0 } n ? n : fallbackName;
        var type = create(name);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    static double Round(double v) => Math.Round(v, 4);

    static bool One(ForeignElement e, RecognitionContext ctx, Func<ShapePrism, bool> wanted, string reason, out ShapePrism prism)
    {
        prism = null!;
        if (e.Prisms.Count == 0) { ctx.Skip(e.ShapeNote is { } why ? $"{reason.Split(" whose")[0]}: {why}" : reason); return false; }
        var distinct = e.Prisms.Where(p => p.Source != "brep").Count() + (e.Prisms.Any(p => p.Source == "brep") ? 1 : 0);
        if (distinct > 1) { ctx.Skip($"{reason.Split(" whose")[0]} made of several bodies"); return false; }
        var candidates = e.Prisms.Where(wanted).ToList();
        if (candidates.Count == 0) { ctx.Skip(reason); return false; }
        // A box gives three prisms: for a column the vertical one; for a beam the longest sweep.
        prism = candidates.OrderByDescending(p => wanted(p) && p.Vertical && e.IfcClass != IfcClass.Beam ? 1 : 0).ThenByDescending(p => p.Height).First();
        return true;
    }
}
