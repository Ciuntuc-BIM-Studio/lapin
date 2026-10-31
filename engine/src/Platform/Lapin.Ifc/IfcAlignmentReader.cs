using Lapin.Geometry;
using Lapin.Ifc;

namespace Lapin.Alignments;

/// <summary>An IfcAlignment read from a file: its entity id, its name and its semantic layers.</summary>
public sealed record IfcAlignmentRead(int Id, string? Name, Alignment Alignment);

/// <summary>
/// Reading IFC 4.3 alignments' semantic segments from a STEP file — the
/// business-logic layer, not the generated geometry: IfcAlignment →
/// (IfcRelNests) IfcAlignmentHorizontal / Vertical / Cant → (IfcRelNests,
/// ordered) IfcAlignmentSegment → DesignParameters. Plane angles are converted
/// to radians when the file's PLANEANGLEUNIT is a degree; lengths to metres
/// when LENGTHUNIT is an SI prefix of the metre. The zero-length segment IFC
/// 4.3 ends each layer with is dropped.
/// </summary>
public static class IfcAlignmentReader
{
    public static List<IfcAlignmentRead> Read(string text) => Read(StepFile.Parse(text));

    public static List<IfcAlignmentRead> Read(StepFile f)
    {
        var (length, angle) = Units(f);
        var o = new List<IfcAlignmentRead>();
        foreach (var a in f.OfType("IFCALIGNMENT"))
        {
            IReadOnlyList<HorizontalSegment> horizontal = [];
            List<VerticalSegment>? vertical = null;
            CantLayer? cant = null;
            foreach (var layer in Nested(f, a.Id))
            {
                var designs = Nested(f, layer.Id).Where(s => s.Type == "IFCALIGNMENTSEGMENT").Select(s => f.Get(s[s.Args.Count - 1])).OfType<StepEntity>().ToList();
                switch (layer.Type)
                {
                    case "IFCALIGNMENTHORIZONTAL": horizontal = designs.Where(d => d.Type == "IFCALIGNMENTHORIZONTALSEGMENT").Select(d => Horizontal(f, d, length, angle)).ToList(); break;
                    case "IFCALIGNMENTVERTICAL": vertical = designs.Where(d => d.Type == "IFCALIGNMENTVERTICALSEGMENT").Select(d => Vertical(d, length)).ToList(); break;
                    case "IFCALIGNMENTCANT":
                        cant = new(Num(layer[layer.Args.Count - 1]) * length, designs.Where(d => d.Type == "IFCALIGNMENTCANTSEGMENT").Select(d => CantOf(d, length)).ToList());
                        break;
                }
            }
            // IFC 4.3 closes every layer with a zero-length segment (the concept template "Alignment Layout"): not a segment of the design.
            if (horizontal.Count > 1 && horizontal[^1].Length == 0) horizontal = horizontal.Take(horizontal.Count - 1).ToList();
            if (vertical is { Count: > 1 } && vertical[^1].HorizontalLength == 0) vertical.RemoveAt(vertical.Count - 1);
            if (cant is { Segments.Count: > 1 } && cant.Segments[^1].HorizontalLength == 0) cant = cant with { Segments = cant.Segments.Take(cant.Segments.Count - 1).ToList() };
            o.Add(new(a.Id, a[2] as string, new Alignment(horizontal, vertical, cant)));
        }
        return o;
    }

    static (double Length, double Angle) Units(StepFile f)
    {
        double length = 1, angle = 1;
        foreach (var e in f.OfType("IFCSIUNIT"))
            if (e[1] is StepEnum { Value: "LENGTHUNIT" } && e[2] is StepEnum p)
                length = p.Value switch { "MILLI" => 1e-3, "CENTI" => 1e-2, "DECI" => 1e-1, "KILO" => 1e3, _ => 1 };
        foreach (var e in f.OfType("IFCCONVERSIONBASEDUNIT"))
            if (e[1] is StepEnum { Value: "PLANEANGLEUNIT" })
                angle = f.Get(e[3]) is { } factor ? Num(factor[0], Math.PI / 180) : Math.PI / 180;
        return (length, angle);
    }

    /// <summary>Children of an object through IfcRelNests, in their stored order.</summary>
    static List<StepEntity> Nested(StepFile f, int id)
    {
        var o = new List<StepEntity>();
        foreach (var e in f.OfType("IFCRELNESTS"))
        {
            if (e[4] is not StepRef r || r.Id != id) continue;
            foreach (var c in e[5] as List<object?> ?? []) if (f.Get(c) is { } child) o.Add(child);
        }
        return o;
    }

    static double Num(object? v, double fallback = 0) => v switch
    {
        double d => d,
        long l => l,
        int i => i,
        StepTyped t => Num(t.Value, fallback),
        _ => fallback,
    };
    static double? OptNum(object? v) => v == null ? null : Num(v);

    static HorizontalSegment Horizontal(StepFile f, StepEntity e, double u, double angle)
    {
        var xy = f.Get(e[2])?[0] as List<object?>;
        var start = xy is { Count: >= 2 } ? new Vec2(Num(xy[0]) * u, Num(xy[1]) * u) : new Vec2(0, 0);
        return new(SegmentTypes.Horizontal((e[8] as StepEnum)?.Value) ?? HorizontalType.Line, start, Num(e[3]) * angle, Num(e[4]) * u, Num(e[5]) * u, Num(e[6]) * u, OptNum(e[7]) is { } h ? h * u : null);
    }

    static VerticalSegment Vertical(StepEntity e, double u) =>
        new(SegmentTypes.Vertical((e[8] as StepEnum)?.Value) ?? VerticalType.ConstantGradient, Num(e[2]) * u, Num(e[3]) * u, Num(e[4]) * u, Num(e[5]), Num(e[6]), OptNum(e[7]) is { } r ? r * u : null);

    static CantSegment CantOf(StepEntity e, double u) =>
        new(SegmentTypes.Cant((e[8] as StepEnum)?.Value) ?? CantType.ConstantCant, Num(e[2]) * u, Num(e[3]) * u, Num(e[4]) * u, OptNum(e[5]) is { } l2 ? l2 * u : null, Num(e[6]) * u, OptNum(e[7]) is { } r2 ? r2 * u : null);
}
