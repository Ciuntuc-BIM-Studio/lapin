using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall type's sample in the Wall types dialog's preview (decision 529): a straight wall, <c>length</c> long (3 m), drawn
/// from right to left so its R face (layer 1, the exterior) is on top in plan as the dialog's table reads, and a short wall
/// of the same type, <c>branch</c> long (1.2 m), joined to its middle in T on the L side, so the joins and the layers'
/// priorities show. Its top as a new wall of the type takes it (the storey above, the type's own height); <c>height</c>, an
/// unconnected height instead. The section is taken across the long wall a fifth of its length from its left end, looking
/// away from the T, so the R face is on the drawing's right, the L face on its left.
/// </summary>
sealed class WallTypePreview : ITypePreview
{
    public bool Handles(string typeClass) => typeClass == IfcClass.WallType;

    public TypePreviewSample Build(TypePreviewScratch s, JsonObject type, JsonObject sample)
    {
        var args = (JsonObject)type.DeepClone();
        var savedId = (string?)args["id"];
        args.Remove("id");
        var typeId = (string?)s.Run("architecture.createWallType", args)?["id"] ?? throw new CommandException("The type could not be made.");
        // A saved type's top for new walls (WallTypeTop, saved on its own): the sample's walls take it too.
        if (savedId != null && s.Source.Get(savedId) is { Class: IfcClass.WallType } saved)
            s.Edit(edit =>
            {
                var t = edit.Model.Get(typeId)!;
                var ta = t.CloneAttrs();
                foreach (var k in new[] { WallTop.TypeTopKey, WallTop.TypeOffsetKey, WallTop.TypeHeightKey })
                    if (saved.Attrs[k] is { } v) ta[k] = v.DeepClone();
                edit.Replace(t.With(attrs: ta));
            });
        var length = Math.Clamp(Json.Number(sample["length"]) ?? 3.0, 1.0, 20.0);
        var branch = Math.Clamp(Json.Number(sample["branch"]) ?? 1.2, 0.3, 10.0);
        JsonObject Wall(double x1, double y1, double x2, double y2)
        {
            var w = new JsonObject { ["storeyId"] = s.Storey, ["typeId"] = typeId, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };
            if (Json.Number(sample["height"]) is { } h && h > 0) w["height"] = h;
            return w;
        }
        var main = (string?)s.Run("architecture.addWall", Wall(length, 0, 0, 0))?["id"] ?? throw new CommandException("The sample wall could not be made.");
        var tee = (string?)s.Run("architecture.addWall", Wall(length / 2, 0, length / 2, -branch))?["id"];
        var thickness = LayerSets.BandsOf(s.Model, main) is { Count: > 0 } bands ? bands.Max(b => Math.Max(b.From, b.To)) - bands.Min(b => Math.Min(b.From, b.To)) : 0.3;
        var x = length * 0.2;
        var reach = thickness + 0.4;
        // From the L side to the R side: looking to its left, away from the T.
        return new TypePreviewSample(tee == null ? [main] : [main, tee], new Vec2(x, -reach), new Vec2(x, reach), Math.Min(0.5, length * 0.25));
    }
}
