using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Analysis;
using Lapin.Steel;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The JG Structures exchange file (docs/jg-structures.md, decision 357): the analytical model derived from the physical one,
/// written to the contract JG_Structures_v2_exchange 1.0.0 in its units, null where a value is unknown, checked before it
/// is written and valid against the JSON Schema written from the contract (docs/jg-structures/schema-1.0.0.json).
/// </summary>
public class JgExportTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule());

    /// <summary>
    /// A two-storey concrete frame: four columns 30×30 a storey on a 6 × 5 m grid, beams 30×50 round each level, a floor
    /// over each storey (its core 20 cm of C25/30), a load-bearing wall under the beam on y = 5 on the ground floor, pad
    /// footings under three columns and a strip footing under the wall.
    /// </summary>
    sealed class Frame
    {
        public required Fixture F;
        public required string Ground, First;
        public required Dictionary<string, string> Columns, Beams;
        public required string Slab1, Slab2, Wall, ColumnType, BeamType;
    }

    static Frame Build()
    {
        var f = F();
        var ground = f.Storey;
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var colType = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C30x30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["material"] = "Concrete C25/30" })!["id"]!;
        var beamType = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B30x50", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 }, ["material"] = "Concrete C25/30" })!["id"]!;
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "Floor 20",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 }, new JsonObject { ["material"] = "Concrete C25/30", ["thickness"] = 0.2, ["core"] = true }),
        })!["id"]!;
        var columns = new Dictionary<string, string>();
        var beams = new Dictionary<string, string>();
        (double X, double Y)[] corners = [(0, 0), (6, 0), (6, 5), (0, 5)];
        foreach (var (storey, tag) in new[] { (ground, "G"), (first, "F") })
        {
            foreach (var (x, y) in corners)
                columns[$"{tag}{x},{y}"] = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = storey, ["x"] = x, ["y"] = y, ["typeId"] = colType })!["id"]!;
            for (var i = 0; i < 4; i++)
            {
                var (a, b) = (corners[i], corners[(i + 1) % 4]);
                beams[$"{tag}{i}"] = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = storey, ["x1"] = a.X, ["y1"] = a.Y, ["x2"] = b.X, ["y2"] = b.Y, ["typeId"] = beamType })!["id"]!;
            }
        }
        // The floors over each storey, their outline on the columns' outer faces.
        JsonArray Outline() => new(new JsonArray(-0.15, -0.15), new JsonArray(6.15, -0.15), new JsonArray(6.15, 5.15), new JsonArray(-0.15, 5.15));
        var slab1 = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = first, ["points"] = Outline(), ["typeId"] = slabType })!["id"]!;
        var slab2 = (string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = first, ["points"] = Outline(), ["typeId"] = slabType, ["offset"] = 3.0 })!["id"]!;
        var wallType = f.Type("RC 25", ("Concrete C25/30", 0.25, true));
        var wall = f.Wall(0, 5, 6, 5, wallType);
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = true });
        foreach (var (x, y) in corners.Take(3)) f.Run("structure.addPadFooting", new JsonObject { ["storeyId"] = ground, ["x"] = x, ["y"] = y });
        f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = ground, ["x1"] = 0, ["y1"] = 5, ["x2"] = 6, ["y2"] = 5 });
        return new Frame { F = f, Ground = ground, First = first, Columns = columns, Beams = beams, Slab1 = slab1, Slab2 = slab2, Wall = wall, ColumnType = colType, BeamType = beamType };
    }

    static (JsonObject Doc, JsonObject Report) Export(Fixture f, string units = "contract", JsonObject? more = null)
    {
        var a = more ?? [];
        a["units"] = units;
        var r = f.Session.Query("jg.export", a)!;
        var doc = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String((string)r["data"]!)))!.AsObject();
        return (doc, r["report"]!.AsObject());
    }

    static IEnumerable<JsonObject> Items(JsonObject doc, string entity) => doc[entity]!.AsArray().OfType<JsonObject>();
    static JsonObject Item(JsonObject doc, string entity, string id) => Items(doc, entity).Single(x => (string?)x["id"] == id);
    static JsonObject NodeOf(JsonObject doc, string id) => Item(doc, "nodes", id);
    static double[] At(JsonObject node) => node["coordinates"]!.AsArray().Select(x => (double)x!).ToArray();
    static JsonObject FrameOf(JsonObject doc, string globalId) => Items(doc, "frames").Single(x => (string?)x["id"] == globalId);

    [Fact]
    public void A_two_storey_concrete_frame_exports_nodes_frames_and_shells_on_shared_nodes()
    {
        var t = Build();
        var (doc, report) = Export(t.F);
        Assert.Empty(report["errors"]!.AsArray());
        Assert.Equal("1.0.0", (string?)doc["schema_version"]);
        Assert.Equal("JG_Structures_v2_exchange", (string?)doc["contract"]);
        // Eight columns, eight beams, one frame each (no node falls on their spans), with the GlobalIds as ids.
        Assert.Equal(16, Items(doc, "frames").Count());
        foreach (var id in t.Columns.Values.Concat(t.Beams.Values)) Assert.Equal(id, (string?)FrameOf(doc, id)["lapin"]!["globalId"]);
        // Two floors and the wall, one shell each.
        var shells = Items(doc, "shells").ToList();
        Assert.Equal(3, shells.Count);
        Assert.Equal("wall", (string?)Item(doc, "shells", t.Wall)["kind"]);

        // A beam's ends are the columns' heads: the beam carries the floor, so both lie in its mid-plane (300 − 5 − 10 = 285 cm).
        var beam = FrameOf(doc, t.Beams["G0"]);
        var colA = FrameOf(doc, t.Columns["G0,0"]);
        var colB = FrameOf(doc, t.Columns["G6,0"]);
        Assert.Equal((string?)colA["node_j"], (string?)beam["node_i"]);
        Assert.Equal((string?)colB["node_j"], (string?)beam["node_j"]);
        var head = At(NodeOf(doc, (string)colA["node_j"]!));
        Assert.Equal([0, 0, 285], head.Select(x => Math.Round(x, 6)).ToArray());
        // The beam's own centroid is 275 cm high: its eccentricity is said.
        Assert.Equal(-10, (double)beam["lapin"]!["offset_z"]!, 6);
        // The column above stands on the same node.
        Assert.Equal((string?)colA["node_j"], (string?)FrameOf(doc, t.Columns["F0,0"])["node_i"]);

        // The floor's corners (drawn on the columns' outer faces) are the column heads; the wall's top edge shares the beam's nodes.
        var slab = Item(doc, "shells", t.Slab1);
        var slabNodes = slab["node_ids"]!.AsArray().Select(x => (string)x!).ToHashSet();
        foreach (var c in new[] { "G0,0", "G6,0", "G6,5", "G0,5" }) Assert.Contains((string)FrameOf(doc, t.Columns[c])["node_j"]!, slabNodes);
        var wallNodes = Item(doc, "shells", t.Wall)["node_ids"]!.AsArray().Select(x => (string)x!).ToList();
        Assert.Equal(4, wallNodes.Count);
        var top = FrameOf(doc, t.Beams["G2"]);
        Assert.Contains((string)top["node_i"]!, wallNodes);
        Assert.Contains((string)top["node_j"]!, wallNodes);
        Assert.Contains((string)FrameOf(doc, t.Columns["G0,5"])["node_i"]!, wallNodes);

        // Every reference resolves (the validator says so too).
        var ids = Items(doc, "nodes").Select(n => (string)n["id"]!).ToHashSet();
        foreach (var f in Items(doc, "frames")) { Assert.Contains((string)f["node_i"]!, ids); Assert.Contains((string)f["node_j"]!, ids); }
        foreach (var s in shells) Assert.All(s["node_ids"]!.AsArray(), n => Assert.Contains((string)n!, ids));
        var storeys = Items(doc, "stories").Select(s => (string)s["id"]!).ToHashSet();
        Assert.All(Items(doc, "nodes"), n => Assert.Contains((string)n["story_id"]!, storeys));
    }

    [Fact]
    public void The_columns_and_wall_feet_on_footings_are_fixed_and_the_rest_free()
    {
        var t = Build();
        var (doc, report) = Export(t.F);
        bool[] Restraints(string column) => NodeOf(doc, (string)FrameOf(doc, column)["node_i"]!)["restraints"] is JsonArray r ? r.Select(x => (bool)x!).ToArray() : [];
        foreach (var c in new[] { "G0,0", "G6,0", "G6,5" }) Assert.Equal([true, true, true, true, true, true], Restraints(t.Columns[c]));
        // The fourth column has no pad: it stands on the strip footing under the wall, like the wall's foot.
        Assert.Equal([true, true, true, true, true, true], Restraints(t.Columns["G0,5"]));
        var wallFoot = Item(doc, "shells", t.Wall)["node_ids"]!.AsArray().Select(n => NodeOf(doc, (string)n!)).Where(n => At(n)[2] == 0).ToList();
        Assert.Equal(2, wallFoot.Count);
        Assert.All(wallFoot, n => Assert.NotNull(n["restraints"] as JsonArray));
        // Above the base nothing is restrained, and the restraint's source is said.
        Assert.Null(NodeOf(doc, (string)FrameOf(doc, t.Columns["F0,0"])["node_j"]!)["restraints"]);
        Assert.Contains("footing", (string?)NodeOf(doc, (string)FrameOf(doc, t.Columns["G0,0"])["node_i"]!)["lapin"]!["source"]);
        // Four feet: the wall's two are the corner columns'.
        Assert.Equal(4, (int)report["counts"]!["supports"]!);
    }

    [Fact]
    public void A_column_on_nothing_keeps_null_restraints_and_is_reported()
    {
        var f = F();
        var col = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 })!["id"]!;
        var (doc, report) = Export(f);
        Assert.Null(NodeOf(doc, (string)FrameOf(doc, col)["node_i"]!)["restraints"]);
        Assert.Contains(report["warnings"]!.AsArray(), w => ((string)w!).Contains("no support"));
        Assert.False((bool)report["ready"]!);
    }

    [Fact]
    public void Units_are_the_contracts_and_converted_exactly()
    {
        var t = Build();
        var (doc, _) = Export(t.F);
        var u = doc["units"]!.AsObject();
        Assert.Equal(("cm", "kgf", "kg", "C", "kgf/cm2", "kgf/cm3"), ((string)u["length"]!, (string)u["force"]!, (string)u["mass"]!, (string)u["temperature"]!, (string)u["stress"]!, (string)u["weight_density"]!));
        var first = Item(doc, "stories", t.First);
        Assert.Equal(300.0, (double)first["elevation"]!, 9);
        Assert.Equal(300.0, (double)first["height"]!, 9);
        // C25/30 after EN 1992-1-1 Table 3.1: Ecm 31 GPa = 316 112.0 kgf/cm², fck 25 MPa = 254.93 kgf/cm², ν 0.2.
        var concrete = Items(doc, "materials").Single(m => (string?)m["name"] == "Concrete C25/30");
        Assert.Equal("concrete", (string?)concrete["kind"]);
        Assert.Equal(31e9 / 98066.5, (double)concrete["elastic_modulus"]!, 3);
        Assert.Equal(25e6 / 98066.5, (double)concrete["compressive_strength"]!, 4);
        Assert.Equal(0.2, (double)concrete["poisson_ratio"]!, 9);
        Assert.Null(concrete["yield_strength"]);
        Assert.Equal("standard", (string?)concrete["source"]!["kind"]);
        Assert.Contains("EN 1992-1-1", (string?)concrete["source"]!["values"]!["elastic_modulus"]);
        // SI on request, declared once: metres and kN.
        var (si, _) = Export(t.F, "si");
        Assert.Equal("m", (string?)si["units"]!["length"]);
        Assert.Equal("kN/m2", (string?)si["units"]!["stress"]);
        Assert.Equal(3.0, (double)Item(si, "stories", t.First)["elevation"]!, 9);
        Assert.Equal(31e6, (double)Items(si, "materials").Single(m => (string?)m["name"] == "Concrete C25/30")["elastic_modulus"]!, 3);
    }

    [Fact]
    public void Section_properties_come_from_geometry_in_the_local_axes()
    {
        var t = Build();
        var (doc, _) = Export(t.F);
        var beam = Item(doc, "sections", t.BeamType);
        var p = beam["properties"]!.AsObject();
        // 30 wide (local 3) × 50 deep (local 2): A 1500 cm², I33 = 30·50³/12, I22 = 50·30³/12.
        Assert.Equal(1500, (double)p["area"]!, 6);
        Assert.Equal(312500, (double)p["I33"]!, 4);
        Assert.Equal(112500, (double)p["I22"]!, 4);
        Assert.Equal(12500, (double)p["S33"]!, 4);
        Assert.Equal(7500, (double)p["S22"]!, 4);
        Assert.Equal(18750, (double)p["Z33"]!, 4);
        Assert.Equal(11250, (double)p["Z22"]!, 4);
        Assert.Equal(281737, (double)p["J"]!, 0);
        Assert.Equal("derived_user_geometry", (string?)beam["property_status"]);
        Assert.Equal("user_defined", (string?)beam["kind"]);
        Assert.Equal("rectangle", (string?)beam["geometry"]!["shape"]);
        // The floor's section is its core: 20 cm.
        Assert.Equal(20, (double)Items(doc, "sections").Single(s => (string?)s["category"] == "slab")["thickness"]!, 9);
        // A beam's depth stands up (0°), a column's lies along +Y at rotation 0: local 2 turned 90° from +X.
        Assert.Equal(0, (double)FrameOf(doc, t.Beams["G0"])["local_axis_rotation_deg"]!, 9);
        Assert.Equal(90, (double)FrameOf(doc, t.Columns["G0,0"])["local_axis_rotation_deg"]!, 9);
    }

    [Fact]
    public void An_IPE_300_matches_its_published_values_but_is_not_called_tabulated()
    {
        var p = SectionProperties.Of(StructureAnalytical.Shape(Section.Catalogue["IPE 300"]))!;
        // EN 10365 / the producers' tables: A 53.81 cm², Iy 8356 cm⁴, Wel,y 557.1 cm³, Wpl,y 628.4 cm³, Iz 603.8 cm⁴, Wpl,z 125.2 cm³, It 20.12 cm⁴.
        static void Near(double expected, double actual, double rel = 0.01) => Assert.InRange(actual, expected * (1 - rel), expected * (1 + rel));
        Near(53.81, p.Area * 1e4);
        Near(8356, p.I33 * 1e8);
        Near(557.1, p.S33 * 1e6);
        Near(628.4, p.Z33 * 1e6);
        Near(603.8, p.I22 * 1e8);
        Near(125.2, p.Z22 * 1e6);
        Near(20.12, p.J * 1e8, 0.02);
        Assert.Equal("EN 10365:2017", p.Standard);

        var f = F();
        var type = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = "IPE 300", ["material"] = "S355" })!["id"]!;
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["typeId"] = type });
        var (doc, _) = Export(f);
        var s = Item(doc, "sections", type);
        Assert.Equal("catalog", (string?)s["kind"]);
        Assert.Equal("IPE 300", (string?)s["catalog"]!["designation"]);
        Assert.Equal("IPE", (string?)s["catalog"]!["family"]);
        Assert.Equal("derived_user_geometry", (string?)s["property_status"]);
        Assert.Equal("i_wide_flange", (string?)s["geometry"]!["shape"]);
        // S355: fy 355 MPa, E 210 GPa (EN 1993-1-1), ρ 7850 kg/m³ = 0.00785 kgf/cm³.
        var steel = Items(doc, "materials").Single(m => (string?)m["name"] == "S355");
        Assert.Equal("steel", (string?)steel["kind"]);
        Assert.Equal(355e6 / 98066.5, (double)steel["yield_strength"]!, 3);
        Assert.Equal(210e9 / 98066.5, (double)steel["elastic_modulus"]!, 2);
        Assert.Equal(0.00785, (double)steel["weight_density"]!, 9);
    }

    [Fact]
    public void Other_profiles_have_their_closed_or_thin_walled_values()
    {
        // A circle Ø40: A = π·20², I = π·40⁴/64, J = 2I.
        var c = SectionProperties.Of(new SectionShape("circle", 0.4, 0.4))!;
        Assert.Equal(Math.PI * 0.04, c.Area, 9);
        Assert.Equal(Math.PI * Math.Pow(0.4, 4) / 64, c.I33, 12);
        Assert.Equal(2 * c.I33, c.J, 12);
        // A CHS 114.3×5: A = 17.17 cm², I = 256.9 cm⁴ (EN 10210-2).
        var chs = SectionProperties.Of(StructureAnalytical.Shape(Section.Catalogue["CHS 114.3×5"]))!;
        Assert.InRange(chs.Area * 1e4, 17.1, 17.2);
        Assert.InRange(chs.I33 * 1e8, 256, 258);
        // An RHS 150×100×6 with EN 10210-2 corners (ro = 1.5t, ri = t): A ≈ 27.6 cm², Iy ≈ 850 cm⁴ (885 sharp), It ≈ 920 cm⁴.
        var rhs = SectionProperties.Of(StructureAnalytical.Shape(Section.Catalogue["RHS 150×100×6"]))!;
        Assert.InRange(rhs.Area * 1e4, 27.0, 28.2);
        Assert.InRange(rhs.I33 * 1e8, 835, 870);
        Assert.InRange(rhs.J * 1e8, 880, 960);
        // A cold-formed C: thin-walled J = L·t³/3, its area the sheet's.
        var cf = SectionProperties.Of(StructureAnalytical.Shape(Section.Catalogue["C 200×65×2"]))!;
        Assert.InRange(cf.Area * 1e4, 6.9, 7.5);
        Assert.True(cf.I33 > cf.I22);
        // An L is about its geometric axes, its centroid off the box centre.
        var l = SectionProperties.Of(StructureAnalytical.Shape(Section.Catalogue["L 100×100×10"]))!;
        Assert.InRange(l.Area * 1e4, 19.0, 19.4); // 19.2 tabulated; its toes are left sharp
        Assert.Equal(l.I22, l.I33, 12);
        Assert.True(l.Centroid.X < 0 && l.Centroid.Y < 0);
    }

    [Fact]
    public void An_unknown_material_is_null_never_zero_and_listed_as_missing()
    {
        var f = F();
        var type = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.25, ["depth"] = 0.4 }, ["material"] = "Mystery" })!["id"]!;
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0, ["typeId"] = type });
        var (doc, report) = Export(f);
        var m = Items(doc, "materials").Single(x => (string?)x["name"] == "Mystery");
        foreach (var k in new[] { "elastic_modulus", "poisson_ratio", "yield_strength", "compressive_strength", "weight_density" })
        {
            Assert.True(m.ContainsKey(k));
            Assert.Null(m[k]);
        }
        Assert.Equal("other", (string?)m["kind"]);
        var gaps = report["missing"]!.AsArray().OfType<JsonObject>().Single(x => (string?)x["id"] == (string?)m["id"]);
        Assert.Contains("elastic_modulus", gaps["fields"]!.AsArray().Select(x => (string)x!));
        Assert.Empty(report["errors"]!.AsArray());
        Assert.NotEmpty(doc["documentation"]!["missing"]!.AsArray());
        // The project's own values (IFC's SI units) are taken; a value in MPa where Pa is meant is refused, not guessed.
        var mat = m["id"]!.GetValue<string>();
        f.Run("element.setParam", new JsonObject { ["id"] = mat, ["pset"] = "Pset_MaterialMechanical", ["name"] = "YoungModulus", ["value"] = 30000 });
        f.Run("element.setParam", new JsonObject { ["id"] = mat, ["pset"] = "Pset_MaterialMechanical", ["name"] = "PoissonRatio", ["value"] = 0.25 });
        var (doc2, _) = Export(f);
        var m2 = Item(doc2, "materials", mat);
        Assert.Null(m2["elastic_modulus"]);
        Assert.Equal(0.25, (double)m2["poisson_ratio"]!, 9);
        f.Run("element.setParam", new JsonObject { ["id"] = mat, ["pset"] = "Pset_MaterialMechanical", ["name"] = "YoungModulus", ["value"] = 30e9 });
        Assert.Equal(30e9 / 98066.5, (double)Item(Export(f).Doc, "materials", mat)["elastic_modulus"]!, 2);
    }

    [Fact]
    public void The_validator_catches_a_dangling_reference_a_repeated_id_and_a_bad_catalogue_section()
    {
        var t = Build();
        var (doc, _) = Export(t.F);
        Assert.Empty(JgValidator.Validate(doc));
        var broken = doc.DeepClone().AsObject();
        broken["frames"]![0]!["node_i"] = "N999";
        Assert.Contains(JgValidator.Validate(broken), e => e.Contains("N999") && e.Contains("no such node"));
        var twice = doc.DeepClone().AsObject();
        twice["nodes"]![1]!["id"] = (string)twice["nodes"]![0]!["id"]!;
        Assert.Contains(JgValidator.Validate(twice), e => e.Contains("used twice"));
        var tabulated = doc.DeepClone().AsObject();
        tabulated["sections"]![0]!["property_status"] = "verified_tabulated";
        Assert.Contains(JgValidator.Validate(tabulated), e => e.Contains("verified_tabulated"));
        var catalogue = doc.DeepClone().AsObject();
        catalogue["sections"]![0]!["kind"] = "catalog";
        Assert.Contains(JgValidator.Validate(catalogue), e => e.Contains("catalogue section"));
        var shell = doc.DeepClone().AsObject();
        shell["shells"]![0]!["node_ids"] = new JsonArray("N1", "N2");
        Assert.Contains(JgValidator.Validate(shell), e => e.Contains("fewer than three"));
    }

    [Fact]
    public void The_export_validates_against_the_JSON_Schema_written_from_the_contract()
    {
        var schema = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "docs", "jg-structures", "schema-1.0.0.json")))!;
        var t = Build();
        foreach (var units in new[] { "contract", "si" })
            Assert.Empty(MiniJsonSchema.Validate(schema, Export(t.F, units).Doc));
        // And the schema is not one that passes anything.
        var bad = Export(t.F).Doc;
        bad["nodes"]![0]!["coordinates"] = new JsonArray(1.0, 2.0);
        bad["frames"]![0]!["kind"] = "brace";
        bad["materials"]![0]!.AsObject().Remove("elastic_modulus");
        var errors = MiniJsonSchema.Validate(schema, bad);
        Assert.Contains(errors, e => e.Contains("coordinates"));
        Assert.Contains(errors, e => e.Contains("frames[0].kind"));
        Assert.Contains(errors, e => e.Contains("elastic_modulus is required"));
    }

    [Fact]
    public void Storeys_and_kinds_chosen_leave_the_others_out_and_say_so()
    {
        var t = Build();
        var (doc, report) = Export(t.F, more: new JsonObject { ["storeys"] = new JsonArray(t.Ground), ["kinds"] = new JsonArray("columns", "beams", "supports") });
        Assert.Empty(Items(doc, "shells"));
        Assert.Equal(8, Items(doc, "frames").Count());
        Assert.Contains(report["leftOut"]!.AsArray(), x => (string?)x!["id"] == t.Wall && ((string?)x["reason"])!.Contains("walls"));
        // A wall that is not load-bearing is left out unless every wall is asked for.
        var f = F();
        f.Wall(0, 0, 4, 0);
        Assert.Empty(Items(Export(f).Doc, "shells"));
        Assert.Single(Items(Export(f, more: new JsonObject { ["allWalls"] = true }).Doc, "shells"));
    }

    [Fact]
    public void A_beam_ending_on_another_splits_it_and_a_slab_splits_along_an_inner_beam()
    {
        var f = F();
        var colType = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        foreach (var (x, y) in new[] { (0.0, 0.0), (8.0, 0.0), (8.0, 6.0), (0.0, 6.0) })
            f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["typeId"] = colType });
        string Beam(double x1, double y1, double x2, double y2) => (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 })!["id"]!;
        var south = Beam(0, 0, 8, 0);
        Beam(8, 0, 8, 6); Beam(8, 6, 0, 6); Beam(0, 6, 0, 0);
        // A secondary beam from the middle of the south beam to the middle of the north one.
        var inner = Beam(4, 0, 4, 6);
        var slabType = (string)f.Run("architecture.createSlabType", new JsonObject { ["layers"] = new JsonArray(new JsonObject { ["material"] = "C30/37", ["thickness"] = 0.18, ["core"] = true }) })!["id"]!;
        var second = (string)f.Run("storey.create", new JsonObject { ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var slab = (string)f.Run("architecture.addSlab", new JsonObject
        {
            ["storeyId"] = second, ["typeId"] = slabType,
            ["points"] = new JsonArray(new JsonArray(0.0, 0.0), new JsonArray(8.0, 0.0), new JsonArray(8.0, 6.0), new JsonArray(0.0, 6.0)),
        })!["id"]!;
        var (doc, report) = Export(f);
        Assert.Empty(report["errors"]!.AsArray());
        // The south beam is two frames, #1 and #2, meeting at the inner beam's end.
        var pieces = Items(doc, "frames").Where(x => (string?)x["lapin"]!["globalId"] == south).ToList();
        Assert.Equal([$"{south}#1", $"{south}#2"], pieces.Select(x => (string)x["id"]!).ToArray());
        Assert.Equal((string?)pieces[0]["node_j"], (string?)FrameOf(doc, inner)["node_i"]);
        // The floor is two shells either side of the inner beam, sharing its two nodes.
        var halves = Items(doc, "shells").Where(x => (string?)x["lapin"]!["globalId"] == slab).ToList();
        Assert.Equal(2, halves.Count);
        var shared = halves[0]["node_ids"]!.AsArray().Select(x => (string)x!).Intersect(halves[1]["node_ids"]!.AsArray().Select(x => (string)x!)).ToHashSet();
        Assert.Equal(new HashSet<string> { (string)FrameOf(doc, inner)["node_i"]!, (string)FrameOf(doc, inner)["node_j"]! }, shared);
    }

    [Fact]
    public void A_steel_hall_exports_its_members_with_pinned_bases()
    {
        var f = new Fixture(new StructureModule(), new SteelModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Studio.StudioModule());
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["secondary"] = false } });
        var (doc, report) = Export(f);
        Assert.Empty(report["errors"]!.AsArray());
        Assert.NotEmpty(Items(doc, "frames"));
        // Every column base the hall made is a pinned support (its default) on that column's foot.
        var bases = f.Model.OfClass(SteelNodes.Class).Where(n => SteelNodes.KindOf(n) == "base").ToList();
        Assert.NotEmpty(bases);
        var pinned = Items(doc, "nodes").Where(n => n["restraints"] is JsonArray r && r.Select(x => (bool)x!).SequenceEqual([true, true, true, false, false, false])).Count();
        Assert.Equal(bases.Count, pinned);
        // Rafters slope: beams whose local 2 is their own upward normal (rotation 0).
        Assert.Contains(Items(doc, "frames"), x => (string?)x["kind"] == "beam");
        Assert.All(Items(doc, "sections"), s => Assert.NotNull(s["properties"]!["area"]));
        // The frames are one structure: the rafters meet at the apex (their connection's setback closed) and on the columns'
        // axes, the gable posts end under the rafters; every node meets two members or is held.
        var uses = Items(doc, "frames").SelectMany(x => new[] { (string)x["node_i"]!, (string)x["node_j"]! }).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        Assert.All(Items(doc, "nodes"), n => Assert.True(uses[(string)n["id"]!] >= 2 || n["restraints"] is JsonArray, $"node {(string)n["id"]!} hangs free"));
        Assert.Empty(report["warnings"]!.AsArray());
    }

    [Fact]
    public void The_local_axes_rotation_follows_the_depth_about_local_1()
    {
        // A column turned 30°: its depth (the outline's y) is 120° from +X.
        Assert.Equal(120, AnalyticalModel.Rotation(new Vec3(0, 0, 0), new Vec3(0, 0, 3), new Vec3(-Math.Sin(Math.PI / 6), Math.Cos(Math.PI / 6), 0)), 6);
        // A beam along +X with its depth up: 0°; turned on its side (depth along +Y): local 2 from up to +Y is −90° about +X.
        Assert.Equal(0, AnalyticalModel.Rotation(new Vec3(0, 0, 0), new Vec3(5, 0, 0), Vec3.UnitZ), 9);
        Assert.Equal(-90, AnalyticalModel.Rotation(new Vec3(0, 0, 0), new Vec3(5, 0, 0), new Vec3(0, 1, 0)), 6);
    }

    static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "engine", "Lapin.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("No repository root above the tests.");
    }
}
