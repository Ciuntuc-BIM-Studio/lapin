using Lapin.Drawing.Views;

namespace Lapin.Drawing.Documents;

/// <summary>
/// A document as it is written out (docs/plan-documents.md): its blocks with every field already filled from the project,
/// its tables with their rows, its pictures with their pens. DOCX, ODT and PDF are written from the same one, so they say
/// the same thing; nothing here reads the model.
/// </summary>
public sealed record FilledDocument(string Title, DocPage Page, IReadOnlyList<DocBlock> Blocks, DocMeta Meta);

/// <summary>Who and what the document is: written into the file's properties (DOCX core.xml, ODT meta.xml, the PDF's Info).</summary>
public sealed record DocMeta(string Author = "", string Subject = "", string Revision = "", string Status = "", string Lang = "ro");

/// <summary>The paper (mm), its margins (top, right, bottom, left), and the header and footer as runs; a run's Field "page" or "pages" is the page number or count.</summary>
public sealed record DocPage(double W, double H, double Top, double Right, double Bottom, double Left, IReadOnlyList<DocRun> Header, IReadOnlyList<DocRun> Footer)
{
    public static DocPage A4 => new(210, 297, 20, 20, 20, 25, [], []);
    public double TextWidth => W - Left - Right;
}

/// <summary>
/// A piece of text with its look. <see cref="Field"/>: "page" or "pages" (filled by the writer: PAGE / NUMPAGES fields,
/// text:page-number, the PDF's own count); <see cref="Missing"/>: a field the project does not fill yet (its key in Text),
/// written as empty text and listed as to complete.
/// </summary>
public sealed record DocRun(string Text, bool Bold = false, bool Italic = false, string? Field = null, bool Missing = false);

/// <summary>A block of a filled document.</summary>
public abstract record DocBlock;

/// <summary>A heading, level 1–3.</summary>
public sealed record DocHeading(int Level, IReadOnlyList<DocRun> Runs) : DocBlock;

/// <summary>A paragraph; Style "normal", "caption" (under a picture or a table), "note" (smaller, grey).</summary>
public sealed record DocParagraph(IReadOnlyList<DocRun> Runs, string Style = "normal") : DocBlock;

/// <summary>A list, bulleted or numbered, one level.</summary>
public sealed record DocList(bool Ordered, IReadOnlyList<IReadOnlyList<DocRun>> Items) : DocBlock;

/// <summary>
/// A table: its header row (repeated on each page), its rows of cells, which columns are numbers (right-aligned), the
/// columns' relative widths (null: by their content), whether the last row is a total (bold); Plain: a two-column
/// key–value table without a header (the "fields" block).
/// </summary>
public sealed record DocTable(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<IReadOnlyList<DocRun>>> Rows, IReadOnlySet<int> Numeric, IReadOnlyList<double>? Widths = null, bool TotalRow = false, bool Plain = false, string? Title = null) : DocBlock;

/// <summary>
/// A picture: a view drawn as pens on paper W × H mm (y down, as on a sheet), vector in the PDF; in DOCX and ODT the SVG and
/// PNG the interface made of the same pens (looked up by Key in what the export is given), else the SVG alone, else left out.
/// </summary>
public sealed record DocImage(string Key, double W, double H, IReadOnlyList<Pen> Pens, string? Caption = null) : DocBlock;

/// <summary>The table of contents: the headings of levels 1–2 with their pages.</summary>
public sealed record DocToc(string Title = "Cuprins") : DocBlock;

public sealed record DocPageBreak : DocBlock;

/// <summary>A picture's files as the interface made them: the SVG text and a PNG (200 dpi).</summary>
public sealed record DocPicture(string? Svg, byte[]? Png);
