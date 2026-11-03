using System.Text.Json.Nodes;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// Drawing on a face in 3D, as SketchUp does (decision 272): a rectangle, a circle or a polygon drawn on the face
/// picked becomes a sketch on a reference plane lying on that face (the one already there, else a new one, which the
/// 3D view does not draw: the face shows where it is), and, pushed or pulled at once, an extrusion of it: out of the
/// face a solid of its own in the face's material, into it a cut the element is less. All one step; the sketch stays,
/// so the solid or the cut changes with it, and Push/Pull on its end (or a pocket's floor) changes its depth.
/// </summary>
public sealed partial class StudioModule
{
    /// <summary>The field a reference plane made for a face keeps: the element it was drawn on.</summary>
    public const string OnFaceKey = "onFace";

    static void RegisterOnFace(IModuleRegistry r) => r.Command("onFace", "Draw on face", OnFace);

    /// <summary>
    /// <c>{id, point, normal, outline: [[x, y, z]…] | circle: {center, radius}, distance?}</c>: the shape on the face of
    /// element <c>id</c> picked at <c>point</c>; with a distance, extruded out of the face (positive) or cut into it
    /// (negative). Returns the plane, the sketch and the solid made.
    /// </summary>
    static JsonNode? OnFace(ICommandContext ctx, JsonObject a)
    {
        var (e, solids, p, n) = RequirePick(ctx, a, "the face to draw on");
        var face = BrepPick.Face(solids, p, n) ?? throw new CommandException("There is no flat face there: shapes are drawn on flat faces.");
        var normal = face.Normal;
        var on = p - normal * Vec3.Dot(p - face.Outer[0], normal);
        var view = PlaneOnFace(ctx, e, on, normal);
        var plane = RefPlanes.Of(view);
        Vec2 L(Vec3 q) => plane.Local(q).At;
        Shape shape;
        if (a["circle"] is JsonObject c)
        {
            var centre = Features.P(c["center"]) ?? throw new CommandException("A circle needs its centre.");
            var radius = Json.Number(c["radius"]) ?? 0;
            if (radius < 1e-4) throw new CommandException("A circle needs a radius.");
            shape = new Circle(L(centre), radius);
        }
        else
        {
            var pts = (a["outline"] as JsonArray)?.Select(Features.P).Where(x => x != null).Select(x => L(x!.Value)).ToList() ?? [];
            if (pts.Count > 1 && (pts[0] - pts[^1]).Length < 1e-6) pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 3 || Math.Abs(Polygon2.SignedArea(pts)) < 1e-6) throw new CommandException("A shape on a face needs three corners at least, not in a line.");
            shape = new Polyline(pts, true);
        }
        var sketch = Drafts.Add(ctx, view.Id, null, shape);
        var result = new JsonObject { ["planeId"] = view.Id, ["sketchId"] = sketch.Id };
        var d = Json.Number(a["distance"]) ?? 0;
        if (Math.Abs(d) < 1e-6) return result;
        var storey = Spatial.StoreyOf(ctx.Model, e.Id)?.Id;
        var args = new JsonObject { ["profileId"] = sketch.Id, ["depth"] = d };
        if (storey != null) args["storeyId"] = storey;
        if (d < 0)
        {
            if (storey == null) throw new CommandException($"„{e.Name}” is not an element: nothing to cut into.");
            args["cut"] = new JsonArray(e.Id);
            args["name"] = $"Cut into {e.Name}";
        }
        var made = (string)Create(ctx, args, "extrude")!["id"]!;
        if (d > 0 && ctx.Query.Geometry(e.Id)?.Parts.FirstOrDefault(x => BrepPick.Face([x.Solid], p, n) != null)?.Material is { } mat && ctx.Model.Get(made) is { } solid)
        {
            var attrs = solid.CloneAttrs();
            attrs["material"] = mat;
            ctx.Replace(solid.With(attrs: attrs));
        }
        result["id"] = made;
        result["how"] = d > 0 ? "solid" : "cut";
        return result;
    }

    /// <summary>
    /// The reference plane lying on a face: one made for a face before in that plane, facing the same way (so shapes drawn on
    /// one face share it), else a new one through the point, named after the element and marked as a face's.
    /// </summary>
    static Entity PlaneOnFace(ICommandContext ctx, Entity element, Vec3 at, Vec3 normal)
    {
        foreach (var v in ctx.Model.OfClass(Views.Class))
        {
            if (v.Str("kind") != RefPlanes.Kind) continue;
            var pl = RefPlanes.Of(v);
            if (Vec3.Dot(pl.Normal, normal) > 1 - 1e-7 && Math.Abs(Vec3.Dot(at - pl.Origin, pl.Normal)) < 1e-5) return v;
        }
        var attrs = new JsonObject { ["kind"] = RefPlanes.Kind, ["scale"] = 50, [OnFaceKey] = element.Id };
        foreach (var (k, val) in RefPlanes.OnFace(at, normal)) attrs[k] = val?.DeepClone();
        var view = new Entity(ctx.NewId(), Views.Class, $"{element.Name}, {SideOf(normal)}", attrs);
        ctx.Add(view);
        return view;
    }

    /// <summary>Which face a normal is, in words: the top, the underside, or the compass way it faces (sloping when it leans).</summary>
    public static string SideOf(Vec3 n)
    {
        if (n.Z > 0.999) return "top face";
        if (n.Z < -0.999) return "underside";
        var deg = (Math.Atan2(n.Y, n.X) * 180 / Math.PI + 360) % 360;
        string[] ways = ["east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east"];
        var way = ways[(int)Math.Round(deg / 45) % 8];
        return Math.Abs(n.Z) > 0.02 ? $"{way} slope" : $"{way} face";
    }

    /// <summary>
    /// A face picked on <paramref name="host"/> that is a cut's own (a pocket's floor or side, left by a Studio solid cutting
    /// it): that cut and its face seen from inside, so Push/Pull changes the pocket rather than adding to the host.
    /// </summary>
    static (Entity Cut, BrepFace Face)? CutFaceAt(ICommandContext ctx, Entity host, Vec3 p, Vec3 n)
    {
        foreach (var rel in ctx.Model.RelationsOf(host.Id, IfcClass.RelInterferesElements).Where(x => x.Relating == host.Id))
            foreach (var id in rel.Related)
            {
                if (ctx.Model.Get(id) is not { } cut || !IsVoid(cut) || cut.Str("operation") is null) continue;
                // Its own body (the 3D view draws none for a cut).
                var body = Geometry(cut, ctx.Model, ctx.Services)?.Parts.Select(x => x.Solid).ToList() ?? [];
                if (body.Count > 0 && BrepPick.Face(body, p, -n, 1e-3) is { } f) return (cut, f);
            }
        return null;
    }
}
