using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Structure;

/// <summary>A plane that is not upright, as a height over the plan: z = <see cref="A"/> + <see cref="Bx"/>·x + <see cref="By"/>·y.</summary>
public sealed record ColumnPlane(double A, double Bx, double By)
{
    public double Z(Vec2 p) => A + Bx * p.X + By * p.Y;
    /// <summary>Its unit normal, up (out of a column's top) or down (out of its bottom).</summary>
    public Vec3 Normal(bool up) { var n = new Vec3(-Bx, -By, 1).Normalized(); return up ? n : -n; }
    public ColumnPlane Raised(double dz) => this with { A = A + dz };

    /// <summary>The plane of a triangle (not upright).</summary>
    public static ColumnPlane Of(Vec3 a, Vec3 b, Vec3 c)
    {
        var n = Vec3.Cross(b - a, c - a);
        var (bx, by) = (-n.X / n.Z, -n.Y / n.Z);
        return new ColumnPlane(a.Z - bx * a.X - by * a.Y, bx, by);
    }

    public bool Same(ColumnPlane o, Vec2 at) => Math.Abs(Bx - o.Bx) < 1e-6 && Math.Abs(By - o.By) < 1e-6 && Math.Abs(Z(at) - o.Z(at)) < 5e-4;
}

/// <summary>
/// One end of a column as built (docs/plan-attach-to.md §3.3). Not attached, or attached but left at its own end by its mode:
/// flat at <see cref="Axis"/>. Attached under one plane or several whose region is convex (a ridge seen from below: the
/// lowest of them; a valley seen from above for a bottom: the highest): <see cref="Planes"/>, exact, written to IFC as clips.
/// Anything else (a step, a valley over a top, a curve, a section half under the target): <see cref="Boolean"/>, the column
/// built to <see cref="Reach"/> and <see cref="Cutters"/> taken off by the booleans.
/// </summary>
public sealed record ColumnEnd(string End, bool Attached, double Axis, IReadOnlyList<ColumnPlane> Planes, bool Boolean, double Reach, IReadOnlyList<Solid> Cutters, Func<Vec2, double> At)
{
    public bool Top => End == Attachments.Top;
    /// <summary>The end follows a target: not flat at the column's own end.</summary>
    public bool Shaped => Planes.Count > 0 || Boolean;
    /// <summary>One plane: the prism is built to it directly, with no boolean.</summary>
    public bool OnePlane => Planes.Count == 1 && !Boolean;

    public static ColumnEnd Flat(string end, double z, bool attached) => new(end, attached, z, [], false, z, [], _ => z);

    /// <summary>The height the prism is built to at a plan point before any cut: the plane itself, else flat (at the axis, or at the reach when cut).</summary>
    public double Prism(Vec2 p) => OnePlane ? Planes[0].Z(p) : Shaped ? Reach : Axis;
}

/// <summary>
/// A column as built: <see cref="Base"/> and <see cref="Height"/> along its axis, its ends' shapes, and the problems its
/// attachments have. Without attachments, its drawn base and height (<see cref="Columns.Spec"/>).
/// </summary>
public sealed record BuiltColumn(Section Section, string? Material, double DrawnBase, double DrawnHeight, ColumnEnd Top, ColumnEnd Bottom, IReadOnlyList<string> Problems)
{
    public double Base => Bottom.Axis;
    public double Height => Top.Axis - Bottom.Axis;
    public bool Shaped => Top.Shaped || Bottom.Shaped || Math.Abs(Base - DrawnBase) > 1e-9 || Math.Abs(Height - DrawnHeight) > 1e-9;
}

/// <summary>
/// A column's ends attached to bodies (docs/plan-attach-to.md, stage A2), stored as relations (<see cref="AttachRelations"/>).
/// Attaching changes only the column's own body and what is measured from it; what other elements read of it stays as drawn
/// (<see cref="Columns.Spec"/>, <see cref="Columns.Envelope"/>), and its targets are read as their own producers build them,
/// before the cut order (so a column under a slab reads the slab's soffit, not the hole its own drawn envelope cuts there).
/// </summary>
public static class ColumnEnds
{
    /// <summary>Within this the samples of an end lie on its planes (the sampler's simplification tolerance).</summary>
    const double OnPlane = 5e-4;
    /// <summary>The boolean path's prism goes this far past the highest point of the end.</summary>
    const double Margin = 0.01;
    /// <summary>The samples across a section's box, each way.</summary>
    const int Grid = 9;

    static readonly RevisionCache<BuiltColumn> Builts = new();
    /// <summary>The last column built with its attachments, by entity: its quantities are measured from it (they have no services).</summary>
    static readonly ConditionalWeakTable<Entity, BuiltColumn> Last = new();

    /// <summary>The column as built: its attachments read now, once per revision.</summary>
    public static BuiltColumn Of(IModelView m, Entity column, IServices? services)
    {
        if (services == null || !AttachRelations.Of(m, column.Id, Attachments.Top).Any() && !AttachRelations.Of(m, column.Id, Attachments.Bottom).Any())
            return Plain(m, column);
        var b = Builts.Get(column, m, v => Solve(v, column, services, null));
        Last.AddOrUpdate(column, b);
        return b;
    }

    /// <summary>The column as <paramref name="asIf"/> would attach one end (the tool's ghost): nothing is written or kept.</summary>
    public static BuiltColumn Preview(IModelView m, Entity column, IServices services, AttachSpec asIf) => Solve(m, column, services, asIf);

    /// <summary>The column last built with <see cref="Of"/>, for what has no services (its quantities); as drawn when none was.</summary>
    public static BuiltColumn LastOf(IModelView m, Entity column) =>
        Last.TryGetValue(column, out var b) && (AttachRelations.Of(m, column.Id, Attachments.Top).Any() || AttachRelations.Of(m, column.Id, Attachments.Bottom).Any()) ? b : Plain(m, column);

    static BuiltColumn Plain(IModelView m, Entity column)
    {
        var (section, material, height) = Columns.Spec(m, column);
        var z0 = Place.Elevation(m, column) + column.Num("baseOffset");
        return new BuiltColumn(section, material, z0, height, ColumnEnd.Flat(Attachments.Top, z0 + height, false), ColumnEnd.Flat(Attachments.Bottom, z0, false), []);
    }

    /// <summary>The section's outer ring and holes in the world's plan, counter-clockwise outside.</summary>
    public static (IReadOnlyList<Vec2> Outer, IReadOnlyList<IReadOnlyList<Vec2>> Holes) Rings(Entity column, Section section, bool fine = false)
    {
        var (outline, holes) = fine ? section.Real() : (section.Outline(), section.Holes());
        var at = Place.Point(column);
        var rot = column.Num("rotation");
        return (Polygon2.EnsureCcw(Place.Placed(outline, at, rot).ToList()), holes.Select(h => (IReadOnlyList<Vec2>)Place.Placed(h, at, rot).ToList()).ToList());
    }

    static BuiltColumn Solve(IModelView m, Entity column, IServices services, AttachSpec? asIf)
    {
        var plain = Plain(m, column);
        var problems = new List<string>();
        var (outer, holes) = Rings(column, plain.Section);
        var at = Place.Point(column);
        var samples = Samples(outer, holes, at);
        (IReadOnlyList<Entity> Targets, double Offset, string Mode) Held(string end)
        {
            if (asIf != null && asIf.End == end) return ([.. asIf.Targets.Select(m.Get).OfType<Entity>()], asIf.Offset, asIf.Mode);
            var rels = AttachRelations.Of(m, column.Id, end).ToList();
            if (rels.Count == 0) return ([], 0, Attachments.Both);
            var first = rels[0].Attrs;
            return ([.. rels.Select(r => m.Get(r.Relating)).OfType<Entity>()], Json.Number(first?["offset"]) ?? 0, (string?)first?["mode"] ?? Attachments.Both);
        }
        var (topTargets, topOffset, topMode) = Held(Attachments.Top);
        var (botTargets, botOffset, botMode) = Held(Attachments.Bottom);
        var ownTop = plain.Top.Axis;
        var ownBase = plain.Bottom.Axis;
        // The top first, reading the faces above the drawn base; then the bottom, the faces below the top as built.
        var top = End(m, services, column, Attachments.Top, topTargets, topOffset, topMode, ownTop, ownBase, outer, holes, at, samples, problems);
        var bottom = End(m, services, column, Attachments.Bottom, botTargets, botOffset, botMode, ownBase, top.Axis, outer, holes, at, samples, problems);
        return plain with { Top = top, Bottom = bottom, Problems = problems };
    }

    /// <summary>The plan points an end is read at: the section's corners, its point, and a grid over its box inside it.</summary>
    static List<Vec2> Samples(IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes, Vec2 at)
    {
        var o = new List<Vec2>(outer) { at };
        var (x0, x1, y0, y1) = (outer.Min(p => p.X), outer.Max(p => p.X), outer.Min(p => p.Y), outer.Max(p => p.Y));
        for (var i = 0; i < Grid; i++)
            for (var j = 0; j < Grid; j++)
            {
                var p = new Vec2(x0 + (x1 - x0) * (i + 0.5) / Grid, y0 + (y1 - y0) * (j + 0.5) / Grid);
                if (Polygon2.Contains(outer, p) && !holes.Any(h => Polygon2.Contains(h, p))) o.Add(p);
            }
        return o;
    }

    static string Names(IEnumerable<Entity> targets) => string.Join(", ", targets.Select(t => $"„{t.Name}”"));

    /// <summary>
    /// One end: <paramref name="own"/> its height as drawn, <paramref name="from"/> the other end (the faces read lie beyond it:
    /// above the base for a top, below the top for a bottom).
    /// </summary>
    static ColumnEnd End(IModelView m, IServices services, Entity column, string end, IReadOnlyList<Entity> targets, double offset, string mode, double own, double from,
        IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes, Vec2 at, List<Vec2> samples, List<string> problems)
    {
        var top = end == Attachments.Top;
        if (targets.Count == 0) return ColumnEnd.Flat(end, own, false);
        var s = BodySampler.Sample(column, targets, m, services, uncut: true);
        if (s.InCycle)
        {
            problems.Add(Attachments.CycleProblem(m, services, column, end) ?? $"Its {end} depends on itself: it stands at its own {(top ? "height" : "base")}.");
            return ColumnEnd.Flat(end, own, true);
        }
        var (x0, x1, y0, y1) = (outer.Min(p => p.X) - 1e-6, outer.Max(p => p.X) + 1e-6, outer.Min(p => p.Y) - 1e-6, outer.Max(p => p.Y) + 1e-6);
        var near = s.Tris.Where(t => t.Down == top && t.X1 >= x0 && t.X0 <= x1 && t.Y1 >= y0 && t.Y0 <= y1).ToList();
        // The face over (under) a plan point, with the triangle it lies on; the gap from it applied.
        (double Z, BodySampler.Tri T)? Hit(Vec2 p)
        {
            var best = top ? double.PositiveInfinity : double.NegativeInfinity;
            BodySampler.Tri? tri = null;
            foreach (var t in near)
            {
                if (p.X < t.X0 || p.X > t.X1 || p.Y < t.Y0 || p.Y > t.Y1) continue;
                if (BodySampler.ZAt(t, p) is not { } z) continue;
                if (top ? z > from + 1e-6 && z < best : z < from - 1e-6 && z > best) { best = z; tri = t; }
            }
            return tri == null ? null : (top ? best - offset : best + offset, tri);
        }
        var hits = samples.Select(Hit).ToList();
        var axisHit = Hit(at);
        var covered = hits.Count(h => h != null);
        if (covered == 0 && axisHit == null)
        {
            problems.Add(top ? $"Its top is attached to {Names(targets)}, which is not over it: it stands at its own height."
                : $"Its bottom is attached to {Names(targets)}, which is not under it: it stands on its own base.");
            return ColumnEnd.Flat(end, own, true);
        }
        // A target over part of the section only: said, as it is usually a mistake, and built with the step.
        foreach (var (t, tris) in s.PerTarget)
        {
            var mine = tris.Where(x => x.Down == top && x.X1 >= x0 && x.X0 <= x1 && x.Y1 >= y0 && x.Y0 <= y1).ToList();
            var n = samples.Count(p => top ? BodySampler.UnderAt(mine, p, from) != null : BodySampler.OverAt(mine, p, from) != null);
            if (n > 0 && n < samples.Count) problems.Add(top ? $"Part of its section is not under „{t.Name}”." : $"Part of its section does not stand on „{t.Name}”.");
        }
        var axis = axisHit?.Z ?? own;
        var keep = mode == Attachments.Both || axisHit != null && (mode == Attachments.Extend ? (top ? axis > own + 1e-9 : axis < own - 1e-9) : (top ? axis < own - 1e-9 : axis > own + 1e-9));
        if (!keep) return ColumnEnd.Flat(end, own, true);
        if (top ? axis <= from + 1e-3 : axis >= from - 1e-3)
        {
            problems.Add(top ? $"Its top is attached to {Names(targets)}, which is under its base: it stands at its own height."
                : $"Its bottom is attached to {Names(targets)}, which is over its top: it stands on its own base.");
            return ColumnEnd.Flat(end, own, true);
        }
        double Lower(double a, double b) => top ? Math.Min(a, b) : Math.Max(a, b);

        if (covered == samples.Count)
        {
            // The planes the samples lie on; exact when the surface is their lowest (a top) or highest (a bottom) everywhere.
            var planes = new List<ColumnPlane>();
            foreach (var h in hits)
            {
                var pl = ColumnPlane.Of(h!.Value.T.A, h.Value.T.B, h.Value.T.C).Raised(top ? -offset : offset);
                if (!planes.Any(q => q.Same(pl, at))) planes.Add(pl);
            }
            double Envelope(Vec2 p) => planes.Select(q => q.Z(p)).Aggregate(Lower);
            var exact = hits.Zip(samples).All(x => Math.Abs(x.First!.Value.Z - Envelope(x.Second)) <= OnPlane) && EdgesAgree(outer, p => Hit(p)?.Z, Envelope);
            if (exact)
            {
                // Past the farthest any plane reaches over the corners (the envelope's highest point, a ridge's, lies under it).
                var reach = top ? outer.Max(p => planes.Max(q => q.Z(p))) + Margin : outer.Min(p => planes.Min(q => q.Z(p))) - Margin;
                return new ColumnEnd(end, true, Envelope(at), planes, false, reach, planes.Count == 1 ? [] : [.. planes.Select(q => HalfSpace(q, outer, top, reach)).OfType<Solid>()], Envelope);
            }
        }

        // The boolean path: built past its highest (lowest) point, the region beyond the target's faces over the section taken off.
        var cutters = new List<Solid>();
        var rings = new List<(List<Vec2> Ring, ColumnPlane Plane)>();
        foreach (var t in near)
        {
            var ring = Polygon2.ClipBox(Polygon2.EnsureCcw([t.A.Xy, t.B.Xy, t.C.Xy]), x0 - 1e-3, y0 - 1e-3, x1 + 1e-3, y1 + 1e-3);
            if (ring.Count < 3 || Math.Abs(Polygon2.SignedArea(ring)) < 1e-10) continue;
            var pl = ColumnPlane.Of(t.A, t.B, t.C).Raised(top ? -offset : offset);
            // A face beyond the other end over the section is not this end's (as the samples read it).
            if (ring.Any(p => top ? pl.Z(p) <= from + 1e-6 : pl.Z(p) >= from - 1e-6)) continue;
            rings.Add((ring, pl));
        }
        var extremes = rings.SelectMany(r => r.Ring.Select(p => r.Plane.Z(p))).ToList();
        var uncovered = covered < samples.Count;
        if (uncovered) extremes.Add(own);
        var far = (top ? extremes.Max() : extremes.Min()) + (top ? Margin : -Margin);
        var beyond = far + (top ? 10 : -10);
        foreach (var (ring, pl) in rings)
            if (Between([ring], top ? pl.Z : _ => beyond, top ? _ => beyond : pl.Z) is { } c) cutters.Add(c);
        if (uncovered && services.Get<ISolidBooleans>() is { } booleans)
        {
            // Where no target is, its own end: the region beyond it, less the columns of air over (under) the targets' faces.
            IReadOnlyList<Vec2> box = [new(x0 - 1e-3, y0 - 1e-3), new(x1 + 1e-3, y0 - 1e-3), new(x1 + 1e-3, y1 + 1e-3), new(x0 - 1e-3, y1 + 1e-3)];
            var (lo, hi) = top ? (own, beyond) : (beyond, own);
            var bigLo = Math.Min(lo, hi) - 1;
            var bigHi = Math.Max(lo, hi) + 1;
            if (Between([box], _ => lo, _ => hi) is { } cap)
            {
                var air = rings.Select(r => Between([r.Ring], _ => bigLo, _ => bigHi)).OfType<Solid>().ToList();
                try { if ((air.Count > 0 ? booleans.Difference(cap, air, "column") : cap) is { } rest) cutters.Add(rest); }
                catch (InvalidOperationException) { cutters.Add(cap); }
            }
        }
        double Surface(Vec2 p) => Hit(p)?.Z ?? own;
        return new ColumnEnd(end, true, axis, [], true, far, cutters, Surface);
    }

    /// <summary>Whether the surface along each side of the section (read to a millimetre at its steps) lies on the planes.</summary>
    static bool EdgesAgree(IReadOnlyList<Vec2> outer, Func<Vec2, double?> hit, Func<Vec2, double> envelope)
    {
        for (var i = 0; i < outer.Count; i++)
        {
            var (a, b) = (outer[i], outer[(i + 1) % outer.Count]);
            var len = (b - a).Length;
            if (len < 1e-6) continue;
            var dir = (b - a) * (1 / len);
            var n = Math.Max(2, (int)Math.Ceiling(len / 0.01));
            for (var k = 0; k <= n; k++)
            {
                var p = a + dir * (len * k / n);
                if (hit(p) is not { } z || Math.Abs(z - envelope(p)) > OnPlane) return false;
            }
        }
        return true;
    }

    /// <summary>What a plane takes off: over the section's box (a little wider), from the plane to past the reach.</summary>
    static Solid? HalfSpace(ColumnPlane q, IReadOnlyList<Vec2> outer, bool top, double reach)
    {
        var (x0, x1, y0, y1) = (outer.Min(p => p.X) - 0.05, outer.Max(p => p.X) + 0.05, outer.Min(p => p.Y) - 0.05, outer.Max(p => p.Y) + 0.05);
        IReadOnlyList<Vec2> box = [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)];
        var past = reach + (top ? 10 : -10);
        return top ? Between([box], q.Z, _ => past) : Between([box], _ => past, q.Z);
    }

    /// <summary>
    /// A prism over plan rings (the first outside, counter-clockwise; the rest holes) from <paramref name="lo"/> to
    /// <paramref name="hi"/> at each corner: its ends planar where the functions are (a plane, a level). Null when it is not
    /// taller than a tenth of a millimetre everywhere.
    /// </summary>
    public static Solid? Between(IReadOnlyList<IReadOnlyList<Vec2>> rings, Func<Vec2, double> lo, Func<Vec2, double> hi, string tag = "column")
    {
        if (rings.Count == 0 || rings[0].Count < 3) return null;
        if (rings.SelectMany(r => r).Any(p => hi(p) - lo(p) < 1e-4)) return null;
        var list = new List<IReadOnlyList<Vec2>> { Polygon2.EnsureCcw(rings[0]) };
        foreach (var h in rings.Skip(1)) list.Add(Polygon2.EnsureCcw(h).Reverse().ToList());
        var verts = new List<Vec3>();
        var los = new List<int[]>();
        var his = new List<int[]>();
        foreach (var r in list)
        {
            los.Add(r.Select(p => { verts.Add(p.At(lo(p))); return verts.Count - 1; }).ToArray());
            his.Add(r.Select(p => { verts.Add(p.At(hi(p))); return verts.Count - 1; }).ToArray());
        }
        var faces = new List<FaceInput>
        {
            new(los[0].Reverse().ToArray(), los.Skip(1).Select(l => l.Reverse().ToArray()).ToArray(), -Vec3.UnitZ, "bottom"),
            new(his[0], his.Skip(1).ToArray(), Vec3.UnitZ, "top"),
        };
        for (var k = 0; k < list.Count; k++)
        {
            var r = list[k];
            for (var i = 0; i < r.Count; i++)
            {
                var j = (i + 1) % r.Count;
                var outward = new Vec3(r[j].Y - r[i].Y, r[i].X - r[j].X, 0);
                faces.Add(new FaceInput([los[k][i], los[k][j], his[k][j], his[k][i]], null, outward, k == 0 ? "side" : "reveal"));
            }
        }
        return SolidBuilder.Make(verts, faces, tag);
    }

    /// <summary>The column's body as its ends shape it, before the cut order: the prism between them, the clips and the boolean path's cutters taken off.</summary>
    public static Solid? Body(BuiltColumn b, IReadOnlyList<Vec2> outer, IReadOnlyList<IReadOnlyList<Vec2>> holes, IServices? services, List<string> problems)
    {
        var solid = Between([outer, .. holes], b.Bottom.Prism, b.Top.Prism);
        if (solid == null) return null;
        var cutters = b.Top.Cutters.Concat(b.Bottom.Cutters).ToList();
        if (cutters.Count == 0) return solid;
        if (services?.Get<ISolidBooleans>() is not { } booleans) { problems.Add("Its attached end needs the boolean library (manifold), missing here."); return solid; }
        try { return booleans.Difference(solid, cutters, "column"); }
        catch (InvalidOperationException ex) { problems.Add($"Its attached end could not be cut ({ex.Message})."); return solid; }
    }

    /// <summary>The breakpoints along a side of the section where an end's surface bends or steps (a ridge's line, a step's edge): fractions 0..1.</summary>
    public static List<double> Breaks(ColumnEnd e, Vec2 a, Vec2 b, IServices? services, IModelView m)
    {
        var o = new List<double> { 0, 1 };
        if (e.Planes.Count > 1 && !e.Boolean)
            for (var i = 0; i < e.Planes.Count; i++)
                for (var j = i + 1; j < e.Planes.Count; j++)
                {
                    var (p, q) = (e.Planes[i], e.Planes[j]);
                    double F(Vec2 x) => p.Z(x) - q.Z(x);
                    var (fa, fb) = (F(a), F(b));
                    if (Math.Abs(fa - fb) > 1e-12 && fa * fb < 0) o.Add(fa / (fa - fb));
                }
        else if (e.Boolean)
        {
            // A step's edge found by bisection between samples a centimetre apart.
            var len = (b - a).Length;
            var n = Math.Max(2, (int)Math.Ceiling(len / 0.01));
            for (var k = 0; k < n; k++)
            {
                double t0 = (double)k / n, t1 = (double)(k + 1) / n;
                var (z0, z1) = (e.At(a + (b - a) * t0), e.At(a + (b - a) * t1));
                if (Math.Abs(z1 - z0) < 0.05 * len / n + 1e-4) continue;
                for (var it = 0; it < 20; it++)
                {
                    var mid = (t0 + t1) / 2;
                    if (Math.Abs(e.At(a + (b - a) * mid) - z0) < Math.Abs(e.At(a + (b - a) * mid) - z1)) t0 = mid; else t1 = mid;
                }
                o.Add(t0); o.Add(t1);
            }
        }
        o.Sort();
        return o;
    }
}

/// <summary>Columns' tops and bottoms attached to bodies (docs/plan-attach-to.md, stage A2): stored as relations.</summary>
sealed class ColumnAttach : IAttachable
{
    public IReadOnlyList<string> Classes => [IfcClass.Column];
    public bool Handles(Entity e) => e.Class == IfcClass.Column;
    public IReadOnlyList<string> Ends(Entity e) => [Attachments.Top, Attachments.Bottom];

    public string? Refuse(IModelView m, Entity e, AttachSpec spec, IReadOnlyList<Entity> targets)
    {
        if (spec.Level != null) return $"„{e.Name}” goes to a level by its own height and base offset; its {spec.End} attaches to bodies.";
        if (e.Attrs["steelEnds"] is JsonObject) return $"„{e.Name}” has steel ends: a steel column's ends attach with the steel members (a later stage).";
        if (e.Attrs["at"] is null) return $"„{e.Name}” has no point to stand on.";
        return null;
    }

    public void Attach(ICommandContext ctx, Entity e, AttachSpec spec) => AttachRelations.Set(ctx, e, spec);
    public void Detach(ICommandContext ctx, Entity e, string end) => AttachRelations.Clear(ctx, e, end);
    public AttachState? StateOf(IModelView m, Entity e, string end) => AttachRelations.State(m, e, end);

    public JsonObject Result(IModelView m, IServices services, Entity e, string end, AttachSpec? asIf)
    {
        var b = asIf != null ? ColumnEnds.Preview(m, e, services, asIf) : ColumnEnds.Of(m, e, services);
        var x = end == Attachments.Top ? b.Top : b.Bottom;
        var (outer, _) = ColumnEnds.Rings(e, b.Section);
        // The end face's outline in the world, the ridge's and the steps' points on the sides included.
        var outline = new JsonArray();
        for (var i = 0; i < outer.Count; i++)
        {
            var (p, q) = (outer[i], outer[(i + 1) % outer.Count]);
            foreach (var t in ColumnEnds.Breaks(x, p, q, services, m).SkipLast(1))
            {
                var w = p + (q - p) * t;
                outline.Add((JsonNode)new JsonArray(w.X, w.Y, x.At(w)));
            }
        }
        return new JsonObject
        {
            ["height"] = b.DrawnHeight, ["built"] = b.Height, ["base"] = b.Base, ["axis"] = x.Axis,
            ["path"] = !x.Shaped ? "own" : x.Boolean ? "boolean" : x.Planes.Count == 1 ? "plane" : "planes",
            ["planes"] = new JsonArray([.. x.Planes.Select(pl => { var n = pl.Normal(x.Top); return (JsonNode)new JsonArray(n.X, n.Y, n.Z); })]),
            ["outline"] = outline,
            ["problems"] = new JsonArray([.. b.Problems.Select(p => (JsonNode)p)]),
        };
    }
}
