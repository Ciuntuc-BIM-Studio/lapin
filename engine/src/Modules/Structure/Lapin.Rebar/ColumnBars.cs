using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Rebar;

/// <summary>
/// The longitudinal bars a column's type is configured with (decision 218): so many on each face, the corners shared,
/// of one diameter; a column is reinforced storey by storey, so its bars run from its base to its top and on past it by the
/// lap (starter bars, 1 m by default) for the column above. Laid inside the stirrups; kept on the type as <c>bars</c>.
/// <see cref="Top"/> says how they end (decision 232): <c>auto</c> as starter bars where a column stands on this one, else with
/// a 90° hook inside the top (the top storey's columns); <c>starter</c> or <c>hooks</c> always.
/// </summary>
public sealed record ColumnBarsSpec(bool Enabled, int PerFace, double Diameter, double Lap, int Mark, JsonObject? Label = null, string Top = "auto")
{
    public const string Key = "bars";
    public static readonly string[] Tops = ["auto", "starter", "hooks"];
    /// <summary>The type's own label style, partial, over the project's (<see cref="LabelStyle"/>).</summary>
    public JsonObject? Label { get; init; } = Label;

    public static ColumnBarsSpec Default() => new(false, 3, 0.016, 1.0, 0);

    public static ColumnBarsSpec? Of(Entity type) => type.Attrs[Key] is JsonObject o ? From(o) : null;

    public static ColumnBarsSpec From(JsonObject o)
    {
        var d = Default();
        return new ColumnBarsSpec(o["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) && en, (int)Math.Max(2, Json.Number(o["perFace"]) ?? d.PerFace),
            Json.Number(o["diameter"]) ?? d.Diameter, Json.Number(o["lap"]) ?? d.Lap, (int)(Json.Number(o["mark"]) ?? 0), o[LabelStyle.Key] as JsonObject,
            (string?)o["top"] is { } top && Tops.Contains(top) ? top : d.Top);
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["enabled"] = Enabled, ["perFace"] = PerFace, ["diameter"] = Diameter, ["lap"] = Lap, ["mark"] = Mark, ["top"] = Top };
        if (Label != null) o[LabelStyle.Key] = Label.DeepClone();
        return o;
    }
}

public static class ColumnBars
{
    /// <summary>
    /// A column's bars laid: each a chain in space from the base, up to the top plus the lap, or up to the cover under the
    /// top and bent inwards by a 90° hook (<see cref="Hooked"/>); each bar's nominal length for the schedule, the hook's tail
    /// included (a hook's tail is as long as the face it turns across lets it be), and the longest, for the label.
    /// </summary>
    public sealed record Laid(Entity Element, ColumnBarsSpec Spec, List<Vec3[]> Bars, IReadOnlyList<double> Lengths, bool Hooked = false)
    {
        public double Length => Lengths.Max();
    }

    /// <summary>The columns by the level of their base, to find the one standing on a column without asking every column.</summary>
    public sealed class Bases
    {
        readonly ILookup<long, (Vec2 At, double Z, string Id)> _byLevel;
        public Bases(IModelView m) => _byLevel = m.OfClass(IfcClass.Column).Select(o => (AtOf(o), BaseOf(m, o), o.Id)).ToLookup(x => Key(x.Item2));
        static long Key(double z) => (long)Math.Floor(z / Tolerance);
        /// <summary>The columns whose base is within <see cref="Tolerance"/> of <paramref name="z"/>.</summary>
        public IEnumerable<(Vec2 At, double Z, string Id)> Near(double z)
        {
            var k = Key(z);
            return _byLevel[k - 1].Concat(_byLevel[k]).Concat(_byLevel[k + 1]).Where(x => Math.Abs(x.Z - z) <= Tolerance);
        }
    }

    /// <summary>How near a column's base must be to another's top to stand on it.</summary>
    public const double Tolerance = 0.05;

    static Vec2 AtOf(Entity e) => e.Attrs["at"] is JsonArray q && q.Count >= 2 ? new Vec2(Json.Number(q[0]) ?? 0, Json.Number(q[1]) ?? 0) : default;
    static double BaseOf(IModelView m, Entity e) => (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");

    public static ColumnBarsSpec? SpecOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type ? ColumnBarsSpec.Of(type) : null;

    /// <summary>A column's bars laid; <paramref name="bases"/>, made once, when many columns are asked in turn.</summary>
    public static Laid? Of(IModelView m, Entity e, Bases? bases = null)
    {
        if (e.Class != IfcClass.Column || SpecOf(m, e) is not { Enabled: true } spec) return null;
        var (section, _, height) = Columns.Spec(m, e);
        if (section.Kind != "rectangle" || height <= 1e-6) return null;
        var stirrups = TypeStirrups.SpecOf(m, e) ?? StirrupSpec.Default();
        var inset = stirrups.Cover + (stirrups.Enabled ? stirrups.Diameter : 0) + spec.Diameter / 2;
        var (hx, hy) = (section.Width / 2 - inset, section.Depth / 2 - inset);
        if (hx <= 0 || hy <= 0) return null;
        var at = AtOf(e);
        var baseZ = BaseOf(m, e);
        var rot = e.Num("rotation") * Math.PI / 180;
        var (u, v) = (new Vec2(Math.Cos(rot), Math.Sin(rot)), new Vec2(-Math.Sin(rot), Math.Cos(rot)));
        // Round the perimeter: the corners once, the rest of each face's bars between them.
        var n = spec.PerFace;
        var places = new List<Vec2>();
        foreach (var x in Rebar3D.Spread(n, -hx, hx)) places.Add(new Vec2(x, -hy));
        foreach (var y in Rebar3D.Spread(n, -hy, hy).Skip(1)) places.Add(new Vec2(hx, y));
        foreach (var x in Rebar3D.Spread(n, -hx, hx).Reverse<double>().Skip(1)) places.Add(new Vec2(x, hy));
        foreach (var y in Rebar3D.Spread(n, -hy, hy).Reverse<double>().Skip(1).Take(n - 2)) places.Add(new Vec2(-hx, y));
        var hooked = spec.Top == "hooks" || (spec.Top == "auto" && !Continues(bases ?? new Bases(m), e, at, u, v, section.Width / 2, section.Depth / 2, baseZ + height));
        if (!hooked)
        {
            var bars = places.Select(p => { var w = at + u * p.X + v * p.Y; return new[] { w.At(baseZ), w.At(baseZ + height + spec.Lap) }; }).ToList();
            return new Laid(e, spec, bars, [.. bars.Select(_ => height + spec.Lap)]);
        }
        // The top storey's bars: up to the cover under the top, then a 90° hook (12Ø) inwards, across the face the bar stands on
        // (a corner's along the width), never past the opposite bars.
        var top = height - inset;
        var hook = RebarShapes.HookLength(spec.Diameter, 90);
        var tails = places.Select(p =>
        {
            var alongX = Math.Abs(p.X) / hx >= Math.Abs(p.Y) / hy - 1e-9;
            return (AlongX: alongX, Tail: Math.Min(hook, 2 * (alongX ? hx : hy)));
        }).ToList();
        var hooks = places.Select((p, i) =>
        {
            var w = at + u * p.X + v * p.Y;
            var dir = tails[i].AlongX ? u * -Math.Sign(p.X) : v * -Math.Sign(p.Y);
            return new[] { w.At(baseZ), w.At(baseZ + top), (w + dir * tails[i].Tail).At(baseZ + top) };
        }).ToList();
        return new Laid(e, spec, hooks, [.. tails.Select(t => top + t.Tail)], true);
    }

    /// <summary>Whether a column stands on this one: its base at this one's top (within 5 cm) and its point inside this one's section.</summary>
    static bool Continues(Bases bases, Entity e, Vec2 at, Vec2 u, Vec2 v, double hw, double hd, double topZ)
    {
        foreach (var (oat, _, id) in bases.Near(topZ))
        {
            if (id == e.Id) continue;
            var p = oat - at;
            if (Math.Abs(Vec2.Dot(p, u)) <= hw + 1e-6 && Math.Abs(Vec2.Dot(p, v)) <= hd + 1e-6) return true;
        }
        return false;
    }

    /// <summary>The columns' bars as a view sees them: dots where they pierce it (a horizontal cut, a structural plan), lines up a vertical section; the label asked of the view's labels, keyed <c>columnBars</c>.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, RebarSettings st, List<LabelRequest> labels)
    {
        var m = c.Model;
        if (!Rebar3D.Shows(c, out var limit, out var plan)) yield break;
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        if (hidden.Contains(RebarShapes.LongitudinalLayer)) yield break;
        var storey = c.View.Str("storeyId");
        var bases = new Bases(m);
        foreach (var e in m.OfClass(IfcClass.Column))
        {
            if (Of(m, e, bases) is not { } laid) continue;
            if (plan && Spatial.StoreyOf(m, e.Id)?.Id != storey) continue;
            var (dots, lines) = Rebar3D.Project(c, laid.Bars, limit, plan);
            if (dots.Count == 0 && lines.Count == 0) continue;
            var mk = new ViewMark($"{e.Id}:columnBars");
            var color = Rebar3D.Draw(mk, c, st, dots, lines, laid.Spec.Diameter, RebarShapes.LongitudinalLayer);
            var style = LabelStyle.Resolve("columnBars", st, laid.Spec.Label);
            var text = style.Text(new LabelFacts(laid.Bars.Count.ToString(CultureInfo.InvariantCulture), laid.Spec.Diameter, null, laid.Length, laid.Spec.Mark, st.Steel));
            labels.Add(new LabelRequest(mk, e.Id, "columnBars", style, text, laid.Spec.Mark, color, lines, dots, RebarLabels.AlongOf(lines), RebarLabels.PlacedOn(c.View, e.Id, "columnBars")));
            yield return mk;
        }
    }

    /// <summary>The schedule's entries: each column's bars under its type's mark, one entry per length (a hook's tail is as long as its face lets it be).</summary>
    public static IEnumerable<RebarSchedule.Entry> Entries(IModelView m)
    {
        var bases = new Bases(m);
        foreach (var e in m.OfClass(IfcClass.Column))
            if (Of(m, e, bases) is { } laid)
                foreach (var g in laid.Lengths.GroupBy(x => Math.Round(x, 4)))
                    yield return new RebarSchedule.Entry(laid.Spec.Mark, laid.Spec.Diameter, laid.Hooked ? "L bar" : "Straight bar", g.Count(), g.First(), Derived: true);
    }

    public static int HighestMark(IModelView m) => m.OfClass(IfcClass.ColumnType).Select(t => ColumnBarsSpec.Of(t)?.Mark ?? 0).DefaultIfEmpty(0).Max();

    /// <summary>A column type's bars set from a command: {id, enabled?, perFace?, diameter?, lap?, top? (auto, starter, hooks), mark?, label?: a style patch | null}.</summary>
    public static void Set(ICommandContext ctx, JsonObject a)
    {
        var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: IfcClass.ColumnType } t ? t : throw new CommandException("Longitudinal bars are set on a column type (a beam's are drawn by hand).");
        var now = ColumnBarsSpec.Of(type) ?? ColumnBarsSpec.Default();
        var enabled = a["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) ? en : now.Enabled;
        var perFace = (int)Math.Round(Json.Number(a["perFace"]) ?? now.PerFace);
        if (perFace is < 2 or > 20) throw new CommandException("A column has 2 to 20 bars on each face (its corners counted).");
        var d = Json.Number(a["diameter"]) ?? now.Diameter;
        if (d < 0.006 || d > 0.04) throw new CommandException("A bar is between 6 and 40 mm thick.");
        var lap = Json.Number(a["lap"]) ?? now.Lap;
        if (lap < 0 || lap > 5) throw new CommandException("The starter bars run 0 to 5 m past the top.");
        var mark = (int)(Json.Number(a["mark"]) ?? now.Mark);
        if (enabled && mark <= 0) mark = Rebars.NextMark(ctx.Model);
        var top = (string?)a["top"] ?? now.Top;
        if (!ColumnBarsSpec.Tops.Contains(top)) throw new CommandException("A column's bars end as starter bars, with hooks, or by the column above (auto).");
        var label = a.ContainsKey(LabelStyle.Key) ? LabelStyle.Patch(now.Label, a[LabelStyle.Key]) : now.Label;
        var attrs = type.CloneAttrs();
        attrs[ColumnBarsSpec.Key] = new ColumnBarsSpec(enabled, perFace, d, lap, mark, label, top).ToJson();
        ctx.Replace(type.With(attrs: attrs));
    }
}
