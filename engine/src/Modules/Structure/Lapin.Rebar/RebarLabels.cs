using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// What a group's label says and where it stands (decision 221): the text's parts in order, the mark's balloon, the text's
/// direction, the side of the bars it stands on and how far off them (paper mm), the leader. A style is kept as a partial
/// object wherever it is set (the project's settings by kind, the type, the element) and resolved most-specific-wins.
/// </summary>
public sealed record LabelStyle(IReadOnlyList<string> Parts, bool Balloon, string Direction, string Side, double Offset, bool Leader)
{
    public static readonly string[] Kinds = ["bars", "stirrups", "columnBars", "runs"];
    public static readonly string[] Tokens = ["count", "diameter", "step", "length", "mark", "steel"];
    public static readonly string[] Directions = ["horizontal", "along"];
    /// <summary>In the view's coordinates; <c>auto</c>: to the right of dots, above lines.</summary>
    public static readonly string[] Sides = ["auto", "above", "below", "left", "right"];
    public const string Key = "label";

    public static LabelStyle Default(string kind) => kind switch
    {
        "stirrups" or "runs" => new(["count", "diameter", "step"], true, "along", "auto", 4, true),
        "columnBars" => new(["count", "diameter"], true, "horizontal", "auto", 4, true),
        _ => new(["count", "diameter", "length"], true, "horizontal", "auto", 4, true),
    };

    /// <summary>The style with a partial one's fields over it.</summary>
    public LabelStyle Apply(JsonObject? o)
    {
        if (o == null) return this;
        var parts = o["parts"] is JsonArray pa ? pa.Select(x => (string?)x).OfType<string>().ToList() : null;
        return new LabelStyle(parts is { Count: > 0 } ? parts : Parts,
            o["balloon"] is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : Balloon,
            (string?)o["direction"] ?? Direction, (string?)o["side"] ?? Side, Json.Number(o["offset"]) ?? Offset,
            o["leader"] is JsonValue lv && lv.TryGetValue<bool>(out var l) ? l : Leader);
    }

    /// <summary>The style of a group of <paramref name="kind"/>: the defaults, the project's, the type's, then the element's.</summary>
    public static LabelStyle Resolve(string kind, RebarSettings st, JsonObject? type = null, JsonObject? element = null) =>
        Default(kind).Apply(st.Labels.GetValueOrDefault(kind)).Apply(type).Apply(element);

    public JsonObject ToJson() => new()
    {
        ["parts"] = new JsonArray(Parts.Select(p => (JsonNode)p).ToArray()), ["balloon"] = Balloon, ["direction"] = Direction, ["side"] = Side, ["offset"] = Offset, ["leader"] = Leader,
    };

    /// <summary>The direction's angle (degrees, the view's x axis at 0) when it is one; null for horizontal and along.</summary>
    public static double? AngleOf(string direction) =>
        direction.StartsWith("angle:", StringComparison.Ordinal) && double.TryParse(direction.AsSpan(6), NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : null;

    /// <summary>
    /// A partial style with a command's patch merged in, checked (a field set to null goes back to inheriting); null when nothing is left.
    /// The parts come as a list or as one string of tokens ("count diameter length").
    /// </summary>
    public static JsonObject? Patch(JsonObject? now, JsonNode? patch)
    {
        if (patch == null) return null;
        if (patch is not JsonObject po) throw new CommandException("A label style is an object: parts, balloon, direction, side, offset, leader.");
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
                case "balloon" or "leader":
                    o[k] = v is JsonValue bv && bv.TryGetValue<bool>(out var b) ? b : throw new CommandException($"{k} is yes or no.");
                    break;
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
/// A group's label asked for while its bars are drawn, and drawn once the view's groups are all known, so the labels left
/// where the engine puts them stack in the marks' order and never share a point. Its place is the user's when they moved it
/// (<see cref="Placed"/>): a bar's or a distribution's on the entity, a generated group's on the view (<c>rebarLabels</c>).
/// </summary>
public sealed record LabelRequest(ViewMark Mark, string Owner, string Key, LabelStyle Style, string Text, int MarkNo, string Color,
    IReadOnlyList<(Vec2 A, Vec2 B)> Segments, IReadOnlyList<Vec2> Dots, Vec2? Along, Vec2? Placed)
{
    /// <summary>A bar's or a distribution's own label is told to the interface as <c>data.label</c> (its grip drives the entity); a generated one in <c>data.labels</c>.</summary>
    public bool Own { get; init; }
    /// <summary>The side taken for <c>auto</c> when the group knows better than the rule (a distribution's dimension line: away from its bars).</summary>
    public string? AutoSide { get; init; }
}

public static class RebarLabels
{
    public const string ViewKey = "rebarLabels";

    /// <summary>Where the user put a generated group's label on this view, if they did.</summary>
    public static Vec2? PlacedOn(Entity view, string owner, string key) =>
        view.Attrs[ViewKey] is JsonObject o && o[$"{owner}:{key}"] is JsonArray a ? Rebars.P(a) : null;

    /// <summary>The facts a bar or a distribution drawn by hand gives its label.</summary>
    public static LabelFacts FactsOf(Entity e, RebarSpec s, RebarSettings st, string count, string? step = null) =>
        new(count, s.Diameter, step, RebarShapes.Length(s), Rebars.Mark(e), e.Str("steel") ?? st.Steel);

    /// <summary>The partial style kept on a bar or a distribution (<c>label.style</c>).</summary>
    public static JsonObject? StyleOf(Entity e) => e.Attrs[LabelStyle.Key] is JsonObject o ? o["style"] as JsonObject : null;

    /// <summary>
    /// The labels drawn, the marks' order first (a label without a mark last), each where the user put it, else beside its bars on the
    /// style's side and shifted a row (the text's height and 2 mm) past any label already standing there: under one another for
    /// groups of dots (a cross-section), away from the bars for lines.
    /// </summary>
    public static void Draw(ViewContext c, RebarSettings st, IReadOnlyList<LabelRequest> requests)
    {
        var taken = requests.Where(r => r.Placed != null).Select(r => r.Placed!.Value).ToList();
        var row = (st.TextHeight + 2) * c.Mm;
        var order = requests.Select((r, i) => (r, i)).OrderBy(x => x.r.MarkNo > 0 ? x.r.MarkNo : int.MaxValue).ThenBy(x => x.i).Select(x => x.r);
        foreach (var r in order)
        {
            var r0 = st.Balloon / 2 * c.Mm;
            var balloon = r.Style.ShowsBalloon(r.MarkNo);
            var pts = r.Segments.SelectMany(s => new[] { s.A, s.B }).Concat(r.Dots).ToList();
            if (pts.Count == 0) continue;
            var (lo, hi) = (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
            var side = r.Style.Side == "auto" ? r.AutoSide ?? (r.Along == null ? "right" : "above") : r.Style.Side;
            // The text's direction as seen: along the bars when they have one, else horizontal; readable left to right.
            var u = LabelStyle.AngleOf(r.Style.Direction) is { } deg ? new Vec2(Math.Cos(deg * Math.PI / 180), Math.Sin(deg * Math.PI / 180))
                : r.Style.Direction == "along" && r.Along is { } along ? along : new Vec2(1, 0);
            if (u.X < -1e-9 || (Math.Abs(u.X) < 1e-9 && u.Y < 0)) u = -u;
            var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
            var horizontal = Math.Abs(angle) < 1e-9;
            Vec2 at;
            if (r.Placed is { } placed) at = placed;
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
            if (balloon)
            {
                mk.Polyline(ViewMark.Circle(at, r0, 32), true, RebarShapes.AnnotationLayer, 0.35, r.Color);
                mk.Text(new PenText(at, r.MarkNo.ToString(CultureInfo.InvariantCulture), st.TextHeight, 0, "middle", "middle", true), r.Color);
                if (r.Style.Leader)
                {
                    var target = Nearest(r.Segments, r.Dots, at);
                    if ((target - at).Length > r0 + 0.5 * c.Mm) mk.Line(at + (target - at).Normalized() * r0, target, RebarShapes.AnnotationLayer, 0.25, r.Color);
                }
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
            mk.Data ??= new JsonObject();
            if (r.Own) mk.Data["label"] = Rebars.J(at);
            else
            {
                if (mk.Data["labels"] is not JsonArray labels) mk.Data["labels"] = labels = new JsonArray();
                labels.Add((JsonNode)new JsonObject { ["owner"] = r.Owner, ["key"] = r.Key, ["at"] = Rebars.J(at) });
            }
        }
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

    /// <summary>The view's placed labels set from a command: {viewId, owner, key, at: [x, y] | null (back where the engine puts it)}.</summary>
    public static void Place(ICommandContext ctx, JsonObject a)
    {
        var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("A label is placed on a view.");
        var owner = (string?)a["owner"];
        var key = (string?)a["key"];
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(key)) throw new CommandException("Say whose label it is (owner) and which (key).");
        var attrs = view.CloneAttrs();
        if (attrs[ViewKey] is not JsonObject labels) attrs[ViewKey] = labels = new JsonObject();
        if (a["at"] is JsonArray) labels[$"{owner}:{key}"] = Rebars.J(Args.Point(a["at"], "where the label goes"));
        else labels.Remove($"{owner}:{key}");
        if (labels.Count == 0) attrs.Remove(ViewKey);
        ctx.Replace(view.With(attrs: attrs));
    }
}
