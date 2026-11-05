using System.Globalization;
using System.IO.Compression;
using System.Text;
using static Lapin.Drawing.Documents.DocText;

namespace Lapin.Drawing.Documents;

/// <summary>
/// The document as an ODT (OpenDocument Text, OASIS ODF 1.3), no library; pictures by key (their SVG and PNG), a picture
/// without one left out (its caption kept). The package's <c>mimetype</c> first and stored, then content, styles, meta and
/// the manifest; the styles by ODF's own names (Heading_20_1–3, Text_20_body, Caption, Table_20_Contents, List,
/// Contents_20_1–2, Header, Footer); a picture is a frame holding its SVG and, as the fallback, its PNG; the table of
/// contents a text:table-of-content prefilled with the PDF layout's pages; the header and footer count the pages.
/// </summary>
public static class OdtWriter
{
    const string Ns = "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\""
        + " xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\""
        + " xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\""
        + " xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\""
        + " xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\"";
    const string Head = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n";

    static string Mm(double v) => v.ToString("0.###", CultureInfo.InvariantCulture) + "mm";
    static string Pt(double v) => v.ToString("0.##", CultureInfo.InvariantCulture) + "pt";

    public static byte[] Write(FilledDocument doc, IReadOnlyDictionary<string, DocPicture>? pictures = null)
    {
        var w = new Writer(doc, pictures);
        var body = w.Body();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // First and stored, so a reader knows the package by its first bytes.
            Put(zip, "mimetype", Encoding.ASCII.GetBytes("application/vnd.oasis.opendocument.text"), store: true);
            Put(zip, "content.xml", Head + $"<office:document-content {Ns} office:version=\"1.3\">"
                + FontFaces(DocLook.Of(doc.Page))
                + "<office:automatic-styles>" + w.Automatic() + "</office:automatic-styles>"
                + "<office:body><office:text>" + body + "</office:text></office:body></office:document-content>");
            Put(zip, "styles.xml", Styles(doc));
            Put(zip, "meta.xml", Meta(doc));
            var manifest = new StringBuilder(Head + "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">"
                + "<manifest:file-entry manifest:full-path=\"/\" manifest:version=\"1.3\" manifest:media-type=\"application/vnd.oasis.opendocument.text\"/>"
                + "<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>"
                + "<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/>"
                + "<manifest:file-entry manifest:full-path=\"meta.xml\" manifest:media-type=\"text/xml\"/>");
            if (DocLook.Of(doc.Page).LogoBox(doc.Page) != null && DocLook.Of(doc.Page).Logo is { } logo)
            {
                Put(zip, $"Pictures/logo.{logo.Extension}", logo.Data, store: true);
                manifest.Append($"<manifest:file-entry manifest:full-path=\"Pictures/logo.{logo.Extension}\" manifest:media-type=\"{logo.Mime}\"/>");
            }
            foreach (var (path, type, data) in w.Media)
            {
                Put(zip, path, data, store: type == "image/png");
                manifest.Append($"<manifest:file-entry manifest:full-path=\"{path}\" manifest:media-type=\"{type}\"/>");
            }
            Put(zip, "META-INF/manifest.xml", manifest.Append("</manifest:manifest>").ToString());
        }
        return ms.ToArray();
    }

    /// <summary>The fonts the styles name: Arial always (Lapin's), and the style's own when it is another.</summary>
    static string FontFaces(DocLook look)
    {
        var b = new StringBuilder("<office:font-face-decls><style:font-face style:name=\"Arial\" svg:font-family=\"Arial\" style:font-family-generic=\"swiss\" style:font-pitch=\"variable\"/>");
        if (look.Font != "Arial")
            b.Append($"<style:font-face style:name=\"{Xml(look.Font)}\" svg:font-family=\"&apos;{Xml(look.Font)}&apos;\" style:font-family-generic=\"{(look.Times ? "roman" : "swiss")}\" style:font-pitch=\"variable\"/>");
        return b.Append("</office:font-face-decls>").ToString();
    }

    static string Meta(FilledDocument doc)
    {
        var m = doc.Meta;
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        var b = new StringBuilder(Head + "<office:document-meta xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\""
            + " xmlns:dc=\"http://purl.org/dc/elements/1.1/\" office:version=\"1.3\"><office:meta><meta:generator>Lapin</meta:generator>");
        b.Append("<dc:title>").Append(Xml(doc.Title)).Append("</dc:title>");
        if (m.Subject.Length > 0) b.Append("<dc:subject>").Append(Xml(m.Subject)).Append("</dc:subject>");
        if (m.Author.Length > 0) b.Append("<meta:initial-creator>").Append(Xml(m.Author)).Append("</meta:initial-creator><dc:creator>").Append(Xml(m.Author)).Append("</dc:creator>");
        b.Append($"<meta:creation-date>{now}</meta:creation-date><dc:date>{now}</dc:date>");
        b.Append("<dc:language>").Append(Xml(m.Lang.Length > 0 ? m.Lang : "ro")).Append("</dc:language>");
        if (m.Revision.Length > 0) b.Append("<meta:user-defined meta:name=\"Revision\">").Append(Xml(m.Revision)).Append("</meta:user-defined>");
        if (m.Status.Length > 0) b.Append("<meta:user-defined meta:name=\"Status\">").Append(Xml(m.Status)).Append("</meta:user-defined>");
        return b.Append("</office:meta></office:document-meta>").ToString();
    }

    static string Styles(FilledDocument doc)
    {
        var p = doc.Page;
        var look = DocLook.Of(p);
        var logo = look.LogoBox(p);
        var color = look.HeadingColor is { Length: 7 } hc ? $" fo:color=\"{hc.ToLowerInvariant()}\"" : "";
        var (lang, country) = Language(doc.Meta.Lang);
        var tw = p.TextWidth;
        string Para(string name, string display, string parent, string ppr, string tpr, string extra = "") =>
            $"<style:style style:name=\"{name}\"{(display.Length > 0 ? $" style:display-name=\"{display}\"" : "")} style:family=\"paragraph\"{(parent.Length > 0 ? $" style:parent-style-name=\"{parent}\"" : "")}{extra}>"
            + (ppr.Length > 0 ? $"<style:paragraph-properties {ppr}/>" : "") + (tpr.Length > 0 ? $"<style:text-properties {tpr}/>" : "") + "</style:style>";
        string Heading(int level, double before) => Para($"Heading_20_{level}", $"Heading {level}", "Heading", $"fo:margin-top=\"{Mm(before)}\" fo:margin-bottom=\"2.2mm\"",
            $"fo:font-size=\"{Pt(look.Heading(level))}\" fo:font-weight=\"bold\" style:font-weight-complex=\"bold\"{color}",
            $" style:next-style-name=\"Text_20_body\" style:default-outline-level=\"{level}\" style:class=\"text\"");
        string Tabbed(string name, string display, double indent, bool bold) =>
            $"<style:style style:name=\"{name}\" style:display-name=\"{display}\" style:family=\"paragraph\" style:parent-style-name=\"Index\" style:class=\"index\">"
            + $"<style:paragraph-properties fo:margin-left=\"{Mm(indent)}\" fo:margin-right=\"0mm\" fo:text-indent=\"0mm\" fo:margin-bottom=\"1mm\"><style:tab-stops>"
            + $"<style:tab-stop style:position=\"{Mm(tw - indent)}\" style:type=\"right\" style:leader-style=\"dotted\" style:leader-text=\".\"/></style:tab-stops></style:paragraph-properties>"
            + (bold ? "<style:text-properties fo:font-weight=\"bold\"/>" : "") + "</style:style>";

        var b = new StringBuilder(Head + $"<office:document-styles {Ns} office:version=\"1.3\">");
        b.Append(FontFaces(look));
        b.Append("<office:styles>");
        b.Append($"<style:default-style style:family=\"paragraph\"><style:paragraph-properties fo:orphans=\"2\" fo:widows=\"2\"/><style:text-properties style:font-name=\"{Xml(look.Font)}\" fo:font-size=\"{Pt(look.Body)}\" fo:language=\"{lang}\" fo:country=\"{country}\"/></style:default-style>");
        b.Append("<style:default-style style:family=\"table\"><style:table-properties table:border-model=\"collapsing\"/></style:default-style>");
        b.Append(Para("Standard", "", "", "", "", " style:class=\"text\""));
        b.Append(Para("Text_20_body", "Text body", "Standard", "fo:margin-top=\"0mm\" fo:margin-bottom=\"2.5mm\" fo:line-height=\"115%\"", "", " style:class=\"text\""));
        b.Append(Para("Heading", "", "Standard", "fo:keep-with-next=\"always\" fo:margin-top=\"4mm\" fo:margin-bottom=\"2mm\"", "fo:font-weight=\"bold\"", " style:next-style-name=\"Text_20_body\" style:class=\"text\""));
        b.Append(Heading(1, 7)).Append(Heading(2, 5)).Append(Heading(3, 4));
        b.Append(Para("Title", "", "Heading", "fo:text-align=\"center\"", "fo:font-size=\"22pt\" fo:font-weight=\"bold\"", " style:class=\"chapter\""));
        b.Append(Para("Caption", "", "Standard", "fo:margin-top=\"1.5mm\" fo:margin-bottom=\"3.5mm\"", $"fo:font-size=\"{Pt(look.Caption)}\" fo:font-style=\"italic\"", " style:class=\"extra\""));
        b.Append(Para("Note", "", "Text_20_body", "", $"fo:font-size=\"{Pt(look.Note)}\" fo:color=\"#555555\"", " style:class=\"text\""));
        b.Append(Para("List", "", "Text_20_body", "fo:margin-bottom=\"1mm\"", "", " style:class=\"list\""));
        b.Append(Para("Table_20_Contents", "Table Contents", "Standard", "fo:margin-top=\"0.3mm\" fo:margin-bottom=\"0.3mm\"", $"fo:font-size=\"{Pt(look.Table)}\"", " style:class=\"extra\""));
        b.Append(Para("Table_20_Heading", "Table Heading", "Table_20_Contents", "", "fo:font-weight=\"bold\"", " style:class=\"extra\""));
        b.Append(Para("Index", "", "Standard", "", "", " style:class=\"index\""));
        b.Append(Para("Contents_20_Heading", "Contents Heading", "Heading", "fo:margin-top=\"4mm\" fo:margin-bottom=\"3mm\"", $"fo:font-size=\"{Pt(look.Heading(1))}\" fo:font-weight=\"bold\"", " style:class=\"index\""));
        b.Append(Tabbed("Contents_20_1", "Contents 1", 0, true)).Append(Tabbed("Contents_20_2", "Contents 2", 6, false));
        b.Append(Para("Header", "", "Standard", "", $"fo:font-size=\"{Pt(look.Header)}\" fo:color=\"#555555\"", " style:class=\"extra\""));
        b.Append(Para("Footer", "", "Standard", "fo:text-align=\"center\"", $"fo:font-size=\"{Pt(look.Header)}\" fo:color=\"#555555\"", " style:class=\"extra\""));
        b.Append("<text:outline-style style:name=\"Outline\">");
        for (var l = 1; l <= 10; l++) b.Append($"<text:outline-level-style text:level=\"{l}\" style:num-format=\"\"/>");
        b.Append("</text:outline-style>");
        b.Append("</office:styles>");

        // The body starts at the margins as the PDF's does: the header in the top margin, the footer in the bottom one.
        var hasHeader = p.Header.Count > 0 || logo != null;
        var hasFooter = p.Footer.Count > 0;
        var band = look.Header * 1.2 * 25.4 / 72;
        var headerBand = logo is { } lb ? Math.Max(band, lb.H) : band;
        var top = hasHeader ? DocLook.HeaderTop(p) : p.Top;
        var bottom = hasFooter ? Math.Max(5, p.Bottom * 0.4) : p.Bottom;
        b.Append("<office:automatic-styles><style:page-layout style:name=\"pm1\">")
         .Append($"<style:page-layout-properties fo:page-width=\"{Mm(p.W)}\" fo:page-height=\"{Mm(p.H)}\" style:print-orientation=\"{(p.W > p.H ? "landscape" : "portrait")}\"")
         .Append($" fo:margin-top=\"{Mm(top)}\" fo:margin-bottom=\"{Mm(bottom)}\" fo:margin-left=\"{Mm(p.Left)}\" fo:margin-right=\"{Mm(p.Right)}\"/>")
         .Append(hasHeader ? $"<style:header-style><style:header-footer-properties fo:min-height=\"0mm\" fo:margin-bottom=\"{Mm(Math.Max(1, p.Top - top - headerBand))}\" style:dynamic-spacing=\"false\"/></style:header-style>" : "<style:header-style/>")
         .Append(hasFooter ? $"<style:footer-style><style:header-footer-properties fo:min-height=\"0mm\" fo:margin-top=\"{Mm(Math.Max(1, p.Bottom - bottom - band))}\" style:dynamic-spacing=\"false\"/></style:footer-style>" : "<style:footer-style/>")
         .Append("</style:page-layout>");
        if (logo is { } box)
        {
            // The logo's frame standing on the line (as the body's pictures), and the header's tab: after the logo on the left, at the text's edge on the right.
            b.Append("<style:style style:name=\"frLogo\" style:family=\"graphic\"><style:graphic-properties style:vertical-pos=\"top\" style:vertical-rel=\"baseline\" fo:border=\"none\" fo:padding=\"0mm\"/></style:style>")
             .Append("<style:style style:name=\"P_header_logo\" style:family=\"paragraph\" style:parent-style-name=\"Header\"><style:paragraph-properties><style:tab-stops>")
             .Append(look.Logo!.Right ? $"<style:tab-stop style:position=\"{Mm(tw)}\" style:type=\"right\"/>" : $"<style:tab-stop style:position=\"{Mm(box.W + DocLook.LogoGap)}\"/>")
             .Append("</style:tab-stops></style:paragraph-properties></style:style>");
        }
        b.Append("</office:automatic-styles>");
        b.Append("<office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"pm1\">");
        if (logo is { } frame)
        {
            var image = $"<draw:frame draw:style-name=\"frLogo\" draw:name=\"Logo\" text:anchor-type=\"as-char\" svg:width=\"{Mm(frame.W)}\" svg:height=\"{Mm(frame.H)}\" draw:z-index=\"0\">"
                + $"<draw:image xlink:href=\"Pictures/logo.{look.Logo!.Extension}\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\" draw:mime-type=\"{look.Logo.Mime}\"/></draw:frame>";
            var runs = Writer.Runs(p.Header);
            b.Append("<style:header><text:p text:style-name=\"P_header_logo\">")
             .Append(look.Logo.Right ? runs + "<text:tab/>" + image : image + "<text:tab/>" + runs).Append("</text:p></style:header>");
        }
        else if (hasHeader) b.Append("<style:header><text:p text:style-name=\"Header\">").Append(Writer.Runs(p.Header)).Append("</text:p></style:header>");
        if (hasFooter) b.Append("<style:footer><text:p text:style-name=\"Footer\">").Append(Writer.Runs(p.Footer)).Append("</text:p></style:footer>");
        b.Append("</style:master-page></office:master-styles></office:document-styles>");
        return b.ToString();
    }

    static (string Lang, string Country) Language(string lang)
    {
        var parts = (lang.Length > 0 ? lang : "ro").Split('-', '_');
        var l = parts[0].ToLowerInvariant();
        var c = parts.Length > 1 ? parts[1].ToUpperInvariant() : l switch { "ro" => "RO", "en" => "GB", "fr" => "FR", "de" => "DE", "it" => "IT", "es" => "ES", "hu" => "HU", _ => l.ToUpperInvariant() };
        return (Xml(l), Xml(c));
    }

    sealed class Writer(FilledDocument doc, IReadOnlyDictionary<string, DocPicture>? pictures)
    {
        public readonly List<(string Path, string Type, byte[] Data)> Media = [];
        readonly StringBuilder tableStyles = new();
        int tables, frames;

        /// <summary>Text for ODF: runs of spaces as text:s, tabs and line breaks as their elements.</summary>
        static string Text(string s)
        {
            var b = new StringBuilder();
            var spaces = 0;
            void FlushSpaces(bool atStart)
            {
                if (spaces == 0) return;
                // One space stays a space, unless at the start (ODF collapses it); the rest are counted.
                if (atStart) b.Append(spaces == 1 ? "<text:s/>" : $"<text:s text:c=\"{spaces}\"/>");
                else { b.Append(' '); if (spaces > 1) b.Append(spaces == 2 ? "<text:s/>" : $"<text:s text:c=\"{spaces - 1}\"/>"); }
                spaces = 0;
            }
            foreach (var ch in s.Replace("\r", ""))
            {
                if (ch == ' ') { spaces++; continue; }
                FlushSpaces(b.Length == 0);
                if (ch == '\t') b.Append("<text:tab/>");
                else if (ch == '\n') b.Append("<text:line-break/>");
                else b.Append(Xml(ch.ToString()));
            }
            FlushSpaces(b.Length == 0);
            return b.ToString();
        }

        /// <summary>Runs as spans (T_b, T_i, T_bi), a page field as text:page-number or text:page-count, a missing field as nothing.</summary>
        public static string Runs(IEnumerable<DocRun> runs, bool bold = false)
        {
            var b = new StringBuilder();
            foreach (var r in runs)
            {
                if (r.Missing) continue;
                var style = (bold || r.Bold, r.Italic) switch { (true, true) => "T_bi", (true, false) => "T_b", (false, true) => "T_i", _ => null };
                var inner = r.Field switch
                {
                    "page" => "<text:page-number text:select-page=\"current\">1</text:page-number>",
                    "pages" => "<text:page-count>1</text:page-count>",
                    _ => Text(r.Text ?? ""),
                };
                if (inner.Length == 0) continue;
                b.Append(style != null ? $"<text:span text:style-name=\"{style}\">{inner}</text:span>" : inner);
            }
            return b.ToString();
        }

        public string Automatic()
        {
            var b = new StringBuilder();
            b.Append("<style:style style:name=\"T_b\" style:family=\"text\"><style:text-properties fo:font-weight=\"bold\" style:font-weight-complex=\"bold\"/></style:style>");
            b.Append("<style:style style:name=\"T_i\" style:family=\"text\"><style:text-properties fo:font-style=\"italic\" style:font-style-complex=\"italic\"/></style:style>");
            b.Append("<style:style style:name=\"T_bi\" style:family=\"text\"><style:text-properties fo:font-weight=\"bold\" fo:font-style=\"italic\"/></style:style>");
            b.Append("<style:style style:name=\"P_num\" style:family=\"paragraph\" style:parent-style-name=\"Table_20_Contents\"><style:paragraph-properties fo:text-align=\"end\"/></style:style>");
            b.Append("<style:style style:name=\"P_head_num\" style:family=\"paragraph\" style:parent-style-name=\"Table_20_Heading\"><style:paragraph-properties fo:text-align=\"end\"/></style:style>");
            b.Append("<style:style style:name=\"P_picture\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"><style:paragraph-properties fo:text-align=\"center\" fo:keep-with-next=\"always\" fo:margin-bottom=\"1mm\"/></style:style>");
            b.Append("<style:style style:name=\"P_caption\" style:family=\"paragraph\" style:parent-style-name=\"Caption\"><style:paragraph-properties fo:text-align=\"center\"/></style:style>");
            b.Append("<style:style style:name=\"P_title\" style:family=\"paragraph\" style:parent-style-name=\"Caption\"><style:paragraph-properties fo:keep-with-next=\"always\" fo:margin-bottom=\"1.5mm\"/><style:text-properties fo:font-style=\"normal\" fo:font-weight=\"bold\" fo:font-size=\"" + Pt(DocLook.Of(doc.Page).Body) + "\"/></style:style>");
            b.Append("<style:style style:name=\"P_break\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"><style:paragraph-properties fo:break-before=\"page\"/></style:style>");
            b.Append("<style:style style:name=\"P_after\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"><style:paragraph-properties fo:margin-bottom=\"1mm\"/><style:text-properties fo:font-size=\"4pt\"/></style:style>");
            const string border = "0.5pt solid #000000";
            b.Append($"<style:style style:name=\"C_grid\" style:family=\"table-cell\"><style:table-cell-properties fo:padding-left=\"1.5mm\" fo:padding-right=\"1.5mm\" fo:padding-top=\"0.9mm\" fo:padding-bottom=\"0.9mm\" fo:border=\"{border}\"/></style:style>");
            b.Append($"<style:style style:name=\"C_head\" style:family=\"table-cell\"><style:table-cell-properties fo:padding-left=\"1.5mm\" fo:padding-right=\"1.5mm\" fo:padding-top=\"0.9mm\" fo:padding-bottom=\"0.9mm\" fo:border=\"{border}\" fo:background-color=\"#e6e6e6\"/></style:style>");
            b.Append($"<style:style style:name=\"C_total\" style:family=\"table-cell\"><style:table-cell-properties fo:padding-left=\"1.5mm\" fo:padding-right=\"1.5mm\" fo:padding-top=\"0.9mm\" fo:padding-bottom=\"0.9mm\" fo:border-top=\"1.2pt solid #000000\" fo:border-left=\"{border}\" fo:border-right=\"{border}\" fo:border-bottom=\"{border}\"/></style:style>");
            b.Append("<style:style style:name=\"C_plain\" style:family=\"table-cell\"><style:table-cell-properties fo:padding-left=\"1.5mm\" fo:padding-right=\"1.5mm\" fo:padding-top=\"0.6mm\" fo:padding-bottom=\"0.6mm\" fo:border=\"none\"/></style:style>");
            // A row is never split between pages (the PDF and the DOCX keep them whole too).
            b.Append("<style:style style:name=\"R_keep\" style:family=\"table-row\"><style:table-row-properties fo:keep-together=\"always\"/></style:style>");
            b.Append("<style:style style:name=\"fr1\" style:family=\"graphic\"><style:graphic-properties style:vertical-pos=\"top\" style:vertical-rel=\"baseline\" fo:border=\"none\" fo:padding=\"0mm\"/></style:style>");
            b.Append("<style:style style:name=\"Sect1\" style:family=\"section\"><style:section-properties style:editable=\"false\"/></style:style>");
            b.Append("<text:list-style style:name=\"L_bullet\"><text:list-level-style-bullet text:level=\"1\" text:bullet-char=\"•\">")
             .Append("<style:list-level-properties text:list-level-position-and-space-mode=\"label-alignment\"><style:list-level-label-alignment text:label-followed-by=\"listtab\" text:list-tab-stop-position=\"6mm\" fo:text-indent=\"-6mm\" fo:margin-left=\"6mm\"/></style:list-level-properties>")
             .Append("</text:list-level-style-bullet></text:list-style>");
            b.Append("<text:list-style style:name=\"L_number\"><text:list-level-style-number text:level=\"1\" style:num-suffix=\".\" style:num-format=\"1\">")
             .Append("<style:list-level-properties text:list-level-position-and-space-mode=\"label-alignment\"><style:list-level-label-alignment text:label-followed-by=\"listtab\" text:list-tab-stop-position=\"7mm\" fo:text-indent=\"-7mm\" fo:margin-left=\"7mm\"/></style:list-level-properties>")
             .Append("</text:list-level-style-number></text:list-style>");
            return b.Append(tableStyles).ToString();
        }

        public string Body()
        {
            var b = new StringBuilder();
            foreach (var block in doc.Blocks)
                switch (block)
                {
                    case DocHeading h:
                        var level = Math.Clamp(h.Level, 1, 3);
                        b.Append($"<text:h text:style-name=\"Heading_20_{level}\" text:outline-level=\"{level}\">").Append(Runs(h.Runs)).Append("</text:h>");
                        break;
                    case DocParagraph p:
                        var style = p.Style switch { "caption" => "Caption", "note" => "Note", _ => "Text_20_body" };
                        b.Append($"<text:p text:style-name=\"{style}\">").Append(Runs(p.Runs)).Append("</text:p>");
                        break;
                    case DocList l:
                        b.Append($"<text:list text:style-name=\"{(l.Ordered ? "L_number" : "L_bullet")}\">");
                        foreach (var item in l.Items) b.Append("<text:list-item><text:p text:style-name=\"List\">").Append(Runs(item)).Append("</text:p></text:list-item>");
                        b.Append("</text:list>");
                        break;
                    case DocTable t: Table(b, t); break;
                    case DocImage img: Image(b, img); break;
                    case DocToc c: Toc(b, c); break;
                    case DocPageBreak: b.Append("<text:p text:style-name=\"P_break\"/>"); break;
                }
            return b.ToString();
        }

        void Table(StringBuilder b, DocTable t)
        {
            var widths = ColumnWidths(t, doc.Page.TextWidth);
            var n = widths.Length;
            if (t.Title is { } title) b.Append("<text:p text:style-name=\"P_title\">").Append(Text(title)).Append("</text:p>");
            if (n == 0) return;
            var name = $"Table{++tables}";
            tableStyles.Append($"<style:style style:name=\"{name}\" style:family=\"table\"><style:table-properties style:width=\"{Mm(widths.Sum())}\" table:align=\"left\" fo:margin-top=\"0mm\" fo:margin-bottom=\"0mm\"/></style:style>");
            for (var c = 0; c < n; c++)
                tableStyles.Append($"<style:style style:name=\"{name}.C{c}\" style:family=\"table-column\"><style:table-column-properties style:column-width=\"{Mm(widths[c])}\"/></style:style>");
            b.Append($"<table:table table:name=\"{name}\" table:style-name=\"{name}\">");
            for (var c = 0; c < n; c++) b.Append($"<table:table-column table:style-name=\"{name}.C{c}\"/>");
            void Row(IReadOnlyList<IReadOnlyList<DocRun>> cells, bool header, bool bold)
            {
                b.Append("<table:table-row table:style-name=\"R_keep\">");
                var cell = t.Plain ? "C_plain" : header ? "C_head" : bold ? "C_total" : "C_grid";
                for (var c = 0; c < n; c++)
                {
                    var num = t.Numeric.Contains(c);
                    var para = header ? (num ? "P_head_num" : "Table_20_Heading") : num ? "P_num" : "Table_20_Contents";
                    b.Append($"<table:table-cell table:style-name=\"{cell}\" office:value-type=\"string\"><text:p text:style-name=\"{para}\">")
                     .Append(c < cells.Count ? Runs(cells[c], bold && !header) : "").Append("</text:p></table:table-cell>");
                }
                b.Append("</table:table-row>");
            }
            if (!t.Plain && t.Header.Count > 0)
            {
                b.Append("<table:table-header-rows>");
                Row([.. t.Header.Select(h => (IReadOnlyList<DocRun>)[new DocRun(h)])], true, true);
                b.Append("</table:table-header-rows>");
            }
            for (var i = 0; i < t.Rows.Count; i++) Row(t.Rows[i], false, t.TotalRow && i == t.Rows.Count - 1);
            b.Append("</table:table><text:p text:style-name=\"P_after\"/>");
        }

        void Image(StringBuilder b, DocImage img)
        {
            var picture = pictures != null && pictures.TryGetValue(img.Key, out var p) ? p : null;
            var (w, h) = Fit(img, doc.Page.TextWidth);
            if (picture != null && (picture.Svg != null || picture.Png != null) && w > 0)
            {
                var k = ++frames;
                b.Append("<text:p text:style-name=\"P_picture\">")
                 .Append($"<draw:frame draw:style-name=\"fr1\" draw:name=\"Picture{k}\" text:anchor-type=\"as-char\" svg:width=\"{Mm(w)}\" svg:height=\"{Mm(h)}\" draw:z-index=\"0\">");
                // The SVG first, the PNG as the fallback a reader takes when it cannot draw the SVG.
                if (picture.Svg != null)
                {
                    var path = $"Pictures/picture{k}.svg";
                    Media.Add((path, "image/svg+xml", new UTF8Encoding(false).GetBytes(picture.Svg)));
                    b.Append($"<draw:image xlink:href=\"{path}\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\" draw:mime-type=\"image/svg+xml\"/>");
                }
                if (picture.Png != null)
                {
                    var path = $"Pictures/picture{k}.png";
                    Media.Add((path, "image/png", picture.Png));
                    b.Append($"<draw:image xlink:href=\"{path}\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\" draw:mime-type=\"image/png\"/>");
                }
                if (img.Caption is { Length: > 0 } alt) b.Append("<svg:title>").Append(Xml(alt)).Append("</svg:title>");
                b.Append("</draw:frame></text:p>");
            }
            if (img.Caption is { Length: > 0 } caption) b.Append("<text:p text:style-name=\"P_caption\">").Append(Text(caption)).Append("</text:p>");
        }

        void Toc(StringBuilder b, DocToc toc)
        {
            var entries = Contents(doc);
            const string name = "Table of Contents1";
            b.Append($"<text:table-of-content text:style-name=\"Sect1\" text:protected=\"true\" text:name=\"{name}\">")
             .Append("<text:table-of-content-source text:outline-level=\"2\">")
             .Append($"<text:index-title-template text:style-name=\"Contents_20_Heading\">{Xml(toc.Title)}</text:index-title-template>");
            for (var l = 1; l <= 2; l++)
                b.Append($"<text:table-of-content-entry-template text:outline-level=\"{l}\" text:style-name=\"Contents_20_{l}\">")
                 .Append("<text:index-entry-text/><text:index-entry-tab-stop style:type=\"right\" style:leader-char=\".\"/><text:index-entry-page-number/>")
                 .Append("</text:table-of-content-entry-template>");
            b.Append("</text:table-of-content-source><text:index-body>")
             .Append($"<text:index-title text:style-name=\"Sect1\" text:name=\"{name}_Head\"><text:p text:style-name=\"Contents_20_Heading\">{Text(toc.Title)}</text:p></text:index-title>");
            foreach (var (h, page) in entries)
                b.Append($"<text:p text:style-name=\"Contents_20_{Math.Clamp(h.Level, 1, 2)}\">").Append(Runs(h.Runs.Select(r => r with { Bold = false, Italic = false })))
                 .Append("<text:tab/>").Append(page).Append("</text:p>");
            if (entries.Count == 0) b.Append("<text:p text:style-name=\"Contents_20_1\"/>");
            b.Append("</text:index-body></text:table-of-content>");
        }
    }
}
