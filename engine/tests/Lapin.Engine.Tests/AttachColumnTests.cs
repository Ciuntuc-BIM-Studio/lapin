using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;
using Lapin.Studio;
using Xunit;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage A2 of docs/plan-attach-to.md: a column's top and bottom attached to bodies, with the plan's numbers. The sloped
/// bodies are a test class of this file's own (<see cref="BodiesModule"/>): profiles in the x–z plane swept across y, so
/// an underside is exactly the plane the plan names (the sloped body: z = 3.00 + 0.20·x over x 0..6, y −1..1).
/// </summary>
public class AttachColumnTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new RebarModule(), new DraftingModule(), new StudioModule(), new BodiesModule());
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static IReadOnlyList<string> Problems(Fixture f, string id) => f.Session.Geometry(id)!.Diagnostics ?? [];
    static List<Vec3> Vertices(Fixture f, string id) => [.. f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices)];
    static double Length(Fixture f, string id) => f.Session.Quantities(id).Single(q => q.Name == "Qto_ColumnBaseQuantities").Quantities.Single(q => q.Name == "Length").Value;
    static IServices Services(Fixture f) => (IServices)f.Session.Catalog;

    static JsonArray P(double x, double z) => new(x, z);

    /// <summary>A body of profiles in x–z (each a part), swept from y0 to y1.</summary>
    static string Body(Fixture f, double y0, double y1, params JsonArray[] profiles) =>
        (string)f.Run("bodies.add", new JsonObject { ["y0"] = y0, ["y1"] = y1, ["parts"] = new JsonArray([.. profiles.Select(p => (JsonNode)p)]) })!["id"]!;

    /// <summary>The plan's sloped body: underside z = 3 + 0.2·x over x 0..6, y −1..1, its top at 6.</summary>
    static string Sloped(Fixture f) => Body(f, -1, 1, new JsonArray(P(0, 3), P(6, 4.2), P(6, 6), P(0, 6)));

    /// <summary>A 0.30 × 0.30 column at a point of the ground storey, of its own type (and own height when given).</summary>
    static string Column(Fixture f, double x, double y, double? height = null, string? storey = null)
    {
        var a = new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } };
        if (height != null) a["height"] = height;
        var type = (string)f.Run("structure.createColumnType", a)!["id"]!;
        return (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = storey ?? f.Storey, ["x"] = x, ["y"] = y, ["typeId"] = type })!["id"]!;
    }

    static JsonObject Attach(string end, string mode, string target, params string[] ids) =>
        new() { ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]), ["end"] = end, ["targets"] = new JsonArray(target), ["mode"] = mode };

    static JsonObject End(Fixture f, string id, string end) =>
        (JsonObject)((JsonArray)((JsonObject)f.Session.Query("element.attachments", new JsonObject { ["id"] = id })!)["elements"]![0]!["ends"]!).Single(e => (string)e!["end"]! == end)!;

    /// <summary>Storey 1 at +3.00 and a default floor on it over the box: its soffit at 2.78 (0.02 + 0.05 + 0.15).</summary>
    static string Floor(Fixture f, double x0, double y0, double x1, double y1)
    {
        var upper = (string)f.Run("storey.create", [])!["id"]!;
        return (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = upper, ["points"] = new JsonArray(new JsonArray(x0, y0), new JsonArray(x1, y0), new JsonArray(x1, y1), new JsonArray(x0, y1)),
        })!["id"]!;
    }

    // ------------------------------------------------------------ the basic case

    [Fact]
    public void A_column_attached_under_the_sloped_body_stands_3_400_on_its_axis()
    {
        var f = F();
        var body = Sloped(f);
        var c = Column(f, 2, 0);
        Assert.Equal(0.27, Vol(f, c), 9);
        f.Run("element.attachTo", Attach("top", "both", body, c));
        Assert.Equal(3.43, f.Bounds(c).Max.Z, 6);
        var vs = Vertices(f, c);
        Assert.Equal(3.37, vs.Where(v => v.X < 1.9).Max(v => v.Z), 6);
        Assert.Equal(3.43, vs.Where(v => v.X > 2.1).Max(v => v.Z), 6);
        Assert.Equal(0.3060, Vol(f, c), 9);
        Assert.Equal(3.4, Length(f, c), 9);
        Assert.Empty(Problems(f, c));
        // What element.attachments says of it: one plane, its normal out of the top.
        var end = End(f, c, "top");
        Assert.Equal(3.4, (double)end["result"]!["built"]!, 9);
        Assert.Equal("plane", (string)end["result"]!["path"]!);
        var n = (JsonArray)end["result"]!["planes"]![0]!;
        Assert.Equal(-0.196, (double)n[0]!, 3);
        Assert.Equal(0, (double)n[1]!, 9);
        Assert.Equal(0.981, (double)n[2]!, 3);

        // IFC: the sweep from its lowest corner clipped by one half-space, its normal (−0.196, 0, 0.981).
        var swept = Services(f).All<IIfcSweptBodies>().Select(s => s.BodyOf(f.Model, Services(f), f.Model.Get(c)!)).OfType<IfcSweptBody>().First();
        var clip = Assert.Single(swept.Clips);
        Assert.Equal(-0.196, clip.Normal.X, 3);
        Assert.Equal(0.981, clip.Normal.Z, 3);
        Assert.Equal(3.43, swept.Body.Depth, 6);
        var path = Path.Combine(Path.GetTempPath(), $"attach-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            Assert.Single(step.OfType("IFCBOOLEANCLIPPINGRESULT"));
            Assert.Single(step.OfType("IFCHALFSPACESOLID"));
        }
        finally { File.Delete(path); }

        // The body raised 0.50: the column follows (live).
        f.Session.Edit("raise", e => { var b = e.Model.Get(body)!; var a = b.CloneAttrs(); a["dz"] = 0.5; e.Replace(b.With(attrs: a)); });
        Assert.Equal(3.93, f.Bounds(c).Max.Z, 6);
        Assert.Equal(3.9, Length(f, c), 9);
        // Detached: its own 3.00 again, one undo step.
        f.Run("element.detach", new JsonObject { ["ids"] = new JsonArray(c), ["end"] = "top" });
        Assert.Equal(3, f.Bounds(c).Max.Z, 9);
        Assert.Equal(0.27, Vol(f, c), 9);
        Assert.Equal(3, Length(f, c), 9);
        f.Session.Undo();
        Assert.Equal(3.93, f.Bounds(c).Max.Z, 6);
    }

    [Fact]
    public void A_column_under_a_Studio_loft_s_slope_takes_the_same_way()
    {
        // RoofTests' body: a loft from a 2 × 1 square at +3.00 to the same 8 m on at +6.00, its underside rising 3 over 8 past x = 2.
        var f = F();
        var upper = (string)f.Run("storey.create", [])!["id"]!;
        var top = (string)f.Run("storey.create", [])!["id"]!;
        string Plan(string storey) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey })!["id"]!;
        string Draw(string view, double x0, double x1) => (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = view,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(new JsonArray(x0, 0), new JsonArray(x1, 0), new JsonArray(x1, 1), new JsonArray(x0, 1)), ["closed"] = true },
        })!["ids"]![0]!;
        var a = Draw(Plan(upper), 0, 2);
        var b = Draw(Plan(top), 8, 10);
        var loft = (string)f.Run("studio.loft", new JsonObject { ["profileId"] = a, ["profileIds"] = new JsonArray(a, b) })!["id"]!;
        var c = Column(f, 6, 0.5);
        f.Run("element.attachTo", Attach("top", "both", loft, c));
        // 3 + 0.375 · 4 = 4.5 on the axis; 4.44375 and 4.55625 at the faces.
        Assert.Equal(4.5, Length(f, c), 6);
        Assert.Equal(4.55625, f.Bounds(c).Max.Z, 6);
        Assert.Equal(0.09 * 4.5, Vol(f, c), 9);
    }

    // ------------------------------------------------------------ trim and extend under a slab

    [Fact]
    public void Trim_and_extend_under_a_slab_soffit_and_the_slab_keeps_its_hole()
    {
        var f = F();
        var slab = Floor(f, -2, -2, 8, 2);
        var tall = Column(f, 0, 0, 4.0);
        var plainSlab = Vol(f, slab);
        // Trim: the 4.00 column to the soffit at 2.78.
        f.Run("element.attachTo", Attach("top", "trim", slab, tall));
        Assert.Equal(2.78, f.Bounds(tall).Max.Z, 9);
        Assert.Equal(0.2502, Vol(f, tall), 9);
        Assert.Equal(2.78, Length(f, tall), 9);
        // The slab keeps the hole the column's drawn envelope (0 to 4) cuts: the attachment changes nothing of it.
        Assert.Equal(plainSlab, Vol(f, slab), 9);
        Assert.True(plainSlab < 10 * 4 * 0.22 - 0.09 * 0.15);
        // Extend with its own 4.00: it stays 4.00; with its own 2.50: up to 2.78.
        var high = Column(f, 2, 0, 4.0);
        f.Run("element.attachTo", Attach("top", "extend", slab, high));
        Assert.Equal(4, f.Bounds(high).Max.Z, 9);
        var low = Column(f, 4, 0, 2.5);
        f.Run("element.attachTo", Attach("top", "extend", slab, low));
        Assert.Equal(2.78, f.Bounds(low).Max.Z, 9);
        // Trim with its own 2.50: it stays 2.50.
        var lowTrim = Column(f, 6, 0, 2.5);
        f.Run("element.attachTo", Attach("top", "trim", slab, lowTrim));
        Assert.Equal(2.5, f.Bounds(lowTrim).Max.Z, 9);
        Assert.All(new[] { tall, high, low, lowTrim }, c => Assert.Empty(Problems(f, c)));
    }

    // ------------------------------------------------------------ a ridge, a step

    [Fact]
    public void Under_a_ridge_a_column_takes_two_clips()
    {
        var f = F();
        // A gable: z = 3 + 0.2·x up to x = 2, z = 3.8 − 0.2·x after, one body of two parts.
        var gable = Body(f, -1, 1, new JsonArray(P(0, 3), P(2, 3.4), P(2, 6), P(0, 6)), new JsonArray(P(2, 3.4), P(6, 2.6), P(6, 6), P(2, 6)));
        var c = Column(f, 2, 0);
        f.Run("element.attachTo", Attach("top", "both", gable, c));
        Assert.Equal(3.4, f.Bounds(c).Max.Z, 6);
        var vs = Vertices(f, c);
        Assert.Equal(3.37, vs.Where(v => v.X < 1.9).Max(v => v.Z), 6);
        Assert.Equal(3.37, vs.Where(v => v.X > 2.1).Max(v => v.Z), 6);
        Assert.Equal(0.09 * 3.40 - 2 * (0.15 * 0.30 * 0.03 / 2), Vol(f, c), 9);
        Assert.Equal(0.30465, Vol(f, c), 9);
        Assert.Equal("planes", (string)End(f, c, "top")["result"]!["path"]!);
        var swept = Services(f).All<IIfcSweptBodies>().Select(s => s.BodyOf(f.Model, Services(f), f.Model.Get(c)!)).OfType<IfcSweptBody>().First();
        Assert.Equal(2, swept.Clips.Count);
        // Its side faces have the ridge's point: 3.40 at the middle of the sides along x.
        var faces = ElementFaces.Of(Services(f), f.Model.Get(c)!, f.Model);
        Assert.Contains(faces.Where(x => x.Role.StartsWith("Side")).SelectMany(x => x.Outline), p => Math.Abs(p.X - 2) < 1e-9 && Math.Abs(p.Z - 3.4) < 1e-9);
        Assert.Equal(3.4, faces.Single(x => x.Role == "Top").Outline.Max(p => p.Z), 9);
    }

    [Fact]
    public void A_step_goes_through_the_boolean_path_and_is_said()
    {
        var f = F();
        var slab = Floor(f, -2, -2, 6, 2);
        // A 0.25 × 0.40 beam along y with its top at the storey's 3.00 (soffit 2.60), its edge on the column's axis.
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 2.125, ["y1"] = -2, ["x2"] = 2.125, ["y2"] = 2 })!["id"]!;
        // Its own 2.50 reaches neither: nothing cuts what it is attached to.
        var c = Column(f, 2, 0, 2.5);
        f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray(c), ["end"] = "top", ["targets"] = new JsonArray(beam, slab) });
        Assert.Equal(0.045 * 2.60 + 0.045 * 2.78, Vol(f, c), 9);
        Assert.Equal(0.2421, Vol(f, c), 9);
        Assert.Equal(2.78, f.Bounds(c).Max.Z, 9);
        Assert.Equal("boolean", (string)End(f, c, "top")["result"]!["path"]!);
        Assert.Contains($"Part of its section is not under „{f.Model.Get(beam)!.Name}”.", Problems(f, c));
        // Written to IFC as the B-rep it is.
        Assert.Empty(Services(f).All<IIfcSweptBodies>().Select(s => s.BodyOf(f.Model, Services(f), f.Model.Get(c)!)).OfType<IfcSweptBody>());
    }

    // ------------------------------------------------------------ the cycle rule: what others read stays as drawn

    [Fact]
    public void A_beam_over_attached_columns_keeps_its_clear_span()
    {
        var f = F();
        var slab = Floor(f, -2, -2, 8, 2);
        var a = Column(f, 0, 0);
        var b = Column(f, 5, 0);
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 })!["id"]!;
        var before = Beams.SpanOf(f.Model, f.Model.Get(beam)!).ClearLength;
        Assert.Equal(4.7, before, 9);
        var volume = Vol(f, beam);
        f.Run("element.attachTo", Attach("top", "both", slab, a, b));
        Assert.Equal(2.78, f.Bounds(a).Max.Z, 9);
        Assert.Equal(before, Beams.SpanOf(f.Model, f.Model.Get(beam)!).ClearLength, 9);
        Assert.Equal(volume, Vol(f, beam), 9);
        // The envelope it cuts with is the drawn one.
        Assert.Equal(3, Measure.Bounds(Columns.Envelope(f.Model.Get(a)!, f.Model)!)!.Value.Max.Z, 9);
    }

    // ------------------------------------------------------------ the bottom

    [Fact]
    public void A_bottom_on_a_ramp_stands_on_its_top_face()
    {
        var f = F();
        // A ramp whose top is z = 0.10·x over x 0..6.
        var ramp = Body(f, -1, 1, new JsonArray(P(0, -0.5), P(6, -0.5), P(6, 0.6), P(0, 0)));
        var c = Column(f, 2, 0);
        f.Run("element.attachTo", Attach("bottom", "both", ramp, c));
        var vs = Vertices(f, c);
        Assert.Equal(0.185, vs.Where(v => v.X < 1.9).Min(v => v.Z), 6);
        Assert.Equal(0.215, vs.Where(v => v.X > 2.1).Min(v => v.Z), 6);
        Assert.Equal(3, f.Bounds(c).Max.Z, 9);
        Assert.Equal(2.8, Length(f, c), 9);
        Assert.Equal(0.2520, Vol(f, c), 9);
        Assert.Equal(0.2, (double)End(f, c, "bottom")["result"]!["base"]!, 9);
        // The analytical line from the ramp up.
        var built = Columns.Built(f.Model, f.Model.Get(c)!, Services(f));
        Assert.Equal(0.2, built.Base, 9);
        Assert.Equal(2.8, built.Height, 9);
    }

    // ------------------------------------------------------------ rebar, problems

    [Fact]
    public void Its_bars_and_stirrups_run_its_height_as_built()
    {
        var f = F();
        var body = Sloped(f);
        var c = Column(f, 2, 0);
        var type = f.Model.RelatingOf(c, IfcClass.RelDefinesByType)!.Id;
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = type, ["enabled"] = true });
        f.Run("rebar.setColumnBars", new JsonObject { ["id"] = type, ["enabled"] = true, ["perFace"] = 3, ["diameter"] = 0.016, ["top"] = "hooks" });
        var services = Services(f);
        var plain = ColumnBars.Of(f.Model, f.Model.Get(c)!, null, services)!;
        var plainTop = plain.Bars.Max(b => b.Max(p => p.Z));
        var cover = 3.0 - plainTop;
        var stations = TypeStirrups.Of(f.Model, f.Model.Get(c)!, services)!.Stations.Count;
        f.Run("element.attachTo", Attach("top", "both", body, c));
        var bars = ColumnBars.Of(f.Model, f.Model.Get(c)!, null, services)!;
        Assert.Equal(3.4 - cover, bars.Bars.Max(b => b.Max(p => p.Z)), 9);
        Assert.Equal(plain.Length + 0.4, bars.Length, 9);
        Assert.True(TypeStirrups.Of(f.Model, f.Model.Get(c)!, services)!.Stations.Count > stations);
        // Without the services, as drawn (what the calls before A2 read).
        Assert.Equal(plainTop, ColumnBars.Of(f.Model, f.Model.Get(c)!)!.Bars.Max(b => b.Max(p => p.Z)), 9);
    }

    [Fact]
    public void A_target_away_from_it_is_said_and_it_stands_at_its_own_height()
    {
        var f = F();
        var far = Body(f, -1, 1, new JsonArray(P(7, 3), P(13, 4.2), P(13, 6), P(7, 6)));
        var c = Column(f, 2, 0);
        f.Run("element.attachTo", Attach("top", "both", far, c));
        Assert.Equal(3, f.Bounds(c).Max.Z, 9);
        Assert.Equal(0.27, Vol(f, c), 9);
        Assert.Contains($"Its top is attached to „{f.Model.Get(far)!.Name}”, which is not over it: it stands at its own height.", Problems(f, c));
        Assert.Contains($"which is not over it", ((JsonArray)End(f, c, "top")["problems"]!).Select(p => (string)p!).Single());
        // Refused: a level (columns have their own height for that), a column with steel ends.
        Assert.Throws<CommandException>(() => f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray(c), ["end"] = "top", ["level"] = new JsonObject { ["storey"] = "above" } }));
    }

    [Fact]
    public void The_preview_is_the_end_as_it_would_be_and_writes_nothing()
    {
        var f = F();
        var body = Sloped(f);
        var c = Column(f, 2, 0);
        var revision = f.Model.Revision;
        var ghost = (JsonObject)((JsonObject)f.Session.Query("element.attachPreview", Attach("top", "both", body, c))!)["elements"]![0]!;
        Assert.Equal(revision, f.Model.Revision);
        Assert.Equal(3.4, (double)ghost["built"]!, 9);
        var zs = ((JsonArray)ghost["outline"]!).Select(p => (double)p![2]!).ToList();
        Assert.Equal(3.37, zs.Min(), 6);
        Assert.Equal(3.43, zs.Max(), 6);
        Assert.Equal(3, f.Bounds(c).Max.Z, 9);
    }

    [Fact]
    public void A_circle_through_a_column_is_refused_and_a_forced_one_overflows_nothing()
    {
        var f = F();
        var body = Sloped(f);
        var a = Column(f, 2, 0);
        var b = Column(f, 2, 0, 1);
        f.Run("element.attachTo", Attach("top", "both", body, a));
        f.Run("element.attachTo", Attach("top", "both", a, b));
        Assert.Throws<CommandException>(() => f.Run("element.attachTo", Attach("bottom", "both", b, a)));
        // Forced by hand: each stands at its own end and says so.
        f.Session.Edit("by hand", e => e.AddRelation(new Relation(e.NewId(), IfcClass.RelConnectsElements, b, [a], new JsonObject { ["attach"] = "bottom", ["mode"] = "both" })));
        _ = f.Session.Geometry(a);
        _ = f.Session.Geometry(b);
        Assert.Contains(Problems(f, a).Concat(Problems(f, b)), p => p.Contains("depend on each other"));
    }

    // ------------------------------------------------------------ speed

    /// <summary>
    /// The attachment's speed, timed apart from the parallel tests (<see cref="Scaling.Serial"/>): among them, the full run's load made
    /// the first build take from 365 ms to 2 320 ms on one Linux machine, past its limit (issue #122).
    /// </summary>
    [Collection(Scaling.Serial)]
    public sealed class Speed(ITestOutputHelper output)
    {
        // Measured on 18.11.2026 (Debug, an Apple-silicon Mac, the full suite's machine): 400 columns attached under one slab and
        // built in 66 ms, built again after the slab moved in 54 ms; under 60 bodies of 14 400 triangles, 837 ms. The limits are
        // about thirty and twenty times that, for a slower machine or a busy test run.
        const double LIMIT = 2_000, LIMIT_MANY = 15_000;

        [Fact]
        public void Four_hundred_columns_under_one_slab_read_its_triangles_once_a_revision()
        {
            var f = F();
            var slab = Floor(f, -1, -1, 20, 20);
            var ids = new List<string>();
            // Their own 2.50 stays under the slab: no column cuts it, so what is timed is the attachment, not the slab's 400 holes.
            var type = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["height"] = 2.5 })!["id"]!;
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++)
                for (var j = 0; j < 20; j++)
                    ids.Add((string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = i, ["y"] = j, ["typeId"] = type })!["id"]!);
            foreach (var id in ids) _ = f.Session.Geometry(id);
            var setup = watch.Elapsed.TotalMilliseconds;
            var reads = BodySampler.Reads(slab);
            watch.Restart();
            f.Run("element.attachTo", Attach("top", "both", slab, [.. ids]));
            foreach (var id in ids) _ = f.Session.Geometry(id);
            var first = watch.Elapsed.TotalMilliseconds;
            Assert.All(ids, id => Assert.Equal(2.78, f.Bounds(id).Max.Z, 9));
            // Once for the revision; columns built at once on several threads may each work it out before the first is kept: at most once
            // for each building thread, the pool's and this one, which are more than the cores once the parallel tests have grown the pool.
            var builders = Math.Max(Environment.ProcessorCount, ThreadPool.ThreadCount + 1);
            var once = BodySampler.Reads(slab) - reads;
            Assert.InRange(once, 1, builders);
            // The slab lowered 0.10: every column follows, the slab's triangles worked out again, once for the revision.
            reads = BodySampler.Reads(slab);
            watch.Restart();
            f.Session.Edit("lower", e => { var s = e.Model.Get(slab)!; var a = s.CloneAttrs(); a["offset"] = -0.1; e.Replace(s.With(attrs: a)); });
            foreach (var id in ids) _ = f.Session.Geometry(id);
            var again = watch.Elapsed.TotalMilliseconds;
            Assert.All(ids, id => Assert.Equal(2.68, f.Bounds(id).Max.Z, 9));
            var twice = BodySampler.Reads(slab) - reads;
            Assert.InRange(twice, 1, builders);
            output.WriteLine($"400 columns under one slab: placed and built {setup:0} ms; attached and built {first:0} ms (slab read {once}x); slab moved, rebuilt {again:0} ms (read {twice}x); {builders} building threads at most");
            Assert.True(first < LIMIT, $"attached and built in {first:0} ms");
            Assert.True(again < LIMIT, $"rebuilt in {again:0} ms");
        }

        [Fact]
        public void Four_hundred_columns_under_a_body_of_many_triangles()
        {
            // A Bloc-P-4-sized target stands in: a sloped soffit of 60 × 60 strips, 21 600 triangles, over 400 columns.
            var f = F();
            var parts = new List<JsonArray>();
            for (var i = 0; i < 60; i++)
            {
                var (x0, x1) = (-1 + i * 22.0 / 60, -1 + (i + 1) * 22.0 / 60);
                parts.Add(new JsonArray(P(x0, 3 + 0.05 * x0), P(x1, 3 + 0.05 * x1), P(x1, 6), P(x0, 6)));
            }
            var bodies = Enumerable.Range(0, 60).Select(j => Body(f, -1 + j * 22.0 / 60, -1 + (j + 1) * 22.0 / 60, [.. parts.Select(p => (JsonArray)p.DeepClone())])).ToList();
            var ids = new List<string>();
            var type = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
            for (var i = 0; i < 20; i++)
                for (var j = 0; j < 20; j++)
                    ids.Add((string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = i + 0.4, ["y"] = j + 0.4, ["typeId"] = type })!["id"]!);
            foreach (var id in ids) _ = f.Session.Geometry(id);
            var watch = Stopwatch.StartNew();
            f.Run("element.attachTo", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["end"] = "top", ["targets"] = new JsonArray([.. bodies.Select(x => (JsonNode)x)]) });
            foreach (var id in ids) _ = f.Session.Geometry(id);
            var ms = watch.Elapsed.TotalMilliseconds;
            output.WriteLine($"400 columns under 60 bodies of 60 parts ({BodySampler.TrianglesOf(f.Model.Get(bodies[0])!, f.Model, Services(f), true).Count * 60} triangles): attached and built {ms:0} ms");
            Assert.Equal(3 + 0.05 * 0.4, Length(f, ids[0]), 6);
            Assert.True(ms < LIMIT_MANY, $"{ms:0} ms");
        }
    }

    // ------------------------------------------------------------ the test bodies

    /// <summary>A body of profiles in the x–z plane (each a part) swept from <c>y0</c> to <c>y1</c>, raised by <c>dz</c>.</summary>
    sealed class BodiesModule : ILapinModule
    {
        public const string Class = "LapinTestBody";
        public string Id => "bodies";
        public string Name => "Bodies";

        public void Register(IModuleRegistry r)
        {
            r.Class(new(Class, "Body", [], "#888888"));
            r.Command("add", "Add body", (ctx, a) =>
            {
                var e = new Entity(ctx.NewId(), Class, $"Body {ctx.Model.OfClass(Class).Count() + 1}", new JsonObject
                {
                    ["y0"] = Json.Number(a["y0"]) ?? 0, ["y1"] = Json.Number(a["y1"]) ?? 1, ["dz"] = 0, ["parts"] = a["parts"]!.DeepClone(),
                });
                ctx.Add(e);
                Spatial.Contain(ctx, Spatial.Storeys(ctx.Model).First().Id, e.Id);
                return new JsonObject { ["id"] = e.Id };
            });
            r.Geometry(Class, (e, m, s) =>
            {
                var (y0, y1, dz) = (e.Num("y0"), e.Num("y1"), e.Num("dz"));
                var parts = new List<GeometryPart>();
                foreach (var p in (JsonArray)e.Attrs["parts"]!)
                {
                    var ring = ((JsonArray)p!).Select(q => new Vec3((double)q![0]!, y0, (double)q[1]! + dz)).ToList();
                    if (Prism.ExtrudeOriented(ring, new Vec3(0, y1 - y0, 0)) is { } solid) parts.Add(new GeometryPart(e.Name, "#888888", solid));
                }
                return new ElementGeometry(parts);
            });
        }
    }
}
