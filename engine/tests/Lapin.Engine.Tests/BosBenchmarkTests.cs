using System.Diagnostics;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Mesh.Bos;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Structure;
using Lapin.Studio;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// A BIM Open Schema file's import timed stage by stage into a new project (its three storeys), when one is given
/// (LAPIN_BOS_BENCH=/path/file.bos; LAPIN_BOS_BENCH_MODE=reference|auto, auto by default as the dialog sends it; run in
/// Release): docs/bos.md keeps the numbers. The storeys after it are printed: the file's, at the file's levels.
/// </summary>
public class BosBenchmarkTests(ITestOutputHelper output)
{
    [Fact]
    public void Benchmark_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_BOS_BENCH");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var auto = Environment.GetEnvironmentVariable("LAPIN_BOS_BENCH_MODE") != "reference";
        var sw = Stopwatch.StartNew();
        var total = Stopwatch.StartNew();
        void Lap(string what)
        {
            var line = $"{what,-46} {sw.Elapsed.TotalSeconds,8:0.00} s";
            output.WriteLine(line); Console.WriteLine(line);
            sw.Restart();
        }
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new StudioModule());
        f.Session.New();
        Lap("session");
        var stage = "";
        f.Session.Progress = (s, done, all) => { if (s != stage) { Lap($"  … {(stage == "" ? "start" : stage)}"); stage = s; } };
        var view = BosImport.Read(File.ReadAllBytes(path), Path.GetFileName(path));
        Lap($"read ({view.InstancesOf.Count:N0} entities with bodies)");
        foreach (var st in view.Storeys)
        {
            var l = $"file storey {view.Name(st)} document {view.Data.Entities[st].Document} raw {view.RawElevation(st)?.Value} typical {(view.Typical().TryGetValue(st, out var lo) ? lo : double.NaN):0.000} → {view.Elevation(st)}";
            output.WriteLine(l); Console.WriteLine(l);
        }
        var r = BosImport.Run(f.Session, view, Path.GetFileName(path), out var diff, new FragImportOptions(Recognize: auto));
        Lap($"  … {stage}");
        Lap($"import ({r.Elements:N0} elements, {r.Shared:N0} shared)");
        var parts = f.Session.Parts(diff, 20_000);
        foreach (var p in parts) BinaryFrame.Encode(p);
        Lap($"diff to the interface ({diff.Render.Count:N0} items, {parts.Count} parts)");
        var line = $"total {total.Elapsed.TotalSeconds:0.00} s";
        output.WriteLine(line); Console.WriteLine(line);
        foreach (var s in Spatial.Storeys(f.Session.Model))
        {
            var l = $"storey {s.Name} {s.Num("elevation"):+0.00;-0.00} ({f.Session.Model.RelatedOf(s.Id, IfcClass.RelContainedInSpatialStructure).Count()} elements)";
            output.WriteLine(l); Console.WriteLine(l);
        }
        foreach (var n in r.Notes) { output.WriteLine(n); Console.WriteLine(n); }
        Assert.True(r.Elements > 0);
    }
}
