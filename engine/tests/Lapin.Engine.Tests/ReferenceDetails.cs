using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Model;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The reference details of the material graphics (docs/plan-material-graphics.md, stage 0, decision 670): small projects
/// built from commands, as a user draws them, each with the views that show what its materials draw. Each view is made
/// at 1:20, 1:50 and 1:100 (<see cref="Scales"/>). They are the fixtures of <see cref="MaterialGraphicsReferenceTests"/>
/// and, saved as .lapin projects with <c>LAPIN_REFERENCE_PDF=1</c>, what the user guide's scenes open (stage 8).
/// </summary>
static class ReferenceDetails
{
    public static readonly int[] Scales = [20, 50, 100];

    /// <summary>A detail: its name, its project, and its views (a name and view.create's arguments, without the scale).</summary>
    public sealed record Detail(string Name, string Title, Fixture F, List<(string Name, JsonObject Args)> Views);

    public static IEnumerable<Func<Detail>> All =>
    [
        CompositeWalls, WallOnSlab, Floor, StripFooting, ColumnAndBeam, Roof, Facade,
    ];

    static Fixture New()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        // Every layer drawn in plans, finishes too (a new project's plans leave them out, decision 500).
        f.EveryLayerInPlans();
        return f;
    }

    static string Material(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    static JsonObject Plan(Fixture f) => new() { ["kind"] = "plan", ["storeyId"] = f.Storey };
    static JsonObject Across(string element, double station) => new() { ["kind"] = "section", ["across"] = element, ["station"] = station };
    static JsonObject Section((double X, double Y) a, (double X, double Y) b, double? depth = null)
    {
        var o = new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(a.X, a.Y), ["end"] = new JsonArray(b.X, b.Y) };
        if (depth != null) o["depth"] = depth;
        return o;
    }

    /// <summary>
    /// A composite wall (plaster, brick, a vapour barrier under 2 mm, EPS, render) in an L corner, a T and a free end, with a
    /// window and its sill: the plan, and a section across the wall through the window.
    /// </summary>
    public static Detail CompositeWalls()
    {
        var f = New();
        var type = f.Type("Brick 25 + EPS 10", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Vapour barrier", 0.001, false), ("EPS insulation", 0.1, false), ("Render", 0.01, false));
        var a = f.Wall(0, 0, 4.5, 0, type);
        f.Wall(0, 0, 0, 3, type);
        f.Wall(3, 0, 3, 2.5, type);
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = a, ["x"] = 1.5, ["y"] = 0 });
        return new Detail("composite-walls", "Composite wall: L corner, T, free end, window with sill", f, [("plan", Plan(f)), ("section", Across(a, 1.5))]);
    }

    /// <summary>A wall standing on a floor of finish, screed and concrete, its core going down through the finish (decision 497): a section across it.</summary>
    public static Detail WallOnSlab()
    {
        var f = New();
        var floor = SlabType(f, "Floor 25");
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((-2, -2), (3, -2), (3, 2), (-2, 2)), ["typeId"] = floor });
        var type = f.Type("Brick 25 plastered", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Gypsum plaster", 0.015, false));
        var wall = f.Wall(0, -1.5, 0, 1.5, type);
        return new Detail("wall-on-slab", "Wall on a floor slab, its core through the finishes", f, [("section", Across(wall, 1.5))]);
    }

    static string SlabType(Fixture f, string name) => (string)f.Run("architecture.createSlabType", new JsonObject
    {
        ["name"] = name,
        ["layers"] = new JsonArray(
            new JsonObject { ["material"] = "Ceramic tiles", ["thickness"] = 0.01 },
            new JsonObject { ["material"] = "Cement screed", ["thickness"] = 0.05 },
            new JsonObject { ["material"] = "Vapour barrier", ["thickness"] = 0.001 },
            new JsonObject { ["material"] = "XPS insulation", ["thickness"] = 0.05 },
            new JsonObject { ["material"] = "Reinforced concrete", ["thickness"] = 0.15, ["core"] = true }),
    })!["id"]!;

    /// <summary>A floor: tiles, screed, a vapour barrier, XPS and the concrete slab; a section across it.</summary>
    public static Detail Floor()
    {
        var f = New();
        var floor = SlabType(f, "Floor 26");
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (3, 0), (3, 2), (0, 2)), ["typeId"] = floor });
        return new Detail("floor", "Floor slab: tiles, screed, vapour barrier, XPS, concrete", f, [("section", Section((1.5, -0.5), (1.5, 2.5), 0.5))]);
    }

    /// <summary>A strip footing with its protection board, XPS and waterproofing (marked a membrane), under a wall: a section across it.</summary>
    public static Detail StripFooting()
    {
        var f = New();
        var footing = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 3, ["y2"] = 0 })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = Material(f, "Waterproofing"), ["membrane"] = true });
        return new Detail("strip-footing", "Strip footing with waterproofing, insulation and protection", f, [("section", Across(footing, 1.5))]);
    }

    /// <summary>A concrete column and a beam in a plastered brick wall: the plan and a section across the wall through the column.</summary>
    public static Detail ColumnAndBeam()
    {
        var f = New();
        var type = f.Type("Brick 30 plastered", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.3, true), ("Gypsum plaster", 0.015, false));
        var wall = f.Wall(0, 0, 5, 0, type);
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 0 });
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 });
        return new Detail("column-beam", "Concrete column and beam in a brick wall", f, [("plan", Plan(f)), ("section", Across(wall, 1.2))]);
    }

    /// <summary>A gable roof with its tile covering over a 6 × 4 m plan: a section across the ridge.</summary>
    public static Detail Roof()
    {
        var f = New();
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["style"] = "fr_provence" });
        return new Detail("roof", "Roof with its tile covering", f, [("section", Section((3, -1), (3, 5), 1))]);
    }

    /// <summary>A facade: facing brick, timber cladding, metal cladding, and the tiled roof over them: the south elevation.</summary>
    public static Detail Facade()
    {
        var f = New();
        var brick = f.Type("Facing brick", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Facing brick", 0.115, false));
        var timber = f.Type("Timber clad", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Timber cladding", 0.02, false));
        var metal = f.Type("Metal clad", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Metal cladding", 0.01, false));
        // Drawn right to left, so the facing (the last layer, on the right of the axis) is the south face the elevation sees.
        f.Wall(3, 0, 0, 0, brick);
        f.Wall(6, 0, 3, 0, timber);
        f.Wall(9, 0, 6, 0, metal);
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts((0, 0), (9, 0), (9, 5), (0, 5)), ["style"] = "fr_provence" });
        return new Detail("facade", "Facade: facing brick, timber and metal cladding, roof tiles", f, [("elevation", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })]);
    }
}
