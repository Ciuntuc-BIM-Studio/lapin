using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>
/// Columns: an IfcColumn stands at a point of its storey, turned by its
/// rotation, its section from its IfcColumnType, as tall as the storey (or
/// its type's or its own height).
/// </summary>
public static class Columns
{
    public static Entity CreateType(IEdit edit, string? name, Section section, double? height, string material = "Reinforced concrete")
    {
        var label = section.Kind == "circle" ? $"Ø{section.Width * 1000:0}" : $"{section.Width * 100:0}×{section.Depth * 100:0}";
        var attrs = new JsonObject { ["section"] = section.ToJson(), ["material"] = Materials.Ensure(edit, material).Id };
        if (height != null) attrs["height"] = height;
        var type = new Entity(edit.NewId(), IfcClass.ColumnType, string.IsNullOrWhiteSpace(name) ? $"Column {label}" : name.Trim(), attrs, "COLUMN");
        edit.Add(type);
        return type;
    }

    public static Entity DefaultType(IEdit edit) => CreateType(edit, null, Section.Rectangle(0.3, 0.3), null);

    public static (Section Section, string? Material, double Height) Spec(IModelView m, Entity column)
    {
        var type = Place.TypeOf(m, column.Id);
        var section = Section.From(type?.Attrs["section"]) ?? Section.Rectangle(0.3, 0.3);
        var height = column.NumOrNull("height") ?? type?.NumOrNull("height") ?? Place.StoreyHeight(m, column);
        return (section, type?.Str("material"), height);
    }

    public static Solid? Envelope(Entity column, IModelView m)
    {
        var (section, _, height) = Spec(m, column);
        var z = Place.Elevation(m, column) + column.Num("baseOffset");
        return Prism.ExtrudeFootprint(Place.Placed(section.Outline(), Place.Point(column), column.Num("rotation")), z, height, "column");
    }

    public static ElementGeometry? Geometry(Entity column, IModelView m, IServices? services)
    {
        var (section, material, _) = Spec(m, column);
        if (Envelope(column, m) is not { } solid) return new ElementGeometry([], Diagnostics: ["The column has no height."]);
        var problems = new List<string>();
        var parts = Place.CutByPriority(column, m, services, [new GeometryPart(column.Name, Materials.ColorOf(m, material), solid, material, "body")], problems);
        var footprint = Place.Placed(section.Outline(), Place.Point(column), column.Num("rotation"));
        return new ElementGeometry(parts, [new PlanItem("cut", true, footprint)], [Place.Point(column)], problems.Count > 0 ? problems : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity column, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var (section, _, height) = Spec(m, column);
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Qto_ColumnBaseQuantities",
        [
            new("Length", height, "LENGTH"),
            new("CrossSectionArea", section.Area, "AREA", "Cross section"),
            new("GrossVolume", section.Area * height, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
    }
}

/// <summary>
/// Beams: an IfcBeam runs along its axis, its section from its IfcBeamType,
/// its top at the level above its storey (plus its own offset). Beams join
/// in plan at corners and in T as walls do; a beam is a path element.
/// </summary>
public static class Beams
{
    public static Entity CreateType(IEdit edit, string? name, Section section, string material = "Reinforced concrete")
    {
        var type = new Entity(edit.NewId(), IfcClass.BeamType,
            string.IsNullOrWhiteSpace(name) ? $"Beam {section.Width * 100:0}×{section.Depth * 100:0}" : name.Trim(),
            new JsonObject { ["section"] = section.ToJson(), ["material"] = Materials.Ensure(edit, material).Id }, "BEAM");
        edit.Add(type);
        return type;
    }

    public static Entity DefaultType(IEdit edit) => CreateType(edit, null, Section.Rectangle(0.25, 0.4));

    public static (Section Section, string? Material) Spec(IModelView m, Entity beam)
    {
        var type = Place.TypeOf(m, beam.Id);
        return (Section.From(type?.Attrs["section"]) ?? Section.Rectangle(0.25, 0.4), type?.Str("material"));
    }

    public static double Top(IModelView m, Entity beam) => Place.Elevation(m, beam) + Place.StoreyHeight(m, beam) + beam.Num("topOffset");

    public static JoinWall? Frame(Entity beam, IModelView m)
    {
        var (s, e) = (Joins.Start(beam), Joins.End(beam));
        if ((e - s).Length < 1e-3) return null;
        var w = Spec(m, beam).Section.Width;
        return new JoinWall(s, e, -w / 2, w / 2);
    }

    public static Solid? Envelope(Entity beam, IModelView m)
    {
        if (Frame(beam, m) is not { } f) return null;
        var left = (f.End - f.Start).Normalized().Perp;
        var depth = Spec(m, beam).Section.Depth;
        Vec2[] fp = [f.Start + left * f.YMin, f.End + left * f.YMin, f.End + left * f.YMax, f.Start + left * f.YMax];
        return Prism.ExtrudeFootprint(fp, Top(m, beam) - depth, depth, "beam");
    }

    public static ElementGeometry? Geometry(Entity beam, IModelView m, IServices? services)
    {
        var problems = new List<string>();
        var (section, material) = Spec(m, beam);
        if (Frame(beam, m) is not { } f) return new ElementGeometry([], Diagnostics: ["The beam has no length."]);
        var ends = services?.Get<IPathElements>() is { } paths ? Joins.EndsOf(m, beam, paths, problems) : new (Vec2 Point, EndCut Cut)?[2];
        var fp = Strips.Footprint(f, ends, f.YMin, f.YMax);
        if (Prism.ExtrudeFootprint(fp, Top(m, beam) - section.Depth, section.Depth, "beam") is not { } solid) return new ElementGeometry([], Diagnostics: ["The beam has no section."]);
        var parts = Place.CutByPriority(beam, m, services, [new GeometryPart(beam.Name, Materials.ColorOf(m, material), solid, material, "body")], problems);
        return new ElementGeometry(parts, [new PlanItem("outline", true, fp)], [f.Start, f.End], problems.Count > 0 ? problems : null);
    }

    public static IEnumerable<QuantitySet> Quantities(Entity beam, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0) yield break;
        var section = Spec(m, beam).Section;
        var length = (Joins.End(beam) - Joins.Start(beam)).Length;
        var net = g.Parts.Sum(p => Measure.Volume(p.Solid));
        yield return new QuantitySet("Qto_BeamBaseQuantities",
        [
            new("Length", length, "LENGTH"),
            new("CrossSectionArea", section.Area, "AREA", "Cross section"),
            new("GrossVolume", section.Area * length, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
        ]);
    }
}
