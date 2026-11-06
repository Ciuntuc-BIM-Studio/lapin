using System.Text.Json.Nodes;
using static Lapin.Platform.TitleBlockTemplate;

namespace Lapin.Platform;

/// <summary>
/// The title blocks Lapin brings (decision 340): Lapin standard (drawn by <see cref="Sheets.Compose"/>'s own code, so every sheet
/// from before draws the same; its items here are the same layout, what a duplicate starts from), the Romanian cartuș and an
/// ISO 7200-like block. Read only: Duplicate makes an editable copy.
/// </summary>
public static class BuiltinTitleBlocks
{
    public const string Prefix = "builtin:";
    public const string Standard = Prefix + "standard", Cartus = Prefix + "cartus", Iso7200 = Prefix + "iso7200";

    public static readonly (string Id, string Name, string Description)[] All =
    [
        (Standard, "Lapin standard", "180 × 50 mm: project, sheet, revision and status, number, scale, date, drawn and checked by; the revision table above."),
        (Cartus, "Cartuș (RO)", "The Romanian cartuș, 180 × 62 mm: the verifiers, designer, client, project number, roles with names and signatures, scale, date, phase, titles, sheet number."),
        (Iso7200, "ISO 7200", "ISO 7200's data fields in 180 × 36 mm: legal owner, title, supplementary title, identification number, revision, date of issue, sheet x/y, created and approved by, document type and status."),
    ];

    public static bool Is(string? id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

    public static string? NameOf(string id) => All.FirstOrDefault(b => b.Id == id).Name;

    /// <summary>A built-in's template, a new copy each call; null when there is no such built-in.</summary>
    public static TitleBlockTemplate? Template(string id) => id switch
    {
        Standard => StandardItems(),
        Cartus => CartusItems(),
        Iso7200 => IsoItems(),
        _ => null,
    };

    /// <summary>Lapin standard as items: the layout <see cref="Sheets.Compose"/> draws (180 × 50, y up from the block's bottom).</summary>
    static TitleBlockTemplate StandardItems()
    {
        var items = new JsonArray
        {
            Rect(0, 0, 180, 50, 0.5), Line(0, 30, 180, 30), Line(0, 15, 180, 15),
            Line(126, 15, 126, 30), Line(153, 15, 153, 30),
            Text(3, 45.5, "PROJECT", 1.8), Text(3, 35, "{sheet.project}", 5, true),
            Text(3, 25.5, "SHEET", 1.8), Text(3, 18, "{sheet.name}", 4, true),
            Text(129, 25.5, "REV", 1.8), Text(129, 18, "{sheet.revision}", 4, true),
            Text(156, 25.5, "STATUS", 1.8), Text(156, 18, "{sheet.status}", 4, true),
        };
        (string Label, string Field)[] cells = [("NUMBER", "{sheet.number}"), ("SCALE", "{sheet.scale}"), ("DATE", "{sheet.date}"), ("DRAWN BY", "{sheet.drawnBy}"), ("CHECKED BY", "{sheet.checkedBy}")];
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0) items.Add((JsonNode)Line(i * 36, 0, i * 36, 15));
            items.Add((JsonNode)Text(i * 36 + 3, 10.5, cells[i].Label, 1.8));
            items.Add((JsonNode)Text(i * 36 + 3, 3.5, cells[i].Field, i == 0 ? 4 : 3, i == 0));
        }
        return new TitleBlockTemplate(180, 50, "bottomRight", new TitleBlockFrame(), "above", items);
    }

    /// <summary>The Romanian cartuș: the verifiers' table on top, the designer, client and project number, then the roles, scale and date, titles, phase and sheet number.</summary>
    static TitleBlockTemplate CartusItems()
    {
        var items = new JsonArray { Rect(0, 0, 180, 62, 0.5) };
        void L(double x0, double y0, double x1, double y1, double w = 0.25) => items.Add((JsonNode)Line(x0, y0, x1, y1, w));
        void T(double x, double y, string text, double h, bool bold = false, string align = "start") => items.Add((JsonNode)Text(x, y, text, h, bold, align));
        // The verifiers (Verificator / Expert): a heading row and one row to fill by hand.
        L(0, 50, 180, 50, 0.5); L(0, 56, 180, 56);
        double[] vx = [40, 80, 105, 125];
        foreach (var x in vx) L(x, 50, x, 62);
        (double X, string Label)[] vh = [(1.5, "Verificator / Expert"), (41.5, "Nume"), (81.5, "Semnătura"), (106.5, "Cerința"), (126.5, "Referat / Expertiza nr. / data")];
        foreach (var (x, label) in vh) T(x, 58, label, 1.8);
        // The designer, the client and the site, the project's number.
        L(0, 32, 180, 32, 0.5); L(70, 32, 70, 50); L(140, 32, 140, 50);
        T(1.5, 46.5, "Proiectant", 1.8);
        T(1.5, 41.5, "{project.designer}", 3, true);
        T(1.5, 37.5, "{project.designerAddress}", 2);
        T(1.5, 33.5, "Nr. înreg. {project.designerRegistration}", 1.8);
        T(71.5, 46.5, "Beneficiar", 1.8);
        T(71.5, 42, "{project.client}", 3, true);
        T(71.5, 37.5, "Amplasament", 1.8);
        T(71.5, 34, "{project.address}", 2);
        T(141.5, 46.5, "Proiect nr.", 1.8);
        T(160, 38, "{project.number}", 4.5, true, "middle");
        // The roles: Specificație, Nume, Semnătura over four rows.
        const double row = 6.4;
        for (var i = 1; i < 5; i++) L(0, i * row, 75, i * row);
        L(25, 0, 25, 32); L(55, 0, 55, 32); L(75, 0, 75, 32, 0.5);
        T(1.5, 4 * row + 2, "Specificație", 1.8); T(26.5, 4 * row + 2, "Nume", 1.8); T(56.5, 4 * row + 2, "Semnătura", 1.8);
        (string Role, string Name)[] roles = [("Șef proiect", "{project.projectManager}"), ("Proiectat", "{project.chiefArchitect}"), ("Desenat", "{sheet.drawnBy}"), ("Verificat", "{sheet.checkedBy}")];
        for (var i = 0; i < roles.Length; i++)
        {
            var y = (3 - i) * row + 2;
            T(1.5, y, roles[i].Role, 2.2);
            T(26.5, y, roles[i].Name, 2.2);
        }
        // Scale and date; the project's and the sheet's titles; the phase and the sheet's number.
        L(75, 16, 180, 16); L(100, 0, 100, 32); L(155, 0, 155, 32);
        T(76.5, 28.5, "Scara", 1.8); T(87.5, 20, "{sheet.scale}", 3, false, "middle");
        T(76.5, 12.5, "Data", 1.8); T(87.5, 4, "{sheet.date}", 3, false, "middle");
        T(101.5, 28.5, "Titlu proiect", 1.8); T(101.5, 20, "{project.name}", 3, true);
        T(101.5, 12.5, "Titlu planșă", 1.8); T(101.5, 4, "{sheet.name}", 3, true);
        T(156.5, 28.5, "Faza", 1.8); T(167.5, 19.5, "{sheet.phase}", 4, true, "middle");
        T(156.5, 12.5, "Planșa nr.", 1.8); T(167.5, 3.5, "{sheet.number}", 4.5, true, "middle");
        return new TitleBlockTemplate(180, 62, "bottomRight", new TitleBlockFrame(), "above", items);
    }

    /// <summary>ISO 7200's fields: the people and the document on top, the legal owner, title and supplementary title, then the number, revision, date, language and sheet.</summary>
    static TitleBlockTemplate IsoItems()
    {
        var items = new JsonArray { Rect(0, 0, 180, 36, 0.5) };
        void L(double x0, double y0, double x1, double y1, double w = 0.25) => items.Add((JsonNode)Line(x0, y0, x1, y1, w));
        void Field(double x, double y, string label, string value, double h = 2.5, bool bold = false)
        {
            items.Add((JsonNode)Text(x + 1.5, y + 5.6, label, 1.6));
            items.Add((JsonNode)Text(x + 1.5, y + 1.5, value, h, bold));
        }
        // The top row: technical reference, created by, approved by, document type.
        L(0, 27, 110, 27); L(110, 0, 110, 36, 0.5);
        foreach (var x in new[] { 27.5, 55, 82.5 }) L(x, 27, x, 36);
        Field(0, 27, "Technical reference", "{sheet.checkedBy}");
        Field(27.5, 27, "Created by", "{sheet.drawnBy}");
        Field(55, 27, "Approved by", "{sheet.approvedBy}");
        Field(82.5, 27, "Document type", "Drawing");
        // The document's status, the legal owner, the title and the supplementary title.
        L(0, 18, 110, 18); L(55, 0, 55, 18); L(55, 9, 110, 9); L(55, 18, 55, 27);
        Field(0, 18, "Document status", "{sheet.status}");
        Field(55, 18, "Responsible department", "{project.designer}");
        items.Add((JsonNode)Text(1.5, 14.5, "Legal owner", 1.6));
        items.Add((JsonNode)Text(1.5, 6, "{project.client}", 3.5, true));
        Field(55, 9, "Title", "{project.name}", 3, true);
        Field(55, 0, "Supplementary title", "{sheet.name}", 2.5);
        // The identification number, then revision, date of issue, language and sheet; the scale and the paper.
        L(110, 18, 180, 18); L(110, 9, 180, 9);
        Field(110, 27, "Identification number", "", 2.5);
        items.Add((JsonNode)Text(145, 20, "{sheet.number}", 5, true, "middle"));
        L(130, 9, 130, 18); L(160, 9, 160, 18); L(170, 9, 170, 18);
        Field(110, 9, "Rev.", "{sheet.revision}");
        Field(130, 9, "Date of issue", "{sheet.date}");
        Field(160, 9, "Lang.", "en");
        Field(170, 9, "Sheet", "{sheet.index}/{sheet.count}");
        L(145, 0, 145, 9);
        Field(110, 0, "Scale", "{sheet.scale}");
        Field(145, 0, "Paper", "{sheet.paper}");
        return new TitleBlockTemplate(180, 36, "bottomRight", new TitleBlockFrame(), "above", items);
    }
}
