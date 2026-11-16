using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A sheet's paper as a view (audit §6, decision 620): <c>paper:&lt;sheetId&gt;</c>, a view of kind <c>paper</c> at 1:1 whose
/// drawing coordinates are the paper's, in metres from its lower-left corner, y up (paper mm / 1000), so the Draft tools,
/// texts, symbols, hatches, dimensions and pictures drawn on it are what they are on any view, a typed length in mm is
/// paper mm, and a text's height is its height on paper. Nothing of the model is drawn on it: on the screen it shows the
/// sheet as it prints under what is drawn (the frame, the title block, the views, the views' boxes to snap to); the sheet,
/// its PDF, Print and DXF draw what is on it over the rest. What is on it is the sheet's: deleted with it, copied with
/// "Duplicate sheet". A project from before has nothing on any paper, so its sheets print as they did.
/// </summary>
public static class PaperViews
{
    public const string Kind = "paper";
    public const string Prefix = "paper:";
    /// <summary>The views' boxes under the paper's drawing, on the screen only (a snap to a view's frame).</summary>
    public const string ViewportLayer = "VIEWPORT";
    /// <summary>The paper's edge, on the screen only.</summary>
    public const string PaperLayer = "PAPER";

    public static string IdOf(string sheetId) => Prefix + sheetId;

    public static string? SheetIdOf(string? viewId) =>
        viewId != null && viewId.StartsWith(Prefix, StringComparison.Ordinal) ? viewId[Prefix.Length..] : null;

    public static bool Is(Entity? view) => view?.Str("kind") == Kind;

    /// <summary>The paper of a sheet as a view, by its id (<c>paper:&lt;sheetId&gt;</c>); null when the sheet is not there.</summary>
    public static Entity? Find(IModelView m, string id) =>
        SheetIdOf(id) is { } s && m.Get(s) is { Class: Sheets.Class } sheet
            ? new Entity(id, Views.Class, sheet.Name, new JsonObject { ["kind"] = Kind, ["sheetId"] = sheet.Id, ["scale"] = 1 })
            : null;

    public static Entity? SheetOf(IModelView m, Entity view) => m.Get(view.Str("sheetId") ?? SheetIdOf(view.Id) ?? "") is { Class: Sheets.Class } s ? s : null;

    /// <summary>A point of the paper's view (m, y up from the lower-left corner) on the paper (mm, y down from the top-left).</summary>
    public static Vec2 ToPaper(Vec2 p, double paperHeight) => new(p.X * 1000, paperHeight - p.Y * 1000);

    /// <summary>A point on the paper (mm, y down) in the paper's view (m, y up).</summary>
    public static Vec2 ToView(Vec2 p, double paperHeight) => new(p.X / 1000, (paperHeight - p.Y) / 1000);

    /// <summary>A pen with its points mapped (widths, dashes and text heights are paper mm either way; a text's angle reads the same).</summary>
    public static Pen Map(Pen pen, Func<Vec2, Vec2> f, (double X0, double Y0, double X1, double Y1)? clip = null)
    {
        var o = new Pen(pen.Layer, pen.Stroke, pen.Width, pen.Fill, pen.Dash) { Viewport = pen.Viewport, Block = pen.Block, Clip = clip };
        o.Segments.AddRange(pen.Segments.Select(s => (f(s.A), f(s.B))));
        o.Curves.AddRange(pen.Curves.Select(c => c.Map(f)));
        o.Loops.AddRange(pen.Loops.Select(l => (IReadOnlyList<Vec2>)l.Select(f).ToList()));
        o.Texts.AddRange(pen.Texts.Select(t => t with { At = f(t.At) }));
        o.Images.AddRange(pen.Images.Select(i => i with { Corners = [.. i.Corners.Select(f)] }));
        return o;
    }

    /// <summary>A clip box mapped by <paramref name="f"/> (its corners, kept in order).</summary>
    static (double, double, double, double)? MapClip((double X0, double Y0, double X1, double Y1)? c, Func<Vec2, Vec2> f)
    {
        if (c is not { } b) return null;
        var (a, z) = (f(new Vec2(b.X0, b.Y0)), f(new Vec2(b.X1, b.Y1)));
        return (Math.Min(a.X, z.X), Math.Min(a.Y, z.Y), Math.Max(a.X, z.X), Math.Max(a.Y, z.Y));
    }

    /// <summary>Whether anything is drawn on a sheet's paper (a sheet with nothing on it is composed as it always was).</summary>
    public static bool Any(IModelView m, string sheetId)
    {
        var id = IdOf(sheetId);
        return m.OfClass(IfcClass.Annotation).Any(e => e.Str("viewId") == id) || m.OfClass(ViewImageClass).Any(e => e.Str("viewId") == id);
    }

    /// <summary>The class of a picture laid on a view (the drafting module's <c>LapinViewImage</c>).</summary>
    public const string ViewImageClass = "LapinViewImage";

    /// <summary>What is drawn on a sheet's paper: every entity on its view, and its symbols placed by paper mm (decision 591).</summary>
    public static IEnumerable<Entity> Owned(IModelView m, string sheetId)
    {
        var id = IdOf(sheetId);
        return m.OfClass(IfcClass.Annotation).Where(e => e.Str("viewId") == id || (e.PredefinedType == Symbols.Predefined && e.Str("sheetId") == sheetId))
            .Concat(m.OfClass(ViewImageClass).Where(e => e.Str("viewId") == id));
    }

    // ------------------------------------------------------------ the views placed, once per composition

    static readonly AsyncLocal<(string SheetId, IReadOnlyList<Sheets.Placement> Placements)?> Current = new();

    /// <summary>The sheet's views placed: those of the composition under way (so a legend does not draw them again), else placed now.</summary>
    public static IReadOnlyList<Sheets.Placement> Placements(IQueryContext q, Entity sheet) =>
        Current.Value is { } c && c.SheetId == sheet.Id ? c.Placements : Sheets.Place(q, sheet);

    static IReadOnlyList<Sheets.Placement>? Known(Entity sheet) => Current.Value is { } c && c.SheetId == sheet.Id ? c.Placements : null;

    /// <summary>Whether the views of <paramref name="sheet"/> are placed for the composition under way.</summary>
    internal static bool Placing(Entity sheet) => Known(sheet) != null;

    internal static T With<T>(Entity sheet, IReadOnlyList<Sheets.Placement> placements, Func<T> f)
    {
        var old = Current.Value;
        Current.Value = (sheet.Id, placements);
        try { return f(); }
        finally { Current.Value = old; }
    }

    /// <summary>What a token of a text on the paper says: the sheet's, as its title block reads them (<c>{sheet.number}</c>, <c>{project.name}</c>).</summary>
    public static Func<string, (bool Known, string? Value)>? Tokens(IQueryContext q, Entity view) =>
        SheetOf(q.Model, view) is { } sheet ? TitleBlocks.Resolver(q, sheet, Known(sheet)) : null;

    /// <summary>A text on the paper with its tokens filled (an unknown one as <c>&lt;key&gt;</c>); as it is anywhere else.</summary>
    public static string Fill(ViewContext c, string text)
    {
        if (!Is(c.View) || c.Query == null || !text.Contains('{')) return text;
        return Tokens(c.Query, c.View) is { } resolve ? TitleBlocks.Fill(text, resolve) : text;
    }

    // ------------------------------------------------------------ drawing

    /// <summary>
    /// What is drawn on the paper (the modules' annotations whose view it is, and the legends and scale bars), as marks; a
    /// module's mark of anything else (the model's own symbols, which never belong on paper) is left out.
    /// </summary>
    internal static List<ViewMark> Marks(IQueryContext q, ViewContext c)
    {
        var m = q.Model;
        bool Mine(ViewMark mk) => mk.Owner != null && m.Get(mk.Owner) is { } e && e.Str("viewId") == c.View.Id;
        var o = new List<ViewMark>();
        foreach (var source in q.Services.All<IViewAnnotations>())
            try { o.AddRange(source.Marks(c).Where(Mine)); }
            catch (Exception) { /* a module that cannot draw on paper draws nothing there */ }
        o.AddRange(DrawingKeys.Marks(c));
        return o;
    }

    /// <summary>The sheet under what is drawn on its paper, on the screen: as it prints, the views' boxes and the paper's edge (m, y up).</summary>
    internal static List<Pen> Underlay(IQueryContext q, Entity view)
    {
        if (SheetOf(q.Model, view) is not { } sheet) return [];
        var (W, H) = Sheets.PaperOf(sheet);
        var placements = PaperViews.Placements(q, sheet).ToList();
        Vec2 F(Vec2 p) => ToView(p, H);
        var page = Sheets.Compose(q, sheet, placements, annotations: false);
        var pens = page.Pens.Select(p => Map(p, F, MapClip(p.Clip, F))).ToList();
        var boxes = new Pen(ViewportLayer, "#6b9bd1", 0.13, null, [2, 1.5]);
        foreach (var pl in placements)
        {
            var (a, b, cc, d) = (F(new Vec2(pl.X0, pl.Y0)), F(new Vec2(pl.X1, pl.Y0)), F(new Vec2(pl.X1, pl.Y1)), F(new Vec2(pl.X0, pl.Y1)));
            boxes.Segments.AddRange([(a, b), (b, cc), (cc, d), (d, a)]);
        }
        var edge = new Pen(PaperLayer, "#9aa3ad", 0.1, null, null);
        var (p0, p1, p2, p3) = (new Vec2(0, 0), new Vec2(W / 1000, 0), new Vec2(W / 1000, H / 1000), new Vec2(0, H / 1000));
        edge.Segments.AddRange([(p0, p1), (p1, p2), (p2, p3), (p3, p0)]);
        if (boxes.Segments.Count > 0) pens.Add(boxes);
        pens.Add(edge);
        return pens;
    }

    /// <summary>The paper's whole extent in its view (m): what the screen fits.</summary>
    internal static (Vec2, Vec2)? Bounds(IModelView m, Entity view) =>
        SheetOf(m, view) is { } sheet && Sheets.PaperOf(sheet) is var (W, H) ? (new Vec2(0, 0), new Vec2(W / 1000, H / 1000)) : null;

    /// <summary>What a sheet prints of what is drawn on its paper, in paper mm (y down): none when nothing is (the sheet as it was).</summary>
    public static List<Pen> PrintPens(IQueryContext q, Entity sheet, IReadOnlyList<Sheets.Placement> placements)
    {
        if (!Any(q.Model, sheet.Id) || Find(q.Model, IdOf(sheet.Id)) is not { } view) return [];
        var (_, H) = Sheets.PaperOf(sheet);
        var r = With(sheet, placements, () => Views.Render(q, view, print: true));
        Vec2 F(Vec2 p) => ToPaper(p, H);
        return [.. r.Pens.Select(p => Map(p, F, MapClip(p.Clip, F)))];
    }

    // ------------------------------------------------------------ the sheet's own

    /// <summary>A sheet duplicated: what is drawn on its paper and its symbols, copied onto the copy's (new ids, the same places).</summary>
    public static int Copy(ICommandContext ctx, string fromSheet, string toSheet)
    {
        var n = 0;
        foreach (var e in Owned(ctx.Model, fromSheet).ToList())
        {
            var attrs = e.CloneAttrs();
            if (attrs["viewId"] != null) attrs["viewId"] = IdOf(toSheet);
            if (attrs["sheetId"] != null) attrs["sheetId"] = toSheet;
            attrs.Remove(Pins.Key);
            ctx.Add(new Entity(ctx.NewId(), e.Class, e.Name, attrs, e.PredefinedType));
            n++;
        }
        return n;
    }

    /// <summary>A sheet deleted takes what is drawn on its paper and its symbols with it: they are drawn nowhere else.</summary>
    internal sealed class Deletion : IDeletion
    {
        public bool Delete(ICommandContext ctx, string id)
        {
            if (ctx.Model.Get(id) is not { Class: Sheets.Class }) return false;
            foreach (var e in Owned(ctx.Model, id).Select(e => e.Id).ToList())
                if (ctx.Model.Get(e) != null) ctx.Remove(e);
            return false;
        }
    }

    public static void Register(IModuleRegistry r) => r.Service<IDeletion>(new Deletion());
}
