using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Open-web joists (docs/plan-steel.md, Grinzile cu zăbrele; decision 827): the parametric body (two chords of angles back to
/// back, seats, a Warren of webs), its plan symbol and quantities; IFC as an IfcBeam JOIST of an IfcBeamType JOIST, read back as
/// itself; and recognition from a received body as Revit sends a bar joist (its chords as triangles, its webs as lines), made
/// native under the fidelity gate, a deformed one staying the reference it was.
/// </summary>
public sealed class OpenWebJoistTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule());

    static string Storey(Fixture f) => Spatial.Storeys(f.Model).First().Id;

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void A_joist_is_its_chords_seats_and_a_Warren_of_webs()
    {
        var f = F();
        var type = (string)f.Run("steel.createJoistType", new JsonObject { ["name"] = "J400", ["preset"] = "OWJ 400" })!["id"]!;
        var id = (string)f.Run("steel.addJoist", new JsonObject { ["storeyId"] = Storey(f), ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["typeId"] = type })!["id"]!;
        var e = f.Model.Get(id)!;
        Assert.Equal(OpenWebJoists.Class, e.Class);
        Assert.Equal("IfcBeam", e.Str("exportAs"));
        Assert.Equal("JOIST", e.PredefinedType);
        var spec = OpenWebJoists.SpecOf(f.Model, e);
        var g = f.Session.Geometry(id)!;
        // Its bearing on the storey's top: the top chord's top there, the bottom chord's underside its depth below.
        var (lo, hi) = f.Bounds(id);
        var top = f.Model.Get(Storey(f))!.Num("height", 3);
        Assert.Equal(top, hi.Z, 6);
        Assert.Equal(top - spec.Depth, lo.Z, 6);
        Assert.Equal(0, lo.X, 6);
        Assert.Equal(6, hi.X, 6);
        // Four chord angles, the bottom ones 0.6 m short of each end; 8 panels of 0.6 m between, two webs each, and the two end diagonals.
        Assert.Equal(4, g.Parts.Count(p => p.Key!.StartsWith("top:") || p.Key.StartsWith("bottom:")));
        var webs = OpenWebJoists.Warren(spec, 6);
        Assert.Equal(2 + 2 * 8, webs.Count);
        Assert.Equal(webs.Count, g.Parts.Count(p => p.Key!.StartsWith("web:")));
        var chords = 2 * (spec.Top.Area * 6 + spec.Bottom.Area * (6 - 2 * spec.BottomSetback));
        var chordParts = g.Parts.Where(p => p.Key!.StartsWith("top:") || p.Key.StartsWith("bottom:")).Sum(p => Measure.Volume(p.Solid));
        Assert.Equal(chords, chordParts, 9);
        // The seats: under the top chord at each end, the seat's depth down, as long as the type says.
        var seats = g.Parts.Where(p => p.Key!.StartsWith("seat:")).ToList();
        Assert.NotEmpty(seats);
        Assert.Equal(top - spec.SeatDepth, seats.Min(p => Measure.Bounds(p.Solid)!.Value.Min.Z), 6);
        Assert.Equal(spec.SeatLength, seats.Where(p => p.Key!.StartsWith("seat:start")).Max(p => Measure.Bounds(p.Solid)!.Value.Max.X), 6);
        // In plan: its line, its type's name over it, picked by its footprint.
        Assert.Contains(g.Plan!, p => p.Kind == "symbol" && p.Points.Count == 2);
        Assert.Contains(g.Plan!, p => p.Kind == "text" && p.Text == "J400");
        Assert.Contains(g.Plan!, p => p.Kind == "hit");
        // Its quantities: its length, its weight from its body.
        var q = f.Session.Quantities(id).Single(s => s.Name == "Qto_BeamBaseQuantities");
        Assert.Equal(6, q.Quantities.Single(x => x.Name == "Length").Value, 9);
        Assert.Equal(Volume(f, id) * SteelModule.SteelDensity, q.Quantities.Single(x => x.Name == "NetWeight").Value, 6);
        Assert.Equal(webs.Count, f.Session.Quantities(id).Single(s => s.Name == "Lapin_JoistQuantities").Quantities.Single(x => x.Name == "WebCount").Value);

        // Its type's rules change it: webs drawn as lines have no body, verticals add one at each inner panel point.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(type), ["attrs"] = new JsonObject { ["webs"] = "lines", ["verticals"] = true } });
        g = f.Session.Geometry(id)!;
        Assert.DoesNotContain(g.Parts, p => p.Key!.StartsWith("web:"));
        Assert.Equal(2 + 2 * 8 + 7, g.Lines!.Count);
    }

    [Fact]
    public void Rules_no_joist_has_are_refused()
    {
        var f = F();
        Assert.Throws<CommandException>(() => f.Run("steel.createJoistType", new JsonObject { ["depth"] = 0.03 }));
        Assert.Throws<CommandException>(() => f.Run("steel.createJoistType", new JsonObject { ["preset"] = "nothing" }));
        Assert.Throws<CommandException>(() => f.Run("steel.createJoistType", new JsonObject { ["topThickness"] = 0.05 }));
        Assert.Throws<CommandException>(() => f.Run("steel.addJoist", new JsonObject { ["x1"] = 0, ["y1"] = 0, ["x2"] = 0.05, ["y2"] = 0 }));
    }

    [Fact]
    public void A_joist_exports_as_an_IfcBeam_JOIST_and_comes_back_as_itself()
    {
        var f = F();
        var id = (string)f.Run("steel.addJoist", new JsonObject { ["storeyId"] = Storey(f), ["x1"] = 1, ["y1"] = 2, ["x2"] = 1, ["y2"] = 9 })!["id"]!;
        var volume = Volume(f, id);
        var path = Path.Combine(Path.GetTempPath(), $"joist-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var beam = Assert.Single(step.OfType("IFCBEAM"));
            Assert.Equal("JOIST", (beam[8] as StepEnum)?.Value);
            var type = Assert.Single(step.OfType("IFCBEAMTYPE"));
            Assert.Equal("JOIST", (type[9] as StepEnum)?.Value);
            Assert.Empty(step.OfType("IFCMEMBER"));
            // Its body read back: the joist's own, every chord, seat and web.
            var model = new IfcModel(step);
            var product = model.Products.Single(p => p.Class.Equals("IfcBeam", StringComparison.OrdinalIgnoreCase));
            var (body, _) = model.Shape(product.Id, f.Session.Catalog.Get<ISolidBooleans>());
            Assert.Equal(volume, body.Sum(b => Math.Abs(MeshVolume(b.Mesh))), 6);

            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var back = g.Model.Get(id)!;
            Assert.Equal(OpenWebJoists.Class, back.Class);
            Assert.Equal(OpenWebJoists.TypeClass, g.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Class);
            Assert.Equal(volume, Volume(g, id), 6);
        }
        finally { File.Delete(path); }
    }

    static double MeshVolume(MeshBuilder m)
    {
        double v = 0;
        foreach (var (a, b, c) in m.T) v += Vec3.Dot(m.V[a], Vec3.Cross(m.V[b], m.V[c]));
        return v / 6;
    }

    // ------------------------------------------------------------ recognition

    /// <summary>
    /// A bar joist as Revit's K-Series family sends it (16K2, 4.18 m, feet and inches): its chords' angles 1¼" × ¼", 3/8" apart,
    /// the top chord a leg under the bearing, its ends' seats drawn as a sheet on the end faces (no thickness), the bottom
    /// chord 28" short of each end; its webs only as lines. Along x from (x0, y), its bearing at <paramref name="top"/>.
    /// </summary>
    internal static (List<Vec3> V, List<(int, int, int)> T, List<(Vec3, Vec3)> Lines) RevitJoist(double x0, double y, double top, double length = 4.1783, double sag = 0)
    {
        const double inch = 0.0254;
        var (leg, t, half, depth, seat, setback) = (1.25 * inch, 0.25 * inch, 0.1875 * inch, 16 * inch, 4 * inch, 28 * inch);
        var v = new List<Vec3>();
        var tris = new List<(int, int, int)>();
        // An angle swept along x through the stations given, its outline (w, z) in the joist's frame (w out from its middle).
        void Sweep(IReadOnlyList<Vec2> outline, double side, double[] stations, Func<double, double, double>? dz = null)
        {
            var n = outline.Count;
            var at = v.Count;
            foreach (var u in stations)
                foreach (var q in outline) v.Add(new Vec3(x0 + u, y + side * q.X, top + q.Y + (dz?.Invoke(u, q.Y) ?? 0)));
            var ring = outline.Select(q => new Vec3(q.X, q.Y, 0)).ToList();
            var cap = Triangulator.Face(ring, [.. Enumerable.Range(0, n)], null, Vec3.UnitZ);
            var flip = side < 0;
            void Tri(int a, int b, int c) => tris.Add(flip ? (a, c, b) : (a, b, c));
            // The caps: the first station looks back along −x, the last along +x; the sides between each station and the next.
            foreach (var (a, b, c) in cap) { Tri(at + a, at + b, at + c); Tri(at + (stations.Length - 1) * n + a, at + (stations.Length - 1) * n + c, at + (stations.Length - 1) * n + b); }
            for (var s = 0; s + 1 < stations.Length; s++)
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n;
                    var (p, q, r, w) = (at + s * n + i, at + s * n + j, at + (s + 1) * n + j, at + (s + 1) * n + i);
                    Tri(p, r, q); Tri(p, w, r);
                }
        }
        // The top chord's angle: its flange at its bottom, its top a leg under the bearing; the bottom chord's, its flange at the bottom.
        List<Vec2> L(double ct) => [new(half, ct - leg), new(half + leg, ct - leg), new(half + leg, ct - leg + t), new(half + t, ct - leg + t), new(half + t, ct), new(half, ct)];
        foreach (var side in new[] { 1.0, -1.0 })
        {
            Sweep(L(-leg), side, [0, seat, length - seat, length]);
            // The bottom chord, its middle station lowered by sag (a deformed body).
            Sweep(L(-depth + leg), side, [setback, length / 2, length - setback], (u, _) => Math.Abs(u - length / 2) < 1e-9 ? -sag : 0);
            // The seat as Revit draws it: a sheet on each end face, from the chord's top to the bearing, its flange at the top.
            foreach (var u in new[] { 0.0, length })
            {
                var at = v.Count;
                foreach (var q in new Vec2[] { new(half, -leg), new(half + t, -leg), new(half + t, -t), new(half + leg, -t), new(half + leg, 0), new(half, 0) })
                    v.Add(new Vec3(x0 + u, y + side * q.X, top + q.Y));
                foreach (var (a, b, c) in new[] { (0, 1, 2), (0, 2, 5), (2, 3, 4), (2, 4, 5) }) tris.Add((at + a, at + b, at + c));
            }
        }
        // The webs: a Warren between the chords, as lines.
        var lines = new List<(Vec3, Vec3)>();
        var (zt, zb) = (top - 0.5 * inch, top - depth + 0.5 * inch);
        lines.Add((new Vec3(x0 + seat, y, top - leg), new Vec3(x0 + setback + 4 * inch, y, zb)));
        lines.Add((new Vec3(x0 + length - seat, y, top - leg), new Vec3(x0 + length - setback - 4 * inch, y, zb)));
        for (var u = setback + 4 * inch; u + 20 * inch <= length - setback; u += 20 * inch)
        {
            lines.Add((new Vec3(x0 + u, y, zb), new Vec3(x0 + u + 10 * inch, y, zt)));
            lines.Add((new Vec3(x0 + u + 10 * inch, y, zt), new Vec3(x0 + u + 20 * inch, y, zb)));
        }
        return (v, tris, lines);
    }

    /// <summary>A reference as a receive or an import brings one: a beam of the joist's family and type, its body triangles, its lines kept.</summary>
    static string Reference(Fixture f, (List<Vec3> V, List<(int, int, int)> T, List<(Vec3, Vec3)> Lines) body, string name = "Structural Framing - 16K2")
    {
        var id = GlobalId.New();
        f.Session.Edit("Reference", edit =>
        {
            var attrs = new JsonObject
            {
                ["reference"] = true, ["ifcClass"] = IfcClass.Beam,
                [StoredBodies.Key] = StoredBodies.Write([("Steel", "#8a96a3", body.V, body.T.Select(t => (t.Item1, t.Item2, t.Item3)).ToList())]),
                [MeshRecognition.TypeNameKey] = "16K2",
                [BodyLines.Key] = BodyLines.Write(body.Lines),
                ["properties"] = new JsonObject { ["Speckle"] = new JsonObject { ["family"] = "K-Series Bar Joist-Angle Web", ["type"] = "16K2", ["category"] = "Structural Framing" } },
            };
            edit.Add(new Entity(id, IfcClass.Beam, name, attrs));
            Spatial.Contain(edit, Storey(f), id);
        });
        return id;
    }

    static MeshRecognitionReport Recognise(Fixture f, params string[] ids)
    {
        MeshRecognitionReport r = MeshRecognitionReport.None;
        f.Session.Edit("Recognise", edit => r = MeshRecognition.Recognize(edit, f.Session.Catalog, ids, null, outlineSections: true));
        return r;
    }

    [Fact]
    public void A_received_bar_joist_comes_in_native_its_webs_the_lines_its_file_drew()
    {
        var f = F();
        var body = RevitJoist(1, 2, 3);
        var id = Reference(f, body);
        var (lo0, hi0) = f.Bounds(id);
        var r = Recognise(f, id);
        var e = f.Model.Get(id)!;
        Assert.Equal(OpenWebJoists.Class, e.Class);
        Assert.False(StoredBodies.Has(e));
        Assert.Equal(1, r.Recognized);
        var check = Assert.Single(r.Fidelity!.Checks);
        Assert.True(check.Pass, check.Reason);
        Assert.False(check.Reverted);
        // The file's seats are sheets, not solids: its body is not closed, so the gate compares its box and its base outline.
        Assert.Null(check.Volume);
        var (lo, hi) = f.Bounds(id);
        Assert.True((lo - lo0).Length < 1e-6 && (hi - hi0).Length < 1e-6, $"{lo} {hi} / {lo0} {hi0}");
        // Its rules read from the body (as a reference keeps it, to 0.1 mm): 16" deep, angles 1¼" × ¼", 3/8" apart, the top chord a
        // leg under its bearing, 28" setback, 4" seats 2½" deep.
        var spec = OpenWebJoists.SpecOf(f.Model, e);
        const double inch = 0.0254;
        void Near(double expected, double actual) => Assert.InRange(actual, expected - 1.5e-4, expected + 1.5e-4);
        Near(16 * inch, spec.Depth);
        foreach (var c in new[] { spec.Top, spec.Bottom })
        {
            Near(1.25 * inch, c.Leg); Near(1.25 * inch, c.Height); Near(0.25 * inch, c.Thickness);
            Assert.Equal("bottom", c.Flange);
        }
        Near(0.375 * inch, spec.Gap);
        Near(1.25 * inch, spec.TopDrop);
        Near(28 * inch, spec.BottomSetback);
        Near(4 * inch, spec.SeatLength);
        Near(2.5 * inch, spec.SeatDepth);
        Assert.Equal("lines", spec.Webs);
        // Its webs the file's lines, as they were; its type named after the file's, its own name kept.
        Assert.Equal(body.Lines.Count, OpenWebJoists.WebAxes(e, spec, 4.1783).Count);
        var g = f.Session.Geometry(id)!;
        foreach (var (a, b) in body.Lines)
            Assert.Contains(g.Lines!, l => (l[0] - a).Length < 1e-4 && (l[1] - b).Length < 1e-4);
        Assert.Equal("16K2", f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Name);
        Assert.Equal("Structural Framing - 16K2", e.Name);
    }

    [Fact]
    public void A_deformed_bar_joist_stays_the_reference_it_was()
    {
        var f = F();
        // The bottom chord sagging 20 mm at its middle: no straight angle, no joist Lapin makes as it is.
        var sagging = Reference(f, RevitJoist(1, 2, 3, sag: 0.02));
        // Its webs as solids beside its chords: more pieces than two pairs of chords.
        var body = RevitJoist(1, 6, 3);
        var extra = RevitJoist(1, 6, 3);
        var off = body.V.Count;
        body.V.AddRange(new[] { new Vec3(2, 6, 2.8), new Vec3(2.01, 6, 2.8), new Vec3(2, 6.01, 2.8), new Vec3(2, 6, 2.81) });
        body.T.AddRange([(off, off + 2, off + 1), (off, off + 1, off + 3), (off, off + 3, off + 2), (off + 1, off + 2, off + 3)]);
        var webbed = Reference(f, body);
        // Named a joist, but a plain box: no chords.
        var box = Reference(f, (
            [new(10, 0, 2.6), new(14, 0, 2.6), new(14, 0.2, 2.6), new(10, 0.2, 2.6), new(10, 0, 3), new(14, 0, 3), new(14, 0.2, 3), new(10, 0.2, 3)],
            [(0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7), (0, 1, 5), (0, 5, 4), (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6), (3, 0, 4), (3, 4, 7)], []));
        var r = Recognise(f, sagging, webbed, box);
        foreach (var id in new[] { sagging, webbed })
        {
            var e = f.Model.Get(id)!;
            Assert.Equal(IfcClass.Beam, e.Class);
            Assert.True(StoredBodies.Has(e));
        }
        Assert.Contains(r.Skipped.Keys, k => k.Contains("not straight"));
        Assert.Contains(r.Skipped.Keys, k => k.Contains("not two pairs of chords"));
        // The box is a plain beam, by its prism, as any other.
        Assert.Equal(IfcClass.Beam, f.Model.Get(box)!.Class);
        Assert.Empty(f.Model.OfClass(OpenWebJoists.Class));
        Assert.Empty(f.Model.OfClass(OpenWebJoists.TypeClass));
    }

    [Fact]
    public void A_joist_cut_short_at_one_end_stays_a_reference()
    {
        var f = F();
        // The joist's top chord 3 mm short of its far end (the seat sheet still at the end): the native top chord runs to the bearing.
        var body = RevitJoist(1, 2, 3);
        var far = body.V.Max(p => p.X);
        for (var i = 0; i < body.V.Count; i++)
            if (Math.Abs(body.V[i].X - far) < 1e-9 && body.V[i].Z < 3 - 0.0254 * 1.25 + 1e-6 && body.V[i].Z > 3 - 0.0254 * 2.5 - 1e-6 && Math.Abs(body.V[i].Y - 2) > 0) body.V[i] = body.V[i] with { X = far - 0.003 };
        var id = Reference(f, body);
        var r = Recognise(f, id);
        Assert.True(StoredBodies.Has(f.Model.Get(id)!));
        Assert.Equal(0, r.Recognized);
        Assert.Empty(f.Model.OfClass(OpenWebJoists.TypeClass));
    }
}
