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
/// </summary>
public sealed record ColumnBarsSpec(bool Enabled, int PerFace, double Diameter, double Lap, int Mark)
{
    public const string Key = "bars";

    public static ColumnBarsSpec Default() => new(false, 3, 0.016, 1.0, 0);

    public static ColumnBarsSpec? Of(Entity type) => type.Attrs[Key] is JsonObject o ? From(o) : null;

    public static ColumnBarsSpec From(JsonObject o)
    {
        var d = Default();
        return new ColumnBarsSpec(o["enabled"] is JsonValue ev && ev.TryGetValue<bool>(out var en) && en, (int)Math.Max(2, Json.Number(o["perFace"]) ?? d.PerFace),
            Json.Number(o["diameter"]) ?? d.Diameter, Json.Number(o["lap"]) ?? d.Lap, (int)(Json.Number(o["mark"]) ?? 0));
    }

    public JsonObject ToJson() => new() { ["enabled"] = Enabled, ["perFace"] = PerFace, ["diameter"] = Diameter, ["lap"] = Lap, ["mark"] = Mark };
}

public static class ColumnBars
{
    /// <summary>A column's bars laid: each a line in space from the base to the top plus the lap; their length for the schedule.</summary>
    public sealed record Laid(Entity Element, ColumnBarsSpec Spec, List<Vec3[]> Bars, double Length);

    public static ColumnBarsSpec? SpecOf(IModelView m, Entity e) => m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } type ? ColumnBarsSpec.Of(type) : null;

    public static Laid? Of(IModelView m, Entity e)
    {
        if (e.Class != IfcClass.Column || SpecOf(m, e) is not { Enabled: true } spec) return null;
        var (section, _, height) = Columns.Spec(m, e);
        if (section.Kind != "rectangle" || height <= 1e-6) return null;
        var stirrups = TypeStirrups.SpecOf(m, e) ?? StirrupSpec.Default();
        var inset = stirrups.Cover + (stirrups.Enabled ? stirrups.Diameter : 0) + spec.Diameter / 2;
        var (hx, hy) = (section.Width / 2 - inset, section.Depth / 2 - inset);
        if (hx <= 0 || hy <= 0) return null;
        var at = e.Attrs["at"] is JsonArray q && q.Count >= 2 ? new Vec2(Json.Number(q[0]) ?? 0, Json.Number(q[1]) ?? 0) : default;
        var baseZ = (Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0) + e.Num("baseOffset");
        var rot = e.Num("rotation") * Math.PI / 180;
        var (u, v) = (new Vec2(Math.Cos(rot), Math.Sin(rot)), new Vec2(-Math.Sin(rot), Math.Cos(rot)));
        // Round the perimeter: the corners once, the rest of each face's bars between them.
        var n = spec.PerFace;
        var places = new List<Vec2>();
        foreach (var x in Rebar3D.Spread(n, -hx, hx)) places.Add(new Vec2(x, -hy));
        foreach (var y in Rebar3D.Spread(n, -hy, hy).Skip(1)) places.Add(new Vec2(hx, y));
        foreach (var x in Rebar3D.Spread(n, -hx, hx).Reverse<double>().Skip(1)) places.Add(new Vec2(x, hy));
        foreach (var y in Rebar3D.Spread(n, -hy, hy).Reverse<double>().Skip(1).Take(n - 2)) places.Add(new Vec2(-hx, y));
        var bars = places.Select(p => { var w = at + u * p.X + v * p.Y; return new[] { w.At(baseZ), w.At(baseZ + height + spec.Lap) }; }).ToList();
        return new Laid(e, spec, bars, height + spec.Lap);
    }

    /// <summary>The columns' bars as a view sees them: dots where they pierce it (a horizontal cut, a structural plan), lines up a vertical section; with the label and the mark's balloon.</summary>
    public static IEnumerable<ViewMark> Marks(ViewContext c, RebarSettings st)
    {
        var m = c.Model;
        if (!Rebar3D.Shows(c, out var limit, out var plan)) yield break;
        var hidden = (c.View.Attrs["hiddenLayers"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet() ?? [];
        if (hidden.Contains(RebarShapes.LongitudinalLayer)) yield break;
        var storey = c.View.Str("storeyId");
        foreach (var e in m.OfClass(IfcClass.Column))
        {
            if (Of(m, e) is not { } laid) continue;
            if (plan && Spatial.StoreyOf(m, e.Id)?.Id != storey) continue;
            var (dots, lines) = Rebar3D.Project(c, laid.Bars, limit, plan);
            if (dots.Count == 0 && lines.Count == 0) continue;
            var mk = new ViewMark(e.Id);
            var label = $"{laid.Bars.Count}{RebarShapes.Dia(laid.Spec.Diameter)} L={RebarShapes.Mm(laid.Length)}";
            Rebar3D.Draw(mk, c, st, dots, lines, laid.Spec.Diameter, RebarShapes.LongitudinalLayer, label, laid.Spec.Mark);
            yield return mk;
        }
    }

    public static IEnumerable<RebarSchedule.Entry> Entries(IModelView m)
    {
        foreach (var e in m.OfClass(IfcClass.Column))
            if (Of(m, e) is { } laid) yield return new RebarSchedule.Entry(laid.Spec.Mark, laid.Spec.Diameter, "Straight bar", laid.Bars.Count, laid.Length);
    }

    public static int HighestMark(IModelView m) => m.OfClass(IfcClass.ColumnType).Select(t => ColumnBarsSpec.Of(t)?.Mark ?? 0).DefaultIfEmpty(0).Max();

    /// <summary>A column type's bars set from a command: {id, enabled?, perFace?, diameter?, lap?, mark?}.</summary>
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
        if (enabled && mark <= 0) mark = Math.Max(Rebars.NextMark(ctx.Model), Math.Max(TypeStirrups.HighestMark(ctx.Model), HighestMark(ctx.Model)) + 1);
        var attrs = type.CloneAttrs();
        attrs[ColumnBarsSpec.Key] = new ColumnBarsSpec(enabled, perFace, d, lap, mark).ToJson();
        ctx.Replace(type.With(attrs: attrs));
    }
}
