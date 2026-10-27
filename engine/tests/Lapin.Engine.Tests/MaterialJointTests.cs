using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>
/// A material's "Draw joints" (report #22): off, walls of one material that go
/// on in one plane read as one surface; on, the joint between them is drawn, in
/// the plan, the 3D view and the elevations, for walls in line and for walls
/// stacked storey over storey.
/// </summary>
public class MaterialJointTests
{
    static (Fixture F, string Type, string Wood) Timber()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Timber", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Wood", ["thickness"] = 0.2 }) })!["id"]!;
        return (f, type, f.Model.OfClass(IfcClass.Material).First(m => m.Name == "Wood").Id);
    }

    static IEnumerable<(double X0, double Y0, double X1, double Y1)> Segments(JsonNode d)
    {
        foreach (var pen in d["pens"]!.AsArray())
            if (pen!["segments"]?.AsArray() is { } s)
                for (var k = 0; k + 3 < s.Count; k += 4) yield return ((double)s[k]!, (double)s[k + 1]!, (double)s[k + 2]!, (double)s[k + 3]!);
    }

    static int Edges3D(Fixture f, string id, Func<float[], int, bool> where)
    {
        var item = f.Session.Snapshot().Render.Single(r => r.NodeId == id);
        var n = 0;
        foreach (var part in item.Parts!) for (var i = 0; i + 5 < part.Edges.Length; i += 6) if (where(part.Edges, i)) n++;
        return n;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Walls_in_line_show_their_joint_only_when_the_material_draws_joints(bool joints)
    {
        var (f, type, wood) = Timber();
        var a = f.Wall(0, 0, 5, 0, type);
        f.Wall(5, 0, 10, 0, type);
        if (joints) f.Run("material.update", new JsonObject { ["id"] = wood, ["drawJoints"] = true });
        var plan = f.Session.Query("view.drawing", new JsonObject { ["id"] = "plan:" + f.Storey })!;
        Assert.Equal(joints, Segments(plan).Any(s => Math.Abs(s.X0 - 5) < 1e-6 && Math.Abs(s.X1 - 5) < 1e-6));
        Assert.Equal(joints, Edges3D(f, a, (e, i) => Math.Abs(e[i] - 5) < 1e-4 && Math.Abs(e[i + 3] - 5) < 1e-4) > 0);
        var el = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var elevation = f.Session.Query("view.drawing", new JsonObject { ["id"] = el })!;
        // Seen from the south the joint is the vertical at the middle of the run.
        Assert.Equal(joints, Segments(elevation).Any(s => Math.Abs(s.X0 - s.X1) < 1e-6 && Math.Abs(s.Y0 - s.Y1) > 1 && Math.Abs(s.X0) < 1e-6));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Walls_stacked_storey_over_storey_show_their_joint_only_when_the_material_draws_joints(bool joints)
    {
        var (f, type, wood) = Timber();
        var below = f.Wall(0, 0, 6, 0, type);
        var s1 = (string)f.Run("storey.create", new JsonObject())!["id"]!;
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = s1, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["typeId"] = type });
        if (joints) f.Run("material.update", new JsonObject { ["id"] = wood, ["drawJoints"] = true });
        var z = f.Model.Get(s1)!.Num("elevation");
        Assert.Equal(joints, Edges3D(f, below, (e, i) => Math.Abs(e[i + 2] - z) < 1e-4 && Math.Abs(e[i + 5] - z) < 1e-4 && Math.Abs(e[i] - e[i + 3]) > 1) > 0);
    }
}
