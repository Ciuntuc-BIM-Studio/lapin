using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall's reference line (its location line). The axis stays on the middle of the core (Lapin's layer rule, so IFC's Axis,
/// the layer set usage and every join keep working as they did); the reference line is a line parallel to it (concentric on a
/// curved wall), derived on every ask from the wall's layers:
/// <list type="bullet">
/// <item>the line a wall is drawn on (testers' request, decision 237): the core's centre, the core's exterior or interior face,
/// the wall's exterior or interior finish face, a layer's face, or a signed offset from the core's centre; the exterior is the
/// side of the first layer (the wall's right as drawn). The axis is set off from the line by as much, and where the line goes on
/// from a wall drawn before on the same faces, the two axes are made to meet where those faces meet;</item>
/// <item>kept with the wall when asked (forum topic 159, GitHub #63, decision 685; <see cref="Key"/>): then it is what the wall
/// is snapped and moved by, and the wall grows round it when its build-up changes (a thickness, a type) and turns round it when
/// flipped. Absent, the reference is the core's centre and the wall behaves as it always did.</item>
/// </list>
/// </summary>
public static class WallReference
{
    /// <summary>The attribute a wall keeps its reference line in: {kind, offset?, layer?}.</summary>
    public const string Key = "referenceLine";

    /// <summary>The kinds of reference line; "layer" takes a face index counted from the exterior (0 the exterior face), "offset" a distance left of the core's centre.</summary>
    public static readonly string[] Kinds = ["coreCentre", "coreExterior", "coreInterior", "exteriorFace", "interiorFace", "layer", "offset"];

    /// <summary>The names the Wall tool sent before (decision 237), still taken.</summary>
    public static readonly string[] Lines = ["core", "coreExterior", "coreInterior", "exterior", "interior"];

    /// <summary>A reference line: its kind, the offset left of the core's centre (kind "offset", metres) and the layer face (kind "layer", 0 = the exterior face).</summary>
    public readonly record struct Line(string Kind, double Offset = 0, int Layer = 0)
    {
        public bool IsCoreCentre => Kind == "coreCentre" || (Kind == "offset" && Math.Abs(Offset) < 1e-12);

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["kind"] = Kind };
            if (Kind == "offset") o["offset"] = Math.Round(Offset, 9);
            if (Kind == "layer") o["layer"] = Layer;
            return o;
        }
    }

    static readonly Line CoreCentre = new("coreCentre");

    /// <summary>A reference line as asked: a name (the old ones too), or {kind, offset?, layer?}.</summary>
    public static Line Parse(JsonNode? n)
    {
        if (n is JsonObject o)
        {
            var kind = Canonical((string?)o["kind"]);
            if (kind == "offset")
            {
                var off = Json.Number(o["offset"]) ?? throw new CommandException("Give the reference line's offset from the core's centre.");
                if (!double.IsFinite(off) || Math.Abs(off) > 100) throw new CommandException("A reference line's offset is a distance under 100 m.");
                return new Line(kind, off);
            }
            if (kind == "layer")
            {
                var layer = Json.Number(o["layer"]) ?? throw new CommandException("Give the layer face the reference line is on.");
                if (layer < 0 || layer != Math.Floor(layer)) throw new CommandException("A layer face is counted 0, 1, 2… from the exterior.");
                return new Line(kind, Layer: (int)layer);
            }
            return new Line(kind);
        }
        return new Line(Canonical((string?)n));
    }

    static string Canonical(string? name) => name switch
    {
        null or "" or "core" or "coreCentre" => "coreCentre",
        "exterior" or "exteriorFace" => "exteriorFace",
        "interior" or "interiorFace" => "interiorFace",
        "coreExterior" or "coreInterior" or "layer" or "offset" => name,
        _ => throw new CommandException("A wall's reference line is its core's centre or faces, its exterior or interior face, a layer's face, or an offset."),
    };

    /// <summary>The reference line the wall keeps; null: none (the core's centre).</summary>
    public static Line? Stored(Entity wall) => wall.Attrs[Key] is JsonObject o ? TryParse(o) : null;

    static Line? TryParse(JsonObject o)
    {
        try { var r = Parse(o); return r.IsCoreCentre ? null : r; }
        catch (CommandException) { return null; }
    }

    static Vec2 P(JsonNode? n) => Args.PointOf(n);
    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    /// <summary>Whether a wall's end ("start" or "end") is joined to nothing yet: a joined end belongs to its corner and is not moved.</summary>
    static bool FreeEnd(IModelView m, string id, string key) =>
        !Joins.Of(m, id).Any(r => (string?)r.Attrs?[r.Relating == id ? "relatingConnection" : "relatedConnection"] == (key == "start" ? "ATSTART" : "ATEND"));

    /// <summary>How far left of the axis the reference line lies (the axis then goes that far right of the line drawn).</summary>
    public static double LeftOf(IReadOnlyList<Walls.Band> bands, string reference) => LeftOf(bands, Parse(reference));

    /// <summary>How far left of the axis the reference line lies: the one rule every use of it goes by.</summary>
    public static double LeftOf(IReadOnlyList<Walls.Band> bands, Line reference)
    {
        if (reference.Kind == "offset") return reference.Offset;
        if (bands.Count == 0 || reference.Kind == "coreCentre") return 0;
        var core = bands.Any(b => b.Core) ? bands.First(b => b.Core) : bands.MaxBy(b => b.To - b.From)!;
        // The exterior: the first layer's side (the right, unless the wall is flipped).
        var exteriorRight = bands[0].From <= bands[^1].From;
        var (lo, hi) = (bands.Min(b => b.From), bands.Max(b => b.To));
        return reference.Kind switch
        {
            "coreExterior" => exteriorRight ? core.From : core.To,
            "coreInterior" => exteriorRight ? core.To : core.From,
            "exteriorFace" => exteriorRight ? lo : hi,
            "interiorFace" => exteriorRight ? hi : lo,
            // Face i: layer i's exterior side; the last one, the last layer's interior side.
            "layer" when reference.Layer < bands.Count => exteriorRight ? bands[reference.Layer].From : bands[reference.Layer].To,
            "layer" when reference.Layer == bands.Count => exteriorRight ? bands[^1].To : bands[^1].From,
            "layer" => throw new CommandException($"The wall has {bands.Count + 1} layer faces (0 to {bands.Count}, from the exterior)."),
            _ => throw new CommandException("A wall's reference line is its core's centre or faces, its exterior or interior face, a layer's face, or an offset."),
        };
    }

    /// <summary>A path set off <paramref name="y"/> to its left: a parallel line, or a concentric arc.</summary>
    public static PathCurve Offset(PathCurve c, double y)
    {
        if (Math.Abs(y) < 1e-12) return c;
        if (!c.IsArc)
        {
            var n = (c.End - c.Start).Normalized().Perp * y;
            return new PathCurve(c.Start + n, c.End + n, 0);
        }
        // The arc bulges left when its rise is positive, its centre on the right: left goes away from the centre.
        if (c.Radius + Math.Sign(c.Rise) * y < 1e-3) throw new CommandException("The line is past the arc's centre: the wall would turn inside out.");
        var (s, e, m) = (c.At(0, y), c.At(c.Length, y), c.At(c.Length / 2, y));
        return new PathCurve(s, e, PathCurve.RiseThrough(s, e, m));
    }

    /// <summary>The wall's reference line, derived from its axis and layers; null when it keeps none (its axis is the line).</summary>
    public static PathCurve? LineOf(IModelView m, Entity wall) =>
        Stored(wall) is { } r ? Offset(PathCurve.Of(wall), SafeLeft(Walls.Section(m, wall), r)) : null;

    /// <summary>A kept layer face the build-up has lost (fewer layers now) falls back on the nearest face it has.</summary>
    static double SafeLeft(IReadOnlyList<Walls.Band> bands, Line r) =>
        r.Kind == "layer" && r.Layer > bands.Count ? LeftOf(bands, r with { Layer = bands.Count }) : LeftOf(bands, r);

    /// <summary>Where the wall's kept reference line is now (left of its axis), before its build-up changes; null when it keeps none.</summary>
    public static double? Before(IModelView m, Entity wall) => Stored(wall) is { } r ? SafeLeft(Walls.Section(m, wall), r) : null;

    /// <summary>
    /// After the wall's build-up changed (a type, a thickness, a flip): its axis set off so the kept reference line is where it was,
    /// <paramref name="before"/> left of the old axis. Its joined neighbours' ends go with it (<see cref="Joins.MoveEnds"/>).
    /// </summary>
    public static void Keep(IEdit edit, string wallId, double? before)
    {
        if (before is not { } y0 || edit.Model.Get(wallId) is not { } wall || Stored(wall) is not { } r) return;
        var shift = y0 - SafeLeft(Walls.Section(edit.Model, wall), r);
        if (Math.Abs(shift) < 1e-9) return;
        var moved = Offset(PathCurve.Of(wall), shift);
        var attrs = wall.CloneAttrs();
        if (moved.IsArc) attrs[PathCurve.Key] = Math.Round(moved.Rise, 9);
        var paths = (edit as ICommandContext)?.Services.Get<IPathElements>();
        if (paths == null)
        {
            attrs["start"] = J(moved.Start);
            attrs["end"] = J(moved.End);
            edit.Replace(wall.With(attrs: attrs));
            return;
        }
        edit.Replace(wall.With(attrs: attrs));
        Joins.MoveEnds(edit, paths, edit.Model.Get(wallId)!, Round(moved.Start), Round(moved.End));
    }

    static Vec2 Round(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    /// <summary>The walls keep <paramref name="line"/> as their reference (the core's centre: none); the walls themselves do not move.</summary>
    public static void Set(IEdit edit, IEnumerable<string> ids, Line line)
    {
        var any = false;
        foreach (var id in ids.Distinct())
        {
            any = true;
            var wall = Spatial.Require(edit.Model, id, IfcClass.Wall);
            // Checked against the wall's own layers: a layer face it has.
            LeftOf(Walls.Section(edit.Model, wall), line);
            var attrs = wall.CloneAttrs();
            if (line.IsCoreCentre) { if (attrs.Remove(Key)) edit.Replace(wall.With(attrs: attrs)); continue; }
            attrs[Key] = line.ToJson();
            edit.Replace(wall.With(attrs: attrs));
        }
        if (!any) throw new CommandException("Which wall?");
    }

    /// <summary>A wall just made, its axis set off from the line it was drawn on; a straight one's ends met with a wall drawn before on the same faces.</summary>
    public static Entity Apply(ICommandContext ctx, Entity wall, string reference) => Apply(ctx, wall, Parse(reference), false);

    /// <summary>
    /// A wall just made on <paramref name="reference"/>: its axis set off from the line drawn (a curved wall's concentric with it);
    /// a straight one's ends met with a wall drawn before on the same faces. <paramref name="remember"/>: the wall keeps the line.
    /// </summary>
    public static Entity Apply(ICommandContext ctx, Entity wall, Line reference, bool remember)
    {
        if (remember && !reference.IsCoreCentre)
        {
            var kept = wall.CloneAttrs();
            kept[Key] = reference.ToJson();
            ctx.Replace(wall = wall.With(attrs: kept));
        }
        var y = LeftOf(Walls.Section(ctx.Model, wall), reference);
        if (Math.Abs(y) < 1e-9) return wall;
        if (PathCurve.Of(wall) is { IsArc: true } arc)
        {
            var axis = Offset(arc, -y);
            var arcAttrs = wall.CloneAttrs();
            arcAttrs["start"] = J(axis.Start);
            arcAttrs["end"] = J(axis.End);
            arcAttrs[PathCurve.Key] = Math.Round(axis.Rise, 9);
            var curved = wall.With(attrs: arcAttrs);
            ctx.Replace(curved);
            return curved;
        }
        var (a, b) = (P(wall.Attrs["start"]), P(wall.Attrs["end"]));
        var d = (b - a).Normalized();
        var shift = new Vec2(-d.Y, d.X) * -y;
        var (na, nb) = (a + shift, b + shift);
        var reach = 2.5 * Math.Abs(y) + 0.01;
        var storey = Spatial.StoreyOf(ctx.Model, wall.Id)?.Id;
        // Each end of the line drawn meets at most one wall's free end: the nearest crossing within reach. Both ends are worked
        // out on the line as set off, before either moves, so the second end's crossing is not measured on a line already
        // shortened by the first (a room closed on its faces came out with its last wall askew by up to 2 cm).
        var (sa, sb) = (na, nb);
        var candidates = ctx.Model.OfClass(IfcClass.Wall).Where(w => w.Id != wall.Id && Spatial.StoreyOf(ctx.Model, w.Id)?.Id == storey && w.Attrs[PathCurve.Key] == null).ToList();
        var taken = new HashSet<(string, string)>();
        foreach (var (mine, atStart) in new[] { (sa, true), (sb, false) })
        {
            (Entity Wall, string Key, Vec2 X, double D)? best = null;
            foreach (var other in candidates)
            {
                var (oa, ob) = (P(other.Attrs["start"]), P(other.Attrs["end"]));
                var od = ob - oa;
                if (od.Length < 1e-9 || Math.Abs(Vec2.Cross(d, od.Normalized())) < 0.05) continue;
                // The two axes' lines crossing: where the faces drawn meet, set off alike.
                var t = Vec2.Cross(oa - sa, od) / Vec2.Cross(sb - sa, od);
                var x = sa + (sb - sa) * t;
                foreach (var (theirs, key) in new[] { (oa, "start"), (ob, "end") })
                {
                    if (taken.Contains((other.Id, key)) || !FreeEnd(ctx.Model, other.Id, key)) continue;
                    if ((mine - x).Length < reach && (theirs - x).Length < reach && (mine - theirs).Length < 2 * reach)
                    {
                        var dist = (mine - theirs).Length;
                        if (best == null || dist < best.Value.D) best = (other, key, x, dist);
                    }
                }
            }
            if (best is not { } hit) continue;
            taken.Add((hit.Wall.Id, hit.Key));
            if (atStart) na = hit.X; else nb = hit.X;
            var current = ctx.Model.Get(hit.Wall.Id)!;
            var oattrs = current.CloneAttrs();
            oattrs[hit.Key] = J(hit.X);
            ctx.Replace(current.With(attrs: oattrs));
        }
        if ((nb - na).Length < 1e-3) throw new CommandException("The wall is too short once set off from its line.");
        var attrs = wall.CloneAttrs();
        attrs["start"] = J(na);
        attrs["end"] = J(nb);
        var next = wall.With(attrs: attrs);
        ctx.Replace(next);
        return next;
    }

    /// <summary>
    /// What the interface is told of a wall that keeps a reference line: the line itself (start, end, arc: its rise) and how far
    /// left of the axis it lies, so its snaps, grips and dimensions use it. Derived, never stored.
    /// </summary>
    public sealed class Props : INodeProps
    {
        public void Add(Entity e, IModelView m, JsonObject props)
        {
            if (e.Class != IfcClass.Wall || Stored(e) is not { } r) return;
            var left = SafeLeft(Walls.Section(m, e), r);
            var line = Offset(PathCurve.Of(e), left);
            var o = new JsonObject { ["start"] = new JsonArray(Math.Round(line.Start.X, 9), Math.Round(line.Start.Y, 9)), ["end"] = new JsonArray(Math.Round(line.End.X, 9), Math.Round(line.End.Y, 9)), ["left"] = Math.Round(left, 9) };
            if (line.IsArc) o["arc"] = Math.Round(line.Rise, 9);
            props["referenceAxis"] = o;
        }
    }
}
