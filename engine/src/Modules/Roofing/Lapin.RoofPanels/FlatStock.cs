using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.RoofPanels;

/// <summary>
/// The flashings cut from flat sheet (docs/plan-roof-panels.md §2.4, stage S5), the Panotaj Bilka app's flat_stock rewritten:
/// each piece a whole rectangle (a budget envelope: ridge nodes, ridge–valley heads, a standing seam's hip end and verge caps,
/// odd gutter corners, strips for valley clips) laid on stock sheets (2000 × 1250 mm) with a margin round the sheet and a gap
/// between pieces, never across two sheets; the sheets counted for every combination of ridge cap and rainwater system, the
/// roof's own priced. Worked out on every ask, never stored.
/// </summary>
public static class FlatStock
{
    /// <summary>A size of a blank: <see cref="Base"/> mm plus so many times the ridge cap's width and the gutter's size.</summary>
    public sealed record Size(int Base, int Ridge, int Gutter)
    {
        public int Of(int ridge, int gutter) => Base + Ridge * ridge + Gutter * gutter;
        public static Size Read(JsonNode? n) => n is JsonObject o
            ? new((int)(Json.Number(o["base"]) ?? 0), (int)(Json.Number(o["ridge"]) ?? 0), (int)(Json.Number(o["gutter"]) ?? 0))
            : new((int)(Json.Number(n) ?? 0), 0, 0);
    }

    /// <summary>
    /// A kind of blank, data: its id, its pieces' code prefix and name (<c>{g}</c> the gutter's size, <c>{angle}</c> a corner's),
    /// what counts it (a takeoff stat, or <c>angles</c>: one a corner), its size; <see cref="Per"/> pieces of <see cref="Each"/> mm
    /// share a strip (valley clips).
    /// </summary>
    public sealed record BlankDef(string Id, string Prefix, string Name, string Of, Size Width, Size Height, int Per, int Each);

    /// <summary>The stock sheet (mm), its margin and the gap between pieces, and the kinds of blank (profiles.json, <c>common.flatSheet</c>).</summary>
    public sealed record Data(int Width, int Height, int Margin, int Gap, IReadOnlyList<BlankDef> Blanks, string Note)
    {
        public static Data Read(JsonObject o) => new(
            (int)(Json.Number(o["width"]) ?? 2000), (int)(Json.Number(o["height"]) ?? 1250), (int)(Json.Number(o["margin"]) ?? 10), (int)(Json.Number(o["gap"]) ?? 5),
            [.. ((o["blanks"] as JsonArray) ?? []).OfType<JsonObject>().Select(b => new BlankDef((string?)b["id"] ?? "", (string?)b["prefix"] ?? "", (string?)b["name"] ?? "",
                (string?)b["of"] ?? "", Size.Read(b["width"]), Size.Read(b["height"]), (int)(Json.Number(b["per"]) ?? 0), (int)(Json.Number(b["each"]) ?? 0)))],
            (string?)o["note"] ?? "");
    }

    /// <summary>A piece to cut: its code (NC-01), name, width and height (mm).</summary>
    public sealed record Blank(string Id, string Label, int W, int H);

    /// <summary>A piece laid on a sheet: the sheet (from 1), where (mm from the sheet's corner), its size as laid (turned or not), its own size.</summary>
    public sealed record Placed(string Id, string Label, int Sheet, int X, int Y, int W, int H, int BlankW, int BlankH);

    /// <summary>The pieces laid: how many sheets, each piece's place, the pieces no sheet holds whole.</summary>
    public sealed record Nesting(int Sheets, List<Placed> Pieces, List<Blank> TooBig);

    /// <summary>A combination's sheets: the ridge cap option (null: the profile has one), the rainwater system, the pieces laid.</summary>
    public sealed record Combination(string? Ridge, string System, Nesting Nesting);

    /// <summary>The roof's flat sheet: the profile's sheet article, the stock, every combination and the roof's own (its ridge cap and system).</summary>
    public sealed record Result(FlatSheet Sheet, Data Data, List<Combination> Combinations, Combination Current);

    /// <summary>
    /// Lays the pieces (flat_stock.nest): each in the free rectangle of an open sheet it leaves least room in (the shorter side's
    /// leftover, then the area's, then the earliest sheet, lowest, leftmost), turned when it fits better, else on a new sheet; the
    /// free rectangles split round each piece; tried with the pieces largest first by area, by longer and by shorter side, the
    /// fewest sheets kept.
    /// </summary>
    public static Nesting Nest(IReadOnlyList<Blank> blanks, Data d)
    {
        var (edge, gap) = (d.Margin, d.Gap);
        Nesting Place(IEnumerable<Blank> ordered)
        {
            var sheets = new List<List<(int X, int Y, int W, int H)>>();
            var placed = new List<Placed>();
            var tooBig = new List<Blank>();
            foreach (var b in ordered)
            {
                var turns = b.W == b.H ? new[] { (b.W, b.H) } : [(b.W, b.H), (b.H, b.W)];
                (int, long, int, int, int, int, int)? best = null;
                void Try((int, long, int, int, int, int, int) c) { if (best == null || c.CompareTo(best.Value) < 0) best = c; }
                for (var i = 0; i < sheets.Count; i++)
                    foreach (var (x, y, w, h) in sheets[i])
                        foreach (var (bw, bh) in turns)
                            if (bw + gap <= w && bh + gap <= h) Try((Math.Min(w - bw, h - bh), (long)w * h - (long)bw * bh, i, y, x, bw, bh));
                if (best == null)
                {
                    var (sw, sh) = (d.Width - 2 * edge + gap, d.Height - 2 * edge + gap);
                    foreach (var (bw, bh) in turns)
                        if (bw + gap <= sw && bh + gap <= sh) Try((Math.Min(sw - bw, sh - bh), (long)sw * sh - (long)bw * bh, sheets.Count, edge, edge, bw, bh));
                    if (best == null) { tooBig.Add(b); continue; }
                    sheets.Add([(edge, edge, sw, sh)]);
                }
                var (_, _, si, py, px, pw, ph) = best.Value;
                var (rw, rh) = (px + pw + gap, py + ph + gap);
                var free = new HashSet<(int X, int Y, int W, int H)>();
                foreach (var (fx, fy, fw, fh) in sheets[si])
                {
                    var (fr, ft) = (fx + fw, fy + fh);
                    if (px >= fr || py >= ft || rw <= fx || rh <= fy) { free.Add((fx, fy, fw, fh)); continue; }
                    foreach (var r in new[] { (fx, fy, px - fx, fh), (rw, fy, fr - rw, fh), (fx, fy, fw, py - fy), (fx, rh, fw, ft - rh) })
                        if (Math.Min(r.Item3, r.Item4) > 0) free.Add(r);
                }
                sheets[si] = [.. free.Where(a => !free.Any(o => o != a && a.X >= o.X && a.Y >= o.Y && a.X + a.W <= o.X + o.W && a.Y + a.H <= o.Y + o.H)).Order()];
                placed.Add(new Placed(b.Id, b.Label, si + 1, px, py, pw, ph, b.W, b.H));
            }
            return new Nesting(sheets.Count, placed, tooBig);
        }
        Func<Blank, long>[] sorts = [b => (long)b.W * b.H, b => Math.Max(b.W, b.H), b => Math.Min(b.W, b.H)];
        Nesting? fewest = null;
        foreach (var key in sorts)
        {
            var n = Place(blanks.OrderByDescending(key));
            if (fewest == null || n.Sheets < fewest.Sheets) fewest = n;
        }
        return fewest ?? new Nesting(0, [], []);
    }

    /// <summary>The pieces a roof cuts from flat sheet (flat_stock.roof_blanks): the kinds its profile names, counted from the takeoff's stats and the odd gutter corners.</summary>
    public static List<Blank> Blanks(FlatSheet sheet, Data d, int ridgeWidth, int gutter, IReadOnlyDictionary<string, double> stats, IReadOnlyList<double> angles)
    {
        var o = new List<Blank>();
        foreach (var id in sheet.Blanks)
        {
            if (d.Blanks.FirstOrDefault(b => b.Id == id) is not { } def) continue;
            var (w, h) = (def.Width.Of(ridgeWidth, gutter), def.Height.Of(ridgeWidth, gutter));
            string Name(double? angle = null) => def.Name.Replace("{g}", gutter.ToString(CultureInfo.InvariantCulture))
                .Replace("{angle}", angle is { } a ? a.ToString("0.#", CultureInfo.InvariantCulture) : "");
            if (def.Of == "angles")
            {
                for (var i = 0; i < angles.Count; i++) o.Add(new Blank($"{def.Prefix}-{i + 1:00}", Name(angles[i]), w, h));
                continue;
            }
            var n = (int)Math.Round(stats.GetValueOrDefault(def.Of));
            if (def.Per > 0)
            {
                for (var i = 0; i * def.Per < n; i++) o.Add(new Blank($"{def.Prefix}-{i + 1:00}", Name(), Math.Min(def.Per, n - i * def.Per) * def.Each, h));
                continue;
            }
            for (var i = 0; i < n; i++) o.Add(new Blank($"{def.Prefix}-{i + 1:00}", Name(), w, h));
        }
        return o;
    }

    /// <summary>Every combination of the profile's ridge caps and the rainwater systems, laid; the roof's own the one of its ridge cap and system.</summary>
    public static Result? Of(PanelProfile p, Data d, Rainwater.Data rain, string? ridge, string system, IReadOnlyDictionary<string, double> stats, IReadOnlyList<double> angles)
    {
        if (p.FlatSheet is not { } sheet) return null;
        var ridges = p.Ridges.Count > 0 ? p.Ridges.Select(r => (string?)r).ToList() : [null];
        var combos = new List<Combination>();
        foreach (var r in ridges)
            foreach (var s in rain.Systems)
                combos.Add(new Combination(r, s.Id, Nest(Blanks(sheet, d, sheet.RidgeWidthOf(r), s.Gutter, stats, angles), d)));
        var current = combos.FirstOrDefault(c => c.Ridge == (p.Ridges.Count > 0 ? ridge ?? p.Ridges[0] : null) && c.System == system) ?? combos[0];
        return new Result(sheet, d, combos, current);
    }

    /// <summary>The roof's sheets as the interface reads them: the stock, the current combination's pieces, every combination's sheet count.</summary>
    public static JsonObject ToJson(Result r) => new()
    {
        ["code"] = r.Sheet.Code, ["name"] = r.Sheet.Name,
        ["width"] = r.Data.Width, ["height"] = r.Data.Height, ["margin"] = r.Data.Margin, ["gap"] = r.Data.Gap,
        ["ridge"] = r.Current.Ridge, ["system"] = r.Current.System, ["sheets"] = r.Current.Nesting.Sheets,
        ["pieces"] = new JsonArray([.. r.Current.Nesting.Pieces.OrderBy(x => x.Sheet).ThenBy(x => x.Id, StringComparer.Ordinal).Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Id, ["name"] = x.Label, ["sheet"] = x.Sheet, ["x"] = x.X, ["y"] = x.Y, ["width"] = x.W, ["height"] = x.H,
            ["blankWidth"] = x.BlankW, ["blankHeight"] = x.BlankH,
        })]),
        ["tooBig"] = new JsonArray([.. r.Current.Nesting.TooBig.Select(b => (JsonNode)new JsonObject { ["id"] = b.Id, ["name"] = b.Label, ["width"] = b.W, ["height"] = b.H })]),
        ["combinations"] = new JsonArray([.. r.Combinations.Select(c => (JsonNode)new JsonObject { ["ridge"] = c.Ridge, ["system"] = c.System, ["sheets"] = c.Nesting.Sheets, ["pieces"] = c.Nesting.Pieces.Count })]),
    };
}

/// <summary>
/// A profile's flat sheet (S5): the article its pieces are cut from (its colour's), the ridge cap's width the pieces are sized by
/// (one, or one a ridge option) and the kinds of blank it cuts (profiles.json's <c>common.flatSheet.blanks</c> by id).
/// </summary>
public sealed record FlatSheet(string Code, string Name, int RidgeWidth, IReadOnlyDictionary<string, int> RidgeWidths, IReadOnlyList<string> Blanks)
{
    public int RidgeWidthOf(string? ridge) => ridge != null && RidgeWidths.TryGetValue(ridge, out var w) ? w : RidgeWidth > 0 ? RidgeWidth : RidgeWidths.Values.FirstOrDefault(290);

    public static FlatSheet? Read(JsonNode? n)
    {
        if (n is not JsonObject o || (string?)o["code"] is not { Length: > 0 } code) return null;
        var widths = new Dictionary<string, int>();
        if (o["ridgeWidths"] is JsonObject rw) foreach (var (k, v) in rw) widths[k] = (int)(Json.Number(v) ?? 0);
        return new FlatSheet(code, (string?)o["name"] ?? code, (int)(Json.Number(o["ridgeWidth"]) ?? 0), widths,
            [.. ((o["blanks"] as JsonArray) ?? []).Select(x => (string?)x).OfType<string>()]);
    }
}
