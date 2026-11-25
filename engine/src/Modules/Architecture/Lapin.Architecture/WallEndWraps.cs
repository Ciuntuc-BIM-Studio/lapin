using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A finish wrapped round a wall's free ends (in-app report #192, decision 856): a wall type's <c>wrapEnds</c> (its walls'
/// default) or a wall's own says which face's outer layer turns each free end: <see cref="First"/> (layer 1's, the R face of a
/// wall as drawn), <see cref="Last"/> (layer n's), <see cref="None"/>. At a free end that layer caps the end from its own face
/// to the other one, and every other layer stops its thickness short, so the end shows the finish instead of the core; the
/// wall's outline does not move (Revit's "Wrapping at ends", ArchiCAD's none). Lapin's own design intent, carried in
/// Lapin_Intent: the IFC layer set and usage are unchanged. Absent (every wall made before, every IFC import), nothing is
/// wrapped and each layer shows its own end, as an IfcMaterialLayerSetUsage lays it.
/// Not wrapped: an end a join takes; a curved wall; a wall whose layers end as its file says (<see cref="Walls.LayerEndsKey"/>);
/// an end where a layer has an extent of its own (<see cref="LayerExtents"/>: the user drew that end); an outer layer that is
/// the core, of its own height, or thicker than a quarter of the wall's length.
/// </summary>
public static class WallEndWraps
{
    public const string Key = "wrapEnds";
    public const string None = "none", First = "first", Last = "last";
    public static readonly string[] Values = [None, First, Last];

    /// <summary>A value given in a command: one of <see cref="Values"/>, or null to clear it (a wall then takes its type's).</summary>
    public static string? Parse(JsonNode? n)
    {
        if (n is null) return null;
        return n is JsonValue v && v.TryGetValue<string>(out var s) && Values.Contains(s) ? s
            : throw new CommandException("Wrap at free ends is none, first (layer 1's face) or last (the last layer's face).");
    }

    /// <summary>Sets (or, for null, clears) the attribute on a wall's or a type's attributes.</summary>
    public static void Set(JsonObject attrs, JsonNode? n)
    {
        if (Parse(n) is { } s) attrs[Key] = s;
        else attrs.Remove(Key);
    }

    /// <summary>The wall's own setting, else its type's, else <see cref="None"/>.</summary>
    public static string Of(IModelView m, Entity wall)
    {
        if (wall.Str(Key) is { } own && Values.Contains(own)) return own;
        if (Walls.TypeOf(m, wall.Id)?.Str(Key) is { } typed && Values.Contains(typed)) return typed;
        return None;
    }

    /// <summary>
    /// How a straight wall's free ends are wrapped: the band that wraps, its thickness, which ends, the free ends' lines
    /// (<see cref="EndX"/>: along the axis at a distance across it) and the frame's points. Null when nothing is wrapped.
    /// </summary>
    internal sealed record Wrapping(int Band, double T, bool AtStart, bool AtEnd, Func<int, double, double> EndX, Func<double, double, Vec2> P, double YMin, double YMax)
    {
        /// <summary>How far a band stops short of an end for the wrap (0 for the wrapping band, or an end not wrapped).</summary>
        public double Short(int end, Walls.Band b) => b.Index != Band && (end == 0 ? AtStart : AtEnd) ? T : 0;

        /// <summary>
        /// A band's outline (start From, end From, end To, start To) as corners, the wrapping band's turned round its wrapped
        /// ends: from its own face across to the other face, its thickness deep.
        /// </summary>
        public List<Vec2>[] Corners(Vec2[] whole, Walls.Band band)
        {
            var c = whole.Select(p => new List<Vec2> { p }).ToArray();
            if (band.Index != Band || whole.Length != 4) return c;
            var low = Math.Abs(band.From - YMin) < 1e-9;
            var (inner, opp) = low ? (band.To, YMax) : (band.From, YMin);
            if (AtEnd)
            {
                double E(double y) => EndX(1, y);
                if (low) c[2] = [P(E(opp), opp), P(E(opp) - T, opp), P(E(inner) - T, inner)];
                else c[1] = [P(E(inner) - T, inner), P(E(opp) - T, opp), P(E(opp), opp)];
            }
            if (AtStart)
            {
                double S(double y) => EndX(0, y);
                if (low) c[3] = [P(S(inner) + T, inner), P(S(opp) + T, opp), P(S(opp), opp)];
                else c[0] = [P(S(opp), opp), P(S(opp) + T, opp), P(S(inner) + T, inner)];
            }
            return c;
        }
    }

    /// <summary>
    /// The wrapping of a straight wall laid out from <paramref name="start"/> along its axis, <paramref name="length"/> long;
    /// <paramref name="joined"/>: its ends as its joins leave them (null where no join takes one, before its own angles),
    /// <paramref name="corners"/>: where its layers turn a corner.
    /// </summary>
    internal static Wrapping? Of(IModelView m, Entity wall, IReadOnlyList<Walls.Band> bands, (Vec2 Point, EndCut Cut)?[] joined,
        Func<Walls.Band, (double From, double To)>?[] corners, IReadOnlyList<LayerExtents.Extent> extents, double length,
        Func<int, double, double> endX, Func<double, double, Vec2> p)
    {
        var mode = Of(m, wall);
        if (mode == None || bands.Count < 2 || wall.Attrs[Walls.LayerEndsKey] != null || PathCurve.Of(wall).IsArc) return null;
        var ordered = bands.OrderBy(b => b.Index).ToList();
        if (mode == Last) ordered.Reverse();
        var w = ordered.FirstOrDefault(b => b.To - b.From > 1e-9);
        if (w.To - w.From <= 1e-9) return null;
        var (yMin, yMax) = (bands.Min(b => b.From), bands.Max(b => b.To));
        var t = w.To - w.From;
        if (w.Core || w.Z0 != null || w.Z1 != null || t * 4 > length || !(Math.Abs(w.From - yMin) < 1e-9 || Math.Abs(w.To - yMax) < 1e-9)) return null;
        bool Free(int end, string edge) => joined[end] == null && corners[end] == null && !extents.Any(x => x.Edge == edge);
        var (s, e) = (Free(0, LayerExtents.Start), Free(1, LayerExtents.End));
        return s || e ? new Wrapping(w.Index, t, s, e, endX, p, yMin, yMax) : null;
    }
}
