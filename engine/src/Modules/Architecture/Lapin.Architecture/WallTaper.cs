using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall tapered from its base to its top (docs/plan-wall-lean-taper.md, stage L2; in-app report #191): a straight wall's
/// <see cref="Key"/> (its thickness at its top, metres; its type's when it has none of its own) makes it thinner (or thicker)
/// upwards, as a retaining or foundation wall is. One face stays plumb (<see cref="FaceKey"/>: the first layer's face,
/// <c>first</c>, the default, which is the wall's right face as drawn and so usually its exterior; or the last layer's,
/// <c>last</c>), the other leans. The layer that tapers (<see cref="LayerKey"/>: its index from the first, 0; the core when
/// none is given) takes the whole change; every other layer keeps its thickness measured normal to its face: those between
/// the plumb face and the tapering layer stay plumb, those beyond it lean with the other face (their horizontal width
/// thickness / cosφ, φ the leaning face's angle). The base keeps the wall's thickness as its layers lay it; the top has
/// <see cref="Key"/>. A zoned core tapers with its zones. Built as a leaning wall is (<see cref="WallLean.At"/>: the layers as
/// they are at the base and at the top, the joins worked out at both, the pieces lofted between), so walls meet it where
/// their faces' planes cross. Lapin's design intent, written in Lapin_Intent. Absent, or equal to the wall's thickness,
/// nothing changes: the wall is the prism it was.
/// Stage 2 does not taper (and says so, the wall keeping its thickness) what stage 1 does not lean (<see cref="WallLean.Refusal"/>),
/// nor a layer left with no thickness; a tapered wall does not also lean (its lean is ignored, with a problem).
/// </summary>
public static class WallTaper
{
    public const string Key = "thicknessTop", FaceKey = "plumbFace", LayerKey = "taperLayer";
    public const string First = "first", Last = "last";
    public static readonly string[] Faces = [First, Last];
    /// <summary>The least thickness the tapering layer keeps at its base and its top, metres.</summary>
    public const double MinLayer = 0.01;

    /// <summary>
    /// A wall's taper as it is built: each band's edges across the axis at its base and their change per metre up, in the
    /// order of <see cref="Walls.SectionPlumb"/>; the tapering band; the leaning face's side (max y: true) and its cosine.
    /// </summary>
    public sealed record Taper(IReadOnlyList<(double From, double KFrom, double To, double KTo)> Edges, int Layer, bool LeansAtMax, double Cos, double Bottom, double Top, double Height)
    {
        /// <summary>The bands as they are at <paramref name="h"/> over the base.</summary>
        public IReadOnlyList<Walls.Band> At(IReadOnlyList<Walls.Band> bands, double h) =>
            [.. bands.Select((b, i) => i < Edges.Count ? b with { From = Edges[i].From + Edges[i].KFrom * h, To = Edges[i].To + Edges[i].KTo * h } : b)];

        /// <summary>The wall's faces across the axis over its whole height (the wider of its base and its top).</summary>
        public (double Min, double Max) Envelope =>
            (Edges.Min(e => Math.Min(e.From, e.From + e.KFrom * Height)), Edges.Max(e => Math.Max(e.To, e.To + e.KTo * Height)));

        /// <summary>The middle of its thickness across the axis at <paramref name="h"/> over the base.</summary>
        public double Centre(double h) => (Edges.Min(e => e.From + e.KFrom * h) + Edges.Max(e => e.To + e.KTo * h)) / 2;

        /// <summary>A band's thickness as its quantities give it: the tapering layer's mean, every other layer's normal one.</summary>
        public double Width(int i) => i == Layer
            ? (Edges[i].To - Edges[i].From) + (Edges[i].KTo - Edges[i].KFrom) * Height / 2
            : (Edges[i].To - Edges[i].From) * (Edges[i].KTo != 0 ? Cos : 1);

        /// <summary>The wall's mean thickness (base and top halved).</summary>
        public double MeanThickness => (Bottom + Top) / 2;
    }

    /// <summary>The thickness at the top set: the wall's own, else its type's; null when neither has one.</summary>
    public static double? TopOf(IModelView m, Entity wall) =>
        wall.NumOrNull(Key) ?? Walls.TypeOf(m, wall.Id)?.NumOrNull(Key);

    /// <summary>Which face stays plumb: the wall's own, else its type's, else the first layer's.</summary>
    public static string FaceOf(IModelView m, Entity wall) =>
        wall.Str(FaceKey) is { } own && Faces.Contains(own) ? own : Walls.TypeOf(m, wall.Id)?.Str(FaceKey) is { } t && Faces.Contains(t) ? t : First;

    /// <summary>The layer that tapers as set (the wall's own, else its type's); null: the core.</summary>
    public static int? LayerOf(IModelView m, Entity wall) =>
        wall.NumOrNull(LayerKey) is { } own ? (int)Math.Round(own) : Walls.TypeOf(m, wall.Id)?.NumOrNull(LayerKey) is { } t ? (int)Math.Round(t) : null;

    /// <summary>Whether the wall is asked to taper: a thickness at its top other than its base's.</summary>
    public static bool Wanted(IModelView m, Entity wall)
    {
        if (wall.Class != IfcClass.Wall || TopOf(m, wall) is not { } top) return false;
        var bands = Walls.SectionPlumb(m, wall);
        return Math.Abs(top - (bands.Max(b => b.To) - bands.Min(b => b.From))) > 1e-9;
    }

    /// <summary>The taper the wall is built with; null for a wall of one thickness, or one stage 2 cannot taper (<see cref="Problem"/>).</summary>
    public static Taper? Of(IModelView m, Entity wall)
    {
        if (!Wanted(m, wall)) return null;
        if (WallLean.Cached(wall.Id, out var known)) return known;
        var o = WallLean.Unleveled(() => Refusal(m, wall, null) == null ? Build(m, wall, out _) : null);
        WallLean.Remember(wall.Id, o);
        return o;
    }

    /// <summary>The problem a wall asked to taper that stage 2 cannot build shows; null for every other wall.</summary>
    public static string? Problem(IModelView m, Entity wall, IServices? services)
    {
        if (!Wanted(m, wall)) return null;
        if (Refusal(m, wall, services) is { } refused) return refused;
        Build(m, wall, out var why);
        return why;
    }

    /// <summary>Why stage 2 cannot taper this wall (what stage 1 cannot lean); null when it can.</summary>
    public static string? Refusal(IModelView m, Entity wall, IServices? services) => WallLean.Refusal(m, wall, services, taper: true);

    static Taper? Build(IModelView m, Entity wall, out string? problem)
    {
        problem = null;
        var bands = Walls.SectionPlumb(m, wall);
        var height = Walls.HeightOf(m, wall);
        if (bands.Count == 0 || !(height > 1e-9) || TopOf(m, wall) is not { } top) return null;
        var (yMin, yMax) = (bands.Min(b => b.From), bands.Max(b => b.To));
        var bottom = yMax - yMin;
        // The first layer on the right face (the layers as drawn) or on the left (flipped); the plumb face the chosen one's.
        var firstAtMin = bands[0].From <= bands[^1].From + 1e-12;
        var plumbAtMin = (FaceOf(m, wall) == First) == firstAtMin;
        var layer = LayerOf(m, wall) is { } asked ? bands.ToList().FindIndex(b => b.Index == asked) : -1;
        if (layer < 0)
        {
            if (LayerOf(m, wall) is { } missing) problem = $"The wall has no layer {missing + 1} to taper: its core tapers.";
            layer = Math.Max(0, bands.ToList().FindIndex(b => b.Core));
        }
        var change = top - bottom;
        var cos = height / Math.Sqrt(height * height + change * change);
        // Across from the plumb face (u): the bands in that order, each its edges at the base and their change per metre up.
        double U(double y) => plumbAtMin ? y - yMin : yMax - y;
        double Y(double u) => plumbAtMin ? yMin + u : yMax - u;
        var order = Enumerable.Range(0, bands.Count).OrderBy(i => U(plumbAtMin ? bands[i].From : bands[i].To)).ToList();
        var at = order.IndexOf(layer);
        var u = new (double Near, double KNear, double Far, double KFar)[bands.Count];
        // Between the plumb face and the tapering layer: as laid, plumb.
        for (var k = 0; k <= at; k++)
        {
            var b = bands[order[k]];
            var (n, f) = (U(plumbAtMin ? b.From : b.To), U(plumbAtMin ? b.To : b.From));
            u[order[k]] = (n, 0, f, 0);
        }
        // Beyond it: from the leaning face in, each its normal thickness across that face (wider horizontally by 1/cosφ).
        var face = bottom;
        var slope = change / height;
        for (var k = bands.Count - 1; k > at; k--)
        {
            var b = bands[order[k]];
            var w = (b.To - b.From) / cos;
            u[order[k]] = (face - w, slope, face, slope);
            face -= w;
        }
        // The tapering layer: from its plumb-side edge to the next one's (or the leaning face).
        u[layer] = (u[layer].Near, 0, face, slope);
        var (base0, top0) = (u[layer].Far - u[layer].Near, u[layer].Far + slope * height - u[layer].Near);
        if (base0 < MinLayer - 1e-9 || top0 < MinLayer - 1e-9)
        {
            problem = $"A thickness at the top of {top:0.###} m leaves its tapering layer ({bands[layer].Name}) less than {MinLayer * 1000:0} mm: the wall keeps its thickness.";
            return null;
        }
        var edges = new (double, double, double, double)[bands.Count];
        for (var i = 0; i < bands.Count; i++)
        {
            var (n, kn, f, kf) = u[i];
            // Back across the axis: u grows to the left from the right face, to the right from the left one.
            edges[i] = plumbAtMin ? (Y(n), kn, Y(f), kf) : (Y(f), -kf, Y(n), -kn);
        }
        return new Taper(edges, layer, plumbAtMin, cos, bottom, top, height);
    }

    /// <summary>
    /// A door's or window's body and plan in a tapered wall: the plumb wall's (placed on the middle of the wall's widest
    /// thickness, <see cref="Openings.FrameOf"/>) moved across to the middle of its thickness at the opening's mid-height.
    /// </summary>
    internal static ElementGeometry? Follow(IModelView m, Entity wall, Openings.Placed p, ElementGeometry g)
    {
        if (Of(m, wall) is not { } taper) return g;
        var dir = (Walls.End(wall) - Walls.Start(wall)).Normalized();
        var (lo, hi) = taper.Envelope;
        var d = dir.Perp * (taper.Centre(p.FillSill + p.Height / 2) - (lo + hi) / 2);
        if (d.Length < 1e-12) return g;
        return g with
        {
            Parts = [.. g.Parts.Select(x => x with { Solid = SolidBuilder.Transform(x.Solid, q => new Vec3(q.X + d.X, q.Y + d.Y, q.Z)) })],
            Plan = g.Plan?.Select(x => x with { Points = [.. x.Points.Select(q => q + d)] }).ToList(),
            Snaps = g.Snaps?.Select(q => q + d).ToList(),
        };
    }

    /// <summary>A tapered wall's body as drawn, its joins aside (<see cref="Walls.Envelope"/>): its section lofted from the base to the top.</summary>
    internal static Solid? Envelope(Taper taper, Vec2 start, Vec2 end, double z)
    {
        var left = (end - start).Normalized().Perp;
        IReadOnlyList<Vec2> At(double h)
        {
            var (y0, y1) = (taper.Edges.Min(e => e.From + e.KFrom * h), taper.Edges.Max(e => e.To + e.KTo * h));
            return [start + left * y0, end + left * y0, end + left * y1, start + left * y1];
        }
        return WallLean.Loft(At(0), z, At(taper.Height), z + taper.Height, "wall");
    }

    /// <summary>Sets (or, for null, clears) a type's or a wall's taper settings from a command's arguments (those given only).</summary>
    /// <remarks>The arguments by their names (<c>thicknessTop</c>, <c>plumbFace</c>, <c>taperLayer</c>), as the command catalogue reads them.</remarks>
    public static void Set(JsonObject attrs, JsonObject a)
    {
        if (a.ContainsKey("thicknessTop"))
        {
            if (a["thicknessTop"] is null) attrs.Remove(Key);
            else
            {
                var v = Json.Number(a["thicknessTop"]) ?? throw new CommandException("Thickness at top is a length in metres.");
                if (!double.IsFinite(v) || v < MinLayer || v > 10) throw new CommandException($"Thickness at top is between {MinLayer} m and 10 m.");
                attrs[Key] = v;
            }
        }
        if (a.ContainsKey("plumbFace"))
        {
            if (a["plumbFace"] is null) attrs.Remove(FaceKey);
            else if (a["plumbFace"] is JsonValue fv && fv.TryGetValue<string>(out var s) && Faces.Contains(s)) attrs[FaceKey] = s;
            else throw new CommandException($"Face that stays plumb is one of {string.Join(", ", Faces)}.");
        }
        if (a.ContainsKey("taperLayer"))
        {
            if (a["taperLayer"] is null) attrs.Remove(LayerKey);
            else if (Json.Number(a["taperLayer"]) is { } n && n >= 0 && Math.Abs(n - Math.Round(n)) < 1e-9 && n < 100) attrs[LayerKey] = (int)Math.Round(n);
            else throw new CommandException("Layer that tapers is a layer's index from the first (0).");
        }
    }

    /// <summary>Whether a command's arguments set any of the taper settings.</summary>
    public static bool Given(JsonObject a) => a.ContainsKey("thicknessTop") || a.ContainsKey("plumbFace") || a.ContainsKey("taperLayer");
}
