using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// The drill-down picking's chain (<c>core.assemblyPath</c>): the group, the outermost whole, …, the part; the
/// spatial structure never in it; just the element when it is part of nothing.
/// </summary>
public class AssemblyPathTests
{
    static Fixture F() => new(new ArchitectureModule(), new StudioModule(), new StructureModule(), new SteelModule(), new DraftingModule());

    static List<string>? Path(Fixture f, string id) =>
        f.Session.Query("core.assemblyPath", new JsonObject { ["id"] = id })?["path"]?.AsArray().Select(x => (string)x!).ToList();

    [Fact]
    public void A_part_climbs_its_assemblies_to_the_group_and_stops_at_the_storey()
    {
        var f = F();
        var (a, b, c, d) = (f.Wall(0, 0, 4, 0), f.Wall(4, 0, 4, 3), f.Wall(4, 3, 0, 3), f.Wall(10, 0, 14, 0));
        string outer = "", inner = "";
        f.Session.Edit("Assemblies", e =>
        {
            outer = e.NewId(); inner = e.NewId();
            e.Add(new Entity(outer, "IfcElementAssembly", "Frame", []));
            e.Add(new Entity(inner, "IfcElementAssembly", "Truss", []));
            e.Relate(IfcClass.RelContainedInSpatialStructure, f.Storey, outer);
            e.Relate(IfcClass.RelAggregates, outer, inner);
            e.Relate(IfcClass.RelAggregates, outer, c);
            e.Relate(IfcClass.RelAggregates, inner, a);
            e.Relate(IfcClass.RelAggregates, inner, b);
        });
        Assert.Equal([outer, inner, a], Path(f, a));
        Assert.Equal([outer, c], Path(f, c));
        Assert.Equal([outer], Path(f, outer));
        // On its own: itself; the storey and the building are not wholes.
        Assert.Equal([d], Path(f, d));
        Assert.Null(Path(f, "nothing"));

        // In a group, the group is on top (the outer assembly is its member).
        var g = (string)f.Run("group.create", new JsonObject { ["ids"] = new JsonArray(outer, d) })!["id"]!;
        Assert.Equal([g, outer, inner, b], Path(f, b));
        Assert.Equal([g, d], Path(f, d));
        Assert.Equal([g], Path(f, g));
    }

    [Fact]
    public void A_steel_halls_member_has_the_hall_above_it()
    {
        var f = F();
        var hall = (string)f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["secondary"] = false } })!["id"]!;
        var member = f.Model.RelatedOf(hall, IfcClass.RelAggregates).First().Id;
        Assert.Equal([hall, member], Path(f, member));
        Assert.Equal([hall], Path(f, hall));
    }
}
