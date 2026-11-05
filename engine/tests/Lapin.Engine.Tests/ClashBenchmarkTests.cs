using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Clash;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Structure;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The clash tests a project starts with, timed on an IFC file when one is given (LAPIN_CLASH_BENCH=/path/file.ifc;
/// run in Release): the import, then each test's elements, pairs, clashes and seconds (docs/plan-visualisation.md).
/// </summary>
public class ClashBenchmarkTests(ITestOutputHelper output)
{
    [Fact]
    public void Benchmark_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_CLASH_BENCH");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        void Say(string s) { output.WriteLine(s); Console.WriteLine(s); }
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new MepModule(), new ClashModule());
        var sw = Stopwatch.StartNew();
        IfcImport.Run(f.Session, File.ReadAllBytes(path), Path.GetFileName(path), null, out _);
        Say($"import {sw.Elapsed.TotalSeconds:0.0} s, {f.Model.Entities.Count():N0} entities");
        f.Run("clash.seed", new JsonObject());
        // Every element against every other too, the heaviest test there is.
        var everything = (string)f.Run("clash.setSet", new JsonObject { ["name"] = "Everything" })!["id"]!;
        f.Run("clash.setTest", new JsonObject { ["name"] = "Everything × itself", ["setA"] = everything });
        sw.Restart();
        var runs = (JsonArray)f.Run("clash.run", new JsonObject())!;
        Say($"run all {sw.Elapsed.TotalSeconds:0.0} s");
        foreach (var r in runs.OfType<JsonObject>())
            Say($"  {(string?)r["name"],-28} A {(int)r["countA"]!,6:N0}  B {(int)r["countB"]!,6:N0}  pairs {(long)r["pairs"]!,8:N0}  clashes {(int)r["found"]!,6:N0}  {(double)r["seconds"]!,6:0.00} s{((bool)r["truncated"]! ? " (limit)" : "")}");
        // What the heaviest test finds, by the pair of classes, and how deep.
        var all = ((JsonArray)((JsonObject)f.Session.Query("clash.results", new JsonObject { ["id"] = (string)runs.OfType<JsonObject>().Last()["id"]! })!)["clashes"]!).OfType<JsonObject>().ToList();
        foreach (var g in all.GroupBy(c => string.Join(" × ", new[] { (string?)c["aClass"], (string?)c["bClass"] }.Order())).OrderByDescending(g => g.Count()).Take(12))
            Say($"  {g.Key,-40} {g.Count(),5}  median {g.Select(c => (double)c["d"]!).Order().ElementAt(g.Count() / 2) * 1000,6:0} mm");
    }
}
