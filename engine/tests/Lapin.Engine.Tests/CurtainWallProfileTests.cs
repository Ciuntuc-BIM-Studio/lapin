using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>Report #127, a tester's request (Edit Curtain Wall Profile, as Revit's): a curtain wall's boundary drawn in elevation; its grid, mullions, transoms and panels follow, and the profile stays editable.</summary>
public class CurtainWallProfileTests
{
    static string Add(Fixture f, double x1, double y1, double x2, double y2, double height = 3)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["height"] = height };
        return (string)f.Run("architecture.addCurtainWall", a)!["id"]!;
    }

    static JsonArray Top(params (double U, double V)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.U, q.V)).ToArray());
    static void Profile(Fixture f, string cw, JsonNode? top) => f.Run("architecture.setCurtainWallProfile", new JsonObject { ["ids"] = new JsonArray(cw), ["top"] = top });
    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    /// <summary>The highest point of the parts, and the lowest of the highest points along the wall.</summary>
    static (double Max, Vec3[] Vertices) Extent(Fixture f, string id)
    {
        var v = f.Session.Geometry(id)!.Parts.SelectMany(p => p.Solid.Vertices).ToArray();
        return (v.Max(p => p.Z), v);
    }

    [Fact]
    public void A_gable_boundary_puts_a_line_where_it_bends_and_fills_the_triangle_with_a_panel_and_a_sloped_member()
    {
        var f = new Fixture();
        var cw = Add(f, 0, 0, 6, 0);
        var flat = Volume(f, cw);
        Profile(f, cw, Top((0, 3), (3, 5), (6, 3)));
        var g = f.Session.Geometry(cw)!;
        Assert.Null(g.Diagnostics);
        // The wall rises to its highest point and no part goes over the boundary.
        Assert.Equal(5, f.Model.Get(cw)!.Num("height"), 9);
        var (max, verts) = Extent(f, cw);
        Assert.InRange(max, 4.99, 5.001);
        foreach (var v in verts.Where(v => v.X is > 0.01 and < 5.99)) Assert.True(v.Z <= (v.X <= 3 ? 3 + (v.X / 3) * 2 : 5 - ((v.X - 3) / 3) * 2) + 1e-6, $"({v.X}, {v.Z}) is over the boundary");
        // A mullion stands at the apex, 3 m along.
        Assert.Contains(g.Parts, p => p.Key == "mullion:2" || p.Key!.StartsWith("mullion:") && p.Solid.Vertices.Any(q => Math.Abs(q.X - 3) < 0.03 && q.Z > 4.9));
        // Each bay has its top panel and its member along the boundary.
        Assert.True(g.Parts.Count(p => p.Key!.EndsWith(":top") && p.Key.StartsWith("panel:")) >= 4);
        Assert.True(g.Parts.Count(p => p.Key!.EndsWith(":top") && p.Key.StartsWith("transom:")) >= 4);
        // The glass over the wall: more than the rectangle's 3 m, less than 5 m high.
        var q = f.Session.Quantities(cw).First(x => x.Name == "Qto_CurtainWallQuantities");
        Assert.Equal(24, q.Quantities.Single(x => x.Name == "GrossSideArea").Value, 6);
        Assert.True(Volume(f, cw) > flat * 0.9);
        // Every part is a sound solid with a volume, the sloped ones too.
        Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 1e-6, $"{p.Key} has no volume"));
    }

    [Fact]
    public void A_stepped_boundary_keeps_each_bay_to_its_own_height_and_the_transoms_that_fit_under_it()
    {
        var f = new Fixture();
        var cw = Add(f, 0, 0, 6, 0);
        Profile(f, cw, Top((0, 3), (3, 3), (3, 4.5), (6, 4.5)));
        var g = f.Session.Geometry(cw)!;
        Assert.Null(g.Diagnostics);
        // The low half stays under 3 m, the high half reaches 4.5 m.
        foreach (var v in g.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.X < 2.9)) Assert.True(v.Z <= 3 + 1e-6);
        Assert.InRange(Extent(f, cw).Max, 4.49, 4.501);
        // A transom at the level of 3 m in the high bay only (its levels are the grid's: 4.5 m in bays of at most 1.5 m).
        var layout = CurtainWalls.Layout(CurtainSpec.Default, 6, 4.5, CurtainWalls.ProfileOf(f.Model.Get(cw)!));
        var low = layout.Pieces.Where(p => p.Role == "transom" && p.I1 <= 1 + layout.U.ToList().FindIndex(u => Math.Abs(u - 3) < 1e-6) - 1).ToList();
        var highTop = layout.Pieces.Where(p => p.Role == "transom" && layout.U[p.I0] >= 3 - 1e-6 && p.Z1 > 3.2).ToList();
        Assert.NotEmpty(highTop);
        Assert.All(low, p => Assert.True(p.Z1 <= 3 + 1e-6));
    }

    [Fact]
    public void The_profile_stays_editable_the_grid_follows_the_wall_and_flat_gives_the_rectangle_back()
    {
        var f = new Fixture();
        var cw = Add(f, 0, 0, 6, 0);
        var flat = Volume(f, cw);
        Profile(f, cw, Top((0, 3), (3, 4), (6, 3)));
        Assert.NotEqual(flat, Volume(f, cw));
        // Edited again: the same wall, the profile as last given, no rebuilding.
        Profile(f, cw, Top((0, 3), (6, 4)));
        Assert.Equal(2, ((JsonArray)f.Model.Get(cw)!.Attrs["topProfile"]!).Count);
        Assert.InRange(Extent(f, cw).Max, 3.99, 4.001);
        // The wall made shorter keeps its profile, clamped to its length; made longer it stays flat past its end.
        f.Run("architecture.updateCurtainWall", new JsonObject { ["id"] = cw, ["x2"] = 4 });
        Assert.Null(f.Session.Geometry(cw)!.Diagnostics);
        // Flat again: the rectangle, as a wall of its height.
        Profile(f, cw, null);
        Assert.Null(f.Model.Get(cw)!.Attrs["topProfile"]);
        f.Run("architecture.updateCurtainWall", new JsonObject { ["id"] = cw, ["x2"] = 6, ["height"] = 3 });
        Assert.Equal(flat, Volume(f, cw), 6);
        // A profile out of order, or under the base, is refused.
        Assert.Throws<CommandException>(() => Profile(f, cw, Top((3, 3), (1, 3))));
        Assert.Throws<CommandException>(() => Profile(f, cw, Top((0, 0.01), (6, 3))));
    }

    [Fact]
    public void A_profiled_curtain_wall_on_an_arc_and_with_its_members_in_the_schedule_still_builds()
    {
        var f = new Fixture();
        var cw = Add(f, 0, 0, 6, 0);
        Profile(f, cw, Top((0, 3), (2, 4), (4, 4), (6, 3)));
        var q = f.Session.Quantities(cw);
        var parts = q.First(x => x.Name == "Lapin_CurtainWallParts").Quantities;
        Assert.True(parts.Single(x => x.Name == "TransomLength").Value > 0);
        Assert.True(parts.Single(x => x.Name == "GlassArea").Value > 0);
        // The glass is less than the whole side area (the members and the mullions take some).
        Assert.True(parts.Single(x => x.Name == "GlassArea").Value < q.First(x => x.Name == "Qto_CurtainWallQuantities").Quantities.Single(x => x.Name == "GrossSideArea").Value);
        var arc = Add(f, 0, 5, 6, 5);
        f.Run("architecture.updateCurtainWall", new JsonObject { ["id"] = arc, ["x3"] = 3, ["y3"] = 6 });
        Profile(f, arc, Top((0, 3), (3, 4.2), (6, 3)));
        Assert.Null(f.Session.Geometry(arc)!.Diagnostics);
        Assert.InRange(Extent(f, arc).Max, 4.19, 4.21);
    }
}
