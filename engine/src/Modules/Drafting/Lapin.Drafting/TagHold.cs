using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A tag follows its element (tester's report #93), as Revit's and ArchiCAD's do. A tag keeps, beside its points (its
/// head <c>at</c>, its leader's <c>elbow</c> and <c>end</c>, in the view's coordinates), where its element stood when
/// they were set (<c>hold</c>): a point and a direction of the element's in the model (a path element's axis at a
/// fraction of its length, the leader's end's or its middle; a door's or window's place on its wall; a placed
/// element's point and rotation; an outline's centroid; else its body's). Each time the view is drawn, the points
/// go where the element has taken that frame since: turned and moved with it in a plan, moved as the view sees it
/// in a section or an elevation. So a tag follows a move, a turn, a grip dragged, a type changed, every view, sheet
/// and PDF alike, and a tag moved by hand keeps its own offset. A move, turn or mirror of the element carries the
/// tags of it in plans exactly (<see cref="TagFollowers"/>: a mirrored element's tag is mirrored too).
/// </summary>
public static class TagHold
{
    public const string Key = "hold";

    /// <summary>
    /// A spot on a path element's axis: a fraction of its length (<c>f</c>, its middle for a tag that points there), or a
    /// distance from its start (<c>s</c>) or its end (<c>e</c>), the nearer, for a leader pointed at a spot of it: dragging
    /// the other end leaves it there.
    /// </summary>
    public readonly record struct Spot(string On, double V)
    {
        public double Along(PathCurve c) => On switch
        {
            "s" => Math.Clamp(V, 0, c.Length),
            "e" => Math.Clamp(c.Length - V, 0, c.Length),
            _ => Math.Clamp(V, 0, 1) * c.Length,
        };
    }

    /// <summary>Where an element stands: a point, a plan direction (radians), and on a path element the spot of its axis.</summary>
    public readonly record struct Frame(Vec3 P, double A, Spot? On);

    static Vec2 Pt(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    static bool IsPath(Entity e) => e.Class != IfcClass.Annotation && e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray;

    /// <summary>The element and what it moves with: a door or window, its opening, the wall the opening is in.</summary>
    public static IEnumerable<string> Chain(IModelView m, Entity e)
    {
        yield return e.Id;
        var opening = e.Class == IfcClass.OpeningElement ? e : m.RelatingOf(e.Id, IfcClass.RelFillsElement);
        if (opening == null) yield break;
        if (opening.Id != e.Id) yield return opening.Id;
        if (m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is { } host) yield return host.Id;
    }

    /// <summary>Whether the element moves when <paramref name="set"/> does: it, or what it is placed in.</summary>
    public static bool Moves(IModelView m, string? id, IReadOnlySet<string> set) =>
        id != null && (set.Contains(id) || (m.Get(id) is { } e && Chain(m, e).Any(set.Contains)));

    /// <summary>Where the element stands now; null when nothing says it (no geometry given for one only its body places).</summary>
    public static Frame? Of(IModelView m, Entity e, Spot? on, Func<string, ElementGeometry?>? geometry)
    {
        var chain = Chain(m, e).ToList();
        var z = chain.Select(id => Spatial.StoreyOf(m, id)).FirstOrDefault(s => s != null)?.Num("elevation") ?? 0;
        if (IsPath(e))
        {
            var c = PathCurve.Of(e);
            var spot = on ?? new Spot("f", 0.5);
            return OnAxis(c, spot.Along(c), z, spot);
        }
        // A door, a window or an opening: its place along its wall.
        var opening = e.Class == IfcClass.OpeningElement ? e : m.RelatingOf(e.Id, IfcClass.RelFillsElement);
        if (opening?.NumOrNull("along") is { } along && m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is { } wall && IsPath(wall))
            return OnAxis(PathCurve.Of(wall), along, z, null);
        if (e.Attrs["at"] is JsonArray { Count: >= 2 } at)
        {
            var p = Pt(at);
            return new Frame(new Vec3(p.X, p.Y, z), (e.NumOrNull("rotation") ?? 0) * Math.PI / 180, null);
        }
        if (e.Attrs["contour"] is JsonArray contour && contour.OfType<JsonArray>().Select(Pt).ToList() is { Count: >= 3 } loop)
        {
            var c = Centroid(loop);
            return new Frame(new Vec3(c.X, c.Y, z), 0, null);
        }
        // Anything else: the middle of its body's corners (it moves with the body as a whole).
        if (geometry?.Invoke(e.Id) is { Instance: null, Parts.Count: > 0 } g)
        {
            var (sx, sy, sz, n) = (0.0, 0.0, 0.0, 0);
            foreach (var part in g.Parts)
            {
                if (part.Mesh is { } mesh)
                    for (var i = 0; i + 2 < mesh.Xyz.Length; i += 3) { sx += mesh.Xyz[i]; sy += mesh.Xyz[i + 1]; sz += mesh.Xyz[i + 2]; n++; }
                else
                    foreach (var v in part.Solid.Vertices) { sx += v.X; sy += v.Y; sz += v.Z; n++; }
            }
            if (n > 0) return new Frame(new Vec3(sx / n, sy / n, sz / n), 0, null);
        }
        return null;
    }

    static Frame OnAxis(PathCurve c, double s, double z, Spot? f)
    {
        var p = c.PointAt(s);
        var d = c.Length < 1e-9 ? new Vec2(1, 0) : c.TangentAt(s);
        return new Frame(new Vec3(p.X, p.Y, z), Math.Atan2(d.Y, d.X), f);
    }

    /// <summary>An outline's area centroid (its corners' middle when it has no area).</summary>
    static Vec2 Centroid(IReadOnlyList<Vec2> loop)
    {
        double a = 0, cx = 0, cy = 0;
        for (var i = 0; i < loop.Count; i++)
        {
            var (p, q) = (loop[i], loop[(i + 1) % loop.Count]);
            var cross = p.X * q.Y - q.X * p.Y;
            a += cross; cx += (p.X + q.X) * cross; cy += (p.Y + q.Y) * cross;
        }
        return Math.Abs(a) < 1e-12 ? new Vec2(loop.Average(p => p.X), loop.Average(p => p.Y)) : new Vec2(cx / (3 * a), cy / (3 * a));
    }

    /// <summary>The frame a tag's points were set by; null for a tag that has none (an older project's, its element gone).</summary>
    public static Frame? Held(Entity tag) =>
        tag.Attrs[Key] is JsonObject h && h["p"] is JsonArray { Count: >= 3 } p
            ? new Frame(new Vec3(Json.Number(p[0]) ?? 0, Json.Number(p[1]) ?? 0, Json.Number(p[2]) ?? 0), Json.Number(h["a"]) ?? 0,
                new[] { "f", "s", "e" }.Where(k => Json.Number(h[k]) != null).Select(k => (Spot?)new Spot(k, Json.Number(h[k])!.Value)).FirstOrDefault())
            : null;

    static JsonObject ToJson(Frame f)
    {
        var o = new JsonObject { ["p"] = new JsonArray(f.P.X, f.P.Y, f.P.Z), ["a"] = f.A };
        if (f.On is { } on) o[on.On] = on.V;
        return o;
    }

    /// <summary>A view's axes in the model (its x and y), and whether it is a plan; null for one whose axes are its own (across an element).</summary>
    public static (Vec3 Right, Vec3 Up, bool Plan)? Axes(IModelView m, Entity view)
    {
        switch (view.Str("kind") ?? "plan")
        {
            case "plan": return (new Vec3(1, 0, 0), new Vec3(0, 1, 0), true);
            case "section" or "elevation" when !AcrossSections.Is(view) && Views.Look(view) is { } look:
                return (new Vec3(look.Y, -look.X, 0), Vec3.UnitZ, false);
            case RefPlanes.Kind:
                var frame = RefPlanes.Spec(view, false).Frame;
                return (frame.Right, frame.Up, false);
            default: return null;
        }
    }

    /// <summary>Where the element has taken the points since <paramref name="was"/>: turned and moved with it in a plan, moved as the view sees it elsewhere; null when it has not moved.</summary>
    public static Func<Vec2, Vec2>? Shift(Frame was, Frame now, Vec3 right, Vec3 up, bool plan)
    {
        if (plan)
        {
            var (o, n) = (new Vec2(was.P.X, was.P.Y), new Vec2(now.P.X, now.P.Y));
            var da = Math.IEEERemainder(now.A - was.A, 2 * Math.PI);
            if ((n - o).Length < 1e-9 && Math.Abs(da) < 1e-9) return null;
            var (c, s) = (Math.Cos(da), Math.Sin(da));
            return p => { var v = p - o; return n + new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c); };
        }
        var d = now.P - was.P;
        var by = new Vec2(Vec3.Dot(d, right), Vec3.Dot(d, up));
        return by.Length < 1e-9 ? null : p => p + by;
    }

    /// <summary>Where a tag's points are now, its element having moved since they were set; null when they are where they were.</summary>
    public static Func<Vec2, Vec2>? Follow(IModelView m, Entity tag, Entity? e, Vec3 right, Vec3 up, bool plan, Func<string, ElementGeometry?>? geometry) =>
        e != null && Held(tag) is { } was && Of(m, e, was.On, geometry) is { } now ? Shift(was, now, right, up, plan) : null;

    /// <summary>
    /// The tag's frame set to its element's now (<paramref name="attrs"/>: the tag's): on a path element, where the leader's
    /// end is along the axis in a plan (from the nearer end), else at its middle. One only its body places, with no geometry to read
    /// (being moved), keeps its frame carried by <paramref name="carry"/>, or as it was.
    /// </summary>
    public static void Hold(IModelView m, JsonObject attrs, Entity? e, bool plan, Func<string, ElementGeometry?>? geometry, Placement2D? carry = null)
    {
        if (e == null) { attrs.Remove(Key); return; }
        Spot? f = null;
        if (IsPath(e))
        {
            var c = PathCurve.Of(e);
            var s = plan && attrs["end"] is JsonArray end && c.Length > 1e-9 ? (double?)Math.Clamp(c.Project(Pt(end)), 0, c.Length) : null;
            f = s is not { } at ? new Spot("f", 0.5) : at <= c.Length / 2 ? new Spot("s", at) : new Spot("e", c.Length - at);
        }
        if (Of(m, e, f, geometry) is { } now) { attrs[Key] = ToJson(now); return; }
        if (carry != null && attrs[Key] is JsonObject && Held(new Entity("", IfcClass.Annotation, "", attrs)) is { } was)
        {
            var p = carry.Point(new Vec2(was.P.X, was.P.Y));
            attrs[Key] = ToJson(was with { P = new Vec3(p.X, p.Y, was.P.Z) });
        }
    }

    /// <summary>The tag's attributes with its points where its element has taken them, and its frame the element's now.</summary>
    public static JsonObject Baked(IModelView m, Entity tag, Func<string, ElementGeometry?>? geometry)
    {
        var attrs = tag.CloneAttrs();
        var e = m.Get(tag.Str("element") ?? "");
        if (e == null || Views.Find(m, tag.Str("viewId") ?? "") is not { } view || Axes(m, view) is not { } ax) return attrs;
        if (Follow(m, tag, e, ax.Right, ax.Up, ax.Plan, geometry) is { } map)
            foreach (var k in new[] { "at", "elbow", "end" })
                if (attrs[k] is JsonArray) attrs[k] = J(map(Pt(attrs[k])));
        Hold(m, attrs, e, ax.Plan, geometry);
        return attrs;
    }

    /// <summary>Whether a tag's view is a plan (an implicit storey plan too).</summary>
    public static bool InPlan(IModelView m, Entity tag) => Views.Find(m, tag.Str("viewId") ?? "") is { } v && Axes(m, v) is { Plan: true };

    public static bool IsTag(Entity e) => e.Class == IfcClass.Annotation && e.PredefinedType == "TAG";
}

/// <summary>
/// The tags of elements element.transform carries (tester's report #93): set to their elements' frames before
/// anything moves; then, in plans, carried with their element exactly (a turn turns them, a mirror mirrors them); in
/// sections and elevations the drawing moves them as the element moved. A tag copied with its element tags the copy.
/// </summary>
public sealed class TagFollowers : ITransformFollowers
{
    static Vec2 Pt(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    public object? Before(ICommandContext ctx, IReadOnlySet<string> moving, IReadOnlySet<string> selected)
    {
        var m = ctx.Model;
        var touched = new List<string>();
        foreach (var t in m.OfClass(IfcClass.Annotation).Where(TagHold.IsTag).ToList())
        {
            if (!selected.Contains(t.Id) && !TagHold.Moves(m, t.Str("element"), selected)) continue;
            var baked = TagHold.Baked(m, t, ctx.Query.Geometry);
            if (!JsonNode.DeepEquals(baked, t.Attrs)) ctx.Replace(t.With(attrs: baked));
            touched.Add(t.Id);
        }
        return touched;
    }

    public void After(ICommandContext ctx, object? state, Placement2D t, Placement2D plan, IReadOnlySet<string> moving, IReadOnlySet<string> selected, IReadOnlyDictionary<string, string> copies)
    {
        if (state is not List<string> touched) return;
        var m = ctx.Model;
        // A copy leaves the elements where they are: only the tags copied with them change.
        var copying = copies.Count > 0;
        foreach (var id in touched)
        {
            if (m.Get(id) is not { } tag) continue;
            var e = m.Get(tag.Str("element") ?? "");
            var inPlan = TagHold.InPlan(m, tag);
            var rigid = !copying && e != null && TagHold.Moves(m, e.Id, moving);
            var attrs = tag.CloneAttrs();
            if (!selected.Contains(id))
            {
                // Elsewhere than a plan, or sliding in its host: its frame stays the one before, and the drawing moves it.
                if (!(rigid && inPlan)) continue;
                foreach (var k in new[] { "at", "elbow", "end" })
                    if (attrs[k] is JsonArray) attrs[k] = J(plan.Point(Pt(attrs[k])));
            }
            TagHold.Hold(m, attrs, e, inPlan, null, rigid ? plan : null);
            if (!JsonNode.DeepEquals(attrs, tag.Attrs)) ctx.Replace(tag.With(attrs: attrs));
            // A tag copied: with its element, it tags the copy, its leader's end carried too.
            if (copies.TryGetValue(id, out var cid) && m.Get(cid) is { } twin)
            {
                var ca = twin.CloneAttrs();
                var host = e;
                if (e != null && copies.TryGetValue(e.Id, out var ce) && m.Get(ce) is { } copied)
                {
                    ca["element"] = copied.Id;
                    host = copied;
                    if (tag.Attrs["end"] is JsonArray end) ca["end"] = J(t.Point(Pt(end)));
                    ca[TagHold.Key] = attrs[TagHold.Key]?.DeepClone();
                    if (ca[TagHold.Key] == null) ca.Remove(TagHold.Key);
                    TagHold.Hold(m, ca, host, inPlan, null, plan);
                }
                else TagHold.Hold(m, ca, host, inPlan, null);
                ctx.Replace(twin.With(attrs: ca));
            }
        }
    }
}

/// <summary>An older project's tags, which kept no frame of their element: they take its frame as the project opens, and follow it from there.</summary>
public sealed class TagHoldUpgrade : IProjectUpgrade
{
    public (IReadOnlyList<Entity> Entities, IReadOnlyList<Relation> Relations) Upgrade(IReadOnlyList<Entity> entities, IReadOnlyList<Relation> relations)
    {
        if (!entities.Any(e => TagHold.IsTag(e) && e.Attrs[TagHold.Key] == null && e.Str("element") != null)) return (entities, relations);
        var store = new ModelStore();
        new History(store).Execute("Open", edit =>
        {
            foreach (var e in entities) edit.Add(e);
            foreach (var r in relations) edit.AddRelation(r);
        });
        var m = (IModelView)store;
        var list = entities.Select(e =>
        {
            if (!TagHold.IsTag(e) || e.Attrs[TagHold.Key] != null) return e;
            var attrs = e.CloneAttrs();
            TagHold.Hold(m, attrs, m.Get(e.Str("element") ?? ""), TagHold.InPlan(m, e), null);
            return attrs[TagHold.Key] == null ? e : e.With(attrs: attrs);
        }).ToList();
        return (list, relations);
    }
}
