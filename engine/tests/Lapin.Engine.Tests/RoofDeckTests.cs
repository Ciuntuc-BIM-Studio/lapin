using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Architecture.Schedules;
using Lapin.Cost;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A roof without framing (flat, a vault, <c>structure: none</c>) is a deck under its covering (decision 329, the open item
/// of 328): two parts, the deck of its own material (reinforced concrete, 16 cm, unless the roof says) and the covering of
/// the covering's, together as thick as the one body was, so the roof's outside is the same. A section draws two bands, each
/// hatched by its material; a roof window cuts both; a wall attached under a flat roof rises to the deck's underside; the
/// takeoff and the estimate measure each once; IFC writes two IfcSlab ROOF of their materials and reads them back; old
/// roofs are given their deck's material.
/// </summary>
public class RoofDeckTests
{
    static JsonArray House10x6 => new(new JsonArray(0, 0), new JsonArray(10, 0), new JsonArray(10, 6), new JsonArray(0, 6));

    static string AddRoof(Fixture f, string style) =>
        (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = House10x6, ["style"] = style })!["id"]!;

    static void Set(Fixture f, string roof, JsonObject attrs) => f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = attrs });

    /// <summary>An imported roof reference's <c>shape</c> is the file's description of its body, an object: a project holding one opens.</summary>
    [Fact]
    public void A_roof_reference_whose_shape_is_the_files_description_is_read_without_failing()
    {
        var attrs = new JsonObject { ["shape"] = new JsonObject { ["kind"] = "mesh" }, ["structure"] = new JsonArray() };
        Assert.False(RoofCoverings.Unframed(attrs));
        var roof = new Entity("roof-reference", IfcClass.Roof, "Imported roof", attrs);
        var (entities, _) = new RoofDeckUpgrade().Upgrade([roof], []);
        Assert.Contains(entities, e => e.Id == "roof-reference");
        Assert.True(RoofCoverings.Unframed(new JsonObject { ["shape"] = "flat" }));
        Assert.True(RoofCoverings.Unframed(new JsonObject { ["structure"] = "none" }));
    }

    static Entity? Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).FirstOrDefault(e => e.Name == name);

    static GeometryPart Part(Fixture f, string roof, string key) => f.Session.Geometry(roof)!.Parts.Single(p => p.Key == key);

    static (Vec3 Min, Vec3 Max) Box(Solid s) => Measure.Bounds(s)!.Value;

    static Views.Rendered Render(Fixture f, string view) => Views.Render(f.Session.Reader, Views.Find(f.Model, view)!);

    static string Section(Fixture f, double x) =>
        (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(x, -2), ["end"] = new JsonArray(x, 8) })!["id"]!;

    static List<(Vec2 A, Vec2 B)> Segs(Views.Rendered r, string layer) => r.Pens.Where(p => p.Layer == layer).SelectMany(p => p.Segments).ToList();

    /// <summary>The roof's outside: its parts together are the one body it was (the cut envelope), in bounds and in volume.</summary>
    static void SameOutside(Fixture f, string roof)
    {
        var g = f.Session.Geometry(roof)!;
        var body = g.Parts.Where(p => p.Key is "deck" or "covering").ToList();
        Assert.Equal(2, body.Count);
        var env = Roof.Envelope(f.Model.Get(roof)!, f.Model)!;
        var (lo, hi) = Box(env);
        var boxes = body.Select(p => Box(p.Solid)).ToList();
        Assert.Equal(lo.X, boxes.Min(b => b.Min.X), 6); Assert.Equal(hi.X, boxes.Max(b => b.Max.X), 6);
        Assert.Equal(lo.Y, boxes.Min(b => b.Min.Y), 6); Assert.Equal(hi.Y, boxes.Max(b => b.Max.Y), 6);
        Assert.Equal(lo.Z, boxes.Min(b => b.Min.Z), 6); Assert.Equal(hi.Z, boxes.Max(b => b.Max.Z), 6);
        Assert.Equal(Measure.Volume(env), body.Sum(p => Measure.Volume(p.Solid)), 4);
    }

    [Fact]
    public void A_flat_roof_is_a_deck_of_its_own_material_under_its_covering_as_thick_as_before()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_minimalist");
        var e = f.Model.Get(roof)!;
        Assert.Equal(RoofCoverings.DeckDefault, e.Str(RoofCoverings.DeckKey));
        var concrete = Material(f, "Reinforced concrete")!;
        var (deck, covering) = (Part(f, roof, "deck"), Part(f, roof, "covering"));
        Assert.Equal(concrete.Id, deck.Material);
        Assert.Equal(Material(f, "Bitumen membrane")!.Id, covering.Material);
        // The deck 16 cm on the plates, the covering's 2 cm on it: 18 cm in all, as the one body was.
        var (d, c) = (Box(deck.Solid), Box(covering.Solid));
        Assert.Equal(3.0, d.Min.Z, 6); Assert.Equal(3.16, d.Max.Z, 6);
        Assert.Equal(3.16, c.Min.Z, 6); Assert.Equal(3.18, c.Max.Z, 6);
        SameOutside(f, roof);
        // A deck of the roof's own thickness and material: the covering on it.
        Set(f, roof, new JsonObject { ["deckThickness"] = 0.25, [RoofCoverings.DeckKey] = "CLT" });
        Assert.Equal(3.25, Box(Part(f, roof, "deck").Solid).Max.Z, 6);
        Assert.Equal(3.27, Box(Part(f, roof, "covering").Solid).Max.Z, 6);
        Assert.Equal(Material(f, "CLT")!.Id, Part(f, roof, "deck").Material);
        // The build-up lists both, each in its material.
        var layers = new RoofBuildUps().Of(f.Model, f.Model.Get(roof)!)!.Layers;
        Assert.Equal(new List<string?> { Material(f, "Bitumen membrane")!.Id, Material(f, "CLT")!.Id }, layers.Select(l => l.Material).ToList());
        Assert.Equal(0.25, layers[1].Thickness, 9);
        // A deck of no material: the part has none.
        Set(f, roof, new JsonObject { [RoofCoverings.DeckKey] = "" });
        Assert.Null(Part(f, roof, "deck").Material);
        // A framed roof has no deck.
        var framed = AddRoof(f, "fr_provence");
        Assert.DoesNotContain(f.Session.Geometry(framed)!.Parts, p => p.Key == "deck");
    }

    [Fact]
    public void A_flat_roof_s_section_draws_two_bands_each_hatched_by_its_material()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_minimalist");
        // A covering with a section hatch of its own, so its band shows.
        Set(f, roof, new JsonObject { [RoofCoverings.Key] = "Clay tile", ["covering"] = "Green roof" });
        var tile = Material(f, "Clay tile")!;
        Assert.Equal("TILES", MaterialGraphics.Resolve(tile, "section").Hatch);
        Assert.NotNull(MaterialGraphics.Resolve(Material(f, "Reinforced concrete"), "section").Hatch);
        var r = Render(f, Section(f, 5));
        // The cut draws the line between them: across the roof at the deck's top.
        var cut = Segs(r, PenLayer.Cut);
        Assert.Contains(cut, s => Math.Abs(s.A.Y - 3.16) < 1e-6 && Math.Abs(s.B.Y - 3.16) < 1e-6 && Math.Abs(s.B.X - s.A.X) > 5);
        // Hatches in both bands: the deck's from 3.00 to 3.16, the covering's from 3.16 to 3.36 (a green roof's 20 cm).
        var hatch = Segs(r, PenLayer.Hatch);
        bool In(double lo, double hi) => hatch.Any(s => (s.A.Y + s.B.Y) / 2 > lo + 1e-4 && (s.A.Y + s.B.Y) / 2 < hi - 1e-4);
        Assert.True(In(3.0, 3.16), "the deck's band is hatched");
        Assert.True(In(3.16, 3.36), "the covering's band is hatched");
        // Each band by its own material's pattern: the deck's concrete has no tile courses, the covering's are level lines.
        var deckHatch = hatch.Where(s => s.A.Y < 3.16 - 1e-4 && s.B.Y < 3.16 - 1e-4).ToList();
        var coverHatch = hatch.Where(s => s.A.Y > 3.16 + 1e-4 && s.B.Y > 3.16 + 1e-4).ToList();
        Assert.NotEmpty(deckHatch);
        Assert.NotEmpty(coverHatch);
        Assert.Contains(coverHatch, s => Math.Abs(s.A.Y - s.B.Y) < 1e-6 && Math.Abs(s.B.X - s.A.X) > 0.05);
        Assert.Contains(deckHatch, s => Math.Abs(s.A.Y - s.B.Y) > 1e-3);
    }

    [Fact]
    public void A_barrel_roof_is_a_deck_vault_under_its_covering()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_barrel");
        var r = Roof.Resolve(f.Model.Get(roof)!, f.Model)!;
        var arc = Roof.Solve(r).Shape!.Arc!;
        var (deck, covering) = (Part(f, roof, "deck"), Part(f, roof, "covering"));
        Assert.Equal(Material(f, "Reinforced concrete")!.Id, deck.Material);
        Assert.Equal(Material(f, "Metal sheet")!.Id, covering.Material);
        // Every corner of the deck between the vault's underside and 16 cm out; of the covering between there and 3 cm more.
        double Radius(Vec3 p) { var s = arc.S(p.Xy) - arc.SMid; return Math.Sqrt(s * s + (p.Z - arc.Zc) * (p.Z - arc.Zc)); }
        Assert.All(deck.Solid.Vertices, v => Assert.InRange(Radius(v), arc.R - 1e-6, arc.R + 0.16 + 1e-6));
        Assert.All(covering.Solid.Vertices, v => Assert.InRange(Radius(v), arc.R + 0.16 - 1e-6, arc.R + 0.19 + 1e-6));
        SameOutside(f, roof);
        // A section across the vault: hatches in both shells (the metal sheet's by its name, the concrete's).
        f.Run("material.update", new JsonObject { ["id"] = Material(f, "Metal sheet")!.Id, ["section"] = new JsonObject { ["hatch"] = "TILES" } });
        var rendered = Render(f, Section(f, 5));
        var hatch = Segs(rendered, PenLayer.Hatch);
        Assert.NotEmpty(hatch);
        // The view runs along the section from its start (5, −2): x is the world's y + 2, y its z. Hatches in the deck's
        // shell (R to R + 16 cm from the vault's centre) and in the covering's (to R + 19 cm).
        double ViewRadius(Vec2 p) => Math.Sqrt(Math.Pow(p.X - 2 - arc.SMid, 2) + Math.Pow(p.Y - arc.Zc, 2));
        var radii = hatch.Select(h => ViewRadius((h.A + h.B) * 0.5)).ToList();
        Assert.Contains(radii, x => x > arc.R + 0.01 && x < arc.R + 0.15);
        Assert.Contains(radii, x => x > arc.R + 0.165 && x < arc.R + 0.185);
    }

    [Fact]
    public void A_roof_window_in_a_roof_without_framing_cuts_its_deck_and_its_covering()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "fr_provence");
        Set(f, roof, new JsonObject { ["structure"] = "none" });
        Assert.Equal(RoofCoverings.DeckDefault, f.Model.Get(roof)!.Str(RoofCoverings.DeckKey));
        Assert.NotNull(Material(f, "Reinforced concrete"));
        SameOutside(f, roof);
        var before = (Measure.Volume(Part(f, roof, "deck").Solid), Measure.Volume(Part(f, roof, "covering").Solid));
        f.Run("architecture.addRoofOpening", new JsonObject { ["roofId"] = roof, ["x"] = 5, ["y"] = 1.5, ["width"] = 0.78, ["length"] = 1.18 });
        var after = (Measure.Volume(Part(f, roof, "deck").Solid), Measure.Volume(Part(f, roof, "covering").Solid));
        // The hole 0.78 m wide and 1.18 m up the slope, through both, each by its thickness.
        Assert.Equal(0.78 * 1.18 * 0.16, before.Item1 - after.Item1, 3);
        Assert.Equal(0.78 * 1.18 * 0.05, before.Item2 - after.Item2, 3);
        SameOutside(f, roof);
        // Its quantities: the deck's net area less the hole, as the covering's.
        var sets = f.Session.Quantities(roof);
        var deck = sets.Single(s => s.Part == "deck");
        var cover = sets.Single(s => s.Part == "covering");
        double Q(QuantitySet s, string n) => s.Quantities.Single(q => q.Name == n).Value;
        Assert.Equal(Q(cover, "NetArea"), Q(deck, "NetArea"), 9);
        Assert.True(Q(deck, "GrossArea") - Q(deck, "NetArea") > 0.9);
    }

    [Fact]
    public void A_wall_attached_under_a_flat_roof_rises_to_the_deck_s_underside()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_minimalist");
        Set(f, roof, new JsonObject { ["eaveOffset"] = 0.5 });
        var wall = f.Wall(2, 3, 8, 3);
        Assert.Equal(3.0, f.Bounds(wall).Max.Z, 6);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(roof) });
        // To the deck's underside at 3.5 m, not into it, nor to the covering's.
        Assert.Equal(3.5, f.Bounds(wall).Max.Z, 3);
        Set(f, roof, new JsonObject { ["deckThickness"] = 0.3 });
        Assert.Equal(3.5, f.Bounds(wall).Max.Z, 3);
        // Left in auto it keeps its height.
        f.Run("architecture.detachFromRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["mode"] = "auto" });
        Assert.Equal(3.0, f.Bounds(wall).Max.Z, 6);
        // A sloped roof without framing: the attached wall still reads its planes, the deck's underside.
        var f2 = new Fixture();
        var r2 = AddRoof(f2, "fr_provence");
        var inner = f2.Wall(5, 0.5, 5, 5.5);
        f2.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(inner) });
        Set(f2, r2, new JsonObject { ["structure"] = "none" });
        var top = f2.Bounds(inner).Max.Z;
        var deckLow = Box(Part(f2, r2, "deck").Solid);
        Assert.InRange(top, 3.5, deckLow.Max.Z - 0.16 + 1e-3);
    }

    [Fact]
    public void The_takeoff_and_the_estimate_measure_the_deck_and_the_covering_each_once()
    {
        var f = new Fixture(new ArchitectureModule(), new CostModule());
        var roof = AddRoof(f, "modern_minimalist");
        var sets = f.Session.Quantities(roof);
        double Q(QuantitySet s, string n) => s.Quantities.Single(q => q.Name == n).Value;
        var deck = sets.Single(s => s.Part == "deck");
        var cover = sets.Single(s => s.Part == "covering");
        var concrete = Material(f, "Reinforced concrete")!;
        var membrane = Material(f, "Bitumen membrane")!;
        Assert.Equal((concrete.Id, membrane.Id), (deck.Material, cover.Material));
        Assert.Equal(60, Q(deck, "GrossArea"), 6);
        Assert.Equal(0.16, Q(deck, "Width"), 9);
        Assert.Equal(60 * 0.16, Q(deck, "NetVolume"), 6);
        Assert.Equal(60, Q(cover, "GrossArea"), 6);
        Assert.Equal(0.02, Q(cover, "Width"), 9);

        // The takeoff by material: a row for each.
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["groupBy"] = new JsonArray("material"), ["classes"] = new JsonArray(IfcClass.Roof) });
        var grid = new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
        Assert.Single(grid.Rows, r => r[0] == "Reinforced concrete");
        Assert.Single(grid.Rows, r => r[0] == "Bitumen membrane");
        Assert.DoesNotContain(grid.Rows, r => r[0] == "—");

        // Articles ruled by each material: the deck by its volume, the covering by its area, both in the Roof chapter, once.
        var csv = "Cod;Denumire;U.M.;Material;Manoperă\nBET01;Placă din beton armat;mc;480;140\nHID01;Hidroizolație bituminoasă;mp;35;15\n";
        f.Run("cost.importPrices", new JsonObject { ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)), ["name"] = "Prețuri.csv" });
        f.Run("cost.setRule", new JsonObject { ["material"] = concrete.Id, ["code"] = "BET01" });
        f.Run("cost.setRule", new JsonObject { ["material"] = membrane.Id, ["code"] = "HID01" });
        f.Run("cost.createEstimate", new JsonObject());
        var est = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var lines = est["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray().Select(l => ((string)c["name"]!, (JsonObject)l!))).ToList();
        var bet = Assert.Single(lines, x => (string)x.Item2["code"]! == "BET01");
        var hid = Assert.Single(lines, x => (string)x.Item2["code"]! == "HID01");
        Assert.Equal(("Roof", "Roof"), (bet.Item1, hid.Item1));
        Assert.Equal(9.6, (double)bet.Item2["qty"]!, 2);
        Assert.Equal(60, (double)hid.Item2["qty"]!, 2);
    }

    [Fact]
    public void IFC_writes_the_deck_and_the_covering_as_two_roof_slabs_and_reads_them_back()
    {
        var f = new Fixture();
        var roof = AddRoof(f, "modern_minimalist");
        var path = Path.Combine(Path.GetTempPath(), $"roof-deck-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var ifcRoof = step.OfType("IFCROOF").Single();
            var slabs = step.OfType("IFCSLAB").Where(s => s[8] is StepEnum { Value: "ROOF" }).ToList();
            var deck = slabs.Single(s => (string?)s[2] == "Deck");
            var cover = slabs.Single(s => (string?)s[2] == "Covering");
            // Both parts of the IfcRoof.
            Assert.Contains(step.OfType("IFCRELAGGREGATES"), rel => rel[4] is StepRef to && to.Id == ifcRoof.Id
                && rel[5] is List<object?> parts && parts.OfType<StepRef>().Any(x => x.Id == deck.Id) && parts.OfType<StepRef>().Any(x => x.Id == cover.Id));
            // Each of its material.
            bool Of(StepEntity slab, string name) => step.OfType("IFCRELASSOCIATESMATERIAL").Any(rel => rel[5] is StepRef to
                && step.OfType("IFCMATERIAL").Any(m => m.Id == to.Id && (string?)m[0] == name) && rel[4] is List<object?> list && list.OfType<StepRef>().Any(x => x.Id == slab.Id));
            Assert.True(Of(deck, "Reinforced concrete"));
            Assert.True(Of(cover, "Bitumen membrane"));

            var g = new Fixture();
            IfcImport.Run(g.Session, path, null, out _);
            var back = g.Model.OfClass(IfcClass.Roof).Single();
            Assert.Equal("Reinforced concrete", back.Str(RoofCoverings.DeckKey));
            Assert.Equal(Material(g, "Reinforced concrete")!.Id, Part(g, back.Id, "deck").Material);
            Assert.Equal(Material(g, "Bitumen membrane")!.Id, Part(g, back.Id, "covering").Material);
            Assert.Equal(Box(Part(f, roof, "deck").Solid).Max.Z, Box(Part(g, back.Id, "deck").Solid).Max.Z, 6);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void An_old_roof_without_framing_is_given_its_deck_s_material_and_keeps_its_outside()
    {
        var f = new Fixture();
        var flat = AddRoof(f, "modern_minimalist");
        var framed = AddRoof(f, "fr_provence");
        // As a project from before decision 329 kept it: no deck material on the roof, none in the project.
        var old = f.Model.Entities.Where(e => e.Class != IfcClass.Material || e.Name != RoofCoverings.DeckDefault)
            .Select(e => e.Id == flat ? e.With(attrs: Without(e.CloneAttrs(), RoofCoverings.DeckKey)) : e).ToList();
        // Unread, it is the same: the deck reinforced concrete by name, 16 cm.
        var view = new ModelSnapshot(old, f.Model.Relations);
        var g = Roof.Geometry(old.Single(e => e.Id == flat), view)!;
        Assert.Equal(3.16, Box(g.Parts.Single(p => p.Key == "deck").Solid).Max.Z, 6);
        Assert.Equal(RoofCoverings.DeckDefault, g.Parts.Single(p => p.Key == "deck").Material);
        var (up, _) = f.Session.Upgraded(old, f.Model.Relations.ToList());
        Assert.Equal(RoofCoverings.DeckDefault, up.Single(e => e.Id == flat).Str(RoofCoverings.DeckKey));
        Assert.Null(up.Single(e => e.Id == framed).Str(RoofCoverings.DeckKey));
        var concrete = Assert.Single(up, e => e.Class == IfcClass.Material && e.Name == RoofCoverings.DeckDefault);
        // The same id every time it opens; nothing more the second time.
        var (again, _) = f.Session.Upgraded(old, f.Model.Relations.ToList());
        Assert.Equal(concrete.Id, again.Single(e => e.Class == IfcClass.Material && e.Name == RoofCoverings.DeckDefault).Id);
        var (twice, _) = new RoofDeckUpgrade().Upgrade(up, []);
        Assert.Equal(up.Count, twice.Count);
        // Upgraded, the deck is made of the project's material, the outside the same.
        var upView = new ModelSnapshot(up, f.Model.Relations);
        var ug = Roof.Geometry(up.Single(e => e.Id == flat), upView)!;
        Assert.Equal(concrete.Id, ug.Parts.Single(p => p.Key == "deck").Material);
        Assert.Equal(3.18, ug.Parts.Max(p => Box(p.Solid).Max.Z), 6);
    }

    /// <summary>
    /// The properties panel shows a field only where it applies (decision 333): the deck's material and thickness on a roof
    /// without framing, the rafters' fields on a framed one, the mansard's, the vault's and the shed's on their shapes; the
    /// session tells the interface so (showIf).
    /// </summary>
    [Fact]
    public void The_deck_fields_show_only_without_framing_and_the_framings_only_with_it()
    {
        var f = new Fixture();
        var cls = f.Session.Reader.Describe(IfcClass.Roof)!;
        FieldDescriptor Field(string k) => cls.Fields.Single(x => x.Key == k);
        var flat = f.Model.Get(AddRoof(f, "modern_minimalist"))!;
        var gable = f.Model.Get(AddRoof(f, "traditional_campie"))!;
        Assert.True(Roof.Resolve(gable, f.Model)!.Framed);
        foreach (var k in new[] { RoofCoverings.DeckKey, "deckThickness" })
        {
            Assert.True(Field(k).Shown(flat));
            Assert.False(Field(k).Shown(gable));
        }
        foreach (var k in new[] { "rafterSection", "rafterSpacing", "timberGrade", "rafterTail", "rafterSeat", "purlins" })
        {
            Assert.False(Field(k).Shown(flat));
            Assert.True(Field(k).Shown(gable));
        }
        // A gable roof with structure none is a deck again; the structure itself is no choice on a flat roof.
        Set(f, gable.Id, new JsonObject { ["structure"] = "none" });
        var none = f.Model.Get(gable.Id)!;
        Assert.True(Field("deckThickness").Shown(none));
        Assert.False(Field("rafterSpacing").Shown(none));
        Assert.False(Field("structure").Shown(flat));
        Assert.True(Field("structure").Shown(none));
        // The trusses' webs only on trusses, the collar ties' height only on collar ties; the shapes' own fields on their shapes.
        Set(f, gable.Id, new JsonObject { ["structure"] = "trusses" });
        Assert.True(Field("webSection").Shown(f.Model.Get(gable.Id)!));
        Assert.False(Field("collarHeight").Shown(f.Model.Get(gable.Id)!));
        Assert.False(Field("upperPitch").Shown(gable));
        Assert.False(Field("rise").Shown(gable));
        Assert.False(Field("pitch").Shown(flat));
        Assert.True(Field("pitch").Shown(gable));
        Assert.True(Field("covering").Shown(flat));
        // What the interface reads: the conditions with the class's fields.
        var info = System.Text.Json.JsonSerializer.SerializeToNode(f.Session.Info(), Lapin.Protocol.ProtocolJson.Default.SessionInfo)!;
        var roof = info["nodeTypes"]!.AsArray().First(t => (string)t!["type"]! == IfcClass.Roof)!;
        var deck = roof["fields"]!.AsArray().First(x => (string)x!["key"]! == "deckThickness")!;
        Assert.Equal("""[{"shape":["flat","barrel"]},{"structure":["none"]}]""", deck["showIf"]!.ToJsonString());
        Assert.Null(roof["fields"]!.AsArray().First(x => (string)x!["key"]! == "covering")!["showIf"]);
    }

    static JsonObject Without(JsonObject o, string key) { o.Remove(key); return o; }
}
