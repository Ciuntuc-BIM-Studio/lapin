using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>What recognition from triangles made native, and why the rest stayed references (reason → how many).</summary>
public sealed record MeshRecognitionReport(int Recognized, IReadOnlyDictionary<string, int> ByClass, IReadOnlyDictionary<string, int> Skipped)
{
    public static readonly MeshRecognitionReport None = new(0, new Dictionary<string, int>(), new Dictionary<string, int>());

    public JsonObject ToJson()
    {
        static JsonObject Counts(IReadOnlyDictionary<string, int> d)
        {
            var o = new JsonObject();
            foreach (var (k, v) in d.OrderByDescending(kv => kv.Value)) o[k] = v;
            return o;
        }
        return new() { ["recognized"] = Recognized, ["byClass"] = Counts(ByClass), ["skipped"] = Counts(Skipped) };
    }
}

/// <summary>
/// Recognition from triangles (docs/spec-ifc.md, Recunoașterea din triunghiuri, decision 210): a reference whose body
/// is only triangles (a Fragments item, an IFC body no prism could be read from, one imported before) offered to the
/// same recognisers as an IFC file's elements. Its triangles are merged into planar faces; a prism among them is read
/// as IFC's would be. A wall with doors and windows is no prism: its envelope (the top cap swept down to the bottom
/// plane, every vertex inside it) is, and its openings are read from the faces inside it that face up or down — each
/// head over a sill or over the floor, between its jambs. A door or window reference standing in such an opening is
/// its fill, made native with the wall. What no recogniser takes comes back as the reference it was, in its group.
/// </summary>
public static class MeshRecognition
{
    static readonly HashSet<string> Fillers = [IfcClass.Door, IfcClass.Window];

    /// <summary>The references among <paramref name="ids"/> made native where a recogniser takes them (walls first, so their doors and windows go with them).</summary>
    public static MeshRecognitionReport Recognize(IEdit edit, IServices services, IEnumerable<string> ids)
    {
        var recognizers = services.All<IIfcRecognizer>();
        if (recognizers.Count == 0) return MeshRecognitionReport.None;
        var ctx = new RecognitionContext(services);
        var byClass = new Dictionary<string, int>();
        var count = 0;
        var refs = ids.Select(edit.Model.Get).OfType<Entity>().Where(e => StoredBodies.Has(e) && e.Str(Links.Key) == null && Spatial.StoreyOf(edit.Model, e.Id) != null).ToList();
        // The doors and windows, by their boxes, for the walls' openings; walls first.
        var fillers = refs.Where(e => Fillers.Contains(ClassOf(e))).Select(e => (E: e, Box: BoxOf(edit.Model, services, e))).Where(x => x.Box != null).ToList();
        var taken = new HashSet<string>();
        foreach (var e in refs.OrderBy(e => ClassOf(e) is IfcClass.Wall or "IfcWallStandardCase" ? 0 : Fillers.Contains(ClassOf(e)) ? 2 : 1))
        {
            if (taken.Contains(e.Id) || edit.Model.Get(e.Id) is not { } current || !StoredBodies.Has(current)) continue;
            if (StoredBodies.Geometry(current, edit.Model, services) is not { Parts.Count: > 0 } g) { ctx.Skip("no body"); continue; }
            // Each part (a layer) read on its own, a prism or a wall's envelope with its openings; the layers then joined: stacked
            // (a slab's) into one taller prism, side by side (a wall's) into one wider, when they tile its outline exactly.
            var storey = Spatial.StoreyOf(edit.Model, current.Id)!.Id;
            var cls = ClassOf(current);
            var wallLike = cls is IfcClass.Wall or "IfcWallStandardCase";
            // A wall's layers are the parts standing its whole height; a piece inside it (a lintel over an opening) is left to the wall.
            var (pz0, pz1) = (g.Parts.Min(p => p.Solid.Vertices.Min(q => q.Z)), g.Parts.Max(p => p.Solid.Vertices.Max(q => q.Z)));
            var layers = wallLike ? [.. g.Parts.Where(p => p.Solid.Vertices.Min(q => q.Z) < pz0 + 2e-3 && p.Solid.Vertices.Max(q => q.Z) > pz1 - 2e-3)] : g.Parts.ToList();
            var read = layers.Select(p => PartShape(p.Solid, wallLike, cls == IfcClass.Slab)).ToList();
            var prisms = new List<ShapePrism>();
            var openings = new List<ForeignOpening>();
            var fills = new List<Entity>();
            if (read.Count > 0 && read.All(x => x != null) && Join([.. read.Select(x => x!.Value.Prism)]) is { } joined)
            {
                prisms.Add(joined);
                // A piece inside the wall over an opening (its lintel) lowers the opening's head to its underside.
                var undersides = g.Parts.Except(layers).SelectMany(p => p.Solid.Faces.Where(f => f.Normal.Z < -0.999).Select(f => f.Outer.Select(i => p.Solid.Vertices[i]).ToList())).ToList();
                foreach (var h in JoinHoles([.. read.SelectMany(x => x!.Value.Holes)]).Where(h => !Pocket(h, undersides)).Select(h => UnderPieces(h, undersides)))
                {
                    var fill = fillers.FirstOrDefault(f => !taken.Contains(f.E.Id) && !fills.Contains(f.E) && Inside(f.Box!.Value, h.Box));
                    ForeignFill? ff = null;
                    if (fill.E != null)
                    {
                        // As large as its opening (its own box takes its frame's overlap and architraves too).
                        ff = new ForeignFill(fill.E.Id, ClassOf(fill.E), fill.E.Name, h.Width, h.Height, null, null, null, Properties(fill.E));
                        fills.Add(fill.E);
                    }
                    openings.Add(new ForeignOpening(edit.NewId(), null, [h.Prism], ff));
                }
            }
            var all = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
            if (prisms.Count == 0) { ctx.Skip("its body is no prism"); continue; }
            var box = (new Vec3(all.Min(p => p.X), all.Min(p => p.Y), all.Min(p => p.Z)), new Vec3(all.Max(p => p.X), all.Max(p => p.Y), all.Max(p => p.Z)));
            var foreign = new ForeignElement(current.Id, cls, current.PredefinedType, current.Name, current.Str("objectType"), storey, prisms, null, openings,
                null, null, null, null, Properties(current), Box: box);
            // The reference and its fills go; a recogniser makes them anew with the same ids, or they come back as they were.
            var saved = new[] { current }.Concat(fills).Select(x => (E: x, By: Related(edit.Model, x.Id))).ToList();
            foreach (var (x, _) in saved) edit.Remove(x.Id);
            string? made = null;
            foreach (var r in recognizers)
            {
                try { made = r.Recognize(edit, foreign, ctx); }
                catch (CommandException ex) { ctx.Skip(ex.Message); made = null; }
                if (made != null) break;
            }
            foreach (var (x, by) in saved)
            {
                if (edit.Model.Get(x.Id) is { } now)
                {
                    // Made native: what the file said of it kept for the inspector, its group and the rest as they were.
                    var attrs = now.CloneAttrs();
                    attrs["imported"] = new JsonObject { ["ifcClass"] = ClassOf(x), ["name"] = x.Name, ["properties"] = Properties(x) };
                    edit.Replace(now.With(attrs: attrs));
                    foreach (var (c, r) in by) if (c != IfcClass.RelContainedInSpatialStructure && edit.Model.Get(r) != null) edit.RelateAll(c, r, [x.Id]);
                    taken.Add(x.Id);
                    count++;
                    byClass[ClassOf(x)] = byClass.GetValueOrDefault(ClassOf(x)) + 1;
                }
                else
                {
                    edit.Add(x);
                    foreach (var (c, r) in by) if (edit.Model.Get(r) != null) edit.RelateAll(c, r, [x.Id]);
                }
            }
        }
        foreach (var r in recognizers) r.Finish(edit, ctx);
        return new(count, byClass, ctx.Skipped);
    }

    /// <summary>The class a reference stands for: its own, or the one it exports as.</summary>
    static string ClassOf(Entity e) => e.Str("exportAs") ?? e.Class;

    static JsonObject Properties(Entity e) => e.Attrs["properties"] is JsonObject p ? (JsonObject)p.DeepClone() : new JsonObject();

    static List<(int, int, int)> Triangles(IReadOnlyList<GeometryPart> parts)
    {
        var o = new List<(int, int, int)>();
        var off = 0;
        foreach (var p in parts)
        {
            o.AddRange(Measure.Triangles(p.Solid).Select(t => (t.Item1 + off, t.Item2 + off, t.Item3 + off)));
            off += p.Solid.Vertices.Length;
        }
        return o;
    }

    static (Vec3 Min, Vec3 Max)? BoxOf(IModelView m, IServices services, Entity e)
    {
        if (StoredBodies.Geometry(e, m, services) is not { Parts.Count: > 0 } g) return null;
        var v = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        return (new Vec3(v.Min(p => p.X), v.Min(p => p.Y), v.Min(p => p.Z)), new Vec3(v.Max(p => p.X), v.Max(p => p.Y), v.Max(p => p.Z)));
    }

    /// <summary>Whether a door's or window's box stands in an opening's: its middle inside, give or take a frame.</summary>
    static bool Inside((Vec3 Min, Vec3 Max) fill, (Vec3 Min, Vec3 Max) hole)
    {
        var c = (fill.Min + fill.Max) * 0.5;
        const double slack = 0.15;
        return c.X >= hole.Min.X - slack && c.X <= hole.Max.X + slack && c.Y >= hole.Min.Y - slack && c.Y <= hole.Max.Y + slack && c.Z >= hole.Min.Z - slack && c.Z <= hole.Max.Z + slack;
    }

    sealed record Hole(ShapePrism Prism, double Width, double Height, (Vec3 Min, Vec3 Max) Box);

    /// <summary>A part's shape: the one prism it is (a vertical one first), or a wall layer's envelope and its openings.</summary>
    static (ShapePrism Prism, List<Hole> Holes)? PartShape(Solid s, bool wall, bool flat = false)
    {
        // A wall's layer is read standing: a vertical prism, else its envelope with its openings (a plaster layer with a window is
        // also a thin prism lying across the wall, which says nothing of where the wall stands).
        var prisms = IfcModel.PrismsOfSolid(s);
        if (prisms.FirstOrDefault(p => p.Vertical) is { } up) return (up, []);
        if (wall) return Envelope(s);
        if (prisms.FirstOrDefault() is { } p) return (p, []);
        return flat && TopOutline(s) is { } top ? (top, []) : null;
    }

    /// <summary>
    /// A body that is no prism but lies flat (a slab with chamfered or sloping edges, a landing): its one top face, with its
    /// holes, swept down to its lowest point, when every vertex stands under that face in plan and only the top and the
    /// bottom are level.
    /// </summary>
    static ShapePrism? TopOutline(Solid s)
    {
        const double tol = 2e-3;
        var v = s.Vertices;
        var (z0, z1) = (v.Min(p => p.Z), v.Max(p => p.Z));
        if (z1 - z0 < 0.01) return null;
        List<Vec3> Loop(int[] idx) => [.. idx.Select(i => v[i])];
        var tops = s.Faces.Where(f => f.Normal.Z > 0.999 && Loop(f.Outer).All(p => Math.Abs(p.Z - z1) < tol)).ToList();
        if (tops.Count != 1) return null;
        // Level faces only at the top and the bottom: a step in between is another shape.
        if (s.Faces.Any(f => Math.Abs(f.Normal.Z) > 0.999 && Loop(f.Outer).All(p => p.Z > z0 + tol && p.Z < z1 - tol))) return null;
        var outline = Footprints.Plan(Loop(tops[0].Outer));
        if (outline.Count < 3 || !v.All(p => InPolygon(outline, new Vec2(p.X, p.Y), tol))) return null;
        var holes = tops[0].Holes.Select(h => (IReadOnlyList<Vec3>)[.. Footprints.Plan(Loop(h)).Select(q => new Vec3(q.X, q.Y, z0))]).ToList();
        return new ShapePrism([.. outline.Select(q => new Vec3(q.X, q.Y, z0))], holes, new Vec3(0, 0, z1 - z0), false, "mesh");
    }

    static double Area(IReadOnlyList<Vec3> loop)
    {
        double a = 0;
        for (var i = 0; i < loop.Count; i++) { var (p, q) = (loop[i], loop[(i + 1) % loop.Count]); a += p.X * q.Y - q.X * p.Y; }
        return Math.Abs(a) / 2;
    }

    /// <summary>
    /// The parts' prisms as one: alone, as it is; vertical ones on the same outline one over the other (a slab's layers), from
    /// the lowest to the highest; vertical ones at the same heights side by side (a wall's layers), their outlines' hull when
    /// they tile it exactly. Null when they are none of these.
    /// </summary>
    static ShapePrism? Join(List<ShapePrism> ps)
    {
        if (ps.Count == 1) return ps[0];
        if (!ps.All(p => p.Vertical && p.Holes.Count == 0)) return null;
        const double tol = 2e-3;
        var plans = ps.Select(p => Footprints.Plan(p.Outer)).ToList();
        bool SameOutline(List<Vec2> a, List<Vec2> b) => a.Count == b.Count && a.All(x => b.Any(y => (x - y).Length < tol));
        var (z0, z1) = (ps.Min(p => p.BaseZ), ps.Max(p => p.TopZ));
        if (plans.Skip(1).All(pl => SameOutline(pl, plans[0])))
        {
            // Stacked: the heights one after the other, with no gap.
            var ranges = ps.Select(p => (p.BaseZ, p.TopZ)).OrderBy(r => r.BaseZ).ToList();
            for (var i = 1; i < ranges.Count; i++) if (Math.Abs(ranges[i].BaseZ - ranges[i - 1].TopZ) > tol) return null;
            return new ShapePrism([.. plans[0].Select(q => new Vec3(q.X, q.Y, z0))], [], new Vec3(0, 0, z1 - z0), false, "mesh");
        }
        if (ps.All(p => Math.Abs(p.BaseZ - z0) < tol && Math.Abs(p.TopZ - z1) < tol))
        {
            var hull = Polygon2.ConvexHull(plans.SelectMany(pl => pl));
            if (hull is not { Length: >= 3 }) return null;
            var outline = hull.Select(q => new Vec3(q.X, q.Y, z0)).ToList();
            if (Math.Abs(Area(outline) - ps.Sum(p => Area(p.Outer))) > Math.Max(1e-3, Area(outline) * 1e-3)) return null;
            return new ShapePrism(outline, [], new Vec3(0, 0, z1 - z0), false, "mesh");
        }
        return null;
    }

    /// <summary>Two openings' boxes over the same stretch of a wall: along their longer side they overlap by half the narrower; across it they lie within a metre.</summary>
    static bool SameStretch((Vec3 Min, Vec3 Max) a, (Vec3 Min, Vec3 Max) b)
    {
        var alongX = Math.Max(a.Max.X - a.Min.X, b.Max.X - b.Min.X) >= Math.Max(a.Max.Y - a.Min.Y, b.Max.Y - b.Min.Y);
        double Lo(Vec3 v) => alongX ? v.X : v.Y;
        double Across(Vec3 v) => alongX ? v.Y : v.X;
        var overlap = Math.Min(Lo(a.Max), Lo(b.Max)) - Math.Max(Lo(a.Min), Lo(b.Min));
        var gap = Math.Max(Across(a.Min), Across(b.Min)) - Math.Min(Across(a.Max), Across(b.Max));
        return overlap > 0.5 * Math.Min(Lo(a.Max) - Lo(a.Min), Lo(b.Max) - Lo(b.Min)) && gap < 1;
    }

    /// <summary>A layer's hole a piece stands in (a lintel's seat in the core): the piece's underside at its bottom, across it. No opening.</summary>
    static bool Pocket(Hole h, List<List<Vec3>> undersides) => undersides.Any(u =>
    {
        if (Math.Abs(u[0].Z - h.Box.Min.Z) > 2e-3) return false;
        var (lo, hi) = (new Vec2(u.Min(p => p.X), u.Min(p => p.Y)), new Vec2(u.Max(p => p.X), u.Max(p => p.Y)));
        return lo.X < h.Box.Max.X - 1e-3 && hi.X > h.Box.Min.X + 1e-3 && lo.Y < h.Box.Max.Y - 1e-3 && hi.Y > h.Box.Min.Y + 1e-3;
    });

    /// <summary>An opening's head lowered to the underside of a piece over it (a lintel), when one lies across it within its height.</summary>
    static Hole UnderPieces(Hole h, List<List<Vec3>> undersides)
    {
        var head = h.Box.Max.Z;
        foreach (var u in undersides)
        {
            var z = u[0].Z;
            if (z <= h.Box.Min.Z + 1e-3 || z >= head - 1e-3) continue;
            var (lo, hi) = (new Vec2(u.Min(p => p.X), u.Min(p => p.Y)), new Vec2(u.Max(p => p.X), u.Max(p => p.Y)));
            if (lo.X < h.Box.Max.X - 1e-3 && hi.X > h.Box.Min.X + 1e-3 && lo.Y < h.Box.Max.Y - 1e-3 && hi.Y > h.Box.Min.Y + 1e-3) head = z;
        }
        if (head >= h.Box.Max.Z - 1e-6) return h;
        var sill = h.Box.Min.Z;
        return new Hole(h.Prism with { Sweep = new Vec3(0, 0, head - sill) }, h.Width, head - sill, (h.Box.Min, new Vec3(h.Box.Max.X, h.Box.Max.Y, head)));
    }

    /// <summary>
    /// The layers' openings as one each: those over the same stretch whose heights overlap joined across the wall, from the
    /// highest sill to the lowest head among them (a plaster layer goes round a little higher than the core's lintel).
    /// </summary>
    static List<Hole> JoinHoles(List<Hole> holes)
    {
        var o = new List<Hole>();
        foreach (var h in holes)
        {
            // Heights that overlap by half the lower one at least: the same opening, not a lintel's pocket next to it.
            // Over the same stretch of the wall (their longer side), however far apart across it (a plaster layer each side of the core).
            var i = o.FindIndex(x => Math.Min(x.Box.Max.Z, h.Box.Max.Z) - Math.Max(x.Box.Min.Z, h.Box.Min.Z) > 0.5 * Math.Min(x.Height, h.Height) && SameStretch(x.Box, h.Box));
            if (i < 0) { o.Add(h); continue; }
            var x = o[i];
            var hull = Polygon2.ConvexHull(Footprints.Plan(x.Prism.Outer).Concat(Footprints.Plan(h.Prism.Outer)));
            if (hull is not { Length: >= 3 }) continue;
            var (sill, head) = (Math.Max(x.Box.Min.Z, h.Box.Min.Z), Math.Min(x.Box.Max.Z, h.Box.Max.Z));
            var box = (new Vec3(Math.Min(x.Box.Min.X, h.Box.Min.X), Math.Min(x.Box.Min.Y, h.Box.Min.Y), sill), new Vec3(Math.Max(x.Box.Max.X, h.Box.Max.X), Math.Max(x.Box.Max.Y, h.Box.Max.Y), head));
            o[i] = new Hole(new ShapePrism([.. hull.Select(q => new Vec3(q.X, q.Y, sill))], [], new Vec3(0, 0, head - sill), false, "mesh"), Math.Min(x.Width, h.Width), head - sill, box);
        }
        return o;
    }

    /// <summary>
    /// A vertical wall's envelope and its openings: the top cap (one face, the highest) swept down to the lowest plane,
    /// when every vertex stands inside it; the openings from the horizontal faces strictly between: a head (facing down)
    /// over a sill (facing up, under it, along the same stretch) or over the bottom, as wide as the head, across the wall.
    /// </summary>
    static (ShapePrism Prism, List<Hole> Holes)? Envelope(Solid s)
    {
        const double tol = 2e-3;
        var v = s.Vertices;
        var (z0, z1) = (v.Min(p => p.Z), v.Max(p => p.Z));
        if (z1 - z0 < 0.1) return null;
        List<Vec3> Loop(int[] idx) => [.. idx.Select(i => v[i])];
        var tops = s.Faces.Where(f => f.Normal.Z > 0.999 && Loop(f.Outer).All(p => Math.Abs(p.Z - z1) < tol)).ToList();
        if (tops.Count != 1 || tops[0].Holes.Length > 0) return null;
        var cap = Footprints.Plan(Loop(tops[0].Outer));
        if (cap.Count < 3) return null;
        // Every vertex inside the cap in plan: nothing sticks out of the envelope.
        if (!v.All(p => InPolygon(cap, new Vec2(p.X, p.Y), tol))) return null;
        var prism = new ShapePrism([.. cap.Select(p => new Vec3(p.X, p.Y, z0))], [], new Vec3(0, 0, z1 - z0), false, "mesh");
        var holes = new List<Hole>();
        var inner = s.Faces.Where(f => Math.Abs(f.Normal.Z) > 0.999 && Loop(f.Outer).All(p => p.Z > z0 + tol && p.Z < z1 - tol)).ToList();
        foreach (var head in inner.Where(f => f.Normal.Z < 0))
        {
            var hp = Loop(head.Outer);
            var zh = hp[0].Z;
            var (lo, hi) = (new Vec2(hp.Min(p => p.X), hp.Min(p => p.Y)), new Vec2(hp.Max(p => p.X), hp.Max(p => p.Y)));
            // The sill under it over the same stretch, else the floor.
            var sill = inner.Where(f => f.Normal.Z > 0).Select(f => Loop(f.Outer)).Where(sp => sp[0].Z < zh - tol && Overlap(sp, lo, hi)).Select(sp => sp[0].Z).DefaultIfEmpty(z0).Max();
            var outline = Loop(head.Outer).Select(p => new Vec3(p.X, p.Y, sill)).ToList();
            var width = Math.Max(hi.X - lo.X, hi.Y - lo.Y);
            holes.Add(new Hole(new ShapePrism(outline, [], new Vec3(0, 0, zh - sill), false, "mesh"), width, zh - sill, (new Vec3(lo.X, lo.Y, sill), new Vec3(hi.X, hi.Y, zh))));
        }
        return (prism, holes);
    }

    static bool Overlap(List<Vec3> f, Vec2 lo, Vec2 hi)
    {
        var (a, b) = (new Vec2(f.Min(p => p.X), f.Min(p => p.Y)), new Vec2(f.Max(p => p.X), f.Max(p => p.Y)));
        return a.X < hi.X - 1e-3 && b.X > lo.X + 1e-3 && a.Y < hi.Y - 1e-3 && b.Y > lo.Y + 1e-3;
    }

    /// <summary>Inside a polygon or on its edge (within <paramref name="tol"/>).</summary>
    static bool InPolygon(IReadOnlyList<Vec2> poly, Vec2 p, double tol)
    {
        for (var i = 0; i < poly.Count; i++)
        {
            Vec2 a = poly[i], b = poly[(i + 1) % poly.Count];
            var ab = b - a;
            var t = Math.Clamp(Vec2.Dot(p - a, ab) / Math.Max(1e-12, Vec2.Dot(ab, ab)), 0, 1);
            if ((a + ab * t - p).Length <= tol) return true;
        }
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
        return inside;
    }

    /// <summary>What an entity is related by (its storey, group, type…), to relate it again.</summary>
    static List<(string Class, string Relating)> Related(IModelView m, string id) =>
        [.. m.Relations.Where(x => x.Related.Contains(id)).Select(x => (x.Class, x.Relating))];
}
