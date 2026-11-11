using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Stage C of the 3D sketch (docs/plan-studio-sketch.md, decision 376): Follow Me, SketchUp's sweep of a face along
/// edges, and the tape measure's guides.
/// </summary>
public sealed partial class StudioModule
{
    /// <summary>The class of a guide: a line or a point the sketch tools snap to, kept in the project, never exported or printed.</summary>
    public const string GuideClass = "LapinGuide";

    static void RegisterFollowMe(IModuleRegistry r)
    {
        r.Class(new(GuideClass, "Guide", [], "#7a5cc2", Kind: "resource", Template: false));
        r.Command("followMe", "Follow Me", FollowMe);
        // {a: [x, y, z], b?: [x, y, z]}: a guide line through a and b (endless), or a guide point at a.
        r.Command("addGuide", "Guide", (ctx, a) =>
        {
            var p = Features.P(a["a"]) ?? throw new CommandException("A guide needs a point.");
            var attrs = new JsonObject { ["a"] = Features.J(p) };
            if (Features.P(a["b"]) is { } q)
            {
                if ((q - p).Length < 1e-6) throw new CommandException("A guide line needs two points apart.");
                attrs["b"] = Features.J(q);
            }
            var n = ctx.Model.OfClass(GuideClass).Count() + 1;
            var e = new Entity(ctx.NewId(), GuideClass, attrs["b"] != null ? $"Guide {n}" : $"Guide point {n}", attrs);
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });
        r.Command("clearGuides", "Erase guides", (ctx, _) =>
        {
            var ids = ctx.Model.OfClass(GuideClass).Select(e => e.Id).ToList();
            foreach (var id in ids) ctx.Remove(id);
            return new JsonObject { ["removed"] = ids.Count };
        });
    }

    /// <summary>
    /// Follow Me: <c>{profile: {id, point, normal}, path: {id} (a drafted line, polyline, arc or circle) | {face: {id, point,
    /// normal}} (the outline of that face, or of its region), class?, name?}</c>. The face picked (or its region) becomes a
    /// sketch on its plane, the profile; a face given as the path becomes a closed sketch on its own plane; then one Studio
    /// solid sweeps the profile where it stands along the path (<see cref="Solids.FollowMe"/>), following both sketches.
    /// </summary>
    static JsonNode? FollowMe(ICommandContext ctx, JsonObject a)
    {
        var pr = a["profile"] as JsonObject ?? throw new CommandException("Pick the face to sweep.");
        var (e, solids, p, n) = RequirePick(ctx, pr, "the face to sweep");
        var face = BrepPick.Face(solids, p, n) ?? throw new CommandException("There is no flat face there: Follow Me sweeps a flat face.");
        var outline = RegionOnFace(ctx.Model, face, p)?.Outline ?? face.Outer.ToList();
        var profile = Sketch(ctx, e, face, outline);
        string pathId;
        if (a["path"] is JsonObject path && (string?)path["id"] is { } pid && path["face"] == null)
        {
            var d = Drafts.Require(ctx.Model, pid, editable: false);
            if (Drafts.ShapeOf(d) is not (Line or Polyline or Arc or Circle)) throw new CommandException("A path is a line, a polyline, an arc or a circle.");
            pathId = d.Id;
        }
        else if (a["path"]?["face"] is JsonObject pf)
        {
            var (pe, ps, pp, pn) = RequirePick(ctx, pf, "the face whose edges are the path");
            var f2 = BrepPick.Face(ps, pp, pn) ?? throw new CommandException("There is no flat face there for the path.");
            pathId = Sketch(ctx, pe, f2, RegionOnFace(ctx.Model, f2, pp)?.Outline ?? f2.Outer.ToList()).Id;
        }
        else throw new CommandException("Pick the path: a drafted line or a face whose edges it follows.");
        var args = new JsonObject { ["profileId"] = profile.Id, ["pathId"] = pathId, ["name"] = a["name"]?.DeepClone(), ["class"] = a["class"]?.DeepClone() };
        if (Spatial.StoreyOf(ctx.Model, e.Id)?.Id is { } storey) args["storeyId"] = storey;
        var r = Create(ctx, args, "followMe")!.AsObject();
        r["profileId"] = profile.Id;
        r["pathId"] = pathId;
        return r;
    }

    /// <summary>A face's outline (or region) drafted as a closed polyline on the face's reference plane.</summary>
    static Entity Sketch(ICommandContext ctx, Entity e, BrepFace face, IReadOnlyList<Vec3> outline)
    {
        var view = PlaneOnFace(ctx, e, outline[0], face.Normal);
        var plane = RefPlanes.Of(view);
        var pts = outline.Select(q => plane.Local(q).At).ToList();
        if (Polygon2.SignedArea(pts) < 0) pts.Reverse();
        return Drafts.Add(ctx, view.Id, null, new Polyline(pts, true));
    }
}
