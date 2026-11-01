using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// A longitudinal bar cut in two with a lap splice (docs/plan-rebar.md, decision 227): a bar longer than the stock
/// length (12 m, or 6 m) is split where the engineer says. The lap zone is a design decision (a bottom bar laps near
/// the supports, a top bar away from them), so it is given as two stations along the bar's nominal path from its start:
/// <c>from</c>, where the second piece starts, and <c>to</c>, where the first piece ends; the lap is <c>to − from</c>.
/// A station with a lap is the shortcut for a lap that ends at the station (or starts there, <c>side: after</c>); two
/// clicks on the bar give the two stations. The original bar becomes the first piece (its start, its start hook and its
/// mark kept), a new bar the second (the end, the end hook, the next mark); neither has a hook at the cut. A straight bar
/// stays straight; an L, a U or a polyline gives two polylines. The second piece is drawn a little beside the first
/// (<c>lapOffset</c>) so the lap reads; in space it is where it is.
/// </summary>
public static class RebarSplit
{
    static readonly string[] Longitudinal = ["straight", "L", "U", "polyline"];

    public static bool IsLongitudinal(string shape) => Longitudinal.Contains(shape);

    /// <summary>The bar's path in its own frame, hooks left out: the vertices its shape gives, or a polyline's own.</summary>
    public static List<Vec2> PathOf(RebarSpec s)
    {
        if (!IsLongitudinal(s.Shape)) throw new CommandException("Only a longitudinal bar is split.");
        var def = RebarShapes.Def(s.Shape);
        var pts = new List<Vec2>(def.FreeVertices && s.Vertices is { Count: >= 2 } ? s.Vertices : def.Vertices(s.Params));
        if (pts.Count < 2) throw new CommandException("Only a longitudinal bar is split.");
        return pts;
    }

    public static double LengthOf(IReadOnlyList<Vec2> pts)
    {
        var l = 0.0;
        for (var i = 0; i + 1 < pts.Count; i++) l += (pts[i + 1] - pts[i]).Length;
        return l;
    }

    /// <summary>The station (m from the start) of the point of the path nearest to <paramref name="local"/> (the bar's frame).</summary>
    public static double StationOf(IReadOnlyList<Vec2> pts, Vec2 local)
    {
        var best = 0.0;
        var bestD = double.MaxValue;
        var run = 0.0;
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var (a, b) = (pts[i], pts[i + 1]);
            var ab = b - a;
            var l2 = Vec2.Dot(ab, ab);
            var t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(local - a, ab) / l2, 0, 1);
            var d = (a + ab * t - local).Length;
            if (d < bestD) { bestD = d; best = run + t * Math.Sqrt(l2); }
            run += Math.Sqrt(l2);
        }
        return best;
    }

    /// <summary>The point at a station along the path.</summary>
    public static Vec2 PointAt(IReadOnlyList<Vec2> pts, double station)
    {
        var run = 0.0;
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var l = (pts[i + 1] - pts[i]).Length;
            if (station <= run + l || i + 2 == pts.Count) return l < 1e-12 ? pts[i] : pts[i] + (pts[i + 1] - pts[i]) * ((station - run) / l);
            run += l;
        }
        return pts[^1];
    }

    /// <summary>The piece of the path between two stations: its ends interpolated, the vertices between them kept.</summary>
    public static List<Vec2> Between(IReadOnlyList<Vec2> pts, double s0, double s1)
    {
        var o = new List<Vec2> { PointAt(pts, s0) };
        var run = 0.0;
        for (var i = 1; i < pts.Count; i++)
        {
            run += (pts[i] - pts[i - 1]).Length;
            if (run > s0 + 1e-9 && run < s1 - 1e-9) o.Add(pts[i]);
        }
        o.Add(PointAt(pts, s1));
        return o;
    }

    /// <summary>The shortest lap accepted (m): anything less is a cut without a splice.</summary>
    public const double MinLap = 0.01;

    /// <summary>Why a lap from <paramref name="from"/> to <paramref name="to"/> cannot be made on a bar this long, or null: the lap needs a length, and neither piece may be all lap.</summary>
    public static string? Refusal(double length, double from, double to)
    {
        if (to - from < MinLap - 1e-9) return "The lap needs a length.";
        if (from < 1e-6 || to > length - 1e-6) return "The cut is too close to the end.";
        return null;
    }

    /// <summary>
    /// Where the lap lies, from the command's arguments: <c>from</c> and <c>to</c> (m along the path); or a station (<c>station</c>, or the
    /// nearest point of the bar to <c>point</c>) with <c>point2</c> giving the other station (sorted); or a station with <c>lap</c> (the
    /// settings' when none) ending there, or starting there with <c>side: "after"</c> (or with <c>point2</c> beyond it along the bar).
    /// </summary>
    public static (double From, double To) Stations(RebarSpec s, Vec2 at, IReadOnlyList<Vec2> pts, RebarSettings st, JsonObject a)
    {
        var length = LengthOf(pts);
        if (a["from"] != null || a["to"] != null)
        {
            const string both = "The lap goes from one station to another, both from the bar's start.";
            var from = Json.Number(a["from"]) ?? throw new CommandException(both);
            var to = Json.Number(a["to"]) ?? throw new CommandException(both);
            if (from < -1e-9 || to > length + 1e-9) throw new CommandException("The cut is outside the bar.");
            return (from, to);
        }
        double station;
        if (a["station"] != null) station = Json.Number(a["station"]) ?? throw new CommandException("The station is a length from the bar's start.");
        else if (a["point"] != null) station = StationOf(pts, RebarShapes.Local(s, at, Args.Point(a["point"], "where to cut")));
        else throw new CommandException("Say where the lap goes: from and to along the bar, a station and a lap, or a point on it.");
        if (station < -1e-9 || station > length + 1e-9) throw new CommandException("The cut is outside the bar.");
        var other = a["point2"] != null ? StationOf(pts, RebarShapes.Local(s, at, Args.Point(a["point2"], "the lap's other end"))) : (double?)null;
        var lap = Json.Number(a["lap"]);
        if (other != null && lap == null) return (Math.Min(station, other.Value), Math.Max(station, other.Value));
        var l = lap ?? st.LapOf(s.Diameter);
        var after = other != null ? other.Value > station : (string?)a["side"] == "after";
        return after ? (station, station + l) : (station - l, station);
    }

    /// <summary>The two pieces' specs (the bar's frame kept; the second's origin at its own start) for a lap from <paramref name="from"/> to <paramref name="to"/>, or a refusal.</summary>
    static (RebarSpec First, RebarSpec Second, Vec2 SecondOrigin)? Pieces(RebarSpec s, double from, double to, out string? refusal)
    {
        var pts = PathOf(s);
        var length = LengthOf(pts);
        refusal = Refusal(length, from, to);
        if (refusal != null) return null;
        var (endFirst, startSecond) = (to, from);
        if (s.Shape == "straight")
        {
            var first = s with { Params = new Dictionary<string, double>(s.Params) { ["length"] = Math.Round(endFirst, 6) }, End = new Hook(0, 0) };
            var second = s with { Params = new Dictionary<string, double>(s.Params) { ["length"] = Math.Round(length - startSecond, 6) }, Start = new Hook(0, 0) };
            return (first, second, new Vec2(startSecond, 0));
        }
        var radius = s.Shape == "polyline" ? s["bendRadius"] : RebarShapes.BendRadius(s.Diameter);
        var p = new Dictionary<string, double> { ["bendRadius"] = Math.Round(radius, 6) };
        var a = Between(pts, 0, endFirst);
        var b = Between(pts, startSecond, length);
        var origin = b[0];
        var firstSpec = s with { Shape = "polyline", Params = p, Vertices = a.Select(v => new Vec2(Math.Round(v.X, 6), Math.Round(v.Y, 6))).ToList(), End = new Hook(0, 0) };
        var secondSpec = s with { Shape = "polyline", Params = p, Vertices = b.Select(v => new Vec2(Math.Round(v.X - origin.X, 6), Math.Round(v.Y - origin.Y, 6))).ToList(), Start = new Hook(0, 0) };
        return (firstSpec, secondSpec, origin);
    }

    /// <summary>What a cut would give (the command's arguments): the pieces' schedule lengths (hooks included), the lap and its stations, or why it cannot be made.</summary>
    public static JsonObject Preview(RebarSettings st, Entity e, JsonObject a)
    {
        var s = Rebars.SpecOf(e);
        var o = new JsonObject();
        if (!IsLongitudinal(s.Shape)) { o["error"] = "Only a longitudinal bar is split."; return o; }
        var pts = PathOf(s);
        o["length"] = Math.Round(LengthOf(pts), 6);
        double from, to;
        try { (from, to) = Stations(s, Rebars.At(e), pts, st, a); }
        catch (CommandException ex) { o["error"] = ex.Message; return o; }
        o["from"] = Math.Round(from, 6);
        o["to"] = Math.Round(to, 6);
        o["lap"] = Math.Round(to - from, 6);
        var pieces = Pieces(s, from, to, out var refusal);
        if (pieces == null) { o["error"] = refusal; return o; }
        o["first"] = Math.Round(RebarShapes.Length(pieces.Value.First), 6);
        o["second"] = Math.Round(RebarShapes.Length(pieces.Value.Second), 6);
        return o;
    }

    /// <summary>The command: the bar replaced by its first piece, the second added with the next mark → {first, second, lap, from, to}.</summary>
    public static JsonObject Split(ICommandContext ctx, Entity e, JsonObject a)
    {
        var st = RebarSettings.Of(ctx.Model);
        var s = Rebars.SpecOf(e);
        if (!IsLongitudinal(s.Shape)) throw new CommandException("Only a longitudinal bar is split.");
        var at = Rebars.At(e);
        var pts = PathOf(s);
        var (from, to) = Stations(s, at, pts, st, a);
        var pieces = Pieces(s, from, to, out var refusal) ?? throw new CommandException(refusal!);
        var (first, second, origin) = pieces;

        // The first piece: the bar itself, shortened; its start hook stays, none at the cut.
        var attrs = e.CloneAttrs();
        WriteShape(attrs, first);
        var hooks = e.Attrs["hooks"] as JsonObject;
        attrs.Remove("hooks");
        if (hooks?["start"] is JsonObject h0) attrs["hooks"] = new JsonObject { ["start"] = h0.DeepClone() };
        var firstName = first.Shape == s.Shape ? e.Name : $"{RebarShapes.Def(first.Shape).Label} {Rebars.Mark(e)}";
        ctx.Replace(e.With(name: firstName, attrs: attrs));

        // The second piece: a new bar from the lap's start to the end, the end hook on it, the next mark, drawn beside its lap; its label placed afresh.
        var second2 = e.CloneAttrs();
        WriteShape(second2, second);
        second2["at"] = Rebars.J(RebarShapes.Place(s, at, origin));
        second2.Remove("hooks");
        if (hooks?["end"] is JsonObject h1) second2["hooks"] = new JsonObject { ["end"] = h1.DeepClone() };
        var mark = Rebars.NextMark(ctx.Model);
        second2["mark"] = mark;
        second2["lapOffset"] = Math.Round(s.Diameter, 6);
        if (second2["label"] is JsonObject label)
        {
            foreach (var k in new[] { "at", "elbow", "offset" }) label.Remove(k);
            if (label.Count == 0) second2.Remove("label");
        }
        var piece = new Entity(ctx.NewId(), Rebars.Class, $"{RebarShapes.Def(second.Shape).Label} {mark}", second2);
        ctx.Add(piece);
        return new JsonObject { ["first"] = e.Id, ["second"] = piece.Id, ["lap"] = Math.Round(to - from, 6), ["from"] = Math.Round(from, 6), ["to"] = Math.Round(to, 6) };
    }

    /// <summary>A piece's shape written on a bar's attributes: a straight bar's length, or a polyline's vertices and bend radius.</summary>
    static void WriteShape(JsonObject attrs, RebarSpec piece)
    {
        attrs["shape"] = piece.Shape;
        attrs["params"] = new JsonObject(piece.Params.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)kv.Value)));
        if (piece.Vertices != null) attrs["vertices"] = new JsonArray(piece.Vertices.Select(v => (JsonNode)Rebars.J(v)).ToArray());
        else attrs.Remove("vertices");
    }
}
