using System.Globalization;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>One row of a form: its cells, what it lists (an estimate line's key; null for a heading or a total) and how it is set (line, chapter, subtotal, total).</summary>
public sealed record FormRow(IReadOnlyList<string> Cells, string? Key = null, string Style = "line");

/// <summary>A table of the estimate as printed: its title, a line under it, its columns, its rows; which columns are numbers (right-aligned).</summary>
public sealed record CostForm(string Id, string Title, string Subtitle, IReadOnlyList<string> Columns, IReadOnlyList<FormRow> Rows, IReadOnlySet<int> Numeric)
{
    public ScheduleGrid ToGrid() => new(Title, Columns, Rows.Select(r => r.Cells).ToList(), Rows.Select(r => r.Key).ToList());
    public IReadOnlyList<IReadOnlyList<string>> Table => [Columns, .. Rows.Select(r => r.Cells)];
}

/// <summary>
/// The estimate's tables (docs/plan-4d5d.md, stage 6): the estimate itself, and the Romanian forms as practice under
/// Ordinul MDLPA 863/2008 lays them out — F3, the bill of quantities by chapter with unit prices and values split into
/// material, labour, equipment and transport and the totals under them; the object estimate (F2), each chapter's value
/// without VAT, its VAT and its value with VAT; C6, the labour by trade with its hours; C7, the materials; C8, the
/// equipment; C9, the transport. They are groupings of the same estimate, so they add up to its totals. The forms are
/// headed in Romanian (they are Romania's), the estimate in English like the rest of the engine's tables.
/// </summary>
public static class CostForms
{
    public static readonly (string Id, string Label)[] All =
    [
        ("estimate", "Estimate"),
        ("f3", "Bill of quantities (F3)"),
        ("object", "Object estimate (F2)"),
        ("c6", "Labour (C6)"),
        ("c7", "Materials (C7)"),
        ("c8", "Equipment (C8)"),
        ("c9", "Transport (C9)"),
    ];

    /// <summary>The default chapters as the Romanian forms name them.</summary>
    static readonly Dictionary<string, string> ChaptersRo = new()
    {
        ["Earthworks"] = "Terasamente", ["Foundations"] = "Fundații", ["Structure"] = "Structura de rezistență", ["Walls"] = "Zidării și pereți",
        ["Roof"] = "Acoperiș", ["Doors and windows"] = "Tâmplărie", ["Finishes and insulation"] = "Finisaje și izolații", ["Installations"] = "Instalații",
        ["Site works"] = "Lucrări provizorii și organizare de șantier", ["Other"] = "Alte lucrări",
    };

    /// <summary>A chapter's name in a Romanian form: a default chapter in Romanian (after the storey, when the estimate goes by storey); one of the user's as typed.</summary>
    public static string ChapterRo(string name)
    {
        var dot = name.LastIndexOf(" · ", StringComparison.Ordinal);
        var (head, tail) = dot >= 0 ? (name[..(dot + 3)], name[(dot + 3)..]) : ("", name);
        return head + (ChaptersRo.TryGetValue(tail, out var ro) ? ro : tail);
    }

    static string Pct(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>A number as the table shows it (two or three decimals), or as a workbook takes it (a plain number).</summary>
    sealed class Fmt(bool raw)
    {
        public string Money(double v) => raw ? Math.Round(v, 2).ToString(CultureInfo.InvariantCulture) : v.ToString("0.00", CultureInfo.InvariantCulture);
        public string Qty(double v) => raw ? Math.Round(v, 3).ToString(CultureInfo.InvariantCulture) : v.ToString("0.000", CultureInfo.InvariantCulture);
        public string Hours(double v) => raw ? Math.Round(v, 2).ToString(CultureInfo.InvariantCulture) : v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    static string Subtitle(EstimateResult r, IQueryContext q, bool ro) =>
        ro ? $"{Sheets.ProjectName(q)} · {r.Entity.Name} · {r.Settings.Currency}, fără TVA unde nu se spune altfel"
           : $"{Sheets.ProjectName(q)} · {r.Entity.Name} · {r.Settings.Currency}";

    public static CostForm Make(IQueryContext q, EstimateResult r, string form, bool raw = false) => form switch
    {
        "f3" => F3(q, r, new Fmt(raw)),
        "object" => Object(q, r, new Fmt(raw)),
        "c6" => Labour(q, r, new Fmt(raw)),
        "c7" => Resources(q, r, new Fmt(raw), "material"),
        "c8" => Resources(q, r, new Fmt(raw), "equipment"),
        "c9" => Resources(q, r, new Fmt(raw), "transport"),
        _ => Estimate(q, r, new Fmt(raw)),
    };

    /// <summary>A table saying there is no estimate yet, for a schedule placed before one was made.</summary>
    public static CostForm None(string form) =>
        new(form, All.FirstOrDefault(f => f.Id == form).Label ?? "Estimate", "", ["Estimate"], [new FormRow(["No estimate yet: Cost → Estimate makes one."])], new HashSet<int>());

    static CostForm Estimate(IQueryContext q, EstimateResult r, Fmt f)
    {
        var s = r.Settings;
        var other = s.Equivalent;
        List<string> cols = ["No.", "Code", "Description", "Unit", "Quantity", "Material", "Labour", "Equipment", "Transport", "Unit price", "Value"];
        if (other) cols.Add($"Value ({s.Other})");
        var rows = new List<FormRow>();
        string[] Blank(int n) => Enumerable.Repeat("", n).ToArray();
        List<string> Sum(string label, double v, string style = "total")
        {
            List<string> c = ["", "", label, .. Blank(7), f.Money(v)];
            if (other) c.Add(f.Money(Money.Round(s.InOther(v))));
            rows.Add(new FormRow(c, null, style));
            return c;
        }
        for (var ci = 0; ci < r.Chapters.Count; ci++)
        {
            var ch = r.Chapters[ci];
            List<string> head = [$"{ci + 1}", "", ch.Name, .. Blank(8)];
            if (other) head.Add("");
            rows.Add(new FormRow(head, null, "chapter"));
            for (var li = 0; li < ch.Lines.Count; li++)
            {
                var l = ch.Lines[li];
                List<string> c = [$"{ci + 1}.{li + 1}", l.Code, l.Priced ? l.Name : $"{l.Name} (no price)", l.Unit, f.Qty(l.Qty), f.Money(l.M), f.Money(l.L), f.Money(l.E), f.Money(l.T), f.Money(l.UnitPrice), f.Money(l.Value)];
                if (other) c.Add(f.Money(Money.Round(s.InOther(l.Value))));
                rows.Add(new FormRow(c, l.Key));
            }
            Sum($"Total {ch.Name}", ch.Sums.Direct, "subtotal");
        }
        var t = r.Totals;
        Sum("Direct costs", t.Direct.Direct);
        Sum("  of which material", t.Direct.M, "subtotal");
        Sum("  of which labour", t.Direct.L, "subtotal");
        Sum("  of which equipment", t.Direct.E, "subtotal");
        Sum("  of which transport", t.Direct.T, "subtotal");
        Sum($"Contributions on labour (CAM) {Pct(s.LabourTax)} %", t.LabourTax, "subtotal");
        Sum("Direct costs with contributions", t.DirectTotal);
        Sum($"Overheads {Pct(s.Overheads)} %", t.Overheads, "subtotal");
        Sum($"Profit {Pct(s.Profit)} %", t.Profit, "subtotal");
        Sum("Total without VAT", t.Net);
        Sum($"VAT {Pct(s.Vat)} %", t.Vat, "subtotal");
        Sum("Total with VAT", t.Total);
        if (other) Sum($"Total with VAT in {s.Other} at {s.EurRate.ToString("0.####", CultureInfo.InvariantCulture)} RON/EUR", t.Other);
        return new("estimate", $"Estimate: {r.Entity.Name}", Subtitle(r, q, false), cols, rows, Numbers(cols.Count, 4));
    }

    static HashSet<int> Numbers(int count, int from) => Enumerable.Range(from, Math.Max(0, count - from)).ToHashSet();

    static CostForm F3(IQueryContext q, EstimateResult r, Fmt f)
    {
        var s = r.Settings;
        List<string> cols = ["Nr. crt.", "Simbol articol", "Denumire articol", "U.M.", "Cantitate",
            "Preț unitar material", "Preț unitar manoperă", "Preț unitar utilaj", "Preț unitar transport",
            "Valoare material", "Valoare manoperă", "Valoare utilaj", "Valoare transport", "Valoare totală"];
        var rows = new List<FormRow>();
        static string[] Blank(int n) => Enumerable.Repeat("", n).ToArray();
        void Split(string label, Sums x, string style = "total") =>
            rows.Add(new FormRow(["", "", label, .. Blank(6), f.Money(x.M), f.Money(x.L), f.Money(x.E), f.Money(x.T), f.Money(x.Direct)], null, style));
        void One(string label, double v, string style = "total") => rows.Add(new FormRow(["", "", label, .. Blank(10), f.Money(v)], null, style));
        for (var ci = 0; ci < r.Chapters.Count; ci++)
        {
            var ch = r.Chapters[ci];
            rows.Add(new FormRow([$"{ci + 1}", "", $"Capitolul {ci + 1}: {ChapterRo(ch.Name)}", .. Blank(11)], null, "chapter"));
            for (var li = 0; li < ch.Lines.Count; li++)
            {
                var l = ch.Lines[li];
                rows.Add(new FormRow([$"{ci + 1}.{li + 1}", l.Code, l.Priced ? l.Name : $"{l.Name} (fără preț)", l.Unit, f.Qty(l.Qty),
                    f.Money(l.M), f.Money(l.L), f.Money(l.E), f.Money(l.T), f.Money(l.VM), f.Money(l.VL), f.Money(l.VE), f.Money(l.VT), f.Money(l.Value)], l.Key));
            }
            Split($"Total capitolul {ci + 1}", ch.Sums, "subtotal");
        }
        var t = r.Totals;
        Split("Total cheltuieli directe", t.Direct);
        One($"Contribuția asiguratorie pentru muncă (CAM) {Pct(s.LabourTax)} % din manoperă", t.LabourTax, "subtotal");
        One("Total cheltuieli directe, cu CAM", t.DirectTotal);
        One($"Cheltuieli indirecte {Pct(s.Overheads)} %", t.Overheads, "subtotal");
        One($"Profit {Pct(s.Profit)} %", t.Profit, "subtotal");
        One("TOTAL fără TVA", t.Net);
        One($"TVA {Pct(s.Vat)} %", t.Vat, "subtotal");
        One("TOTAL cu TVA", t.Total);
        if (s.Equivalent) One($"Echivalent {s.Other} (curs {s.EurRate.ToString("0.####", CultureInfo.InvariantCulture)} lei/EUR)", t.Other, "subtotal");
        return new("f3", "Formularul F3: Lista cu cantitățile de lucrări pe categorii de lucrări", Subtitle(r, q, true), cols, rows, Numbers(cols.Count, 4));
    }

    static CostForm Object(IQueryContext q, EstimateResult r, Fmt f)
    {
        var s = r.Settings;
        List<string> cols = ["Nr. crt.", "Denumirea capitolelor și subcapitolelor de lucrări", "Valoare fără TVA", "TVA", "Valoare cu TVA"];
        if (s.Equivalent) cols.Add($"Valoare cu TVA ({s.Other})");
        var rows = new List<FormRow>();
        List<string> Row(string no, string name, double net, double vat, double total)
        {
            List<string> c = [no, name, f.Money(net), f.Money(vat), f.Money(total)];
            if (s.Equivalent) c.Add(f.Money(Money.Round(s.InOther(total))));
            return c;
        }
        var head = new List<string> { "I", "Lucrări de construcții și instalații", "", "", "" };
        if (s.Equivalent) head.Add("");
        rows.Add(new FormRow(head, null, "chapter"));
        var shares = Costing.Shares(r);
        for (var i = 0; i < shares.Count; i++)
        {
            var (ch, net, vat, total) = shares[i];
            rows.Add(new FormRow(Row($"{i + 1}", ChapterRo(ch.Name), net, vat, total), "chapter:" + ch.Name));
        }
        rows.Add(new FormRow(Row("", "TOTAL DEVIZ PE OBIECT", r.Totals.Net, r.Totals.Vat, r.Totals.Total), null, "total"));
        return new("object", "Deviz pe obiect (formularul F2)", Subtitle(r, q, true), cols, rows, Numbers(cols.Count, 2));
    }

    /// <summary>The labour (C6): each trade's hours and value; a line whose price list gives no labour resource counts under its trade (else its article), its hours from the norm column.</summary>
    static CostForm Labour(IQueryContext q, EstimateResult r, Fmt f)
    {
        var groups = new Dictionary<string, (double Hours, bool HasHours, double Value)>();
        void Add(string name, double? hours, double value)
        {
            var g = groups.GetValueOrDefault(name);
            groups[name] = (g.Hours + (hours ?? 0), g.HasHours || hours != null, g.Value + value);
        }
        foreach (var l in r.Lines)
        {
            var labour = l.Resources.Where(x => x.Kind == "labour").ToList();
            if (labour.Count > 0)
                foreach (var x in labour) Add(x.Trade ?? x.Name, l.Qty * x.Quantity, Money.Round(l.Qty * x.Quantity * x.Price));
            else if (l.VL != 0) Add(l.Trade ?? $"{l.Code} {l.Name}".Trim(), l.Hours is { } h ? l.Qty * h : null, l.VL);
        }
        var rows = new List<FormRow>();
        var i = 0;
        foreach (var (name, g) in groups.OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var value = Money.Round(g.Value);
            rows.Add(new FormRow([$"{++i}", name, g.HasHours ? f.Hours(g.Hours) : "", g.HasHours && g.Hours > 0 ? f.Money(Money.Round(value / g.Hours)) : "", f.Money(value)]));
        }
        var hours = groups.Values.Where(g => g.HasHours).Sum(g => g.Hours);
        rows.Add(new FormRow(["", "TOTAL", hours > 0 ? f.Hours(hours) : "", "", f.Money(Money.Round(groups.Values.Sum(g => Money.Round(g.Value))))], null, "total"));
        return new("c6", "Formularul C6: Lista consumurilor de resurse umane și număr de ore de manoperă", Subtitle(r, q, true),
            ["Nr. crt.", "Meseria", "Nr. total ore manoperă", "Tarif mediu (lei/oră)", "Valoare"], rows, Numbers(5, 2));
    }

    /// <summary>The materials (C7), equipment (C8) or transport (C9): each resource's quantity and value; a line without resources of the kind counts as one of its own, its quantity the line's.</summary>
    static CostForm Resources(IQueryContext q, EstimateResult r, Fmt f, string kind)
    {
        var groups = new Dictionary<string, Res>(StringComparer.OrdinalIgnoreCase);
        void Add(string code, string name, string unit, double qty, double price, double value)
        {
            var key = $"{code}\u001f{price.ToString(CultureInfo.InvariantCulture)}";
            if (!groups.TryGetValue(key, out var g)) groups[key] = g = new Res(code, name, unit, price);
            g.Qty += qty;
            g.Value += value;
        }
        foreach (var l in r.Lines)
        {
            var own = l.Resources.Where(x => x.Kind == kind).ToList();
            if (own.Count > 0) foreach (var x in own) Add(x.Code, x.Name, x.Unit, l.Qty * x.Quantity, x.Price, Money.Round(l.Qty * x.Quantity * x.Price));
            else
            {
                var (price, value) = kind switch { "equipment" => (l.E, l.VE), "transport" => (l.T, l.VT), _ => (l.M, l.VM) };
                if (value != 0) Add(l.Code, l.Name, l.Unit, l.Qty, price, value);
            }
        }
        var rows = new List<FormRow>();
        var i = 0;
        foreach (var g in groups.Values.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
            rows.Add(new FormRow([$"{++i}", g.Code, g.Name, g.Unit, f.Qty(g.Qty), f.Money(g.Price), f.Money(Money.Round(g.Value))]));
        rows.Add(new FormRow(["", "", "TOTAL", "", "", "", f.Money(Money.Round(groups.Values.Sum(g => Money.Round(g.Value))))], null, "total"));
        var (id, title, what) = kind switch
        {
            "equipment" => ("c8", "Formularul C8: Lista consumurilor de ore de funcționare a utilajelor de construcții", "Denumirea utilajului"),
            "transport" => ("c9", "Formularul C9: Lista consumurilor privind transporturile", "Denumirea transportului"),
            _ => ("c7", "Formularul C7: Lista consumurilor de materiale", "Denumirea materialului"),
        };
        return new(id, title, Subtitle(r, q, true), ["Nr. crt.", "Cod", what, "U.M.", "Cantitate", "Preț unitar", "Valoare"], rows, Numbers(7, 4));
    }

    // ------------------------------------------------------------------ files

    /// <summary>Forms as one workbook, a sheet each (numbers as numbers).</summary>
    public static byte[] Xlsx(IReadOnlyList<CostForm> forms) =>
        XlsxWriter.Write(forms.Select(f => (SheetName(f.Id), (IReadOnlyList<IReadOnlyList<string>>)[[f.Title], [f.Subtitle], .. f.Table])).ToList());

    static string SheetName(string id) => id switch { "estimate" => "Deviz", "object" => "F2", _ => id.ToUpperInvariant() };

    /// <summary>
    /// Forms as a PDF: A4 landscape pages, the title and the project over each, the columns sized to what they hold
    /// (the description widest, cut short past its share), headings repeated on every page, chapters and totals in bold.
    /// </summary>
    public static byte[] Pdf(IReadOnlyList<CostForm> forms)
    {
        const double W = 297, H = 210, M = 12, Size = 2.2, Line = 4.4, Top = 26;
        var pages = new List<PaperPage>();
        foreach (var form in forms)
        {
            var cols = form.Columns.Count;
            // Each column as wide as its longest cell (the headings broken into words), the widest text column taking what is left.
            double Chars(string s) => s.Length * Size * 0.52;
            var widths = new double[cols];
            for (var c = 0; c < cols; c++)
            {
                var head = form.Columns[c].Split(' ').Max(w => Chars(w));
                var body = form.Rows.Where(r => r.Style == "line").Select(r => c < r.Cells.Count ? Chars(r.Cells[c]) : 0).DefaultIfEmpty(0).Max();
                widths[c] = Math.Max(head, Math.Min(body, form.Numeric.Contains(c) ? 40 : 120)) + 2.5;
            }
            var text = Enumerable.Range(0, cols).Where(c => !form.Numeric.Contains(c)).OrderByDescending(c => widths[c]).FirstOrDefault();
            var spare = W - 2 * M - widths.Sum();
            widths[text] = Math.Max(20, widths[text] + spare);
            if (widths.Sum() > W - 2 * M) { var k = (W - 2 * M) / widths.Sum(); for (var c = 0; c < cols; c++) widths[c] *= k; }
            var xs = new double[cols + 1];
            xs[0] = M;
            for (var c = 0; c < cols; c++) xs[c + 1] = xs[c] + widths[c];
            var headLines = form.Columns.Select((h, c) => Wrap(h, widths[c] - 2, Size)).ToList();
            var headH = headLines.Max(l => l.Count) * Line;
            var perPage = Math.Max(1, (int)((H - M - Top - headH - 6) / Line));
            var chunks = form.Rows.Count == 0 ? [Array.Empty<FormRow>()] : form.Rows.Chunk(perPage).ToList();
            for (var pi = 0; pi < chunks.Count; pi++)
            {
                var ink = new Pen("TEXT", "#000000", 0, null, null);
                var rules = new Pen("TITLE", "#000000", 0.2, null, null);
                var thin = new Pen("TITLE", "#888888", 0.1, null, null);
                var shade = new Pen("TITLE", null, 0, "#ececec", null);
                void T(double x, double y, string s, double size, bool bold = false, string anchor = "start") => ink.Texts.Add(new PenText(new Vec2(x, y), s, size, Anchor: anchor, Bold: bold));
                var y = M + 5;
                T(M, y, form.Title, 4, true);
                T(W - M, y, chunks.Count > 1 ? $"{pi + 1} / {chunks.Count}" : "", 2.6, anchor: "end");
                y += 6; T(M, y, form.Subtitle, 2.8);
                y = M + Top - 4;
                rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
                for (var c = 0; c < cols; c++)
                    for (var k = 0; k < headLines[c].Count; k++)
                        T(form.Numeric.Contains(c) ? xs[c + 1] - 1 : xs[c] + 1, y + (k + 1) * Line - 1.2, headLines[c][k], Size, true, form.Numeric.Contains(c) ? "end" : "start");
                y += headH + 1;
                rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
                foreach (var row in chunks[pi])
                {
                    var bold = row.Style is "chapter" or "total";
                    if (row.Style == "chapter") shade.Loops.Add([new Vec2(M, y), new Vec2(W - M, y), new Vec2(W - M, y + Line), new Vec2(M, y + Line)]);
                    for (var c = 0; c < cols && c < row.Cells.Count; c++)
                    {
                        var cell = row.Cells[c];
                        if (cell.Length == 0) continue;
                        // A heading or a total's label runs on over the empty cells after it.
                        var room = widths[c];
                        if (!form.Numeric.Contains(c)) for (var n = c + 1; n < cols && n < row.Cells.Count && row.Cells[n].Length == 0 && !form.Numeric.Contains(n); n++) room += widths[n];
                        if (row.Style != "line" && !form.Numeric.Contains(c)) for (var n = c + 1; n < cols && n < row.Cells.Count && row.Cells[n].Length == 0; n++) room += widths[n];
                        var shown = Fit(cell, room - 2, Size);
                        T(form.Numeric.Contains(c) ? xs[c + 1] - 1 : xs[c] + 1, y + Line - 1.2, shown, Size, bold, form.Numeric.Contains(c) ? "end" : "start");
                    }
                    y += Line;
                    (row.Style == "line" ? thin : rules).Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
                }
                pages.Add(new PaperPage(W, H, [shade, thin, rules, ink], $"{form.Title} {pi + 1}"));
            }
        }
        return PdfWriter.Write(pages);
    }

    sealed class Res(string code, string name, string unit, double price)
    {
        public string Code { get; } = code;
        public string Name { get; } = name;
        public string Unit { get; } = unit;
        public double Price { get; } = price;
        public double Qty { get; set; }
        public double Value { get; set; }
    }

    /// <summary>A heading broken into lines that fit the width (by words).</summary>
    static List<string> Wrap(string s, double width, double size)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var w in s.Split(' '))
        {
            var next = cur.Length == 0 ? w : cur + " " + w;
            if (cur.Length > 0 && next.Length * size * 0.52 > width) { lines.Add(cur); cur = w; }
            else cur = next;
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines;
    }

    static string Fit(string s, double width, double size)
    {
        var max = Math.Max(3, (int)(width / (size * 0.5)));
        return s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}

/// <summary>A form as a schedule: the project's estimate (or the one the schedule names) in one of its tables, placed on sheets, exported like any schedule.</summary>
public sealed class CostSchedule(string form, string label) : IScheduleSource
{
    public string Kind => form == "estimate" ? "estimate" : "estimate-" + form;
    public string Label => label;
    public ScheduleGrid Grid(IQueryContext query) => Of(query, null);
    public ScheduleGrid Grid(IQueryContext query, Entity schedule) => Of(query, schedule);
    ScheduleGrid Of(IQueryContext query, Entity? schedule) =>
        (Costing.EstimateOf(query.Model, schedule?.Str("estimate")) is { } e ? CostForms.Make(query, Costing.Compute(query, e), form) : CostForms.None(form)).ToGrid();
}
