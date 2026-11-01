using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The roof's timber framing (decision 229, docs/plan-roof-framing.md): couples with collar ties and trusses along the main
/// ridge, Nordic sections in mm, bird's mouths over the plates, every member a part of its own, the cutting list and IFC.
/// </summary>
public class RoofFramingTests
{
    static JsonArray House10x6 => new(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6));

    static string AddRoof(Fixture f, string style, JsonObject? attrs = null)
    {
        var id = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = style })!["id"]!;
        if (attrs != null) f.Run("architecture.updateRoof", new JsonObject { ["id"] = id, ["attrs"] = attrs });
        return id;
    }

    static (Roof.Resolved R, List<RoofParts.Member> Members) Framing(Fixture f, string roof)
    {
        var r = Roof.Resolve(f.Model.Get(roof)!, f.Model)!;
        return (r, RoofParts.Framing(r, Roof.Solve(r).Shape!));
    }

    static double Qty(Fixture f, string id, string name) =>
        f.Session.Quantities(id).SelectMany(s => s.Quantities).Where(q => q.Name == name).Sum(q => q.Value);

    [Fact]
    public void A_swedish_truss_roof_puts_a_truss_at_every_station_over_the_walls()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "se_truss");                               // gable 27°, trusses at 1.2 m, chords 45×170, webs 45×95
        var e = f.Model.Get(roof)!;
        Assert.Equal("trusses", e.Str("structure"));
        var (r, members) = Framing(f, roof);
        // Ten stations over the 10.6 m between the rakes; the two over the gable overhangs stand past the walls and keep rafters.
        var trusses = members.Where(m => m.Truss >= 0).GroupBy(m => m.Truss).ToList();
        Assert.Equal(8, trusses.Count);
        Assert.Equal(4, members.Count(m => m.Role == "rafter"));
        Assert.All(trusses, t =>
        {
            Assert.Equal(2, t.Count(m => m.Role == "topChord"));
            var bottom = Assert.Single(t, m => m.Role == "bottomChord");
            Assert.Equal(6 + Roof.WallPlate.W, bottom.Length, 9);
            Assert.Equal(r.BaseZ + 0.17 / 2, bottom.A.Z, 9);
            // Fink: four webs, 45×95.
            Assert.Equal(4, t.Count(m => m.Role == "web"));
            Assert.All(t.Where(m => m.Role == "web"), w => Assert.Equal((0.045, 0.095), (w.Width, w.Depth)));
        });
        // No purlins and no ridge beam under trusses; the chords in mm.
        Assert.DoesNotContain(members, m => m.Role is "purlin" or "ridge");
        Assert.All(members.Where(m => m.Role == "topChord"), m => Assert.Equal((0.045, 0.17), (m.Width, m.Depth)));
        Assert.Equal(8, Qty(f, roof, "TrussCount"));
        // Every member is a part of its own, a truss's under its number.
        var parts = f.Session.Geometry(roof)!.Parts;
        Assert.Equal(members.Count, parts.Count(p => p.Key!.StartsWith("timber:") || p.Key.StartsWith("truss:")));
        Assert.Contains(parts, p => p.Key == "truss:1:web:4" && p.Name == "Web 45x95 mm");
        Assert.Contains(parts, p => p.Properties?.Contains(("Grade", "C24")) == true);
    }

    [Fact]
    public void A_collar_tie_roof_pairs_its_rafters_and_ties_them_at_two_thirds_of_the_rise()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "nordic_collar");                          // gable 38°, 45×195 at 0.6 m
        var (r, members) = Framing(f, roof);
        var collars = members.Where(m => m.Role == "collar").ToList();
        // Nineteen stations, seventeen of them over the walls: seventeen couples tied, every station a pair of rafters.
        Assert.Equal(17, collars.Count);
        Assert.Equal(38, members.Count(m => m.Role == "rafter"));
        Assert.DoesNotContain(members, m => m.Role is "purlin" or "ridge" or "topChord");
        var rise = 3 * Math.Tan(38 * Math.PI / 180);
        Assert.All(collars, c =>
        {
            Assert.Equal(r.BaseZ + 0.66 * rise + 0.195 / 2, c.A.Z, 6);
            Assert.Equal(c.A.Z, c.B.Z, 9);
            Assert.InRange(c.Length, 1.0, 6.0);
        });
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["collarHeight"] = 0.5 } });
        Assert.All(Framing(f, roof).Members.Where(m => m.Role == "collar"), c => Assert.Equal(r.BaseZ + 0.5 * rise + 0.195 / 2, c.A.Z, 6));
    }

    [Fact]
    public void A_rafter_sits_on_the_plate_by_a_bird_s_mouth_and_its_tail_may_be_cut_square()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "se_falu_red", new JsonObject { ["structure"] = "rafters", ["rafterSection"] = "45x195 mm", ["rafterSpacing"] = 0.6 });
        var (r, members) = Framing(f, roof);
        var rafters = members.Where(m => m.Role == "rafter").ToList();
        Assert.NotEmpty(rafters);
        // Notched: seven corners on its face, and nothing of it under the plate's top over the plate's outer half.
        Assert.All(rafters, m => Assert.Equal(7, m.Profile!.Count));
        bool OverPlate(Vec3 v) => v.Y is > -0.07 and < 0 || v.Y is > 6 and < 6.07;
        var bodies = rafters.Select(m => m.Body("rafter")!).ToList();
        Assert.All(bodies.SelectMany(b => b.Vertices).Where(OverPlate), v => Assert.True(v.Z >= r.BaseZ - 1e-9, $"{v}"));
        // The heel is at most a third of the rafter's depth.
        var g = Math.Tan(38 * Math.PI / 180);
        Assert.All(rafters, m => Assert.True(m.Profile!.Select(p => p.Z).Distinct().Count() >= 5));
        Assert.True(Roof.WallPlate.W / 2 * g <= 0.195 / Math.Cos(Math.Atan(g)) / 3 + 1e-9);
        // Without the seat it is a plain board through the plate.
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["rafterSeat"] = "none", ["rafterTail"] = "square" } });
        var plain = Framing(f, roof).Members.Where(m => m.Role == "rafter").ToList();
        Assert.All(plain, m => Assert.Equal(4, m.Profile!.Count));
        // A square tail: its foot comes in from its top, by the depth times the sine of the pitch.
        var m0 = plain[0];
        var foot = m0.Profile![0];
        var top = m0.Profile[^1];
        Assert.Equal(0.195 * Math.Sin(Math.Atan(g)), (foot - top).Xy.Length, 6);
    }

    [Fact]
    public void A_hip_roof_s_trusses_stand_between_its_hips_and_a_shed_s_are_mono_trusses()
    {
        var f = new Fixture();
        var hip = AddRoof(f, "traditional_campie", new JsonObject { ["structure"] = "trusses", ["rafterSection"] = "45x170 mm", ["rafterSpacing"] = 1.2 });
        var (_, members) = Framing(f, hip);
        Assert.InRange(members.Where(m => m.Truss >= 0).Select(m => m.Truss).Distinct().Count(), 3, 4);
        // The hip ends keep their rafters, and the hip rafters stay.
        Assert.Contains(members, m => m.Role == "rafter");
        Assert.Contains(members, m => m.Role == "hip");

        var g = new Fixture();
        var shed = AddRoof(g, "modern_monopitch", new JsonObject { ["structure"] = "trusses", ["rafterSection"] = "45x170 mm", ["rafterSpacing"] = 1.2, ["gableOverhang"] = 0 });
        var trusses = Framing(g, shed).Members.Where(m => m.Truss >= 0).GroupBy(m => m.Truss).ToList();
        Assert.NotEmpty(trusses);
        Assert.All(trusses, t =>
        {
            Assert.Single(t, m => m.Role == "topChord");
            Assert.Single(t, m => m.Role == "bottomChord");
            // A post at the high end, one at the middle, two diagonals.
            Assert.Equal(4, t.Count(m => m.Role == "web"));
            Assert.Equal(2, t.Count(m => m.Role == "web" && Math.Abs(m.A.X - m.B.X) < 1e-9 && Math.Abs(m.A.Y - m.B.Y) < 1e-9));
        });
    }

    [Fact]
    public void The_roof_timber_schedule_is_a_cutting_list()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "se_truss");
        var (_, members) = Framing(f, roof);
        var rows = RoofTimberSchedule.Rows(f.Model);
        Assert.Equal(members.Count, rows.Sum(x => x.Count));
        var chords = Assert.Single(rows, x => x.Role == "Bottom chord");
        Assert.Equal(("45x170 mm", "C24", 8, 6.14), (chords.Section, chords.Grade, chords.Count, chords.Length));
        Assert.Equal(members.Sum(m => m.Volume), rows.Sum(x => x.Volume), 9);
        var grid = new RoofTimberSchedule().Grid(new Q(f));
        Assert.Equal(["Roof", "Member", "Section", "Grade", "Length (m)", "Pieces", "Total length (m)", "Volume (m³)"], grid.Columns);
        Assert.Contains(grid.Rows, x => x[0] == "Total" && x[2] == "45x95 mm" && x[5] == "32");
    }

    [Fact]
    public void Trusses_go_to_IFC_as_element_assemblies_of_typed_members()
    {
        var f = new Fixture();
        AddRoof(f, "se_truss");
        var path = Path.Combine(Path.GetTempPath(), $"trusses-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var file = StepFile.Read(path);
            Assert.Equal(8, file.OfType("IFCELEMENTASSEMBLY").Count(a => a[9] is StepEnum { Value: "TRUSS" }));
            Assert.Equal(24, file.OfType("IFCMEMBER").Count(m => m[8] is StepEnum { Value: "CHORD" }));
            Assert.Equal(4, file.OfType("IFCMEMBER").Count(m => m[8] is StepEnum { Value: "RAFTER" }));
            Assert.Contains(file.OfType("IFCMEMBER"), m => m[8] is StepEnum { Value: "PLATE" });
            Assert.Contains(file.OfType("IFCPROPERTYSET"), p => (string?)p[2] == "Lapin_Timber");
        }
        finally { File.Delete(path); }
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
