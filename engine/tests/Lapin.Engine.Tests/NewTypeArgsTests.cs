using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Issues #146 and #147 (a CYPECAD MEP user's): every type dialog makes its type with its parameters and its mark, as Wall types
/// has since issue #32. Floor, ceiling, stair, column, beam, strip and pad types take <c>params</c> and <c>mark</c> in the command
/// that makes them: one undo step, its elements have them from the start, a wrong one makes nothing.
/// </summary>
public sealed class NewTypeArgsTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule());

    static JsonObject With(JsonObject a, string? pset, string name, JsonNode? value, string? kind = null, bool instance = false)
    {
        var p = new JsonObject { ["name"] = name, ["value"] = value };
        if (pset != null) p["pset"] = pset;
        if (kind != null) p["kind"] = kind;
        if (instance) p["instance"] = true;
        a["mark"] = "T1";
        a["params"] = new JsonArray(p, new JsonObject { ["name"] = "Renovation", ["kind"] = "choice", ["options"] = new JsonArray("new", "existing"), ["value"] = "new", ["instance"] = true });
        return a;
    }

    public static TheoryData<string, string, string, string> Commands => new()
    {
        { "architecture.createSlabType", IfcClass.SlabType, "Pset_SlabCommon", """{"name":"F 20","layers":[{"material":"Concrete","thickness":0.2,"core":true}]}""" },
        { "architecture.createCeilingType", Ceilings.TypeClass, "", """{"spec":{"name":"Gypsum"}}""" },
        { "structure.createColumnType", IfcClass.ColumnType, "Pset_ColumnCommon", """{"name":"C 30","section":{"kind":"rectangle","width":0.3,"depth":0.3}}""" },
        { "structure.createBeamType", IfcClass.BeamType, "Pset_BeamCommon", """{"name":"B 25x50","section":{"kind":"rectangle","width":0.25,"depth":0.5}}""" },
        { "structure.createPadType", IfcClass.FootingType, "Pset_FootingCommon", """{"name":"PF"}""" },
        { "structure.createStripType", IfcClass.FootingType, "Pset_FootingCommon", """{"name":"SF","layers":[{"material":"Reinforced concrete","thickness":0.3,"core":true}]}""" },
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void A_type_is_made_with_its_mark_and_parameters_in_one_step(string command, string cls, string pset, string args)
    {
        var f = F();
        var a = (JsonObject)JsonNode.Parse(args)!;
        // A standard property where the class has a standard set; else one of the user's.
        With(a, pset.Length > 0 ? pset : null, pset.Length > 0 ? "Status" : "Fire rating", "New", pset.Length > 0 ? null : "text");
        var before = f.Model.OfClass(cls).Count();
        var id = (string)f.Run(command, a)!["id"]!;
        var type = f.Model.Get(id)!;
        Assert.Equal(cls, type.Class);
        Assert.Equal("T1", type.Str("mark"));
        var ps = (type.Attrs["params"] as JsonArray)!.OfType<JsonObject>().ToList();
        Assert.Equal(2, ps.Count);
        Assert.True((bool)ps[1]["instance"]!);
        // One undo step takes the type, its mark and its parameters away.
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Equal(before, f.Model.OfClass(cls).Count());
    }

    [Fact]
    public void A_stair_type_takes_them_too()
    {
        var f = F();
        var a = With(new JsonObject { ["spec"] = StairSpec.Default().ToJson() }, null, "Fire rating", "R60", "text");
        var id = (string)f.Run("architecture.createStairType", a)!["id"]!;
        Assert.Equal("T1", f.Model.Get(id)!.Str("mark"));
        Assert.Equal(2, (f.Model.Get(id)!.Attrs["params"] as JsonArray)!.Count);
    }

    [Fact]
    public void A_beam_drawn_with_it_has_the_mark_and_its_instance_default_from_the_start()
    {
        var f = F();
        var a = With(new JsonObject { ["name"] = "B 25x50", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.25, ["depth"] = 0.5 } }, null, "Fire rating", "R90", "text");
        var type = (string)f.Run("structure.createBeamType", a)!["id"]!;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0, ["typeId"] = type })!["id"]!;
        Assert.Equal("T1", TagText.Mark(f.Model, f.Model.Get(beam)!));
        var own = Assert.Single((f.Model.Get(beam)!.Attrs["params"] as JsonArray)!.OfType<JsonObject>());
        Assert.Equal("Renovation", (string?)own["name"]);
    }

    [Fact]
    public void Without_them_a_type_is_made_as_before_and_a_wrong_one_makes_nothing()
    {
        var f = F();
        var id = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = "B", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.25, ["depth"] = 0.5 } })!["id"]!;
        Assert.Null(f.Model.Get(id)!.Attrs["mark"]);
        Assert.Null(f.Model.Get(id)!.Attrs["params"]);
        // An empty mark is none.
        var blank = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = "C", ["mark"] = "  ", ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 } })!["id"]!;
        Assert.Null(f.Model.Get(blank)!.Attrs["mark"]);
        var beams = f.Model.OfClass(IfcClass.BeamType).Count();
        Assert.Throws<CommandException>(() => f.Run("structure.createBeamType", new JsonObject { ["name"] = "Wrong", ["params"] = new JsonArray(new JsonObject { ["pset"] = "Pset_BeamCommon", ["name"] = "IsExternal", ["value"] = "maybe" }) }));
        Assert.Throws<CommandException>(() => f.Run("structure.createBeamType", new JsonObject { ["name"] = "Wrong", ["mark"] = new string('x', 81) }));
        Assert.Throws<CommandException>(() => f.Run("architecture.createSlabType", new JsonObject { ["layers"] = new JsonArray(new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.2, ["core"] = true }), ["mark"] = 5 }));
        Assert.Equal(beams, f.Model.OfClass(IfcClass.BeamType).Count());
    }
}
