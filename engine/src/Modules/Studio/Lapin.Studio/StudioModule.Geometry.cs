using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drafting.Cad;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

public sealed partial class StudioModule
{
    /// <summary>
    /// The booleans as the last build with services had them: a cut priority asks for the envelope without
    /// services, and a solid's steps (push/pull, fillet…) still need them there.
    /// </summary>
    static ISolidBooleans? _booleans;

    /// <summary>
    /// A shell's inner body: the operation done again with its outline moved in by the thickness (its holes out),
    /// and its ends moved in by as much where they stay closed, or run a hair past where they open
    /// (<c>start</c>: the sketch's end; <c>end</c>: the far one; <c>both</c>, <c>none</c>).
    /// </summary>
    sealed record Inner(double By, bool OpenStart, bool OpenEnd);

    const double Past = 1e-3;

    public static ElementGeometry? Geometry(Entity e, IModelView m, IServices? services)
    {
        if (Thick.Is(e.Str("operation")) || e.Str("operation") == Thick.BoxSlab) return ThickGeometry(e, m, services);
        if (services?.Get<ISolidBooleans>() is { } b) _booleans = b;
        var booleans = services?.Get<ISolidBooleans>() ?? _booleans;
        var (solid, sketchPlan, problem) = Base(e, m, services, null);
        if (solid == null) return new ElementGeometry([], Diagnostics: [problem ?? "The solid could not be built from its sketch."]);
        var problems = new List<string>();
        var features = e.Attrs["features"] as JsonArray;
        if (features is { Count: > 0 })
        {
            var (done, issues) = Features.Apply(solid, features, new Features.Context(booleans, f =>
            {
                var t = Json.Number(f["thickness"]) ?? 0;
                if (t <= 1e-6) return (null, "a shell needs a thickness.");
                var open = (string?)f["open"] ?? "none";
                var (inner, _, why) = Base(e, m, services, new Inner(t, open is "start" or "both", open is "end" or "both"));
                return (inner, why);
            }));
            problems.AddRange(issues);
            if (done == null) return new ElementGeometry([], Diagnostics: problems.Count > 0 ? problems : ["Nothing is left of the solid."]);
            solid = done;
        }
        if (Placed(e) is { } xf) solid = StoredBodies.Place(solid, xf);
        var material = e.Str("material");
        // In plan: the outline of what it covers (its sketch, when an extrusion drawn in plan is all it is; else its shadow's hull).
        var plan = new List<PlanItem>();
        if (sketchPlan != null && features is not { Count: > 0 } && Placed(e) == null) plan.AddRange(sketchPlan);
        else if (Polygon2.ConvexHull(solid.Vertices.Select(v => new Vec2(v.X, v.Y))) is { Length: >= 3 } hull) plan.Add(new PlanItem("outline", true, hull));
        var parts = new List<GeometryPart> { new(e.Name, material != null ? Materials.ColorOf(m, material) : "#b9b2a6", solid, material, "body") };
        // Cut by what ranks above it (walls through a pavement…); its envelope for others is the uncut solid. A void
        // only cuts what it is a tool of: nothing ranks over it.
        if (!IsVoid(e)) parts = CutPriorities.Apply(e, m, services, parts, problems);
        return new ElementGeometry(parts, plan, plan.SelectMany(p => p.Points).ToList(), problems.Count > 0 ? problems : null);
    }

    /// <summary>Where a solid drawn on a section, an elevation or a reference plane was moved or turned to in plan (12 numbers, as an instance's).</summary>
    public static double[]? Placed(Entity e) =>
        e.Attrs["placed"] is JsonArray a && a.Count == 12 ? a.Select(x => Json.Number(x) ?? 0).ToArray() : null;

    /// <summary>
    /// The body the operation makes from the sketch (or, without one, from its own numbers); with <paramref name="inner"/>,
    /// a shell's inner body. In plan, an extrusion's outline when it was drawn in plan and stands straight up.
    /// </summary>
    static (Solid? Solid, List<PlanItem>? Plan, string? Problem) Base(Entity e, IModelView m, IServices? services, Inner? inner)
    {
        var op = e.Str("operation");
        switch (op)
        {
            case "primitive":
            {
                var (body, why) = PrimitiveBody(e, inner);
                return (body, null, why);
            }
            case "prism":
            {
                if (inner != null) return (null, null, "a face pushed or pulled on another element is not hollowed.");
                if (PointList(e.Attrs["outline"]) is not { Count: >= 3 } ring || Features.P(e.Attrs["normal"]) is not { } n || n.Length < 1e-9) return (null, null, "The face it was made from is missing.");
                var holes = (e.Attrs["holes"] as JsonArray)?.Select(PointList).OfType<List<Vec3>>().Select(h => (IReadOnlyList<Vec3>)h).ToList() ?? [];
                var d = e.Num("depth");
                if (Math.Abs(d) < 1e-9) return (null, null, "The push or pull has no distance.");
                var u = n.Normalized();
                const double eps = 1e-4;
                // Into the element a hair (a pull, so the two share volume), or out of it a hair (a push, so no face is shared).
                var nudge = d > 0 ? -u * eps : u * eps;
                var solid = Prism.Extrude(ring.Select(p => p + nudge).ToList(), holes.Select(h => (IReadOnlyList<Vec3>)h.Select(p => p + nudge).ToList()).ToList(), u * (d > 0 ? d + eps : d - eps), "studio");
                return (solid, null, solid == null ? "The face could not be pushed or pulled." : null);
            }
            case "body":
            {
                if (inner != null) return (null, null, "a body made from an element or a mesh is not hollowed.");
                if (e.Attrs[BodyKey] is not JsonObject body) return (null, null, "The body is missing.");
                var solid = BodyOf(body);
                if (solid == null) return (null, null, "The body could not be read.");
                var at = Features.P(e.Attrs["at"]) ?? Vec3.Zero;
                var rot = e.Num("rotation") * Math.PI / 180;
                var (rc, rs) = (Math.Cos(rot), Math.Sin(rot));
                return (SolidBuilder.Transform(solid, p => new Vec3(at.X + p.X * rc - p.Y * rs, at.Y + p.X * rs + p.Y * rc, at.Z + p.Z)), null, null);
            }
        }

        var profile = m.Get(e.Str("profile") ?? "");
        if (profile == null) return (null, null, "The solid's sketch is gone: draw its outline again.");
        var outline = Sketches.OutlineOf(m, profile);
        if (outline == null) return (null, null, "The solid's outline is no longer closed.");
        var outer2 = outline.Loop;
        var (plane, planeProblem) = PlaneOfSketch(m, services, profile);
        if (plane == null) return (null, null, planeProblem);
        var holes2 = Sketches.HoleOutlines(m, profile, ProfileIds(m)).Select(h => h.Loop).ToList();
        if (inner != null)
        {
            if (Solids.Inset(outer2, inner.By) is not { } io) return (null, null, "the walls are too thick for the outline.");
            var ih = new List<IReadOnlyList<Vec2>>();
            foreach (var h in holes2) { if (Solids.Inset(h, -inner.By) is not { } x) return (null, null, "the walls are too thick round a hole."); ih.Add(x); }
            (outer2, holes2) = (io, ih);
        }
        var shift = plane.Normal * e.Num("offset");
        List<Vec3> W(IReadOnlyList<Vec2> l) => l.Select(p => plane.World(p) + shift).ToList();
        switch (op)
        {
            case "extrude":
            {
                var depth = e.NumOrNull("depth") ?? 1;
                var start = Flag(e.Attrs["symmetric"]) ? -depth / 2 : 0.0;
                var n = plane.Normal;
                if (e.Attrs["upTo"] is JsonObject upTo)
                {
                    // Up to a face of another element: found again on that element as it is now.
                    if (m.Get((string?)upTo["id"] ?? "") is not { } target) return (null, null, "The element it runs up to is gone.");
                    var producers = services?.Get<IGeometryProducers>();
                    var parts = producers?.Geometry(target.Class)?.Invoke(target, m, services!)?.Parts.Select(p => p.Solid).ToList() ?? [];
                    if (Features.P(upTo["point"]) is not { } at || Features.P(upTo["normal"]) is not { } fn) return (null, null, "The face it runs up to is not given.");
                    var face = parts.Count > 0 ? BrepPick.Face(parts, at, fn, double.PositiveInfinity) : null;
                    var (q, qn) = face != null ? (face.Outer[0], face.Normal) : (at, fn.Normalized());
                    var dir = Math.Sign(depth == 0 ? 1 : depth) * n;
                    if (Vec3.Dot(q - (plane.Origin + shift), dir) < 0) dir = -dir;
                    var lean = Vec3.Dot(dir, qn) > 0 ? 1 : -1;
                    if (inner != null) q -= qn * lean * (inner.OpenEnd ? -Past : inner.By);
                    var baseShift = inner == null ? Vec3.Zero : dir * (inner.OpenStart ? -Past : inner.By);
                    var to = Solids.ExtrudeToPlane(W(outer2).Select(p => p + baseShift).ToList(), holes2.Select(h => (IReadOnlyList<Vec3>)W(h).Select(p => p + baseShift).ToList()).ToList(), dir, q, qn);
                    return (to.Solid, null, to.Problem);
                }
                var (s0, s1) = (start, start + depth);
                if (inner != null)
                {
                    var sg = Math.Sign(depth);
                    s0 += sg * (inner.OpenStart ? -Past : inner.By);
                    s1 -= sg * (inner.OpenEnd ? -Past : inner.By);
                    if (Math.Sign(s1 - s0) != sg) return (null, null, "the walls are thicker than the solid is deep.");
                }
                var taper = e.Num("taper");
                if (Math.Abs(taper) > 1e-9)
                {
                    var o2 = outer2;
                    var h2 = holes2;
                    // A shell's inner body leans as the outer does: its base moved in by as much more as the outer leans by its start.
                    if (inner != null)
                    {
                        var lean = (s0 - start) * Math.Sign(depth) * Math.Tan(taper * Math.PI / 180);
                        if (Solids.Inset(o2, lean) is not { } lo) return (null, null, "the walls are too thick for the outline.");
                        o2 = lo;
                        h2 = h2.Select(h => Solids.Inset(h, -lean)).OfType<IReadOnlyList<Vec2>>().ToList();
                    }
                    var tap = Solids.ExtrudeTapered(o2, h2, p => plane.World(p) + shift + n * s0, n * (s1 - s0), taper);
                    return (tap.Solid, null, tap.Problem);
                }
                var solid = Solids.Extrude(W(outer2).Select(p => p + n * s0).ToList(), holes2.Select(h => (IReadOnlyList<Vec3>)W(h).Select(p => p + n * s0).ToList()).ToList(), n * (s1 - s0));
                List<PlanItem>? plan = null;
                if (inner == null && Math.Abs(plane.Normal.Z) > 0.999)
                {
                    plan = [new PlanItem("outline", true, outer2)];
                    plan.AddRange(holes2.Select(h => new PlanItem("outline", true, h)));
                }
                return (solid, plan, solid == null ? "The extrusion could not be built." : null);
            }
            case "sweep":
            {
                if (m.Get(e.Str("guide") ?? "") is not { } g || Drafts.ShapeOf(g) is not { } gs) return (null, null, "The path is missing: draw it again to rebuild.");
                var path = Geo.Polylines(gs, Math.PI / 24)[0].Select(p => plane.World(p) + shift).ToList();
                // The outline's own box places an inner (inset) profile, so it sits where the outline does.
                var own = Sketches.OutlineOf(m, profile)!.Loop;
                var box = (own.Min(p => p.X), own.Max(p => p.X), own.Min(p => p.Y));
                if (inner != null)
                {
                    var closedPath = path.Count >= 4 && (path[^1] - path[0]).Length < 1e-6;
                    if (!closedPath) path = Trim(path, inner.OpenStart ? -Past : inner.By, inner.OpenEnd ? -Past : inner.By);
                    if (path.Count < 2) return (null, null, "the walls are thicker than the path is long.");
                }
                Solids.SweepResult swept;
                if (e.Str("endProfile") is { } endId)
                {
                    if (m.Get(endId) is not { } endDraft || Sketches.OutlineOf(m, endDraft)?.Loop is not { } endLoop) return (null, null, "The end profile is missing or no longer closed: draw it again to rebuild.");
                    var endRef = endLoop;
                    if (inner != null && Solids.Inset(endLoop, inner.By) is { } ie) endLoop = ie;
                    swept = Solids.SweepMorph(outer2, endLoop, path, plane.Normal, own, endRef);
                }
                else if (e.Str("rail") is { } railId)
                {
                    if (m.Get(railId) is not { } railDraft || Drafts.ShapeOf(railDraft) is not { } rs) return (null, null, "The second rail is missing: draw it again to rebuild.");
                    var rail = Geo.Polylines(rs, Math.PI / 24)[0].Select(p => plane.World(p) + shift).ToList();
                    swept = Solids.SweepRails(outer2, holes2, path, rail, plane.Normal, box);
                }
                else swept = Solids.SweepPlaced(outer2, holes2, path, plane.Normal, e.Str("place") ?? "centre", e.Num("placeOffset"),
                    new Solids.SweepShape(e.Num("twist"), e.NumOrNull("scaleEnd") ?? 1, e.Num("rise"), e.NumOrNull("turns") ?? 1, box));
                return (swept.Solid, null, swept.Problem);
            }
            case "loft":
            {
                var loops = new List<IReadOnlyList<Vec2>>();
                var worlds = new List<Func<Vec2, Vec3>>();
                foreach (var id in (e.Attrs["profiles"] as JsonArray)?.Select(x => (string?)x).OfType<string>() ?? [])
                {
                    if (m.Get(id) is not { } d || Sketches.OutlineOf(m, d)?.Loop is not { } loop) return (null, null, "A profile of the loft is missing or no longer closed.");
                    if (PlaneOfSketch(m, services, d).Plane is not { } pl) return (null, null, "A profile's view is gone, or lies on no plane.");
                    if (inner != null) { if (Solids.Inset(loop, inner.By) is not { } il) return (null, null, "the walls are too thick for a profile."); loop = il; }
                    loops.Add(loop);
                    worlds.Add(q => pl.World(q) + shift);
                }
                if (inner != null && worlds.Count >= 2)
                {
                    // Closed ends moved into the loft by the thickness, open ones a hair past.
                    var first = worlds[0](Vec2Mid(loops[0]));
                    var last = worlds[^1](Vec2Mid(loops[^1]));
                    var along = (last - first).Normalized();
                    var (w0, w1) = (worlds[0], worlds[^1]);
                    var (d0, d1) = (along * (inner.OpenStart ? -Past : inner.By), -along * (inner.OpenEnd ? -Past : inner.By));
                    worlds[0] = q => w0(q) + d0;
                    worlds[^1] = q => w1(q) + d1;
                }
                List<Vec3>? guide = null;
                if (e.Str("rail") is { } railId)
                {
                    if (m.Get(railId) is not { } rd || Drafts.ShapeOf(rd) is not { } rs || PlaneOfSketch(m, services, rd).Plane is not { } rp)
                        return (null, null, "The loft's guide is missing: draw it again to rebuild.");
                    guide = Geo.Polylines(rs, Math.PI / 24)[0].Select(p => rp.World(p)).ToList();
                }
                var lofted = Solids.LoftThrough(loops, worlds, guide, Flag(e.Attrs["smooth"]));
                return (lofted.Solid, null, lofted.Problem);
            }
            case "revolve":
            {
                if (m.Get(e.Str("guide") ?? "") is not { } g || Drafts.ShapeOf(g) is not Line ax) return (null, null, "The axis is missing: draw it again to rebuild.");
                var (a3, b3) = (plane.World(ax.A) + shift, plane.World(ax.B) + shift);
                var angle = e.NumOrNull("angle") ?? 360;
                var full = Math.Abs(Math.Abs(angle) - 360) < 1e-6;
                var turnStart = 0.0;
                if (inner != null && !full)
                {
                    // Closed ends turned in by the thickness at the profile's middle, open ones a hair past.
                    var mid = plane.World(Vec2Mid(outer2)) + shift;
                    var axis = (b3 - a3).Normalized();
                    var v = mid - a3;
                    var r = (v - axis * Vec3.Dot(v, axis)).Length;
                    if (r < 1e-6) return (null, null, "the profile lies on its axis.");
                    var sg = Math.Sign(angle);
                    var (ds, de) = ((inner.OpenStart ? -Past : inner.By) / r * 180 / Math.PI, (inner.OpenEnd ? -Past : inner.By) / r * 180 / Math.PI);
                    turnStart = sg * ds;
                    angle -= sg * (ds + de);
                    if (Math.Sign(angle) != sg) return (null, null, "the walls are thicker than the turn is long.");
                }
                var ring = W(outer2);
                var rh = holes2.Select(h => (IReadOnlyList<Vec3>)W(h)).ToList();
                if (Math.Abs(turnStart) > 1e-12)
                {
                    var axis = (b3 - a3).Normalized();
                    var t = turnStart * Math.PI / 180;
                    ring = ring.Select(p => Features.Turn(p, a3, axis, t)).ToList();
                    rh = rh.Select(h => (IReadOnlyList<Vec3>)h.Select(p => Features.Turn(p, a3, axis, t)).ToList()).ToList();
                }
                var solid = Solids.Revolve(ring, rh, a3, b3 - a3, angle);
                return (solid, null, solid == null ? "The revolution could not be built." : null);
            }
        }
        return (null, null, $"„{op}” is not an operation Studio knows.");
    }

    static Vec2 Vec2Mid(IReadOnlyList<Vec2> l) => new((l.Min(p => p.X) + l.Max(p => p.X)) / 2, (l.Min(p => p.Y) + l.Max(p => p.Y)) / 2);

    /// <summary>A path cut back at its start and end by those lengths (or run on past them, when negative).</summary>
    static List<Vec3> Trim(List<Vec3> path, double atStart, double atEnd)
    {
        var p = path.ToList();
        if (p.Count < 2) return p;
        if (atStart < 0) p[0] -= (p[1] - p[0]).Normalized() * -atStart;
        if (atEnd < 0) p[^1] += (p[^1] - p[^2]).Normalized() * -atEnd;
        List<Vec3> Cut(List<Vec3> q, double by)
        {
            while (by > 1e-12 && q.Count >= 2)
            {
                var seg = (q[1] - q[0]).Length;
                if (seg > by + 1e-9) { q[0] += (q[1] - q[0]).Normalized() * by; return q; }
                by -= seg;
                q.RemoveAt(0);
            }
            return q;
        }
        if (atStart > 0) p = Cut(p, atStart);
        if (atEnd > 0) { p.Reverse(); p = Cut(p, atEnd); p.Reverse(); }
        return p.Count >= 2 ? p : [];
    }

    /// <summary>A primitive's body from its numbers; a shell's inner one smaller by the thickness (its ends moved in, or opened).</summary>
    static (Solid? Solid, string? Problem) PrimitiveBody(Entity e, Inner? inner)
    {
        var shape = e.Str("shape") ?? "box";
        var at = Features.P(e.Attrs["at"]) ?? Vec3.Zero;
        var (w, l, h, r, r2) = (e.Num("width"), e.Num("length"), e.Num("height"), e.Num("radius"), e.Num("radius2"));
        if (inner != null)
        {
            var t = inner.By;
            var (lo, hi) = (inner.OpenStart ? -Past : t, inner.OpenEnd ? -Past : t);
            switch (shape)
            {
                case "box": (w, l, h) = (w - 2 * t, l - 2 * t, h - lo - hi); at += new Vec3(0, 0, lo); break;
                case "cylinder": (r, h) = (r - t, h - lo - hi); at += new Vec3(0, 0, lo); break;
                case "cone": (r, r2, h) = (r - t, Math.Max(0, r2 - t), h - lo - hi); at += new Vec3(0, 0, lo); break;
                case "sphere": r -= t; at += new Vec3(0, 0, t); break;
                case "torus": r2 -= t; at += new Vec3(0, 0, t); break;
                default: return (null, $"a {shape} is not hollowed.");
            }
            if (w < 0 || l < 0 || h <= 0 && shape is "box" or "cylinder" or "cone" || r <= 0 && shape is "cylinder" or "cone" or "sphere" || r2 <= 0 && shape == "torus")
                return (null, "the walls are too thick for its size.");
        }
        var res = Solids.Primitive(shape, at, e.Num("rotation"), w, l, h, r, r2);
        return (res.Solid, res.Problem);
    }

    static List<Vec3>? PointList(JsonNode? n) => n is JsonArray a ? a.Select(Features.P).Where(p => p != null).Select(p => p!.Value).ToList() : null;

    // ------------------------------------------------------------ bodies made into Studio solids

    /// <summary>The attribute a body solid keeps its triangles in (StoredBodies' format, in its own coordinates): not sent to the interface.</summary>
    public const string BodyKey = StoredBodies.SolidBodyKey;

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonObject, Solid> _bodies = [];

    static Solid? BodyOf(JsonObject body)
    {
        if (_bodies.TryGetValue(body, out var known)) return known;
        var verts = new List<Vec3>();
        var tris = new List<(int, int, int)>();
        foreach (var p in (body["parts"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var (v, t) = StoredBodies.ReadPart(p);
            var off = verts.Count;
            for (var k = 0; k + 2 < v.Length; k += 3) verts.Add(new Vec3(v[k], v[k + 1], v[k + 2]));
            for (var k = 0; k + 2 < t.Length; k += 3) tris.Add((t[k] + off, t[k + 1] + off, t[k + 2] + off));
        }
        if (tris.Count == 0) return null;
        Solid? s;
        try { s = SolidFromMesh.Build(verts, tris, "studio"); } catch (InvalidOperationException) { s = null; }
        if (s == null || s.Faces.Length == 0) return null;
        _bodies.AddOrUpdate(body, s);
        return s;
    }
}
