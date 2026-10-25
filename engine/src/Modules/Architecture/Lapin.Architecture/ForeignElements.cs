using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// Foreign IFC walls and floors rebuilt natively (docs/spec-ifc.md,
/// Recunoașterea). A wall: from its vertical prism's footprint, the axis along
/// the long sides, the thickness across them, the height from the sweep; its
/// layers from the file's layer set, else one layer of its material; its
/// openings from their boxes in the wall's frame, filled with doors and
/// windows whose types come from the file's (or from their size). A floor:
/// the prism's base as the contour, its holes as openings, its height as the
/// thickness. Each keeps the file's GlobalId as its id.
/// </summary>
public sealed class ForeignElements : IIfcRecognizer
{
    const double MinThickness = 0.01, MaxThickness = 1.2, MinSlabThickness = 0.03, MaxSlabThickness = 1.5;

    public string? Recognize(IEdit edit, ForeignElement e, RecognitionContext ctx) => e.IfcClass switch
    {
        IfcClass.Wall or "IfcWallStandardCase" => Wall(edit, e, ctx),
        IfcClass.Slab => Slab(edit, e, ctx),
        _ => null,
    };

    /// <summary>Every wall made this import is joined to its neighbours, as a drawn wall is.</summary>
    public void Finish(IEdit edit, RecognitionContext ctx)
    {
        if (ctx.Services.Get<IPathElements>() is not { } paths) return;
        foreach (var id in ctx.Made.Where(id => edit.Model.Get(id)?.Class == IfcClass.Wall).ToList())
        {
            try { Joins.Auto(edit, paths, id); }
            catch (CommandException ex) { ctx.Notes.Add($"Wall {id}: {ex.Message}"); }
        }
    }

    // ─── Walls ────────────────────────────────────────────────────────────────

    /// <summary>
    /// A wall from what the file gives, in this order: its body as one vertical
    /// prism (the footprint gives thickness and, without an 'Axis', the axis);
    /// else its parts (a wall whose layers are IfcBuildingElementParts, each a
    /// prism: they become the layers, side by side); else its 'Axis' with its
    /// layer set and its body's box (a wall cut by a roof, or in several
    /// bodies). The file's 'Axis' is the reference line when it has one, and
    /// the file's layer usage places the layers against it; without them the
    /// axis runs along the middle of the footprint.
    /// </summary>
    string? Wall(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a wall without a storey"); return null; }
        var axis = e.Axis is { Count: >= 2 } a && (P(a[^1]) - P(a[0])).Length > 1e-3 ? (A: P(a[0]), B: P(a[^1])) : ((Vec2 A, Vec2 B)?)null;
        Vec2 start, end; double thickness, baseZ, height;
        List<LayerSpec> layers;
        (bool Negative, double Offset) usage;
        if (OnePrismOrNull(e, p => p.Vertical) is { } prism)
        {
            if (Footprints.Axis(Footprints.Plan(prism.Outer)) is not { } fp) { ctx.Skip("a wall without a readable footprint"); return null; }
            var dir = (fp.End - fp.Start).Normalized();
            if (axis is { } ax && Math.Abs(Cross((ax.B - ax.A).Normalized(), dir)) < 0.05)
            {
                (start, end) = (ax.A, ax.B);
                dir = (end - start).Normalized();
                var (nMin, nMax) = Across(Footprints.Plan(prism.Outer), start, dir);
                thickness = nMax - nMin;
                layers = Layers(e, thickness, e.Material ?? "Masonry");
                usage = e.Usage is { } u && Math.Abs(e.Layers?.Sum(l => l.Thickness) - thickness ?? 1) < 0.002 ? u : (false, nMin);
            }
            else
            {
                if (fp.Coverage < 0.5) { ctx.Skip("a wall whose footprint has no two long parallel sides (curved, or not a wall)"); return null; }
                (start, end, thickness) = (fp.Start, fp.End, fp.Thickness);
                layers = Layers(e, thickness, e.Material ?? "Masonry");
                usage = (false, -thickness / 2);
            }
            baseZ = prism.BaseZ; height = prism.Height;
        }
        else if (e.Parts is { Count: > 0 } allParts && axis is { } axP && allParts.Where(pt => pt.Extents != null).ToList() is { Count: > 0 } parts)
        {
            // A wall whose layers are parts: each part's extents across the axis is a band; side by side they are the layers.
            (start, end) = (axP.A, axP.B);
            var bands = parts.Select(pt => (From: pt.Extents!.Value.Across0, To: pt.Extents!.Value.Across1, Material: pt.Material ?? pt.Name ?? "Material", pt.Extents!.Value.Z0, pt.Extents!.Value.Z1)).OrderBy(b => b.From).ToList();
            bands = bands.Where(b => b.To - b.From >= 0.003).ToList();
            if (bands.Count == 0 || bands.Zip(bands.Skip(1)).Any(z => z.Second.From - z.First.To > 0.005 || z.First.To - z.Second.From > 0.6 * Math.Min(z.First.To - z.First.From, z.Second.To - z.Second.From)))
            { ctx.Skip("a wall whose parts are not layers side by side"); return null; }
            // Overlapping neighbours (studs within insulation) share: the thinner band yields.
            for (var i = 0; i + 1 < bands.Count; i++)
                if (bands[i].To > bands[i + 1].From)
                {
                    var cut = (bands[i].To + bands[i + 1].From) / 2;
                    bands[i] = bands[i] with { To = cut }; bands[i + 1] = bands[i + 1] with { From = cut };
                }
            thickness = bands[^1].To - bands[0].From;
            var core = bands.OrderByDescending(b => b.To - b.From).First();
            layers = bands.Select(b => new LayerSpec(b.Material, Math.Round(b.To - b.From, 4), b == core)).ToList();
            usage = (false, bands[0].From);
            baseZ = bands.Min(b => b.Z0); height = bands.Max(b => b.Z1) - baseZ;
        }
        else if (axis is { } ax5 && e.Extents is { } ext)
        {
            // A body that is not a prism (cut by a roof, in several bodies): the file's axis and layers, the body's extents for the rest.
            (start, end) = (ax5.A, ax5.B);
            var fromLayers = e.Layers is { Count: > 0 } ls ? ls.Sum(l => l.Thickness) : 0;
            thickness = fromLayers > 0 ? fromLayers : ext.Across1 - ext.Across0;
            layers = Layers(e, thickness, e.Material ?? "Masonry");
            usage = e.Usage ?? (false, fromLayers > 0 ? -thickness / 2 : ext.Across0);
            baseZ = ext.Z0; height = ext.Z1 - ext.Z0;
            ctx.Notes.Add($"{(e.Prisms.Count > 0 ? "a wall whose body is not one vertical prism" : "a wall: " + (e.ShapeNote ?? "no prism"))}: made from its axis, its layers and its body's extents ({e.Name ?? e.GlobalId}).");
        }
        else
        {
            var detail = e.Parts is { Count: > 0 } ps ? $" ({ps.Count} parts{(axis == null ? ", no axis" : "")})" : axis == null ? " (no axis)" : "";
            ctx.Skip((e.Prisms.Count == 0 && e.ShapeNote is { } why ? $"a wall: {why}" : e.Prisms.Count > 0 ? "a wall whose body is not one vertical prism" : "a wall without a body") + detail);
            return null;
        }
        var length = (end - start).Length;
        if (thickness < MinThickness || thickness > MaxThickness || length < Math.Min(thickness, 0.3)) { ctx.Skip("a wall whose footprint is not a wall's (too thick for its length)"); return null; }
        var baseOffset = Math.Round(baseZ - storey.Num("elevation"), 6);
        height = Math.Round(height, 6);
        if (!(height > 0)) { ctx.Skip("a wall without a height"); return null; }
        var type = WallType(edit, e, layers, thickness, ctx);
        var predefined = e.PredefinedType is "STANDARD" or "SOLIDWALL" or "PARTITIONING" or "ELEMENTEDWALL" or "PLUMBINGWALL" or "SHEAR" or "MOVABLE" or "PARAPET" or "POLYGONAL" ? e.PredefinedType : "STANDARD";
        var wall = new Entity(e.GlobalId, IfcClass.Wall, e.Name ?? type.Name, Walls.Attrs(start, end, height, baseOffset), predefined);
        edit.Add(wall);
        Spatial.Contain(edit, storey.Id, wall.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, wall.Id);
        // The layers where the file has them: the usage's sense and offset against the reference line.
        LayerSets.LayOut(edit, wall, type, "AXIS2", (_, _) => Math.Round(usage.Offset, 6), usage.Negative);
        ctx.Made.Add(wall.Id);
        var d = (end - start).Normalized();
        foreach (var op in e.Openings) Opening(edit, wall, start, d, storey.Num("elevation") + baseOffset, height, op, ctx);
        return wall.Id;
    }

    static Vec2 P(Vec3 v) => new(v.X, v.Y);
    static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>The polygon's extent across a line: the least and the greatest distance left of it (negative right).</summary>
    static (double Min, double Max) Across(IReadOnlyList<Vec2> plan, Vec2 origin, Vec2 dir)
    {
        var n = new Vec2(-dir.Y, dir.X);
        double min = double.MaxValue, max = double.MinValue;
        foreach (var q in plan) { var v = Vec2.Dot(q - origin, n); min = Math.Min(min, v); max = Math.Max(max, v); }
        return (min, max);
    }

    /// <summary>The file's layers when they add up to the thickness; else one layer of the material.</summary>
    static List<LayerSpec> Layers(ForeignElement e, double thickness, string material) =>
        e.Layers is { Count: > 0 } ls && Math.Abs(ls.Sum(l => l.Thickness) - thickness) < 0.002
            ? ls.Select(l => new LayerSpec(l.Material, Math.Round(l.Thickness, 4), l.Core)).ToList()
            : [new LayerSpec(material, Math.Round(thickness, 4), true)];

    /// <summary>The wall type: the file's type by name, else one per layers; made once per import, or found by name in the project.</summary>
    Entity WallType(IEdit edit, ForeignElement e, List<LayerSpec> layers, double thickness, RecognitionContext ctx)
    {
        if (!layers.Any(l => l.Core)) layers = layers.Select((l, i) => i == layers.IndexOf(layers.OrderByDescending(x => x.Thickness).First()) ? l with { Core = true } : l).ToList();
        var signature = string.Join("|", layers.Select(l => $"{l.Material}:{l.Thickness}"));
        var key = e.TypeName is { Length: > 0 } tn ? "wall:type:" + tn + ":" + signature : "wall:" + signature;
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var name = e.TypeName is { Length: > 0 } n ? n : layers.Count == 1 ? $"{layers[0].Material} {thickness * 1000:0}" : $"{string.Join(" + ", layers.Select(l => l.Material))} {thickness * 1000:0}";
        var existing = edit.Model.OfClass(IfcClass.WallType).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) && LayerSets.Of(edit.Model, t.Id) is { } set && Math.Abs(Lapin.Model.Layers.Read(set).Sum(l => l.Thickness) - thickness) < 1e-6);
        var type = existing ?? WallTypes.Create(edit, name, layers);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    /// <summary>An opening from its box in the wall's frame; a door or a window in it when the file has one, a bare opening else.</summary>
    void Opening(IEdit edit, Entity wall, Vec2 start, Vec2 dir, double baseZ, double wallHeight, ForeignOpening op, RecognitionContext ctx)
    {
        if (op.Prisms.Count == 0) { ctx.Skip("an opening without a prism body"); return; }
        double aMin = double.MaxValue, aMax = double.MinValue, zMin = double.MaxValue, zMax = double.MinValue;
        foreach (var p in op.Prisms)
            foreach (var v in p.Outer.Concat(p.Outer.Select(q => q + p.Sweep)))
            {
                var a = Vec2.Dot(new Vec2(v.X, v.Y) - start, dir);
                aMin = Math.Min(aMin, a); aMax = Math.Max(aMax, a); zMin = Math.Min(zMin, v.Z); zMax = Math.Max(zMax, v.Z);
            }
        var width = Math.Round(aMax - aMin, 4);
        var height = Math.Round(Math.Min(zMax, baseZ + wallHeight) - Math.Max(zMin, baseZ), 4);
        var along = Math.Round((aMin + aMax) / 2, 4);
        var sill = Math.Round(Math.Max(0, zMin - baseZ), 4);
        var kind = op.Fill is { } fl ? (fl.IfcClass.StartsWith("IfcDoor") ? "door" : "window") : "opening";
        if (kind == "door") sill = 0;
        var w = op.Fill?.Width ?? width; var h = op.Fill?.Height ?? height;
        if (!(w > 0 && h > 0)) { ctx.Skip($"a {kind} without a size"); return; }
        try { Openings.CheckFits(edit.Model, wall, along, sill, w, h); }
        catch (CommandException ex) { ctx.Skip($"a {kind} that does not fit its wall ({ex.Message.TrimEnd('.')})"); return; }
        var opening = new Entity(op.GlobalId is { Length: 22 } og && edit.Model.Get(og) == null ? og : edit.NewId(), IfcClass.OpeningElement, op.Name ?? "Opening",
            kind == "opening" ? new JsonObject { ["along"] = along, ["sill"] = sill, ["width"] = w, ["height"] = h } : new JsonObject { ["along"] = along, ["sill"] = sill }, "OPENING");
        edit.Add(opening);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelVoidsElement, wall.Id, [opening.Id]));
        if (op.Fill is not { } fill) { ctx.Made.Add(opening.Id); return; }
        var type = FillType(edit, fill, kind, w, h, kind == "window" ? sill : 0, ctx);
        var cls = kind == "door" ? IfcClass.Door : IfcClass.Window;
        var spec = JoinerySpec.From(type.Attrs["spec"]);
        var marks = edit.Model.OfClass(cls).Count() + 1;
        var right = fill.Operation is { } o && o.Contains("RIGHT", StringComparison.OrdinalIgnoreCase);
        var made = new Entity(fill.GlobalId is { Length: 22 } fg && edit.Model.Get(fg) == null ? fg : edit.NewId(), cls, fill.Name ?? $"{spec?.Mark}{marks}", new JsonObject { ["flipHand"] = right, ["flipSide"] = false }, kind == "door" ? "DOOR" : "WINDOW");
        edit.Add(made);
        edit.AddRelation(new Relation(edit.NewId(), IfcClass.RelFillsElement, opening.Id, [made.Id]));
        edit.Relate(IfcClass.RelDefinesByType, type.Id, made.Id);
        if (Spatial.StoreyOf(edit.Model, wall.Id) is { } storey) Spatial.Contain(edit, storey.Id, made.Id);
        var attrs = made.CloneAttrs();
        attrs["imported"] = new JsonObject { ["ifcClass"] = fill.IfcClass, ["name"] = fill.Name, ["typeName"] = fill.TypeName, ["operation"] = fill.Operation, ["properties"] = fill.Properties.DeepClone() };
        edit.Replace(made.With(attrs: attrs));
        ctx.Made.Add(made.Id);
    }

    /// <summary>A door or window type: the file's by name, else one per size and operation, from Lapin's default of the kind at that size.</summary>
    Entity FillType(IEdit edit, ForeignFill fill, string kind, double w, double h, double sill, RecognitionContext ctx)
    {
        var size = $"{w * 1000:0}×{h * 1000:0}";
        var key = fill.TypeName is { Length: > 0 } tn ? $"{kind}:type:{tn}:{size}" : $"{kind}:{size}:{fill.Operation}";
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var name = fill.TypeName is { Length: > 0 } n ? n : kind == "door" ? $"Door {size}" : $"Window {size}";
        var cls = kind == "door" ? IfcClass.DoorType : IfcClass.WindowType;
        var existing = edit.Model.OfClass(cls).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) && JoinerySpec.From(t.Attrs["spec"]) is { } s && Math.Abs(s.Width - w) < 1e-6 && Math.Abs(s.Height - h) < 1e-6);
        if (existing != null) { ctx.TypeIds[key] = existing.Id; return existing; }
        var spec = (kind == "door" ? JoinerySpec.DefaultDoor(name) : JoinerySpec.DefaultWindow(name)) with { Width = Math.Round(w, 4), Height = Math.Round(h, 4), SillHeight = Math.Round(sill, 4) };
        var type = Openings.CreateType(edit, spec);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    // ─── Floors ──────────────────────────────────────────────────────────────

    string? Slab(IEdit edit, ForeignElement e, RecognitionContext ctx)
    {
        if (!OnePrism(e, ctx, p => p.Vertical, "a floor whose body is not one vertical prism", out var prism)) return null;
        var thickness = Math.Round(prism.Height, 4);
        if (thickness < MinSlabThickness || thickness > MaxSlabThickness) { ctx.Skip("a floor whose thickness is not a floor's"); return null; }
        var contour = Footprints.Plan(prism.Outer);
        if (contour.Count < 3) { ctx.Skip("a floor without a readable contour"); return null; }
        var storey = edit.Model.Get(e.StoreyId);
        if (storey == null) { ctx.Skip("a floor without a storey"); return null; }
        var offset = Math.Round(prism.TopZ - storey.Num("elevation"), 6);
        var type = SlabType(edit, e, thickness, ctx);
        var slab = new Entity(e.GlobalId, IfcClass.Slab, e.Name ?? type.Name, new JsonObject { ["contour"] = Slabs.ContourJson(contour), ["offset"] = offset }, e.PredefinedType is "ROOF" or "LANDING" or "BASESLAB" ? e.PredefinedType : "FLOOR");
        edit.Add(slab);
        Spatial.Contain(edit, storey.Id, slab.Id);
        Slabs.SetType(edit, slab, type);
        foreach (var hole in prism.Holes)
        {
            var ring = Footprints.Plan(hole);
            if (ring.Count < 3) continue;
            try { Slabs.AddOpening(edit, slab, ring); } catch (CommandException ex) { ctx.Notes.Add($"Floor {e.Name}: {ex.Message}"); }
        }
        ctx.Made.Add(slab.Id);
        return slab.Id;
    }

    Entity SlabType(IEdit edit, ForeignElement e, double thickness, RecognitionContext ctx)
    {
        var layers = e.Layers is { Count: > 0 } ls && Math.Abs(ls.Sum(l => l.Thickness) - thickness) < 0.002
            ? ls.Select(l => new LayerSpec(l.Material, Math.Round(l.Thickness, 4), l.Core)).ToList()
            : [new LayerSpec(e.Material ?? "Reinforced concrete", thickness, true)];
        var key = e.TypeName is { Length: > 0 } tn ? "slab:type:" + tn : "slab:" + string.Join("|", layers.Select(l => $"{l.Material}:{l.Thickness}"));
        if (ctx.TypeIds.TryGetValue(key, out var id) && edit.Model.Get(id) is { } known) return known;
        var name = e.TypeName is { Length: > 0 } n ? n : $"{layers[0].Material} {thickness * 1000:0}";
        var existing = edit.Model.OfClass(IfcClass.SlabType).FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        var type = existing ?? Slabs.CreateType(edit, name, layers);
        ctx.TypeIds[key] = type.Id;
        return type;
    }

    // ─── Shared ──────────────────────────────────────────────────────────────

    /// <summary>The element's one prism of the wanted kind (a box offers three; one with a hole only one), or a reason.</summary>
    internal static bool OnePrism(ForeignElement e, RecognitionContext ctx, Func<ShapePrism, bool> wanted, string reason, out ShapePrism prism)
    {
        prism = null!;
        if (e.Prisms.Count == 0) { ctx.Skip(e.ShapeNote is { } why ? $"{Label(e)}: {why}" : reason); return false; }
        // Several decompositions of the same body (a B-rep box) share their vertices; several bodies do not.
        var bodies = e.Prisms.GroupBy(p => p.Source == "brep" ? Key(p) : Guid.NewGuid().ToString()).ToList();
        if (bodies.Count > 1) { ctx.Skip($"{Label(e)} made of several bodies"); return false; }
        var candidates = e.Prisms.Where(wanted).ToList();
        if (candidates.Count == 0) { ctx.Skip(reason); return false; }
        prism = candidates.OrderByDescending(p => Math.Abs(Footprints.Area(Footprints.Plan(p.Outer)))).First();
        return true;
    }

    /// <summary>The element's one prism of the wanted kind, or null (several bodies, none of the kind, no prism).</summary>
    static ShapePrism? OnePrismOrNull(ForeignElement e, Func<ShapePrism, bool> wanted)
    {
        if (e.Prisms.Count == 0) return null;
        var bodies = e.Prisms.GroupBy(p => p.Source == "brep" ? Key(p) : Guid.NewGuid().ToString()).Count();
        if (bodies > 1) return null;
        return e.Prisms.Where(wanted).OrderByDescending(p => Math.Abs(Footprints.Area(Footprints.Plan(p.Outer)))).FirstOrDefault();
    }

    static string Label(ForeignElement e) => e.IfcClass.Length > 3 ? "a " + e.IfcClass[3..].ToLowerInvariant() : e.IfcClass;

    static string Key(ShapePrism p)
    {
        var pts = p.Outer.Concat(p.Outer.Select(q => q + p.Sweep)).Select(q => (Math.Round(q.X, 4), Math.Round(q.Y, 4), Math.Round(q.Z, 4))).OrderBy(t => t).ToList();
        return string.Join(";", pts.Take(8));
    }
}
