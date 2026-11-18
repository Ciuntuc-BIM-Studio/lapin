using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The reference details of the material graphics (docs/plan-material-graphics.md, stage 0, decision 678; moved into the engine
/// by decision 681): small projects built from commands, as a user draws them, each with the views that show what its materials
/// draw. The engine draws them for a material standard's sample sheet (<c>standards.previewSheet</c>: the catalogue's preview,
/// the store's pictures); the tests (<c>ReferenceDetails</c>, <c>MaterialGraphicsReferenceTests</c>) check and snapshot them.
/// They need the architecture and structure modules' commands; a detail whose commands the product lacks is left out.
/// </summary>
public static class MaterialReferenceDetails
{
    public static readonly int[] Scales = [20, 50, 100];

    /// <summary>A detail as built: its name, its title, and its views (a name and view.create's arguments, without the scale).</summary>
    public sealed record Detail(string Name, string Title, List<(string Name, JsonObject Args)> Views);

    /// <summary>Every detail, in order: its name and how it is built in a session.</summary>
    public static readonly (string Name, Func<Session, Detail> Build)[] All =
    [
        ("composite-walls", CompositeWalls), ("wall-on-slab", WallOnSlab), ("floor", Floor), ("strip-footing", StripFooting),
        ("column-beam", ColumnAndBeam), ("roof", Roof), ("facade", Facade),
    ];

    /// <summary>Every layer drawn in plans, finishes too (a new project's plans leave them out, decision 500), as the details are drawn.</summary>
    public static void EveryLayerInPlans(Session s) => Run(s, "viewStyle.update", new JsonObject { ["id"] = "architectural", ["layersPlan"] = "all" });

    static JsonNode? Run(Session s, string command, JsonObject args) { s.Execute(command, args, out var r); return r; }

    static string Storey(Session s) => Spatial.Storeys(s.Model).First().Id;

    static string Type(Session s, string name, params (string Material, double Thickness, bool Core)[] layers) =>
        (string)Run(s, "architecture.createWallType", new JsonObject
        {
            ["name"] = name,
            ["layers"] = new JsonArray(layers.Select(l => (JsonNode)new JsonObject { ["material"] = l.Material, ["thickness"] = l.Thickness, ["core"] = l.Core }).ToArray()),
        })!["id"]!;

    static string Wall(Session s, double x1, double y1, double x2, double y2, string typeId) =>
        (string)Run(s, "architecture.addWall", new JsonObject { ["storeyId"] = Storey(s), ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = typeId })!["id"]!;

    static string Material(Session s, string name) => s.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;

    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    static JsonObject Plan(Session s) => new() { ["kind"] = "plan", ["storeyId"] = Storey(s) };
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
    public static Detail CompositeWalls(Session s)
    {
        var type = Type(s, "Brick 25 + EPS 10", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Vapour barrier", 0.001, false), ("EPS insulation", 0.1, false), ("Render", 0.01, false));
        var a = Wall(s, 0, 0, 4.5, 0, type);
        Wall(s, 0, 0, 0, 3, type);
        Wall(s, 3, 0, 3, 2.5, type);
        Run(s, "architecture.addWindow", new JsonObject { ["wallId"] = a, ["x"] = 1.5, ["y"] = 0 });
        return new Detail("composite-walls", "Composite wall: L corner, T, free end, window with sill", [("plan", Plan(s)), ("section", Across(a, 1.5))]);
    }

    /// <summary>A wall standing on a floor of finish, screed and concrete, its core going down through the finish (decision 497): a section across it.</summary>
    public static Detail WallOnSlab(Session s)
    {
        var floor = SlabType(s, "Floor 25");
        Run(s, "architecture.addSlab", new JsonObject { ["storeyId"] = Storey(s), ["points"] = Pts((-2, -2), (3, -2), (3, 2), (-2, 2)), ["typeId"] = floor });
        var type = Type(s, "Brick 25 plastered", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Gypsum plaster", 0.015, false));
        var wall = Wall(s, 0, -1.5, 0, 1.5, type);
        return new Detail("wall-on-slab", "Wall on a floor slab, its core through the finishes", [("section", Across(wall, 1.5))]);
    }

    static string SlabType(Session s, string name) => (string)Run(s, "architecture.createSlabType", new JsonObject
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
    public static Detail Floor(Session s)
    {
        var floor = SlabType(s, "Floor 26");
        Run(s, "architecture.addSlab", new JsonObject { ["storeyId"] = Storey(s), ["points"] = Pts((0, 0), (3, 0), (3, 2), (0, 2)), ["typeId"] = floor });
        return new Detail("floor", "Floor slab: tiles, screed, vapour barrier, XPS, concrete", [("section", Section((1.5, -0.5), (1.5, 2.5), 0.5))]);
    }

    /// <summary>A strip footing with its protection board, XPS and waterproofing (marked a membrane), under a wall: a section across it.</summary>
    public static Detail StripFooting(Session s)
    {
        var footing = (string)Run(s, "structure.addStripFooting", new JsonObject { ["storeyId"] = Storey(s), ["x1"] = 0, ["y1"] = 0, ["x2"] = 3, ["y2"] = 0 })!["id"]!;
        Run(s, "material.update", new JsonObject { ["id"] = Material(s, "Waterproofing"), ["membrane"] = true });
        return new Detail("strip-footing", "Strip footing with waterproofing, insulation and protection", [("section", Across(footing, 1.5))]);
    }

    /// <summary>A concrete column and a beam in a plastered brick wall: the plan and a section across the wall through the column.</summary>
    public static Detail ColumnAndBeam(Session s)
    {
        var type = Type(s, "Brick 30 plastered", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.3, true), ("Gypsum plaster", 0.015, false));
        var wall = Wall(s, 0, 0, 5, 0, type);
        Run(s, "structure.addColumn", new JsonObject { ["storeyId"] = Storey(s), ["x"] = 2.5, ["y"] = 0 });
        Run(s, "structure.addBeam", new JsonObject { ["storeyId"] = Storey(s), ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 });
        return new Detail("column-beam", "Concrete column and beam in a brick wall", [("plan", Plan(s)), ("section", Across(wall, 1.2))]);
    }

    /// <summary>A gable roof with its tile covering over a 6 × 4 m plan: a section across the ridge.</summary>
    public static Detail Roof(Session s)
    {
        Run(s, "architecture.addRoof", new JsonObject { ["storeyId"] = Storey(s), ["points"] = Pts((0, 0), (6, 0), (6, 4), (0, 4)), ["style"] = "fr_provence" });
        return new Detail("roof", "Roof with its tile covering", [("section", Section((3, -1), (3, 5), 1))]);
    }

    /// <summary>A facade: facing brick, timber cladding, metal cladding, and the tiled roof over them: the south elevation.</summary>
    public static Detail Facade(Session s)
    {
        var brick = Type(s, "Facing brick", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Facing brick", 0.115, false));
        var timber = Type(s, "Timber clad", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Timber cladding", 0.02, false));
        var metal = Type(s, "Metal clad", ("Gypsum plaster", 0.015, false), ("Brick masonry", 0.25, true), ("Metal cladding", 0.01, false));
        // Drawn right to left, so the facing (the last layer, on the right of the axis) is the south face the elevation sees.
        Wall(s, 3, 0, 0, 0, brick);
        Wall(s, 6, 0, 3, 0, timber);
        Wall(s, 9, 0, 6, 0, metal);
        Run(s, "architecture.addRoof", new JsonObject { ["storeyId"] = Storey(s), ["points"] = Pts((0, 0), (9, 0), (9, 5), (0, 5)), ["style"] = "fr_provence" });
        return new Detail("facade", "Facade: facing brick, timber and metal cladding, roof tiles", [("elevation", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })]);
    }
}
