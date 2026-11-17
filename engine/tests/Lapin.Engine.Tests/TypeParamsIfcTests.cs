using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Type parameters and the IFC export (a CYPECAD MEP user's issues #29–#32, decision 667): the type's properties also on each
/// occurrence when asked (#30, the occurrence's own value winning), a wall type made with its parameters in one step (#32), what an
/// unticked yes/no writes (#31), and a wall with openings keeping a body of its own beside its parts when they are grouped (#29).
/// </summary>
public sealed class TypeParamsIfcTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-type-params-ifc").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new ArchitectureModule());

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
    static StepEntity Product(StepFile f, string cls, string gid) => f.OfType(cls).Single(e => (string?)e[0] == gid);

    /// <summary>A property's value as the file writes it ("IFCBOOLEAN(.T.)"), or null when the set does not have it.</summary>
    static string? Value(StepFile f, StepEntity set, string name)
    {
        var p = Items(set[4]).Select(x => f.Get(x)!).FirstOrDefault(x => (string?)x[0] == name);
        return p?[2] is StepTyped t ? $"{t.Type}({(t.Value switch { StepEnum e => $".{e.Value}.", string s => $"'{s}'", var v => v })})" : null;
    }

    static string? ValueIn(StepFile f, List<StepEntity> sets, string set, string name) =>
        sets.FirstOrDefault(s => (string?)s[2] == set) is { } found ? Value(f, found, name) : null;

    // ─── #30: the type's properties on each occurrence ──────────────────────

    [Fact]
    public void Asked_the_types_properties_go_on_each_occurrence_too_the_occurrences_own_value_winning()
    {
        var f = F();
        var type = f.Type("Exterior 30", ("Brick", 0.3, true));
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "Pset_WallCommon", ["name"] = "IsExternal", ["value"] = true });
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "EI90" });
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "Acme_Checks", ["name"] = "Checked by", ["kind"] = "text", ["value"] = "IC" });
        var plain = f.Wall(0, 0, 5, 0, type);
        var own = f.Wall(0, 4, 5, 4, type);
        f.Run("element.setParam", new JsonObject { ["id"] = own, ["pset"] = "Pset_WallCommon", ["name"] = "IsExternal", ["value"] = false });
        f.Run("element.setParam", new JsonObject { ["id"] = own, ["name"] = "Fire rating", ["kind"] = "text", ["value"] = "EI30" });

        // Off (the default): the export is as it was, the type's properties on the type only.
        var before = StepFile.Read(Export(f, "off.ifc"));
        var plainOff = SetsOf(before, Product(before, "IFCWALL", plain));
        Assert.Null(ValueIn(before, plainOff, "Pset_WallCommon", "IsExternal"));
        Assert.DoesNotContain(plainOff, s => (string?)s[2] == IfcExport.ParamsSetName);
        Assert.DoesNotContain(plainOff, s => (string?)s[2] == "Acme_Checks");
        Assert.Equal("IFCBOOLEAN(.T.)", ValueIn(before, TypeSetsOf(before, Assert.Single(before.OfType("IFCWALLTYPE"))), "Pset_WallCommon", "IsExternal"));
        Assert.Equal(File.ReadAllLines(Path.Combine(_dir, "off.ifc")).Where(l => !l.StartsWith("FILE_NAME", StringComparison.Ordinal)),
            File.ReadAllLines(Export(f, "default.ifc", new IfcExportOptions(TypePropertiesOnOccurrences: false))).Where(l => !l.StartsWith("FILE_NAME", StringComparison.Ordinal)));

        // On: every set of the type's on the wall too, in the sets of the same names; the wall's own value over the type's.
        var on = StepFile.Read(Export(f, "on.ifc", new IfcExportOptions(TypePropertiesOnOccurrences: true)));
        var plainOn = SetsOf(on, Product(on, "IFCWALL", plain));
        Assert.Equal("IFCBOOLEAN(.T.)", ValueIn(on, plainOn, "Pset_WallCommon", "IsExternal"));
        Assert.Equal("IFCTEXT('EI90')", ValueIn(on, plainOn, IfcExport.ParamsSetName, "Fire rating"));
        Assert.Equal("IFCTEXT('IC')", ValueIn(on, plainOn, "Acme_Checks", "Checked by"));
        // Lapin's own value of the common set stays beside it, once.
        Assert.NotNull(ValueIn(on, plainOn, "Pset_WallCommon", "Reference"));
        Assert.Single(plainOn, s => (string?)s[2] == "Pset_WallCommon");
        var ownOn = SetsOf(on, Product(on, "IFCWALL", own));
        Assert.Equal("IFCBOOLEAN(.F.)", ValueIn(on, ownOn, "Pset_WallCommon", "IsExternal"));
        Assert.Equal("IFCTEXT('EI30')", ValueIn(on, ownOn, IfcExport.ParamsSetName, "Fire rating"));
        Assert.Single(Items(ownOn.Single(s => (string?)s[2] == "Pset_WallCommon")[4]).Select(x => (string?)on.Get(x)![0]), n => n == "IsExternal");
        // The type keeps its own sets as before.
        Assert.Equal("IFCBOOLEAN(.T.)", ValueIn(on, TypeSetsOf(on, Assert.Single(on.OfType("IFCWALLTYPE"))), "Pset_WallCommon", "IsExternal"));
    }

    [Fact]
    public void The_export_command_takes_the_option()
    {
        var f = F();
        var type = f.Type("Exterior 30", ("Brick", 0.3, true));
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "Pset_WallCommon", ["name"] = "IsExternal", ["value"] = true });
        var wall = f.Wall(0, 0, 5, 0, type);
        var path = Path.Combine(_dir, "command.ifc");
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = "project.exportIfc", ["params"] = new JsonObject { ["path"] = path, ["typePropertiesOnOccurrences"] = true } }.ToJsonString(), Caller.Local);
        Assert.Null(JsonNode.Parse(BinaryFrame.HeaderText(reply))!["error"]);
        var file = StepFile.Read(path);
        Assert.Equal("IFCBOOLEAN(.T.)", ValueIn(file, SetsOf(file, Product(file, "IFCWALL", wall)), "Pset_WallCommon", "IsExternal"));
    }

    // ─── #32: a wall type made with its parameters ──────────────────────────

    [Fact]
    public void A_wall_type_is_made_with_its_parameters_in_one_step_and_its_walls_have_them_from_the_start()
    {
        var f = F();
        var type = (string)f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Exterior 30",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3, ["core"] = true }),
            ["params"] = new JsonArray(
                new JsonObject { ["pset"] = "Pset_WallCommon", ["name"] = "IsExternal", ["value"] = true },
                new JsonObject { ["name"] = "Renovation", ["kind"] = "choice", ["options"] = new JsonArray("new", "existing"), ["value"] = "new", ["instance"] = true }),
        })!["id"]!;
        var ps = (f.Model.Get(type)!.Attrs["params"] as JsonArray)!.OfType<JsonObject>().ToList();
        Assert.Equal(2, ps.Count);
        // A standard property takes its kind from the standard set.
        Assert.Equal("bool", (string?)ps[0]["kind"]);
        Assert.True((bool)ps[1]["instance"]!);

        // A wall drawn with it: the type's IsExternal is its type's, the instance default its own copy.
        var wall = f.Wall(0, 0, 5, 0, type);
        var own = Assert.Single((f.Model.Get(wall)!.Attrs["params"] as JsonArray)!.OfType<JsonObject>());
        Assert.Equal("Renovation", (string?)own["name"]);
        var file = StepFile.Read(Export(f, "made.ifc", new IfcExportOptions(TypePropertiesOnOccurrences: true)));
        Assert.Equal("IFCBOOLEAN(.T.)", ValueIn(file, SetsOf(file, Product(file, "IFCWALL", wall)), "Pset_WallCommon", "IsExternal"));

        // One undo step: the wall goes, then the type with its parameters.
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(f.Model.Get(type));
    }

    [Fact]
    public void A_wall_types_parameters_are_checked_as_one_parameter_is()
    {
        var f = F();
        var before = f.Model.OfClass(IfcClass.WallType).Count();
        Assert.Throws<CommandException>(() => f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "Wrong",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Brick", ["thickness"] = 0.3, ["core"] = true }),
            ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_WallCommon", ["name"] = "IsExternal", ["value"] = "maybe" }),
        }));
        Assert.Equal(before, f.Model.OfClass(IfcClass.WallType).Count());
    }

    // ─── #31: what an unticked yes/no writes ───────────────────────────────

    [Fact]
    public void An_unticked_yes_or_no_writes_false_on_the_type_and_on_the_element_and_one_without_a_value_writes_nothing()
    {
        var f = F();
        var type = f.Type("Partition 12", ("Brick", 0.12, true));
        // As the panel adds them: a yes/no starts unticked, false.
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "Pset_WallCommon", ["name"] = "IsExternal", ["value"] = false });
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["name"] = "Checked", ["kind"] = "bool", ["value"] = false });
        // An instance default unticked: not on the type, on each wall as its own.
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["name"] = "Demolish", ["kind"] = "bool", ["value"] = false, ["instance"] = true });
        var wall = f.Wall(0, 0, 5, 0, type);
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "Pset_WallCommon", ["name"] = "LoadBearing", ["value"] = false });
        // A yes/no without a value (a script's, or a formula that gives none): nothing.
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["name"] = "Unknown", ["kind"] = "bool", ["value"] = null });

        var file = StepFile.Read(Export(f, "bools.ifc"));
        var typeSets = TypeSetsOf(file, Assert.Single(file.OfType("IFCWALLTYPE")));
        Assert.Equal("IFCBOOLEAN(.F.)", ValueIn(file, typeSets, "Pset_WallCommon", "IsExternal"));
        Assert.Equal("IFCBOOLEAN(.F.)", ValueIn(file, typeSets, IfcExport.ParamsSetName, "Checked"));
        Assert.Null(ValueIn(file, typeSets, IfcExport.ParamsSetName, "Demolish"));
        var wallSets = SetsOf(file, Product(file, "IFCWALL", wall));
        Assert.Equal("IFCBOOLEAN(.F.)", ValueIn(file, wallSets, "Pset_WallCommon", "LoadBearing"));
        Assert.Equal("IFCBOOLEAN(.F.)", ValueIn(file, wallSets, IfcExport.ParamsSetName, "Demolish"));
        Assert.Null(ValueIn(file, wallSets, IfcExport.ParamsSetName, "Unknown"));
        // The type's value is not on the wall unless asked (#30).
        Assert.Null(ValueIn(file, wallSets, "Pset_WallCommon", "IsExternal"));
    }

    // ─── #29: a wall of grouped parts keeps a body ─────────────────────────

    [Fact]
    public void A_wall_with_an_opening_keeps_its_own_body_beside_its_grouped_parts()
    {
        var f = F();
        var type = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        var opened = f.Wall(0, 0, 6, 0, type);
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = opened, ["along"] = 3 });
        var plain = f.Wall(0, 5, 6, 5, type);

        var file = StepFile.Read(Export(f, "grouped.ifc"));
        int Faces(StepEntity rep) => Items(rep[3]).Sum(b => Items(file.Get(file.Get(b)![0])![0]).Count);
        StepEntity? Body(StepEntity product) => product[6] == null ? null
            : Items(file.Get(product[6])![2]).Select(r => file.Get(r)!).FirstOrDefault(r => (string?)r[1] == "Body");

        var w = Product(file, "IFCWALL", opened);
        var parts = Items(file.OfType("IFCRELAGGREGATES").Single(r => Equals(r[4], Ref(w)))[5]).Select(p => file.Get(p)!).ToList();
        Assert.True(parts.Count >= 4, $"three layers and a lintel: {parts.Count}");
        // Its own body: every part's faces, the same items (no geometry written twice).
        var body = Body(w);
        Assert.NotNull(body);
        var partItems = parts.Select(Body).OfType<StepEntity>().SelectMany(r => Items(r[3])).ToHashSet();
        Assert.Equal(partItems.Count, Items(body![3]).Count);
        Assert.All(Items(body[3]), i => Assert.Contains(i, partItems));
        Assert.True(Faces(body) > 0);
        // The wall without an opening: one body, its layers, as before.
        var p = Product(file, "IFCWALL", plain);
        Assert.NotNull(Body(p));
        Assert.DoesNotContain(file.OfType("IFCRELAGGREGATES"), r => Equals(r[4], Ref(p)));

        // Read back by Lapin: the walls rebuilt, no reference beside them, nothing reverted by the gate.
        var back = F();
        var r = IfcImport.Run(back.Session, Path.Combine(_dir, "grouped.ifc"), null, out _);
        Assert.Equal(0, r.References);
        Assert.Equal(2, back.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(0, r.Fidelity?.Failed ?? 0);
        Assert.Equal(0, r.Fidelity?.Reverted ?? 0);

        // Read back as references: the parts carry the body, the wall's own is not added to them (nothing twice).
        var refs = F();
        IfcImport.Run(refs.Session, Path.Combine(_dir, "grouped.ifc"), new IfcImportOptions(Mode: "reference"), out _);
        Assert.Null(refs.Model.Get(opened));
        Assert.NotNull(refs.Model.Get(plain));
        Assert.Equal(parts.Count, refs.Model.Entities.Count(e => parts.Any(x => (string?)x[0] == e.Id)));
    }
}
