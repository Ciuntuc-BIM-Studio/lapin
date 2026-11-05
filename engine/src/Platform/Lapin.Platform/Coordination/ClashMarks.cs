using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Coordination;

/// <summary>
/// The clash report's open problems marked in plans, sections and elevations (decision 293): a red cloud round each
/// one's place with its number, drawn by the engine with the view's annotations, so a click selects its MEP element
/// (or its host). Only in a view that asks (<see cref="Attr"/>, View → Clash marks, or the check's dialog); on the
/// screen only, never on a sheet, a PDF or a DXF unless the view asks for that too (<see cref="PrintAttr"/>).
/// A plan marks those of the storeys it shows; a section or an elevation those in front of it, within its depth.
/// </summary>
public static class ClashMarks
{
    /// <summary>
    /// A clash another module keeps (the clash module's tests, docs/plan-visualisation.md): where, the element a click on its
    /// cloud selects, the element whose storey a plan asks about, the label in the cloud ("2.14": the test's number and the clash's).
    /// </summary>
    public sealed record Item(Vec3 Centre, string SelectId, string? StoreyOf, string Label, System.Text.Json.Nodes.JsonObject? Data = null);

    /// <summary>A module's open clashes, drawn by the same clouds as the openings' report.</summary>
    public interface ISource
    {
        IEnumerable<Item> Items(IQueryContext q);
    }

    public const string Attr = "clashMarks", PrintAttr = "clashMarksPrint";
    public const string Layer = "CLASHES";
    public const string Color = "#e03131";
    /// <summary>The cloud's radius and the number's height, paper millimetres.</summary>
    const double Radius = 6, TextHeight = 3.2;

    /// <summary>Whether a view draws the marks: asked for, and for print only when asked for print too.</summary>
    public static bool Shows(Entity view, bool print) => view.Bool(Attr) && (!print || view.Bool(PrintAttr));

    public static List<ViewMark> Marks(IQueryContext q, ViewContext c, ViewSpec spec, IReadOnlyList<(ViewSpec Spec, double Station)> pieces)
    {
        var o = new List<ViewMark>();
        var m = q.Model;
        var items = new List<Item>();
        if (ClashReports.Read(m) is { } report)
            foreach (var i in report.Items.Where(i => i.State == ClashReports.Open))
                items.Add(new Item(i.Centre, i.MepId ?? i.HostId, i.HostId, i.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    new System.Text.Json.Nodes.JsonObject { ["clash"] = i.Number, ["key"] = i.Key }));
        foreach (var source in q.Services.All<ISource>())
            try { items.AddRange(source.Items(q)); }
            catch (CommandException) { }
        if (items.Count == 0) return o;
        var display = Views.StoreyDisplay.Of(c.View);
        var storey = c.View.Str("storeyId");
        var list = pieces.Count > 1 ? pieces : [(spec, 0.0)];
        foreach (var item in items)
        {
            if (spec.Kind == "plan")
            {
                var hostStorey = item.StoreyOf is { } sid && m.Get(sid) is { } h ? Spatial.StoreyOf(m, h.Id)?.Id : null;
                if (display.Shows(storey, hostStorey) == null) continue;
            }
            foreach (var (s, station) in list)
            {
                var f = s.Frame;
                var at = f.Map(item.Centre);
                if (spec.Kind != "plan")
                {
                    var depth = f.Depth(item.Centre);
                    if (depth < -0.5 || depth > s.Below + 0.5) continue;
                    if (s.Span is { } sp && (at.X < sp.Min || at.X > sp.Max)) continue;
                }
                o.Add(Cloud(c, item, at + new Vec2(station, 0)));
            }
        }
        return o;
    }

    /// <summary>A revision cloud: arcs bulging out round a circle, the number in its middle.</summary>
    static ViewMark Cloud(ViewContext c, Item item, Vec2 at)
    {
        var mark = new ViewMark(item.SelectId) { Data = item.Data };
        var r = Radius * c.Mm;
        const int bumps = 10, steps = 6;
        var pts = new List<Vec2>();
        for (var k = 0; k < bumps; k++)
        {
            double a0 = 2 * Math.PI * k / bumps, a1 = 2 * Math.PI * (k + 1) / bumps;
            Vec2 p0 = at + new Vec2(Math.Cos(a0), Math.Sin(a0)) * r, p1 = at + new Vec2(Math.Cos(a1), Math.Sin(a1)) * r;
            var mid = (p0 + p1) * 0.5;
            var chord = (p1 - p0).Length;
            var outward = (mid - at).Normalized();
            // A half circle on the chord, bulging outward.
            for (var i = 0; i <= steps; i++)
            {
                var t = Math.PI * i / steps;
                var along = (p0 - mid).Normalized();
                pts.Add(mid + along * (Math.Cos(t) * chord / 2) + outward * (Math.Sin(t) * chord / 2 * 0.8));
            }
        }
        mark.Polyline(pts, true, Layer, 0.35, Color);
        mark.Text(new PenText(at - new Vec2(0, TextHeight * c.Mm / 2), item.Label, TextHeight, 0, "middle", "bottom", true), Color);
        mark.Snaps.Add(at);
        return mark;
    }
}
