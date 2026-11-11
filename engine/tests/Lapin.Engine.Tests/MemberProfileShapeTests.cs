using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.ProfileLibraries;

namespace Lapin.Engine.Tests;

/// <summary>
/// A library profile's real outline in curtain wall mullions and transoms and door and window frames and sashes (decision 417): holes
/// included, oriented along each member, butted at corners as plain members are; without a profile nothing changes.
/// </summary>
public class MemberProfileShapeTests
{
    /// <summary>An aluminium section 50 wide, 100 deep, not symmetric: a chamber (a hole), a nose on its +y at its +x side.</summary>
    static SectionProfile Alu(string name = "ALU 50x100") => new()
    {
        Name = name, Kind = "outline", Material = "aluminium", B = 0.05, H = 0.1,
        Rings = [
            new Vec2[] { new(-0.025, -0.05), new(0.025, -0.05), new(0.025, 0.05), new(0.005, 0.05), new(0.005, 0.03), new(-0.025, 0.03) },
            new Vec2[] { new(-0.015, -0.04), new(-0.015, 0.01), new(0.015, 0.01), new(0.015, -0.04) },
        ],
    };

    static double AreaOf(SectionProfile p) => p.Rings!.Sum(r => Polygon2.SignedArea(r));

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static JsonObject Used(Fixture f, SectionProfile p) => (JsonObject)f.Run("profiles.use", new JsonObject { ["profile"] = p.ToJson(), ["library"] = "Alu" })!["section"]!.DeepClone();

    static string Curtain(Fixture f, JsonObject? mullion, JsonObject? transom)
    {
        var d = CurtainSpec.Default;
        var spec = d with { Mullion = mullion != null ? new CurtainSection(0, 0, mullion) : d.Mullion, Transom = transom != null ? new CurtainSection(0, 0, transom) : d.Transom };
        var json = spec.ToJson();
        var type = (string)f.Run("architecture.createCurtainWallType", new JsonObject { ["name"] = "CW", ["spec"] = json })!["id"]!;
        return (string)f.Run("architecture.addCurtainWall", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["height"] = 3 })!["id"]!;
    }

    static GeometryPart Part(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.Single(p => p.Key == key);

    [Fact]
    public void Mullions_and_transoms_take_the_real_outline_with_its_holes_and_keep_their_place()
    {
        var f = F();
        var section = Used(f, Alu());
        var id = Curtain(f, section, section);
        var layout = CurtainWalls.Layout(CurtainWalls.SpecOf(f.Model, f.Model.Get(id)!), 6, 3);
        Assert.Equal(0.05, layout.Pieces.First(p => p.Role == "mullion").A0 + layout.Pieces.First(p => p.Role == "mullion").A1, 9);
        // An interior mullion on its grid line: its area × its height, its chamber open up it.
        var mullion = Part(f, id, "mullion:1").Solid;
        Assert.Equal(AreaOf(Alu()) * 3, Measure.Volume(mullion), 6);
        var (a, b) = Measure.Bounds(mullion)!.Value;
        Assert.Equal(0.05, b.X - a.X, 6);
        Assert.Equal(0.1, b.Y - a.Y, 6);
        Assert.Equal(1.5, (a.X + b.X) / 2, 6);
        Assert.Equal(0, (a.Y + b.Y) / 2, 6);
        // Its y out of the building, the wall's right: along +x that is −y; the notch (a corner of the outline's far side) stands at y = −0.03 there.
        Assert.Contains(mullion.Vertices, v => Math.Abs(v.X - (1.5 + 0.005)) < 1e-6 && Math.Abs(v.Y - -0.03) < 1e-6 && Math.Abs(v.Z) < 1e-6);
        Assert.DoesNotContain(mullion.Vertices, v => Math.Abs(v.X - (1.5 - 0.025)) < 1e-6 && Math.Abs(v.Y - -0.05) < 1e-6);
        // A transom between two mullions, its x up: its section across the bay, butted at their faces.
        var transom = f.Session.Geometry(id)!.Parts.First(p => p.Key!.StartsWith("transom:0:1", StringComparison.Ordinal)).Solid;
        var run = 1.5 - 0.05 - 0.025; // the edge mullion's whole width, the next's half
        Assert.Equal(AreaOf(Alu()) * run, Measure.Volume(transom), 6);
        var (t0, t1) = Measure.Bounds(transom)!.Value;
        Assert.Equal(0.05, t1.Z - t0.Z, 6);
        Assert.Equal(0.1, t1.Y - t0.Y, 6);
        Assert.Equal(0.05, t0.X, 6);
    }

    [Fact]
    public void Plain_members_are_what_they_were_and_a_plan_cuts_the_real_mullion()
    {
        var f = F();
        var plain = Curtain(f, null, null);
        Assert.Equal(8, Part(f, plain, "mullion:1").Solid.Vertices.Length);
        var g = F();
        var section = Used(g, Alu());
        var id = Curtain(g, section, null);
        var view = (string)g.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = g.Storey, ["scale"] = 50 })!["id"]!;
        var d = Views.Generate(new Q(g), Views.Find(g.Model, view)!).Drawing!;
        var cut = d.Cuts.Where(c => c.Element == id && c.Key == "mullion:1").ToList();
        Assert.Contains(cut, c => c.Loops.Count == 2 && c.Loops[0].Length == 6 && c.Loops[1].Length == 4);
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    [Fact]
    public void A_member_follows_its_profile_when_it_is_updated_from_the_library()
    {
        var f = F();
        var section = Used(f, Alu());
        var id = Curtain(f, section, null);
        var copy = f.Model.OfClass(ProjectProfiles.Class).Single();
        var wider = Alu() with
        {
            B = 0.06, Rings = [new Vec2[] { new(-0.03, -0.05), new(0.03, -0.05), new(0.03, 0.05), new(-0.03, 0.05) }],
        };
        f.Run("profiles.update", new JsonObject { ["id"] = copy.Id, ["profile"] = wider.ToJson() });
        var (a, b) = Measure.Bounds(Part(f, id, "mullion:1").Solid)!.Value;
        Assert.Equal(0.06, b.X - a.X, 6);
    }

    static JoinerySpec WindowWith(JsonObject? frame, JsonObject? sash, JoineryProfile? system = null) =>
        JoinerySpec.From((JoinerySpec.DefaultWindow("Alu") with { FrameSection = frame, SashSection = sash, Profile = system }).ToJson())!;

    [Fact]
    public void A_window_frame_takes_the_sizes_and_outline_of_its_profile_mirrored_jamb_to_jamb()
    {
        var f = F();
        var section = Used(f, Alu());
        var s = WindowWith(section, null);
        Assert.Equal((0.05, 0.1), (s.Frame.Width, s.Frame.Depth));
        var boxes = Joinery.Boxes(s);
        var jambs = boxes.Where(b => b.Member is { Upright: true }).ToList();
        Assert.Equal(2, jambs.Count);
        Assert.Equal(2, boxes.Count(b => b.Member is { Upright: false }));
        Assert.Equal(-s.Width / 2, jambs[0].Min.X, 9);
        Assert.Equal(s.Width / 2, jambs[1].Max.X, 9);
        // In a wall: frame part area, each jamb's outline up its height, each rail's across its clear run, with chambers.
        var wall = f.Wall(0, 0, 6, 0);
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = s.ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["typeId"] = type, ["sill"] = 0.9 })!["id"]!;
        var frame = Part(f, window, "frame").Solid;
        var inner = s.Width - 2 * 0.05;
        // The grid has one mullion down the middle of a plain box, as without a profile.
        var plainMullion = boxes.Where(b => b.Role == "frame" && b.Member == null).Sum(b => (b.Max.X - b.Min.X) * (b.Max.Y - b.Min.Y) * (b.Max.Z - b.Min.Z));
        var area = AreaOf(Alu());
        Assert.Equal(area * 2 * s.Height + area * 2 * inner + plainMullion, Measure.Volume(frame), 6);
        // The notch, towards the glass on both jambs: at x = ∓(w/2 − 0.05 + … ) mirrored about the window's axis.
        var left = jambs[0].Member!.On(jambs[0], new Vec2(0.005, 0.03));
        var right = jambs[1].Member!.On(jambs[1], new Vec2(0.005, 0.03));
        Assert.Equal(-left.Across, right.Across, 9);
        Assert.Equal(left.Y, right.Y, 9);
    }

    [Fact]
    public void Plans_and_sections_draw_the_real_outline()
    {
        var f = F();
        var s = WindowWith(Used(f, Alu()), null);
        var plan = JoinerySymbols.Plan(s);
        Assert.Contains(plan, x => x.Weight == "cut" && x.Closed && x.Pts.Count == 6);
        var section = JoinerySymbols.Section(s);
        Assert.Contains(section, x => x.Weight == "cut" && x.Closed && x.Pts.Count == 6);
        // At once as deep and wide as the profile says.
        var loop = section.First(x => x.Weight == "cut" && x.Pts.Count == 6);
        Assert.Equal(0.1, loop.Pts.Max(p => p.X) - loop.Pts.Min(p => p.X), 9);
    }

    [Fact]
    public void A_sash_too_and_the_profile_system_gives_way_only_where_the_profile_is_the_real_shape()
    {
        var f = F();
        var section = Used(f, Alu());
        var system = new JoineryProfile("aluminium", 0.012, 0.006, 0.015, true);
        // Without a profile: the system's stop, lip, beads and drips.
        var plain = Joinery.Boxes(WindowWith(null, null, system));
        // With both: their real outlines instead of the stop, lip and drips; the beads stay.
        var real = WindowWith(section, section, system);
        var boxes = Joinery.Boxes(real);
        Assert.Equal((0.05, 0.1), (real.Sash.Width, real.Sash.Depth));
        Assert.Equal(8, boxes.Count(b => b.Role == "sash" && b.Member != null));
        Assert.DoesNotContain(boxes, b => b.Role == "frame" && b.Profile == null && b.Member == null && Math.Abs(b.Max.Y - b.Min.Y - 0.015) < 1e-9);
        Assert.Contains(plain, b => b.Role == "frame" && b.Member == null && Math.Abs(b.Max.Y - b.Min.Y - 0.015) < 1e-9);
        // Beads: bands a little over the glass, a ring on each panel.
        Assert.Equal(8, boxes.Count(b => b.Role == "sash" && b.Member == null && Math.Abs(b.Max.Y - b.Min.Y - 0.018) < 1e-9));
        // Only the frame: the sash keeps the system's lip.
        var frameOnly = Joinery.Boxes(WindowWith(section, null, system));
        Assert.Contains(frameOnly, b => b.Role == "sash" && Math.Abs(b.Max.Y - b.Min.Y - 0.006) < 1e-9);
    }

    [Fact]
    public void Without_a_profile_the_body_is_what_it_was_and_the_spec_keeps_its_profile_through_JSON()
    {
        var spec = JoinerySpec.DefaultWindow("Plain");
        Assert.All(Joinery.Boxes(spec), b => Assert.Null(b.Member));
        Assert.Null(JoinerySpec.From(spec.ToJson())!.FrameSection);
        var f = F();
        var s = WindowWith(Used(f, Alu()), null);
        var again = JoinerySpec.From(s.ToJson())!;
        Assert.Equal("ALU 50x100", (string?)again.FrameSection!["name"]);
    }

    [Fact]
    public void An_arched_head_keeps_its_band_and_a_round_window_is_untouched()
    {
        var f = F();
        var section = Used(f, Alu());
        var arch = WindowWith(section, null) with { Shape = new JoineryShape("round") };
        var boxes = Joinery.Boxes(arch);
        Assert.Equal(2, boxes.Count(b => b.Member is { Upright: true }));
        Assert.Contains(boxes, b => b.Role == "frame" && b.Profile != null);
        var round = WindowWith(section, section) with { Shape = new JoineryShape("circle"), Columns = [1], Panels = [new(0, 0, "fixed")] };
        Assert.All(Joinery.Boxes(round), b => Assert.Null(b.Member));
    }
}
