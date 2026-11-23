using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A platform's slopes made a grading (docs/plan-site-grading.md, T6 follow-up): its own cut and fill batters (<c>cutSlope</c>,
/// <c>fillSlope</c>: offset rings at the height it reaches, booleans on the terrain) replaced by a T5 slope to the ground from its
/// outline, outwards, at the same 1 : N (<see cref="Gradings"/>: each ray daylighted exactly on the ground's triangles), live as the
/// platform changes. One undo step. A platform not converted keeps its batters exactly as before.
/// </summary>
public static class PlatformGradings
{
    public static void Register(IModuleRegistry r)
    {
        // {id (a platform)}: its batters off, a grading slope from its outline in their place. Returns {id (the grading), notes?}.
        r.Command("platformSlopesToGrading", "Convert slopes to grading", (ctx, a) =>
        {
            var m = ctx.Model;
            var pad = Spatial.Require(m, (string?)a["id"], IfcClass.EarthworksCut);
            if (Ditches.Is(pad)) throw new CommandException("A ditch has no batters: only a platform's slopes become a grading.");
            if (OutlineHoles.Of(pad).Count > 0) throw new CommandException($"„{pad.Name}” has holes, so it has no batters of its own (they are plumb): nothing to convert.");
            var cut = pad.NumOrNull("cutSlope") is { } c && c > 0 ? c : (double?)null;
            var fill = pad.NumOrNull("fillSlope") is { } f && f > 0 ? f : (double?)null;
            if (cut == null && fill == null) throw new CommandException($"„{pad.Name}” has no slopes of its own (it is cut plumb into the ground): draw a slope to the ground from it instead.");
            var terrain = Terrains.TerrainOf(m, pad.Id) is { } t && Terrains.IsTerrain(t) ? t : throw new CommandException($"„{pad.Name}” is on no terrain.");
            var attrs = pad.CloneAttrs();
            attrs.Remove("cutSlope");
            attrs.Remove("fillSlope");
            ctx.Replace(pad.With(attrs: attrs));
            var args = new JsonObject
            {
                ["kind"] = Gradings.Slope, ["edges"] = new JsonArray(new JsonObject { ["id"] = pad.Id }), ["side"] = "out", ["terrainId"] = terrain.Id,
                ["cutSlope"] = cut ?? Gradings.DefaultCut, ["fillSlope"] = fill ?? Gradings.DefaultFill, ["name"] = $"{pad.Name} slopes",
            };
            if (Spatial.StoreyOf(m, terrain.Id) is { } storey) args["storeyId"] = storey.Id;
            var made = Gradings.Add(ctx, args);
            // A batter it had only one way: the other takes the grading's default, said.
            var more = new List<string>();
            if (cut == null) more.Add($"It had no cut slope (plumb): the grading cuts at 1:{Gradings.DefaultCut:0.##}.");
            if (fill == null) more.Add($"It had no fill slope (plumb): the grading fills at 1:{Gradings.DefaultFill:0.##}.");
            if (more.Count > 0)
            {
                if (made["notes"] is not JsonArray notes) made["notes"] = notes = new JsonArray();
                foreach (var n in more) notes.Add((JsonNode)n);
            }
            return made;
        });
    }
}
