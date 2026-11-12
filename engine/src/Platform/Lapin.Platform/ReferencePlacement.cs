using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A reference (an element drawn from the triangles a file brought: a mesh's piece of furniture, an IFC file's element Lapin
/// did not recognise) moved, turned, mirrored, copied or raised like any element (decision 448). Its body stays the file's,
/// only where it stands changes: an element with a body of its own gives it to a shared body (<see cref="StoredBodies.SharedClass"/>)
/// the first time, and from then on it is an instance placed by its 12 numbers, so its copies (a row of chairs) share the
/// triangles. A mirror makes a body of its own again, turned inside out back (a shared body is never drawn mirrored). What
/// lies in world coordinates beside the body goes with it: the body before its openings cut it, the openings the file
/// gave it (and the doors and windows filling them, <see cref="ReferenceFills"/>), its own 2D and the direction its layers run. A link's elements stay read-only (the session refuses the edit).
/// </summary>
public sealed class ReferencePlacement : IElementPlacement, IElementRise
{
    public bool Handles(Entity e) => StoredBodies.Has(e);

    public void Transform(ICommandContext ctx, Entity e, Placement2D t, IReadOnlySet<string> moving)
    {
        ctx.Replace(Carried(ctx, e, Of(t), t));
        // The doors and windows in its kept openings go with it, as a native wall's (one carried on its own is not carried twice).
        foreach (var filler in ReferenceFills.Fillers(e).Distinct().Where(id => !moving.Contains(id)).ToList())
            if (ctx.Model.Get(filler) is { } door && StoredBodies.Has(door)) ctx.Replace(Carried(ctx, door, Of(t), t));
    }

    public string? Copy(ICommandContext ctx, Entity e, Placement2D t)
    {
        // The original shares its body first, so the copy names the same one.
        var source = t.Mirrored ? e : Shared(ctx, e);
        if (!ReferenceEquals(source, e)) ctx.Replace(source);
        var id = Placement.DeepCopy(ctx, source, Placement2D.Move(new Vec2(0, 0)));
        var copy = ctx.Model.Get(id)!;
        // The file's openings go with the copy under GlobalIds of its own (an id is never written twice), and the doors and
        // windows filling them are copied too, each filling the copy's opening, as a native wall's copy takes its doors.
        var attrs = copy.CloneAttrs();
        if (attrs[StoredBodies.OpeningsKey] is JsonArray openings)
            foreach (var o in openings.OfType<JsonObject>())
            {
                o["id"] = ctx.NewId();
                o.Remove("rel");
                var fills = new JsonArray();
                foreach (var f in (o["fills"] as JsonArray ?? []).OfType<JsonObject>())
                    if ((string?)f["id"] is { } filler && ctx.Model.Get(filler) is { } door && StoredBodies.Has(door))
                        fills.Add((JsonNode)new JsonObject { ["id"] = Copy(ctx, door, t) });
                o["fills"] = fills;
            }
        ctx.Replace(Carried(ctx, copy.With(attrs: attrs), Of(t), t));
        return id;
    }

    public bool Lift(ICommandContext ctx, Entity e, double dz)
    {
        double[] up = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, dz];
        ctx.Replace(Carried(ctx, e, up, null));
        foreach (var filler in ReferenceFills.Fillers(e).Distinct().ToList())
            if (ctx.Model.Get(filler) is { } door && StoredBodies.Has(door)) ctx.Replace(Carried(ctx, door, up, null));
        return true;
    }

    /// <summary>A plan transformation as an instance's 12 numbers: the images of x, y and z, and of the origin.</summary>
    public static double[] Of(Placement2D t)
    {
        var o = t.Point(new Vec2(0, 0));
        var x = t.Point(new Vec2(1, 0)) - o;
        var y = t.Point(new Vec2(0, 1)) - o;
        return [x.X, x.Y, 0, y.X, y.Y, 0, 0, 0, 1, o.X, o.Y, 0];
    }

    /// <summary><paramref name="m"/> after <paramref name="xf"/>: an instance placed by xf, then carried by m.</summary>
    public static double[] Then(double[] xf, double[] m)
    {
        var r = new double[12];
        for (var k = 0; k < 3; k++)
        {
            var v = Dir(m, new Vec3(xf[3 * k], xf[3 * k + 1], xf[3 * k + 2]));
            (r[3 * k], r[3 * k + 1], r[3 * k + 2]) = (v.X, v.Y, v.Z);
        }
        var p = StoredBodies.Apply(m, new Vec3(xf[9], xf[10], xf[11]));
        (r[9], r[10], r[11]) = (p.X, p.Y, p.Z);
        return r;
    }

    static Vec3 Dir(double[] m, Vec3 d) => new(m[0] * d.X + m[3] * d.Y + m[6] * d.Z, m[1] * d.X + m[4] * d.Y + m[7] * d.Z, m[2] * d.X + m[5] * d.Y + m[8] * d.Z);

    static bool Mirrors(double[] m) => Vec3.Dot(Vec3.Cross(new Vec3(m[0], m[1], m[2]), new Vec3(m[3], m[4], m[5])), new Vec3(m[6], m[7], m[8])) < 0;

    /// <summary>An element with a body of its own made an instance of a shared body holding it, where it stands (the same element otherwise).</summary>
    static Entity Shared(ICommandContext ctx, Entity e)
    {
        if (e.Attrs[StoredBodies.Key] is not JsonObject body) return e;
        var shared = new Entity(ctx.NewId(), StoredBodies.SharedClass, $"{e.Name} (shared)", new JsonObject { [StoredBodies.Key] = body.DeepClone() });
        ctx.Add(shared);
        var attrs = e.CloneAttrs();
        attrs.Remove(StoredBodies.Key);
        attrs[StoredBodies.BodyOf] = shared.Id;
        attrs[StoredBodies.XfKey] = StoredBodies.WriteXf([1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]);
        return e.With(attrs: attrs);
    }

    /// <summary>
    /// The element carried by <paramref name="m"/> (a plan transformation's, <paramref name="t"/>, which turns its 2D
    /// texts; null for a rise): an instance's numbers, or a mirrored one's own body; whatever lies in world coordinates with it.
    /// </summary>
    static Entity Carried(ICommandContext ctx, Entity e, double[] m, Placement2D? t)
    {
        var mirrored = Mirrors(m);
        if (!mirrored) e = Shared(ctx, e);
        var attrs = e.CloneAttrs();
        if (StoredBodies.InstanceOf(e) is var (bodyId, xf))
        {
            if (mirrored)
            {
                // Its body of its own, placed where the instance stood, then carried as any.
                var own = ctx.Model.Get(bodyId)?.Attrs[StoredBodies.Key]?.DeepClone() as JsonObject ?? new JsonObject { ["parts"] = new JsonArray() };
                Body(own, Then(xf, m));
                attrs[StoredBodies.Key] = own;
                attrs.Remove(StoredBodies.BodyOf);
                attrs.Remove(StoredBodies.XfKey);
            }
            else attrs[StoredBodies.XfKey] = StoredBodies.WriteXf(Then(xf, m));
        }
        else Body(attrs[StoredBodies.Key], m);
        Body(attrs[StoredBodies.UncutKey], m);
        Body(attrs[StoredBodies.OpeningsKey], m);
        if (attrs[ReferenceLayers.AxisKey] is JsonArray { Count: 3 } ax)
        {
            var d = Dir(m, new Vec3(Json.Number(ax[0]) ?? 0, Json.Number(ax[1]) ?? 0, Json.Number(ax[2]) ?? 0));
            attrs[ReferenceLayers.AxisKey] = new JsonArray(Math.Round(d.X, 9), Math.Round(d.Y, 9), Math.Round(d.Z, 9));
        }
        if (attrs[Plan2d.Key] is JsonObject plan && plan["plan"] is JsonArray items)
            foreach (var item in items.OfType<JsonObject>())
            {
                Flat(item["pts"] as JsonArray, m);
                foreach (var loop in (item["loops"] as JsonArray)?.OfType<JsonArray>() ?? []) Flat(loop, m);
                // A text turns with it, and stays readable when mirrored: it runs the other way along its line.
                if (t != null && Json.Number(item["angle"]) is var angle && (angle != null || item["text"] != null))
                {
                    var a = t.Angle((angle ?? 0) * Math.PI / 180) + (t.Mirrored ? Math.PI : 0);
                    item["angle"] = Math.Round(Math.IEEERemainder(a * 180 / Math.PI, 360), 3);
                }
            }
        return e.With(attrs: attrs);
    }

    /// <summary>A stored body (or a list or object of them: a reference's openings) carried by <paramref name="m"/>, a mirror's triangles turned back.</summary>
    static void Body(JsonNode? n, double[] m)
    {
        switch (n)
        {
            case JsonArray list:
                foreach (var x in list) Body(x, m);
                break;
            case JsonObject o when o["parts"] is JsonArray parts:
                var mirrored = Mirrors(m);
                foreach (var part in parts.OfType<JsonObject>())
                {
                    var (v, tr) = StoredBodies.ReadPart(part);
                    for (var i = 0; i + 2 < v.Length; i += 3)
                    {
                        var w = StoredBodies.Apply(m, new Vec3(v[i], v[i + 1], v[i + 2]));
                        (v[i], v[i + 1], v[i + 2]) = (Math.Round(w.X, 4), Math.Round(w.Y, 4), Math.Round(w.Z, 4));
                    }
                    if (mirrored) for (var i = 0; i + 2 < tr.Length; i += 3) (tr[i + 1], tr[i + 2]) = (tr[i + 2], tr[i + 1]);
                    var written = StoredBodies.WritePart((string?)part["name"] ?? "Body", (string?)part["color"] ?? "#c9ced6", v, tr);
                    part.Remove("v"); part.Remove("t");
                    part["v64"] = written["v64"]!.DeepClone();
                    part["t64"] = written["t64"]!.DeepClone();
                }
                break;
            case JsonObject o:
                foreach (var (_, x) in o) Body(x, m);
                break;
        }
    }

    static void Flat(JsonArray? pts, double[] m)
    {
        if (pts == null) return;
        for (var i = 0; i + 1 < pts.Count; i += 2)
        {
            var p = StoredBodies.Apply(m, new Vec3(Json.Number(pts[i]) ?? 0, Json.Number(pts[i + 1]) ?? 0, 0));
            pts[i] = Math.Round(p.X, 4);
            pts[i + 1] = Math.Round(p.Y, 4);
        }
    }
}
