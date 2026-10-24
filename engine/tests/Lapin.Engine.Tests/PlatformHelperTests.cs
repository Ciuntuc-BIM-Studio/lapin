using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>The argument readers every module shares: what is malformed is refused by name, never read as zero.</summary>
public class ArgsTests
{
    static string Refused(Action a) => Assert.Throws<CommandException>(a).Message;

    [Fact]
    public void A_point_is_two_finite_numbers()
    {
        var p = Args.Point(new JsonArray(1, 2.5), "centre");
        Assert.Equal((1, 2.5), (p.X, p.Y));
        Assert.Contains("centre", Refused(() => Args.Point(new JsonArray(1), "centre")));
        Assert.Contains("[x, y]", Refused(() => Args.Point(new JsonArray(1, "a"), "start")));
        Assert.Contains("[x, y]", Refused(() => Args.Point(JsonValue.Create("1,2"), "start")));
        Assert.Contains("[x, y]", Refused(() => Args.Point(null, "end")));

        var q = Args.Point(new JsonObject { ["x1"] = 1, ["y1"] = "2" }, "x1", "y1");
        Assert.Equal((1, 2), (q.X, q.Y));
        Assert.Equal("Missing y1.", Refused(() => Args.Point(new JsonObject { ["x1"] = 1 }, "x1", "y1")));
        Assert.Equal("x1 must be a number.", Refused(() => Args.Point(new JsonObject { ["x1"] = "abc", ["y1"] = 0 }, "x1", "y1")));
    }

    [Fact]
    public void An_outline_is_a_list_of_points_or_the_rectangle_between_two_corners()
    {
        var pts = Args.Points(new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 3)) });
        Assert.Equal(3, pts.Count);
        Assert.Equal(3, pts[2].Y);
        Assert.Contains("point 2", Refused(() => Args.Points(new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, "x"), new JsonArray(4, 3)) })));
        Assert.Contains("empty", Refused(() => Args.Points(new JsonObject { ["points"] = new JsonArray() })));
        Assert.Contains("list", Refused(() => Args.Points(new JsonObject { ["points"] = "0,0 4,0" })));

        var rect = Args.Points(new JsonObject { ["x1"] = 1, ["y1"] = 1, ["x2"] = 5, ["y2"] = 4 });
        Assert.Equal([(1.0, 1.0), (5.0, 1.0), (5.0, 4.0), (1.0, 4.0)], rect.Select(p => (p.X, p.Y)).ToArray());
        Assert.Equal("Missing y2.", Refused(() => Args.Points(new JsonObject { ["x1"] = 1, ["y1"] = 1, ["x2"] = 5 })));
    }

    [Fact]
    public void Ids_take_the_list_and_the_single_one_once_each_and_skip_what_is_not_a_string()
    {
        Assert.Equal(["a", "b"], Args.Ids(new JsonObject { ["ids"] = new JsonArray("a", "b", 3, null, "a"), ["id"] = "b" }));
        Assert.Equal(["z"], Args.Ids(new JsonObject { ["id"] = "z" }));
        Assert.Empty(Args.Ids(new JsonObject()));
    }

    [Fact]
    public void A_join_style_is_butt_or_mitre()
    {
        Assert.Null(Args.JoinStyle(null));
        Assert.Equal(JoinStyle.Butt, Args.JoinStyle(JsonValue.Create("butt")));
        Assert.Equal(JoinStyle.Mitre, Args.JoinStyle(JsonValue.Create("mitre")));
        Assert.Contains("miter", Refused(() => Args.JoinStyle(JsonValue.Create("miter"))));
    }

    [Fact]
    public void Commands_refuse_malformed_arguments_instead_of_reading_zeros()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        var slab = Refused(() => f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(5, "a"), new JsonArray(5, 4)) }));
        Assert.Contains("point 2", slab);
        Assert.Empty(f.Model.OfClass(IfcClass.Slab));
        // A typo in a join style, for beams and strip footings as for walls.
        Assert.Contains("miter", Refused(() => f.Run("structure.restyleJoins", new JsonObject { ["ids"] = new JsonArray(), ["style"] = "miter" })));
        // A partial door or window configuration is unreadable, not an internal error later on.
        Assert.Contains("unreadable", Refused(() => f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = new JsonObject { ["kind"] = "door" } })));
        Assert.Empty(f.Model.OfClass(IfcClass.DoorType));
    }
}

/// <summary>One dash table for pens, drafting standards and CAD layers.</summary>
public class DashesTests
{
    [Fact]
    public void The_same_names_give_the_same_dashes_everywhere()
    {
        Assert.Equal(new double[] { 6, 1.5, 1, 1.5 }, LineStyles.DashOf("center"));
        Assert.Equal(new double[] { 1.5, 1 }, LineStyles.DashOf("hidden"));
        Assert.Equal(new double[] { 4, 1, 0.3, 1 }, Standards.DashOf("dashdot"));
        Assert.Equal(new double[] { 3, 1.5 }, Dashes.Of("dashed"));
        Assert.Empty(Dashes.Of("continuous"));
        Assert.Null(new Lapin.Drafting.Cad.CadLayer("x").Dash);
        Assert.Equal(new double[] { 0.3, 1 }, new Lapin.Drafting.Cad.CadLayer("x", LineType: "dotted").Dash);
        Assert.Equal(LineStyles.Roles, LineRole.All);
    }
}
