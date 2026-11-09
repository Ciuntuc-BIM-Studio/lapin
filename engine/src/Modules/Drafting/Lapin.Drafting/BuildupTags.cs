using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>
/// A build-up tag on a view (docs/plan-buildup-tags.md): the element's layers written as an architectural note, its
/// header, a line per layer and its footer, left-aligned from the tag's point (the block's top left), wrapped at the tag
/// type's width; and its leaders, one from the block's side to the element (bent at an elbow, pointed elsewhere, as a
/// box's), or one per layer, each from its line out of the block's side facing the element to a point inside its own
/// layer. Those points lie on a line across the layers through the leader's end (the element's middle until it is
/// dragged along it): in a plan where the wall's layers lie across its axis there, in a section where the layers' cut
/// crosses that line (a layer of no thickness on the line between its neighbours). Where a view cannot tell the layers
/// apart (a floor in plan, an elevation) the tag keeps one leader. The tag follows its element (decision 320) as any tag.
/// With an elbow leader (decision 331, <see cref="TagLeaders"/>) the tag's point is the knee and the block lies beside it.
/// </summary>
public static class BuildupTags
{
    static JsonArray J(Vec2 p) => new(p.X, p.Y);

    /// <summary>Where the leader points by default: the axis's middle on a path element in plan, else the middle of what it draws.</summary>
    internal static Vec2? DefaultEnd(ViewContext c, Entity e)
    {
        if (c.Kind == "plan" && e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray)
        {
            var curve = PathCurve.Of(e);
            var p = curve.PointAt(curve.Length / 2);
            return c.Map(new Vec3(p.X, p.Y, 0));
        }
        return Tags.Anchor(c.Drawing, e.Id);
    }

    /// <summary>Where a line A + d·t crosses even-odd loops: the stretches of t inside them.</summary>
    static List<(double A, double B)> Inside(IEnumerable<Vec2[]> loops, Vec2 a, Vec2 d)
    {
        var ts = new List<double>();
        foreach (var loop in loops)
            for (var i = 0; i < loop.Length; i++)
            {
                var (p, q) = (loop[i], loop[(i + 1) % loop.Length]);
                var e = q - p;
                var den = d.X * e.Y - d.Y * e.X;
                if (Math.Abs(den) < 1e-12) continue;
                var w = p - a;
                var t = (w.X * e.Y - w.Y * e.X) / den;
                var u = (w.X * d.Y - w.Y * d.X) / den;
                // Each edge counted once at a corner: its start in, its end out.
                if (u >= 0 && u < 1) ts.Add(t);
            }
        ts.Sort();
        var o = new List<(double, double)>();
        for (var i = 0; i + 1 < ts.Count; i += 2) if (ts[i + 1] - ts[i] > 1e-9) o.Add((ts[i], ts[i + 1]));
        return o;
    }

    static (double A, double B)? Nearest(List<(double A, double B)> spans) =>
        spans.Count == 0 ? null : spans.MinBy(s => s.A <= 0 && s.B >= 0 ? 0 : Math.Min(Math.Abs(s.A), Math.Abs(s.B)));

    /// <summary>
    /// The point inside each layer a leader ends at, by its index in the set, on the line across the layers through
    /// <paramref name="anchor"/>; null when this view cannot tell the layers apart.
    /// </summary>
    public static Dictionary<int, Vec2>? LayerPoints(ViewContext c, Entity e, BuildUp b, Vec2 anchor)
    {
        var f = c.Frame;
        var world = f.Origin + f.Right * anchor.X + f.Up * anchor.Y;
        var path = e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray && !b.Stack;
        Vec3 across;
        PathCurve curve = default;
        var s = 0.0;
        if (path)
        {
            curve = PathCurve.Of(e);
            if (curve.Length < 1e-9) return null;
            s = Math.Clamp(curve.Project(new Vec2(world.X, world.Y)), 0, curve.Length);
            var n = curve.TangentAt(s).Perp;
            across = new Vec3(n.X, n.Y, 0);
        }
        else across = Vec3.UnitZ;
        var d = new Vec2(Vec3.Dot(across, f.Right), Vec3.Dot(across, f.Up));
        if (d.Length < 0.3) return null;
        d = d.Normalized();
        var points = new Dictionary<int, Vec2>();
        // A plan: the layers across the wall's axis where the line crosses it, as its usage lays them.
        if (path && c.Kind == "plan" && LayerSets.BandsOf(c.Model, e.Id) is { Count: > 0 } bands)
        {
            foreach (var band in bands)
            {
                var p = curve.At(s, (band.From + band.To) / 2);
                points[band.Index] = c.Map(new Vec3(p.X, p.Y, world.Z));
            }
            return points;
        }
        // Elsewhere: where the line crosses each layer's cut.
        var spans = new Dictionary<int, (double A, double B)>();
        foreach (var l in b.Layers)
        {
            var key = $"layer:{l.Index}";
            var loops = c.Drawing.Cuts.Where(p => p.Element == e.Id && (p.Key == key || p.Key?.StartsWith(key + ":", StringComparison.Ordinal) == true)).SelectMany(p => p.Loops);
            if (Nearest(Inside(loops, anchor, d)) is { } span) spans[l.Index] = span;
        }
        if (spans.Count == 0) return null;
        foreach (var (i, span) in spans) points[i] = anchor + d * ((span.A + span.B) / 2);
        // A layer of no thickness: on the line between its neighbours' cuts.
        foreach (var l in b.Layers.Where(l => !spans.ContainsKey(l.Index)))
        {
            if (spans.TryGetValue(l.Index - 1, out var before) && spans.TryGetValue(l.Index + 1, out var after))
            {
                var (ta, tb) = new[] { (before.A, after.A), (before.A, after.B), (before.B, after.A), (before.B, after.B) }.MinBy(x => Math.Abs(x.Item1 - x.Item2));
                points[l.Index] = anchor + d * ((ta + tb) / 2);
            }
        }
        return points;
    }

    public static ViewMark Mark(ViewContext c, Entity t, Entity? e, TagType type, TextStyle style, Func<Vec2, Vec2>? follow)
    {
        Vec2? Followed(Vec2? p) => p is { } q && follow != null ? follow(q) : p;
        // A text of the tag's own (Properties → Text) is its line per layer, over its type's.
        if (t.Str("template") is { Length: > 0 } own) type = type with { Template = own };
        Vec2? Pt(JsonNode? n) => n is JsonArray a && a.Count >= 2 && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;
        var at = Followed(Pt(t.Attrs["at"])) ?? default;
        var color = e == null ? "#c0392b" : style.Color ?? "#000000";
        var mm = c.Mm;
        var h = style.Height * mm;
        var mk = new ViewMark(t.Id);
        var buildUp = e != null ? BuildUps.Of(c.Model, e, c.Services) : null;
        var content = e != null && buildUp != null ? BuildupText.Content(c.Model, e, type, c.Book.Units, c.Services, buildUp) : null;
        content ??= new BuildupContent(["?"], [], [], false);
        var lay = BuildupText.Lay(content, style.Height, style.Bold, type.BulletOrDefault, type.WidthOrDefault);
        var angle = Tags.AngleOf(c, t, e);
        var turn = new Turn(at, angle * Math.PI / 180);
        var leaderOn = t.Attrs["leader"] is not JsonValue lo || !lo.TryGetValue<bool>(out var isOn) || isOn;
        // An elbow (decision 331): the tag's point is the knee, the block beside it, the leaders' knees one column.
        if (TagLeaders.ShapeOf(t, type) == TagLeaders.Elbow)
        {
            var to = Followed(Pt(t.Attrs["end"])) ?? (e != null ? DefaultEnd(c, e) : null);
            List<(int, Vec2)>? targets = null;
            if (leaderOn && to is { } a && e != null && buildUp != null && type.LeaderPerLayer && content.Entries.Count > 0 && LayerPoints(c, e, buildUp, a) is { Count: > 0 } pts)
                targets = content.Entries.Select((x, i) => (i, pts.TryGetValue(x.Layer, out var q) ? q : (Vec2?)null)).Where(x => x.Item2 != null).Select(x => (x.i, x.Item2!.Value)).ToList();
            var (em, head, side) = TagLeaders.Buildup(t.Id, lay, type, style, mm, at, to, leaderOn, targets, color, turn.Angle);
            em.Data = Tags.ElbowData(at, head, side, leaderOn, e, to);
            Tags.AngleData(em.Data, t, e, c, head, lay.Height * mm / 2 + 1.5 * h);
            em.Data["buildup"] = true;
            em.Snaps.Add(at);
            return em;
        }
        foreach (var x in lay.Texts)
            mk.Text(turn.Text(new Vec2(at.X + x.X * mm, at.Y - x.Y * mm), x.Text, style, "start", "bottom"), color);
        var (left, right, top, bottom) = (at.X, at.X + lay.Width * mm, at.Y, at.Y - lay.Height * mm);
        // What a click on the tag hits: its block, never drawn.
        mk.Polyline([turn.P(new(left, bottom)), turn.P(new(right, bottom)), turn.P(new(right, top)), turn.P(new(left, top))], true, "PICK", 0, color);
        var data = new JsonObject { ["at"] = J(at), ["leader"] = leaderOn, ["of"] = e?.Id, ["buildup"] = true, ["shape"] = TagLeaders.Straight };
        Tags.AngleData(data, t, e, c, turn.P(new Vec2(at.X + lay.Width * mm / 2, at.Y - lay.Height * mm / 2)), lay.Height * mm / 2 + 1.5 * h);
        var end = Followed(Pt(t.Attrs["end"])) ?? (e != null ? DefaultEnd(c, e) : null);
        var elbow = Followed(Pt(t.Attrs["elbow"]));
        var dot = h / 5;
        var drawn = false;
        if ((bool)data["leader"]! && end is { } anchor && e != null && buildUp != null && type.LeaderPerLayer && content.Entries.Count > 0
            && LayerPoints(c, e, buildUp, anchor) is { Count: > 0 } points)
        {
            // One per layer: out of the block's side facing the layers, at its line, a short stub, then into its layer.
            var targets = content.Entries.Select((x, i) => (Entry: i, To: points.TryGetValue(x.Layer, out var p) ? p : (Vec2?)null)).Where(x => x.To != null).ToList();
            if (targets.Count > 0)
            {
                var toRight = targets.Average(x => turn.Inv(x.To!.Value).X) > (left + right) / 2;
                var (side, sign) = toRight ? (right + 0.8 * h, 1) : (left - 0.8 * h, -1);
                foreach (var (entry, to) in targets)
                {
                    var from = turn.P(new Vec2(side, at.Y - lay.EntryMid[entry] * mm));
                    var stub = from + turn.V(new Vec2(sign * 2.5 * mm, 0));
                    mk.Polyline([from, stub, to!.Value], false, "TAG", Pens.Default.Hatch, color);
                    mk.Polyline(ViewMark.Circle(to.Value, dot, 8), true, "TAG", Pens.Default.Seen, color);
                    mk.Snaps.Add(to.Value);
                }
                data["end"] = J(anchor);
                drawn = true;
            }
        }
        if (!drawn && (bool)data["leader"]! && end is { } target)
        {
            // One: from the side of the block nearer the element (its middle), through its elbow.
            var toward = turn.Inv(elbow ?? target);
            var midY = (top + bottom) / 2;
            var from = turn.P(toward.X < left ? new Vec2(left - 0.8 * h, midY)
                : toward.X > right ? new Vec2(right + 0.8 * h, midY)
                : new Vec2((left + right) / 2, toward.Y > top ? top + 0.5 * h : bottom - 0.5 * h));
            Vec2[] path = elbow is { } k ? [from, k, target] : [from, target];
            mk.Polyline(path, false, "TAG", Pens.Default.Hatch, color);
            mk.Polyline(ViewMark.Circle(target, dot, 8), true, "TAG", Pens.Default.Seen, color);
            data["from"] = J(from);
            data["end"] = J(target);
            if (elbow is { } kk) data["elbow"] = J(kk);
            mk.Snaps.Add(target);
        }
        mk.Data = data;
        mk.Snaps.Add(at);
        return mk;
    }
}
