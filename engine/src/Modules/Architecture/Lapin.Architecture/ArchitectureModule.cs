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
        ], "#b5654a"));
        r.Class(new(IfcClass.WallType, "Wall type", [new("mark", "Mark", "text")], "#b5654a", Kind: "type"));
        r.Class(new(IfcClass.OpeningElement, "Opening", [new("width", "Width", "length", Min: 0.01), new("height", "Height", "length", Min: 0.01)], Kind: "element"));
        r.Class(new(IfcClass.Door, "Door", [], "#c8a27a"));
        r.Class(new(IfcClass.Window, "Window", [], "#9cc8e8"));
        r.Class(new(IfcClass.DoorType, "Door type", [], "#c8a27a", Kind: "type"));
        r.Class(new(IfcClass.WindowType, "Window type", [], "#9cc8e8", Kind: "type"));
        r.Geometry(IfcClass.Door, (fill, model, _) => Openings.FillGeometry(fill, model));
        r.Geometry(IfcClass.Window, (fill, model, _) => Openings.FillGeometry(fill, model));
        r.Quantities(IfcClass.Door, (fill, _, model) => FillQuantities(fill, model, "Qto_DoorBaseQuantities"));
        r.Quantities(IfcClass.Window, (fill, _, model) => FillQuantities(fill, model, "Qto_WindowBaseQuantities"));
        r.Catalog("joinery", new JsonObject
        {
            ["windowOperations"] = new JsonArray(Joinery.WindowOperations.Select(x => (JsonNode)x).ToArray()),
            ["doorOperations"] = new JsonArray(Joinery.DoorOperations.Select(x => (JsonNode)x).ToArray()),
            ["defaultDoor"] = JoinerySpecs.Door.ToJson(),
            ["defaultWindow"] = JoinerySpecs.Window.ToJson(),
        });

        // The configurator's preview: the body as boxes (front elevation in X–Z), and why the spec cannot be built.
        r.Query("joineryPreview", (q, a) =>
        {
            var spec = JoinerySpec.From(a["spec"]);
            if (spec == null) return new JsonObject { ["problem"] = "The configuration is unreadable." };
            var problem = Joinery.Problem(spec);
            var boxes = problem == null ? Joinery.Boxes(spec) : [];
            return new JsonObject
            {
                ["problem"] = problem,
                ["boxes"] = new JsonArray(boxes.Select(b => (JsonNode)new JsonObject
                {
                    ["role"] = b.Role, ["x0"] = b.Min.X, ["x1"] = b.Max.X, ["y0"] = b.Min.Y, ["y1"] = b.Max.Y, ["z0"] = b.Min.Z, ["z1"] = b.Max.Z,
                }).ToArray()),
                ["panels"] = new JsonArray(Joinery.Normalised(spec).Select(p =>
                {
                    var r = Joinery.PanelRect(spec, p);
                    return (JsonNode)new JsonObject { ["operation"] = p.Operation, ["x0"] = r.X0, ["x1"] = r.X1, ["z0"] = r.Z0, ["z1"] = r.Z1 };
                }).ToArray()),
                ["ifcType"] = spec.IsDoor ? Joinery.DoorOperation(spec).Type : Joinery.WindowPartitioning(spec).Type,
            };
        });

        r.Command("addDoor", "Door", (ctx, a) => AddFill(ctx, a, "door"));
        r.Command("addWindow", "Window", (ctx, a) => AddFill(ctx, a, "window"));

        r.Command("moveOpening", "Move opening", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Nothing to move.");
            Openings.Move(ctx, e, Json.Number(a["along"]), Json.Number(a["sill"]));
            return null;
        });

        r.Command("flipJoinery", "Flip", (ctx, a) =>
        {
            foreach (var id in Ids(a))
                if (ctx.Model.Get(id) is { Class: IfcClass.Door or IfcClass.Window } fill) Openings.Flip(ctx, fill, (string?)a["axis"] ?? "y");
            return null;
        });

        r.Command("createJoineryType", "New door or window type", (ctx, a) =>
        {
            var spec = JoinerySpec.From(a["spec"]) ?? throw new CommandException("The type's configuration is missing or unreadable.");
            return new JsonObject { ["id"] = Openings.CreateType(ctx, spec).Id };
        });

        r.Command("updateJoineryType", "Change door or window type", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.");
            Openings.UpdateType(ctx, type, JinerySpecOrThrow(a["spec"]));
            return null;
        });

        r.Command("setJoineryType", "Change type", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["typeId"] ?? "") is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.");
            foreach (var id in Ids(a))
                if (ctx.Model.Get(id) is { Class: IfcClass.Door or IfcClass.Window } fill) Openings.SetType(ctx, fill, type);
            return null;
        });

        r.Geometry(IfcClass.Wall, (wall, model, services) => Walls.Geometry(wall, model, services));
        r.Quantities(IfcClass.Wall, (wall, geometry, model) => Walls.Quantities(wall, geometry, model));
        r.Catalog("defaultZones", new JsonArray(WallZones.Defaults("Brick").Select(z => (JsonNode)z).ToArray()));

        r.Command("createWallType", "New wall type", (ctx, a) =>
        {
            var type = WallTypes.Create(ctx, (string?)a["name"], WallTypes.ParseLayers(a["layers"]), (string?)a["mark"], a["zones"], a["lintels"]);
            return new JsonObject { ["id"] = type.Id };
        });

        r.Command("updateWallType", "Change wall type", (ctx, a) =>
        {
            var type = Spatial.Require(ctx.Model, (string?)a["id"], IfcClass.WallType);
            WallTypes.Update(ctx, type, (string?)a["name"], a["layers"] is null ? null : WallTypes.ParseLayers(a["layers"]), a["zones"], a["lintels"]);
            return null;
        });

        r.Command("addWall", "Wall", (ctx, a) =>
        {
            var storeyId = (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id ?? throw new CommandException("The project has no storey.");
            var type = (string?)a["typeId"] is { } typeId ? Spatial.Require(ctx.Model, typeId, IfcClass.WallType) : null;
            var wall = (a["join"] is JsonValue j && j.TryGetValue<bool>(out var join) && !join)
                ? Walls.AddUnjoined(ctx, storeyId, P(a, "x1", "y1"), P(a, "x2", "y2"), Json.Number(a["height"]) ?? Walls.DefaultHeight, Json.Number(a["baseOffset"]) ?? 0, type)
                : Walls.Add(ctx, storeyId, P(a, "x1", "y1"), P(a, "x2", "y2"), Json.Number(a["height"]) ?? Walls.DefaultHeight, Json.Number(a["baseOffset"]) ?? 0, type, Style(a["joinStyle"]) ?? JoinStyle.Butt);
            return new JsonObject { ["id"] = wall.Id };
        });

        r.Command("joinWalls", "Join walls", (ctx, a) =>
        {
            var w1 = Spatial.Require(ctx.Model, (string?)a["a"], IfcClass.Wall);
            var w2 = Spatial.Require(ctx.Model, (string?)a["b"], IfcClass.Wall);
            var priority = (string?)a["priority"] switch { "a" => 'a', "b" => 'b', _ => (char?)null };
            var join = Joins.Join(ctx, w1, w2, Style(a["style"]), priority);
            return new JsonObject { ["kind"] = join.Kind.ToString(), ["style"] = join.Style == JoinStyle.Mitre ? "mitre" : "butt" };
        });

        r.Command("unjoinWalls", "Unjoin walls", (ctx, a) =>
        {
            Joins.Unjoin(ctx, (string?)a["a"] ?? "", (string?)a["b"] ?? "");
            return null;
        });

        r.Command("restyleJoins", "Join style", (ctx, a) =>
        {
            var swap = a["swap"] is JsonValue v && v.TryGetValue<bool>(out var sw) && sw;
            var style = Style(a["style"]);
            if (style == null && !swap) throw new CommandException("Say how: a style (butt or mitre), or swap.");
            var n = Joins.Restyle(ctx, Ids(a), style, swap);
            if (n == 0) throw new CommandException("The selected walls have no corner to restyle.");
            return new JsonObject { ["corners"] = n };
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
            if ((end - start).Length < 1e-3) throw new CommandException("A wall needs two different points: it would have no length.");
            var height = Json.Number(a["height"]) ?? wall.Num("height", Walls.DefaultHeight);
            if (!(height > 0)) throw new CommandException("A wall's height must be positive.");
            var thickness = Json.Number(a["thickness"]) ?? wall.NumOrNull("thickness");
            if (thickness is <= 0) throw new CommandException("A wall's thickness must be positive.");
            ctx.Replace(wall.With(attrs: Walls.Attrs(Walls.Start(wall), Walls.End(wall), height, Json.Number(a["baseOffset"]) ?? wall.Num("baseOffset"), thickness)));
            var moveJoined = a["moveJoinedEnds"] is not JsonValue mj || !mj.TryGetValue<bool>(out var keep) || keep;
            if ((start - Walls.Start(wall)).Length > 1e-12 || (end - Walls.End(wall)).Length > 1e-12)
                Joins.MoveEnds(ctx, ctx.Model.Get(wall.Id)!, start, end, moveJoined);
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

    static JoinerySpec JinerySpecOrThrow(JsonNode? n) => JoinerySpec.From(n) ?? throw new CommandException("The type's configuration is missing or unreadable.");

    /// <summary>A door or window in the wall picked: where the click falls along it (or <c>along</c>), of the type given or the project's first.</summary>
    static JsonNode AddFill(ICommandContext ctx, JsonObject a, string kind)
    {
        var wall = Spatial.Require(ctx.Model, (string?)a["wallId"], IfcClass.Wall);
        var type = (string?)a["typeId"] is { } typeId
            ? ctx.Model.Get(typeId) is { Class: IfcClass.DoorType or IfcClass.WindowType } t ? t : throw new CommandException("There is no such door or window type.")
            : Openings.EnsureType(ctx, kind);
        var along = Json.Number(a["along"]) ?? Openings.AlongOf(wall, new Vec2(Json.Number(a["x"]) ?? 0, Json.Number(a["y"]) ?? 0));
        var fill = Openings.AddFill(ctx, wall, type, along, Json.Number(a["sill"]));
        return new JsonObject { ["id"] = fill.Id };
    }

    static IEnumerable<QuantitySet> FillQuantities(Entity fill, IModelView m, string set)
    {
        if (Openings.SpecOf(m, fill.Id) is not { } spec) yield break;
        yield return new QuantitySet(set,
        [
            new("Width", spec.Width, "LENGTH"),
            new("Height", spec.Height, "LENGTH"),
            new("Area", spec.Width * spec.Height, "AREA"),
        ]);
    }

    static JoinStyle? Style(JsonNode? n) => (string?)n switch { "mitre" => JoinStyle.Mitre, "butt" => JoinStyle.Butt, null => null, var other => throw new CommandException($"A join style is butt or mitre, not „{other}”.") };

    static Vec2 P(JsonObject a, string x, string y) =>
        new(Json.Number(a[x]) ?? throw new CommandException($"Missing {x}."), Json.Number(a[y]) ?? throw new CommandException($"Missing {y}."));

    static IEnumerable<string> Ids(JsonObject a)
    {
        var ids = a["ids"] is JsonArray arr ? arr.Select(n => (string?)n).OfType<string>().ToList() : [];
        if ((string?)a["id"] is { } one) ids.Add(one);
        return ids.Distinct();
    }
}
