using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Cost;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Mesh.Bos;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// What an element imported as a reference measures (the user's report "can't get quantities"): a reference keeps its IFC
/// class, but not the attributes its class's native quantities are worked out from, and the takeoff gave a wall 0 m and no
/// doors at all. A reference's quantities are its file's own quantity sets, in SI by the file's units, then what its stored
/// body gives for what the file lacks (its volume when closed, the count); its type is the one its file relates it to.
/// The same for Fragments and BIM Open Schema items.
/// </summary>
public class ReferenceQuantityTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-ref-qto").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new CostModule());

    /// <summary>A small house of single-layer walls (so a wall goes out whole, not as its layers), a door, a window, a floor, a column and a beam.</summary>
    static Fixture House()
    {
        var f = Full();
        var concrete = f.Type("Concrete 25", ("Concrete", 0.25, true));
        var wall = f.Wall(0, 0, 6, 0, concrete);
        f.Wall(6, 0, 6, 4, concrete);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.5 });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(6, 0), new JsonArray(6, 4), new JsonArray(0, 4)) });
        var ct = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C 30x30", ["section"] = new JsonObject { ["shape"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = ct, ["x"] = 3, ["y"] = 2 });
        var bt = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B 30x50", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.5 } })!["id"]!;
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = bt, ["x1"] = 0, ["y1"] = 4, ["x2"] = 6, ["y2"] = 4 });
        return f;
    }

    static readonly string[] Measured = ["Kind", "Type", "Count", "Length (m)", "Gross area (m²)", "Net area (m²)"];

    /// <summary>The takeoff's rows by kind and type, with the columns it has of <paramref name="columns"/>, the total left out.</summary>
    static Dictionary<string, Dictionary<string, string>> Takeoff(Fixture f, IEnumerable<string> columns)
    {
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "quantities" })!["id"]!;
        var grid = new QuantityTakeoff().Grid(f.Session.Reader, f.Model.Get(id)!);
        var cols = grid.Columns.ToList();
        return grid.Rows.Where(r => r[0] != "Total").ToDictionary(r => $"{r[0]} / {r[1]}",
            r => columns.Where(cols.Contains).ToDictionary(c => c, c => r[cols.IndexOf(c)]));
    }

    static QuantitySet BaseSet(Fixture f, string id) => f.Session.Quantities(id).First(s => s.Part == null);

    static void Near(Quantity file, IReadOnlyList<Quantity> got, string what)
    {
        var hit = Assert.Single(got, x => x.Name == file.Name);
        Assert.Equal(file.Kind, hit.Kind);
        Assert.True(Math.Abs(file.Value - hit.Value) <= 1e-4 * Math.Max(1, Math.Abs(file.Value)), $"{what} {file.Name}: {file.Value} in the file, {hit.Value} measured");
    }

    [Fact]
    public void A_reference_from_IFC_measures_by_its_files_quantity_sets_and_is_named_by_its_files_type()
    {
        var a = House();
        var path = Path.Combine(_dir, "house.ifc");
        IfcExport.Write(a.Session, path);
        var b = Full();
        IfcImport.Run(b.Session, path, new IfcImportOptions("reference"), out _);

        // Every element came as a reference, and measures what the file says of it: Lapin's own quantities, written to the file.
        foreach (var cls in new[] { IfcClass.Wall, IfcClass.Door, IfcClass.Window, IfcClass.Slab, IfcClass.Column, IfcClass.Beam })
        {
            var refs = b.Model.OfClass(cls).ToList();
            Assert.NotEmpty(refs);
            foreach (var e in refs)
            {
                Assert.True(ReferenceLayers.IsReference(e), $"{e.Class} {e.Name} is not a reference");
                if (a.Model.Get(e.Id) is not { } native) continue;
                var file = BaseSet(a, native.Id);
                var got = BaseSet(b, e.Id);
                Assert.Equal(file.Name, got.Name);
                foreach (var q in file.Quantities.Where(q => double.IsFinite(q.Value))) Near(q, got.Quantities, e.Class);
                Assert.Contains(got.Quantities, x => x.Name == "Count" && x.Value == 1);
            }
        }
        Assert.All(b.Model.OfClass(IfcClass.Wall), w => Assert.True(BaseSet(b, w.Id).Quantities.Single(q => q.Name == "Length").Value > 3.5));

        // The takeoff: the same rows as the native model's, its doors and windows counted, each under its type's name. (The wall
        // with the openings goes out as its layer, a part, its lintels as beams: only the plain wall comes back a wall.)
        var nativeRows = Takeoff(a, Measured);
        var referenced = Takeoff(b, Measured);
        Assert.Equal(["Wall", "Concrete 25", "1", "4.00", "12.00"], referenced["Wall / Concrete 25"].Values.Take(5));
        foreach (var key in nativeRows.Keys.Where(k => k.StartsWith("Door /") || k.StartsWith("Window /") || k.StartsWith("Floor /") || k.StartsWith("Column /") || k.StartsWith("Beam / B")))
        {
            Assert.True(referenced.ContainsKey(key), $"no row {key}: {string.Join("; ", referenced.Keys)}");
            Assert.Equal(nativeRows[key], referenced[key]);
        }
        Assert.Contains("Door / Door 900×2100", referenced.Keys);
        Assert.DoesNotContain(referenced, r => r.Value.Values.Any(v => v == "0.00"));
    }

    static string Gid(string key) => Guid("ref-qto-test", key);

    /// <summary>
    /// A file from another program, in millimetres with its areas in square metres and volumes in cubic metres (as
    /// ArchiCAD writes them): a wall typed WT-1 with its base quantities, one length with a unit of its own (centimetres);
    /// a block with no quantities at all.
    /// </summary>
    string Foreign()
    {
        var f = new Step();
        string G(string key) => S(Gid(key));
        var origin = f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
        var world = f.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
        var ctx = f.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
        var mm = f.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("MILLI"), E("METRE"));
        var m2 = f.Add("IFCSIUNIT", Derived, E("AREAUNIT"), Null, E("SQUARE_METRE"));
        var m3 = f.Add("IFCSIUNIT", Derived, E("VOLUMEUNIT"), Null, E("CUBIC_METRE"));
        var cm = f.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), E("CENTI"), E("METRE"));
        var project = f.Add("IFCPROJECT", G("project"), Null, S("Foreign"), Null, Null, Null, Null, L([ctx]), f.Add("IFCUNITASSIGNMENT", L([mm, m2, m3])));
        var pl = f.Add("IFCLOCALPLACEMENT", Null, world);
        var building = f.Add("IFCBUILDING", G("building"), Null, S("Building"), Null, Null, pl, Null, Null, E("ELEMENT"), Null, Null, Null);
        var storeyPlace = f.Add("IFCLOCALPLACEMENT", pl, world);
        var storey = f.Add("IFCBUILDINGSTOREY", G("storey"), Null, S("Ground"), Null, Null, storeyPlace, Null, Null, E("ELEMENT"), "0.");
        f.Add("IFCRELAGGREGATES", G("agg1"), Null, Null, Null, project, L([building]));
        f.Add("IFCRELAGGREGATES", G("agg2"), Null, Null, Null, building, L([storey]));
        string Box(double cx, double cy, double dx, double dy, double depth)
        {
            var prof = f.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, f.Add("IFCAXIS2PLACEMENT2D", f.Add("IFCCARTESIANPOINT", $"({R(cx)},{R(cy)})"), Null), R(dx), R(dy));
            var solid = f.Add("IFCEXTRUDEDAREASOLID", prof, f.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null), f.Add("IFCDIRECTION", "(0.,0.,1.)"), R(depth));
            return f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L([f.Add("IFCSHAPEREPRESENTATION", ctx, S("Body"), S("SweptSolid"), L([solid]))]));
        }
        // A wall 5 m × 0.25 m × 3 m, in millimetres.
        var wall = f.Add("IFCWALL", G("wall"), Null, S("Wall 1"), Null, S("Basic Wall:WT-1"), f.Add("IFCLOCALPLACEMENT", storeyPlace, world), Box(2500, 125, 5000, 250, 3000), Null, E("STANDARD"));
        var qto = f.Add("IFCELEMENTQUANTITY", G("qto"), Null, S("Qto_WallBaseQuantities"), Null, Null, L([
            f.Add("IFCQUANTITYLENGTH", S("Length"), Null, Null, "5000.", Null),
            f.Add("IFCQUANTITYLENGTH", S("Width"), Null, cm, "25.", Null),
            f.Add("IFCQUANTITYLENGTH", S("Height"), Null, Null, "3000.", Null),
            f.Add("IFCQUANTITYAREA", S("GrossSideArea"), Null, Null, "15.", Null),
            f.Add("IFCQUANTITYAREA", S("NetSideArea"), Null, Null, "14.5", Null),
            f.Add("IFCQUANTITYVOLUME", S("NetVolume"), Null, Null, "3.625", Null),
        ]));
        f.Add("IFCRELDEFINESBYPROPERTIES", G("defs"), Null, Null, Null, L([wall]), qto);
        var type = f.Add("IFCWALLTYPE", G("type"), Null, S("WT-1"), Null, Null, Null, Null, Null, Null, E("STANDARD"));
        f.Add("IFCRELDEFINESBYTYPE", G("rt"), Null, Null, Null, L([wall]), type);
        // A block 1 m × 2 m × 0.5 m, and nothing said of it.
        var block = f.Add("IFCBUILDINGELEMENTPROXY", G("block"), Null, S("Block"), Null, Null, f.Add("IFCLOCALPLACEMENT", storeyPlace, world), Box(10000, 1000, 1000, 2000, 500), Null, E("ELEMENT"));
        f.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", G("contained"), Null, Null, Null, L([wall, block]), storey);
        var path = Path.Combine(_dir, "foreign.ifc");
        f.Write(path, "foreign.ifc", "ViewDefinition [ReferenceView_V1.2]");
        return path;
    }

    [Fact]
    public void A_files_quantities_are_read_in_SI_by_its_units_and_a_body_without_any_is_measured_for_its_volume()
    {
        var f = Full();
        IfcImport.Run(f.Session, Foreign(), new IfcImportOptions("reference"), out _);
        var wall = f.Model.Get(Gid("wall"))!;
        Assert.True(ReferenceLayers.IsReference(wall));
        var q = BaseSet(f, wall.Id).Quantities.ToDictionary(x => x.Name);
        Assert.Equal(5.0, q["Length"].Value, 9);
        Assert.Equal(0.25, q["Width"].Value, 9);   // its own unit, centimetres
        Assert.Equal(3.0, q["Height"].Value, 9);
        Assert.Equal(15.0, q["GrossSideArea"].Value, 9);
        Assert.Equal(14.5, q["NetSideArea"].Value, 9);
        Assert.Equal(3.625, q["NetVolume"].Value, 9);   // the file's, not the body's 3.75
        // A gross volume the file does not give is not made up from the body.
        Assert.False(q.ContainsKey("GrossVolume"));
        Assert.Equal("WT-1", ReferenceTypes.NameOf(f.Model, wall));

        // The block: no quantities in its file; its volume from its body, nothing else made up.
        var block = BaseSet(f, Gid("block"));
        Assert.Equal(ReferenceQuantities.MeasuredSet, block.Name);
        Assert.Equal(1.0, block.Quantities.Single(x => x.Name == "NetVolume").Value, 6);
        Assert.Equal(1, block.Quantities.Single(x => x.Name == "Count").Value);
        Assert.DoesNotContain(block.Quantities, x => x.Kind is "LENGTH" or "AREA");

        var rows = Takeoff(f, ["Kind", "Type", "Count", "Length (m)", "Net area (m²)", "Gross volume (m³)", "Net volume (m³)"]);
        Assert.Equal("5.00", rows["Wall / WT-1"]["Length (m)"]);
        Assert.Equal("14.50", rows["Wall / WT-1"]["Net area (m²)"]);
        Assert.Equal("3.625", rows["Wall / WT-1"]["Net volume (m³)"]);
        Assert.Equal("1.000", rows.Single(r => r.Key.EndsWith(" / —", StringComparison.Ordinal)).Value["Net volume (m³)"]);

        // Exported again: the file's quantity set as it came, nothing Lapin measured on the body added.
        var again = Path.Combine(_dir, "again.ifc");
        IfcExport.Write(f.Session, again);
        var text = File.ReadAllText(again);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "'Qto_WallBaseQuantities'"));
        Assert.DoesNotContain(ReferenceQuantities.MeasuredSet, text);
    }

    [Fact]
    public void An_article_on_a_references_type_prices_it_by_the_files_quantities()
    {
        var f = Full();
        f.Run("cost.importPrices", new JsonObject
        {
            ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("Cod articol;Denumire;U.M.;Material;Manoperă;Utilaj;Transport\nBET01;Beton;mc;400;80;10;10\nZID02;Zidărie;mp;50;30;0;5\n")),
            ["name"] = "Prețuri.csv",
        });
        IfcImport.Run(f.Session, Foreign(), new IfcImportOptions("reference"), out _);
        var wall = f.Model.Get(Gid("wall"))!;
        var type = ReferenceTypes.TypeOf(f.Model, wall)!;
        Assert.Equal(IfcSource.RecordClass, type.Class);
        Assert.Equal("WT-1", type.Name);
        f.Run("cost.setArticles", new JsonObject { ["type"] = type.Id, ["articles"] = new JsonArray(new JsonObject { ["code"] = "BET01" }, new JsonObject { ["code"] = "ZID02" }) });
        f.Run("cost.createEstimate", new JsonObject { ["name"] = "Deviz" });
        var est = (JsonObject)f.Session.Query("cost.estimate", new JsonObject())!;
        var lines = est["chapters"]!.AsArray().SelectMany(c => c!["lines"]!.AsArray()).ToDictionary(l => (string)l!["code"]!, l => (double)l!["qty"]!);
        Assert.Equal(3.625, lines["BET01"], 6);
        Assert.Equal(14.5, lines["ZID02"], 6);
        // The mapping lists the type by its file's name.
        var mapping = (JsonObject)f.Session.Query("cost.mapping", new JsonObject())!;
        Assert.Contains(mapping["types"]!.AsArray(), t => (string?)t!["name"] == "WT-1");
    }

    [Fact]
    public void A_Fragments_item_measures_by_the_quantity_sets_its_file_carries()
    {
        var a = House();
        var bytes = MeshExport.Write(a.Session, new MeshExportOptions("frag", Origin: "internal")).Bytes;
        var b = Full();
        FragImport.Run(b.Session, bytes, "house.frag", out _);
        AssertSameMeasures(a, b);
    }

    [Fact]
    public void A_BIM_Open_Schema_item_measures_by_the_quantity_sets_its_file_carries()
    {
        var a = House();
        var (bytes, _) = MeshExport.Write(a.Session, new MeshExportOptions("bos", Origin: "internal"));
        var b = Full();
        BosImport.Run(b.Session, new BosView(BosData.Read(bytes)), "house.bos", out _);
        AssertSameMeasures(a, b);
    }

    /// <summary>Each wall, column, beam and floor of <paramref name="b"/> (imported from <paramref name="a"/>'s file) has the base quantities <paramref name="a"/> measured.</summary>
    static void AssertSameMeasures(Fixture a, Fixture b)
    {
        var count = 0;
        foreach (var cls in new[] { IfcClass.Wall, IfcClass.Column, IfcClass.Beam, IfcClass.Slab })
            foreach (var native in a.Model.OfClass(cls))
            {
                var e = b.Model.Get(native.Id);
                Assert.NotNull(e);
                Assert.True(ReferenceQuantities.Applies(e!), $"{e!.Class} is not measured as a reference");
                var file = BaseSet(a, native.Id);
                var got = b.Session.Quantities(e.Id).FirstOrDefault(s => s.Name == file.Name);
                Assert.True(got != null, $"{cls}: no {file.Name} in {string.Join(", ", b.Session.Quantities(e.Id).Select(s => s.Name))}");
                foreach (var q in file.Quantities.Where(q => q.Kind is "LENGTH" or "AREA" or "VOLUME")) Near(q, got!.Quantities, cls);
                count++;
            }
        Assert.True(count >= 5, $"{count} elements checked");
        Assert.Equal(Takeoff(a, Measured)["Column / C 30x30"]["Length (m)"], Takeoff(b, Measured).Single(r => r.Key.StartsWith("Column", StringComparison.Ordinal)).Value["Length (m)"]);
    }
}
