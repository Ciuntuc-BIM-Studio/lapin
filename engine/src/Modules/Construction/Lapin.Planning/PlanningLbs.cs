using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// The location-based schedule's query, its view's settings and its sheet (<see cref="Lbs"/>): <c>planning.lbs</c> (read
/// only, derived on every ask), <c>planning.lbsView</c> (the view's settings, one undo step) and <c>planning.lbsSheet</c>
/// (the chart on a new sheet, drawn by the engine as <see cref="LbsSheet"/>).
/// </summary>
public sealed partial class PlanningModule
{
    static void RegisterLbs(IModuleRegistry r)
    {
        r.Class(new(Lbs.Class, "Location-based schedule", [], "#5b7a99", Kind: "resource", Template: false));
        r.Service<ISheetContent>(new LbsSheet());

        // {view?, level?, zoneField?, collapsed?} → the locations (a tree, rows from the foot), the segments, the groups, the tasks not placed.
        r.Query("lbs", (q, a) =>
        {
            var view = Lbs.ViewOf(q.Model, (string?)a["view"]);
            var s = Lbs.SettingsOf(view, a);
            return Lbs.ToJson(Lbs.Build(q, s), view, s);
        });

        // The view's settings changed (made when the project has none): the level, the zones' field, the levels folded, a label, a note.
        r.Command("lbsView", "Location-based schedule", (ctx, a) =>
        {
            var view = EnsureLbsView(ctx, (string?)a["id"]);
            var s = Lbs.Change(Lbs.Settings.Read(view.Attrs["lbs"] as JsonObject), a, ctx.NewId);
            var attrs = view.CloneAttrs();
            attrs["lbs"] = s.ToJson();
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : view.Name;
            ctx.Replace(view.With(name: name, attrs: attrs));
            return new JsonObject { ["id"] = view.Id };
        });

        // The chart on a new sheet (A3 landscape unless asked), the view as its content.
        r.Command("lbsSheet", "LBS on sheet", (ctx, a) =>
        {
            if (Plan.Read(ctx.Model).Rows.Count == 0) throw new CommandException("The schedule has no tasks to put on a sheet.");
            var view = EnsureLbsView(ctx, (string?)a["id"]);
            var paper = (string?)a["paper"] ?? "A3";
            var portrait = (string?)a["orientation"] == "portrait";
            var (fw, fh) = Frame(paper, portrait);
            var (w, h, _) = new LbsSheet().Pages(ctx.Query, ctx.Model.Get(view.Id)!, fw, fh)[0];
            var sheet = Sheets.AddSheet(ctx, view.Name, paper, portrait, [(view.Id, Sheets.Margin + 4 + w / 2, Sheets.Margin + 4 + h / 2, 1)]);
            return new JsonObject { ["sheetId"] = sheet.Id, ["id"] = view.Id };
        });
    }

    static Entity EnsureLbsView(ICommandContext ctx, string? id)
    {
        if (Lbs.ViewOf(ctx.Model, id) is { } v) return v;
        if (id != null) throw new CommandException("Not a location-based schedule.");
        var e = new Entity(ctx.NewId(), Lbs.Class, "Location-based schedule", new JsonObject { ["lbs"] = Lbs.Settings.Default.ToJson() });
        ctx.Add(e);
        return e;
    }
}

/// <summary>
/// The location-based schedule on a sheet: the locations up the left (the storeys' elevations beside them), the time scale
/// along the top (months and weeks, or years and months when the works are long), each task's lines and blocks in its
/// group's colour (the critical ones outlined in red), the status date dashed, the notes and the legend; so the PDF prints
/// what the pane shows.
/// </summary>
public sealed class LbsSheet : ISheetContent
{
    const double AxisW = 42, HeadH = 10, TitleH = 9, LegendH = 8, Text = 2.2;
    const string Critical = "#d64545", Grid = "#c8c8c8";

    public bool Handles(Entity e) => e.Class == Lbs.Class;

    static string Dmy(DateOnly d) => d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>A colour two thirds of the way to white (a block's fill under its outline).</summary>
    public static string Lighten(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#' || !int.TryParse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)) return "#dddddd";
        int L(int c) => c + (255 - c) * 2 / 3;
        return $"#{L(v >> 16 & 255):x2}{L(v >> 8 & 255):x2}{L(v & 255):x2}";
    }

    public IReadOnlyList<(double W, double H, List<Pen> Pens)> Pages(IQueryContext q, Entity e, double maxWidth, double? maxHeight)
    {
        var s = Lbs.SettingsOf(e, null);
        var c = Lbs.Build(q, s);
        var p = c.Plan;
        var width = Math.Max(160, maxWidth);
        var height = Math.Max(90, maxHeight ?? width * 0.6);
        var rows = c.Locations.Where(l => l.Row >= 0).OrderBy(l => l.Row).ToList();
        var chartW = width - AxisW;
        var bodyTop = TitleH + HeadH;
        var bodyH = height - bodyTop - LegendH;
        var axisH = Math.Max(1e-6, c.Height);
        double Y(double at) => bodyTop + bodyH * (1 - at / axisH);
        var finish = c.Result.Times.Count > 0 ? c.Result.Times.Values.Max(t => t.Finish) : p.Start;
        var from = p.Start.AddDays(-2);
        var to = finish.AddDays(4);
        var days = Math.Max(7, to.DayNumber - from.DayNumber);
        var k = chartW / days;
        double X(DateOnly d) => AxisW + (d.DayNumber - from.DayNumber) * k;

        var text = new Pen("TEXT", "#000000", 0, null, null);
        var rule = new Pen("LBS", "#000000", 0.25, null, null);
        var thin = new Pen("LBS-GRID", Grid, 0.1, null, null);
        var status = new Pen("LBS-STATUS", Critical, 0.25, null, [2, 1]);
        var critical = new Pen("LBS-CRITICAL", Critical, 0.5, null, null);
        var byColour = new Dictionary<string, (Pen Line, Pen Fill)>();
        (Pen Line, Pen Fill) Colour(string col) => byColour.TryGetValue(col, out var x) ? x : byColour[col] = (new Pen("LBS-TASK", col, 0.6, null, null), new Pen("LBS-BLOCK", col, 0.15, Lighten(col), null));

        text.Texts.Add(new PenText(new Vec2(0, 6), $"{p.Name}: location-based schedule", 5, Anchor: "start", Bold: true));
        text.Texts.Add(new PenText(new Vec2(width, 6), $"{Dmy(p.Calendar.NextWorking(p.Start))} – {Dmy(finish)}", 3, Anchor: "end"));
        // The frame, the time scale's two tiers.
        var bottom = bodyTop + bodyH;
        rule.Loops.Add([new(0, TitleH), new(width, TitleH), new(width, bottom), new(0, bottom)]);
        rule.Segments.Add((new Vec2(AxisW, TitleH), new Vec2(AxisW, bottom)));
        rule.Segments.Add((new Vec2(AxisW, TitleH + HeadH / 2), new Vec2(width, TitleH + HeadH / 2)));
        rule.Segments.Add((new Vec2(0, bodyTop), new Vec2(width, bodyTop)));
        var weeks = k * 7 >= 7;
        for (var d = weeks ? new DateOnly(from.Year, from.Month, 1) : new DateOnly(from.Year, 1, 1); d <= to; d = weeks ? d.AddMonths(1) : d.AddYears(1))
        {
            var xs = Math.Max(AxisW, X(d));
            if (xs > width) break;
            rule.Segments.Add((new Vec2(xs, TitleH), new Vec2(xs, TitleH + HeadH / 2)));
            var label = weeks ? d.ToString("MM.yyyy", CultureInfo.InvariantCulture) : d.ToString("yyyy", CultureInfo.InvariantCulture);
            if (width - xs > 12) text.Texts.Add(new PenText(new Vec2(xs + 1, TitleH + HeadH / 2 - 1.2), label, Text, Anchor: "start"));
        }
        var tick = weeks ? from.AddDays((8 - (int)from.DayOfWeek) % 7) : new DateOnly(from.Year, from.Month, 1).AddMonths(1);
        for (var d = tick; d <= to; d = weeks ? d.AddDays(7) : d.AddMonths(1))
        {
            var xs = X(d);
            if (xs > width) break;
            thin.Segments.Add((new Vec2(xs, TitleH + HeadH / 2), new Vec2(xs, bottom)));
            var label = weeks ? d.ToString("dd.MM", CultureInfo.InvariantCulture) : d.ToString("MM", CultureInfo.InvariantCulture);
            if ((weeks ? 7 : 30) * k >= label.Length * Text * 0.55 + 1) text.Texts.Add(new PenText(new Vec2(xs + 0.6, bodyTop - 1.2), label, Text * 0.85, Anchor: "start"));
        }
        // The locations: a band a row, its name (and a storey's elevation) on the axis.
        foreach (var l in rows)
        {
            var y = Y(l.At);
            thin.Segments.Add((new Vec2(AxisW, y), new Vec2(width, y)));
            rule.Segments.Add((new Vec2(0, y), new Vec2(AxisW, y)));
            var mid = Y(l.At + l.Extent / 2);
            var name = l.Name.Length > 22 ? l.Name[..21] + "…" : l.Name;
            text.Texts.Add(new PenText(new Vec2(1, mid + 0.8), name, Text, Anchor: "start", Bold: l.Folded));
            // A storey's elevation at its foot; a zone's first row shows its storey's.
            var parent = c.Locations.FirstOrDefault(x => x.Id == l.Parent);
            var elevation = l.Elevation ?? (parent?.Elevation is { } pe && l.At <= parent.At + 1e-6 ? pe : null);
            if (elevation is { } ev) text.Texts.Add(new PenText(new Vec2(AxisW - 1, y - 0.6), ev.ToString("+0.00;-0.00;±0.00", CultureInfo.InvariantCulture), Text * 0.8, Anchor: "end"));
        }
        // The segments: a line rising across its location's band, a block over a run of locations; a milestone a tick.
        foreach (var x in c.Segments)
        {
            var (line, fill) = Colour(x.Colour);
            var x0 = X(x.Start);
            var x1 = x.Rep == "milestone" ? x0 : X(x.Finish.AddDays(1));
            var (ya, yb) = (Y(x.Y0), Y(x.Y1));
            if (x.Rep == "block")
            {
                fill.Loops.Add([new(x0, ya), new(x1, ya), new(x1, yb), new(x0, yb)]);
                if (x.Critical) critical.Loops.Add([new(x0, ya), new(x1, ya), new(x1, yb), new(x0, yb)]);
            }
            else
            {
                line.Segments.Add((new Vec2(x0, ya), new Vec2(x1, yb)));
                if (x.Critical) critical.Segments.Add((new Vec2(x0, ya), new Vec2(x1, yb)));
            }
            var lab = s.Labels.GetValueOrDefault(x.Id);
            var at = new Vec2((x0 + x1) / 2 + (lab?.Dx ?? 0) * 0.25, (ya + yb) / 2 - 0.8 + (lab?.Dy ?? 0) * 0.25);
            var angle = lab?.Angle ?? (x.Rep == "line" && x1 > x0 ? Math.Atan2(ya - yb, x1 - x0) * 180 / Math.PI : 0);
            text.Texts.Add(new PenText(at, x.Group, Text * 0.8, Angle: angle));
        }
        if (p.DataDate is { } dd && X(dd.AddDays(1)) is var xd && xd > AxisW && xd < width) status.Segments.Add((new Vec2(xd, bodyTop), new Vec2(xd, bottom)));
        // The notes, at their date and location.
        foreach (var n in s.Notes)
            if (c.Locations.FirstOrDefault(l => l.Id == n.Location) is { } nl)
                text.Texts.Add(new PenText(new Vec2(X(n.Date), Y(nl.At + nl.Extent / 2)), n.Text, Text, Anchor: "start", Italic: true));
        // The legend: a swatch and a name a group.
        var lx = 0.0;
        foreach (var g in c.Groups)
        {
            var w = 8 + g.Key.Length * Text * 0.55;
            if (lx + w > width) break;
            var (_, f) = Colour(g.Colour);
            var ly = bottom + 2.5;
            f.Loops.Add([new(lx, ly), new(lx + 4, ly), new(lx + 4, ly + 3), new(lx, ly + 3)]);
            text.Texts.Add(new PenText(new Vec2(lx + 5, ly + 2.8), g.Key, Text, Anchor: "start"));
            lx += w;
        }
        List<Pen> pens = [thin, rule, .. byColour.Values.Select(v => v.Fill), .. byColour.Values.Select(v => v.Line), critical, status, text];
        return [(width, bottom + LegendH, pens)];
    }
}
