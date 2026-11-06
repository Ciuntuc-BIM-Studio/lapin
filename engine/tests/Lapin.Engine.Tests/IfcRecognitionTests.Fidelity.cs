using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// The fidelity gate (docs/spec-ifc.md, Poarta de fidelitate): every recognised element measured against the body the file
/// gives it (volume, bounding box, base outline); enforced by default (since 06.11.2026), one that differs coming in as the
/// reference it would have been; under "report", measured only, the import unchanged.
/// </summary>
public partial class IfcRecognitionTests
{
    /// <summary>
    /// Three walls 5 m long, 300 thick: a plain one with a window (as the file says), one whose top a sloping plane cuts
    /// (IfcBooleanClippingResult: recognition takes the uncut extrusion, flat at 3 m) and one with an end cut at 45° (its
    /// footprint a trapezoid: the wall's own end cut, <see cref="Joins.OwnEnd"/>).
    /// </summary>
    string GateWalls()
    {
        var x = new Foreign();
        var f = x.F;
        // The plain wall along x from (0, 0), a window 1200 × 1500 at 2.5 m along, sill 900.
        var plainPl = x.Place(0, 0, 0);
        var plain = x.Element("IFCWALL", "plain", "Plain", plainPl, x.Shape(x.Box(2500, 0, 0, 5000, 300, 2800)), E("STANDARD"));
        x.Material(plain, x.Brick);
        var op = x.Element("IFCOPENINGELEMENT", "plain-op", "Opening", x.Place(0, 0, 0, 0, plainPl), x.Shape(x.Box(2500, 0, 900, 1200, 500, 1500)), E("OPENING"));
        var win = x.Element("IFCWINDOW", "plain-win", "F1", x.Place(0, 0, 0, 0, plainPl), x.Shape(x.Box(2500, 0, 900, 1200, 100, 1500)), "1500.", "1200.", E("WINDOW"), E("SINGLE_PANEL"), Null);
        f.Add("IFCRELVOIDSELEMENT", x.G("plain-v"), Null, Null, Null, plain, op);
        f.Add("IFCRELFILLSELEMENT", x.G("plain-f"), Null, Null, Null, op, win);
        // The sloped wall at y = 4 m: 3 m extruded, cut above a plane from 2.4 m at its start to 2.8 m at its end.
        var n = new Lapin.Geometry.Vec3(400, 0, -5000).Normalized();
        var plane = f.Add("IFCPLANE", f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,2400.)"), f.Add("IFCDIRECTION", $"({R(n.X)},{R(n.Y)},{R(n.Z)})"), Null));
        var clipped = f.Add("IFCBOOLEANCLIPPINGRESULT", E("DIFFERENCE"), x.Box(2500, 0, 0, 5000, 300, 3000), f.Add("IFCHALFSPACESOLID", plane, True));
        var sloped = x.Element("IFCWALL", "sloped", "Sloped", x.Place(0, 4000, 0), x.Shape(clipped, "Clipping"), E("STANDARD"));
        x.Material(sloped, x.Brick);
        // The wall with an end cut at y = 8 m: its far end at 45°, the long side 5 m, the short one 4.7 m.
        var trapezoid = f.Add("IFCPOLYLINE", L(new[] { "(0.,-150.)", "(5000.,-150.)", "(4700.,150.)", "(0.,150.)", "(0.,-150.)" }.Select(p => f.Add("IFCCARTESIANPOINT", p))));
        var cut = f.Add("IFCEXTRUDEDAREASOLID", f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, trapezoid), f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null), f.Add("IFCDIRECTION", "(0.,0.,1.)"), "2800.");
        var ended = x.Element("IFCWALL", "ended", "Ended", x.Place(0, 8000, 0), x.Shape(cut), E("STANDARD"));
        x.Material(ended, x.Brick);
        return x.Write(Path.Combine(_dir, "gate.ifc"));
    }

    static FidelityCheck CheckOf(IfcImportResult r, string id, string source = "ifc") => r.Fidelity!.Checks.Single(c => c.Id == id && c.Source == source);

    [Fact]
    public void The_gate_reports_a_faithful_wall_as_passing_and_leaves_the_import_as_it_was()
    {
        var path = GateWalls();
        var f = Full();
        var r = IfcImport.Run(f.Session, path, new IfcImportOptions(Fidelity: new FidelityOptions(FidelityOptions.Report)), out _);
        Assert.Equal(FidelityOptions.Report, r.Fidelity!.Options.Mode);
        var plain = CheckOf(r, Guid("foreign-test", "plain"));
        output.WriteLine(plain.ToJson().ToJsonString());
        Assert.True(plain.Measured);
        Assert.True(plain.Pass, plain.Reason);
        Assert.InRange(Math.Abs(plain.Volume!.Value), 0, 0.005);
        Assert.InRange(plain.BoxMm!.Value, 0, 1);
        Assert.InRange(plain.OutlineMm!.Value, 0, 1);
        // Report only: all three walls native, the window with its wall; the JSON and the note carry it.
        foreach (var key in new[] { "plain", "sloped", "ended" })
        {
            var w = f.Model.Get(Guid("foreign-test", key))!;
            Assert.Equal(IfcClass.Wall, w.Class);
            Assert.False(StoredBodies.Has(w));
        }
        Assert.Equal(IfcClass.Window, f.Model.Get(Guid("foreign-test", "plain-win"))!.Class);
        Assert.Equal(0, r.Fidelity.Reverted);
        Assert.Equal(1, r.Fidelity.Failed);
        var json = r.ToJson()["fidelity"]!;
        Assert.Equal(3, (int)json["measured"]!);
        Assert.Contains(r.Notes, n => n.StartsWith("Fidelity (report") && n.Contains("1 differ"));
        // The same import with the gate off is the same model.
        var g = Full();
        var off = IfcImport.Run(g.Session, path, new IfcImportOptions(Fidelity: new FidelityOptions(FidelityOptions.Off)), out _);
        Assert.Null(off.Fidelity);
        Assert.Equal(r.Recognized, off.Recognized);
        static IEnumerable<(string, int)> Kinds(IModelView m) => m.Entities.GroupBy(e => e.Class).Select(k => (k.Key, k.Count())).OrderBy(k => k.Key);
        Assert.Equal(Kinds(f.Model), Kinds(g.Model));
    }

    [Fact]
    public void A_wall_whose_cut_top_recognition_flattens_fails_at_its_top()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, GateWalls(), null, out _);
        var sloped = CheckOf(r, Guid("foreign-test", "sloped"));
        output.WriteLine(sloped.ToJson().ToJsonString());
        Assert.True(sloped.Measured);
        Assert.False(sloped.Pass);
        // Flat at 3 m where the file's top runs from 2.4 to 2.8 m: 200 mm too high, about a fifth more volume.
        Assert.Equal("top", sloped.BoxSide);
        Assert.Equal(200, sloped.BoxMm!.Value, 0);
        Assert.InRange(sloped.Volume!.Value, 0.15, 0.25);
        Assert.Contains("cut by a boolean", sloped.Cause);
        Assert.Contains("box 200 mm at the top", sloped.Reason);
    }

    [Fact]
    public void A_wall_whose_end_the_file_cuts_at_an_angle_comes_in_with_that_cut()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, GateWalls(), null, out _);
        var ended = CheckOf(r, Guid("foreign-test", "ended"));
        output.WriteLine(ended.ToJson().ToJsonString());
        Assert.True(ended.Measured);
        Assert.True(ended.Pass, ended.Reason);
        // Free at its end (FoundJoins), which crosses the axis halfway along the 45° cut, cut at -45°: the left face 150 mm
        // short of the axis's end, the right face 150 mm past it, as the file's trapezoid.
        var w = f.Model.Get(Guid("foreign-test", "ended"))!;
        Assert.Equal(-45, w.Num(Joins.EndAngle), 6);
        Assert.Null(w.NumOrNull(Joins.StartAngle));
        Assert.Equal(4.85, Walls.End(w).X, 6);
        Assert.InRange(Math.Abs(ended.Volume!.Value), 0, 1e-6);
        Assert.InRange(ended.OutlineMm!.Value, 0, 0.1);
    }

    [Fact]
    public void Enforce_keeps_the_walls_that_differ_as_references()
    {
        var f = Full();
        var r = IfcImport.Run(f.Session, GateWalls(), new IfcImportOptions(Fidelity: new FidelityOptions(FidelityOptions.Enforce)), out _);
        var m = f.Model;
        var plain = m.Get(Guid("foreign-test", "plain"))!;
        Assert.False(StoredBodies.Has(plain));
        Assert.Equal(IfcClass.Window, m.Get(Guid("foreign-test", "plain-win"))!.Class);
        Assert.False(StoredBodies.Has(m.Get(Guid("foreign-test", "ended"))!));
        foreach (var key in new[] { "sloped" })
        {
            var w = m.Get(Guid("foreign-test", key))!;
            Assert.True(StoredBodies.Has(w), key);
            Assert.Equal("IfcWall", w.Str("ifcClass"));
            Assert.NotNull(Spatial.StoreyOf(m, w.Id));
            Assert.True(CheckOf(r, w.Id).Reverted);
            // Not offered again to recognition from its triangles, which would make the same wall.
            Assert.DoesNotContain(r.Fidelity!.Checks, c => c.Id == w.Id && c.Source == "triangles");
        }
        // The reference keeps the file's sloped top: its highest point 2.8 m.
        Assert.Equal(2.8, f.Bounds(Guid("foreign-test", "sloped")).Max.Z, 3);
        Assert.Equal(1, r.Fidelity!.Reverted);
        Assert.Equal(2, r.RecognizedByClass!.GetValueOrDefault("IfcWall"));
        Assert.Equal(1, r.ReferencesByClass!.GetValueOrDefault("IfcWall"));
        // No wall type left without its walls.
        Assert.All(m.OfClass(IfcClass.WallType), t => Assert.NotEmpty(m.RelatedOf(t.Id, IfcClass.RelDefinesByType)));
        // And the import's rules carry it as the dialog sends it.
        var o = Lapin.Protocol.EngineDispatcher.Rules(new IfcImportOptions(), new JsonObject { ["fidelity"] = "enforce", ["fidelityMm"] = 2, ["fidelityVolume"] = 1 });
        Assert.Equal(new FidelityOptions(FidelityOptions.Enforce, 2, 0.01), o.Gate);
    }

    /// <summary>
    /// The gate on a real file, when one is given (LAPIN_IFC_FIDELITY=/path/to/file.ifc): recognised elements per class,
    /// how many fail at 1 mm / 0.5 % (and at looser tolerances), the deviations' distribution, the worst, and the gate's cost.
    /// </summary>
    [Fact]
    public void The_gate_on_a_sample_file_when_given()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IFC_FIDELITY");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        void Line(string s) { output.WriteLine(s); Console.WriteLine(s); }
        var bytes = File.ReadAllBytes(path);
        // The import without the gate, then with it, to see what it costs (twice each, the first warming up).
        Fixture? last = null;
        double Time(FidelityOptions? o, out IfcImportResult result)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            last = Full();
            result = IfcImport.Run(last.Session, bytes, Path.GetFileName(path), new IfcImportOptions(Fidelity: o), out _);
            return sw.Elapsed.TotalMilliseconds;
        }
        Time(new FidelityOptions(FidelityOptions.Off), out _);
        var offMs = Time(new FidelityOptions(FidelityOptions.Off), out var off);
        var onMs = Time(new FidelityOptions(FidelityOptions.Report), out var r);
        // The default, enforce: what differs comes in as the reference it was.
        var enforceMs = Time(null, out var enforced);
        var rep = r.Fidelity!;
        Assert.Equal(FidelityOptions.Enforce, enforced.Fidelity!.Options.Mode);
        Line($"enforce: {enforceMs:0} ms (its gate {enforced.Fidelity!.Milliseconds:0} ms), {enforced.Fidelity.Reverted} made references again, recognised {enforced.Recognized}, references {enforced.References} (report: {r.References})");
        foreach (var g in enforced.Fidelity.Checks.Where(c => c.Reverted).GroupBy(c => (c.IfcClass, c.Source)).OrderByDescending(g => g.Count()))
            Line($"  reverted {g.Key.IfcClass,-22} {g.Key.Source,-9} {g.Count(),5}");
        // The enforced import whole: no type left without its elements, every reverted element a reference the views draw.
        var em = last!.Model;
        var empty = Full().Model.Entities.Select(e => e.Id).ToHashSet();
        var orphans = em.Entities.Where(e => !empty.Contains(e.Id) && e.Class.EndsWith("Type") && e.Class.StartsWith("Ifc") && !em.RelatedOf(e.Id, IfcClass.RelDefinesByType).Any()).ToList();
        Line($"  types the import made left without elements after enforce: {orphans.Count} ({string.Join(", ", orphans.GroupBy(t => t.Class).Select(g => $"{g.Key} {g.Count()}"))})");
        var reverted = enforced.Fidelity.Checks.Where(c => c.Reverted).Select(c => (Check: c, E: em.Get(c.Id))).ToList();
        // One the reference import leaves out too is as it would have been (a wall whose body is its parts: the parts come instead).
        var asReferences = Full();
        IfcImport.Run(asReferences.Session, bytes, Path.GetFileName(path), new IfcImportOptions("reference"), out _);
        string Why((FidelityCheck Check, Entity? E) x) => x.E == null ? (asReferences.Model.Get(x.Check.Id) == null ? "" : "gone from the model") : !StoredBodies.Has(x.E) ? $"no stored body ({x.E.Class})"
            : last.Session.Geometry(x.E.Id) is not { Parts.Count: > 0 } ? "no geometry" : "";
        var undrawn = reverted.Count(x => Why(x) != "");
        Line($"  reverted elements not drawn as references: {undrawn} of {reverted.Count}");
        foreach (var g in reverted.Where(x => Why(x) != "").GroupBy(x => (Why(x), x.Check.IfcClass, x.Check.Source))) Line($"    {g.Count()} × {g.Key}: {string.Join(", ", g.Take(3).Select(x => x.Check.Name + " " + x.Check.Id))}");
        Assert.Equal(0, undrawn);
        Line($"{Path.GetFileName(path)}: import {offMs:0} ms without the gate, {onMs:0} ms with it (the gate itself {rep.Milliseconds:0} ms); recognised {off.Recognized} / {r.Recognized}");
        Line($"checks {rep.Checks.Count}, measured {rep.Measured}, failed {rep.Failed} at {rep.Options.Mm} mm / {rep.Options.Volume * 100} %");
        foreach (var g in rep.Checks.GroupBy(c => (c.IfcClass, c.Source)).OrderByDescending(g => g.Count()))
            Line($"  {g.Key.IfcClass,-22} {g.Key.Source,-9} {g.Count(),5} measured {g.Count(c => c.Measured),5} failed {g.Count(c => c.Measured && !c.Pass),5}");
        foreach (var g in rep.Checks.Where(c => !c.Measured).GroupBy(c => c.Reason)) Line($"  not measured: {g.Count()} × {g.Key}");
        var measured = rep.Checks.Where(c => c.Measured).ToList();
        foreach (var (mm, vol) in new[] { (1.0, 0.005), (2.0, 0.01), (5.0, 0.02), (10.0, 0.05), (50.0, 0.1) })
            Line($"  would fail at {mm,4} mm / {vol * 100,4} %: {measured.Count(c => Math.Abs(c.Volume ?? 0) > vol || c.BoxMm > mm || (c.OutlineMm ?? 0) > mm)}");
        string Dist(IEnumerable<double> xs, double[] edges)
        {
            var list = xs.ToList();
            var parts = new List<string>();
            var lo = double.NegativeInfinity;
            foreach (var e in edges) { parts.Add($"≤{e}: {list.Count(v => v > lo && v <= e)}"); lo = e; }
            parts.Add($">{edges[^1]}: {list.Count(v => v > edges[^1])}");
            return string.Join(", ", parts);
        }
        Line($"  |volume| %: {Dist(measured.Where(c => c.Volume != null).Select(c => Math.Abs(c.Volume!.Value) * 100), [0.01, 0.1, 0.5, 1, 5, 20])}; open file bodies {measured.Count(c => c.Volume == null)}");
        Line($"  box mm: {Dist(measured.Select(c => c.BoxMm ?? 0), [0.1, 1, 5, 20, 100, 500])}");
        Line($"  outline mm: {Dist(measured.Where(c => c.OutlineMm != null).Select(c => c.OutlineMm!.Value), [0.1, 1, 5, 20, 100, 500])}; no outline {measured.Count(c => c.OutlineMm == null)}");
        foreach (var g in measured.Where(c => !c.Pass).GroupBy(c => (c.IfcClass, c.Cause)).OrderByDescending(g => g.Count()))
            Line($"  cause {g.Count(),4} × {g.Key.IfcClass}: {g.Key.Cause}");
        foreach (var g in measured.Where(c => !c.Pass).GroupBy(c => c.BoxSide).OrderByDescending(g => g.Count())) Line($"  box side {g.Key}: {g.Count()}");
        Line($"  volume: file {measured.Sum(c => c.FileVolume ?? 0):0.###} m³, native {measured.Sum(c => c.NativeVolume ?? 0):0.###} m³; passed by their joins {rep.ByJoin}; " +
            $"failing in plan at joins add {measured.Where(c => !c.Pass && c.InPlanOnly && c.JoinedTo is { Count: > 0 }).Sum(c => (c.NativeVolume ?? 0) - (c.FileVolume ?? 0)):0.###} m³");
        Line("  worst:");
        foreach (var c in measured.Where(c => !c.Pass).OrderByDescending(c => Math.Max(Math.Abs(c.Volume ?? 0) * 1000, Math.Max(c.BoxMm ?? 0, c.OutlineMm ?? 0))).Take(25))
            Line($"    {c.IfcClass} {c.Name} ({c.Id}, {c.Source}): {c.Reason} — {c.Cause}");
        if (Environment.GetEnvironmentVariable("LAPIN_IFC_FIDELITY_JSON") is { Length: > 0 } outJson) File.WriteAllText(outJson, r.ToJson().ToJsonString());
        Assert.Equal(off.Recognized, r.Recognized);
    }
}
