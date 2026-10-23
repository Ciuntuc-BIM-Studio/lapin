using System.Text.Json.Nodes;
using BubbleBim.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>Architecture: layered walls and their types.</summary>
public sealed class ArchitectureModule : ILapinModule
{
    public string Id => "architecture";
    public string Name => "Architecture";

    public void Register(IModuleRegistry r)
    {
        r.Class(new(IfcClass.Wall, "Wall",
        [
            new("height", "Height", "length", Min: 0.01),
            new("baseOffset", "Base offset", "length"),
            new("thickness", "Thickness (no layers)", "length", Min: 0.001),
        ], "#b5654a"));
        r.Class(new(IfcClass.WallType, "Wall type", [new("mark", "Mark", "text")], "#b5654a", Kind: "type"));

        r.Geometry(IfcClass.Wall, (wall, model, _) => Walls.Geometry(wall, model));

        r.Command("createWallType", "New wall type", (ctx, a) =>
        {
            var type = WallTypes.Create(ctx, (string?)a["name"], WallTypes.ParseLayers(a["layers"]), (string?)a["mark"]);
            return new JsonObject { ["id"] = type.Id };
        });

        r.Command("updateWallType", "Change wall type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.WallType);
            WallTypes.Update(ctx, type, (string?)a["name"], a["layers"] is null ? null : WallTypes.ParseLayers(a["layers"]));
            return null;
        });

        r.Command("addWall", "Wall", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var type = (string?)a["typeId"] is { } typeId ? Spatial.Require(ctx.Model, typeId, IfcClass.WallType) : null;
            var wall = Walls.Add(ctx, storeyId, P(a, "x1", "y1"), P(a, "x2", "y2"),
                Json.Number(a["height"]) ?? Walls.DefaultHeight, Json.Number(a["baseOffset"]) ?? 0, type);
            return new JsonObject { ["id"] = wall.Id };
        });

        r.Command("updateWall", "Change wall", (ctx, a) =>
        {
            var wall = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.Wall);
            var start = Walls.Start(wall);
            var end = Walls.End(wall);
            if (Json.Number(a["x1"]) is { } x1) start = start with { X = x1 };
            if (Json.Number(a["y1"]) is { } y1) start = start with { Y = y1 };
            if (Json.Number(a["x2"]) is { } x2) end = end with { X = x2 };
            if (Json.Number(a["y2"]) is { } y2) end = end with { Y = y2 };
            if ((end - start).Length < 1e-3) throw new CommandException("A wall needs two different points.");
            var height = Json.Number(a["height"]) ?? wall.Num("height", Walls.DefaultHeight);
            if (!(height > 0)) throw new CommandException("A wall's height must be positive.");
            var thickness = Json.Number(a["thickness"]) ?? wall.NumOrNull("thickness");
            if (thickness is <= 0) throw new CommandException("A wall's thickness must be positive.");
            ctx.Replace(wall.With(attrs: Walls.Attrs(start, end, height, Json.Number(a["baseOffset"]) ?? wall.Num("baseOffset"), thickness)));
            return null;
        });

        r.Command("setWallType", "Change type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["typeId"], IfcClass.WallType);
            foreach (var id in Ids(a)) Walls.SetType(ctx, Spatial.Require(ctx.Model, id, IfcClass.Wall), type);
            return null;
        });

        r.Command("flipWall", "Flip", (ctx, a) =>
        {
            var axis = (string?)a["axis"] ?? "y";
            foreach (var id in Ids(a)) Walls.Flip(ctx, Spatial.Require(ctx.Model, id, IfcClass.Wall), axis);
            return null;
        });
    }

    static Vec2 P(JsonObject a, string x, string y) =>
        new(Json.Number(a[x]) ?? throw new CommandException($"Missing {x}."), Json.Number(a[y]) ?? throw new CommandException($"Missing {y}."));

    static IEnumerable<string> Ids(JsonObject a)
    {
        var ids = a["ids"] is JsonArray arr ? arr.Select(n => (string?)n).OfType<string>().ToList() : [];
        if ((string?)a["id"] is { } one) ids.Add(one);
        return ids.Distinct();
    }
}
