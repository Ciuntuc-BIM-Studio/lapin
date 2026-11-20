using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// Unfolded roof covering plans (2D flat sheet layouts): projects each roof
/// plane (and unrolls a barrel vault) into true-scale 2D coordinates with
/// opening holes, edge lengths, dimensions, and net covering areas, integrated
/// with <see cref="IElementViewRoles"/> and <see cref="IViewAnnotations"/>.
/// </summary>
public static class RoofUnfold
{
    public const string ViewKey = "roofUnfold";
    const double SheetGap = 1.0;

    /// <summary>One unfolded roof plane or barrel sheet in 2D layout coordinates.</summary>
    public sealed record Sheet(
        int Index,
        bool Barrel,
        double Pitch,
        double Width,
        double SlopeLength,
        double GrossArea,
        double NetArea,
        IReadOnlyList<Vec2> Outer,
        IReadOnlyList<IReadOnlyList<Vec2>> Holes,
        IReadOnlyList<double> EdgeLengths,
        Vec2 Min,
        Vec2 Max);

    /// <summary>All unfolded sheets for a roof, arranged without overlap.</summary>
    public sealed record UnfoldedLayout(
        Entity Roof,
        IReadOnlyList<Sheet> Sheets,
        double TotalGrossArea,
        double TotalNetArea);

    /// <summary>Computes the unfolded 2D covering sheets for <paramref name="roof"/>.</summary>
    public static UnfoldedLayout? Layout(IModelView m, Entity roof)
    {
        var r = Roof.Resolve(roof, m);
        if (r == null) return null;

        var raw = new List<(bool Barrel, double Pitch, List<Vec2> Outer, List<List<Vec2>> Holes)>();
        if (r.Flat)
        {
            var eaves = Roof.EaveOutline(r).ToList();
            if (eaves.Count < 3) return null;
            raw.Add((false, 0, eaves, []));
        }
        else
        {
            var (shape, _) = Roof.Solve(r);
            if (shape == null) return null;

            if (r.Barrel && shape.Arc is { } arc)
            {
                var barrel = UnfoldBarrel(shape, arc);
                if (barrel != null) raw.Add(barrel.Value);
            }
            else
            {
                var diags = new List<string>();
                var openings = RoofOpenings.Place(roof, shape, m, diags);
                for (var i = 0; i < shape.Planes.Count; i++)
                {
                    var plane = shape.Planes[i];
                    var holes = openings.Where(o => o.Host.PlaneIndex == i).Select(o => o.Hole).ToList();
                    if (UnfoldPlane(shape, plane, holes) is { } unfolded)
                        raw.Add(unfolded);
                }
            }
        }

        if (raw.Count == 0) return null;

        // Normalize each sheet to (0, 0) and compute its true metrics, then pack into a neat grid.
        var normalized = new List<(bool Barrel, double Pitch, double Width, double SlopeLength, double Gross, double Net, List<Vec2> Outer, List<List<Vec2>> Holes, List<double> Edges)>(raw.Count);
        foreach (var (barrel, pitch, outer, holes) in raw)
        {
            var minU = outer.Min(p => p.X);
            var minV = outer.Min(p => p.Y);
            var maxU = outer.Max(p => p.X);
            var maxV = outer.Max(p => p.Y);
            var shift = new Vec2(minU, minV);
            var normOuter = outer.Select(p => p - shift).ToList();
            var normHoles = holes.Select(h => h.Select(p => p - shift).ToList()).ToList();
            var gross = Math.Abs(Polygon2.SignedArea(normOuter));
            var holeArea = normHoles.Sum(h => Math.Abs(Polygon2.SignedArea(h)));
            var net = Math.Max(0, gross - holeArea);
            var edges = Enumerable.Range(0, normOuter.Count)
                .Select(k => (normOuter[(k + 1) % normOuter.Count] - normOuter[k]).Length)
                .ToList();
            normalized.Add((barrel, pitch, maxU - minU, maxV - minV, gross, net, normOuter, normHoles, edges));
        }

        var maxRowWidth = normalized.Count <= 2
            ? double.PositiveInfinity
            : Math.Max(normalized.Max(s => s.Width), normalized.Sum(s => s.Width + SheetGap) / Math.Ceiling(Math.Sqrt(normalized.Count)));

        var sheets = new List<Sheet>(normalized.Count);
        double x = 0, y = 0, rowHeight = 0;
        for (var i = 0; i < normalized.Count; i++)
        {
            var s = normalized[i];
            if (x > 0 && x + s.Width > maxRowWidth)
            {
                x = 0;
                y -= rowHeight + SheetGap * 1.5;
                rowHeight = 0;
            }
            var offset = new Vec2(x, y);
            var placedOuter = s.Outer.Select(p => p + offset).ToList();
            var placedHoles = s.Holes.Select(h => (IReadOnlyList<Vec2>)h.Select(p => p + offset).ToList()).ToList();
            sheets.Add(new Sheet(
                i + 1, s.Barrel, s.Pitch, s.Width, s.SlopeLength, s.Gross, s.Net,
                placedOuter, placedHoles, s.Edges, offset, offset + new Vec2(s.Width, s.SlopeLength)));
            x += s.Width + SheetGap;
            rowHeight = Math.Max(rowHeight, s.SlopeLength);
        }

        return new UnfoldedLayout(
            roof,
            sheets,
            sheets.Sum(s => s.GrossArea),
            sheets.Sum(s => s.NetArea));
    }

    static (bool Barrel, double Pitch, List<Vec2> Outer, List<List<Vec2>> Holes)? UnfoldPlane(
        RoofShape shape, RoofPlane plane, IReadOnlyList<IReadOnlyList<Vec3>> holes)
    {
        if (plane.Points.Count < 3) return null;
        var normal = VecMath.NewellNormal(plane.Points).Normalized();
        if (normal.Z < 0) normal = -normal;

        var a = shape.Outline[plane.Edge];
        var b = shape.Outline[(plane.Edge + 1) % shape.Outline.Count];
        var eave2 = b - a;
        var uRaw = eave2.Length > 1e-9 ? new Vec3(eave2.X / eave2.Length, eave2.Y / eave2.Length, 0) : new Vec3(1, 0, 0);
        var u = (uRaw - normal * Vec3.Dot(uRaw, normal)).Normalized();
        var v = Vec3.Cross(normal, u).Normalized();
        if (v.Z < 0) v = -v;

        var origin = plane.Points[0];
        Vec2 Project(Vec3 p)
        {
            var d = p - origin;
            return new Vec2(Vec3.Dot(d, u), Vec3.Dot(d, v));
        }

        var outer = plane.Points.Select(Project).ToList();
        var holes2 = holes.Select(h => h.Select(Project).ToList()).ToList();
        return (false, plane.Pitch, outer, holes2);
    }

    static (bool Barrel, double Pitch, List<Vec2> Outer, List<List<Vec2>> Holes)? UnfoldBarrel(RoofShape shape, BarrelArc arc)
    {
        var allPts = shape.Planes.SelectMany(p => p.Points).ToList();
        if (allPts.Count == 0) return null;

        var sLo = allPts.Min(p => arc.S(p.Xy));
        var sHi = allPts.Max(p => arc.S(p.Xy));
        var tLo = allPts.Min(p => Vec2.Dot(p.Xy - arc.A, arc.Dir));
        var tHi = allPts.Max(p => Vec2.Dot(p.Xy - arc.A, arc.Dir));

        var theta0 = Math.Asin(Math.Clamp((sLo - arc.SMid) / arc.R, -1, 1));
        var theta1 = Math.Asin(Math.Clamp((sHi - arc.SMid) / arc.R, -1, 1));
        var arcWidth = Math.Abs(arc.R * (theta1 - theta0));
        var length = Math.Abs(tHi - tLo);
        if (arcWidth < 1e-6 || length < 1e-6) return null;

        var rect = new List<Vec2>
        {
            new(0, 0),
            new(length, 0),
            new(length, arcWidth),
            new(0, arcWidth),
        };
        return (true, 0, rect, []);
    }

    /// <summary>Finds or creates the drafting view holding <paramref name="roof"/>'s unfolded covering plan.</summary>
    public static Entity View(ICommandContext ctx, Entity roof)
    {
        if (ctx.Model.OfClass(Views.Class).FirstOrDefault(v => v.Str(ViewKey) == roof.Id) is { } existing)
            return existing;
        var attrs = new JsonObject
        {
            ["kind"] = "drafting",
            ["scale"] = 50,
            ["discipline"] = "Architecture",
            [ViewKey] = roof.Id,
        };
        var v = new Entity(ctx.NewId(), Views.Class, $"Unfolded covering · {roof.Name}", attrs);
        ctx.Add(v);
        return v;
    }
}

/// <summary>Emits 2D unfolded roof covering sheets, dimensions, edge lengths, and area summaries on unfolding drafting views.</summary>
sealed class RoofUnfoldMarks : IViewAnnotations
{
    public IEnumerable<ViewMark> Marks(ViewContext c)
    {
        if (c.View.Str(RoofUnfold.ViewKey) is not { } roofId || c.Model.Get(roofId) is not { Class: IfcClass.Roof } roof)
            yield break;
        var layout = RoofUnfold.Layout(c.Model, roof);
        if (layout == null) yield break;

        var mm = c.Mm;
        var text = Standards.ResolveText(c.Book, []);
        // A text's height is in paper millimetres, as every pen text's (its place is in metres: offsets scale by mm).
        var hLabel = text.Height;
        var hTitle = text.Height * 1.15;
        const string ink = "#000000", holeColor = "#b5654a", dimColor = "#1f4e8c";

        var mk = new ViewMark(roof.Id);
        foreach (var s in layout.Sheets)
        {
            mk.Polyline(s.Outer, true, "ROOF-UNFOLD", 0.35, ink);

            // Opening holes with diagonal cross lines.
            foreach (var hole in s.Holes)
            {
                mk.Polyline(hole, true, "ROOF-UNFOLD", 0.25, holeColor);
                if (hole.Count >= 4)
                {
                    mk.Line(hole[0], hole[hole.Count / 2], "ROOF-UNFOLD", 0.15, holeColor);
                    mk.Line(hole[1], hole[1 + hole.Count / 2], "ROOF-UNFOLD", 0.15, holeColor);
                }
                else if (hole.Count == 3)
                {
                    var cx = new Vec2(hole.Average(p => p.X), hole.Average(p => p.Y));
                    foreach (var pt in hole) mk.Line(cx, pt, "ROOF-UNFOLD", 0.15, holeColor);
                }
            }

            // Edge length labels along each outer edge.
            for (var i = 0; i < s.Outer.Count; i++)
            {
                var a = s.Outer[i];
                var b = s.Outer[(i + 1) % s.Outer.Count];
                var d = b - a;
                var len = d.Length;
                if (len < 0.15) continue;
                var mid = (a + b) * 0.5;
                var rawDeg = Math.Atan2(d.Y, d.X) * 180 / Math.PI;
                var angle = Standards.ReadableDeg(rawDeg);
                var outward = new Vec2(d.Y, -d.X).Normalized();
                mk.Text(new PenText(mid + outward * (1.2 * mm), $"{len.ToString("0.00", CultureInfo.InvariantCulture)} m", hLabel * 0.85, angle, Baseline: "middle", Font: text.Font), dimColor);
            }

            // Overall width dimension line below sheet and slope-length dimension line to the left.
            var dimOff = 4.0 * mm;
            var tick = 1.0 * mm;
            var wY = s.Min.Y - dimOff;
            mk.Line(new Vec2(s.Min.X, wY), new Vec2(s.Max.X, wY), "DIMENSION", 0.18, dimColor)
              .Line(new Vec2(s.Min.X, wY - tick), new Vec2(s.Min.X, wY + tick), "DIMENSION", 0.25, dimColor)
              .Line(new Vec2(s.Max.X, wY - tick), new Vec2(s.Max.X, wY + tick), "DIMENSION", 0.25, dimColor)
              .Text(new PenText(new Vec2((s.Min.X + s.Max.X) * 0.5, wY - 0.8 * mm), $"W {s.Width.ToString("0.00", CultureInfo.InvariantCulture)} m", hLabel, Baseline: "top", Font: text.Font), dimColor);

            var lX = s.Min.X - dimOff;
            mk.Line(new Vec2(lX, s.Min.Y), new Vec2(lX, s.Max.Y), "DIMENSION", 0.18, dimColor)
              .Line(new Vec2(lX - tick, s.Min.Y), new Vec2(lX + tick, s.Min.Y), "DIMENSION", 0.25, dimColor)
              .Line(new Vec2(lX - tick, s.Max.Y), new Vec2(lX + tick, s.Max.Y), "DIMENSION", 0.25, dimColor)
              .Text(new PenText(new Vec2(lX - 0.8 * mm, (s.Min.Y + s.Max.Y) * 0.5), $"L {s.SlopeLength.ToString("0.00", CultureInfo.InvariantCulture)} m", hLabel, 90, Baseline: "bottom", Font: text.Font), dimColor);

            // Sheet title above the panel.
            var title = s.Barrel
                ? $"Barrel vault · {s.NetArea.ToString("0.00", CultureInfo.InvariantCulture)} m²"
                : $"Slope {s.Index} · {s.Pitch.ToString("0.#", CultureInfo.InvariantCulture)}° · {s.NetArea.ToString("0.00", CultureInfo.InvariantCulture)} m²";
            mk.Text(new PenText(new Vec2((s.Min.X + s.Max.X) * 0.5, s.Max.Y + 2.5 * mm), title, hTitle, Bold: true, Font: text.Font), ink);
        }

        // Total covering area summary above the top row.
        var topY = layout.Sheets.Max(s => s.Max.Y) + 7.5 * mm;
        var minX = layout.Sheets.Min(s => s.Min.X);
        var summary = $"Total covering area: {layout.TotalNetArea.ToString("0.00", CultureInfo.InvariantCulture)} m² ({layout.Sheets.Count} {(layout.Sheets.Count == 1 ? "sheet" : "sheets")})";
        mk.Text(new PenText(new Vec2(minX, topY), summary, hTitle * 1.1, Anchor: "start", Bold: true, Font: text.Font), ink);

        yield return mk;
    }
}

/// <summary>Exposes a roof's unfolded covering plan as an element view role.</summary>
sealed class RoofViewRoles : IElementViewRoles
{
    public IEnumerable<ElementViewRole> Roles() =>
    [
        new("roof.unfoldedCovering", "Unfolded covering", (_, e) => e.Class == IfcClass.Roof,
            v => v.Str(RoofUnfold.ViewKey), (ctx, e, _) => RoofUnfold.View(ctx, e).Id),
    ];
}
