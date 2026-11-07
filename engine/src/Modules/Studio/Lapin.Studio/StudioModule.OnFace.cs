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
    /// The 3D sketch (docs/plan-studio-sketch.md, decision 374): with no shape at all, only the face's plane (made or
    /// found), to draft on with the command line; with <c>open: true</c>, the outline is an open line on the face (a
    /// sketch, never extruded); with <c>sketchId</c>, a closed shape already drafted on a plane is the one pushed or pulled.
    /// </summary>
    static JsonNode? OnFace(ICommandContext ctx, JsonObject a)
    {
        var (e, solids, p, n) = RequirePick(ctx, a, "the face to draw on");
        var face = BrepPick.Face(solids, p, n) ?? throw new CommandException("There is no flat face there: shapes are drawn on flat faces.");
        var normal = face.Normal;
        var on = p - normal * Vec3.Dot(p - face.Outer[0], normal);
        Entity sketch;
        Entity view;
        if (a["sketchId"] is JsonValue sv && (string?)sv is { } sid)
        {
            sketch = Drafts.Require(ctx.Model, sid);
            if (Drafts.ShapeOf(sketch) is not (Circle or Polyline { Closed: true })) throw new CommandException("Only a closed shape is pushed or pulled: close the outline first.");
            view = ctx.Model.Get(sketch.Str("viewId") ?? "") ?? throw new CommandException("The sketch lies on no view.");
        }
        else
        {
            view = PlaneOnFace(ctx, e, on, normal);
            if (a["outline"] is null && a["circle"] is null && a["arc"] is null) return new JsonObject { ["planeId"] = view.Id, ["name"] = view.Name };
            sketch = Drafts.Add(ctx, view.Id, null, ShapeOnPlane(view, a));
        }
        var result = new JsonObject { ["planeId"] = view.Id, ["sketchId"] = sketch.Id };
        var d = Json.Number(a["distance"]) ?? 0;
        if (Math.Abs(d) < 1e-6) return result;
        if (Drafts.ShapeOf(sketch) is Polyline { Closed: false } or Line) throw new CommandException("An open line stays a sketch: only a closed shape is pushed or pulled.");
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

    /// <summary>The shape given in the model (a circle, a closed outline, or an open line with <c>open</c>) on the plane's own (x, y).</summary>
    static Shape ShapeOnPlane(Entity view, JsonObject a)
    {
        var plane = RefPlanes.Of(view);
        Vec2 L(Vec3 q) => plane.Local(q).At;
        Shape shape;
        if (a["arc"] is JsonObject arc)
        {
            Vec2 A(string k) => L(Features.P(arc[k]) ?? throw new CommandException("An arc needs its start, its end and a point on it."));
            return ArcThrough(A("start"), A("through"), A("end")) ?? throw new CommandException("The three points of the arc lie on a line.");
        }
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
            var open = a["open"] is JsonValue ov && ov.TryGetValue<bool>(out var o) && o;
            if (open)
            {
                for (var i = pts.Count - 1; i > 0; i--) if ((pts[i] - pts[i - 1]).Length < 1e-6) pts.RemoveAt(i);
                if (pts.Count < 2) throw new CommandException("A line on a face needs two points at least.");
                return pts.Count == 2 ? new Line(pts[0], pts[1]) : new Polyline(pts, false);
            }
            if (pts.Count > 1 && (pts[0] - pts[^1]).Length < 1e-6) pts.RemoveAt(pts.Count - 1);
            if (pts.Count < 3 || Math.Abs(Polygon2.SignedArea(pts)) < 1e-6) throw new CommandException("A shape on a face needs three corners at least, not in a line.");
            shape = new Polyline(pts, true);
        }
        return shape;
    }

    /// <summary>
    /// The reference plane lying on a face: one made for a face before in that plane, facing the same way (so shapes drawn on
    /// one face share it), else a new one through the point, named after the element and marked as a face's.
    /// </summary>
    static Entity PlaneOnFace(ICommandContext ctx, Entity element, Vec3 at, Vec3 normal)
    {
        if (FacePlane(ctx.Model, at, normal) is { } found) return found;
        var attrs = new JsonObject { ["kind"] = RefPlanes.Kind, ["scale"] = 50, [OnFaceKey] = element.Id };
        foreach (var (k, val) in RefPlanes.OnFace(at, normal)) attrs[k] = val?.DeepClone();
        var view = new Entity(ctx.NewId(), Views.Class, $"{element.Name}, {SideOf(normal)}", attrs);
        ctx.Add(view);
        return view;
    }

    /// <summary>The reference plane lying on a face's plane and facing its way, if one was made (by a shape drawn on it, or Sketch on face).</summary>
    static Entity? FacePlane(IModelView m, Vec3 at, Vec3 normal)
    {
        foreach (var v in m.OfClass(Views.Class))
        {
            if (v.Str("kind") != RefPlanes.Kind) continue;
            var pl = RefPlanes.Of(v);
            if (Vec3.Dot(pl.Normal, normal) > 1 - 1e-7 && Math.Abs(Vec3.Dot(at - pl.Origin, pl.Normal)) < 1e-5) return v;
        }
        return null;
    }

    /// <summary>
    /// A face split by the lines sketched on its plane (stage B, decision 375): the region round the point picked, in the
    /// model, or null when the lines split nothing there (no sketch on the face, or the region is the whole face).
    /// </summary>
    internal static List<Vec3>? RegionOnFace(IModelView m, BrepFace face, Vec3 p)
    {
        var n = face.Normal;
        if (FacePlane(m, face.Outer[0], n) is not { } view) return null;
        var plane = RefPlanes.Of(view);
        var segs = new List<(Vec2, Vec2)>();
        void Loop(IReadOnlyList<Vec2> pts, bool closed)
        {
            for (var i = 0; i + 1 < pts.Count; i++) segs.Add((pts[i], pts[i + 1]));
            if (closed && pts.Count > 2) segs.Add((pts[^1], pts[0]));
        }
        var drawn = 0;
        foreach (var d in Drafts.On(m, view.Id))
            if (Drafts.ShapeOf(d) is { } sh && d.Attrs["hatch"] == null)
                foreach (var pl in Drafts.Flatten(sh)) { Loop(pl, false); drawn++; }
        if (drawn == 0) return null;
        Vec2 L(Vec3 q) => plane.Local(q).At;
        Loop(face.Outer.Select(L).ToList(), true);
        foreach (var h in face.Holes) Loop(h.Select(L).ToList(), true);
        var region = SketchRegions.RegionAt(segs, L(p));
        if (region == null) return null;
        var whole = Math.Abs(Polygon2.SignedArea(face.Outer.Select(L).ToList()));
        if (Math.Abs(Polygon2.SignedArea(region)) >= whole - 1e-9) return null;
        return region.Select(plane.World).ToList();
    }

    /// <summary>The arc from a start through a point to an end, counter-clockwise as Lapin keeps arcs; null when the three lie on a line.</summary>
    public static Arc? ArcThrough(Vec2 a, Vec2 m, Vec2 b)
    {
        var d = 2 * (a.X * (m.Y - b.Y) + m.X * (b.Y - a.Y) + b.X * (a.Y - m.Y));
        if (Math.Abs(d) < 1e-12) return null;
        double S(Vec2 q) => q.X * q.X + q.Y * q.Y;
        var c = new Vec2((S(a) * (m.Y - b.Y) + S(m) * (b.Y - a.Y) + S(b) * (a.Y - m.Y)) / d, (S(a) * (b.X - m.X) + S(m) * (a.X - b.X) + S(b) * (m.X - a.X)) / d);
        double Ang(Vec2 q) => Math.Atan2(q.Y - c.Y, q.X - c.X);
        var (sa, ma, ea) = (Ang(a), Ang(m), Ang(b));
        // Counter-clockwise from a to b passes m, or else the arc runs from b to a.
        var ccw = Geo.AngleInArc(ma, sa, ea);
        return ccw ? new Arc(c, (a - c).Length, sa, ea) : new Arc(c, (a - c).Length, ea, sa);
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
