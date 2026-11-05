using System.Globalization;
using System.IO.Compression;
using System.Text;
using static Lapin.Drawing.Documents.DocText;

namespace Lapin.Drawing.Documents;

/// <summary>
/// The document as a DOCX (Office Open XML WordprocessingML, ECMA-376), no library; pictures by key (their SVG and PNG), a
/// picture without one left out (its caption kept). Styles by Word's built-in ids (Normal, Heading1–3, Caption, TableGrid,
/// ListParagraph, TOC1–2, Header, Footer), so a company's template takes them over; the header and footer carry PAGE and
/// NUMPAGES; the table of contents is a TOC field prefilled with the headings and the PDF layout's pages, which Word
/// refreshes on opening (<c>w:updateFields</c>); header rows repeat on each page.
/// </summary>
public static class DocxWriter
{
    const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    const string SvgExt = "{96DAC541-7B7A-43D3-8B79-37D633B846F1}";

    public static byte[] Write(FilledDocument doc, IReadOnlyDictionary<string, DocPicture>? pictures = null)
    {
        var w = new Writer(doc, pictures);
        var body = w.Body();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var look = DocLook.Of(doc.Page);
            var logo = look.LogoBox(doc.Page);
            var hasHeader = doc.Page.Header.Count > 0 || logo != null;
            var hasFooter = doc.Page.Footer.Count > 0;
            var types = new StringBuilder(XmlHead + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
                + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
                + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
                + "<Default Extension=\"png\" ContentType=\"image/png\"/>"
                + "<Default Extension=\"svg\" ContentType=\"image/svg+xml\"/>"
                + (logo != null && look.Logo!.Jpeg ? "<Default Extension=\"jpeg\" ContentType=\"image/jpeg\"/>" : "")
                + "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>"
                + "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>"
                + "<Override PartName=\"/word/numbering.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml\"/>"
                + "<Override PartName=\"/word/settings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml\"/>"
                + "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>"
                + "<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
            if (hasHeader) types.Append("<Override PartName=\"/word/header1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml\"/>");
            if (hasFooter) types.Append("<Override PartName=\"/word/footer1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml\"/>");
            Put(zip, "[Content_Types].xml", types.Append("</Types>").ToString());
            Put(zip, "_rels/.rels", XmlHead + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + $"<Relationship Id=\"rId1\" Type=\"{Rel}officeDocument\" Target=\"word/document.xml\"/>"
                + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>"
                + $"<Relationship Id=\"rId3\" Type=\"{Rel}extended-properties\" Target=\"docProps/app.xml\"/></Relationships>");

            var rels = new StringBuilder(XmlHead + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + $"<Relationship Id=\"rIdStyles\" Type=\"{Rel}styles\" Target=\"styles.xml\"/>"
                + $"<Relationship Id=\"rIdNumbering\" Type=\"{Rel}numbering\" Target=\"numbering.xml\"/>"
                + $"<Relationship Id=\"rIdSettings\" Type=\"{Rel}settings\" Target=\"settings.xml\"/>");
            if (hasHeader) rels.Append($"<Relationship Id=\"rIdHeader\" Type=\"{Rel}header\" Target=\"header1.xml\"/>");
            if (hasFooter) rels.Append($"<Relationship Id=\"rIdFooter\" Type=\"{Rel}footer\" Target=\"footer1.xml\"/>");
            foreach (var (id, path, _) in w.Media) rels.Append($"<Relationship Id=\"{id}\" Type=\"{Rel}image\" Target=\"{path}\"/>");
            Put(zip, "word/_rels/document.xml.rels", rels.Append("</Relationships>").ToString());

            var p = doc.Page;
            var sect = new StringBuilder("<w:sectPr>");
            if (hasHeader) sect.Append("<w:headerReference w:type=\"default\" r:id=\"rIdHeader\"/>");
            if (hasFooter) sect.Append("<w:footerReference w:type=\"default\" r:id=\"rIdFooter\"/>");
            sect.Append($"<w:pgSz w:w=\"{Twips(p.W)}\" w:h=\"{Twips(p.H)}\"{(p.W > p.H ? " w:orient=\"landscape\"" : "")}/>")
                .Append($"<w:pgMar w:top=\"{Twips(p.Top)}\" w:right=\"{Twips(p.Right)}\" w:bottom=\"{Twips(p.Bottom)}\" w:left=\"{Twips(p.Left)}\" w:header=\"{Twips(Math.Max(5, p.Top * 0.4))}\" w:footer=\"{Twips(Math.Max(5, p.Bottom * 0.4))}\" w:gutter=\"0\"/>")
                .Append("</w:sectPr>");
            Put(zip, "word/document.xml", XmlHead + $"<w:document xmlns:w=\"{W}\" xmlns:r=\"{R}\""
                + " xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\""
                + " xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\""
                + " xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\""
                + " xmlns:asvg=\"http://schemas.microsoft.com/office/drawing/2016/SVG/main\">"
                + "<w:body>" + body + sect + "</w:body></w:document>");
            Put(zip, "word/styles.xml", Styles(doc));
            Put(zip, "word/numbering.xml", w.Numbering());
            Put(zip, "word/settings.xml", XmlHead + $"<w:settings xmlns:w=\"{W}\"><w:defaultTabStop w:val=\"709\"/>"
                + $"<w:characterSpacingControl w:val=\"doNotCompress\"/><w:updateFields w:val=\"true\"/><w:themeFontLang w:val=\"{Xml(Lang(doc.Meta.Lang))}\"/><w:compat><w:compatSetting w:name=\"compatibilityMode\" w:uri=\"http://schemas.microsoft.com/office/word\" w:val=\"15\"/></w:compat></w:settings>");
            if (hasHeader) Put(zip, "word/header1.xml", Header(doc, look, logo));
            if (logo != null)
            {
                Put(zip, "word/_rels/header1.xml.rels", XmlHead + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                    + $"<Relationship Id=\"rIdLogo\" Type=\"{Rel}image\" Target=\"media/logo.{look.Logo!.Extension}\"/></Relationships>");
                Put(zip, $"word/media/logo.{look.Logo.Extension}", look.Logo.Data);
            }
            if (hasFooter) Put(zip, "word/footer1.xml", XmlHead + $"<w:ftr xmlns:w=\"{W}\" xmlns:r=\"{R}\"><w:p><w:pPr><w:pStyle w:val=\"Footer\"/><w:jc w:val=\"center\"/></w:pPr>{Writer.Runs(p.Footer)}</w:p></w:ftr>");
            foreach (var (_, path, data) in w.Media) Put(zip, "word/" + path, data);
            Put(zip, "docProps/core.xml", Core(doc));
            Put(zip, "docProps/app.xml", XmlHead + "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\">"
                + "<Application>Lapin</Application><DocSecurity>0</DocSecurity><ScaleCrop>false</ScaleCrop><Company></Company><LinksUpToDate>false</LinksUpToDate><SharedDoc>false</SharedDoc><HyperlinksChanged>false</HyperlinksChanged><AppVersion>1.0000</AppVersion></Properties>");
        }
        return ms.ToArray();
    }

    /// <summary>A language tag as Word writes it (ro-RO, en-US…).</summary>
    static string Lang(string lang) => lang switch { "" => "ro-RO", "ro" => "ro-RO", "en" => "en-GB", "fr" => "fr-FR", "de" => "de-DE", "it" => "it-IT", "es" => "es-ES", "hu" => "hu-HU", _ => lang };

    static string Core(FilledDocument doc)
    {
        var m = doc.Meta;
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var b = new StringBuilder(XmlHead + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\""
            + " xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
        b.Append("<dc:title>").Append(Xml(doc.Title)).Append("</dc:title>");
        if (m.Subject.Length > 0) b.Append("<dc:subject>").Append(Xml(m.Subject)).Append("</dc:subject>");
        if (m.Author.Length > 0) b.Append("<dc:creator>").Append(Xml(m.Author)).Append("</dc:creator><cp:lastModifiedBy>").Append(Xml(m.Author)).Append("</cp:lastModifiedBy>");
        // cp:revision is a count to Word: a revision like "B" goes in cp:version.
        if (m.Revision.Length > 0)
            b.Append(m.Revision.All(char.IsAsciiDigit) ? $"<cp:revision>{Xml(m.Revision)}</cp:revision>" : $"<cp:version>{Xml(m.Revision)}</cp:version>");
        if (m.Status.Length > 0) b.Append("<cp:contentStatus>").Append(Xml(m.Status)).Append("</cp:contentStatus>");
        b.Append("<dc:language>").Append(Xml(Lang(m.Lang))).Append("</dc:language>");
        b.Append($"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{now}</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">{now}</dcterms:modified>");
        return b.Append("</cp:coreProperties>").ToString();
    }

    /// <summary>
    /// The header: its text, and the style's logo as an inline picture (on the left, the text after it at a tab; on the right,
    /// the text first and the logo at a right tab on the text's edge).
    /// </summary>
    static string Header(FilledDocument doc, DocLook look, (double X, double Y, double W, double H)? logo)
    {
        var p = doc.Page;
        var runs = Writer.Runs(p.Header);
        if (logo is null) return XmlHead + $"<w:hdr xmlns:w=\"{W}\" xmlns:r=\"{R}\"><w:p><w:pPr><w:pStyle w:val=\"Header\"/></w:pPr>{runs}</w:p></w:hdr>";
        var b = new StringBuilder(XmlHead + $"<w:hdr xmlns:w=\"{W}\" xmlns:r=\"{R}\""
            + " xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\""
            + " xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\""
            + " xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><w:p><w:pPr><w:pStyle w:val=\"Header\"/>");
        var box = logo.Value;
        var right = look.Logo!.Right;
        b.Append(right ? $"<w:tabs><w:tab w:val=\"right\" w:pos=\"{Twips(p.TextWidth)}\"/></w:tabs>" : $"<w:tabs><w:tab w:val=\"left\" w:pos=\"{Twips(box.W + DocLook.LogoGap)}\"/></w:tabs>")
         .Append("</w:pPr>");
        var (cx, cy) = (Emu(box.W), Emu(box.H));
        var picture = "<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">"
            + $"<wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"9000\" name=\"Logo\" descr=\"Logo\"/>"
            + "<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
            + "<pic:nvPicPr><pic:cNvPr id=\"9000\" name=\"Logo\"/><pic:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></pic:cNvPicPr></pic:nvPicPr>"
            + "<pic:blipFill><a:blip r:embed=\"rIdLogo\"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
            + $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>"
            + "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        if (right) b.Append(runs).Append("<w:r><w:tab/></w:r>").Append(picture);
        else b.Append(picture).Append("<w:r><w:tab/></w:r>").Append(runs);
        return b.Append("</w:p></w:hdr>").ToString();
    }

    static string Styles(FilledDocument doc)
    {
        static string Hp(double pt) => ((int)Math.Round(pt * 2)).ToString(CultureInfo.InvariantCulture);
        var look = DocLook.Of(doc.Page);
        var font = Xml(look.Font);
        var color = look.HeadingColor is { Length: 7 } hc ? $"<w:color w:val=\"{hc[1..].ToUpperInvariant()}\"/>" : "";
        var tab = Twips(doc.Page.TextWidth);
        string Para(string id, string name, string ppr, string rpr, string basedOn = "Normal", string next = "Normal", string extra = "") =>
            $"<w:style w:type=\"paragraph\" w:styleId=\"{id}\"><w:name w:val=\"{name}\"/>{(basedOn.Length > 0 ? $"<w:basedOn w:val=\"{basedOn}\"/>" : "")}<w:next w:val=\"{next}\"/>{extra}<w:qFormat/><w:pPr>{ppr}</w:pPr><w:rPr>{rpr}</w:rPr></w:style>";
        string Heading(int level, int before) =>
            Para($"Heading{level}", $"heading {level}", $"<w:keepNext/><w:keepLines/><w:spacing w:before=\"{before}\" w:after=\"120\"/><w:outlineLvl w:val=\"{level - 1}\"/>",
                $"<w:b/><w:bCs/>{color}<w:sz w:val=\"{Hp(look.Heading(level))}\"/><w:szCs w:val=\"{Hp(look.Heading(level))}\"/>", extra: "<w:uiPriority w:val=\"9\"/>");
        var b = new StringBuilder(XmlHead + $"<w:styles xmlns:w=\"{W}\">");
        b.Append($"<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"{font}\" w:hAnsi=\"{font}\" w:eastAsia=\"{font}\" w:cs=\"{font}\"/><w:sz w:val=\"{Hp(look.Body)}\"/><w:szCs w:val=\"{Hp(look.Body)}\"/><w:lang w:val=\"{Xml(Lang(doc.Meta.Lang))}\"/></w:rPr></w:rPrDefault>")
         .Append("<w:pPrDefault><w:pPr><w:spacing w:after=\"140\" w:line=\"264\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>");
        b.Append(Para("Normal", "Normal", "", "", basedOn: "").Replace("<w:style w:type=\"paragraph\" w:styleId=\"Normal\">", "<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\">"));
        b.Append(Heading(1, 360)).Append(Heading(2, 240)).Append(Heading(3, 200));
        b.Append(Para("Title", "Title", "<w:spacing w:after=\"240\"/>", "<w:b/><w:sz w:val=\"44\"/><w:szCs w:val=\"44\"/>"));
        b.Append(Para("Caption", "caption", "<w:spacing w:before=\"60\" w:after=\"200\"/>", $"<w:i/><w:iCs/><w:sz w:val=\"{Hp(look.Caption)}\"/><w:szCs w:val=\"{Hp(look.Caption)}\"/>"));
        b.Append(Para("Note", "Note", "", $"<w:color w:val=\"555555\"/><w:sz w:val=\"{Hp(look.Note)}\"/><w:szCs w:val=\"{Hp(look.Note)}\"/>"));
        b.Append(Para("ListParagraph", "List Paragraph", "<w:spacing w:after=\"60\"/><w:ind w:left=\"720\"/><w:contextualSpacing/>", ""));
        b.Append(Para("TableText", "Table Text", "<w:spacing w:before=\"20\" w:after=\"20\" w:line=\"240\" w:lineRule=\"auto\"/>", $"<w:sz w:val=\"{Hp(look.Table)}\"/><w:szCs w:val=\"{Hp(look.Table)}\"/>"));
        b.Append(Para("TOCHeading", "TOC Heading", "<w:keepNext/><w:spacing w:before=\"240\" w:after=\"160\"/>", $"<w:b/><w:sz w:val=\"{Hp(look.Heading(1))}\"/><w:szCs w:val=\"{Hp(look.Heading(1))}\"/>", basedOn: "Normal"));
        b.Append(Para("TOC1", "toc 1", $"<w:tabs><w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"{tab}\"/></w:tabs><w:spacing w:after=\"60\"/>", "<w:b/>"));
        b.Append(Para("TOC2", "toc 2", $"<w:tabs><w:tab w:val=\"right\" w:leader=\"dot\" w:pos=\"{tab}\"/></w:tabs><w:spacing w:after=\"40\"/><w:ind w:left=\"340\"/>", ""));
        b.Append(Para("Header", "header", "<w:spacing w:after=\"0\"/>", $"<w:color w:val=\"555555\"/><w:sz w:val=\"{Hp(look.Header)}\"/><w:szCs w:val=\"{Hp(look.Header)}\"/>"));
        b.Append(Para("Footer", "footer", "<w:spacing w:after=\"0\"/>", $"<w:color w:val=\"555555\"/><w:sz w:val=\"{Hp(look.Header)}\"/><w:szCs w:val=\"{Hp(look.Header)}\"/>"));
        b.Append("<w:style w:type=\"character\" w:styleId=\"Hyperlink\"><w:name w:val=\"Hyperlink\"/><w:rPr><w:color w:val=\"0563C1\"/><w:u w:val=\"single\"/></w:rPr></w:style>");
        b.Append("<w:style w:type=\"table\" w:default=\"1\" w:styleId=\"TableNormal\"><w:name w:val=\"Normal Table\"/><w:tblPr><w:tblInd w:w=\"0\" w:type=\"dxa\"/><w:tblCellMar><w:top w:w=\"0\" w:type=\"dxa\"/><w:left w:w=\"85\" w:type=\"dxa\"/><w:bottom w:w=\"0\" w:type=\"dxa\"/><w:right w:w=\"85\" w:type=\"dxa\"/></w:tblCellMar></w:tblPr></w:style>");
        b.Append("<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/><w:basedOn w:val=\"TableNormal\"/><w:pPr><w:spacing w:after=\"0\"/></w:pPr><w:tblPr><w:tblBorders>"
            + "<w:top w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:left w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:bottom w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>"
            + "<w:right w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:insideH w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/><w:insideV w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"/>"
            + "</w:tblBorders></w:tblPr></w:style>");
        b.Append("<w:style w:type=\"numbering\" w:default=\"1\" w:styleId=\"NoList\"><w:name w:val=\"No List\"/></w:style>");
        return b.Append("</w:styles>").ToString();
    }

    sealed class Writer(FilledDocument doc, IReadOnlyDictionary<string, DocPicture>? pictures)
    {
        public readonly List<(string Id, string Path, byte[] Data)> Media = [];
        readonly List<int> orderedLists = [];   // a w:num each, restarting at 1
        int bookmark, drawing;

        public string Body()
        {
            var b = new StringBuilder();
            var toc = doc.Blocks.Any(x => x is DocToc) ? DocText.Contents(doc) : [];
            var headingIndex = 0;
            foreach (var block in doc.Blocks)
                switch (block)
                {
                    case DocHeading h:
                        var level = Math.Clamp(h.Level, 1, 3);
                        b.Append($"<w:p><w:pPr><w:pStyle w:val=\"Heading{level}\"/></w:pPr>");
                        if (h.Level <= 2) b.Append($"<w:bookmarkStart w:id=\"{bookmark}\" w:name=\"_Toc{headingIndex + 1:D6}\"/>");
                        b.Append(Runs(h.Runs));
                        if (h.Level <= 2) b.Append($"<w:bookmarkEnd w:id=\"{bookmark++}\"/>");
                        b.Append("</w:p>");
                        headingIndex++;
                        break;
                    case DocParagraph p:
                        var style = p.Style switch { "caption" => "Caption", "note" => "Note", _ => null };
                        b.Append("<w:p>").Append(style != null ? $"<w:pPr><w:pStyle w:val=\"{style}\"/></w:pPr>" : "").Append(Runs(p.Runs)).Append("</w:p>");
                        break;
                    case DocList l:
                        var num = 1;
                        if (l.Ordered) { orderedLists.Add(orderedLists.Count); num = 1 + orderedLists.Count; }
                        foreach (var item in l.Items)
                            b.Append($"<w:p><w:pPr><w:pStyle w:val=\"ListParagraph\"/><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"{num}\"/></w:numPr></w:pPr>").Append(Runs(item)).Append("</w:p>");
                        break;
                    case DocTable t: Table(b, t); break;
                    case DocImage img: Image(b, img); break;
                    case DocToc c: Toc(b, c, toc); break;
                    case DocPageBreak: b.Append("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>"); break;
                }
            return b.ToString();
        }

        /// <summary>Runs as w:r, a page field as PAGE or NUMPAGES, a missing field as nothing.</summary>
        public static string Runs(IEnumerable<DocRun> runs, bool bold = false)
        {
            var b = new StringBuilder();
            foreach (var r in runs)
            {
                if (r.Missing) continue;
                var rpr = (bold || r.Bold ? "<w:b/><w:bCs/>" : "") + (r.Italic ? "<w:i/><w:iCs/>" : "");
                var props = rpr.Length > 0 ? $"<w:rPr>{rpr}</w:rPr>" : "";
                if (r.Field is "page" or "pages")
                {
                    var instr = r.Field == "page" ? "PAGE" : "NUMPAGES";
                    b.Append($"<w:r>{props}<w:fldChar w:fldCharType=\"begin\"/></w:r><w:r>{props}<w:instrText xml:space=\"preserve\"> {instr} </w:instrText></w:r>")
                     .Append($"<w:r>{props}<w:fldChar w:fldCharType=\"separate\"/></w:r><w:r>{props}<w:t>1</w:t></w:r><w:r>{props}<w:fldChar w:fldCharType=\"end\"/></w:r>");
                    continue;
                }
                var text = r.Text ?? "";
                if (text.Length == 0) continue;
                b.Append("<w:r>").Append(props);
                var lines = text.Replace("\r", "").Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    if (i > 0) b.Append("<w:br/>");
                    var parts = lines[i].Split('\t');
                    for (var k = 0; k < parts.Length; k++)
                    {
                        if (k > 0) b.Append("<w:tab/>");
                        if (parts[k].Length > 0) b.Append("<w:t xml:space=\"preserve\">").Append(Xml(parts[k])).Append("</w:t>");
                    }
                }
                b.Append("</w:r>");
            }
            return b.ToString();
        }

        void Table(StringBuilder b, DocTable t)
        {
            var widths = ColumnWidths(t, doc.Page.TextWidth);
            var n = widths.Length;
            if (t.Title is { } title)
                b.Append("<w:p><w:pPr><w:keepNext/><w:spacing w:after=\"80\"/></w:pPr>").Append(Runs([new DocRun(title, Bold: true)])).Append("</w:p>");
            if (n == 0) return;
            var tw = widths.Sum(Twips);
            b.Append("<w:tbl><w:tblPr>").Append(t.Plain ? "" : "<w:tblStyle w:val=\"TableGrid\"/>")
             .Append($"<w:tblW w:w=\"{tw}\" w:type=\"dxa\"/>");
            if (t.Plain) b.Append("<w:tblBorders><w:top w:val=\"nil\"/><w:left w:val=\"nil\"/><w:bottom w:val=\"nil\"/><w:right w:val=\"nil\"/><w:insideH w:val=\"nil\"/><w:insideV w:val=\"nil\"/></w:tblBorders>");
            b.Append("<w:tblLayout w:type=\"fixed\"/><w:tblLook w:val=\"04A0\" w:firstRow=\"1\" w:lastRow=\"0\" w:firstColumn=\"0\" w:lastColumn=\"0\" w:noHBand=\"0\" w:noVBand=\"1\"/></w:tblPr><w:tblGrid>");
            foreach (var wd in widths) b.Append($"<w:gridCol w:w=\"{Twips(wd)}\"/>");
            b.Append("</w:tblGrid>");
            void Row(IReadOnlyList<IReadOnlyList<DocRun>> cells, bool header, bool bold)
            {
                b.Append("<w:tr>");
                b.Append(header ? "<w:trPr><w:cantSplit/><w:tblHeader/></w:trPr>" : "<w:trPr><w:cantSplit/></w:trPr>");
                for (var c = 0; c < n; c++)
                {
                    b.Append($"<w:tc><w:tcPr><w:tcW w:w=\"{Twips(widths[c])}\" w:type=\"dxa\"/>");
                    if (bold && !header && !t.Plain) b.Append("<w:tcBorders><w:top w:val=\"single\" w:sz=\"12\" w:space=\"0\" w:color=\"000000\"/></w:tcBorders>");
                    if (header) b.Append("<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"E6E6E6\"/>");
                    b.Append("</w:tcPr><w:p><w:pPr><w:pStyle w:val=\"TableText\"/>").Append(header ? "<w:keepNext/>" : "")
                     .Append(t.Numeric.Contains(c) ? "<w:jc w:val=\"right\"/>" : "").Append("</w:pPr>")
                     .Append(c < cells.Count ? Runs(cells[c], bold) : "").Append("</w:p></w:tc>");
                }
                b.Append("</w:tr>");
            }
            if (!t.Plain && t.Header.Count > 0) Row([.. t.Header.Select(h => (IReadOnlyList<DocRun>)[new DocRun(h)])], true, true);
            for (var i = 0; i < t.Rows.Count; i++) Row(t.Rows[i], false, t.TotalRow && i == t.Rows.Count - 1);
            // A paragraph after the table, so two tables in a row stay two.
            b.Append("</w:tbl><w:p><w:pPr><w:spacing w:after=\"60\"/></w:pPr></w:p>");
        }

        void Image(StringBuilder b, DocImage img)
        {
            var picture = pictures != null && pictures.TryGetValue(img.Key, out var p) ? p : null;
            var (w, h) = Fit(img, doc.Page.TextWidth);
            if (picture != null && (picture.Png != null || picture.Svg != null) && w > 0)
            {
                var k = ++drawing;
                var png = $"rIdImg{k}";
                Media.Add((png, $"media/image{k}.png", picture.Png ?? DocPng.White()));
                string? svg = null;
                if (picture.Svg != null) { svg = $"rIdSvg{k}"; Media.Add((svg, $"media/image{k}.svg", new UTF8Encoding(false).GetBytes(picture.Svg))); }
                var (cx, cy) = (Emu(w), Emu(h));
                var name = Xml(img.Caption ?? img.Key);
                b.Append($"<w:p><w:pPr><w:keepNext/><w:spacing w:after=\"60\"/><w:jc w:val=\"center\"/></w:pPr><w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">")
                 .Append($"<wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"{k}\" name=\"Picture {k}\" descr=\"{name}\"/>")
                 .Append("<wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>")
                 .Append("<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>")
                 .Append($"<pic:nvPicPr><pic:cNvPr id=\"{k}\" name=\"Picture {k}\"/><pic:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></pic:cNvPicPr></pic:nvPicPr>")
                 .Append($"<pic:blipFill><a:blip r:embed=\"{png}\">")
                 .Append(svg != null ? $"<a:extLst><a:ext uri=\"{SvgExt}\"><asvg:svgBlip r:embed=\"{svg}\"/></a:ext></a:extLst>" : "")
                 .Append("</a:blip><a:stretch><a:fillRect/></a:stretch></pic:blipFill>")
                 .Append($"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>")
                 .Append("</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>");
            }
            if (img.Caption is { Length: > 0 } caption)
                b.Append("<w:p><w:pPr><w:pStyle w:val=\"Caption\"/><w:jc w:val=\"center\"/></w:pPr>").Append(Runs([new DocRun(caption)])).Append("</w:p>");
        }

        void Toc(StringBuilder b, DocToc toc, IReadOnlyList<(DocHeading Heading, int Page)> entries)
        {
            b.Append("<w:p><w:pPr><w:pStyle w:val=\"TOCHeading\"/></w:pPr>").Append(Runs([new DocRun(toc.Title)])).Append("</w:p>");
            const string begin = "<w:r><w:fldChar w:fldCharType=\"begin\" w:dirty=\"true\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> TOC \\o \"1-2\" \\h \\z \\u </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>";
            const string end = "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";
            if (entries.Count == 0) { b.Append("<w:p><w:pPr><w:pStyle w:val=\"TOC1\"/></w:pPr>").Append(begin).Append(end).Append("</w:p>"); return; }
            // The anchors follow the headings' order among all headings, as the bookmarks do.
            var all = doc.Blocks.OfType<DocHeading>().ToList();
            var from = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                var (h, page) = entries[i];
                var index = all.FindIndex(from, x => ReferenceEquals(x, h));
                if (index >= 0) from = index + 1;
                b.Append($"<w:p><w:pPr><w:pStyle w:val=\"TOC{Math.Clamp(h.Level, 1, 2)}\"/></w:pPr>").Append(i == 0 ? begin : "")
                 .Append($"<w:hyperlink w:anchor=\"_Toc{index + 1:D6}\" w:history=\"1\">").Append(Runs(h.Runs.Select(r => r with { Bold = false, Italic = false })))
                 .Append($"<w:r><w:tab/></w:r><w:r><w:t>{page}</w:t></w:r></w:hyperlink>")
                 .Append(i == entries.Count - 1 ? end : "").Append("</w:p>");
            }
        }

        public string Numbering()
        {
            var b = new StringBuilder(XmlHead + $"<w:numbering xmlns:w=\"{W}\">");
            b.Append("<w:abstractNum w:abstractNumId=\"0\"><w:multiLevelType w:val=\"hybridMultilevel\"/><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"•\"/><w:lvlJc w:val=\"left\"/>")
             .Append($"<w:pPr><w:ind w:left=\"360\" w:hanging=\"360\"/></w:pPr><w:rPr><w:rFonts w:ascii=\"{Font}\" w:hAnsi=\"{Font}\" w:hint=\"default\"/></w:rPr></w:lvl></w:abstractNum>");
            b.Append("<w:abstractNum w:abstractNumId=\"1\"><w:multiLevelType w:val=\"hybridMultilevel\"/><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/><w:lvlJc w:val=\"left\"/>")
             .Append("<w:pPr><w:ind w:left=\"397\" w:hanging=\"397\"/></w:pPr></w:lvl></w:abstractNum>");
            b.Append("<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num>");
            foreach (var k in orderedLists)
                b.Append($"<w:num w:numId=\"{k + 2}\"><w:abstractNumId w:val=\"1\"/><w:lvlOverride w:ilvl=\"0\"><w:startOverride w:val=\"1\"/></w:lvlOverride></w:num>");
            return b.Append("</w:numbering>").ToString();
        }
    }
}
