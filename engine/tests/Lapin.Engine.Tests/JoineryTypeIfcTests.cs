using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Issues #141 and #150 (a CYPECAD MEP tester): a door's and a window's type parameters in the IFC, on the type and, when asked,
/// on each occurrence; a mirrored door keeping its type when asked (the hand in OperationType); lintels as beams of their own.
/// </summary>
public sealed class JoineryTypeIfcTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-joinery-type-ifc").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new ArchitectureModule(), new StructureModule());

    string Export(Fixture f, string name, IfcExportOptions? options = null)
    {
        var path = Path.Combine(_dir, name);
        IfcExport.Write(f.Session, path, options);
        return path;
    }

    static List<object?> Items(object? o) => o as List<object?> ?? [];
    static StepRef Ref(StepEntity e) => new(e.Id);
    static List<StepEntity> SetsOf(StepFile f, StepEntity e) =>
        f.OfType("IFCRELDEFINESBYPROPERTIES").Where(r => Items(r[4]).Contains(Ref(e))).Select(r => f.Get(r[5])!).ToList();
    static List<StepEntity> TypeSetsOf(StepFile f, StepEntity type) => Items(type[5]).Select(r => f.Get(r)!).ToList();
    static StepEntity Product(StepFile f, string cls, string tag) => f.OfType(cls).Single(e => (string?)e[7] == tag);
    static StepEntity TypeOf(StepFile f, StepEntity e) => f.Get(f.OfType("IFCRELDEFINESBYTYPE").Single(r => Items(r[4]).Contains(Ref(e)))[5])!;

    static string? Value(StepFile f, StepEntity set, string name)
    {
        var p = Items(set[4]).Select(x => f.Get(x)!).FirstOrDefault(x => (string?)x[0] == name);
        return p?[2] is StepTyped t ? $"{t.Type}({(t.Value switch { StepEnum e => $".{e.Value}.", string s => $"'{s}'", var v => v })})" : null;
    }

    static string? ValueIn(StepFile f, List<StepEntity> sets, string set, string name) =>
        sets.Where(s => (string?)s[2] == set).Select(s => Value(f, s, name)).FirstOrDefault(v => v != null);

    /// <summary>A wall with a door, a window and a door flipped by hand, the two types given the tester's code.</summary>
    static (Fixture F, string Door, string Window, string Mirrored) House()
    {
        var f = F();
        var wall = f.Wall(0, 0, 8, 0);
        string Add(string what, double along) => (string)f.Run($"architecture.add{what}", new JsonObject { ["wallId"] = wall, ["along"] = along })!["id"]!;
        var door = Add("Door", 1);
        var window = Add("Window", 3);
        var mirrored = Add("Door", 5);
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(mirrored), ["axis"] = "x" });
        string TypeId(string id) => f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Id;
        Assert.Equal(TypeId(door), TypeId(mirrored));
        f.Run("element.setParam", new JsonObject { ["id"] = TypeId(door), ["pset"] = "Estudio_Catalogo", ["name"] = "CodigoTipo", ["kind"] = "text", ["value"] = "EPUE-01" });
        f.Run("element.setParam", new JsonObject { ["id"] = TypeId(window), ["pset"] = "Estudio_Catalogo", ["name"] = "CodigoTipo", ["kind"] = "text", ["value"] = "ECAR-01" });
        return (f, door, window, mirrored);
    }

    [Fact]
    public void Door_and_window_types_write_their_parameters_and_each_occurrence_has_them_when_asked()
    {
        var (f, door, window, mirrored) = House();
        foreach (var on in new[] { false, true })
        {
            var file = StepFile.Read(Export(f, $"types-{on}.ifc", new IfcExportOptions(TypePropertiesOnOccurrences: on)));
            var d = Product(file, "IFCDOOR", door);
            var w = Product(file, "IFCWINDOW", window);
            var h = Product(file, "IFCDOOR", mirrored);
            Assert.Equal("IFCDOORTYPE", TypeOf(file, d).Type);
            Assert.Equal("IFCWINDOWTYPE", TypeOf(file, w).Type);
            Assert.Equal("IFCTEXT('EPUE-01')", ValueIn(file, TypeSetsOf(file, TypeOf(file, d)), "Estudio_Catalogo", "CodigoTipo"));
            Assert.Equal("IFCTEXT('ECAR-01')", ValueIn(file, TypeSetsOf(file, TypeOf(file, w)), "Estudio_Catalogo", "CodigoTipo"));
            // The mirrored door's type (the default) has them too.
            Assert.Equal("IFCTEXT('EPUE-01')", ValueIn(file, TypeSetsOf(file, TypeOf(file, h)), "Estudio_Catalogo", "CodigoTipo"));
            foreach (var e in new[] { d, w, h })
                Assert.Equal(on ? (e == w ? "IFCTEXT('ECAR-01')" : "IFCTEXT('EPUE-01')") : null, ValueIn(file, SetsOf(file, e), "Estudio_Catalogo", "CodigoTipo"));
        }
    }

    [Fact]
    public void Changing_a_door_or_window_type_in_its_configurator_keeps_its_parameters()
    {
        // The tester's project lost them (#141): the configurator's save replaced the type's attributes with its configuration.
        var (f, door, window, _) = House();
        foreach (var id in new[] { door, window })
        {
            var type = f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!;
            var spec = Openings.SpecOf(f.Model, id)!;
            f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = type.Id, ["spec"] = (spec with { Name = type.Name + " b", Mark = "Z9" }).ToJson() });
            var after = f.Model.Get(type.Id)!;
            Assert.Equal(type.Name + " b", after.Name);
            Assert.Equal("Z9", after.Str("mark"));
            Assert.Contains((after.Attrs["params"] as JsonArray)!.OfType<JsonObject>(), p => (string?)p["name"] == "CodigoTipo");
        }
        var file = StepFile.Read(Export(f, "changed.ifc"));
        Assert.Equal("IFCTEXT('ECAR-01')", ValueIn(file, TypeSetsOf(file, TypeOf(file, Product(file, "IFCWINDOW", window))), "Estudio_Catalogo", "CodigoTipo"));
        Assert.Equal("IFCTEXT('EPUE-01')", ValueIn(file, TypeSetsOf(file, TypeOf(file, Product(file, "IFCDOOR", door))), "Estudio_Catalogo", "CodigoTipo"));
    }

    [Fact]
    public void Asked_a_mirrored_door_keeps_its_type_the_hand_in_its_own_operation_and_reads_back_flipped()
    {
        var (f, door, _, mirrored) = House();
        static string? Enum(object? v) => (v as StepEnum)?.Value;

        // Off (the default, decision 530): the mirror image's own type, as before.
        var before = StepFile.Read(Export(f, "mirror-off.ifc"));
        Assert.Equal(2, before.OfType("IFCDOORTYPE").Count());
        Assert.EndsWith("(mirrored)", (string?)TypeOf(before, Product(before, "IFCDOOR", mirrored))[2]);
        Assert.Equal(File.ReadAllLines(Path.Combine(_dir, "mirror-off.ifc")).Where(l => !l.StartsWith("FILE_NAME", StringComparison.Ordinal)),
            File.ReadAllLines(Export(f, "mirror-default.ifc", new IfcExportOptions(MirroredSameType: false))).Where(l => !l.StartsWith("FILE_NAME", StringComparison.Ordinal)));

        // On: one door type, the left hand's; the flipped door says its right hand itself.
        var path = Export(f, "mirror-on.ifc", new IfcExportOptions(MirroredSameType: true));
        var file = StepFile.Read(path);
        var type = Assert.Single(file.OfType("IFCDOORTYPE"));
        Assert.DoesNotContain("mirrored", (string?)type[2]);
        Assert.Equal("SINGLE_SWING_LEFT", Enum(type[10]));
        var d = Product(file, "IFCDOOR", door);
        var h = Product(file, "IFCDOOR", mirrored);
        Assert.Equal(type.Id, TypeOf(file, d).Id);
        Assert.Equal(type.Id, TypeOf(file, h).Id);
        Assert.Equal("SINGLE_SWING_LEFT", Enum(d[11]));
        Assert.Equal("SINGLE_SWING_RIGHT", Enum(h[11]));
        Assert.Equal("IFCTEXT('EPUE-01')", ValueIn(file, TypeSetsOf(file, type), "Estudio_Catalogo", "CodigoTipo"));

        // Lapin reads either back the same: one type, the door flipped.
        foreach (var p in new[] { path, Path.Combine(_dir, "mirror-off.ifc") })
        {
            var g = F();
            IfcImport.Run(g.Session, p, null, out _);
            var t = Assert.Single(g.Model.OfClass(IfcClass.DoorType));
            Assert.Equal(t.Id, g.Model.RelatingOf(mirrored, IfcClass.RelDefinesByType)!.Id);
            Assert.True(g.Model.Get(mirrored)!.Bool("flipHand"));
            Assert.False(g.Model.Get(door)!.Bool("flipHand"));
        }
    }

    // ─── #150: lintels with their own type and quantities ──────────────────

    /// <summary>A layered wall with a window, so a precast lintel (the default rule) over it.</summary>
    static (Fixture F, string Wall) Lintelled()
    {
        var f = F();
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var wall = f.Wall(0, 0, 6, 0, type);
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 });
        Assert.Contains(f.Session.Geometry(wall)!.Parts, p => p.Key?.StartsWith("lintel:", StringComparison.Ordinal) == true);
        return (f, wall);
    }

    static List<StepEntity> Lintels(StepFile file) => file.OfType("IFCBEAM").Where(b => (b[8] as StepEnum)?.Value == "LINTEL").ToList();
    static StepEntity? QtoOf(StepFile file, StepEntity e, string name) =>
        file.OfType("IFCRELDEFINESBYPROPERTIES").Where(r => Items(r[4]).Contains(Ref(e))).Select(r => file.Get(r[5])!).FirstOrDefault(s => s.Type == "IFCELEMENTQUANTITY" && (string?)s[2] == name);
    static double Q(StepFile file, StepEntity qto, string name) => (double)Items(qto[5]).Select(x => file.Get(x)!).Single(x => (string?)x[0] == name)[3]!;

    [Fact]
    public void A_grouped_walls_lintel_carries_its_own_quantities_not_the_wall()
    {
        var (f, wall) = Lintelled();
        var set = f.Session.Quantities(wall).Single(s => s.Name == "Qto_BeamBaseQuantities");
        var file = StepFile.Read(Export(f, "grouped.ifc"));
        var lintel = Assert.Single(Lintels(file));
        var qto = QtoOf(file, lintel, "Qto_BeamBaseQuantities");
        Assert.NotNull(qto);
        Assert.Equal(set.Quantities.Single(q => q.Name == "NetVolume").Value, Q(file, qto!, "NetVolume"), 6);
        Assert.Equal(set.Quantities.Single(q => q.Name == "Length").Value, Q(file, qto!, "Length"), 6);
        Assert.Null(QtoOf(file, file.OfType("IFCWALL").Single(w => (string?)w[7] == wall), "Qto_BeamBaseQuantities"));
        // Untyped unless asked, as before.
        Assert.DoesNotContain(file.OfType("IFCRELDEFINESBYTYPE"), r => Items(r[4]).Contains(Ref(lintel)));
    }

    [Fact]
    public void Asked_lintels_are_beams_of_their_own_typed_and_measured_in_either_detail()
    {
        var (f, wall) = Lintelled();
        var set = f.Session.Quantities(wall).Single(s => s.Name == "Qto_BeamBaseQuantities");

        // One body each, off (the default): the lintel in the wall's body, its quantities on the wall, as before.
        var simple = StepFile.Read(Export(f, "simple-off.ifc", new IfcExportOptions(Detail: "simple")));
        Assert.Empty(Lintels(simple));
        Assert.NotNull(QtoOf(simple, simple.OfType("IFCWALL").Single(w => (string?)w[7] == wall), "Qto_BeamBaseQuantities"));
        Assert.Equal(File.ReadAllLines(Path.Combine(_dir, "simple-off.ifc")).Where(l => !l.StartsWith("FILE_NAME", StringComparison.Ordinal)),
            File.ReadAllLines(Export(f, "simple-default.ifc", new IfcExportOptions(Detail: "simple", LintelBeams: false))).Where(l => !l.StartsWith("FILE_NAME", StringComparison.Ordinal)));

        foreach (var detail in new[] { "simple", "group" })
        {
            var path = Export(f, $"lintels-{detail}.ifc", new IfcExportOptions(Detail: detail, LintelBeams: true));
            var file = StepFile.Read(path);
            var w = file.OfType("IFCWALL").Single(x => (string?)x[7] == wall);
            var lintel = Assert.Single(Lintels(file));
            // Under the wall, as its part.
            Assert.Contains(file.OfType("IFCRELAGGREGATES"), r => Equals(r[4], Ref(w)) && Items(r[5]).Contains(Ref(lintel)));
            // Its own type: a generic IfcBeamType LINTEL by its name and material.
            var rel = file.OfType("IFCRELDEFINESBYTYPE").Single(r => Items(r[4]).Contains(Ref(lintel)));
            var type = file.Get(rel[5])!;
            Assert.Equal("IFCBEAMTYPE", type.Type);
            Assert.Equal("LINTEL", (type[9] as StepEnum)?.Value);
            Assert.StartsWith("Lintel (precast)", (string?)type[2]);
            // Its own quantities, the wall's set no longer.
            var qto = QtoOf(file, lintel, "Qto_BeamBaseQuantities");
            Assert.NotNull(qto);
            Assert.Equal(set.Quantities.Single(q => q.Name == "NetVolume").Value, Q(file, qto!, "NetVolume"), 6);
            Assert.Null(QtoOf(file, w, "Qto_BeamBaseQuantities"));
            // The wall's body in one body each: the rest, the lintel's faces not twice.
            if (detail == "simple") Assert.NotNull(w[6]);

            // Read back by Lapin: the wall rebuilt with its lintel, no beam beside it, no beam type made.
            var g = F();
            var r = IfcImport.Run(g.Session, path, null, out _);
            Assert.Single(g.Model.OfClass(IfcClass.Wall));
            Assert.Empty(g.Model.OfClass(IfcClass.Beam));
            Assert.Empty(g.Model.OfClass(IfcClass.BeamType));
            Assert.Equal(0, r.Fidelity?.Reverted ?? 0);
        }
    }
}
