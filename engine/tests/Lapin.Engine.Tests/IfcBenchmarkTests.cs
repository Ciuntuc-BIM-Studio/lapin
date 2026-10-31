using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Structure;
using Lapin.Studio;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// An IFC file's import timed phase by phase, with the memory after each, when one is given
/// (LAPIN_IFC_BENCH=/path/file.ifc; run in Release): docs/plan-instancing.md keeps the numbers.
/// </summary>
public class IfcBenchmarkTests(ITestOutputHelper output)
{
    [Fact]
    public void Benchmark_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IFC_BENCH");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var log = new List<string>();
        var sw = Stopwatch.StartNew();
        void Lap(string what)
        {
            var proc = Process.GetCurrentProcess(); proc.Refresh();
            // (No peak: macOS does not report one.)
        var line = $"{what,-34} {sw.Elapsed.TotalSeconds,8:0.00} s   managed {GC.GetTotalMemory(false) / 1048576.0,7:0} MB   working set {proc.WorkingSet64 / 1048576.0,7:0} MB";
            log.Add(line); output.WriteLine(line); Console.WriteLine(line);
            sw.Restart();
        }
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());
        Lap("session");
        var bytes = File.ReadAllBytes(path);
        Lap($"read {bytes.Length / 1048576.0:0} MB");
        var step = StepFile.Read(bytes);
        Lap($"STEP parse ({step.Entities.Count:N0} entities)");
        step = null;
        var r = IfcImport.Run(f.Session, bytes, Path.GetFileName(path), null, out var diff);
        Lap("import (parse + recognise + build)");
        output.WriteLine(r.ToJson().ToJsonString());
        Console.WriteLine(r.ToJson().ToJsonString());
        var entities = f.Session.Model.Entities.Count();
        Lap($"model: {entities:N0} entities, diff render {diff.Render.Count:N0}");
        // The triangles sent: each item's own and its parts', each shared mesh once (docs/plan-display-precision.md).
        long Tris(IEnumerable<RenderPart>? ps) => ps?.Sum(p => (long)p.Indices.Length / 3) ?? 0;
        var sent = diff.Render.Sum(i => i.Indices.Length / 3 + Tris(i.Parts)) + (diff.Meshes ?? []).Sum(m => Tris(m.Parts));
        long Coarse(IEnumerable<RenderPart>? ps) => ps?.Sum(p => p.Lods?.Sum(l => (long)l.Positions.Length / 9) ?? 0) ?? 0;
        var coarse = diff.Render.Sum(i => Coarse(i.Parts)) + (diff.Meshes ?? []).Sum(m => Coarse(m.Parts));
        // Drawn: every instance its mesh's; from afar, each part's coarsest copy.
        long Coarsest(IEnumerable<RenderPart>? ps) => ps?.Sum(p => p.Lods is { Count: > 0 } l ? (long)l[^1].Positions.Length / 9 : (long)p.Indices.Length / 3) ?? 0;
        var byMesh = (diff.Meshes ?? []).ToDictionary(m => m.Id);
        long Drawn(Func<IEnumerable<RenderPart>?, long> parts) => diff.Render.Sum(i => i.Instance is { } inst && byMesh.TryGetValue(inst.Mesh, out var m) ? parts(m.Parts) : i.Indices.Length / 3 + parts(i.Parts));
        Lap($"triangles sent {sent:N0}, in coarser copies {coarse:N0}; drawn {Drawn(Tris):N0}, from afar {Drawn(Coarsest):N0}");
        try { var frame = BinaryFrame.Encode(diff); Lap($"diff frame, whole {frame.Length / 1048576.0:0} MB ({f.Session.Parts(diff, 20_000).Count} parts sent)"); }
        catch (Exception ex) { Lap($"diff frame FAILED: {ex.GetType().Name}"); }
        bytes = null; diff = null!;
        GC.Collect();
        Lap("GC");
        var snap = f.Session.Snapshot();
        Lap($"snapshot ({snap.Render.Count:N0} render items)");
        try { var snapFrame = BinaryFrame.Encode(snap); Lap($"snapshot frame {snapFrame.Length / 1048576.0:0} MB"); }
        catch (Exception ex) { Lap($"snapshot frame FAILED: {ex.GetType().Name}"); }
        snap = null;
        // The busiest storey's plan.
        var storey = Spatial.Storeys(f.Session.Model).OrderByDescending(s => f.Session.Model.RelatedOf(s.Id, IfcClass.RelContainedInSpatialStructure).Count()).First().Id;
        var drawing = f.Session.Query("view.drawing", new JsonObject { ["id"] = "plan:" + storey });
        Lap($"plan drawing ({drawing?.ToJsonString().Length / 1048576.0:0} MB JSON)");
        drawing = null;
        var lapin = Path.Combine(Path.GetTempPath(), "lapin-bench.lapin");
        PackageFiles.Save(f.Session, lapin);
        Lap($"save .lapin ({new FileInfo(lapin).Length / 1048576.0:0} MB)");
        var g = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());
        var opened = PackageFiles.Open(g.Session, lapin);
        Lap($"open .lapin ({g.Session.Model.Entities.Count():N0} entities, {opened.Render.Count:N0} render)");
        File.Delete(lapin);
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "lapin-bench.txt"), log);
    }
}
