using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A label's parts edited on screen (decision 292): a door's or window's
/// marker (each line of its texts, each end of its line) and an opening's void
/// label, each part moved, its text turned, a line's end drawn out or pulled
/// in. A part is kept by its key in a JSON object ({"o0": {"d": [2, -1.5],
/// "rot": 30}, "b": {"ext": 4}}): <c>d</c> its offset in paper mm along the
/// label's own directions (along and across its wall, so it follows the
/// element when the element moves or turns), <c>rot</c> its turn in degrees
/// from the way it reads by default (always drawn upright), <c>ext</c> how
/// many paper mm a line's end reaches further (negative: shorter). A part
/// without any of them takes its default again.
/// </summary>
public static class LabelParts
{
    /// <summary>One part's edits; null: as by default.</summary>
    public readonly record struct Part(Vec2? D, double? Rot, double? Ext)
    {
        public bool Edited => D != null || Rot != null || Ext != null;
    }

    /// <summary>The part named <paramref name="key"/> of <paramref name="parts"/>.</summary>
    public static Part Of(JsonObject? parts, string key)
    {
        if (parts?[key] is not JsonObject p) return default;
        Vec2? d = p["d"] is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;
        return new Part(d, Json.Number(p["rot"]), Json.Number(p["ext"]));
    }

    /// <summary>
    /// The parts after a command's change: <c>part</c> names one; <c>offset</c> ([along, across] paper mm; null: back
    /// in place), <c>rot</c> (degrees; null: as by default), <c>ext</c> (paper mm; null: as by default), <c>reset</c>
    /// (true: the part, or every part when none is named, as by default). Null when nothing is left edited.
    /// </summary>
    public static JsonObject? Apply(JsonObject? parts, JsonObject a)
    {
        var o = parts?.DeepClone() as JsonObject ?? [];
        var key = ((string?)a["part"])?.Trim();
        if (a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset)
        {
            if (string.IsNullOrEmpty(key)) return null;
            o.Remove(key);
            return o.Count > 0 ? o : null;
        }
        if (!a.ContainsKey("offset") && !a.ContainsKey("rot") && !a.ContainsKey("ext")) return o.Count > 0 ? o : null;
        if (string.IsNullOrEmpty(key)) throw new CommandException("Which part of the label?");
        var p = o[key]?.DeepClone() as JsonObject ?? [];
        if (a.ContainsKey("offset"))
        {
            if (a["offset"] is null) p.Remove("d");
            else if (a["offset"] is JsonArray d && d.Count >= 2 && Json.Number(d[0]) is { } x && Json.Number(d[1]) is { } y && double.IsFinite(x) && double.IsFinite(y))
                p["d"] = new JsonArray(Math.Round(x, 3), Math.Round(y, 3));
            else throw new CommandException("An offset is two numbers, paper mm along and across.");
        }
        foreach (var k in new[] { "rot", "ext" })
        {
            if (!a.ContainsKey(k)) continue;
            if (a[k] is null) { p.Remove(k); continue; }
            var n = Json.Number(a[k]) is { } v && double.IsFinite(v) ? v : throw new CommandException(k == "rot" ? "A turn is in degrees." : "A line's end reaches a number of paper mm.");
            if (k == "rot") n = Normal(n);
            if (Math.Abs(n) < 1e-9) p.Remove(k); else p[k] = Math.Round(n, 3);
        }
        if (p.Count > 0) o[key] = p; else o.Remove(key);
        return o.Count > 0 ? o : null;
    }

    /// <summary>An angle in degrees in (−180°, 180°].</summary>
    public static double Normal(double deg)
    {
        deg %= 360;
        if (deg > 180) deg -= 360;
        else if (deg <= -180) deg += 360;
        return deg;
    }

    /// <summary>A text's angle turned so it never reads upside down: folded into (−90°, 90°].</summary>
    public static double Upright(double deg)
    {
        deg = Normal(deg);
        if (deg > 90 + 1e-9) deg -= 180;
        else if (deg <= -90 + 1e-9) deg += 180;
        return deg;
    }

    /// <summary>A direction's angle in degrees.</summary>
    public static double AngleOf(Vec2 d) => Math.Atan2(d.Y, d.X) * 180 / Math.PI;

    /// <summary>The unit vector at <paramref name="deg"/> degrees.</summary>
    public static Vec2 Dir(double deg) => new(Math.Cos(deg * Math.PI / 180), Math.Sin(deg * Math.PI / 180));

    /// <summary>A text's rough width (the tags' measure: 0.62 of its height a character).</summary>
    public static double Width(string text, double h) => Math.Max(1, text.Length) * h * 0.62;

    static JsonArray J(Vec2 p) => new(Math.Round(p.X, 6), Math.Round(p.Y, 6));

    /// <summary>
    /// What the interface needs to give a part its grips: where it is (<c>at</c>), where it would be unmoved
    /// (<c>base</c>), the label's directions (<c>u</c> along, <c>v</c> across), paper mm in view units (<c>mm</c>);
    /// a text's angle drawn, the angle its turn counts from (<c>from</c>) and where its turning grip stands
    /// (<c>rotAt</c>), the box it covers ([cx, cy, half width, half height, angle]: a click on it picks the label); a line
    /// end's direction outwards (<c>dir</c>).
    /// </summary>
    public static JsonObject TextData(string key, Vec2 at, Vec2 @base, Vec2 u, Vec2 v, double mm, double angle, double from, Vec2 rotAt, Part p,
        (Vec2 C, double Hw, double Hh, double Deg) box) => new()
    {
        ["key"] = key, ["kind"] = "text", ["at"] = J(at), ["base"] = J(@base), ["u"] = J(u), ["v"] = J(v), ["mm"] = mm,
        ["angle"] = Math.Round(angle, 6), ["from"] = Math.Round(from, 6), ["rotAt"] = J(rotAt),
        ["box"] = new JsonArray(Math.Round(box.C.X, 6), Math.Round(box.C.Y, 6), Math.Round(box.Hw, 6), Math.Round(box.Hh, 6), Math.Round(box.Deg, 6)),
        ["moved"] = p.D != null, ["turned"] = p.Rot != null,
    };

    /// <inheritdoc cref="TextData"/>
    public static JsonObject EndData(string key, Vec2 at, Vec2 @base, Vec2 dir, double mm, Part p) => new()
    {
        ["key"] = key, ["kind"] = "end", ["at"] = J(at), ["base"] = J(@base), ["dir"] = J(dir), ["mm"] = mm, ["moved"] = p.Ext != null,
    };

    /// <summary>A point moved by a part's offset (paper mm along <paramref name="u"/> and <paramref name="v"/>).</summary>
    public static Vec2 Moved(Vec2 p, Part part, Vec2 u, Vec2 v, double mm) => part.D is { } d ? p + (u * d.X + v * d.Y) * mm : p;

    /// <summary>Whether the box of a text (its centre, half sizes, angle) is crossed by the segment a–b or holds it.</summary>
    public static bool Crosses((Vec2 C, double Hw, double Hh, double Deg) box, Vec2 a, Vec2 b)
    {
        // Into the box's own axes: a box round the origin.
        var (u, c) = (Dir(box.Deg), box.C);
        var vv = new Vec2(-u.Y, u.X);
        Vec2 L(Vec2 p) => new(Vec2.Dot(p - c, u), Vec2.Dot(p - c, vv));
        var (p0, p1) = (L(a), L(b));
        // Liang–Barsky against [−hw, hw] × [−hh, hh].
        double t0 = 0, t1 = 1;
        var d = p1 - p0;
        foreach (var (pp, q) in new[] { (-d.X, p0.X + box.Hw), (d.X, box.Hw - p0.X), (-d.Y, p0.Y + box.Hh), (d.Y, box.Hh - p0.Y) })
        {
            if (Math.Abs(pp) < 1e-15) { if (q < 0) return false; continue; }
            var t = q / pp;
            if (pp < 0) { if (t > t1) return false; if (t > t0) t0 = t; }
            else { if (t < t0) return false; if (t < t1) t1 = t; }
        }
        return t0 <= t1;
    }

    /// <summary>The four sides of a text's box.</summary>
    public static IEnumerable<(Vec2 A, Vec2 B)> Sides((Vec2 C, double Hw, double Hh, double Deg) box)
    {
        var u = Dir(box.Deg) * box.Hw;
        var v = new Vec2(-Dir(box.Deg).Y, Dir(box.Deg).X) * box.Hh;
        Vec2[] q = [box.C - u - v, box.C + u - v, box.C + u + v, box.C - u + v];
        for (var i = 0; i < 4; i++) yield return (q[i], q[(i + 1) % 4]);
    }

    /// <summary>The box a drawn text covers (its anchor read: start, middle or end along; top, middle or bottom across).</summary>
    public static (Vec2 C, double Hw, double Hh, double Deg) BoxOf(PenText t, double mm)
    {
        var h = t.Height * mm;
        var w = Width(t.Text, h);
        var u = Dir(t.Angle);
        var v = new Vec2(-u.Y, u.X);
        var c = t.At + u * (t.Anchor switch { "start" => w / 2, "end" => -w / 2, _ => 0 })
            + v * (t.Baseline switch { "bottom" or "alphabetic" => h / 2, "top" or "hanging" => -h / 2, _ => 0 });
        return (c, w / 2, h / 2, t.Angle);
    }
}
