using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Export;
using Lapin.Drawing.Hatch;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage 0 of docs/plan-material-graphics.md (decision 678): the reference details (<see cref="ReferenceDetails"/>), each view
/// drawn at 1:20, 1:50 and 1:100 by the engine's own path (Views.Render → Pens), checked for what is right whatever the
/// convention:
/// <list type="bullet">
/// <item>no cut line between two joined elements' bands of one material;</item>
/// <item>a hatch inside the bands it fills;</item>
/// <item>the pens' hierarchy: cut over seen over hatch;</item>
/// <item>every band whose material has a hatch, wide enough on paper to show one, hatched;</item>
/// <item>membranes on an interface between layers (or along their own thin band).</item>
/// </list>
/// A snapshot of the pens (each pen's layer, colour, width, fill and dashes with its strokes' count and length, not pixels)
/// is kept beside this file (<c>MaterialGraphicsReference.snap.txt</c>) and compared: <c>LAPIN_UPDATE_SNAPSHOTS=1</c> writes
/// it again. <c>LAPIN_REFERENCE_PDF=1</c> writes every detail's sheets as one PDF, and each detail as a .lapin project, to
/// <c>build/material-graphics-reference/</c>.
/// </summary>
public class MaterialGraphicsReferenceTests
{
    /// <summary>The layers the model's drawing lays (what the snapshot holds); the annotations' are left out.</summary>
    static readonly HashSet<string> ModelLayers = [PenLayer.Fill, PenLayer.Hatch, PenLayer.Cut, PenLayer.Seen, PenLayer.Hidden, PenLayer.Beyond, PenLayer.Overhead, PenLayer.Membrane, SurfaceHatches.Layer, SurfaceHatches.FillLayer];

    internal sealed record Drawn(ReferenceDetails.Detail Detail, string View, int Scale, string ViewId, Views.Rendered R, string Kind);

    static readonly Lazy<List<Drawn>> All = new(DrawAll);

    static List<Drawn> DrawAll() => Draw(ReferenceDetails.All, ReferenceDetails.Scales);

    /// <summary>Each detail's views drawn at each scale.</summary>
    static List<Drawn> Draw(IEnumerable<Func<ReferenceDetails.Detail>> details, int[] scales)
    {
        var o = new List<Drawn>();
        foreach (var make in details)
        {
            var d = make();
            foreach (var (name, args) in d.Views)
                foreach (var scale in scales)
                {
                    var a = (JsonObject)args.DeepClone();
                    a["scale"] = scale;
                    a["name"] = $"{d.Name} {name} 1:{scale}";
                    var id = (string)d.F.Run("view.create", a)!["id"]!;
                    var view = Views.Find(d.F.Model, id)!;
                    var r = Views.Render(d.F.Session.Reader, view);
                    Assert.True(r.Problem == null, $"{d.Name}/{name}: {r.Problem}");
                    var kind = (string)args["kind"]! == "plan" ? "plan" : (string)args["kind"]! == "elevation" ? "elevation" : "section";
                    o.Add(new Drawn(d, name, scale, id, r, kind));
                }
        }
        return o;
    }

    public static IEnumerable<object[]> Cases() => All.Value.Select(d => new object[] { d.Detail.Name, d.View, d.Scale });

    static Drawn Get(string detail, string view, int scale) => All.Value.Single(x => x.Detail.Name == detail && x.View == view && x.Scale == scale);

    // As the view resolves it: its project's material standard (decision 681), at its scale.
    static ResolvedMaterial? Look(Drawn d, string? material) => material == null ? null
        : d.Detail.F.Model.Get(material) is { } m ? MaterialGraphics.Resolve(m, d.Kind, scale: d.Scale, standard: MaterialStandards.Of(d.Detail.F.Model))
        : GlobalId.IsValid(material) ? null : MaterialGraphics.Resolve(null, d.Kind, material, scale: d.Scale, standard: MaterialStandards.Of(d.Detail.F.Model));

    // ------------------------------------------------------------ geometry

    static double DistToSeg(Vec2 p, Vec2 a, Vec2 b)
    {
        var ab = b - a;
        var l2 = Vec2.Dot(ab, ab);
        var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
        return (a + ab * t - p).Length;
    }

    static double DistToBoundary(DPolygon poly, Vec2 p) =>
        poly.Loops.SelectMany(l => l.Select((q, i) => DistToSeg(p, q, l[(i + 1) % l.Length]))).DefaultIfEmpty(double.MaxValue).Min();

    /// <summary>Even-odd over the polygon's loops.</summary>
    static bool Inside(DPolygon poly, Vec2 p)
    {
        var inside = false;
        foreach (var l in poly.Loops)
            for (int i = 0, j = l.Length - 1; i < l.Length; j = i++)
                if ((l[i].Y > p.Y) != (l[j].Y > p.Y) && p.X < (l[j].X - l[i].X) * (p.Y - l[i].Y) / (l[j].Y - l[i].Y) + l[i].X) inside = !inside;
        return inside;
    }

    static double Area(DPolygon poly) => Math.Abs(poly.Loops.Sum(l => Polygon2.SignedArea(l)));
    static double Perimeter(DPolygon poly) => poly.Loops.Sum(l => l.Select((q, i) => (l[(i + 1) % l.Length] - q).Length).Sum());

    // ------------------------------------------------------------ the rules

    [Theory]
    [MemberData(nameof(Cases))]
    public void No_cut_line_runs_between_two_elements_bands_of_one_material(string detail, string view, int scale) => CheckNoCutLineInsideMaterial(Get(detail, view, scale));

    internal static void CheckNoCutLineInsideMaterial(Drawn d)
    {
        var (detail, view, scale) = (d.Detail.Name, d.View, d.Scale);
                if (d.R.Generated is not { } g || g.Cuts.Count == 0) return;
        var tol = 1e-4;
        var bad = new List<string>();
        foreach (var pen in d.R.Pens.Where(p => p.Layer == PenLayer.Cut))
            foreach (var (a, b) in pen.Segments)
            {
                if ((b - a).Length < 1e-3) continue;
                var mid = (a + b) * 0.5;
                var on = g.Cuts.Where(c => DistToBoundary(c, mid) < tol && Look(d, c.Material) is { Membrane: false, DrawJoints: false, Joins: true })
                    .GroupBy(c => MaterialGraphics.Normalise(Look(d, c.Material)!.Name));
                // The material on both sides of the line, of two elements: the line runs inside the material.
                var n = Normal(a, b) * 2e-3;
                foreach (var grp in on)
                    if (grp.FirstOrDefault(c => Inside(c, mid + n)) is { } left && grp.Any(c => c.Element != left.Element && Inside(c, mid - n)))
                        bad.Add($"{grp.Key} at {mid.X:0.###},{mid.Y:0.###}");
            }
        Assert.True(bad.Count == 0, $"{detail}/{view} 1:{scale}: a cut line between bands of one material: {string.Join("; ", bad.Distinct().Take(8))}");
    }

    static Vec2 Normal(Vec2 a, Vec2 b) { var d = b - a; var l = d.Length; return l < 1e-12 ? new Vec2(0, 0) : new Vec2(-d.Y / l, d.X / l); }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Hatches_stay_inside_their_bands(string detail, string view, int scale) => CheckHatchesInside(Get(detail, view, scale));

    internal static void CheckHatchesInside(Drawn d)
    {
        var (detail, view, scale) = (d.Detail.Name, d.View, d.Scale);
                if (d.R.Generated is not { } g) return;
        var tol = 0.2 * scale / 1000;   // 0.2 mm on paper
        var outside = 0;
        var total = 0;
        foreach (var pen in d.R.Pens.Where(p => p.Layer == PenLayer.Hatch))
            foreach (var (a, b) in pen.Segments)
            {
                total++;
                foreach (var p in new[] { a, b, (a + b) * 0.5 })
                    if (!g.Cuts.Any(c => Inside(c, p) || DistToBoundary(c, p) < tol)) { outside++; break; }
            }
        Assert.True(outside == 0, $"{detail}/{view} 1:{scale}: {outside} of {total} hatch strokes leave the cut bands.");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Cut_lines_are_heavier_than_seen_lines_and_seen_lines_than_hatches(string detail, string view, int scale) => CheckPenHierarchy(Get(detail, view, scale));

    internal static void CheckPenHierarchy(Drawn d)
    {
        var (detail, view, scale) = (d.Detail.Name, d.View, d.Scale);
        var pens = d.R.Pens.Where(p => p.Segments.Count > 0).ToList();
        double Max(string layer) => pens.Where(p => p.Layer == layer).Select(p => p.Width).DefaultIfEmpty(0).Max();
        double Min(string layer) => pens.Where(p => p.Layer == layer).Select(p => p.Width).DefaultIfEmpty(double.MaxValue).Min();
        var (cut, seen, hatch) = (Max(PenLayer.Cut), Max(PenLayer.Seen), Math.Max(Max(PenLayer.Hatch), Max(SurfaceHatches.Layer)));
        if (cut > 0 && seen > 0) Assert.True(cut > seen, $"cut {cut} ≤ seen {seen}");
        if (seen > 0 && hatch > 0) Assert.True(seen > hatch, $"seen {seen} ≤ hatch {hatch}");
        if (cut > 0 && hatch > 0) Assert.True(Min(PenLayer.Cut) > hatch, $"a cut line {Min(PenLayer.Cut)} no heavier than a hatch {hatch}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Every_band_whose_material_has_a_hatch_is_hatched(string detail, string view, int scale) => CheckBandsHatched(Get(detail, view, scale));

    internal static void CheckBandsHatched(Drawn d)
    {
        var (detail, view, scale) = (d.Detail.Name, d.View, d.Scale);
                var patterns = DraftingResources.Patterns(d.Detail.F.Model);
        if (d.Kind == "elevation")
        {
            // A facade whose materials hatch their surfaces shows them.
            Assert.NotEmpty(d.R.Pens.Where(p => p.Layer == SurfaceHatches.Layer).SelectMany(p => p.Segments));
            return;
        }
        if (d.R.Generated is not { } g) return;
        var strokes = d.R.Pens.Where(p => p.Layer == PenLayer.Hatch).SelectMany(p => p.Segments).Select(s => (s.A + s.B) * 0.5).ToList();
        var solids = d.R.Pens.Where(p => p.Layer == PenLayer.Hatch && p.Loops.Count > 0).ToList();
        var missing = new List<string>();
        foreach (var c in g.Cuts)
        {
            if (Look(d, c.Material) is not { Hatch: { } h, Membrane: false } look || HatchLibrary.Find(h, patterns) is not { } pattern) continue;
            // A band thinner than 3 mm on paper may fall between a pattern's lines.
            var thick = 2 * Area(c) / Math.Max(1e-9, Perimeter(c)) * 2 * 1000 / scale;
            if (thick < 3) continue;
            var hatched = pattern.Solid ? solids.Count > 0 : strokes.Any(p => Inside(c, p));
            if (!hatched) missing.Add($"{look.Name} ({h}) of {c.Class}");
        }
        Assert.True(missing.Count == 0, $"{detail}/{view} 1:{scale}: bands not hatched: {string.Join(", ", missing.Distinct())}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Membranes_lie_on_an_interface_or_along_their_own_band(string detail, string view, int scale) => CheckMembranes(Get(detail, view, scale));

    internal static void CheckMembranes(Drawn d)
    {
        var (detail, view, scale) = (d.Detail.Name, d.View, d.Scale);
                if (d.R.Generated is not { } g) return;
        var tol = 1e-4;
        foreach (var (a, b) in d.R.Pens.Where(p => p.Layer == PenLayer.Membrane).SelectMany(p => p.Segments))
        {
            var mid = (a + b) * 0.5;
            var onBoundaries = g.Cuts.Count(c => DistToBoundary(c, mid) < tol);
            var inOwnBand = g.Cuts.Any(c => Inside(c, mid) && Look(d, c.Material)?.Membrane == true || Inside(c, mid) && Area(c) / Math.Max(1e-9, Perimeter(c)) < MaterialGraphics.MembraneThickness);
            Assert.True(onBoundaries >= 2 || inOwnBand, $"{detail}/{view} 1:{scale}: a membrane at {mid.X:0.###},{mid.Y:0.###} lies on no interface.");
        }
    }

    [Fact]
    public void The_details_show_membranes_where_their_layers_have_them()
    {
        // The wall's and the floor's vapour barriers (1 mm) and the footing's waterproofing (marked a membrane) draw as membranes.
        foreach (var (detail, view) in new[] { ("composite-walls", "plan"), ("floor", "section"), ("strip-footing", "section") })
            Assert.NotEmpty(Get(detail, view, 50).R.Pens.Where(p => p.Layer == PenLayer.Membrane).SelectMany(p => p.Segments));
    }

    // ------------------------------------------------------------ every material standard (decision 681)

    public static IEnumerable<object[]> Standards() => MaterialStandard.BuiltIns.Where(s => !s.IsLapin).Select(s => new object[] { s.Id });

    /// <summary>Each built-in standard draws the details (applied to their projects) by the same rules, at 1:20 and 1:100.</summary>
    [Theory]
    [MemberData(nameof(Standards))]
    public void Every_standard_draws_the_details_by_the_rules(string id)
    {
        var drawn = Draw(MaterialReferenceDetails.All.Select(x => (Func<ReferenceDetails.Detail>)(() =>
            ReferenceDetails.Make(x.Build, f => f.Run("standards.applyMaterials", new JsonObject { ["id"] = id })))), [20, 100]);
        foreach (var d in drawn)
        {
            Assert.Equal(id, MaterialStandards.Of(d.Detail.F.Model).Id);
            CheckNoCutLineInsideMaterial(d);
            CheckHatchesInside(d);
            CheckPenHierarchy(d);
            CheckBandsHatched(d);
            CheckMembranes(d);
        }
        // The standard draws otherwise than Lapin's: somewhere a pen differs.
        Assert.NotEqual(string.Concat(All.Value.Where(x => x.Scale is 20 or 100).Select(x => Snapshot(x.R.Pens))), string.Concat(drawn.Select(x => Snapshot(x.R.Pens))));
    }

    // ------------------------------------------------------------ the snapshot

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>The model's pens of a drawing, one line each: layer, colour, width, fill, dashes, strokes (count, length in m), loops, in drawing order.</summary>
    public static string Snapshot(IEnumerable<Pen> pens)
    {
        var b = new StringBuilder();
        foreach (var p in pens.Where(p => ModelLayers.Contains(p.Layer)))
            b.Append("  ").Append(p.Layer).Append(' ').Append(p.Stroke ?? "-").Append(' ').Append(F(p.Width)).Append(' ').Append(p.Fill ?? "-")
             .Append(' ').Append(p.Dash is { Length: > 0 } dd ? string.Join(",", dd.Select(F)) : "-")
             .Append(" s=").Append(p.Segments.Count).Append(" len=").Append(F(Math.Round(p.Segments.Sum(s => (s.B - s.A).Length), 2)))
             .Append(" loops=").Append(p.Loops.Count).Append('\n');
        return b.ToString();
    }

    static string SnapshotPath([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "MaterialGraphicsReference.snap.txt");

    [Fact]
    public void The_pens_are_as_the_snapshot_keeps_them()
    {
        var b = new StringBuilder("# Material graphics reference details: the model's pens of each view (decision 678). LAPIN_UPDATE_SNAPSHOTS=1 writes it again.\n");
        foreach (var d in All.Value) b.Append(d.Detail.Name).Append('/').Append(d.View).Append(" 1:").Append(d.Scale).Append('\n').Append(Snapshot(d.R.Pens));
        var now = b.ToString();
        var path = SnapshotPath();
        if (Environment.GetEnvironmentVariable("LAPIN_UPDATE_SNAPSHOTS") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, now);
            return;
        }
        var kept = File.ReadAllText(path).ReplaceLineEndings("\n");
        if (kept == now) return;
        var (a, c) = (kept.Split('\n'), now.Split('\n'));
        var first = Enumerable.Range(0, Math.Min(a.Length, c.Length)).FirstOrDefault(i => a[i] != c[i], Math.Min(a.Length, c.Length));
        var header = c.Take(first + 1).LastOrDefault(l => !l.StartsWith(' ')) ?? "";
        Assert.Fail($"The drawings changed (LAPIN_UPDATE_SNAPSHOTS=1 to keep the change). First at line {first + 1}, in {header}:\n kept: {(first < a.Length ? a[first] : "(end)")}\n  now: {(first < c.Length ? c[first] : "(end)")}");
    }

    // ------------------------------------------------------------ the sheets, for the eye

    [Fact]
    public void The_reference_sheets_print()
    {
        var write = Environment.GetEnvironmentVariable("LAPIN_REFERENCE_PDF") == "1";
        var pages = new List<PaperPage>();
        foreach (var group in All.Value.GroupBy(d => d.Detail))
        {
            var f = group.Key.F;
            foreach (var byView in group.GroupBy(d => d.View))
            {
                var drawn = byView.OrderBy(d => d.Scale).ToList();
                double Width(Drawn d) => d.R.Bounds is { } bb ? (bb.Max.X - bb.Min.X) * 1000 / d.Scale : 50;
                var total = drawn.Sum(Width) + 40 * (drawn.Count + 1);
                var paper = total > 780 ? "A0" : "A1";
                var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = $"{group.Key.Title} — {byView.Key}", ["paper"] = paper })!["id"]!;
                var x = 40.0;
                foreach (var d in drawn)
                {
                    var w = Width(d);
                    f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = d.ViewId, ["x"] = x + w / 2, ["y"] = 250 });
                    x += w + 40;
                }
                pages.Add(Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!));
            }
        }
        var pdf = PdfWriter.Write(pages);
        Assert.StartsWith("%PDF", Encoding.Latin1.GetString(pdf, 0, 4));
        if (!write) return;
        var dir = Path.Combine(RepoRoot(), "build", "material-graphics-reference");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "material-graphics-reference.pdf"), pdf);
        foreach (var detail in All.Value.Select(d => d.Detail).Distinct())
        {
            using var file = File.Create(Path.Combine(dir, detail.Name + ".lapin"));
            ProjectPackage.Write(detail.F.Session, file);
        }
    }

    static string RepoRoot([CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));
}
