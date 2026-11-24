using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A view printed on its own, with no sheet (forum topic 225, publish sets): one page as large as the drawing at the
/// view's scale with a margin round it and the view's name and scale under it. The drawing is the one a sheet places
/// (<see cref="Views.Render"/> for print, turned as the view shows, cropped to its crop region), clipped and placed by
/// the sheets' own <see cref="Sheets.ClipToBox"/>, so a view prints here as it prints on a sheet.
/// </summary>
public static class ViewPages
{
    /// <summary>The white round the drawing (mm).</summary>
    public const double Margin = 10;
    /// <summary>The band under the drawing that holds its name and scale (mm).</summary>
    public const double TitleBand = 8;
    const double TitleHeight = 3.5;

    public static PaperPage Page(IQueryContext q, Entity view)
    {
        var r = Views.Render(q, view, print: true);
        var k = 1000 / r.Scale;
        var turn = Views.Turn(q.Model, view);
        Vec2 T(Vec2 p) => turn == 0 ? p : Turning.Turn(p, turn);
        // The box the drawing takes, in its own coordinates as it shows (turned): its crop region when it has one, as on a sheet.
        var box = turn == 0 ? r.Bounds : Turning.Bounds(r.Pens, turn);
        if (ViewCrops.ActiveOf(q.Model, view, r.Scale) is { } crop)
        {
            var (lo, hi) = crop.Annotations ?? crop.Model;
            if (crop.Annotations == null && Turning.Bounds(r.Pens, turn) is { } drawn)
                (lo, hi) = (new Vec2(Math.Min(lo.X, drawn.Min.X), Math.Min(lo.Y, drawn.Min.Y)), new Vec2(Math.Max(hi.X, drawn.Max.X), Math.Max(hi.Y, drawn.Max.Y)));
            box = (lo, hi);
        }
        if (box is not { } b || r.Pens.Count == 0) throw new CommandException($"“{view.Name}” draws nothing to print.");
        var w = Math.Max((b.Max.X - b.Min.X) * k, 1);
        var h = Math.Max((b.Max.Y - b.Min.Y) * k, 1);
        var (W, H) = (w + 2 * Margin, h + 2 * Margin + TitleBand);
        // Paper y runs down, as a sheet's.
        Vec2 ToPaper(Vec2 p) { var t = T(p); return new(Margin + (t.X - b.Min.X) * k, Margin + (b.Max.Y - t.Y) * k); }
        const double eps = 1e-6;
        var pens = r.Pens.Select(p => Sheets.ClipToBox(p, ToPaper, Margin - eps, Margin - eps, Margin + w + eps, Margin + h + eps, null, turn * 180 / Math.PI, r.Scale / 1000))
            .OfType<Pen>().ToList();
        var title = new Pen("TITLE", "#000000", 0, null, null);
        title.Texts.Add(new PenText(new Vec2(Margin, Margin + h + TitleBand - 1), $"{view.Name}  1:{r.Scale:0}", TitleHeight, Anchor: "start", Bold: true));
        pens.Add(title);
        return new PaperPage(W, H, pens, view.Name);
    }

    /// <summary>The views as one PDF, a page each.</summary>
    public static byte[] Pdf(IQueryContext q, IEnumerable<Entity> views) => PdfWriter.Write(views.Select(v => Page(q, v)).ToList(), Sheets.ImageOf(q));
}
