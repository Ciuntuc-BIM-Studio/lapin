using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// What a group's label says and where it stands (decisions 221, 222): the text's parts in order, the mark's balloon, the text's
/// direction, the side of the bars it stands on and how far off them (paper mm), the leader (none, one, or one to each bar seen
/// as a dot) and, for a distribution, the dimension string it is marked with. A style is kept as a partial object wherever it
/// is set (the project's settings by kind, the type, the element) and resolved most-specific-wins.
/// </summary>
public sealed record LabelStyle(IReadOnlyList<string> Parts, bool Balloon, string Direction, string Side, double Offset, string Leader, string Line)
{
    public static readonly string[] Kinds = ["bars", "stirrups", "columnBars", "runs"];
    public static readonly string[] Tokens = ["count", "diameter", "step", "length", "mark", "steel"];
    public static readonly string[] Directions = ["horizontal", "along"];
    /// <summary>In the view's coordinates; <c>auto</c>: to the right of dots, above lines, under a distribution's string.</summary>
    public static readonly string[] Sides = ["auto", "above", "below", "left", "right"];
    /// <summary><c>each</c>: a line to every bar seen as a dot (one for one); to a group of lines, one as <c>one</c>.</summary>
    public static readonly string[] Leaders = ["none", "one", "each"];
    /// <summary><c>string</c>: a distribution marked as a dimension string without values; <c>none</c>: the text and balloon beside its bars.</summary>
    public static readonly string[] Lines = ["none", "string"];
    public const string Key = "label";

    /// <summary>The kind the style was made for: what an old <c>leader: true</c> means when it is applied.</summary>
    public string Kind { get; init; } = "bars";

    public static LabelStyle Default(string kind) => kind switch
    {
        "stirrups" or "runs" => new LabelStyle(["count", "diameter", "step"], true, "along", "auto", 4, "one", "string") { Kind = kind },
        "columnBars" => new LabelStyle(["count", "diameter"], true, "horizontal", "auto", 4, "each", "none") { Kind = kind },
        _ => new LabelStyle(["count", "diameter", "length"], true, "horizontal", "auto", 4, "each", "none") { Kind = kind },
    };

    /// <summary>The style with a partial one's fields over it (an old boolean leader: yes is the kind's default, no is none).</summary>
    public LabelStyle Apply(JsonObject? o)
    {
        if (o == null) return this;
        var parts = o["parts"] is JsonArray pa ? pa.Select(x => (string?)x).OfType<string>().ToList() : null;
        var leader = o["leader"] is JsonValue lv && lv.TryGetValue<bool>(out var lb) ? (lb ? Default(Kind).Leader : "none")
            : (string?)o["leader"] is { } ls && Leaders.Contains(ls) ? ls : Leader;
        return new LabelStyle(parts is { Count: > 0 } ? parts : Parts,
            o["balloon"] is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : Balloon,
            (string?)o["direction"] ?? Direction, (string?)o["side"] ?? Side, Json.Number(o["offset"]) ?? Offset,
            leader, (string?)o["line"] is { } ln && Lines.Contains(ln) ? ln : Line) { Kind = Kind };
    }

    /// <summary>The style of a group of <paramref name="kind"/>: the defaults, the project's, the type's, then the element's.</summary>
    public static LabelStyle Resolve(string kind, RebarSettings st, JsonObject? type = null, JsonObject? element = null) =>
        Default(kind).Apply(st.Labels.GetValueOrDefault(kind)).Apply(type).Apply(element);

    public JsonObject ToJson() => new()
    {
        ["parts"] = new JsonArray(Parts.Select(p => (JsonNode)p).ToArray()), ["balloon"] = Balloon, ["direction"] = Direction, ["side"] = Side, ["offset"] = Offset, ["leader"] = Leader, ["line"] = Line,
    };

    /// <summary>The direction's angle (degrees, the view's x axis at 0) when it is one; null for horizontal and along.</summary>
    public static double? AngleOf(string direction) =>
        direction.StartsWith("angle:", StringComparison.Ordinal) && double.TryParse(direction.AsSpan(6), NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : null;

    /// <summary>
    /// A partial style with a command's patch merged in, checked (a field set to null goes back to inheriting); null when nothing is left.
    /// The parts come as a list or as one string of tokens ("count diameter length"); a boolean leader (the old way) is none when no, inherited when yes.
    /// </summary>
    public static JsonObject? Patch(JsonObject? now, JsonNode? patch)
    {
        if (patch == null) return null;
        if (patch is not JsonObject po) throw new CommandException("A label style is an object: parts, balloon, direction, side, offset, leader, line.");
        var o = now?.DeepClone() as JsonObject ?? new JsonObject();
        foreach (var (k, v) in po)
        {
            if (v == null) { o.Remove(k); continue; }
            switch (k)
            {
                case "parts":
                {
                    var tokens = v is JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToList() : ((string?)v ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                    foreach (var t in tokens)
                        if (!Tokens.Contains(t) && !t.StartsWith("text:", StringComparison.Ordinal))
                            throw new CommandException($"„{t}” is not a part of a label ({string.Join(", ", Tokens)}, text:…).");
                    if (tokens.Count == 0) o.Remove(k); else o[k] = new JsonArray(tokens.Select(t => (JsonNode)t).ToArray());
                    break;
                }
                case "balloon":
                    o[k] = v is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException("balloon is yes or no.");
                    break;
                case "leader":
                {
                    if (v is JsonValue lv && lv.TryGetValue<bool>(out var lb)) { if (lb) o.Remove(k); else o[k] = "none"; break; }
                    var l = (string?)v ?? "";
                    if (!Leaders.Contains(l)) throw new CommandException("The leader is none, one, or one to each bar (each).");
                    o[k] = l;
                    break;
                }
                case "line":
                {
                    var l = (string?)v ?? "";
                    if (!Lines.Contains(l)) throw new CommandException("A distribution is marked with a dimension string (string) or without a line (none).");
                    o[k] = l;
                    break;
                }
                case "direction":
                {
                    var d = (string?)v ?? "";
                    if (!Directions.Contains(d) && AngleOf(d) == null) throw new CommandException("The text runs horizontal, along the bars, or at an angle (angle:30).");
                    o[k] = d;
                    break;
                }
                case "side":
                {
                    var s = (string?)v ?? "";
                    if (!Sides.Contains(s)) throw new CommandException("The label stands above, below, left or right of the bars, or where it fits (auto).");
                    o[k] = s;
                    break;
                }
                case "offset":
                {
                    var off = Json.Number(v) ?? throw new CommandException("The offset is in mm on paper.");
                    if (off < 0 || off > 100) throw new CommandException("The offset is between 0 and 100 mm.");
                    o[k] = off;
                    break;
                }
                default: throw new CommandException($"„{k}” is not a field of a label style.");
            }
        }
        return o.Count > 0 ? o : null;
    }

    /// <summary>Never a balloon without a mark: an empty circle says nothing.</summary>
    public bool ShowsBalloon(int mark) => Balloon && mark > 0;

    /// <summary>What a group's label says of it, part by part: "41Ø8/150", "3Ø16 L=6000".</summary>
    public string Text(LabelFacts f)
    {
        var sb = new System.Text.StringBuilder();
        string? last = null;
        foreach (var part in Parts)
        {
            string? s = part switch
            {
                "count" => f.Count,
                "diameter" => RebarShapes.Dia(f.Diameter),
                "step" => f.Step != null ? "/" + f.Step : null,
                "length" => f.Length is { } len ? "L=" + RebarShapes.Mm(len) : null,
                "mark" => f.Mark > 0 ? f.Mark.ToString(CultureInfo.InvariantCulture) : null,
                "steel" => f.Steel,
                _ => part.StartsWith("text:", StringComparison.Ordinal) ? part[5..] : null,
            };
            if (string.IsNullOrEmpty(s)) continue;
            // The diameter sits on the count (3Ø16) and the step on the diameter (Ø8/150); the rest a word apart.
            var glued = sb.Length == 0 || part == "step" || (part == "diameter" && last == "count");
            if (!glued) sb.Append(' ');
            sb.Append(s);
            last = part;
        }
        return sb.ToString();
    }
}

/// <summary>What a label can say of a group: how many ("41", "2×41"), the diameter, the step text, the length of one, the mark, the steel.</summary>
public sealed record LabelFacts(string Count, double Diameter, string? Step, double? Length, int Mark, string Steel);

/// <summary>
/// Where the user put a label, each part null when left to the engine: its point (the balloon's, or the text's without one),
/// the leader's elbow, and a distribution's string offset (paper mm, signed, from the bars' edge on the string's side). Kept as
/// <c>{at, elbow, offset}</c> on a bar's or a distribution's <c>label</c>, and on the view for generated labels; an old <c>[x, y]</c> is the point alone.
/// </summary>
public sealed record LabelPlace(Vec2? At, Vec2? Elbow, double? Offset)
{
    public static readonly LabelPlace None = new(null, null, null);
    public bool IsEmpty => At == null && Elbow == null && Offset == null;

    public static LabelPlace? From(JsonNode? n) => n switch
    {
        JsonArray a => new LabelPlace(Rebars.P(a), null, null),
        JsonObject o => new LabelPlace(o["at"] is JsonArray at ? Rebars.P(at) : null, o["elbow"] is JsonArray el ? Rebars.P(el) : null, Json.Number(o["offset"])),
        _ => null,
    };

    public JsonObject ToJson()
    {
        var o = new JsonObject();
        if (At is { } at) o["at"] = Rebars.J(at);
        if (Elbow is { } elbow) o["elbow"] = Rebars.J(elbow);
        if (Offset is { } off) o["offset"] = off;
        return o;
    }

    /// <summary>A command's fields written over what is kept (an old array first made an object): a point or a number sets, null takes away.</summary>
    public static void Write(JsonObject into, JsonObject a)
    {
        foreach (var k in new[] { "at", "elbow" })
            if (a.ContainsKey(k)) { if (a[k] is JsonArray) into[k] = Rebars.J(Args.Point(a[k], k == "at" ? "the label's point" : "the leader's elbow")); else into.Remove(k); }
        if (a.ContainsKey("offset"))
        {
            if (Json.Number(a["offset"]) is { } off)
            {
                if (off < -100 || off > 100) throw new CommandException("The string stands within 100 mm of the bars.");
                into["offset"] = Math.Round(off, 2);
            }
            else into.Remove("offset");
        }
    }
}

/// <summary>A distribution's string: its path as seen, where its bars cross it (distances along), and how far across the bars reach (to the left of the path, as <see cref="PathCurve"/> has y).</summary>
public sealed record LabelString(PathCurve Path, IReadOnlyList<double> Stations, double Lo, double Hi);

/// <summary>
/// A group's label asked for while its bars are drawn, and drawn once the view's groups are all known, so the labels left
/// where the engine puts them stack in the marks' order and never share a point. Its place is the user's when they moved it
/// (<see cref="Placed"/>): a bar's or a distribution's on the entity, a generated group's on the view (<c>rebarLabels</c>).
/// </summary>
public sealed record LabelRequest(ViewMark Mark, string Owner, string Key, LabelStyle Style, string Text, int MarkNo, string Color,
    IReadOnlyList<(Vec2 A, Vec2 B)> Segments, IReadOnlyList<Vec2> Dots, Vec2? Along, LabelPlace? Placed)
{
    /// <summary>A bar's or a distribution's own label is told to the interface as <c>data.label</c> (its grip drives the entity); a generated one in <c>data.labels</c>.</summary>
    public bool Own { get; init; }
    /// <summary>The side taken for <c>auto</c> when the group knows better than the rule.</summary>
    public string? AutoSide { get; init; }
    /// <summary>A distribution's string, drawn when the style asks for it (<see cref="LabelStyle.Line"/>).</summary>
    public LabelString? String { get; init; }
}

public static class RebarLabels
{
    public const string ViewKey = "rebarLabels";
    /// <summary>The leader's pen, and the string's.</summary>
    public const double LeaderWidth = 0.25;
    /// <summary>A string's tick: 2 mm on paper across the line, as a dimension's.</summary>
    public const double TickMm = 2;

    /// <summary>Where the user put a generated group's label on this view, if they did.</summary>
    public static LabelPlace? PlacedOn(Entity view, string owner, string key) =>
        view.Attrs[ViewKey] is JsonObject o ? LabelPlace.From(o[$"{owner}:{key}"]) : null;

    /// <summary>The facts a bar or a distribution drawn by hand gives its label.</summary>
    public static LabelFacts FactsOf(Entity e, RebarSpec s, RebarSettings st, string count, string? step = null) =>
        new(count, s.Diameter, step, RebarShapes.Length(s), Rebars.Mark(e), e.Str("steel") ?? st.Steel);

    /// <summary>The partial style kept on a bar or a distribution (<c>label.style</c>).</summary>
    public static JsonObject? StyleOf(Entity e) => e.Attrs[LabelStyle.Key] is JsonObject o ? o["style"] as JsonObject : null;

    /// <summary>
    /// The labels drawn, the marks' order first (a label without a mark last), each where the user put it, else beside its bars on the
    /// style's side and shifted a row (the text's height and 2 mm) past any label already standing there: under one another for
    /// groups of dots (a cross-section), away from the bars for lines. A distribution with a string is marked along it instead.
    /// </summary>
    public static void Draw(ViewContext c, RebarSettings st, IReadOnlyList<LabelRequest> requests)
    {
        var taken = requests.Where(r => r.Placed?.At != null).Select(r => r.Placed!.At!.Value).ToList();
        var order = requests.Select((r, i) => (r, i)).OrderBy(x => x.r.MarkNo > 0 ? x.r.MarkNo : int.MaxValue).ThenBy(x => x.i).Select(x => x.r);
        foreach (var r in order)
        {
            var data = r.String != null && r.Style.Line == "string" ? DrawString(c, st, r, r.String) : DrawBeside(c, st, r, taken);
            if (data == null) continue;
            var mk = r.Mark;
            mk.Data ??= new JsonObject();
            if (r.Own) mk.Data["label"] = data;
            else
            {
                if (mk.Data["labels"] is not JsonArray labels) mk.Data["labels"] = labels = new JsonArray();
                data["owner"] = r.Owner;
                data["key"] = r.Key;
                labels.Add((JsonNode)data);
            }
        }
    }

    /// <summary>The text (and balloon) beside the bars, as decision 221 draws them; what the interface is told of the label, or null for a group with nothing drawn.</summary>
    static JsonObject? DrawBeside(ViewContext c, RebarSettings st, LabelRequest r, List<Vec2> taken)
    {
        var row = (st.TextHeight + 2) * c.Mm;
        var r0 = st.Balloon / 2 * c.Mm;
        var balloon = r.Style.ShowsBalloon(r.MarkNo);
        var place = r.Placed ?? LabelPlace.None;
        var pts = r.Segments.SelectMany(s => new[] { s.A, s.B }).Concat(r.Dots).ToList();
        if (pts.Count == 0) return null;
        var (lo, hi) = (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
        var side = r.Style.Side == "auto" ? r.AutoSide ?? (r.Along == null ? "right" : "above") : r.Style.Side;
        var (u, angle, horizontal) = Reading(r);
        Vec2 at;
        if (place.At is { } placed) at = placed;
        else
        {
            var off = r.Style.Offset * c.Mm + (balloon ? r0 : 0);
            at = side switch
            {
                "below" => new Vec2((lo.X + hi.X) / 2, lo.Y - off),
                "left" => new Vec2(lo.X - off, (lo.Y + hi.Y) / 2),
                "right" => new Vec2(hi.X + off, (lo.Y + hi.Y) / 2),
                _ => new Vec2((lo.X + hi.X) / 2, hi.Y + off),
            };
            // A balloon-led label along the bars is centred as a whole over the middle.
            if (!horizontal && balloon) at -= u * ((r.Text.Length * st.TextHeight * 0.3 + 0.5) * c.Mm);
            var shift = r.Along == null ? new Vec2(0, -1) : side switch { "below" => new Vec2(0, -1), "left" => new Vec2(-1, 0), "right" => new Vec2(1, 0), _ => new Vec2(0, 1) };
            for (var guard = 0; guard < 64; guard++)
            {
                var near = taken.Where(q => (q - at).Length < row * 0.99).ToList();
                if (near.Count == 0) break;
                at = near.OrderBy(q => Vec2.Dot(q - at, shift)).Last() + shift * row;
            }
        }
        taken.Add(at);
        var mk = r.Mark;
        var data = new JsonObject { ["at"] = Rebars.J(at) };
        if (balloon)
        {
            Balloon(mk, st, at, r0, r.MarkNo, r.Color);
            Leader(mk, c, r, at, r0, place.Elbow, Targets(r, place.Elbow ?? at), data);
            // The text to the right of the balloon (to its left when the label stands left of the bars), or after it along the direction.
            var left = horizontal && side == "left";
            var textAt = at + (left ? -u : u) * (r0 + 1.0 * c.Mm);
            mk.Text(new PenText(textAt, r.Text, st.TextHeight, angle, left ? "end" : "start", "middle"), r.Color);
        }
        else
        {
            var anchor = !horizontal ? "middle" : side switch { "left" => "end", "right" => "start", _ => "middle" };
            mk.Text(new PenText(at, r.Text, st.TextHeight, angle, anchor, "middle"), r.Color);
        }
        mk.Snaps.Add(at);
        return data;
    }

    /// <summary>
    /// A distribution marked as a dimension string (decision 222): a line parallel to its path at the offset outside the bars, a tick
    /// where each bar crosses it and at both ends, no values; the text above the line along it; the balloon touching the line's start
    /// end, or where the user put it, joined to the line by the leader. The string's middle is told for its grip (dragged across the bars, it changes the offset).
    /// </summary>
    static JsonObject DrawString(ViewContext c, RebarSettings st, LabelRequest r, LabelString sp)
    {
        var mk = r.Mark;
        var r0 = st.Balloon / 2 * c.Mm;
        var balloon = r.Style.ShowsBalloon(r.MarkNo);
        var place = r.Placed ?? LabelPlace.None;
        var length = sp.Path.Length;
        var mid = length / 2;
        var tangent = sp.Path.TangentAt(mid);
        var perp = tangent.Perp;
        // The side the string stands on, named as seen; left to itself, under a path running across the view, right of one running up it.
        var side = r.Style.Side == "auto" ? r.AutoSide ?? (Math.Abs(tangent.X) >= Math.Abs(tangent.Y) ? "below" : "right") : r.Style.Side;
        var outward = side switch { "above" => new Vec2(0, 1), "below" => new Vec2(0, -1), "left" => new Vec2(-1, 0), _ => new Vec2(1, 0) };
        var up = Vec2.Dot(outward, perp) >= 0 ? 1 : -1;
        var offset = place.Offset ?? r.Style.Offset;
        var y = up > 0 ? sp.Hi + offset * c.Mm : sp.Lo - offset * c.Mm;
        var line = sp.Path.Line(0, length, y);
        mk.Polyline(line, false, RebarShapes.AnnotationLayer, LeaderWidth, r.Color);
        var ticks = new List<double>();
        foreach (var s in sp.Stations.Concat([0, length]).OrderBy(s => s))
            if (ticks.Count == 0 || s - ticks[^1] > 1e-6) ticks.Add(s);
        foreach (var s in ticks)
        {
            var (p, n) = (sp.Path.At(s, y), sp.Path.TangentAt(s).Perp);
            mk.Line(p - n * (TickMm / 2 * c.Mm), p + n * (TickMm / 2 * c.Mm), RebarShapes.AnnotationLayer, LeaderWidth, r.Color);
        }
        // The text above the line as it reads (left to right), a millimetre off it, centred on its middle.
        var u = tangent;
        if (u.X < -1e-9 || (Math.Abs(u.X) < 1e-9 && u.Y < 0)) u = -u;
        var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
        var lineMid = sp.Path.At(mid, y);
        var textAt = lineMid + u.Perp * (1.0 * c.Mm);
        var data = new JsonObject();
        Vec2 at;
        if (balloon)
        {
            at = place.At ?? sp.Path.At(0, y) - sp.Path.TangentAt(0) * r0;
            Balloon(mk, st, at, r0, r.MarkNo, r.Color);
            var target = RebarModule.Nearest(line, place.Elbow ?? at, line[0]);
            Leader(mk, c, r, at, r0, place.Elbow, [target], data);
            mk.Text(new PenText(textAt, r.Text, st.TextHeight, angle, "middle", "bottom"), r.Color);
        }
        else
        {
            at = place.At ?? textAt;
            mk.Text(new PenText(at, r.Text, st.TextHeight, angle, "middle", "bottom"), r.Color);
        }
        data["at"] = Rebars.J(at);
        data["string"] = new JsonObject { ["mid"] = Rebars.J(lineMid), ["dir"] = Rebars.J(perp * up), ["offset"] = Math.Round(offset, 2), ["mm"] = c.Mm };
        mk.Snaps.Add(at);
        mk.Snaps.AddRange(line);
        return data;
    }

    /// <summary>The text's direction as seen: along the bars when they have one, else horizontal; readable left to right.</summary>
    static (Vec2 U, double Angle, bool Horizontal) Reading(LabelRequest r)
    {
        var u = LabelStyle.AngleOf(r.Style.Direction) is { } deg ? new Vec2(Math.Cos(deg * Math.PI / 180), Math.Sin(deg * Math.PI / 180))
            : r.Style.Direction == "along" && r.Along is { } along ? along : new Vec2(1, 0);
        if (u.X < -1e-9 || (Math.Abs(u.X) < 1e-9 && u.Y < 0)) u = -u;
        var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
        return (u, angle, Math.Abs(angle) < 1e-9);
    }

    static void Balloon(ViewMark mk, RebarSettings st, Vec2 at, double r0, int mark, string color)
    {
        mk.Polyline(ViewMark.Circle(at, r0, 32), true, RebarShapes.AnnotationLayer, 0.35, color);
        mk.Text(new PenText(at, mark.ToString(CultureInfo.InvariantCulture), st.TextHeight, 0, "middle", "middle", true), color);
    }

    /// <summary>
    /// The leader from the balloon's edge, through the elbow when there is one, to the targets; none when the style has none or the
    /// balloon already touches. The interface is told the elbow, or, without one, the leader's middle, where a drag makes the elbow.
    /// </summary>
    static void Leader(ViewMark mk, ViewContext c, LabelRequest r, Vec2 at, double r0, Vec2? elbow, IReadOnlyList<Vec2> targets, JsonObject data)
    {
        if (r.Style.Leader == "none" || targets.Count == 0) return;
        var gap = r0 + 0.5 * c.Mm;
        if (elbow is { } e)
        {
            if ((e - at).Length > gap) mk.Line(at + (e - at).Normalized() * r0, e, RebarShapes.AnnotationLayer, LeaderWidth, r.Color);
            foreach (var t in targets) if ((t - e).Length > 1e-9) mk.Line(e, t, RebarShapes.AnnotationLayer, LeaderWidth, r.Color);
            data["elbow"] = Rebars.J(e);
            return;
        }
        foreach (var t in targets)
            if ((t - at).Length > gap) mk.Line(at + (t - at).Normalized() * r0, t, RebarShapes.AnnotationLayer, LeaderWidth, r.Color);
        var centre = new Vec2(targets.Average(t => t.X), targets.Average(t => t.Y));
        if ((centre - at).Length > gap) data["leader"] = Rebars.J((at + (centre - at).Normalized() * r0 + centre) * 0.5);
    }

    /// <summary>
    /// Where a group's leaders end, from <paramref name="from"/> (the elbow, or the balloon): with <c>each</c>, every dot of the
    /// group (one for one; over 16, the two ends and the middle one in their order across the view); else the nearest point of the group.
    /// </summary>
    static List<Vec2> Targets(LabelRequest r, Vec2 from)
    {
        if (r.Style.Leader == "each" && r.Dots.Count > 0)
        {
            var dots = r.Dots.OrderBy(d => d.X).ThenBy(d => d.Y).ToList();
            return dots.Count <= 16 ? dots : [dots[0], dots[dots.Count / 2], dots[^1]];
        }
        return [Nearest(r.Segments, r.Dots, from)];
    }

    /// <summary>The point of the group nearest to <paramref name="p"/>: on its segments or at its dots.</summary>
    static Vec2 Nearest(IReadOnlyList<(Vec2 A, Vec2 B)> segments, IReadOnlyList<Vec2> dots, Vec2 p)
    {
        var best = dots.Count > 0 ? dots[0] : segments[0].A;
        var bestD = (best - p).Length;
        foreach (var d in dots) if ((d - p).Length < bestD) { bestD = (d - p).Length; best = d; }
        foreach (var (a, b) in segments)
        {
            var q = RebarModule.Nearest([a, b], p, a);
            if ((q - p).Length < bestD) { bestD = (q - p).Length; best = q; }
        }
        return best;
    }

    /// <summary>The side name a direction as seen points to: its dominant axis.</summary>
    public static string SideOf(Vec2 outward) => Math.Abs(outward.X) > Math.Abs(outward.Y) ? (outward.X > 0 ? "right" : "left") : (outward.Y > 0 ? "above" : "below");

    /// <summary>The direction most of a group's lines run in as seen (its longest line's); null for dots alone.</summary>
    public static Vec2? AlongOf(IReadOnlyList<(Vec2 A, Vec2 B)> segments)
    {
        if (segments.Count == 0) return null;
        var (a, b) = segments.MaxBy(s => (s.B - s.A).Length);
        return (b - a).Length < 1e-9 ? null : (b - a).Normalized();
    }

    /// <summary>
    /// A generated label's place on the view set from a command: {viewId, owner, key, at?, elbow?, offset?}, each given field set
    /// ([x, y], a number) or taken away (null: back where the engine puts it); the label forgotten when nothing is left.
    /// </summary>
    public static void Place(ICommandContext ctx, JsonObject a)
    {
        var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("A label is placed on a view.");
        var owner = (string?)a["owner"];
        var key = (string?)a["key"];
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(key)) throw new CommandException("Say whose label it is (owner) and which (key).");
        var attrs = view.CloneAttrs();
        if (attrs[ViewKey] is not JsonObject labels) attrs[ViewKey] = labels = new JsonObject();
        var k = $"{owner}:{key}";
        var place = LabelPlace.From(labels[k])?.ToJson() ?? new JsonObject();
        LabelPlace.Write(place, a);
        if (place.Count > 0) labels[k] = place; else labels.Remove(k);
        if (labels.Count == 0) attrs.Remove(ViewKey);
        ctx.Replace(view.With(attrs: attrs));
    }
}
