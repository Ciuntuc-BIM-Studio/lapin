using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// An IFC file's elevations timed without and with surface hatches (decision 325), and with the sun's shadows, when one is
/// given (LAPIN_HATCH_BENCH=/path/file.ifc; run in Release): every material of the file is given an elevation hatch.
/// </summary>
public class SurfaceHatchBenchmarkTests(ITestOutputHelper output)
{
    /// <summary>A rendered view as an SVG, to look at.</summary>
    static void Svg(Views.Rendered r, string file)
    {
        if (r.Bounds is not { } b) return;
        var sb = new System.Text.StringBuilder();
        string F(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='{F(b.Min.X)} {F(-b.Max.Y)} {F(b.Max.X - b.Min.X)} {F(b.Max.Y - b.Min.Y)}' width='2400'><rect x='{F(b.Min.X)}' y='{F(-b.Max.Y)}' width='100%' height='100%' fill='white'/>");
        foreach (var p in r.Pens)
        {
            foreach (var l in p.Loops) sb.Append($"<path d='M{string.Join("L", l.Select(q => F(q.X) + " " + F(-q.Y)))}Z' fill='{p.Fill ?? "none"}'/>");
            if (p.Segments.Count > 0)
                sb.Append($"<path d='{string.Join("", p.Segments.Select(s => $"M{F(s.A.X)} {F(-s.A.Y)}L{F(s.B.X)} {F(-s.B.Y)}"))}' stroke='{p.Stroke ?? "#000"}' stroke-width='{F(Math.Max(p.Width, 0.05) * r.Scale / 1000)}' fill='none'/>");
        }
        sb.Append("</svg>");
        File.WriteAllText(file, sb.ToString());
    }

    [Fact]
    public void Benchmark_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_HATCH_BENCH");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        void Log(string s) { output.WriteLine(s); Console.WriteLine(s); }
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());
        IfcImport.Run(f.Session, File.ReadAllBytes(path), Path.GetFileName(path), null, out _);
        var materials = f.Model.OfClass(IfcClass.Material).ToList();
        Log($"{materials.Count} materials: {string.Join(", ", materials.Select(m => m.Name).Take(20))}");
        foreach (var m in materials)
            f.Run("material.update", new JsonObject { ["id"] = m.Id, ["elevation"] = new JsonObject { ["hatch"] = MaterialGraphics.FromName(m.Name ?? "").Hatch ?? "BRICK" } });
        foreach (var dir in new[] { "south", "east" })
        {
            var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = dir })!["id"]!;
            double Time(string what)
            {
                var sw = Stopwatch.StartNew();
                var r = Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);
                var s = sw.Elapsed.TotalSeconds;
                var strokes = r.Pens.Where(p => p.Layer == SurfaceHatches.Layer).Sum(p => p.Segments.Count);
                Log($"{dir,-6} {what,-34} {s,6:0.00} s   {strokes,8:N0} surface strokes");
                if (Environment.GetEnvironmentVariable("LAPIN_HATCH_SVG") is { Length: > 0 } dirOut) Svg(r, Path.Combine(dirOut, $"{dir}-{what.Replace(' ', '-').Replace("(", "").Replace(")", "")}.svg"));
                return s;
            }
            f.Run("view.setStyle", new JsonObject { ["id"] = view, ["surfaceHatch"] = false });
            Time("warm-up");
            Time("without surface hatches");
            f.Run("view.setStyle", new JsonObject { ["id"] = view, ["surfaceHatch"] = null });
            Time("with surface hatches (first)");
            Time("with surface hatches (again)");
            f.Run("view.setShadows", new JsonObject { ["id"] = view, ["mode"] = "view" });
            Time("with hatches and shadows");
            f.Run("view.setStyle", new JsonObject { ["id"] = view, ["surfaceHatch"] = false });
            Time("with shadows only");
        }
    }
}
