using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// The line a wall is drawn on (testers' request, decision 237): the axis stays on the middle of the core (Lapin's layer
/// rule), but the line clicked may be the core's centre, the core's exterior or interior face, or the wall's exterior or
/// interior finish face; the exterior is the side of the first layer (the wall's right as drawn). The axis is set off from
/// the line by as much, and where the line goes on from a wall drawn before on the same faces, the two axes are made to meet
/// where those faces meet, so the corner joins as the faces were drawn.
/// </summary>
public static class WallReference
{
    public static readonly string[] Lines = ["core", "coreExterior", "coreInterior", "exterior", "interior"];

    static Vec2 P(JsonNode? n) => n is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9));

    /// <summary>Whether a wall's end ("start" or "end") is joined to nothing yet: a joined end belongs to its corner and is not moved.</summary>
    static bool FreeEnd(IModelView m, string id, string key) =>
        !Joins.Of(m, id).Any(r => (string?)r.Attrs?[r.Relating == id ? "relatingConnection" : "relatedConnection"] == (key == "start" ? "ATSTART" : "ATEND"));

    /// <summary>How far left of the axis the reference line lies (the axis then goes that far right of the line drawn).</summary>
    public static double LeftOf(IReadOnlyList<Walls.Band> bands, string reference)
    {
        if (bands.Count == 0 || reference == "core") return 0;
        var core = bands.Any(b => b.Core) ? bands.First(b => b.Core) : bands.MaxBy(b => b.To - b.From)!;
        // The exterior: the first layer's side (the right, unless the wall is flipped).
        var exteriorRight = bands[0].From <= bands[^1].From;
        var (lo, hi) = (bands.Min(b => b.From), bands.Max(b => b.To));
        return reference switch
        {
            "coreExterior" => exteriorRight ? core.From : core.To,
            "coreInterior" => exteriorRight ? core.To : core.From,
            "exterior" => exteriorRight ? lo : hi,
            "interior" => exteriorRight ? hi : lo,
            _ => throw new CommandException("A wall is drawn on its core's centre or faces, or on its exterior or interior face."),
        };
    }

    /// <summary>A wall just made, its axis set off from the line it was drawn on; its ends met with a wall drawn before on the same faces.</summary>
    public static Entity Apply(ICommandContext ctx, Entity wall, string reference)
    {
        var y = LeftOf(Walls.Section(ctx.Model, wall), reference);
        if (Math.Abs(y) < 1e-9) return wall;
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
}
