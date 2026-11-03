using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Steel, stage 4 (decision 275): a hall's secondary steel — purlins, side and gable rails, the eaves beam, bracing, fly braces, their cleats and gussets, rebuilt over the same ids, IFC.</summary>
public class SteelSecondaryTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
    static double Tan(double deg) => Math.Tan(deg * Math.PI / 180);
    static double Cos(double deg) => Math.Cos(deg * Math.PI / 180);
    static JsonArray J(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    static string Hall(Fixture f, JsonObject? attrs = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = attrs ?? [] };
        return (string)f.Run("steel.addHall", a)!["id"]!;
    }

    static Dictionary<string, Entity> Members(Fixture f, string hall) => Halls.Members(f.Model, hall);
    static Dictionary<string, Entity> Nodes(Fixture f, string hall) => Halls.Nodes(f.Model, hall);
    static IEnumerable<Vec3> Body(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Where(p => p.Key == "body").SelectMany(p => p.Solid.Vertices);
    static int Count(Dictionary<string, Entity> m, string prefix) => m.Keys.Count(k => k.StartsWith(prefix, StringComparison.Ordinal));
    static IEnumerable<GeometryPart> Parts(Fixture f, Entity node) => f.Session.Geometry(node.Id)!.Parts;
    static int Of(Fixture f, Entity node, string part) => Parts(f, node).Count(p => SteelNodes.P(p.Properties!, "Part") == part);

    [Fact]
    public void Purlins_stand_on_the_rafters_at_their_spacing_one_a_bay_or_continuous_past_the_gables()
    {
        var f = F();
        var hall = Hall(f);
        var m = Members(f, hall);
        // 20 m span, IPE 400 columns, 10°: from 0.35 m to 9.8 m across, 9.45 / cos 10° = 9.6 m of slope at 1.5 m → 7 spaces, 8 lines a side.
        var lines = HallSecondary.PurlinLines(HallSpec.Default);
        Assert.Equal(8, lines.Count);
        Assert.Equal(0.35, lines[0], 9);
        Assert.Equal(9.8, lines[^1], 9);
        Assert.Equal(8 * 2 * 5, Count(m, "pur:"));
        var p = m["pur:a:3:2"];
        Assert.Equal(SteelModule.Member, p.Class);
        Assert.Equal("PURLIN", p.PredefinedType);
        Assert.Equal("Z 200×65×2", MemberAxes.SectionOf(f.Model, p).Name);
        // On the rafter's top: its underside in the roof's plane, along the second bay.
        var x = lines[3];
        var top = 6 + (x + 0.2) * Tan(10);
        var n = new Vec3(-Math.Sin(10 * Math.PI / 180), 0, Cos(10));
        var body = Body(f, p.Id).ToList();
        Assert.Equal(0, body.Min(v => Vec3.Dot(v - new Vec3(x, 0, top), n)), 6);
        Assert.Equal(0.2, body.Max(v => Vec3.Dot(v - new Vec3(x, 0, top), n)), 6);
        Assert.Equal(6, body.Min(v => v.Y), 6);
        Assert.Equal(12, body.Max(v => v.Y), 6);
        // The first and last bays' run past the gables by the overhang.
        Assert.Equal(-0.15, Body(f, m["pur:a:0:1"].Id).Min(v => v.Y), 6);
        Assert.Equal(30.15, Body(f, m["pur:b:7:5"].Id).Max(v => v.Y), 6);

        // Continuous: one a line, the whole length.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["purlinRun"] = "continuous" } });
        m = Members(f, hall);
        Assert.Equal(16, Count(m, "pur:"));
        Assert.Equal(30.3, Body(f, m["pur:a:0"].Id).Max(v => v.Y) - Body(f, m["pur:a:0"].Id).Min(v => v.Y), 6);
    }

    [Fact]
    public void Each_purlin_sits_on_a_cleat_on_every_rafter_bolted_through_its_web()
    {
        var f = F();
        var hall = Hall(f);
        var nodes = Nodes(f, hall);
        // Eight lines a side, two sides, six frames.
        Assert.Equal(8 * 2 * 6, nodes.Keys.Count(k => k.StartsWith("cleat@pur:", StringComparison.Ordinal)));
        var mid = nodes["cleat@pur:a:3@f2"];
        Assert.Equal("cleat", SteelNodes.KindOf(mid));
        var js = SteelNodes.MembersOf(mid);
        var m = Members(f, hall);
        Assert.Equal([m["f2:raf:a"].Id, m["pur:a:3:2"].Id, m["pur:a:3:3"].Id], js.Select(j => j.Id));
        Assert.Equal([null, "end", "start"], js.Select(j => j.End));
        // One plate 8 mm (a Z 200), two rows of two M12 bolts a purlin end.
        var spec = SteelNodes.Spec(f.Model, mid);
        Assert.Equal(0.008, spec.Plate, 9);
        Assert.Equal(0.012, spec.Bolt, 9);
        Assert.Equal(1, Of(f, mid, "Plate"));
        Assert.Equal(4, Of(f, mid, "Bolt"));
        // The cleat stands on the rafter's top, square to the roof, as wide as the rafter's flange.
        var plate = Parts(f, mid).Single(p => SteelNodes.P(p.Properties!, "Part") == "Plate");
        var b = Measure.Bounds(plate.Solid)!.Value;
        Assert.Equal(0.17, b.Max.Y - b.Min.Y, 6);
        Assert.Equal(0.008 * 0.17 * 0.12, Measure.Volume(plate.Solid), 9);
        // At a gable, one purlin end each side of it.
        Assert.Equal(2, Of(f, nodes["cleat@pur:a:3@f0"], "Bolt"));
        // The cleats' parts in the schedule.
        Assert.Contains(SteelPartsSchedule.Rows(f.Model), r => r.Part == "Plate" && r.Size == "PL 8×120×170");
    }

    [Fact]
    public void Side_rails_run_bay_by_bay_on_the_columns_and_gable_rails_post_to_post_under_the_roof()
    {
        var f = F();
        var hall = Hall(f);
        var m = Members(f, hall);
        // From 0.3 m to under the 6 m eaves at 1.5 m: four spaces, five levels; two walls, five bays.
        var levels = HallSecondary.RailLevels(HallSpec.Default);
        Assert.Equal(5, levels.Count);
        Assert.Equal(0.3, levels[0], 9);
        Assert.Equal(6 - 0.065 / 2 - 0.05, levels[^1], 9);
        Assert.Equal(5 * 2 * 5, Count(m, "rail:"));
        var rail = m["rail:a:1:3"];
        Assert.Equal("USERDEFINED", rail.PredefinedType);
        Assert.Equal("Side rail", rail.Str("objectType"));
        // On the column's outer face (0.2 m out from axis A), its web level, 200 mm out, toes down.
        var body = Body(f, rail.Id).ToList();
        Assert.Equal(-0.2, body.Max(v => v.X), 6);
        Assert.Equal(-0.4, body.Min(v => v.X), 6);
        Assert.Equal(levels[1] + 0.0325, body.Max(v => v.Z), 6);
        Assert.Equal(levels[1] - 0.0325, body.Min(v => v.Z), 6);
        var web = body.Where(v => v.Z > levels[1] + 0.03).ToList();
        Assert.True(web.Max(v => v.X) - web.Min(v => v.X) > 0.19, "the web is at the top");
        // Gable rails between the posts (0, 5, 10, 15, 20 m), as many levels as fit under the gable rafter: 4, 5, 5, 4 a gable.
        Assert.Equal(2 * 18, Count(m, "grail:"));
        Assert.False(m.ContainsKey("grail:0:4:1"));
        Assert.True(m.ContainsKey("grail:0:4:2"));
        // Outside the posts' outer face, along x.
        var g = Body(f, m["grail:0:2:2"].Id).ToList();
        Assert.Equal(-0.12, g.Max(v => v.Y), 6);
        Assert.Equal(5, g.Min(v => v.X), 6);
        Assert.Equal(10, g.Max(v => v.X), 6);
        // Cleats on the columns and posts: one a level a frame a wall, one a level a post.
        var nodes = Nodes(f, hall);
        Assert.Equal(5 * 2 * 6, nodes.Keys.Count(k => k.StartsWith("cleat@rail:", StringComparison.Ordinal)));
        Assert.Equal(2 * (4 + 5 + 5 + 5 + 4), nodes.Keys.Count(k => k.StartsWith("cleat@grail:", StringComparison.Ordinal)));
        // A corner post is narrower than the columns: the cleat bridges the gap to the rail's face.
        var corner = nodes["cleat@rail:a:1@f0"];
        var plate = Parts(f, corner).Single(p => SteelNodes.P(p.Properties!, "Part") == "Plate");
        Assert.Equal(-0.06, Measure.Bounds(plate.Solid)!.Value.Max.X, 6);
    }

    [Fact]
    public void An_eaves_beam_takes_the_top_of_the_walls_and_the_rails_stay_a_spacing_under_it()
    {
        var f = F();
        var hall = Hall(f, new JsonObject { ["eavesBeam"] = true });
        var m = Members(f, hall);
        Assert.Equal(2 * 5, Count(m, "eb:"));
        var eb = m["eb:b:2"];
        Assert.Equal("Eaves beam", eb.Str("objectType"));
        var body = Body(f, eb.Id).ToList();
        Assert.Equal(6 - 0.05, body.Max(v => v.Z), 6);
        Assert.Equal(20.2, body.Min(v => v.X), 6);
        var levels = HallSecondary.RailLevels(Halls.SpecOf(f.Model.Get(hall)!));
        Assert.True(levels[^1] <= 6 - 0.05 - 0.0325 - 1.5 + 1e-9);
        Assert.Equal(2 * 6, Nodes(f, hall).Keys.Count(k => k.StartsWith("cleat@eb:", StringComparison.Ordinal)));
    }

    [Fact]
    public void Braced_bays_take_X_bracing_in_the_roof_and_the_walls_on_gusset_plates()
    {
        var f = F();
        var hall = Hall(f, new JsonObject { ["bracing"] = J(1, 5) });
        var m = Members(f, hall);
        // The roof: 9.2 m of rafter to brace (0.5 m to 9.7 m) over a 6 m bay: two panels a side, two diagonals each.
        Assert.Equal(2 * 2 * 2 * 2, Count(m, "rbr:"));
        // The walls: from 0.3 m to under the haunch, about one panel; both walls.
        Assert.Equal(2 * 2 * 2, Count(m, "wbr:"));
        var d = m["wbr:5:b:0:1"];
        Assert.Equal("BRACE", d.PredefinedType);
        Assert.Equal("Ø20", MemberAxes.SectionOf(f.Model, d).Label);
        // In the wall's plane, beside it by half the gusset and half the bar; from frame 5 to frame 6 (y 24 to 30), set back from the columns' webs.
        var body = Body(f, d.Id).ToList();
        Assert.Equal(20 + 0.005 + 0.01, body.Average(v => v.X), 3);
        Assert.True(body.Min(v => v.Y) > 24 + 0.0043 && body.Max(v => v.Y) < 30 - 0.0043);
        // A gusset at each panel point a diagonal comes to: roof (3 a frame a side) and walls (2 a frame a wall), both ends of each braced bay.
        var nodes = Nodes(f, hall);
        Assert.Equal(2 * (2 * 2 * 3 + 2 * 2 * 2), nodes.Keys.Count(k => k.StartsWith("gusset@", StringComparison.Ordinal)));
        var g = nodes["gusset@rbr:1:a:1@f0"];
        Assert.Equal(2, SteelNodes.MembersOf(g).Count(j => j.End != null));
        // A round bar's end takes its nut, not bolts: one plate.
        Assert.Equal(1, Of(f, g, "Plate"));
        Assert.Equal(0, Of(f, g, "Bolt"));
        // Angles: two bolts a diagonal end.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["brace"] = "L 60×60×6" } });
        Assert.Equal(4, Of(f, Nodes(f, hall)["gusset@rbr:1:a:1@f0"], "Bolt"));
        Assert.Equal(16 + 8, Count(Members(f, hall), "rbr:") + Count(Members(f, hall), "wbr:"));
        // No wall bracing, then none at all.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["wallBracing"] = false } });
        Assert.Equal(0, Count(Members(f, hall), "wbr:"));
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["bracing"] = new JsonArray() } });
        Assert.Equal(0, Count(Members(f, hall), "rbr:"));
        Assert.DoesNotContain(Nodes(f, hall).Keys, k => k.StartsWith("gusset@", StringComparison.Ordinal));
    }

    [Fact]
    public void Fly_braces_hold_the_rafters_and_columns_inner_flange_from_every_second_purlin_and_rail()
    {
        var f = F();
        var hall = Hall(f);
        var m = Members(f, hall);
        // Clear of the haunches: purlins 3 and 5 of 0…7; four frames between the gables, both sides of each rafter.
        Assert.Equal(2 * 2 * 2 * 4, Count(m, "fly:"));
        Assert.False(m.ContainsKey("fly:1:a:1:p"));
        // Rails 1 and 3 of 0…4 (under the haunch), both walls.
        Assert.Equal(2 * 2 * 2 * 4, Count(m, "cfly:"));
        var fly = m["fly:2:a:3:p"];
        Assert.Equal("Fly brace", fly.Str("objectType"));
        // From the purlin's web, down to the rafter's bottom flange, at 45° (as far along as the rafter is deep).
        var ax = MemberAxes.Of(f.Model, fly)!;
        var x = HallSecondary.PurlinLines(HallSpec.Default)[3];
        var rafterTop = 6 + (x + 0.2) * Tan(10);
        Assert.Equal(12 + 0.36, ax.Start.Y, 6);
        Assert.Equal(12 + 0.085 - 0.025, ax.End.Y, 6);
        Assert.Equal(rafterTop - (0.36 - 0.0127 / 2) * Cos(10), ax.End.Z, 6);
        var node = Nodes(f, hall)["fly@fly:2:a:3:p"];
        Assert.Equal(2, Of(f, node, "Bolt"));
        // Off: none, nor their connections.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["flyBraces"] = false } });
        Assert.Equal(0, Count(Members(f, hall), "fly:") + Count(Members(f, hall), "cfly:"));
        Assert.DoesNotContain(Nodes(f, hall).Keys, k => k.StartsWith("fly@", StringComparison.Ordinal));
    }

    [Fact]
    public void A_hall_rebuilt_keeps_its_secondary_steels_ids_and_its_secondary_steel_off_takes_it_away()
    {
        var f = F();
        var hall = Hall(f);
        var before = Members(f, hall).ToDictionary(x => x.Key, x => x.Value.Id);
        var nodesBefore = Nodes(f, hall).ToDictionary(x => x.Key, x => x.Value.Id);
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["eaves"] = 6.5, ["purlin"] = "Z 150×55×2" } });
        var after = Members(f, hall);
        Assert.All(after.Where(x => before.ContainsKey(x.Key)), x => Assert.Equal(before[x.Key], x.Value.Id));
        Assert.Equal(before["pur:a:3:2"], after["pur:a:3:2"].Id);
        Assert.Equal("Z 150×55×2", MemberAxes.SectionOf(f.Model, after["pur:a:3:2"]).Name);
        Assert.Equal(nodesBefore["cleat@pur:a:3@f2"], Nodes(f, hall)["cleat@pur:a:3@f2"].Id);
        // Off: the frames as stage 3 built them.
        f.Run("steel.updateHall", new JsonObject { ["id"] = hall, ["attrs"] = new JsonObject { ["secondary"] = false } });
        Assert.Equal(4 * 4 + 2 * 7, Members(f, hall).Count);
        Assert.Equal(42, Nodes(f, hall).Count);
        f.Session.Undo();
        Assert.Equal(after.Count, Members(f, hall).Count);
        // Deleting the hall takes its secondary steel too.
        f.Run("element.delete", new JsonObject { ["id"] = hall });
        Assert.Empty(f.Model.OfClass(SteelModule.Member));
        Assert.Empty(f.Model.OfClass(SteelNodes.Class));
    }

    [Fact]
    public void The_preview_counts_and_weighs_the_secondary_steel()
    {
        var f = F();
        var p = (JsonObject)f.Session.Query("steel.hallPreview", new JsonObject { ["attrs"] = new JsonObject { ["eavesBeam"] = true } })!;
        Assert.Null(p["problem"]);
        var sum = p["summary"]!;
        Assert.Equal(80, (int)sum["purlins"]!);
        // Under an eaves beam, four levels of side rails (a spacing under it); the gable rails as before.
        Assert.Equal(40 + 36, (int)sum["rails"]!);
        Assert.Equal(10, (int)sum["eavesBeams"]!);
        Assert.Equal(12, (int)sum["braces"]!);
        Assert.Equal(64, (int)sum["flyBraces"]!);
        var hall = Hall(f, new JsonObject { ["eavesBeam"] = true });
        double Kg(string kind) => Members(f, hall).Where(x => Halls.KindOfRole(HallSpec.Default, x.Key) == kind)
            .SelectMany(x => f.Session.Geometry(x.Value.Id)!.Parts).Sum(g => Measure.Volume(g.Solid)) * SteelModule.SteelDensity;
        var w = sum["weights"]!;
        Assert.Equal(Kg("purlin"), (double)w["purlin"]!, 0.1);
        Assert.Equal(Kg("rail"), (double)w["rail"]!, 0.1);
        Assert.Equal(Kg("brace"), (double)w["brace"]!, 0.1);
        // A Z 200×65×2 is about 5.7 kg/m: 80 purlins of 6 m and the overhangs.
        Assert.InRange((double)w["purlin"]!, 80 * 6 * 5.5, 80 * 6.2 * 6.2);
        // The typical frame's elevation shows its purlins' and rails' sections.
        Assert.Contains(p["frame"]!.AsArray(), i => (string)i!["role"]! == "purlin");
        Assert.Contains(p["frame"]!.AsArray(), i => (string)i!["role"]! == "rail");
        Assert.Contains(p["plan"]!.AsArray(), i => (string)i!["role"]! == "brace");
    }

    [Fact]
    public void The_secondary_steel_exports_as_members_of_the_halls_system_with_its_cleats_and_gussets()
    {
        var f = F();
        var hall = Hall(f, new JsonObject { ["bays"] = J(6), ["gable"] = "frame", ["eavesHaunch"] = null, ["apexHaunch"] = null, ["bracing"] = J(1), ["brace"] = "L 60×60×6", ["eavesBeam"] = true });
        var m = Members(f, hall);
        var nodes = Nodes(f, hall);
        var path = Path.Combine(Path.GetTempPath(), $"secondary-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var members = step.OfType("IFCMEMBER").ToList();
            Assert.Equal(Count(m, "pur:"), members.Count(x => x[8] is StepEnum { Value: "PURLIN" }));
            Assert.Equal(Count(m, "rbr:") + Count(m, "wbr:") + Count(m, "fly:") + Count(m, "cfly:"), members.Count(x => x[8] is StepEnum { Value: "BRACE" }));
            Assert.Equal(Count(m, "rail:") + Count(m, "grail:"), members.Count(x => x[4] is string s && s == "Side rail"));
            Assert.Equal(2, members.Count(x => x[4] is string s && s == "Eaves beam"));
            // Each connection an assembly; the cleats' and gussets' plates and bolts.
            Assert.Equal(nodes.Count, step.OfType("IFCELEMENTASSEMBLY").Count());
            var bolts = nodes.Values.Sum(n => Of(f, n, "Bolt"));
            var anchors = nodes.Values.Sum(n => Of(f, n, "Anchor"));
            Assert.Equal(bolts + anchors, step.OfType("IFCMECHANICALFASTENER").Count());
            // The hall's system holds them all.
            var system = Assert.Single(step.OfType("IFCBUILDINGSYSTEM"));
            var assigned = step.OfType("IFCRELASSIGNSTOGROUP").Where(r => r[6] is StepRef sr && sr.Id == system.Id).SelectMany(r => (List<object?>)r[4]!).Count();
            Assert.Equal(m.Count + nodes.Count, assigned);

            // Read back: a purlin's volume, a cleat's.
            var model = new IfcModel(step);
            var booleans = f.Session.Catalog.Get<ISolidBooleans>()!;
            double Read(string tag) => model.Products.Where(p => p.Tag == tag || p.Tag?.StartsWith(tag + "/", StringComparison.Ordinal) == true).Sum(p => model.Shape(p.Id, booleans).Body.Sum(b => Math.Abs(MeshVolume(b.Mesh))));
            var purlin = m["pur:a:2:1"];
            Assert.Equal(f.Session.Geometry(purlin.Id)!.Parts.Sum(p => Measure.Volume(p.Solid)), Read(purlin.Id), 7);
            var cleat = nodes["cleat@pur:a:2@f1"];
            Assert.Equal(Parts(f, cleat).Sum(p => Measure.Volume(p.Solid)), Read(cleat.Id), 7);
        }
        finally { File.Delete(path); }
    }

    static double MeshVolume(MeshBuilder mesh) => mesh.T.Sum(t => Vec3.Dot(mesh.V[t.A], Vec3.Cross(mesh.V[t.B], mesh.V[t.C]))) / 6;
}
