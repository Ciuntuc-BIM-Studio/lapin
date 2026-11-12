using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A type's instance defaults (a tester's report: fire rating on the type, new / existing / demolish on each element): a type's
/// parameter marked <c>instance</c> is copied onto an element given the type (placed with it, its type changed) in the same undo
/// step, the element may change it, the IFC export leaves it out of the type's sets; elements made before are filled only when
/// asked. And the export's parameter types and the phases' Status.
/// </summary>
public sealed class TypeDefaultsTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-type-defaults").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture F() => new(new ArchitectureModule(), new PhasesModule());

    static List<JsonObject> Params(Fixture f, string id) => (f.Model.Get(id)!.Attrs["params"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    static string Typed(Fixture f)
    {
        var type = f.Type("WT Fire", ("Plaster", 0.015, false), ("Brick", 0.25, true));
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "Pset_WallCommon", ["name"] = "FireRating", ["value"] = "EI 60" });
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "RAVA_Wall", ["name"] = "Renovation", ["kind"] = "choice", ["options"] = new JsonArray("new", "existing", "demolish"), ["value"] = "new", ["instance"] = true });
        return type;
    }

    [Fact]
    public void A_wall_placed_with_a_type_takes_its_instance_defaults_in_the_same_undo_step_and_may_change_them()
    {
        var f = F();
        var type = Typed(f);
        var p = Params(f, type).Single(x => (string?)x["name"] == "Renovation");
        Assert.True((bool)p["instance"]!);
        Assert.False(Params(f, type).Single(x => (string?)x["name"] == "FireRating").ContainsKey("instance"));

        var wall = f.Wall(0, 0, 5, 0, type);
        var own = Assert.Single(Params(f, wall));
        Assert.Equal("Renovation", (string?)own["name"]);
        Assert.Equal("RAVA_Wall", (string?)own["pset"]);
        Assert.Equal("new", (string?)own["value"]);
        Assert.False(own.ContainsKey("instance"));

        // The element's own value; the type's default stays.
        f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "RAVA_Wall", ["name"] = "Renovation", ["value"] = "existing" });
        Assert.Equal("existing", (string?)Params(f, wall).Single()["value"]);
        Assert.Equal("new", (string?)Params(f, type).Single(x => (string?)x["name"] == "Renovation")["value"]);

        // Changing a value on the type keeps the flag; one undo takes back the change of value, then the wall with its copy.
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["pset"] = "RAVA_Wall", ["name"] = "Renovation", ["value"] = "demolish" });
        Assert.True((bool)Params(f, type).Single(x => (string?)x["name"] == "Renovation")["instance"]!);
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal("new", (string?)Params(f, wall).Single()["value"]);
        f.Session.Undo();
        Assert.Null(f.Model.Get(wall));
    }

    [Fact]
    public void A_wall_given_another_type_takes_the_defaults_it_lacks_and_keeps_its_own()
    {
        var f = F();
        var plain = f.Type("WT Plain", ("Brick", 0.25, true));
        var wall = f.Wall(0, 0, 5, 0, plain);
        var other = f.Wall(0, 3, 5, 3, plain);
        Assert.Empty(Params(f, wall));
        f.Run("element.setParam", new JsonObject { ["id"] = other, ["pset"] = "RAVA_Wall", ["name"] = "Renovation", ["kind"] = "text", ["value"] = "existing" });
        var type = Typed(f);
        f.Run("architecture.setWallType", new JsonObject { ["ids"] = new JsonArray(wall, other), ["typeId"] = type });
        Assert.Equal("new", (string?)Params(f, wall).Single()["value"]);
        Assert.Equal("existing", (string?)Params(f, other).Single()["value"]);
    }

    [Fact]
    public void Elements_made_before_are_filled_only_when_asked_in_one_undo_step()
    {
        var f = F();
        var type = f.Type("WT Later", ("Brick", 0.25, true));
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(0, 3, 5, 3, type);
        f.Run("element.setParam", new JsonObject { ["id"] = type, ["name"] = "Status", ["kind"] = "text", ["value"] = "NEW", ["instance"] = true });
        Assert.Empty(Params(f, a));
        f.Run("element.setParam", new JsonObject { ["id"] = b, ["name"] = "Status", ["kind"] = "text", ["value"] = "EXISTING" });
        var r = f.Run("types.applyInstanceDefaults", new JsonObject { ["id"] = type })!;
        Assert.Equal(1, (int)r["filled"]!);
        Assert.Equal("NEW", (string?)Params(f, a).Single()["value"]);
        Assert.Equal("EXISTING", (string?)Params(f, b).Single()["value"]);
        f.Session.Undo();
        Assert.Empty(Params(f, a));
        // A type without any is told so.
        var plain = f.Type("WT Plain", ("Brick", 0.25, true));
        Assert.Throws<CommandException>(() => f.Run("types.applyInstanceDefaults", new JsonObject { ["id"] = plain }));
    }

    [Fact]
    public void The_ifc_export_writes_an_instance_default_on_the_element_not_on_the_type()
    {
        var f = F();
        var type = Typed(f);
        f.Wall(0, 0, 5, 0, type);
        var path = Path.Combine(_dir, "defaults.ifc");
        IfcExport.Write(f.Session, path, null);
        var file = StepFile.Read(path);
        var wallType = file.OfType("IFCWALLTYPE").Single(t => (string?)t[2] == "WT Fire");
        var typeSets = (wallType[5] as List<object?> ?? []).Select(r => file.Get(r)!).ToList();
        Assert.DoesNotContain(typeSets, s => (string?)s[2] == "RAVA_Wall");
        Assert.Contains(typeSets, s => (string?)s[2] == "Pset_WallCommon");
        var text = File.ReadAllText(path);
        Assert.Contains("'RAVA_Wall'", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Renovation',$,IFCLABEL('new'),$)", text);
    }

    [Fact]
    public void Parameters_of_a_set_of_ones_own_keep_their_ifc_types_in_the_project_s_units()
    {
        var f = F();
        var wall = f.Wall(0, 0, 4, 0);
        void Set(string name, string kind, JsonNode value) =>
            f.Run("element.setParam", new JsonObject { ["id"] = wall, ["pset"] = "RAVA_Data", ["name"] = name, ["kind"] = kind, ["value"] = value });
        Set("Loadbearing", "bool", true);
        Set("Clear height", "length", 2.5);
        Set("Face area", "area", 12.5);
        Set("Slope", "angle", 90);
        Set("U value", "number", 0.17);
        Set("Code", "text", "W-01");
        var path = Path.Combine(_dir, "typed.ifc");
        IfcExport.Write(f.Session, path, null);
        var text = File.ReadAllText(path);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Loadbearing',$,IFCBOOLEAN(.T.),$)", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Clear height',$,IFCLENGTHMEASURE(2.5),$)", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Face area',$,IFCAREAMEASURE(12.5),$)", text);
        // Degrees in Lapin, radians in the file (its plane angle unit).
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Slope',$,IFCPLANEANGLEMEASURE(1.570796327),$)", text);
        Assert.Contains("IFCSIUNIT(*,.PLANEANGLEUNIT.,$,.RADIAN.)", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('U value',$,IFCREAL(0.17),$)", text);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Code',$,IFCTEXT('W-01'),$)", text);
    }

    [Fact]
    public void Phases_give_the_common_set_its_ifc_status_and_the_user_s_own_value_wins()
    {
        var f = F();
        var kept = f.Wall(0, 0, 5, 0);
        var demolished = f.Wall(0, 3, 5, 3);
        var added = f.Wall(0, 6, 5, 6);
        var temporary = f.Wall(0, 9, 5, 9);
        var own = f.Wall(0, 12, 5, 12);
        var none = f.Wall(0, 15, 5, 15);
        f.Run("phases.assign", new JsonObject { ["id"] = kept, ["created"] = "existing" });
        f.Run("phases.assign", new JsonObject { ["id"] = demolished, ["created"] = "existing", ["demolished"] = "new-construction" });
        f.Run("phases.assign", new JsonObject { ["id"] = added, ["created"] = "new-construction" });
        f.Run("phases.assign", new JsonObject { ["id"] = temporary, ["created"] = "new-construction", ["demolished"] = "new-construction" });
        f.Run("phases.assign", new JsonObject { ["id"] = own, ["created"] = "new-construction" });
        f.Run("element.setParam", new JsonObject { ["id"] = own, ["pset"] = "Pset_WallCommon", ["name"] = "Status", ["value"] = "OTHER" });
        var path = Path.Combine(_dir, "status.ifc");
        IfcExport.Write(f.Session, path, null);
        var file = StepFile.Read(path);
        string? Status(string id)
        {
            var wall = file.OfType("IFCWALL").Single(w => (string?)w[0] == id);
            var sets = file.OfType("IFCRELDEFINESBYPROPERTIES").Where(r => (r[4] as List<object?> ?? []).Contains(new StepRef(wall.Id))).Select(r => file.Get(r[5])!);
            var common = sets.FirstOrDefault(s => (string?)s[2] == "Pset_WallCommon");
            var prop = (common?[4] as List<object?> ?? []).Select(p => file.Get(p)!).FirstOrDefault(p => (string?)p[0] == "Status");
            return prop?[2] is StepTyped t ? t.Value as string : null;
        }
        Assert.Equal("EXISTING", Status(kept));
        Assert.Equal("DEMOLISH", Status(demolished));
        Assert.Equal("NEW", Status(added));
        Assert.Equal("TEMPORARY", Status(temporary));
        Assert.Equal("OTHER", Status(own));
        Assert.Null(Status(none));
    }
}
