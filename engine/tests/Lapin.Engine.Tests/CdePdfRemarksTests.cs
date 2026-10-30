using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde;
using Lapin.Platform.Cde.PdfRemarks;
using Lapin.Protocol;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Remarks on sheets read back from PDF annotations (docs/plan-cde.md,
/// stage 3): the PDF reader on files built here — a classic xref table with
/// an incremental update carrying the annotations, an xref stream with an
/// object stream (real Flate, a PNG predictor), broken files — the text
/// strings and dates, the page-to-sheet transform (with /Rotate and a page
/// of another size), which sheet a file belongs to, and reading the same
/// file twice updating rather than duplicating.
/// </summary>
public sealed class CdePdfRemarksTests
{
    const double Pt = 72 / 25.4;
    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    static Fixture Fx() => new(new ArchitectureModule(), new DraftingModule(), new StructureModule());

    static string Sheet(Fixture f, string number = "A-101", string name = "Ground floor plan", string paper = "A3")
    {
        var s = (string)f.Run("sheet.create", new JsonObject { ["name"] = name, ["number"] = number, ["paper"] = paper })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["name"] = name, ["scale"] = 100 })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = s, ["viewId"] = plan, ["x"] = 150, ["y"] = 120 });
        return s;
    }

    // ------------------------------------------------------------ building PDFs

    /// <summary>A PDF with a classic xref table: the objects given in order from 1, the trailer's Root 1.</summary>
    static byte[] Pdf(params string[] objects)
    {
        using var f = new MemoryStream();
        void W(string s) => f.Write(Encoding.Latin1.GetBytes(s));
        W("%PDF-1.4\n%âãÏÓ\n");
        var offsets = new List<long>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(f.Position); W($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = f.Position;
        W($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) W($"{o:D10} 00000 n \n");
        W($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return f.ToArray();
    }

    /// <summary>An incremental update appended: the objects (number, body) new or replaced, a new xref section pointing back at the previous one.</summary>
    static byte[] Update(byte[] pdf, params (int Num, string Body)[] objects)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var prev = long.Parse(text[(text.LastIndexOf("startxref", StringComparison.Ordinal) + 9)..].Trim().Split('\n')[0].Trim(), CultureInfo.InvariantCulture);
        var size = int.Parse(System.Text.RegularExpressions.Regex.Match(text, @"/Size (\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
        using var f = new MemoryStream();
        f.Write(pdf);
        void W(string s) => f.Write(Encoding.Latin1.GetBytes(s));
        if (pdf[^1] != '\n') W("\n");
        var offsets = new List<(int, long)>();
        foreach (var (num, body) in objects) { offsets.Add((num, f.Position)); W($"{num} 0 obj\n{body}\nendobj\n"); }
        var xref = f.Position;
        W("xref\n");
        foreach (var (num, off) in offsets.OrderBy(x => x.Item1)) W($"{num} 1\n{off:D10} 00000 n \n");
        var newSize = Math.Max(size, objects.Max(o => o.Num) + 1);
        W($"trailer\n<< /Size {newSize} /Root 1 0 R /Prev {prev} >>\nstartxref\n{xref}\n%%EOF\n");
        return f.ToArray();
    }

    static byte[] Deflate(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal)) z.Write(data);
        return ms.ToArray();
    }

    /// <summary>A one-page PDF of a paper (mm), landscape as Lapin prints A3: the page is object 3, its content 4, Annots as given.</summary>
    static string[] PageObjects(double w, double h, string annots = "", int rotate = 0, string mediaBox = "") =>
    [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        $"<< /Type /Page /Parent 2 0 R /MediaBox {(mediaBox.Length > 0 ? mediaBox : $"[0 0 {F(w * Pt)} {F(h * Pt)}]")}{(rotate != 0 ? $" /Rotate {rotate}" : "")} /Contents 4 0 R{(annots.Length > 0 ? $" /Annots {annots}" : "")} >>",
        "<< /Length 0 >>\nstream\n\nendstream",
    ];

    // The annotations a reviewer leaves on an A3 landscape sheet (420 × 297 mm), in points, PDF's y up.
    static string Pts(params double[] mm) => string.Join(' ', mm.Select(v => F(v * Pt)));
    static string Y(double mm) => F((297 - mm) * Pt);

    /// <summary>A square from (100, 50) to (200, 120) mm on the paper.</summary>
    static readonly string Square = $"<< /Type /Annot /Subtype /Square /Rect [{Pts(100)} {Y(120)} {Pts(200)} {Y(50)}] /Contents (Move this wall) /T (Ana) /M (D:20261030101500+02'00') /Subj (Rectangle) /C [1 0 0] /NM (sq-1) >>";

    [Fact]
    public void Annotations_in_an_incremental_update_are_read_with_their_text_author_dates_and_threads()
    {
        var pdf = Pdf(PageObjects(420, 297));
        // The reviewer's save: the page replaced with its Annots, the annotations appended, a popup, a reply (IRT), a review state.
        var annotated = Update(pdf,
            (3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(420 * Pt)} {F(297 * Pt)}] /Contents 4 0 R /Annots [5 0 R 6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R 12 0 R 13 0 R] >>"),
            (5, Square),
            // A sticky note with UTF-16BE text (Romanian letters), the popup it opens.
            (6, $"<< /Type /Annot /Subtype /Text /Rect [{Pts(30)} {Y(40)} {Pts(36)} {Y(34)}] /Contents <FEFF0218006900200074006F0074002000610069006300690020021B> /T <FEFF0049006F006E0075021B> /CreationDate (D:20261030090000Z) /Popup 7 0 R /Open true >>"),
            (7, $"<< /Type /Annot /Subtype /Popup /Rect [0 0 100 100] /Parent 6 0 R >>"),
            // A reply to the square, and a review state (Completed) on it.
            (8, $"<< /Type /Annot /Subtype /Text /Rect [{Pts(100)} {Y(50)} {Pts(120)} {Y(30)}] /Contents (Done in P02) /T (Ionut) /M (D:20261031120000) /IRT 5 0 R /RT /R >>"),
            (9, $"<< /Type /Annot /Subtype /Text /Rect [0 0 20 20] /Contents (Completed set by Ana) /T (Ana) /M (D:20261101080000) /IRT 5 0 R /State /Completed /StateModel /Review >>"),
            // A highlight by quads, an ink stroke, a line, a strike-out and a Bluebeam cloud (a polygon with the cloud intent).
            (10, $"<< /Type /Annot /Subtype /Highlight /Rect [{Pts(250)} {Y(70)} {Pts(300)} {Y(60)}] /QuadPoints [{Pts(250)} {Y(60)} {Pts(300)} {Y(60)} {Pts(250)} {Y(70)} {Pts(300)} {Y(70)}] /Contents (Check this) /T (Ana) /C [1 1 0] >>"),
            (11, $"<< /Type /Annot /Subtype /Ink /Rect [{Pts(10)} {Y(200)} {Pts(60)} {Y(150)}] /InkList [[{Pts(10)} {Y(150)} {Pts(35)} {Y(175)} {Pts(60)} {Y(200)}]] /T (Ana) >>"),
            (12, $"<< /Type /Annot /Subtype /Line /Rect [{Pts(300)} {Y(250)} {Pts(400)} {Y(200)}] /L [{Pts(300)} {Y(200)} {Pts(400)} {Y(250)}] /IT /LineArrow /Contents (to the stair) /T (Ana) >>"),
            (13, $"<< /Type /Annot /Subtype /Polygon /Rect [{Pts(200)} {Y(260)} {Pts(260)} {Y(220)}] /Vertices [{Pts(200)} {Y(220)} {Pts(260)} {Y(220)} {Pts(230)} {Y(260)}] /IT /PolygonCloud /Contents (Cloud) /T (Bluebeam) >>"));

        var read = PdfAnnotations.Read(annotated);
        Assert.Empty(read.Warnings);
        Assert.Single(read.Pages);
        Assert.Equal(1, read.Skipped);   // the popup
        var kinds = read.Annotations.Select(a => a.Subtype).ToList();
        Assert.Equal(["Square", "Text", "Text", "Text", "Highlight", "Ink", "Line", "Polygon"], kinds);
        var square = read.Annotations[0];
        Assert.Equal("5:0", square.Key);
        Assert.Equal("sq-1", square.Name);
        Assert.Equal("Move this wall", square.Contents);
        Assert.Equal("Ana", square.Author);
        Assert.Equal("Rectangle", square.Subject);
        Assert.Equal("2026-10-30T10:15:00+02:00", square.Modified);
        Assert.Equal("#ff0000", square.Color);
        var note = read.Annotations[1];
        Assert.Equal("Și tot aici ț", note.Contents);
        Assert.Equal("Ionuț", note.Author);
        Assert.Equal("2026-10-30T09:00:00Z", note.Created);
        Assert.Equal("5:0", read.Annotations[2].InReplyTo);
        Assert.Equal("Completed", read.Annotations[3].State);
        Assert.Equal("LineArrow", read.Annotations[6].Intent);
        Assert.Equal("PolygonCloud", read.Annotations[7].Intent);

        // Onto the sheet: the square where it was drawn (mm, y down), the thread folded, the state read.
        var f = Fx();
        var s = Sheet(f);
        var r = CdeRemarks.FromPdf(f.Model, annotated, "PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf", null, null);
        Assert.Equal("name", (string)r["by"]!);
        Assert.Equal("P01", (string)r["revision"]!);
        Assert.Equal(s, (string)((JsonArray)r["sheets"]!)[0]!["sheetId"]!);
        var remarks = ((JsonArray)r["remarks"]!).OfType<JsonObject>().ToList();
        Assert.Equal(["rect", "note", "highlight", "ink", "arrow", "cloud"], remarks.Select(x => (string)x["kind"]!).ToList());
        var sq = remarks[0];
        Assert.Equal([100, 50, 200, 120], ((JsonArray)sq["rect"]!).Select(v => Math.Round((double)v!, 1)).ToArray());
        Assert.Equal(s, (string)sq["sheetId"]!);
        Assert.Equal("sq-1", (string)sq["source"]!["annot"]!);
        Assert.Equal("P01", (string)sq["source"]!["revision"]!);
        Assert.Equal("Completed", (string)sq["state"]!);
        var replies = (JsonArray)sq["replies"]!;
        Assert.Single(replies);
        Assert.Equal("Done in P02", (string)replies[0]!["text"]!);
        Assert.Equal("Ionut", (string)replies[0]!["author"]!);
        // The highlight's quad as a loop, the ink stroke's points, the arrow's two ends, the cloud's three vertices.
        Assert.Equal([250, 60, 300, 70], ((JsonArray)remarks[2]["rect"]!).Select(v => Math.Round((double)v!, 1)).ToArray());
        Assert.Equal(4, ((JsonArray)remarks[2]["paths"]![0]!).Count);
        var ink = (JsonArray)remarks[3]["paths"]![0]!;
        Assert.Equal(3, ink.Count);
        Assert.Equal(35, Math.Round((double)ink[1]![0]!, 1));
        Assert.Equal(175, Math.Round((double)ink[1]![1]!, 1));
        var arrow = (JsonArray)remarks[4]["paths"]![0]!;
        Assert.Equal(300, Math.Round((double)arrow[0]![0]!, 1));
        Assert.Equal(250, Math.Round((double)arrow[1]![1]!, 1));
        Assert.Equal(3, ((JsonArray)remarks[5]["paths"]![0]!).Count);
        // The note's name is its first line; the state marks the thread as resolved on import.
        Assert.Equal("Move this wall", (string)sq["text"]!);
    }

    [Fact]
    public void Reading_the_same_file_again_updates_the_remarks_instead_of_duplicating_them_and_keeps_their_status()
    {
        var f = Fx();
        var s = Sheet(f);
        var pdf = Update(Pdf(PageObjects(420, 297)),
            (3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(420 * Pt)} {F(297 * Pt)}] /Contents 4 0 R /Annots [5 0 R 6 0 R] >>"),
            (5, Square),
            (6, $"<< /Type /Annot /Subtype /Text /Rect [0 0 20 20] /Contents (Completed) /T (Ana) /IRT 5 0 R /State /Completed /StateModel /Review >>"));
        var r = CdeRemarks.FromPdf(f.Model, pdf, "PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf", null, null);
        var imported = (JsonObject)f.Run("cde.importRemarks", new JsonObject { ["remarks"] = r["remarks"]!.DeepClone() })!;
        Assert.Equal(1, (int)imported["added"]!);
        Assert.Equal(0, (int)imported["updated"]!);
        var remark = CdeRemarks.Of(f.Model).Single();
        Assert.Equal("Move this wall", remark.Name);
        Assert.Equal("resolved", remark.Str("status"));   // the reviewer marked it completed
        Assert.Equal(s, remark.Str("sheetId"));

        // The status changed here; the file read again (the reviewer edited the text): updated, not doubled, the status kept.
        f.Run("cde.setRemarkStatus", new JsonObject { ["id"] = remark.Id, ["status"] = "closed" });
        var edited = Update(pdf, (5, Square.Replace("Move this wall", "Move this wall 200 mm")));
        var r2 = CdeRemarks.FromPdf(f.Model, edited, "PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf", null, null);
        var again = (JsonObject)f.Run("cde.importRemarks", new JsonObject { ["remarks"] = r2["remarks"]!.DeepClone() })!;
        Assert.Equal(0, (int)again["added"]!);
        Assert.Equal(1, (int)again["updated"]!);
        var one = CdeRemarks.Of(f.Model).Single();
        Assert.Equal(remark.Id, one.Id);
        Assert.Equal("Move this wall 200 mm", one.Str("text"));
        Assert.Equal("closed", one.Str("status"));
        // The query lists it by sheet; undo takes the update back; delete removes it.
        Assert.Single((JsonArray)f.Session.Query("cde.remarks", new JsonObject { ["sheetId"] = s })!);
        f.Session.Undo();
        Assert.Equal("Move this wall", CdeRemarks.Of(f.Model).Single().Str("text"));
        f.Run("cde.deleteRemarks", new JsonObject { ["ids"] = new JsonArray(remark.Id) });
        Assert.Empty(CdeRemarks.Of(f.Model));
        Assert.Throws<CommandException>(() => f.Run("cde.setRemarkStatus", new JsonObject { ["id"] = remark.Id, ["status"] = "odd" }));
    }

    [Fact]
    public void An_xref_stream_with_an_object_stream_is_read_through_the_predictor()
    {
        // Objects 1–3 and the annotation 5 live in the object stream 4; the xref stream is 6, with /W [1 2 1] and PNG Up prediction.
        var members = new (int Num, string Body)[]
        {
            (1, "<< /Type /Catalog /Pages 2 0 R >>"),
            (2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            (3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(420 * Pt)} {F(297 * Pt)}] /Annots [5 0 R] >>"),
            (5, $"<< /Type /Annot /Subtype /FreeText /Rect [{Pts(50)} {Y(100)} {Pts(150)} {Y(80)}] /Contents (12.50 m \\(measured\\)) /T (Bluebeam) /Subj (Length Measurement) >>"),
        };
        var header = new StringBuilder();
        var bodies = new StringBuilder();
        foreach (var (num, body) in members) { header.Append($"{num} {bodies.Length} "); bodies.Append(body).Append('\n'); }
        var headerText = header.ToString();
        var objStmData = Encoding.Latin1.GetBytes(headerText + bodies);
        var objStm = Deflate(objStmData);

        using var f = new MemoryStream();
        void W(string s) => f.Write(Encoding.Latin1.GetBytes(s));
        W("%PDF-1.5\n");
        var at4 = f.Position;
        W($"4 0 obj\n<< /Type /ObjStm /N {members.Length} /First {headerText.Length} /Length {objStm.Length} /Filter /FlateDecode >>\nstream\n");
        f.Write(objStm);
        W("\nendstream\nendobj\n");
        var at6 = f.Position;
        // The rows: 0 free; 1–3 and 5 in stream 4 at their indices; 4 and 6 at their offsets. /W [1 2 1]: 4 bytes a row.
        byte[][] rows =
        [
            [0, 0, 0, 0],
            [2, 0, 4, 0], [2, 0, 4, 1], [2, 0, 4, 2],
            [1, (byte)(at4 >> 8), (byte)at4, 0],
            [2, 0, 4, 3],
            [1, (byte)(at6 >> 8), (byte)at6, 0],
        ];
        var predicted = new MemoryStream();
        var prev = new byte[4];
        foreach (var row in rows)
        {
            predicted.WriteByte(2);   // Up
            for (var i = 0; i < 4; i++) predicted.WriteByte((byte)(row[i] - prev[i]));
            prev = row;
        }
        var xrefData = Deflate(predicted.ToArray());
        W($"6 0 obj\n<< /Type /XRef /Size 7 /W [1 2 1] /Root 1 0 R /Filter /FlateDecode /DecodeParms << /Predictor 12 /Columns 4 >> /Length {xrefData.Length} >>\nstream\n");
        f.Write(xrefData);
        W($"\nendstream\nendobj\nstartxref\n{at6}\n%%EOF\n");

        var read = PdfAnnotations.Read(f.ToArray());
        Assert.Empty(read.Warnings);
        var a = Assert.Single(read.Annotations);
        Assert.Equal("FreeText", a.Subtype);
        Assert.Equal("12.50 m (measured)", a.Contents);
        Assert.Equal("Length Measurement", a.Subject);
        Assert.Equal("5:0", a.Key);
        // A Bluebeam measurement is a remark like any other.
        Assert.Equal("text", CdeRemarks.KindOf(a.Subtype, a.Intent));
    }

    [Fact]
    public void Text_strings_and_dates_decode_as_the_specification_says()
    {
        Assert.Equal("a(b)c\\d\nA", PdfText.Decode(Lit(@"a\(b\)c\\d\nA")));
        Assert.Equal("AB", PdfText.Decode(Lit(@"\101\102")));
        Assert.Equal("one two", PdfText.Decode(Lit("one \\\ntwo")));   // a continued line
        Assert.Equal("x\ny", PdfText.Decode(Lit("x\r\ny")));
        Assert.Equal("nested (paren) ok", PdfText.Decode(Lit("nested (paren) ok")));
        Assert.Equal("Hi", PdfText.Decode(new PdfLexer(Encoding.Latin1.GetBytes("<FEFF00480069>")).Parse() is PdfString s ? s.Bytes : []));
        Assert.Equal("é•—€", PdfText.Decode([0xE9, 0x80, 0x84, 0xA0]));   // PDFDocEncoding: é, a bullet, an em dash, the euro
        Assert.Equal("Ab", PdfText.Decode([0xEF, 0xBB, 0xBF, (byte)'A', (byte)'b']));
        Assert.Equal("P", PdfText.Decode(new PdfLexer(Encoding.Latin1.GetBytes("<5>")).Parse() is PdfString h ? h.Bytes : []));   // an odd digit
        Assert.Equal("2026-10-30T10:15:00+02:00", PdfText.Date("D:20261030101500+02'00'"));
        Assert.Equal("2026-10-30T09:00:00Z", PdfText.Date("D:20261030090000Z"));
        Assert.Equal("2026-10-30T00:00:00", PdfText.Date("20261030"));
        Assert.Equal("2026-01-01T00:00:00", PdfText.Date("D:2026"));
        Assert.Null(PdfText.Date("yesterday"));
        Assert.Null(PdfText.Date(""));
        // Names with #xx, references, numbers in the wild.
        var lx = new PdfLexer(Encoding.Latin1.GetBytes("<< /A#20B 12 0 R /N --5 /M 3. /Arr [1 2 R 3] >>"));
        var d = Assert.IsType<PdfDict>(lx.Parse());
        Assert.Equal(new PdfRef(12, 0), d["A B"]);
        Assert.Equal(-5, ((PdfNumber)d["N"]!).Value);
        Assert.Equal(3, ((PdfNumber)d["M"]!).Value);
        Assert.Equal([new PdfRef(1, 2), new PdfNumber(3)], ((PdfArray)d["Arr"]!).Items);
    }

    static byte[] Lit(string inside) => new PdfLexer(Encoding.Latin1.GetBytes($"({inside})")).Parse() is PdfString s ? s.Bytes : [];

    [Fact]
    public void The_page_maps_onto_the_paper_undoing_the_writer_with_rotation_and_another_size()
    {
        // Lapin's own page: A3 landscape at 1:1, no rotation. A point 100 mm from the left, 50 mm from the top.
        var page = new PdfPageInfo(0, 0, 0, 420 * Pt, 297 * Pt, 0);
        var map = new PdfPageMap(page, 420, 297);
        Assert.False(map.Scaled);
        var (x, y) = map.ToSheet(100 * Pt, (297 - 50) * Pt);
        Assert.Equal(100, x, 2);
        Assert.Equal(50, y, 2);
        // The same sheet printed through another tool as a portrait page turned by /Rotate 90: the user space is 297 wide, 420 high,
        // and the displayed top-left is the user space's bottom-left.
        var turned = new PdfPageMap(new PdfPageInfo(0, 0, 0, 297 * Pt, 420 * Pt, 90), 420, 297);
        Assert.False(turned.Scaled);
        Assert.Equal((420.0, 297.0), (Math.Round(turned.Displayed.W / Pt), Math.Round(turned.Displayed.H / Pt)));
        var (tx, ty) = turned.ToSheet(50 * Pt, 100 * Pt);   // 50 up from the bottom (x), 100 along (y) → 100 from the left, 50 from the top
        Assert.Equal(100, tx, 2);
        Assert.Equal(50, ty, 2);
        var (bx, by) = turned.ToSheet(0, 0);
        Assert.Equal((0.0, 0.0), (bx, by));
        var (ex, ey) = turned.ToSheet(297 * Pt, 420 * Pt);
        Assert.Equal(420, ex, 2);
        Assert.Equal(297, ey, 2);
        // 180 and 270 go round the other corners.
        var (hx, hy) = new PdfPageMap(new PdfPageInfo(0, 0, 0, 420 * Pt, 297 * Pt, 180), 420, 297).ToSheet(0, 0);
        Assert.Equal(420, hx, 2);
        Assert.Equal(0, hy, 2);
        var (qx, qy) = new PdfPageMap(new PdfPageInfo(0, 0, 0, 297 * Pt, 420 * Pt, 270), 420, 297).ToSheet(297 * Pt, 420 * Pt);
        Assert.Equal(0, qx, 2);
        Assert.Equal(0, qy, 2);
        // An A4 print of the A3 sheet: the remarks are scaled up onto the paper, and said so.
        var small = new PdfPageMap(new PdfPageInfo(0, 0, 0, 297 * Pt, 210 * Pt, 0), 420, 297);
        Assert.True(small.Scaled);
        var (sx, sy) = small.ToSheet(148.5 * Pt, 105 * Pt);
        Assert.Equal(210, sx, 1);
        Assert.Equal(148.5, sy, 1);
        // A CropBox offset: the displayed origin is the box's corner.
        var cropped = new PdfPageMap(new PdfPageInfo(0, 20, 30, 20 + 420 * Pt, 30 + 297 * Pt, 0), 420, 297);
        var (cx, cy) = cropped.ToSheet(20, 30 + 297 * Pt);
        Assert.Equal((0.0, 0.0), (cx, cy));
    }

    [Fact]
    public void The_writers_own_PDF_reads_back_with_a_square_where_it_was_drawn()
    {
        var f = Fx();
        f.Wall(0, 0, 5, 0);
        var s = Sheet(f);
        var printed = Sheets.Pdf(f.Session.Reader, [f.Model.Get(s)!]);
        // The writer's first page is object 5 (catalog, pages, two fonts, then the page): a reviewer's update puts the square on it.
        var pages = PdfDocument.Load(printed).Pages();
        Assert.Single(pages);
        Assert.Equal(420 * Pt, pages[0].MediaBox[2], 1);
        var text = Encoding.Latin1.GetString(printed);
        var pageObj = System.Text.RegularExpressions.Regex.Match(text, @"(\d+) 0 obj\n<< /Type /Page ").Groups[1].Value;
        var pageBody = System.Text.RegularExpressions.Regex.Match(text, @"\d+ 0 obj\n(<< /Type /Page .*?>>)\nendobj", System.Text.RegularExpressions.RegexOptions.Singleline).Groups[1].Value;
        var annotated = Update(printed, (int.Parse(pageObj, CultureInfo.InvariantCulture), pageBody[..^2] + " /Annots [50 0 R] >>"), (50, Square));
        var r = CdeRemarks.FromPdf(f.Model, annotated, "anything.pdf", null, s);
        Assert.Equal("chosen", (string)r["by"]!);
        Assert.Empty((JsonArray)r["warnings"]!);
        var remark = ((JsonArray)r["remarks"]!).OfType<JsonObject>().Single();
        Assert.Equal([100, 50, 200, 120], ((JsonArray)remark["rect"]!).Select(v => Math.Round((double)v!, 1)).ToArray());
    }

    [Fact]
    public void A_file_is_matched_to_its_sheet_by_the_issue_record_the_manifest_the_name_or_the_choice()
    {
        var f = Fx();
        var a = Sheet(f, "A-101", "Ground floor plan");
        var b = Sheet(f, "A-102", "First floor plan");
        var m = f.Model;
        // The project's record of an issue: the sheet's file, the set, the model.
        f.Run("cde.recordIssue", new JsonObject
        {
            ["name"] = "Stage 2", ["revision"] = "P01", ["status"] = "S2", ["container"] = "Shared",
            ["files"] = new JsonArray(
                new JsonObject { ["name"] = "PRJ-ORG-ZZ-00-DR-A-0101_S2_P01.pdf", ["kind"] = "pdf", ["sheetId"] = a, ["sha256"] = "x", ["size"] = 1 },
                new JsonObject { ["name"] = "PRJ-ORG-ZZ-00-DR-A-0102_S2_P01.pdf", ["kind"] = "pdf", ["sheetId"] = b, ["sha256"] = "x", ["size"] = 1 },
                new JsonObject { ["name"] = "PRJ-ORG-ZZ-ZZ-DR-A-0000_S2_P01.pdf", ["kind"] = "pdf", ["sha256"] = "x", ["size"] = 1 },
                new JsonObject { ["name"] = "PRJ-ORG-ZZ-ZZ-M3-A-0001_S2_P01.ifc", ["kind"] = "ifc", ["sha256"] = "x", ["size"] = 1 }),
        });
        var byIssue = CdeRemarks.Match(m, "PRJ-ORG-ZZ-00-DR-A-0102_S2_P01.pdf", 1, null, null);
        Assert.Equal(("issue", "P01"), (byIssue.By, byIssue.Revision));
        Assert.Equal([b], byIssue.PageSheets);
        // The set's pages go to the issue's sheets in order; a page beyond them has none.
        var set = CdeRemarks.Match(m, "PRJ-ORG-ZZ-ZZ-DR-A-0000_S2_P01.pdf", 3, null, null);
        Assert.Equal([a, b, null], set.PageSheets);
        Assert.Equal("issue", set.By);
        // A file the project never recorded, but a manifest beside it lists (the folder came from another computer).
        var manifest = new List<ManifestFile> { new("other.pdf", b, "P03", "m1"), new("other-set.pdf", null, "P03", "m1"), new("other-a.pdf", a, "P03", "m1") };
        var byManifest = CdeRemarks.Match(m, "OTHER.pdf", 1, manifest, null);
        Assert.Equal(("manifest", "P03", b), (byManifest.By, byManifest.Revision, byManifest.PageSheets[0]));
        Assert.Equal([b, a], CdeRemarks.Match(m, "other-set.pdf", 2, manifest, null).PageSheets);
        // The convention alone: the stem, else the number field; the revision from the name.
        var byName = CdeRemarks.Match(m, "PRJ-ORG-ZZ-00-DR-A-0101_S3_P02.pdf", 1, null, null);
        Assert.Equal(("name", "P02", a), (byName.By, byName.Revision, byName.PageSheets[0]));
        Assert.Equal(b, CdeRemarks.Match(m, "XX-YYY-ZZ-01-DR-S-0102_S3_P02.pdf", 1, null, null).PageSheets[0]);
        // Nothing fits: the user picks; the choice applies to every page.
        var none = CdeRemarks.Match(m, "scan.pdf", 2, null, null);
        Assert.Equal(("none", null), (none.By, none.PageSheets[0]));
        Assert.Equal([a, a], CdeRemarks.Match(m, "scan.pdf", 2, null, a).PageSheets);
    }

    [Fact]
    public void A_broken_file_yields_what_can_be_read_and_a_warning_never_an_exception()
    {
        // Garbage, an empty file, a file with no xref, a file whose xref offsets are wrong, a truncated one.
        foreach (var bytes in new[] { Encoding.Latin1.GetBytes("not a pdf at all"), Array.Empty<byte>(), new byte[] { 0x25, 0x50, 0x44, 0x46 } })
        {
            var r = PdfAnnotations.Read(bytes);
            Assert.Empty(r.Annotations);
            Assert.NotEmpty(r.Warnings);
        }
        var good = Update(Pdf(PageObjects(420, 297)),
            (3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(420 * Pt)} {F(297 * Pt)}] /Contents 4 0 R /Annots [5 0 R] >>"),
            (5, Square));
        // No xref at all: scanned, the newest page (with its Annots) wins.
        var text = Encoding.Latin1.GetString(good);
        var noXref = Encoding.Latin1.GetBytes(System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^xref\n[\s\S]*?(?=trailer)", "").Replace("startxref", "startxxxx"));
        var scanned = PdfAnnotations.Read(noXref);
        Assert.True(scanned.Annotations.Count == 1, $"annotations {scanned.Annotations.Count}, pages {scanned.Pages.Count}: {string.Join(" | ", scanned.Warnings)}");
        Assert.Contains(scanned.Warnings, w => w.Contains("scanned"));
        // Offsets shifted by junk at the front: the xref lies, the file is scanned.
        var shifted = new byte[] { (byte)'a', (byte)'b' }.Concat(good).ToArray();
        var fixedUp = PdfAnnotations.Read(shifted);
        Assert.Single(fixedUp.Annotations);
        Assert.Contains(fixedUp.Warnings, w => w.Contains("scanned"));
        // Truncated in the middle of the update: the older page (no annotations) is what the scan finds, and no exception.
        var cut = PdfAnnotations.Read(good[..(good.Length - 120)]);
        Assert.NotNull(cut);
        // FromPdf on garbage with a sheet chosen: no remarks, warnings, the sheet listed for no page.
        var f = Fx();
        var s = Sheet(f);
        var r2 = CdeRemarks.FromPdf(f.Model, Encoding.Latin1.GetBytes("%PDF-1.7 junk"), "x.pdf", null, s);
        Assert.Empty((JsonArray)r2["remarks"]!);
        Assert.NotEmpty((JsonArray)r2["warnings"]!);
        // A page of another size than the sheet's paper says so.
        var a4 = Update(Pdf(PageObjects(297, 210)),
            (3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(297 * Pt)} {F(210 * Pt)}] /Contents 4 0 R /Annots [5 0 R] >>"),
            (5, Square));
        var r3 = CdeRemarks.FromPdf(f.Model, a4, "x.pdf", null, s);
        Assert.Contains(((JsonArray)r3["warnings"]!).Select(w => (string)w!), w => w.Contains("scaled"));
        Assert.Single((JsonArray)r3["remarks"]!);
    }

    [Fact]
    public void The_host_reads_a_file_by_path_with_the_manifests_beside_it_and_scans_the_folder()
    {
        var dir = Directory.CreateTempSubdirectory("lapin-remarks-");
        try
        {
            var f = Fx();
            var s = Sheet(f);
            var shared = Path.Combine(dir.FullName, "Shared");
            Directory.CreateDirectory(shared);
            Directory.CreateDirectory(Path.Combine(dir.FullName, "Published"));
            var pdf = Update(Pdf(PageObjects(420, 297)),
                (3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(420 * Pt)} {F(297 * Pt)}] /Contents 4 0 R /Annots [5 0 R] >>"),
                (5, Square));
            File.WriteAllBytes(Path.Combine(shared, "reviewed.pdf"), pdf);
            File.WriteAllBytes(Path.Combine(shared, "transmittal.pdf"), Pdf(PageObjects(210, 297)));
            File.WriteAllText(Path.Combine(shared, "issue.json"), new JsonObject
            {
                ["format"] = "lapin-issue/1", ["revision"] = "P05", ["transmittal"] = "transmittal.pdf",
                ["files"] = new JsonArray(new JsonObject { ["name"] = "reviewed.pdf", ["kind"] = "pdf", ["sheetId"] = s }, new JsonObject { ["name"] = "model.ifc", ["kind"] = "ifc" }),
            }.ToJsonString());
            File.WriteAllText(Path.Combine(shared, "notes.json"), "{\"hello\": 1}");
            f.Run("cde.setFolder", new JsonObject { ["path"] = dir.FullName });

            var dispatcher = new EngineDispatcher(f.Session, PackageFiles.Create());
            JsonObject Call(string method, JsonObject p)
            {
                var (reply, _) = dispatcher.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
                var o = (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(reply))!;
                if (o["error"] is { } err) throw new InvalidOperationException((string)err!);
                return o;
            }
            var read = (JsonObject)Call("cde.readPdf", new JsonObject { ["path"] = Path.Combine(shared, "reviewed.pdf") })["result"]!;
            Assert.Equal("manifest", (string)read["by"]!);
            Assert.Equal("P05", (string)read["revision"]!);
            Assert.Equal(s, (string)((JsonArray)read["remarks"]!)[0]!["sheetId"]!);
            // By its bytes, as a browser sends them: the same, matched by the user's choice.
            var byData = (JsonObject)Call("cde.readPdf", new JsonObject { ["data"] = Convert.ToBase64String(pdf), ["name"] = "scan.pdf", ["sheetId"] = s })["result"]!;
            Assert.Equal("chosen", (string)byData["by"]!);
            Assert.Single((JsonArray)byData["remarks"]!);
            // The scan: the issued sheet's PDF (newer than any issue the project knows, never read), not the transmittal nor the stray JSON.
            var scan = (JsonArray)Call("cde.scanRemarks", [])["result"]!;
            var one = Assert.Single(scan.OfType<JsonObject>());
            Assert.Equal("reviewed.pdf", (string)one["name"]!);
            Assert.Equal("Shared", (string)one["container"]!);
            Assert.Equal(s, (string)one["sheetId"]!);
            Assert.True((bool)one["unread"]!);
            // Once read into the project, it is no longer unread.
            f.Run("cde.importRemarks", new JsonObject { ["remarks"] = read["remarks"]!.DeepClone() });
            var after = (JsonArray)Call("cde.scanRemarks", [])["result"]!;
            Assert.False((bool)after[0]!["unread"]!);
            Assert.NotNull((string?)after[0]!["read"]);
            // A guest cannot read the host's files.
            Assert.Equal("A guest cannot reach files on the host's computer.", EngineDispatcher.AllowedRemote("cde.readPdf", new JsonObject { ["path"] = "/x.pdf" }, "editor"));
            Assert.Equal("Only the host can do that.", EngineDispatcher.AllowedRemote("cde.scanRemarks", [], "editor"));
        }
        finally { dir.Delete(recursive: true); }
    }
}
