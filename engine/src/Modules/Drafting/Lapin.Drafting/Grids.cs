using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting;

/// <summary>A grid axis: its tag (1, A, A1…) and its line in plan (m).</summary>
public sealed record Axis(string Tag, Vec2 A, Vec2 B, string? Ends = null)
{
    public Vec2 Dir => (B - A).Normalized();
    public JsonObject ToJson()
    {
        var o = new JsonObject { ["tag"] = Tag, ["a"] = new JsonArray(A.X, A.Y), ["b"] = new JsonArray(B.X, B.Y) };
        if (Ends != null) o["ends"] = Ends;
        return o;
    }
}

/// <summary>
/// The project's grids: an IfcGrid holds its axes, in plan, for every storey.
/// They are drawn in the project's default axis type (Drafting standards):
/// its bubble, ends, size, line type, colour and label text style, sized from
/// the view's scale. Plans show the axes; sections the axes they cross;
/// elevations those that run towards the eye.
/// </summary>
public static class Grids
{
    public static List<Axis> Axes(Entity grid) => grid.Attrs["axes"] is JsonArray arr
        ? arr.OfType<JsonObject>().Select(o => new Axis((string?)o["tag"] ?? "?", P(o["a"]), P(o["b"]), (string?)o["ends"] is "both" or "start" or "end" or "none" ? (string?)o["ends"] : null)).ToList()
        : [];

    static Vec2 P(JsonNode? n) => Args.PointOf(n);

    /// <summary>One axis as the interface selects it: the grid's id and the axis's tag.</summary>
    public static string AxisId(string gridId, string tag) => $"{gridId}#{tag}";

    public static (string Grid, string Tag)? ParseAxisId(string id) => id.IndexOf('#') is var i and > 0 ? (id[..i], id[(i + 1)..]) : null;

    /// <summary>
    /// The project's grid new axes go to: its first, but never one a module drives (a generator's own grid, rebuilt from
    /// its spec: docs/overrides.md, Driven elements); made when there is none.
    /// </summary>
    public static Entity Ensure(ICommandContext ctx)
    {
        var driven = ctx.Services.All<IDrivenElements>();
        if (ctx.Model.OfClass(IfcClass.Grid).FirstOrDefault(g => !driven.Any(d => d.Drives(ctx.Model, g))) is { } g) return g;
        var grid = new Entity(ctx.NewId(), IfcClass.Grid, "Grid", new JsonObject { ["axes"] = new JsonArray() });
        ctx.Add(grid);
        return grid;
    }

    /// <summary>The label after <paramref name="tag"/>: 1 → 2, 9 → 10, A → B, Z → AA, A1 → A2.</summary>
    public static string Next(string tag)
    {
        var m = System.Text.RegularExpressions.Regex.Match(tag, @"^(.*?)(\d+)$");
        if (m.Success) return m.Groups[1].Value + (long.Parse(m.Groups[2].Value) + 1);
        if (tag.Length == 0) return "1";
        var chars = tag.ToCharArray();
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (chars[i] is >= 'A' and < 'Z' or >= 'a' and < 'z') { chars[i]++; return new string(chars); }
            if (chars[i] is 'Z' or 'z') { chars[i] = chars[i] == 'Z' ? 'A' : 'a'; continue; }
            return tag + "1";
        }
        return (char.IsUpper(tag[0]) ? "A" : "a") + new string(chars);
    }

    /// <summary>
    /// The label before <paramref name="tag"/> (issue #64), Next's inverse: 2 → 1, 10 → 9, B → A, AA → Z, A2 → A1; null when
    /// there is none (0, A, a label with no letter or number to count down).
    /// </summary>
    public static string? Prev(string tag)
    {
        var m = System.Text.RegularExpressions.Regex.Match(tag, @"^(.*?)(\d+)$");
        if (m.Success)
        {
            var n = long.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (n == 0) return null;
            var digits = m.Groups[2].Value;
            var text = (n - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Leading zeros kept: 05 → 04.
            return m.Groups[1].Value + (digits.Length > 1 && digits[0] == '0' ? text.PadLeft(digits.Length, '0') : text);
        }
        if (tag.Length == 0 || !char.IsAsciiLetter(tag[^1])) return null;
        var chars = tag.ToCharArray();
        var i = chars.Length - 1;
        for (; i >= 0 && char.IsAsciiLetter(chars[i]); i--)
        {
            if (chars[i] is not ('A' or 'a')) { chars[i]--; return new string(chars); }
            chars[i] = chars[i] == 'A' ? 'Z' : 'z';
        }
        // Every letter borrowed: one letter fewer (AA → Z); a lone A has nothing before it.
        var head = i + 1;
        if (chars.Length - head == 1) return null;
        return new string(chars, 0, head) + new string(chars, head + 1, chars.Length - head - 1);
    }

    /// <summary>
    /// A family's labels counted from <paramref name="first"/> (Annotate → Grid…, issue #64): up with Next, or down with
    /// Prev when <paramref name="descending"/> (refused when they would run out).
    /// </summary>
    public static List<string> Series(string first, int count, bool descending = false)
    {
        var list = new List<string>(count);
        string? tag = first;
        for (var i = 0; i < count; i++)
        {
            if (tag == null) throw new CommandException($"Counting down from {first}, the labels run out after {list[^1]}: start from a higher one.");
            list.Add(tag);
            tag = descending ? Prev(tag) : Next(tag);
        }
        return list;
    }

    /// <summary>
    /// A new axis's tag: after the last axis that runs the same way; the first
    /// axis of a direction takes the axis type's first label, or letters when
    /// another direction already counts in numbers (and the other way round).
    /// </summary>
    public static string NextTag(IReadOnlyList<Axis> axes, Vec2 dir, string first)
    {
        var same = axes.Where(x => Math.Abs(Vec2.Cross(x.Dir, dir)) < 0.02).ToList();
        if (same.Count > 0) return Next(same[^1].Tag);
        var taken = axes.Select(x => x.Tag).ToHashSet();
        if (axes.Count == 0 || !taken.Contains(first)) return first;
        return char.IsDigit(first[0]) ? "A" : "1";
    }

    /// <summary>
    /// Parallel axes lined up: in each family of axes that run the same way
    /// (a 180° turn included), every axis starts on the family's first start
    /// line and ends on its last. An axis keeps its own direction, so its
    /// bubble stays at the end it was on. Axes alone in their family, and
    /// those not in <paramref name="only"/>, stay as they are.
    /// With <paramref name="to"/> (an axis's tag, forum 47, decision 531), that axis is the reference: the others of its
    /// family (those in <paramref name="only"/>, when given) take its start and end lines, it stays as it is, and the
    /// other families are left alone.
    /// </summary>
    public static List<Axis> Align(IReadOnlyList<Axis> axes, IReadOnlySet<string>? only, out int aligned, string? to = null)
    {
        var result = axes.ToList();
        aligned = 0;
        var reference = to == null ? -1 : axes.ToList().FindIndex(x => x.Tag == to);
        if (to != null && reference < 0) throw new CommandException($"The grid has no axis {to}.");
        var todo = Enumerable.Range(0, axes.Count).Where(i => only == null || only.Contains(axes[i].Tag) || i == reference).ToList();
        if (reference >= 0) todo = todo.Where(i => Math.Abs(Vec2.Cross(axes[i].Dir, axes[reference].Dir)) < 0.02).Prepend(reference).Distinct().ToList();
        while (todo.Count > 0)
        {
            var d = axes[todo[0]].Dir;
            var family = todo.Where(i => Math.Abs(Vec2.Cross(axes[i].Dir, d)) < 0.02).ToList();
            todo.RemoveAll(family.Contains);
            if (family.Count < 2) continue;
            double S(Vec2 p) => Vec2.Dot(p, d);
            var lines = reference >= 0 ? new List<int> { reference } : family;
            var lo = lines.Min(i => Math.Min(S(axes[i].A), S(axes[i].B)));
            var hi = lines.Max(i => Math.Max(S(axes[i].A), S(axes[i].B)));
            foreach (var i in family)
            {
                if (i == reference) continue;
                var x = axes[i];
                // The axis's line, as a point on it and the family's direction.
                var o = x.A - d * S(x.A);
                var (p, q) = (o + d * lo, o + d * hi);
                result[i] = Vec2.Dot(x.Dir, d) >= 0 ? x with { A = p, B = q } : x with { A = q, B = p };
                aligned++;
            }
        }
        return result;
    }

    /// <summary>Across an axis, the way its family counts: towards +x, or towards +y for an axis along x (whichever way it was drawn).</summary>
    public static Vec2 Across(Vec2 dir)
    {
        var n = new Vec2(-dir.Y, dir.X);
        return n.X < -1e-9 || (Math.Abs(n.X) <= 1e-9 && n.Y < 0) ? new Vec2(-n.X, -n.Y) : n;
    }

    /// <summary>Whether an axis is one of the family spaced along x (it runs up the plan rather than across it).</summary>
    public static bool AlongX(Axis x) => Math.Abs(x.B.Y - x.A.Y) >= Math.Abs(x.B.X - x.A.X);

    /// <summary>Whether a label holds an I or an O, the letters grids leave out (they read as 1 and 0).</summary>
    static bool HasIO(string label) => label.Any(ch => ch is 'I' or 'O' or 'i' or 'o');

    /// <summary>
    /// The labels of a renumbering (forum #75): numbers from <paramref name="start"/> (an integer, its leading zeros kept:
    /// 01, 02…), or letters from it (A, B… Z, AA…), leaving out those with an I or an O when <paramref name="skipIO"/>;
    /// each after <paramref name="prefix"/>. When <paramref name="descending"/> (issue #64) they count down from the start
    /// (10, 9, 8…; E, D, C…), refused when they would run out; with no start, down to 1 or A (the ascending labels the
    /// other way round).
    /// </summary>
    public static List<string> Labels(int count, string scheme, string? start, string prefix = "", bool skipIO = true, bool descending = false)
    {
        if (descending && string.IsNullOrWhiteSpace(start))
        {
            var up = Labels(count, scheme, null, prefix, skipIO);
            up.Reverse();
            return up;
        }
        var list = new List<string>(count);
        if (scheme == "numbers")
        {
            var first = start?.Trim() is { Length: > 0 } st ? st : "1";
            if (!long.TryParse(first, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n)) throw new CommandException("Numbers start from a whole number: 1, 0, 101.");
            if (descending && n - (count - 1) < 0) throw new CommandException($"Counting down from {first}, {count} axes need a start of {count - 1} or more.");
            // Counting down, only a start written with leading zeros keeps its width (05, 04…; 10, 9…).
            var width = !descending || (first.Length > 1 && first[0] == '0') ? first.Length : 1;
            for (var i = 0; i < count; i++) list.Add(prefix + (descending ? n - i : n + i).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width, '0'));
            return list;
        }
        if (scheme != "letters") throw new CommandException("Axes are renumbered with numbers or letters.");
        string? label = start?.Trim() is { Length: > 0 } sl ? sl : "A";
        if (!label.All(char.IsAsciiLetter)) throw new CommandException("Letters start from a letter: A, a, AA.");
        string? Step(string l) => descending ? Prev(l) : Next(l);
        if (skipIO) while (label != null && HasIO(label)) label = Step(label);
        for (var i = 0; i < count; i++)
        {
            if (label == null) throw new CommandException($"Counting down from {start!.Trim()}, the letters run out before {count} axes: start from a later one.");
            list.Add(prefix + label);
            label = Step(label);
            if (skipIO) while (label != null && HasIO(label)) label = Step(label);
        }
        return list;
    }

    /// <summary>
    /// The new tags of a renumbering (forum #75): the axes named (all of the family when none is, x the ones spaced along x,
    /// numbered left to right; y those spaced along y, bottom to top) in order across their family, each given the next
    /// label. The axes named must run one way. With <paramref name="reverse"/> (issue #64) the order is the other way: right
    /// to left, top to bottom; with <paramref name="descending"/> the labels count down (Labels). Old tag → new tag, the
    /// unchanged ones too.
    /// </summary>
    public static Dictionary<string, string> Renumber(IReadOnlyList<Axis> axes, IReadOnlyCollection<string>? tags, string? direction, string scheme, string? start, string prefix = "", bool skipIO = true, bool reverse = false, bool descending = false)
    {
        List<Axis> chosen;
        if (tags is { Count: > 0 })
        {
            chosen = axes.Where(x => tags.Contains(x.Tag)).ToList();
            if (chosen.Count == 0) throw new CommandException("The grid has none of those axes.");
            var d0 = chosen[0].Dir;
            if (chosen.Any(x => Math.Abs(Vec2.Cross(x.Dir, d0)) >= 0.02)) throw new CommandException("Renumber the axes of one direction at a time.");
        }
        else
        {
            var x = direction switch { "x" => true, "y" => false, _ => throw new CommandException("Which axes: x (numbered left to right) or y (bottom to top)?") };
            chosen = axes.Where(a => AlongX(a) == x).ToList();
            if (chosen.Count == 0) throw new CommandException(x ? "The grid has no axis running up the plan." : "The grid has no axis running across the plan.");
        }
        var n = Across(chosen[0].Dir);
        var sign = reverse ? -1 : 1;
        var ordered = chosen.Select((a, i) => (a, i)).OrderBy(p => sign * Math.Round(Vec2.Dot(p.a.A, n), 6)).ThenBy(p => p.i).Select(p => p.a).ToList();
        var labels = Labels(ordered.Count, scheme, start, prefix ?? "", skipIO, descending);
        var map = new Dictionary<string, string>();
        for (var i = 0; i < ordered.Count; i++) map[ordered[i].Tag] = labels[i];
        var others = axes.Where(a => !map.ContainsKey(a.Tag)).Select(a => a.Tag).ToHashSet();
        if (map.Values.FirstOrDefault(others.Contains) is { } clash) throw new CommandException($"The grid has an axis {clash} already: start the labels after it, or give them a prefix.");
        return map;
    }

    /// <summary>
    /// What names a grid's axes by their tag follows them when tags change (a renumbering, an axis renamed): a dimension's
    /// end on an axis ({kind: grid, grid, tag}) and any "grid#tag" an element keeps (a selection, a view's list). One pass
    /// over the model; only the elements that change are replaced, in the command's undo step.
    /// </summary>
    public static int Retag(ICommandContext ctx, string gridId, IReadOnlyDictionary<string, string> map)
    {
        var moves = map.Where(p => p.Key != p.Value).ToDictionary(p => p.Key, p => p.Value);
        if (moves.Count == 0) return 0;
        var hash = gridId + "#";
        string? Moved(JsonNode? v) => v is JsonValue sv && sv.TryGetValue<string>(out var s) && s.StartsWith(hash, StringComparison.Ordinal) && moves.TryGetValue(s[hash.Length..], out var t) ? hash + t : null;
        bool Walk(JsonNode? n, bool apply)
        {
            var changed = false;
            switch (n)
            {
                case JsonObject o:
                    if ((string?)(o["kind"] as JsonValue) == "grid" && o["grid"] is JsonValue gv && gv.TryGetValue<string>(out var g) && g == gridId
                        && o["tag"] is JsonValue tv && tv.TryGetValue<string>(out var tag) && moves.TryGetValue(tag, out var nt))
                    {
                        if (!apply) return true;
                        o["tag"] = nt;
                        changed = true;
                    }
                    foreach (var (k, v) in o.ToList())
                    {
                        if (k == "tag" && changed) continue;
                        if (Moved(v) is { } m) { if (!apply) return true; o[k] = m; changed = true; }
                        else if (v is JsonObject or JsonArray && Walk(v, apply)) { if (!apply) return true; changed = true; }
                    }
                    break;
                case JsonArray arr:
                    for (var i = 0; i < arr.Count; i++)
                    {
                        if (Moved(arr[i]) is { } m) { if (!apply) return true; arr[i] = m; changed = true; }
                        else if (arr[i] is JsonObject or JsonArray && Walk(arr[i], apply)) { if (!apply) return true; changed = true; }
                    }
                    break;
            }
            return changed;
        }
        var count = 0;
        foreach (var e in ctx.Model.Entities.Where(e => e.Id != gridId && Walk(e.Attrs, false)).ToList())
        {
            var attrs = e.CloneAttrs();
            Walk(attrs, true);
            ctx.Replace(e.With(attrs: attrs));
            count++;
        }
        return count;
    }

    public static IEnumerable<ViewMark> Marks(ViewContext c)
    {
        var axisStyle = Standards.ResolveAxis(c.Book, new JsonObject { ["axisStyle"] = c.Book.DefaultAxisStyle });
        var text = Standards.ResolveText(c.Book, new JsonObject { ["axisStyle"] = c.Book.DefaultAxisStyle });
        var color = axisStyle.Color ?? "#000000";
        var dash = Standards.DashOf(c.Book, axisStyle.LineType);
        var r = axisStyle.Size * c.Mm / 2;
        // Sections and elevations run the axes over what they draw, else over the storeys.
        var storeys = Spatial.Storeys(c.Model).ToList();
        (double Y0, double Y1) span = c.Drawing.Bounds is { } drawn ? (drawn.Min.Y, drawn.Max.Y)
            : storeys.Count > 0 ? (storeys.Min(x => x.Num("elevation")), storeys.Max(x => x.Num("elevation") + x.Num("height", 3))) : (0, 3);
        foreach (var grid in c.Model.OfClass(IfcClass.Grid))
            foreach (var axis in Axes(grid))
            {
                Vec2 a, b;
                if (c.Kind == "plan") (a, b) = (axis.A, axis.B);
                else
                {
                    var k = c.Scale / 100;
                    double x;
                    if (c.Kind == "section")
                    {
                        // Where the axis crosses the section's cut: its plane, or the piece of a jogged section whose stretch holds it (report #146).
                        if (Views.CrossingX(c, axis.A, axis.B) is not { } cx) continue;
                        x = cx;
                    }
                    else
                    {
                        // An elevation sees the axes that run towards it.
                        var look = new Vec2(c.Frame.Dir.X, c.Frame.Dir.Y);
                        if (Math.Abs(Vec2.Dot(axis.Dir, look)) < 0.7) continue;
                        x = c.Map(axis.A).X;
                    }
                    (a, b) = (new Vec2(x, span.Y0 - 0.5 * k), new Vec2(x, span.Y1 + 1.0 * k));
                }
                // Each axis is its own mark: a click selects that axis, Delete removes it.
                var mk = new ViewMark(AxisId(grid.Id, axis.Tag));
                mk.Line(a, b, "GRID", Pens.Default.Hidden, color, dash);
                var u = (b - a).Normalized();
                var ends = c.Kind == "plan" ? axis.Ends ?? axisStyle.Ends : "end";
                // A symbol of the user's for the bubbles (the grid's own, else the project's), its insertion point on the axis' end, +y outward.
                var bubbleSymbol = SymbolSlots.SymbolFor(c.Model, "gridBubble", grid);
                foreach (var (end, outward, on) in new[] { (a, -u, ends is "both" or "start"), (b, u, ends is "both" or "end") })
                {
                    if (!on) continue;
                    if (bubbleSymbol != null)
                    {
                        var tag = axis.Tag;
                        SymbolSlots.Draw(c, mk, bubbleSymbol, end, outward, false, axisStyle.Size / Standards.DefaultAxis.Size, "GRID", color,
                            key => key is "grid.label" or "attr.label" ? (true, tag) : (false, null));
                        continue;
                    }
                    var centre = end + outward * r;
                    var outline = axisStyle.Bubble == "square"
                        ? new[] { centre + new Vec2(-r, -r), centre + new Vec2(r, -r), centre + new Vec2(r, r), centre + new Vec2(-r, r) }
                        : ViewMark.Circle(centre, r);
                    mk.Polyline(outline, true, "GRID", Pens.Default.Seen, color);
                    mk.Text(new PenText(centre, axis.Tag, Math.Min(text.Height, axisStyle.Size * 0.55), Baseline: "middle", Bold: text.Bold, Font: text.Font, Italic: text.Italic), color);
                }
                mk.Snaps.AddRange([a, b]);
                yield return mk;
            }
    }
}
