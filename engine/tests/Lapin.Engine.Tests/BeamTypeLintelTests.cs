using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #135: structure types from a CSV (columns guessed in five languages, the profile resolved, the choices for those the
/// project has, one step to undo, written back), a beam type as a precast lintel (its section, lengths, material and bearing,
/// the rule's own bearing over it), typed IfcBeamType LINTEL in IFC and listed by its type and mark; one catalogue with the
/// lintel products; an old project's lintels as they were.
/// </summary>
public class BeamTypeLintelTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-r135").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture New() => new(new ArchitectureModule(), new StructureModule());

    /// <summary>A producer's export as the tester's: one row a length, its values and units in columns of their own.</summary>
    const string Producer =
        "name.value;height.value;height.unit;height.note;length.name;length.value;length.unit;width.name;width.value;width.unit;weight.value;code.value\n" +
        "KP 7 - 100;238;mm;;Délka;1000;mm;Šířka;70;mm;35;64168410\n" +
        "KP 7 - 125;238;mm;;Délka;1250;mm;Šířka;70;mm;43,75;64168412\n" +
        "KP 7 - 150;238;mm;;Délka;1500;mm;Šířka;70;mm;52,5;64168415\n" +
        "KP 11,5 - 100 (24);71;mm;;Délka;1000;mm;Šířka;115;mm;14;64168110\n" +
        "KP 11,5 - 100 (40);71;mm;;Délka;1000;mm;Šířka;115;mm;14;64168140\n" +
        "KP 11,5 - 125 (24);71;mm;;Délka;1250;mm;Šířka;115;mm;17,5;64168142\n";

    static JsonObject Args(string text, string kind = "lintel", bool merge = true, JsonObject? choices = null)
    {
        var a = new JsonObject { ["text"] = text, ["kind"] = kind, ["merge"] = merge };
        if (choices != null) a["choices"] = choices;
        return a;
    }

    static List<Entity> BeamTypes(Fixture f) => f.Model.OfClass(IfcClass.BeamType).OrderBy(t => t.Name).ToList();

    [Fact]
    public void A_producers_table_is_read_by_its_value_and_unit_columns_and_rows_of_one_section_become_one_type_with_its_lengths()
    {
        var f = New();
        var g = TypeTables.Guess(Lapin.Platform.Tables.Csv(Producer));
        Assert.Equal(0, g.Header);
        Assert.Equal(0, g.Map["name"]!.Index);
        Assert.Equal(1, g.Map["h"]!.Index);
        Assert.Equal(5, g.Map["length"]!.Index);
        Assert.Equal(8, g.Map["b"]!.Index);
        Assert.Equal(11, g.Map["code"]!.Index);
        Assert.Equal(2, g.UnitColumns["h"]);

        var preview = f.Session.Query("structure.previewTypes", Args(Producer))!.AsObject();
        Assert.True((bool)preview["mergeable"]!);
        var rows = preview["rows"]!.AsArray();
        Assert.Equal(["KP 7", "KP 11,5"], rows.Select(r => (string)r!["name"]!));
        Assert.All(rows, r => Assert.Equal("new", (string)r!["status"]!));

        var done = f.Run("structure.importTypes", Args(Producer))!;
        Assert.Equal(2, (int)done["typesMade"]!);
        var kp7 = BeamTypes(f).Single(t => t.Name == "KP 7");
        Assert.Equal(LintelTypes.Lintel, kp7.PredefinedType);
        var l7 = LintelTypes.Read(f.Model, kp7)!;
        Assert.Equal(0.07, l7.Width, 9);
        Assert.Equal(0.238, l7.Height, 9);
        Assert.Equal([1.0, 1.25, 1.5], l7.Lengths);
        Assert.Equal("64168415", l7.CodeOf(1.5));
        // The two 1.00 m rows of KP 11,5 (two pallet sizes) are one length, the first's code.
        var l11 = LintelTypes.Read(f.Model, BeamTypes(f).Single(t => t.Name == "KP 11,5"))!;
        Assert.Equal([1.0, 1.25], l11.Lengths);
        Assert.Equal("64168110", l11.CodeOf(1.0));

        // Not merged: one type a row.
        var g2 = New();
        g2.Run("structure.importTypes", Args(Producer, merge: false));
        Assert.Equal(6, BeamTypes(g2).Count);
        Assert.Equal([1.25], LintelTypes.Read(g2.Model, BeamTypes(g2).Single(t => t.Name == "KP 7 - 125"))!.Lengths);
    }

    [Theory]
    [InlineData(new[] { "KP 7 - 100", "KP 7 - 125", "KP 7 - 200" }, "KP 7")]
    [InlineData(new[] { "KP 11,5 - 100 (24)", "KP 11,5 - 125 (40)" }, "KP 11,5")]
    [InlineData(new[] { "Beam", "Beam" }, "Beam")]
    [InlineData(new[] { "Alpha", "Beta" }, "Alpha")]
    public void Merged_rows_are_named_by_what_their_names_share_up_to_a_words_end(string[] names, string expected) =>
        Assert.Equal(expected, TypeTables.CommonName(names));

    [Fact]
    public void Columns_are_guessed_in_czech_romanian_german_and_french_and_the_profile_is_resolved_or_the_row_refused()
    {
        foreach (var head in new[]
        {
            "Název;Druh;Profil;Výška (mm);Šířka (mm);Materiál;Označení;Uložení (mm);Výrobce;Kód;Délka (mm)",
            "Denumire;Tip element;Profil;Înălțime (mm);Lățime (mm);Material;Marcaj;Reazem (mm);Producător;Cod;Lungime (mm)",
            "Bezeichnung;Bauteil;Profil;Höhe (mm);Breite (mm);Werkstoff;Position;Auflager (mm);Hersteller;Artikelnummer;Länge (mm)",
            "Nom;Genre;Profil;Hauteur (mm);Largeur (mm);Matériau;Repère;Appui (mm);Fabricant;Code;Longueur (mm)",
        })
        {
            var g = TypeTables.Guess(Lapin.Platform.Tables.Csv(head + "\nX;beam;;400;250;;;;;;\n"));
            Assert.True(g.Header == 0, head);
            Assert.Equal(Enumerable.Range(0, 11).ToList(), new[] { "name", "kind", "profile", "h", "b", "material", "mark", "bearing", "manufacturer", "code", "length" }.Select(k => g.Map[k]?.Index ?? -1).ToList());
        }
        var f = New();
        const string text = "Název;Druh;Profil;Výška;Šířka;Materiál;Označení;Uložení\n" +
            "Průvlak 25×40;nosník;;400;250;Beton;P1;\n" +
            "Sloup IPE;sloup;IPE 300;;;;S1;\n" +
            "Neznámý;nosník;XYZ 999;;;;;\n" +
            "Překlad;překlad;;71;115;;L1;125\n";
        var rows = f.Session.Query("structure.previewTypes", new JsonObject { ["text"] = text, ["kind"] = "beam" })!["rows"]!.AsArray();
        Assert.Equal(["new", "new", "error", "new"], rows.Select(r => (string)r!["status"]!));
        Assert.Contains("XYZ 999", (string)rows[2]!["problem"]!);
        var done = f.Run("structure.importTypes", new JsonObject { ["text"] = text, ["kind"] = "beam" })!;
        Assert.Equal((3, 1), ((int)done["typesMade"]!, (int)done["typesSkipped"]!));
        var beam = f.Model.OfClass(IfcClass.BeamType).Single(t => t.Name == "Průvlak 25×40");
        Assert.Equal(("rectangle", 0.25, 0.4), ((string)beam.Attrs["section"]!["kind"]!, (double)beam.Attrs["section"]!["width"]!, (double)beam.Attrs["section"]!["depth"]!));
        Assert.Equal("Beton", f.Model.Get(beam.Str("material")!)!.Name);
        Assert.Equal("P1", beam.Str("mark"));
        var column = Assert.Single(f.Model.OfClass(IfcClass.ColumnType));
        Assert.Equal("IPE 300", (string?)column.Attrs["section"]!["name"]);
        Assert.Equal("Steel", f.Model.Get(column.Str("material")!)!.Name);
        var lintel = f.Model.OfClass(IfcClass.BeamType).Single(t => t.Name == "Překlad");
        Assert.Equal(LintelTypes.Lintel, lintel.PredefinedType);
        Assert.Equal(0.125, LintelTypes.Read(f.Model, lintel)!.Bearing!.Value, 9);
    }

    [Fact]
    public void A_profile_of_a_library_the_interface_holds_is_copied_into_the_project_with_its_type()
    {
        var f = New();
        var library = new JsonArray(new JsonObject
        {
            ["name"] = "Precast lintels",
            ["profiles"] = new JsonArray(new JsonObject { ["name"] = "PL 150", ["kind"] = "rectangle", ["h"] = 0.15, ["b"] = 0.125, ["material"] = "concrete" }),
        });
        var done = f.Run("structure.importTypes", new JsonObject { ["text"] = "Name;Profile\nLintel PL;PL 150\n", ["kind"] = "lintel", ["libraries"] = library })!;
        Assert.Equal(1, (int)done["typesMade"]!);
        var copy = Assert.Single(f.Model.OfClass(Lapin.Platform.ProfileLibraries.ProjectProfiles.Class));
        var t = Assert.Single(f.Model.OfClass(IfcClass.BeamType));
        Assert.Equal(copy.Id, (string?)t.Attrs["section"]!["profileId"]);
        Assert.Equal(0.15, LintelTypes.Read(f.Model, t)!.Height, 9);
    }

    [Fact]
    public void Types_the_project_has_are_the_same_updated_kept_both_or_skipped_as_chosen_and_the_import_is_one_step_to_undo()
    {
        var f = New();
        f.Run("structure.importTypes", Args(Producer));
        var kp7 = BeamTypes(f).Single(t => t.Name == "KP 7").Id;
        // The same file again: nothing differs.
        var again = f.Run("structure.importTypes", Args(Producer))!;
        Assert.Equal((0, 2), ((int)again["typesMade"]!, (int)again["typesReused"]!));
        // A new length for KP 7, KP 11,5 higher.
        var changed = Producer + "KP 7 - 175;238;mm;;Délka;1750;mm;Šířka;70;mm;61;64168417\n";
        changed = changed.Replace("KP 11,5 - 125 (24);71;", "KP 11,5 - 125 (24);80;").Replace("KP 11,5 - 100 (24);71;", "KP 11,5 - 100 (24);80;").Replace("KP 11,5 - 100 (40);71;", "KP 11,5 - 100 (40);80;");
        var preview = f.Session.Query("structure.previewTypes", Args(changed))!.AsObject();
        var conflicts = preview["conflicts"]!.AsArray();
        Assert.Equal(["KP 7", "KP 11,5"], conflicts.Select(c => (string)c!["name"]!));
        Assert.Contains("lengths", conflicts[0]!["differs"]!.AsArray().Select(d => (string)d!));
        Assert.Contains("section", conflicts[1]!["differs"]!.AsArray().Select(d => (string)d!));

        var before = f.Model.Entities.Count();
        var r = f.Run("structure.importTypes", Args(changed, choices: new JsonObject { ["KP 7"] = "update", ["KP 11,5"] = "both" }))!;
        Assert.Equal((1, 1), ((int)r["typesMade"]!, (int)r["typesChanged"]!));
        Assert.Equal(["KP 11,5 (2)"], r["renamed"]!.AsArray().Select(x => (string)x!));
        Assert.Equal([1.0, 1.25, 1.5, 1.75], LintelTypes.Read(f.Model, kp7)!.Lengths);
        Assert.Equal(0.08, LintelTypes.Read(f.Model, BeamTypes(f).Single(t => t.Name == "KP 11,5 (2)"))!.Height, 9);
        Assert.Equal(0.071, LintelTypes.Read(f.Model, BeamTypes(f).Single(t => t.Name == "KP 11,5"))!.Height, 9);
        // One step back: as it was.
        f.Session.Undo();
        Assert.Equal(before, f.Model.Entities.Count());
        Assert.Equal([1.0, 1.25, 1.5], LintelTypes.Read(f.Model, kp7)!.Lengths);
        // Skipped: left as it is.
        var s = f.Run("structure.importTypes", Args(changed, choices: new JsonObject { ["KP 7"] = "skip", ["KP 11,5"] = "skip" }))!;
        Assert.Equal(2, (int)s["typesSkipped"]!);
        Assert.Equal([1.0, 1.25, 1.5], LintelTypes.Read(f.Model, kp7)!.Lengths);
    }

    [Fact]
    public void The_types_written_as_a_CSV_are_read_back_the_same()
    {
        var f = New();
        f.Run("structure.importTypes", Args(Producer));
        f.Run("structure.createBeamType", new JsonObject { ["name"] = "HEA beam", ["section"] = "HEA 240" });
        f.Run("structure.createColumnType", new JsonObject { ["name"] = "C 30", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } });
        var csv = (string)f.Session.Query("structure.typesCsv", new JsonObject())!;
        Assert.StartsWith(string.Join(';', TypeTables.CsvHeads), csv);
        Assert.Contains("KP 7;lintel;;238;70;Reinforced concrete;;;;;1000|1250|1500;64168410|64168412|64168415", csv);
        // Read back into the same project: everything the same.
        var back = f.Run("structure.importTypes", new JsonObject { ["text"] = csv, ["kind"] = "beam" })!;
        Assert.Equal((0, 0, 4), ((int)back["typesMade"]!, (int)back["typesChanged"]!, (int)back["typesReused"]!));
        // And into a new one: the same types.
        var g = New();
        g.Run("structure.importTypes", new JsonObject { ["text"] = csv, ["kind"] = "beam" });
        Assert.Equal(f.Model.OfClass(IfcClass.BeamType).Select(t => (t.Name, t.PredefinedType)).Order(), g.Model.OfClass(IfcClass.BeamType).Select(t => (t.Name, t.PredefinedType)).Order());
        Assert.Equal("HEA 240", (string?)g.Model.OfClass(IfcClass.BeamType).Single(t => t.Name == "HEA beam").Attrs["section"]!["name"]);
    }

    [Fact]
    public void A_beam_types_mark_bearing_and_lengths_are_changed_in_Structure_types_each_length_keeping_its_code()
    {
        var f = New();
        f.Run("structure.importTypes", Args(Producer));
        var kp7 = BeamTypes(f).Single(t => t.Name == "KP 7").Id;
        f.Run("structure.updateType", new JsonObject { ["id"] = kp7, ["mark"] = "P7", ["bearing"] = 0.15, ["lengths"] = new JsonArray(1.0, 1.5, 2.0) });
        var l = LintelTypes.Read(f.Model, kp7)!;
        Assert.Equal(("P7", 0.15), (l.Mark, l.Bearing!.Value));
        Assert.Equal([1.0, 1.5, 2.0], l.Lengths);
        Assert.Equal("64168415", l.CodeOf(1.5));
        Assert.Null(l.CodeOf(2.0));
        f.Run("structure.updateType", new JsonObject { ["id"] = kp7, ["mark"] = "", ["bearing"] = null });
        Assert.Equal((null, null), (LintelTypes.Read(f.Model, kp7)!.Mark, LintelTypes.Read(f.Model, kp7)!.Bearing));
    }

    // ------------------------------------------------------------------ a beam type as a lintel

    /// <summary>Two brick walls, the door type's lintel a beam type KP 7 (bearing 125 by default, 1.25, 1.5, 1.75 m), a door in each.</summary>
    static (Fixture F, string TypeId, string DoorType, string[] Walls) Doors(bool ownBearing = false)
    {
        var f = New();
        f.Run("structure.importTypes", new JsonObject
        {
            ["text"] = "Name;Kind;h;b;Material;Mark;Bearing;Manufacturer;Lengths\nKP 7;lintel;238;70;Precast concrete;P7;125;Heluz;1250|1500|1750\n",
        });
        var typeId = f.Model.OfClass(IfcClass.BeamType).Single().Id;
        var wallType = f.Type("Brick 30", ("Brick", 0.3, true));
        var walls = new[] { f.Wall(0, 0, 6, 0, wallType), f.Wall(0, 4, 6, 4, wallType) };
        var rule = new LintelRule("precast", 0.25, 0.25, BeamType: typeId, OwnBearing: ownBearing ? true : null);
        var door = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultDoor("D1") with { Width = 1.0, Height = 2.1, Lintel = rule }).ToJson() })!["id"]!;
        foreach (var w in walls) f.Run("architecture.addDoor", new JsonObject { ["wallId"] = w, ["along"] = 3.0, ["typeId"] = door });
        return (f, typeId, door, walls);
    }

    static GeometryPart Lintel(Fixture f, string wall) => Assert.Single(f.Session.Geometry(wall)!.Parts, p => p.Key!.StartsWith("lintel:"));

    [Fact]
    public void A_doors_lintel_of_a_beam_type_takes_its_section_its_stock_length_its_material_and_its_bearing_in_both_walls()
    {
        var (f, typeId, _, walls) = Doors();
        foreach (var w in walls)
        {
            var part = Lintel(f, w);
            var b = Measure.Bounds(part.Solid)!.Value;
            // 1.00 + 2 × 0.125 = 1.25: the type's shortest length that covers it, centred over the door (2.5 … 3.5).
            Assert.Equal(2.375, b.Min.X, 6);
            Assert.Equal(3.625, b.Max.X, 6);
            Assert.Equal(0.07, b.Max.Y - b.Min.Y, 6);
            Assert.Equal(2.1, b.Min.Z, 6);
            Assert.Equal(2.338, b.Max.Z, 6);
            Assert.Equal("Precast concrete", f.Model.Get(part.Material!)!.Name);
            Assert.Equal(typeId, part.TypeId);
        }
        // The type changed: the lintels follow it.
        f.Run("structure.updateType", new JsonObject { ["id"] = typeId, ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.115, ["depth"] = 0.071 } });
        var after = Measure.Bounds(Lintel(f, walls[1]).Solid)!.Value;
        Assert.Equal(0.115, after.Max.Y - after.Min.Y, 6);
        Assert.Equal(2.171, after.Max.Z, 6);
    }

    [Fact]
    public void The_rules_own_bearing_wins_over_the_types()
    {
        var (f, _, _, walls) = Doors(ownBearing: true);
        var b = Measure.Bounds(Lintel(f, walls[0]).Solid)!.Value;
        // 1.00 + 2 × 0.25 = 1.50.
        Assert.Equal(2.25, b.Min.X, 6);
        Assert.Equal(3.75, b.Max.X, 6);
    }

    [Fact]
    public void A_beam_type_with_no_lengths_is_cut_to_length()
    {
        var p = new LintelProduct("X", 0.1, 0.2, []);
        Assert.Equal(1.37, Lintels.ProductLength(p, 1.37)!.Value, 9);
        Assert.Empty(Lintels.Problems(new LintelRule("precast", 0.2, 0.2, Product: p, BeamType: "t")));
        Assert.NotEmpty(Lintels.Problems(new LintelRule("precast", 0.2, 0.2, Product: p)));
    }

    [Fact]
    public void In_IFC_the_lintels_are_beams_typed_by_the_beam_type_LINTEL()
    {
        var (f, typeId, _, walls) = Doors();
        var path = Path.Combine(_dir, "lintels.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Read(path);
        var type = Assert.Single(file.OfType("IFCBEAMTYPE"));
        Assert.Equal("KP 7", (string?)type[2]);
        Assert.Equal(new StepEnum("LINTEL"), type[9]);
        var lintels = file.OfType("IFCBEAM").Where(b => Equals(b[8], new StepEnum("LINTEL"))).ToList();
        Assert.Equal(2, lintels.Count);
        var rel = Assert.Single(file.OfType("IFCRELDEFINESBYTYPE"), r => Equals(r[5], new StepRef(type.Id)));
        Assert.Equal(lintels.Select(l => l.Id).Order(), (rel[4] as List<object?>)!.OfType<StepRef>().Select(x => x.Id).Order());
    }

    [Fact]
    public void The_lintel_schedule_lists_them_by_their_type_and_mark_with_the_code_of_the_length()
    {
        var (f, typeId, _, _) = Doors();
        f.Run("structure.importTypes", new JsonObject
        {
            ["text"] = "Name;Kind;h;b;Material;Mark;Bearing;Manufacturer;Lengths;Codes\nKP 7;lintel;238;70;Precast concrete;P7;125;Heluz;1250|1500|1750;A125|A150|A175\n",
            ["choices"] = new JsonObject { ["KP 7"] = "update" },
        });
        var data = Schedules.Of(f.Session.Reader, "lintel");
        var e = Assert.Single(data.Entries);
        Assert.Equal("P7", e.Mark);
        Assert.Equal(2, e.Total);
        Assert.Equal("Heluz KP 7 (A125)", e.Lintel!.Product);
        Assert.Equal("KP 7", e.Lintel.BeamType);
        Assert.True(e.Lintel.Fits);
        Assert.Equal(1.25, e.Lintel.Length, 6);
        // The doors' lintel row names it.
        var joinery = Schedules.Of(f.Session.Reader, "door");
        Assert.Equal("P7", joinery.LintelMarks!.Values.Distinct().Single());
    }

    // ------------------------------------------------------------------ one catalogue, old projects

    [Fact]
    public void A_lintel_catalogue_imported_makes_its_beam_types_and_old_rules_with_a_product_build_as_before()
    {
        var f = New();
        var wallType = f.Type("Brick 30", ("Brick", 0.3, true));
        var wall = f.Wall(0, 0, 6, 0, wallType);
        // An older project: the rule keeps its product's copy, no beam type.
        var product = new LintelProduct("L1", 0.115, 0.071, [1.0, 1.25, 1.5], "Porotherm");
        var old = new LintelRule("precast", 0.25, 0.25, Product: product);
        var json = old.ToJson().ToJsonString();
        Assert.DoesNotContain("beamType", json);
        Assert.DoesNotContain("ownBearing", json);
        var door = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultDoor("D1") with { Width = 0.9, Height = 2.1, Lintel = old }).ToJson() })!["id"]!;
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 3.0, ["typeId"] = door });
        var before = Measure.Bounds(Lintel(f, wall).Solid)!.Value;
        Assert.Null(Lintel(f, wall).TypeId);

        var r = f.Run("architecture.importLintelProducts", new JsonObject { ["text"] = "L1;115;71;1000|1250|1500;Porotherm\nL2;175;71;1000|1250;Porotherm" })!;
        Assert.Equal(2, (int)r["typesMade"]!);
        var types = f.Model.OfClass(IfcClass.BeamType).OrderBy(t => t.Name).ToList();
        Assert.Equal(["L1", "L2"], types.Select(t => t.Name));
        Assert.All(types, t => Assert.Equal(LintelTypes.Lintel, t.PredefinedType));
        Assert.Equal([1.0, 1.25, 1.5], LintelTypes.Read(f.Model, types[0])!.Lengths);
        Assert.Equal("Porotherm", LintelTypes.Read(f.Model, types[0])!.Manufacturer);
        // The catalogue is still listed, and the lintel types beside it.
        Assert.Equal(2, f.Session.Query("architecture.lintelProducts", new JsonObject())!.AsArray().Count);
        Assert.Equal(["L1", "L2"], f.Session.Query("architecture.lintelTypes", new JsonObject())!.AsArray().Select(x => (string)x!["name"]!));
        // Imported again: the same types changed in place, not made twice.
        var again = f.Run("architecture.importLintelProducts", new JsonObject { ["text"] = "L1;115;71;1000|1250|1500|1750;Porotherm" })!;
        Assert.Equal((0, 1), ((int)again["typesMade"]!, (int)again["typesChanged"]!));
        Assert.Equal(2, f.Model.OfClass(IfcClass.BeamType).Count());

        // The old rule's lintel as it was.
        var after = Measure.Bounds(Lintel(f, wall).Solid)!.Value;
        Assert.Equal(before, after);
        Assert.Equal(1.5, after.Max.X - after.Min.X, 6);
    }
}
