using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A piece of a path element cut or carried on: its id, and the stretch of the element's axis as it was that it covers (metres from its start; below 0 or past its length where an end was carried on).</summary>
public readonly record struct PathPiece(string Id, double S0, double S1);

/// <summary>
/// What a module keeps right on its path elements when they are split, trimmed or carried on (<see cref="PathEdits"/>): the
/// attributes that belong to a stretch of the element or to one of its ends (a wall's layer ends, its layer extents at an end)
/// and the elements it hosts (a wall's openings and sweeps), put on the piece they fall on.
/// </summary>
public interface IPathPieces
{
    bool Handles(Entity e);

    /// <summary>Why the element cannot be cut or its ends moved this way (a wall with a top profile of its own along it); null when it can.</summary>
    string? Refuse(IModelView m, Entity e);

    /// <summary>
    /// The attributes of the piece of <paramref name="e"/> from <paramref name="s0"/> to <paramref name="s1"/> along its axis,
    /// written into <paramref name="attrs"/> (a copy of the element's own). <paramref name="newStart"/>, <paramref name="newEnd"/>:
    /// that end of the piece is a cut, not one of the element's own ends.
    /// </summary>
    void Piece(IModelView m, Entity e, JsonObject attrs, double s0, double s1, bool newStart, bool newEnd);

    /// <summary>
    /// What <paramref name="e"/> (as it was) hosted, put on the pieces once they are written: each on the piece it falls on,
    /// placed from that piece's start. Refuses (a <see cref="CommandException"/>) what would lie across a cut or where the
    /// element is cut away.
    /// </summary>
    void Rehost(ICommandContext ctx, Entity e, IReadOnlyList<PathPiece> pieces);
}

/// <summary>
/// CAD's edits on elements along an axis (audit 2026-11-15, §6: offset, trim, extend and split existed only for drafted lines):
/// walls, beams and strip footings, straight or curved. Each is one command, one undo step.
/// <list type="bullet">
/// <item><c>element.split</c>: two elements where there was one, at a point or where another element or a line crosses it. The
/// first keeps its GlobalId and the stretch from its start; the second is a copy of it (its type, storey, material layers on
/// the same side, properties, groups) from the cut to its end. The joins at its end go with the second; the two are joined in
/// line at the cut; openings and sweeps go to the part they fall on (<see cref="IPathPieces"/>), and one across the cut refuses.</item>
/// <item><c>element.trimExtend</c>: one end brought to a boundary, Revit's Trim/Extend: the element is picked on the part to keep,
/// and the end on the other side of the boundary (or the end nearer it, when it lies past the element) goes to it. The boundary
/// is another element's axis, or the face of it nearer (or farther from) the part kept, or a drafted line or arc. A curved
/// element is carried on along its circle.</item>
/// <item><c>element.offset</c>: a parallel copy at a distance, on the side picked (a curved element's copy is concentric), measured
/// from the axis or between the faces; the copy keeps what a copy keeps (openings and fills too).</item>
/// </list>
/// </summary>
public static class PathEdits
{
    /// <summary>The shortest a piece may be left.</summary>
    public const double MinLength = 0.05;

    public static readonly string[] Faces = ["axis", "near", "far"];

    /// <summary>The attributes holding something for each end, as {start, end}: a cut piece's new end takes none.</summary>
    static readonly string[] EndObjects = [Joins.AutoKey, "steelEnds"];

    public static void Register(IModuleRegistry r)
    {
        // {id, at: [x, y] (a point on the element, or near it) | with: id (an element crossing it) | line: [[x, y], [x, y]] (a line
        // crossing it) | arc: {c, r}; at? beside with/line/arc picks the crossing nearest it; coupling?: false (an MEP run: open ends
        // at the cut, no coupling)} → {ids: [first, second], at}. Railings, MEP runs, paths and roads too, along their polylines (IPolylinePieces).
        r.Command("element.split", "Split element", (ctx, a) => Split(ctx, a));
        // {id, at (on the part to keep), boundary: id | line: [[x, y], [x, y]] | arc: {c: [x, y], r}, face?: axis | near | far} → {id}.
        r.Command("element.trimExtend", "Trim or extend element", (ctx, a) => TrimExtend(ctx, a));
        // {id, side: [x, y], distance | through: true, from?: axis | face} → {id}.
        r.Command("element.offset", "Offset element", (ctx, a) => Offset(ctx, a));
    }

    // ─── The axis ─────────────────────────────────────────────────────────────

    static (Vec2 C, double R, double A0, int Sense) Circle(PathCurve c)
    {
        var centre = c.Centre;
        var d = c.Start - centre;
        return (centre, c.Radius, Math.Atan2(d.Y, d.X), c.Rise > 0 ? -1 : 1);
    }

    /// <summary>The point <paramref name="s"/> along the axis; past its ends a straight one goes on straight, a curved one on its circle.</summary>
    public static Vec2 OnAxis(PathCurve c, double s)
    {
        if (!c.IsArc) return c.PointAt(s);
        var (centre, r, a0, sense) = Circle(c);
        var a = a0 + sense * s / r;
        return centre + new Vec2(Math.Cos(a), Math.Sin(a)) * r;
    }

    /// <summary>Where a point on the axis's line (or circle) lies along it: below 0 before its start, past its length beyond its end (an arc's, the nearer way round).</summary>
    public static double Along(PathCurve c, Vec2 p)
    {
        if (!c.IsArc) return c.Project(p);
        var (centre, r, a0, sense) = Circle(c);
        var d = p - centre;
        var turned = sense * (Math.Atan2(d.Y, d.X) - a0);
        turned -= 2 * Math.PI * Math.Floor(turned / (2 * Math.PI));
        var sweep = c.Sweep;
        if (turned <= sweep + 1e-12) return turned * r;
        var past = (turned - sweep) * r;
        var before = (2 * Math.PI - turned) * r;
        return past <= before ? c.Length + past : -before;
    }

    /// <summary>The axis of the stretch from <paramref name="s0"/> to <paramref name="s1"/>: its ends and its rise.</summary>
    public static (Vec2 Start, Vec2 End, double Rise) Stretch(PathCurve c, double s0, double s1)
    {
        var (a, b) = (OnAxis(c, s0), OnAxis(c, s1));
        if (!c.IsArc) return (a, b, 0);
        if (s1 - s0 >= 2 * Math.PI * c.Radius - 1e-3) throw new CommandException("The curved element would close round on itself.");
        return (a, b, PathCurve.RiseThrough(a, b, OnAxis(c, (s0 + s1) / 2)));
    }

    /// <summary>A line (from a point, along a direction) or a circle: what an axis or a boundary is in plan, its ends let run on.</summary>
    public readonly record struct Curve(Vec2 P, Vec2 Dir, double R)
    {
        public bool IsCircle => R > 0;
        public static Curve Line(Vec2 a, Vec2 b) => new(a, (b - a).Normalized(), 0);
        public static Curve Round(Vec2 c, double r) => new(c, default, r);

        /// <summary>The axis as a line or circle, <paramref name="y"/> to its left.</summary>
        public static Curve Of(PathCurve c, double y = 0)
        {
            if (!c.IsArc) { var d = (c.End - c.Start).Normalized(); return new Curve(c.Start + d.Perp * y, d, 0); }
            var (centre, r, _, sense) = Circle(c);
            var radius = r - sense * y;
            if (radius <= 1e-6) throw new CommandException("That face of the curved element turns round nothing.");
            return Round(centre, radius);
        }
    }

    /// <summary>Where two lines or circles cross (none when parallel, apart or the same).</summary>
    public static List<Vec2> Cross(Curve a, Curve b)
    {
        if (!a.IsCircle && !b.IsCircle)
        {
            var den = Vec2.Cross(a.Dir, b.Dir);
            if (Math.Abs(den) < 1e-9) return [];
            return [a.P + a.Dir * (Vec2.Cross(b.P - a.P, b.Dir) / den)];
        }
        if (a.IsCircle && !b.IsCircle) (a, b) = (b, a);
        if (!a.IsCircle)
        {
            // A line and a circle.
            var foot = a.P + a.Dir * Vec2.Dot(b.P - a.P, a.Dir);
            var h = (foot - b.P).Length;
            if (h > b.R + 1e-9) return [];
            var half = Math.Sqrt(Math.Max(0, b.R * b.R - h * h));
            return half < 1e-9 ? [foot] : [foot - a.Dir * half, foot + a.Dir * half];
        }
        // Two circles.
        var d = b.P - a.P;
        var dist = d.Length;
        if (dist < 1e-9 || dist > a.R + b.R + 1e-9 || dist < Math.Abs(a.R - b.R) - 1e-9) return [];
        var along = (dist * dist + a.R * a.R - b.R * b.R) / (2 * dist);
        var off = Math.Sqrt(Math.Max(0, a.R * a.R - along * along));
        var u = d * (1 / dist);
        var mid = a.P + u * along;
        return off < 1e-9 ? [mid] : [mid + u.Perp * off, mid - u.Perp * off];
    }

    // ─── What is edited ───────────────────────────────────────────────────────

    /// <summary>A path element of a module's, with its axis, that may be edited: not pinned, not a reference from a file, nothing a module refuses.</summary>
    static Entity Editable(ICommandContext ctx, string? id)
    {
        var m = ctx.Model;
        var e = m.Get(id ?? "") ?? throw new CommandException("The element is not in the project.");
        var paths = ctx.Services.Get<IPathElements>();
        if (paths?.IsPath(e.Class) != true) throw new CommandException($"„{e.Name}” has no axis to cut: walls, beams and strip footings do.");
        if (e.Attrs["start"] is not JsonArray || e.Attrs["end"] is not JsonArray || StoredBodies.Has(e) || e.Attrs["reference"] != null)
            throw new CommandException($"„{e.Name}” is a reference from a file: convert it to native first.");
        Pins.Refuse([e]);
        foreach (var h in Hooks(ctx, e))
            if (h.Refuse(m, e) is { } why) throw new CommandException(why);
        return e;
    }

    static IEnumerable<IPathPieces> Hooks(ICommandContext ctx, Entity e) => ctx.Services.All<IPathPieces>().Where(h => h.Handles(e));

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    /// <summary>The element's attributes for its stretch from <paramref name="s0"/> to <paramref name="s1"/>.</summary>
    static JsonObject PieceAttrs(ICommandContext ctx, Entity e, double s0, double s1, bool newStart, bool newEnd)
    {
        var c = PathCurve.Of(e);
        var (p, q, rise) = Stretch(c, s0, s1);
        var attrs = e.CloneAttrs();
        attrs["start"] = J(p);
        attrs["end"] = J(q);
        if (Math.Abs(rise) > 1e-9) attrs[PathCurve.Key] = Math.Round(rise, 9); else attrs.Remove(PathCurve.Key);
        // An end moved or made by the cut has no end cut of its own (its angle): its join, if any, cuts it.
        if (newStart || Math.Abs(s0) > 1e-9) attrs.Remove(Joins.StartAngle);
        if (newEnd || Math.Abs(s1 - c.Length) > 1e-9) attrs.Remove(Joins.EndAngle);
        foreach (var k in EndObjects)
            if (attrs[k] is JsonObject o)
            {
                if (newStart) o.Remove("start");
                if (newEnd) o.Remove("end");
                if (o.Count == 0) attrs.Remove(k);
            }
        foreach (var h in Hooks(ctx, e)) h.Piece(ctx.Model, e, attrs, s0, s1, newStart, newEnd);
        return attrs;
    }

    /// <summary>
    /// <paramref name="to"/> made like <paramref name="from"/> in what relates it: its storey, type, material (a layer set usage
    /// of its own, the same layers on the same side), the groups and assemblies it is part of.
    /// </summary>
    public static void CopyRelations(ICommandContext ctx, Entity from, Entity to)
    {
        var m = ctx.Model;
        foreach (var r in m.RelationsOf(from.Id).Where(r => r.Related.Contains(from.Id)).ToList())
            switch (r.Class)
            {
                case IfcClass.RelContainedInSpatialStructure or IfcClass.RelDefinesByType:
                    ctx.Relate(r.Class, r.Relating, to.Id);
                    break;
                case IfcClass.RelAssociatesMaterial when m.Get(r.Relating) is { } relating:
                    if (relating.Class == IfcClass.MaterialLayerSetUsage)
                    {
                        var usage = new Entity(ctx.NewId(), relating.Class, relating.Name, relating.CloneAttrs(), relating.PredefinedType);
                        ctx.Add(usage);
                        foreach (var ur in m.RelationsOf(relating.Id).Where(x => x.Related.Contains(relating.Id)).ToList()) ctx.Relate(ur.Class, ur.Relating, usage.Id);
                        ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelAssociatesMaterial, usage.Id, [to.Id]));
                    }
                    else ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelAssociatesMaterial, relating.Id, [to.Id]));
                    break;
                case Groups.Rel or IfcClass.RelAggregates:
                    if (ctx.Model.GetRelation(r.Id) is { } now && !now.Related.Contains(to.Id)) ctx.ReplaceRelation(now with { Related = [.. now.Related, to.Id] });
                    break;
            }
    }

    /// <summary>A new element like <paramref name="like"/> with <paramref name="attrs"/>: what it came from in a file is the first one's only.</summary>
    public static Entity AddLike(ICommandContext ctx, Entity like, JsonObject attrs)
    {
        attrs.Remove("imported");
        attrs.Remove(IfcSource.Key);
        attrs.Remove(Pins.Key);
        var n = new Entity(ctx.NewId(), like.Class, like.Name, attrs, like.PredefinedType);
        ctx.Add(n);
        CopyRelations(ctx, like, n);
        return ctx.Model.Get(n.Id)!;
    }

    /// <summary>Which end of the element a join holds it by (ATSTART, ATEND, ATPATH), from the element's side.</summary>
    static string? Mine(Relation r, string id) => (string?)r.Attrs?[r.Relating == id ? "relatingConnection" : "relatedConnection"];

    /// <summary>Where along the element a join meets it: the other element's end or crossing nearest its axis.</summary>
    static double MeetAlong(IModelView m, PathCurve c, Relation r, string id)
    {
        if (m.Get(Joins.Other(r, id)) is not { } other || other.Attrs["start"] is not JsonArray) return 0;
        var oc = PathCurve.Of(other);
        var points = new List<Vec2> { oc.Start, oc.End };
        try { points.AddRange(Cross(Curve.Of(c), Curve.Of(oc))); } catch (CommandException) { }
        var best = points.MinBy(p => c.Distance(p));
        return Along(c, best);
    }

    // ─── Split ────────────────────────────────────────────────────────────────

    /// <summary>The element named by <c>id</c> when it lies along a polyline (<see cref="IPolylinePieces"/>), with its module's hook.</summary>
    static (Entity, IPolylinePieces)? Polyline(ICommandContext ctx, JsonObject a) =>
        ctx.Model.Get((string?)a["id"] ?? "") is { } e && ctx.Services.Get<IPathElements>()?.IsPath(e.Class) != true && PolylineEdits.Hook(ctx, e) is { } h ? (e, h) : null;

    public static JsonObject Split(ICommandContext ctx, JsonObject a)
    {
        if (Polyline(ctx, a) is { } poly) return PolylineEdits.Split(ctx, a, poly.Item1, poly.Item2);
        var m = ctx.Model;
        var e = Editable(ctx, (string?)a["id"]);
        var c = PathCurve.Of(e);
        var length = c.Length;
        var at = Args.TryPoint(a["at"]);
        double s;
        if (a["with"] != null || a["line"] != null || a["arc"] != null)
        {
            var crossing = Boundary(ctx, a, e, "with", null);
            var hits = Cross(Curve.Of(c), crossing).Select(p => Along(c, p)).Where(x => x > 1e-6 && x < length - 1e-6).ToList();
            if (hits.Count == 0) throw new CommandException($"That does not cross „{e.Name}”.");
            s = at is { } pick ? hits.MinBy(x => Math.Abs(x - Along(c, pick))) : hits.MinBy(x => Math.Abs(x - length / 2));
        }
        else s = Along(c, at ?? throw new CommandException("Where? A point on the element."));
        if (s < MinLength || s > length - MinLength)
            throw new CommandException($"Split „{e.Name}” at least {MinLength * 1000:0} mm from its ends: each part needs a length.");

        // The joins at its end, and those that meet it past the cut, go with the second part.
        var toSecond = Joins.Of(m, e.Id).Where(r => Mine(r, e.Id) switch
        {
            "ATSTART" => false,
            "ATEND" => true,
            _ => MeetAlong(m, c, r, e.Id) > s,
        }).ToList();

        var firstAttrs = PieceAttrs(ctx, e, 0, s, false, true);
        var secondAttrs = PieceAttrs(ctx, e, s, length, true, false);
        ctx.Replace(e.With(attrs: firstAttrs));
        var second = AddLike(ctx, e, secondAttrs);
        foreach (var r in toSecond)
            if (ctx.Model.GetRelation(r.Id) is { } now)
                ctx.ReplaceRelation(now with { Relating = now.Relating == e.Id ? second.Id : now.Relating, Related = [.. now.Related.Select(x => x == e.Id ? second.Id : x)] });
        foreach (var h in Hooks(ctx, e)) h.Rehost(ctx, e, [new PathPiece(e.Id, 0, s), new PathPiece(second.Id, s, length)]);

        if (ctx.Services.Get<IPathElements>() is { } paths)
        {
            Joins.Refresh(ctx, paths, [e.Id, second.Id]);
            // The two parts meet in line at the cut, as one element did.
            try { Joins.Join(ctx, paths, ctx.Model.Get(e.Id)!, ctx.Model.Get(second.Id)!); } catch (CommandException) { }
        }
        return new JsonObject { ["ids"] = new JsonArray(e.Id, second.Id), ["at"] = J(OnAxis(c, s)) };
    }

    // ─── Trim and extend ──────────────────────────────────────────────────────

    /// <summary>
    /// The line or circle named by <c>{key}</c> (an element's id: its axis, or a face of it) or by <c>line</c> or <c>arc</c>.
    /// <paramref name="keep"/>: the point on the element kept, which says which face is near.
    /// </summary>
    public static Curve BoundaryOf(ICommandContext ctx, JsonObject a, Entity e, string key, Vec2 keep) => Boundary(ctx, a, e, key, keep);

    static Curve Boundary(ICommandContext ctx, JsonObject a, Entity e, string key, Vec2? keep)
    {
        if (a["line"] is JsonArray { Count: >= 2 } line)
        {
            var (p, q) = (Args.PointOf(line[0]), Args.PointOf(line[1]));
            if ((q - p).Length < 1e-9) throw new CommandException("The line has no length.");
            return Curve.Line(p, q);
        }
        if (a["arc"] is JsonObject arc)
        {
            var r = Json.Number(arc["r"]) ?? 0;
            if (!(r > 1e-9)) throw new CommandException("The arc has no radius.");
            return Curve.Round(Args.PointOf(arc["c"]), r);
        }
        var id = (string?)a[key] ?? throw new CommandException("To what? Pick an element, or a drafted line or arc.");
        if (id == e.Id) throw new CommandException("Pick another element as the boundary.");
        var b = ctx.Model.Get(id) ?? throw new CommandException("The boundary is not in the project.");
        if (b.Attrs["start"] is not JsonArray || b.Attrs["end"] is not JsonArray) throw new CommandException($"„{b.Name}” has no axis to trim to: pick a wall, a beam, a strip footing, or a drafted line.");
        var bc = PathCurve.Of(b);
        var face = (string?)a["face"] ?? "axis";
        if (!Faces.Contains(face)) throw new CommandException("To its axis, or to its near or far face.");
        if (face == "axis") return Curve.Of(bc);
        var frame = ctx.Services.Get<IPathElements>()?.FrameOf(b, ctx.Model) ?? throw new CommandException($"„{b.Name}” has no faces to trim to: take its axis.");
        // The face on the side of the part kept is the near one.
        var from = keep ?? OnAxis(PathCurve.Of(e), PathCurve.Of(e).Length / 2);
        var left = Vec2.Cross(bc.TangentAt(Math.Clamp(bc.Project(from), 0, bc.Length)), from - bc.PointAt(Math.Clamp(bc.Project(from), 0, bc.Length))) > 0;
        var y = (face == "near") == left ? frame.YMax : frame.YMin;
        return Curve.Of(bc, y);
    }

    public static JsonObject TrimExtend(ICommandContext ctx, JsonObject a)
    {
        if (Polyline(ctx, a) is { } poly) return PolylineEdits.TrimExtend(ctx, a, poly.Item1, poly.Item2);
        var m = ctx.Model;
        var e = Editable(ctx, (string?)a["id"]);
        var c = PathCurve.Of(e);
        var length = c.Length;
        var pick = Args.TryPoint(a["at"]) ?? OnAxis(c, length / 2);
        var kept = Math.Clamp(Along(c, pick), 0, length);
        var boundary = Boundary(ctx, a, e, "boundary", pick);
        var hits = Cross(Curve.Of(c), boundary).Select(p => Along(c, p)).ToList();
        if (hits.Count == 0) throw new CommandException($"„{e.Name}” never meets the boundary, carried on as it runs.");
        // The crossing on the element, else the one nearest an end of it; equal, the one nearer the pick.
        double Off(double x) => x < 0 ? -x : x > length ? x - length : 0;
        var sx = hits.OrderBy(Off).ThenBy(x => Math.Abs(x - kept)).First();
        var (s0, s1) = sx <= 1e-9 ? (sx, length)
            : sx >= length - 1e-9 ? (0, sx)
            : kept <= sx ? (0, sx) : (sx, length);
        if (Math.Abs(s0) < 1e-9 && Math.Abs(s1 - length) < 1e-9) throw new CommandException($"„{e.Name}” already ends at the boundary.");
        if (s1 - s0 < MinLength) throw new CommandException($"„{e.Name}” would be left shorter than {MinLength * 1000:0} mm: pick it on the part to keep.");
        var movedStart = Math.Abs(s0) > 1e-9;

        // The joins at the moved end go, and those that met the part cut away.
        foreach (var r in Joins.Of(m, e.Id).ToList())
        {
            var mine = Mine(r, e.Id);
            var goes = mine switch
            {
                "ATSTART" => movedStart,
                "ATEND" => !movedStart,
                _ => MeetAlong(m, c, r, e.Id) is var at && (at < s0 - 1e-6 || at > s1 + 1e-6),
            };
            if (goes) ctx.RemoveRelation(r.Id);
        }
        ctx.Replace(e.With(attrs: PieceAttrs(ctx, e, s0, s1, false, false)));
        foreach (var h in Hooks(ctx, e)) h.Rehost(ctx, e, [new PathPiece(e.Id, s0, s1)]);
        if (ctx.Services.Get<IPathElements>() is { } paths)
        {
            Joins.Refresh(ctx, paths, [e.Id]);
            // Brought to the axis of another of its kind, the two are joined there (a corner or a T).
            if ((string?)a["boundary"] is { } bid && ((string?)a["face"] ?? "axis") == "axis" && ctx.Model.Get(bid) is { } b && b.Class == e.Class)
                try { Joins.Join(ctx, paths, ctx.Model.Get(e.Id)!, b); } catch (CommandException) { }
        }
        return new JsonObject { ["id"] = e.Id };
    }

    // ─── Offset ───────────────────────────────────────────────────────────────

    public static JsonObject Offset(ICommandContext ctx, JsonObject a)
    {
        if (Polyline(ctx, a) is { } poly) return PolylineEdits.Offset(ctx, a, poly.Item1, poly.Item2);
        var m = ctx.Model;
        var e = m.Get((string?)a["id"] ?? "") ?? throw new CommandException("The element is not in the project.");
        if (ctx.Services.Get<IPathElements>()?.IsPath(e.Class) != true || e.Attrs["start"] is not JsonArray || e.Attrs["end"] is not JsonArray)
            throw new CommandException($"„{e.Name}” has no axis to offset: walls, beams and strip footings do.");
        var c = PathCurve.Of(e);
        var side = Args.TryPoint(a["side"]) ?? throw new CommandException("Which side? A point on it.");
        var foot = Math.Clamp(c.Project(side), 0, c.Length);
        var across = Vec2.Dot(side - c.PointAt(foot), c.TangentAt(foot).Perp);
        if (Math.Abs(across) < 1e-9) throw new CommandException("Pick a point off the axis: the side the copy goes.");
        var sign = Math.Sign(across);
        var through = a["through"] is JsonValue tv && tv.TryGetValue<bool>(out var tb) && tb;
        var from = (string?)a["from"] ?? "axis";
        if (from is not ("axis" or "face")) throw new CommandException("Measured from the axis, or between the faces.");
        var frame = ctx.Services.Get<IPathElements>()?.FrameOf(e, m);
        var width = frame is { } f ? f.YMax - f.YMin : 0;
        double shift;
        if (through) shift = Math.Abs(across);
        else
        {
            var d = Json.Number(a["distance"]) ?? throw new CommandException("Offset by how much?");
            if (!double.IsFinite(d) || d <= 1e-9) throw new CommandException("The offset distance must be positive.");
            // Between the faces: the copy's face stands the distance off the element's face on that side.
            shift = from == "face" ? d + width : d;
        }
        if (shift < width - 1e-9) throw new CommandException($"The copy would overlap „{e.Name}”: offset it at least {width * 1000:0} mm from the axis.");
        var y = sign * shift;

        if (!c.IsArc)
        {
            var id = Placement.Copy(ctx, e, Placement2D.Move(c.TangentAt(0).Perp * y));
            return new JsonObject { ["id"] = id };
        }
        // A curved element's copy is concentric: its radius y less (to the left of a counter-clockwise arc, towards its centre).
        var copyId = Placement.Copy(ctx, e, Placement2D.Move(new Vec2(0, 0)));
        var copy = ctx.Model.Get(copyId)!;
        var (p, q) = (c.At(0, y), c.At(c.Length, y));
        var rise = PathCurve.RiseThrough(p, q, c.At(c.Length / 2, y));
        var attrs = copy.CloneAttrs();
        attrs["start"] = J(p);
        attrs["end"] = J(q);
        attrs[PathCurve.Key] = Math.Round(rise, 9);
        ctx.Replace(copy.With(attrs: attrs));
        // What it hosts keeps its angle round the centre: its distances along scale with the radius.
        var k = new PathCurve(p, q, rise).Length / c.Length;
        foreach (var h in ctx.Model.RelatedOf(copyId, IfcClass.RelVoidsElement).ToList())
        {
            var ha = h.CloneAttrs();
            var changed = false;
            foreach (var key in new[] { "along", "start", "end" })
                if (Json.Number(ha[key]) is { } v && ha[key] is JsonValue) { ha[key] = Math.Round(v * k, 9); changed = true; }
            if (changed) ctx.Replace(h.With(attrs: ha));
        }
        return new JsonObject { ["id"] = copyId };
    }
}
