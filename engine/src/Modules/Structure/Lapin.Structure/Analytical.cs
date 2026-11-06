using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;

namespace Lapin.Structure;

/// <summary>
/// The structure's members in the analytical model (docs/jg-structures.md): each column from its foot up its height on its
/// point, its section's depth along its rotation; each beam on its whole axis (column centre to column centre), its centroid
/// half its depth under its top, a curved one as chords; pad footings as supports under their footprint, strip footings
/// along their axis, fixed (a footing cast with what stands on it).
/// </summary>
public sealed class StructureAnalytical : IAnalyticalElements
{
    public void Contribute(IModelView m, AnalyticalContribution into)
    {
        foreach (var c in m.OfClass(IfcClass.Column))
        {
            var (section, material, height) = Columns.Spec(m, c);
            if (!(height > 0)) { into.LeftOut.Add(new(c.Id, c.Name, c.Class, "no height")); continue; }
            var at = Place.Point(c);
            var z = Place.Elevation(m, c) + c.Num("baseOffset");
            var r = c.Num("rotation") * Math.PI / 180;
            // The section's depth (its outline's y) turned by the column's rotation.
            var depth = new Vec3(-Math.Sin(r), Math.Cos(r), 0);
            into.Frames.Add(new AnalyticalFrame(c.Id, c.Name, c.Class, "column", AnalyticalKinds.Columns, [at.At(z), at.At(z + height)], depth,
                SectionOf(m, c, section, "Column"), material, Spatial.StoreyOf(m, c.Id)?.Id, section.Depth, section.Width));
        }
        foreach (var b in m.OfClass(IfcClass.Beam))
        {
            var (section, material) = Beams.Spec(m, b);
            var curve = PathCurve.Of(b);
            if (curve.Length < 1e-3) { into.LeftOut.Add(new(b.Id, b.Name, b.Class, "no length")); continue; }
            var z = Beams.Top(m, b) - section.Depth / 2;
            var pts = (curve.IsArc ? curve.Stations(0, curve.Length).Select(curve.PointAt) : [curve.Start, curve.End]).Select(p => p.At(z)).ToList();
            into.Frames.Add(new AnalyticalFrame(b.Id, b.Name, b.Class, "beam", AnalyticalKinds.Beams, pts, Vec3.UnitZ, SectionOf(m, b, section, "Beam"), material,
                Spatial.StoreyOf(m, b.Id)?.Id, section.Depth, section.Width, Mirrored: b.Bool("flipped"),
                Note: curve.IsArc ? $"curved: {pts.Count - 1} chords, at most 1 mm off the arc" : null));
        }
        foreach (var f in m.OfClass(IfcClass.Footing))
        {
            var pad = f.PredefinedType == "PAD_FOOTING" || Place.TypeOf(m, f.Id) is { } t && PadFootings.IsPad(t);
            var solid = pad ? PadFootings.Envelope(f, m) : StripFootings.Envelope(f, m);
            if (solid == null || Measure.Bounds(solid) is not { } box) { into.LeftOut.Add(new(f.Id, f.Name, f.Class, "no body")); continue; }
            var spec = SupportSpec.Fixed($"footing „{f.Name}” ({(pad ? "pad" : "strip")}): fixed, cast with what stands on it");
            if (pad)
                into.Supports.Add(new AnalyticalSupport(f.Id, f.Name, spec, Region: [box.Min.Xy, new Vec2(box.Max.X, box.Min.Y), box.Max.Xy, new Vec2(box.Min.X, box.Max.Y)],
                    Bottom: box.Min.Z, Top: box.Max.Z));
            else if (StripFootings.Frame(f, m) is { } frame)
                into.Supports.Add(new AnalyticalSupport(f.Id, f.Name, spec, LineA: frame.Start, LineB: frame.End, Width: frame.YMax - frame.YMin, Bottom: box.Min.Z, Top: box.Max.Z));
        }
    }

    /// <summary>A member's section, keyed by its type (one section a type), its catalogue name kept when its dimensions are the catalogue's.</summary>
    internal static FrameSection SectionOf(IModelView m, Entity e, Section s, string what)
    {
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        return new FrameSection(type?.Id ?? $"{s.Kind}-{e.Id}", type?.Name ?? s.Label, Shape(s), type != null ? $"{what} type „{type.Name}”, section {s.Label}" : $"{what} „{e.Name}”, section {s.Label}");
    }

    /// <summary>The section as the analytical model reads it; a catalogue designation only when the dimensions are exactly the catalogue's.</summary>
    public static SectionShape Shape(Section s)
    {
        var designation = s.Name != null && Section.Catalogue.TryGetValue(s.Name, out var c)
            && Math.Abs(c.Width - s.Width) < 1e-9 && Math.Abs(c.Depth - s.Depth) < 1e-9 && Math.Abs(c.Web - s.Web) < 1e-9 && Math.Abs(c.Flange - s.Flange) < 1e-9 && c.Kind == s.Kind
            ? c.Name : null;
        return new SectionShape(s.Kind, s.Width, s.Depth, s.Web, s.Flange, s.Lip, designation);
    }
}
