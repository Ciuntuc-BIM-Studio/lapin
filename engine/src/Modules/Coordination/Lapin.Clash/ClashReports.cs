using System.Globalization;
using System.Text;
using System.Xml;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Clash;

/// <summary>
/// A test's clashes grouped (docs/plan-visualisation.md, stage 2, after BimGo's ClashGroups, MIT-0): by the storey,
/// by the element in most of them (greedy: the element with the most clashes takes them, then the next), by the pair of
/// classes, or by status. Worked out when asked, never kept: a run again regroups.
/// </summary>
public static class ClashGroups
{
    public const string None = "none", Storey = "storey", Element = "element", Classes = "classes", Status = "status";
    /// <summary>The group of clashes no element shares with another (by element).</summary>
    public const string Other = "Other clashes";
    public static readonly string[] Modes = [None, Storey, Element, Classes, Status];

    /// <summary>Each clash's group (its number → the group's name, in the order the groups come); empty for none.</summary>
    public static Dictionary<int, string> Of(IReadOnlyList<ClashRecord> clashes, string mode, Func<string, ElementFacts?> facts)
    {
        var o = new Dictionary<int, string>();
        string Name(string id) => facts(id) is { } f ? f.Entity.Name is { Length: > 0 } n ? n : f.ClassLabel : id;
        switch (mode)
        {
            case Storey:
                foreach (var c in clashes) o[c.Number] = facts(c.A)?.Storey ?? facts(c.B)?.Storey ?? "No storey";
                break;
            case Classes:
                foreach (var c in clashes)
                {
                    var pair = new[] { facts(c.A)?.ClassLabel ?? "?", facts(c.B)?.ClassLabel ?? "?" };
                    Array.Sort(pair, StringComparer.CurrentCulture);
                    o[c.Number] = $"{pair[0]} × {pair[1]}";
                }
                break;
            case Status:
                foreach (var c in clashes) o[c.Number] = c.Status;
                break;
            case Element:
                // Elements of one name (two walls of a type) told apart: the second "Brick wall (2)".
                // A group needs two clashes at least (BimGo's MIN_SIZE): the clashes left alone go together at the end.
                var left = clashes.ToList();
                var named = new Dictionary<string, int>();
                while (left.Count > 0)
                {
                    var best = left.SelectMany(c => new[] { c.A, c.B }).GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First();
                    if (best.Count() < 2) { foreach (var c in left) o[c.Number] = Other; break; }
                    var top = best.Key;
                    var name = Name(top);
                    named[name] = named.GetValueOrDefault(name) + 1;
                    if (named[name] > 1) name = $"{name} ({named[name]})";
                    foreach (var c in left.Where(c => c.A == top || c.B == top)) o[c.Number] = name;
                    left.RemoveAll(c => c.A == top || c.B == top);
                }
                break;
        }
        return o;
    }
}

/// <summary>
/// A test's clashes written out (docs/plan-visualisation.md, stage 2): XML as Navisworks writes its clash reports
/// (exchange / batchtest / clashtests / clashtest / clashresults, groups as clashgroup; BimGo's writer, MIT-0), read by
/// other coordination tools; and a PDF list on A4 landscape, grouped when asked.
/// </summary>
public static class ClashReportFiles
{
    static string Num(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    public static byte[] Xml(IQueryContext q, ClashTests.Test t, IReadOnlyList<ClashRecord> clashes, IReadOnlyDictionary<int, string> groups, IReadOnlyDictionary<string, SearchSets.Set> sets)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var stream = new MemoryStream();
        using (var x = XmlWriter.Create(stream, settings))
        {
            void At(string n, string? v) => x.WriteAttributeString(n, Safe(v));
            void El(string n, string? v) => x.WriteElementString(n, Safe(v));
            x.WriteStartDocument();
            x.WriteStartElement("exchange");
            At("units", "m"); At("filename", q.Title ?? Sheets.ProjectName(q)); At("generator", "Lapin"); At("created", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
            x.WriteStartElement("batchtest");
            At("name", "Clash report"); At("units", "m");
            x.WriteStartElement("clashtests");
            x.WriteStartElement("clashtest");
            At("name", t.Name); At("guid", t.Id); At("test_type", t.Kind == ClashTests.Clearance ? "clearance" : "hard"); At("status", "ok"); At("tolerance", Num(t.Tolerance));
            void Selection(string side, string? setId)
            {
                x.WriteStartElement(side);
                x.WriteStartElement("clashselection");
                At("selfintersect", "0"); At("primtypes", "1");
                El("locator", "lcop_selection_set_tree/" + (setId != null && sets.TryGetValue(setId, out var s) ? s.Name : t.Kind == ClashTests.Openings ? "Openings" : ""));
                x.WriteEndElement();
                x.WriteEndElement();
            }
            Selection("left", t.SetA);
            Selection("right", t.IsSelf ? t.SetA : t.SetB);
            x.WriteStartElement("summary");
            At("total", clashes.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var s in ClashTests.Statuses) At(s, clashes.Count(c => c.Status == s).ToString(CultureInfo.InvariantCulture));
            El("testtype", t.Kind == ClashTests.Clearance ? "Clearance" : "Hard");
            El("teststatus", "OK");
            x.WriteEndElement();
            x.WriteStartElement("clashresults");
            void Result(ClashRecord c)
            {
                x.WriteStartElement("clashresult");
                At("name", $"Clash{c.Number}"); At("guid", $"{t.Id}|{c.Key}"); At("status", c.Status); At("distance", Num(c.Crossing ? -Math.Abs(c.Distance) : c.Distance));
                El("description", c.Crossing ? "Hard" : "Clearance");
                El("resultstatus", char.ToUpperInvariant(c.Status[0]) + c.Status[1..]);
                x.WriteStartElement("clashpoint");
                x.WriteStartElement("pos3f"); At("x", Num(c.At.X)); At("y", Num(c.At.Y)); At("z", Num(c.At.Z)); x.WriteEndElement();
                x.WriteEndElement();
                El("gridlocation", SearchSets.Facts(q, q.Model.Get(c.A)!)?.Storey ?? "");
                void Date(string n, string? iso)
                {
                    if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)) return;
                    x.WriteStartElement(n); x.WriteStartElement("date");
                    At("year", d.Year.ToString(CultureInfo.InvariantCulture)); At("month", d.Month.ToString(CultureInfo.InvariantCulture)); At("day", d.Day.ToString(CultureInfo.InvariantCulture));
                    At("hour", d.Hour.ToString(CultureInfo.InvariantCulture)); At("minute", d.Minute.ToString(CultureInfo.InvariantCulture)); At("second", d.Second.ToString(CultureInfo.InvariantCulture)); At("utc", "true");
                    x.WriteEndElement(); x.WriteEndElement();
                }
                Date("createddate", c.Found);
                Date("lastseendate", c.Seen);
                Date("resolveddate", c.Resolved);
                if (c.AssignedTo != null) El("assignedto", c.AssignedTo);
                if (c.Notes != null) { x.WriteStartElement("comments"); x.WriteStartElement("comment"); At("kind", "notes"); El("body", c.Notes); x.WriteEndElement(); x.WriteEndElement(); }
                x.WriteStartElement("clashobjects");
                foreach (var id in new[] { c.A, c.B })
                {
                    var e = q.Model.Get(id);
                    var f = e != null ? SearchSets.Facts(q, e) : null;
                    x.WriteStartElement("clashobject");
                    x.WriteStartElement("objectattribute"); El("name", "IfcGUID"); El("value", id); x.WriteEndElement();
                    El("layer", f?.Storey ?? "");
                    x.WriteStartElement("smarttags");
                    void Tag(string n, string? v) { x.WriteStartElement("smarttag"); El("name", n); El("value", v ?? ""); x.WriteEndElement(); }
                    Tag("Item Name", e?.Name);
                    Tag("Item Type", f?.ClassLabel);
                    Tag("IfcClass", e?.Class);
                    if (f?.Type != null) Tag("Type", f.Type);
                    if (f?.Link != null) Tag("Linked file", f.Link);
                    x.WriteEndElement();
                    x.WriteEndElement();
                }
                x.WriteEndElement();
                x.WriteEndElement();
            }
            if (groups.Count == 0) foreach (var c in clashes) Result(c);
            else
                foreach (var g in clashes.GroupBy(c => groups.GetValueOrDefault(c.Number, "")))
                {
                    x.WriteStartElement("clashgroup");
                    At("name", g.Key); At("count", g.Count().ToString(CultureInfo.InvariantCulture));
                    x.WriteStartElement("clashresults");
                    foreach (var c in g) Result(c);
                    x.WriteEndElement();
                    x.WriteEndElement();
                }
            x.WriteEndElement(); // clashresults
            x.WriteEndElement(); // clashtest
            x.WriteEndElement(); // clashtests
            x.WriteEndElement(); // batchtest
            x.WriteEndElement(); // exchange
            x.WriteEndDocument();
        }
        return stream.ToArray();
    }

    /// <summary>Text XML can hold: control characters taken out.</summary>
    static string Safe(string? s) => s == null ? "" : new string(s.Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || ch >= ' ').ToArray());

    /// <summary>The list as a PDF: A4 landscape, the project, the test and its run, the counts, then one line a clash under its group.</summary>
    public static byte[] Pdf(IQueryContext q, ClashTests.Test t, IReadOnlyList<ClashRecord> clashes, IReadOnlyDictionary<int, string> groups, bool stale)
    {
        const double W = 297, H = 210, M = 15, Line = 5.5;
        var rows = new List<(string? Group, ClashRecord? Clash)>();
        if (groups.Count == 0) rows.AddRange(clashes.Select(c => ((string?)null, (ClashRecord?)c)));
        else foreach (var g in clashes.GroupBy(c => groups.GetValueOrDefault(c.Number, ""))) { rows.Add((g.Key, null)); rows.AddRange(g.Select(c => ((string?)null, (ClashRecord?)c))); }
        var perPage = (int)((H - 2 * M - 45) / Line);
        var chunks = rows.Count == 0 ? [new List<(string?, ClashRecord?)>()] : rows.Chunk(Math.Max(1, perPage)).Select(c => c.ToList()).ToList();
        var pages = new List<PaperPage>();
        string Name(string id) => q.Model.Get(id) is { } e ? e.Name is { Length: > 0 } n ? n : q.Describe(e.Class)?.Label ?? e.Class : id;
        static string Fit(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
        for (var pi = 0; pi < chunks.Count; pi++)
        {
            var text = new Pen("TEXT", "#000000", 0, null, null);
            var red = new Pen("TEXT", "#c92a2a", 0, null, null);
            var rules = new Pen("TITLE", "#000000", 0.25, null, null);
            double y = M;
            void T(double x, string s, double size = 2.6, bool bold = false, string anchor = "start", Pen? pen = null) => (pen ?? text).Texts.Add(new PenText(new Vec2(x, y), s, size, Anchor: anchor, Bold: bold));
            y += 6; T(M, "CLASH REPORT", 5.5, true); T(W - M, Sheets.ProjectName(q), 4, true, "end");
            y += 7; T(M, $"{t.Name} · {(t.Kind == ClashTests.Clearance ? "clearance" : t.Kind == ClashTests.Openings ? "openings for MEP" : "hard")} {t.Tolerance * 1000:0} mm" + (t.LastRun is { } r ? $" · run {(string?)r["date"]}" : ""), 3);
            if (stale) T(W - M, "Out of date: the model has changed since this run", 3, true, "end", red);
            y += 5; T(M, string.Join(" · ", ClashTests.Statuses.Select(s => $"{clashes.Count(c => c.Status == s)} {s}")), 3);
            if (chunks.Count > 1) T(W - M, $"page {pi + 1} / {chunks.Count}", 2.6, anchor: "end");
            y += 4; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            double[] cols = [M, M + 12, M + 34, M + 94, M + 154, M + 176, M + 206];
            y += 5; T(cols[0], "No.", 2.6, true); T(cols[1], "Status", 2.6, true); T(cols[2], "Element A", 2.6, true); T(cols[3], "Element B", 2.6, true);
            T(cols[4], t.Kind == ClashTests.Clearance ? "Gap (mm)" : "Depth (mm)", 2.6, true); T(cols[5], "Assigned to", 2.6, true); T(cols[6], "Notes", 2.6, true);
            y += 1.5; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            foreach (var (group, c) in chunks[pi])
            {
                y += Line;
                if (c == null) { T(cols[0], group ?? "", 2.8, true); continue; }
                var pen = c.Status is ClashTests.New or ClashTests.Active ? red : text;
                T(cols[0], c.Number.ToString(CultureInfo.InvariantCulture), 2.6, true, pen: pen); T(cols[1], c.Status);
                T(cols[2], Fit(Name(c.A), 34)); T(cols[3], Fit(Name(c.B), 34)); T(cols[4], Math.Round(c.Distance * 1000).ToString("0", CultureInfo.InvariantCulture));
                T(cols[5], Fit(c.AssignedTo ?? "", 16)); T(cols[6], Fit(c.Notes ?? "", 40));
            }
            y += 2; rules.Segments.Add((new Vec2(M, y), new Vec2(W - M, y)));
            pages.Add(new PaperPage(W, H, [rules, text, red], $"Clash report {pi + 1}"));
        }
        return PdfWriter.Write(pages);
    }
}
