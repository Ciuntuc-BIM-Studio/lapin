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
        foreach (var candidate in ctx.Model.OfClass(IfcClass.Wall).Where(w => w.Id != wall.Id && Spatial.StoreyOf(ctx.Model, w.Id)?.Id == storey && w.Attrs[PathCurve.Key] == null).ToList())
        {
            var other = candidate;
            var (oa, ob) = (P(other.Attrs["start"]), P(other.Attrs["end"]));
            var od = ob - oa;
            if (od.Length < 1e-9 || Math.Abs(Vec2.Cross(d, od.Normalized())) < 0.05) continue;
            // The two axes' lines crossing: where the faces drawn meet, set off alike.
            var den = Vec2.Cross(b - a, od);
            var t = Vec2.Cross(oa - na, od) / den;
            var x = na + (nb - na) * t;
            foreach (var (mine, setMine) in new (Vec2, Action<Vec2>)[] { (na, p => na = p), (nb, p => nb = p) })
                foreach (var (theirs, key) in new[] { (oa, "start"), (ob, "end") })
                    if ((mine - x).Length < reach && (theirs - x).Length < reach && (mine - theirs).Length < 2 * reach)
                    {
                        setMine(x);
                        var oattrs = other.CloneAttrs();
                        oattrs[key] = J(x);
                        ctx.Replace(other.With(attrs: oattrs));
                        other = ctx.Model.Get(other.Id)!;
                    }
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
