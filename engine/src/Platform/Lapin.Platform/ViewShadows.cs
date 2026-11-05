using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A view's sun shadows (docs/plan-visualisation.md, vector shadows), free: elevations, sections and site plans draw the
/// shadows the sun casts on the faces they see, as a filled area under their lines (<see cref="SunShadows"/>), so they
/// print on sheets and in PDF and DXF. Each view says where its sun is, as Archicad's elevations do:
/// <list type="bullet">
/// <item><b>none</b> (the default);</item>
/// <item><b>project</b>: the sun of the project's place on the view's own day and hour (<see cref="Sun"/>);</item>
/// <item><b>view</b>: a sun of the view's own, turned from the way the view looks (<c>shadowAzimuth</c>, degrees, 0 the sun behind the
/// viewer, positive to the left; <c>shadowAltitude</c>). The default is the drawing convention: the sun over the left shoulder, its
/// rays along a cube's diagonal (45°, 35.26°), so a shadow is as deep as what casts it stands out. A plan's default comes from the
/// sheet's top left (135°).</item>
/// </list>
/// A section casts only what lies beyond its cut; a storey plan draws none (its cut walls would cast short shadows).
/// </summary>
public static class ViewShadows
{
    public const string Attr = "shadows";
    public const string None = "none", Project = "project", View = "view";
    public static readonly string[] Modes = [None, Project, View];
    public const string Layer = "SHADOWS";
    public const string DefaultColor = "#cfcac0";
    /// <summary>The convention's altitude: the rays along a cube's diagonal (atan(1/√2)).</summary>
    public static readonly double ConventionAltitude = Math.Atan(1 / Math.Sqrt(2)) * 180 / Math.PI;

    public static string ModeOf(Entity view) => view.Str(Attr) is { } m && Modes.Contains(m) ? m : None;

    /// <summary>Whether a view of this kind may draw shadows: an elevation, a section, a site plan.</summary>
    public static bool Allowed(Entity view) => view.Str("kind") is "elevation" or "section" || SitePlans.Of(view) != null;

    /// <summary>The direction towards the view's sun (world, unit), or null when it draws none or the sun is down.</summary>
    public static Vec3? SunOf(IModelView m, Entity view, ViewFrame frame)
    {
        var mode = ModeOf(view);
        if (mode == None || !Allowed(view)) return null;
        if (mode == Project)
        {
            var date = DateOnly.TryParseExact(view.Str("shadowDate") ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : new DateOnly(DateTime.UtcNow.Year, 3, 21);
            var hour = view.NumOrNull("shadowHour") ?? 14;
            var (lon, lat, _) = Sun.Place(m);
            var pos = Sun.At(Sun.Utc(date, hour, Sun.ZoneOf(lon), Sun.InEurope(lon, lat)), lat, lon);
            return pos.Up ? Sun.Direction(m, pos) : null;
        }
        var plan = Math.Abs(frame.Dir.Z) > 0.9;
        var az = (view.NumOrNull("shadowAzimuth") ?? (plan ? 135 : 45)) * Math.PI / 180;
        var alt = Math.Clamp(view.NumOrNull("shadowAltitude") ?? ConventionAltitude, 1, 89) * Math.PI / 180;
        // The way the view looks, level, and its right: a plan's from the sheet's up.
        var forward = plan ? new Vec3(frame.Up.X, frame.Up.Y, 0).Normalized() : new Vec3(frame.Dir.X, frame.Dir.Y, 0).Normalized();
        var right = new Vec3(frame.Right.X, frame.Right.Y, 0).Normalized();
        if (forward == Vec3.Zero || right == Vec3.Zero) return null;
        var level = forward * (-Math.Cos(az)) + right * (-Math.Sin(az));
        return (level * Math.Cos(alt) + Vec3.UnitZ * Math.Sin(alt)).Normalized();
    }

    /// <summary>The view's shadows as one filled pen (under everything it draws), or null when it draws none.</summary>
    public static Pen? PenOf(IModelView m, Entity view, ViewSpec spec, IReadOnlyList<DrawPart> parts, bool pieces)
    {
        if (pieces || SunOf(m, view, spec.Frame) is not { } sun) return null;
        var plan = spec.Kind == "plan";
        var solids = parts.Select((p, i) => (p.Solid, i)).ToList();
        var areas = SunShadows.Draw(solids, spec.Frame, sun, cut: spec.Cut && !plan, depth: spec.Cut && !plan ? spec.Below : double.PositiveInfinity);
        if (areas.Count == 0) return null;
        var color = view.Str("shadowColor") is { Length: 7 } c && c[0] == '#' ? c : DefaultColor;
        var pen = new Pen(Layer, null, 0, color, null);
        foreach (var a in areas) pen.Loops.Add(a.Outline);
        return pen;
    }

    public static void Register(IModuleRegistry r)
    {
        // A view's sun shadows: {id, mode: none | project | view, azimuth?, altitude?, date?, hour?, color?}; what is not given stays.
        r.Command("view.setShadows", "Sun shadows", (ctx, a) =>
        {
            var view = Views.Find(ctx.Model, (string?)a["id"] ?? "") ?? throw new CommandException("There is no such view.");
            if (!Allowed(view)) throw new CommandException("Shadows are drawn in elevations, sections and site plans.");
            var attrs = view.CloneAttrs();
            if ((string?)a["mode"] is { } mode)
            {
                if (!Modes.Contains(mode)) throw new CommandException($"A view's shadows are {string.Join(", ", Modes)}.");
                if (mode == None) attrs.Remove(Attr); else attrs[Attr] = mode;
            }
            if (Json.Number(a["azimuth"]) is { } az) attrs["shadowAzimuth"] = Math.Round(((az % 360) + 540) % 360 - 180, 3);
            if (Json.Number(a["altitude"]) is { } alt) attrs["shadowAltitude"] = alt is > 0 and < 90 ? alt : throw new CommandException("The sun's altitude is above 0° and below 90°.");
            if ((string?)a["date"] is { } date)
                attrs["shadowDate"] = DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? date : throw new CommandException("The day is written YYYY-MM-DD.");
            if (Json.Number(a["hour"]) is { } hour) attrs["shadowHour"] = hour is >= 0 and < 24 ? hour : throw new CommandException("The hour is 0 to 24.");
            if ((string?)a["color"] is { } color) attrs["shadowColor"] = color is { Length: 7 } && color[0] == '#' ? color : throw new CommandException("A colour is written #rrggbb.");
            if (a.ContainsKey("reset")) { attrs.Remove("shadowAzimuth"); attrs.Remove("shadowAltitude"); }
            ctx.Replace(view.With(attrs: attrs));
            return null;
        });
    }
}
