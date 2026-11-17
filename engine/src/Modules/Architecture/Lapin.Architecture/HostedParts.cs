using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Architecture.Balconies;

namespace Lapin.Architecture;

/// <summary>
/// What architecture hangs on a wall or a floor other than its openings and sweeps, put on the part it falls on when the
/// element is split, trimmed or carried on (audit 2026-11-15 §15, decision 619's leftovers):
/// <list type="bullet">
/// <item>Finishes painted on a straight wall's sides or a floor's top and soffit (<see cref="FinishZones"/>): their seeds and
/// split lines are kept in the face's own frame, from the wall's start or the floor's first corner, so each is put on its part
/// and re-measured from that part's start or first corner. A finish whose region runs on across the cut (no split line
/// between) paints the other part too; one over the whole face, every part; one left where the element is cut away goes.</item>
/// <item>Sunshades along a wall (<c>wall</c>, <c>from</c>, <c>to</c>): on the part they lie along; one across the cut, or cut
/// away, refuses (as an opening does). Those over a window follow the window.</item>
/// <item>Balconies on a wall (<c>wallId</c>, <c>along</c>): on the part their middle lies on; one left where the wall is cut
/// away stands free where it is (as when its wall moves away from it).</item>
/// </list>
/// A curved wall's finishes stay on its first part: their frame runs from its face's first corner, which a cut moves.
/// </summary>
public sealed class HostedParts : ISplitFollower
{
    const double Inside = 0.05;

    public void Parts(ICommandContext ctx, Entity was, IReadOnlyList<PathPiece> pieces)
    {
        if (was.Class == IfcClass.Wall)
        {
            Sunshade(ctx, was, pieces);
            Balcony(ctx, was, pieces);
            if (!PathCurve.Of(was).IsArc) WallFinishes(ctx, was, pieces);
        }
        else if (was.Class == IfcClass.Slab) SlabFinishes(ctx, was, pieces);
    }

    // ─── Sunshades and balconies ──────────────────────────────────────────────

    static void Sunshade(ICommandContext ctx, Entity was, IReadOnlyList<PathPiece> pieces)
    {
        foreach (var s in ctx.Model.OfClass(Sunshades.Class).Where(x => x.Str("wall") == was.Id).ToList())
        {
            var (from, to) = (s.Num("from"), s.Num("to"));
            var (lo, hi) = (Math.Min(from, to), Math.Max(from, to));
            var on = pieces.Where(p => lo >= p.S0 - 1e-6 && hi <= p.S1 + 1e-6).Select(p => (PathPiece?)p).FirstOrDefault();
            if (on is not { } piece)
                throw new CommandException(pieces.Any(p => lo < p.S1 - 1e-6 && hi > p.S0 + 1e-6)
                    ? $"„{s.Name}” runs across the cut in „{was.Name}”: split it elsewhere, or shorten the sunshade first."
                    : $"„{s.Name}” on „{was.Name}” would fall where the wall is cut away: move it first, or keep the other part.");
            if (piece.Id == was.Id && Math.Abs(piece.S0) < 1e-12) continue;
            var a = s.CloneAttrs();
            a["wall"] = piece.Id;
            a["from"] = Math.Round(from - piece.S0, 9);
            a["to"] = Math.Round(to - piece.S0, 9);
            ctx.Replace(s.With(attrs: a));
        }
    }

    static void Balcony(ICommandContext ctx, Entity was, IReadOnlyList<PathPiece> pieces)
    {
        var length = PathCurve.Of(was).Length;
        foreach (var b in ctx.Model.OfClass(BalconyModule.Class).Where(x => x.Str("wallId") == was.Id).ToList())
        {
            var along = b.Num("along");
            // Past an end (a corner balcony), with the part that keeps that end.
            PathPiece? on = along <= 0 ? pieces.Where(p => p.S0 <= 1e-6).Select(p => (PathPiece?)p).FirstOrDefault()
                : along >= length ? pieces.Where(p => p.S1 >= length - 1e-6).Select(p => (PathPiece?)p).FirstOrDefault()
                : pieces.Where(p => along >= p.S0 - 1e-6 && along <= p.S1 + 1e-6).Select(p => (PathPiece?)p).FirstOrDefault();
            var a = b.CloneAttrs();
            if (on is not { } piece) { a.Remove("wallId"); a.Remove("along"); }
            else if (piece.Id == was.Id && Math.Abs(piece.S0) < 1e-12) continue;
            else { a["wallId"] = piece.Id; a["along"] = Math.Round(along - piece.S0, 6); }
            ctx.Replace(b.With(attrs: a));
        }
    }

    // ─── Finishes on faces ────────────────────────────────────────────────────

    /// <summary>The finish zones and split lines on the host's faces.</summary>
    static IEnumerable<Entity> OnHost(IModelView m, string id) =>
        m.Entities.Where(x => x.Class is FinishZones.Class or FinishZones.SplitClass && x.Str("hostId") == id).ToList();

    static Vec2? Uv(JsonNode? n) => n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;
    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    /// <summary>A zone or a split line on each of <paramref name="parts"/>, its points re-measured there; none: it goes.</summary>
    static void Place(ICommandContext ctx, Entity z, string role, IReadOnlyList<string> parts, Func<string, string, Func<Vec2, Vec2>?> shift)
    {
        var first = true;
        foreach (var part in parts)
        {
            if (shift(part, role) is not { } f) continue;
            var a = z.CloneAttrs();
            a["hostId"] = part;
            if (z.Class == FinishZones.Class) a["covers"] = part;
            if (a["points"] is JsonArray pts) a["points"] = new JsonArray([.. pts.Select(n => Uv(n) is { } q ? (JsonNode)J(f(q)) : n?.DeepClone())]);
            if (first) { ctx.Replace(z.With(attrs: a)); first = false; }
            else Copy(ctx, z, a);
        }
        if (first) ctx.Remove(z.Id);
    }

    static void Copy(ICommandContext ctx, Entity like, JsonObject attrs)
    {
        var c = new Entity(ctx.NewId(), like.Class, like.Name, attrs, like.PredefinedType);
        ctx.Add(c);
        foreach (var r in ctx.Model.RelationsOf(like.Id, IfcClass.RelContainedInSpatialStructure).Where(x => x.Related.Contains(like.Id)).ToList())
            ctx.Relate(r.Class, r.Relating, c.Id);
    }

    /// <summary>Whether a split line on the face lies between two points of it (the segment between them crosses one).</summary>
    static bool Blocked(IEnumerable<Entity> splits, Vec2 a, Vec2 b)
    {
        foreach (var s in splits)
        {
            if (s.Attrs["points"] is not JsonArray arr) continue;
            var pts = arr.Select(Uv).OfType<Vec2>().ToList();
            if (s.Bool("closed") && pts.Count >= 3) pts.Add(pts[0]);
            for (var i = 0; i + 1 < pts.Count; i++)
                if (Crosses(a, b, pts[i], pts[i + 1])) return true;
        }
        return false;
    }

    static bool Crosses(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
    {
        double O(Vec2 p, Vec2 q, Vec2 r) => Vec2.Cross(q - p, r - p);
        var (d1, d2, d3, d4) = (O(c, d, a), O(c, d, b), O(a, b, c), O(a, b, d));
        return d1 * d2 < 0 && d3 * d4 < 0;
    }

    static Vec2 Closest(Vec2 a, Vec2 b, Vec2 p)
    {
        var ab = b - a;
        var l2 = Vec2.Dot(ab, ab);
        return l2 < 1e-18 ? a : a + ab * Math.Clamp(Vec2.Dot(p - a, ab) / l2, 0, 1);
    }

    static void WallFinishes(ICommandContext ctx, Entity was, IReadOnlyList<PathPiece> pieces)
    {
        if (!OnHost(ctx.Model, was.Id).Any()) return;
        // On a straight wall's side, u runs along the axis from its start (against it on the left side): along = u · sign.
        static double Sign(string role) => role == "Face.Left" ? -1 : 1;
        bool Side(string role) => role is "Face.Left" or "Face.Right";
        var byId = pieces.ToDictionary(p => p.Id);
        var ids = pieces.Select(p => p.Id).ToList();
        Func<Vec2, Vec2>? Shift(string part, string role) =>
            !Side(role) ? (part == was.Id ? q => q : null) : q => new Vec2(q.X - Sign(role) * byId[part].S0, q.Y);
        // Which role a point is measured on is the zone's; the side's sign is the same for both, so test along with each.
        string? PartOf(Vec2 q, string role)
        {
            if (!Side(role)) return was.Id;
            var along = q.X * Sign(role);
            return pieces.Where(p => along >= p.S0 - 1e-6 && along <= p.S1 + 1e-6).Select(p => p.Id).FirstOrDefault();
        }
        foreach (var role in OnHost(ctx.Model, was.Id).Select(z => z.Str("face")).OfType<string>().Distinct().ToList())
        {
            var mine = OnHost(ctx.Model, was.Id).Where(z => z.Str("face") == role).ToList();
            if (mine.Count == 0) continue;
            Vec2? Into(string part, Vec2 q)
            {
                if (!Side(role)) return null;
                var p = byId[part];
                var along = Math.Clamp(q.X * Sign(role), p.S0 + Math.Min(Inside, (p.S1 - p.S0) / 2), p.S1 - Math.Min(Inside, (p.S1 - p.S0) / 2));
                return new Vec2(along * Sign(role), q.Y);
            }
            IEnumerable<string> SplitParts(Entity s)
            {
                if (!Side(role)) return [was.Id];
                var al = (s.Attrs["points"] as JsonArray ?? []).Select(Uv).OfType<Vec2>().Select(q => q.X * Sign(role)).ToList();
                if (al.Count == 0) return [];
                return pieces.Where(p => al.Max() > p.S0 - 1e-6 && al.Min() < p.S1 + 1e-6).Select(p => p.Id);
            }
            RehostOn(ctx, was, mine, ids, Shift, q => PartOf(q, role), Into, SplitParts);
        }
    }

    static void RehostOn(ICommandContext ctx, Entity was, List<Entity> all, IReadOnlyList<string> parts, Func<string, string, Func<Vec2, Vec2>?> shift,
        Func<Vec2, string?> partOf, Func<string, Vec2, Vec2?> into, Func<Entity, IEnumerable<string>> splitParts)
    {
        var splits = all.Where(x => x.Class == FinishZones.SplitClass).ToList();
        foreach (var z in all)
        {
            var role = z.Str("face")!;
            if (z.Class == FinishZones.SplitClass)
            {
                Place(ctx, z, role, z.Attrs["height"] != null ? parts : splitParts(z).ToList(), shift);
                continue;
            }
            if (Uv(z.Attrs["seed"]) is not { } seed) { Place(ctx, z, role, parts, shift); continue; }
            var home = partOf(seed);
            var targets = new List<(string Part, Vec2 Seed)>();
            if (home != null) targets.Add((home, seed));
            foreach (var p in parts.Where(p => p != home))
                if (into(p, seed) is { } t && !Blocked(splits, seed, t)) targets.Add((p, t));
            var first = true;
            foreach (var (part, at) in targets)
            {
                if (shift(part, role) is not { } f) continue;
                var a = z.CloneAttrs();
                a["hostId"] = part;
                a["covers"] = part;
                a["seed"] = J(f(at));
                if (first) { ctx.Replace(z.With(attrs: a)); first = false; }
                else Copy(ctx, z, a);
            }
            if (first) ctx.Remove(z.Id);
        }
    }

    static void SlabFinishes(ICommandContext ctx, Entity was, IReadOnlyList<PathPiece> pieces)
    {
        var m = ctx.Model;
        var zones = OnHost(m, was.Id).ToList();
        if (zones.Count == 0) return;
        var oldO = Slabs.Contour(was) is { Count: > 0 } oc ? oc[0] : default;
        var ids = pieces.Select(p => p.Id).ToList();
        var outlines = ids.ToDictionary(id => id, id => ArcOutline.Points(Slabs.Corners(m.Get(id)!), Slabs.Rises(m.Get(id)!)));
        foreach (var role in zones.Select(z => z.Str("face")).OfType<string>().Distinct().ToList())
        {
            // The face's directions in plan, from a part (a floor's faces keep them).
            if (FinishZones.FrameOf(m, ctx.Services, m.Get(ids[0])!, role) is not { } frame || Math.Abs(frame.N.Z) < 0.7) continue;
            var (u, v) = (new Vec2(frame.U.X, frame.U.Y), new Vec2(frame.V.X, frame.V.Y));
            Vec2 Plan(Vec2 q) => oldO + u * q.X + v * q.Y;
            Vec2 Local(Vec2 p, Vec2 o) => new(Vec2.Dot(p - o, u), Vec2.Dot(p - o, v));
            Func<Vec2, Vec2>? Shift(string part, string _) =>
                Slabs.Contour(m.Get(part)!) is { Count: > 0 } c ? q => Local(Plan(q), c[0]) : null;
            string? PartOf(Vec2 q) => ids.FirstOrDefault(id => Polygon2.Contains(outlines[id], Plan(q)));
            Vec2? Into(string part, Vec2 q)
            {
                var ring = outlines[part];
                var p = Plan(q);
                var near = Enumerable.Range(0, ring.Count).Select(i => Closest(ring[i], ring[(i + 1) % ring.Count], p)).MinBy(x => (x - p).Length);
                var centre = ring.Aggregate(default(Vec2), (s, x) => s + x) * (1.0 / ring.Count);
                var inside = near + (centre - near).Normalized() * Inside;
                return Polygon2.Contains(ring, inside) ? Local(inside, oldO) : null;
            }
            var mine = zones.Where(z => z.Str("face") == role).ToList();
            RehostOn(ctx, was, mine, ids, Shift, PartOf, Into, _ => ids);
        }
    }
}
