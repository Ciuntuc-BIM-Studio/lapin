using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// The roofs' timber as a cutting list (decision 229): every member of every framed roof, grouped by roof, role, section,
/// grade and length (to 10 mm), with the pieces, the total length and the volume; the totals per section and grade after.
/// </summary>
public sealed class RoofTimberSchedule : IScheduleSource
{
    public string Kind => "roofTimber";
    public string Label => "Roof timber";

    public sealed record Row(string Roof, string Role, string Section, string Grade, double Length, int Count, double Volume);

    public static List<Row> Rows(IModelView m)
    {
        var o = new List<Row>();
        foreach (var roof in m.OfClass(IfcClass.Roof).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (Roof.Resolve(roof, m) is not { Framed: true } r || Roof.Solve(r).Shape is not { } shape) continue;
            var members = RoofParts.Framing(r, shape, RoofOpenings.Place(roof, shape, m, []));
            o.AddRange(members.GroupBy(x => (x.Role, Section: Roof.TimberSection(x), Length: Math.Round(x.Length, 2)))
                .OrderBy(g => Order(g.Key.Role)).ThenBy(g => g.Key.Section).ThenByDescending(g => g.Key.Length)
                .Select(g => new Row(roof.Name, RoofParts.RoleLabel(g.Key.Role), g.Key.Section, r.Grade, g.Key.Length, g.Count(), g.Sum(x => x.Volume))));
        }
        return o;
    }

    /// <summary>
    /// A timber member as a part a field schedule lists (audit S1): what it is (its role's name, and the role's place in the
    /// list), its section, grade, length (to 10 mm, as the cutting list groups them) and volume, and which kind of element it is
    /// of (walls, floors, roofs: the order the timber schedule lists them in).
    /// </summary>
    public static ElementPart Part(string key, string role, object roleOrder, string section, string grade, double length, double volume, int elementOrder) =>
        ElementPart.Of(key, ("memberRole", role), ("memberRoleOrder", roleOrder), ("timberSection", section), ("timberGrade", grade),
            ("timberLength", new FieldValue(Math.Round(length, 2), Math.Round(length, 2).ToString("0.00", CultureInfo.InvariantCulture))),
            ("timberVolume", volume), ("timberElementOrder", elementOrder));

    public static readonly (string Key, string Label)[] PartFields =
    [
        ("memberRole", "Member"), ("memberRoleOrder", "Member's place in the list"), ("timberSection", "Section"), ("timberGrade", "Grade"),
        ("timberLength", "Length (m, to 10 mm)"), ("timberVolume", "Volume (m³)"), ("timberElementOrder", "Walls, floors, roofs: its element's place"),
    ];

    /// <summary>The members of a framed roof, each a part.</summary>
    public static IEnumerable<ElementPart> MemberParts(IModelView m, Entity roof)
    {
        if (roof.Class != IfcClass.Roof || Roof.Resolve(roof, m) is not { Framed: true } r || Roof.Solve(r).Shape is not { } shape) yield break;
        var i = 0;
        foreach (var x in RoofParts.Framing(r, shape, RoofOpenings.Place(roof, shape, m, [])))
            yield return Part((i++).ToString(CultureInfo.InvariantCulture), RoofParts.RoleLabel(x.Role), Order(x.Role), Roof.TimberSection(x), r.Grade, x.Length, x.Volume, 2);
    }

    /// <summary>The roofs' members (<c>roofTimber</c>).</summary>
    sealed class Parts : IPartSource
    {
        public string Source => "roofTimber";
        public string Label => "Roof timber members";
        public IReadOnlyList<(string Key, string Label)> Fields => PartFields;
        public IEnumerable<ElementPart> PartsOf(IQueryContext q, Entity element) => MemberParts(q.Model, element);
    }

    /// <summary>
    /// The columns of the cutting list as a field schedule (the timber schedule's too): by element, member, section, grade and
    /// length, the pieces, total length and volume, a totals line per section and grade.
    /// </summary>
    public static string TemplateFields(string source, string elementHeading, string? classes) => $$"""
        {
          "source": "{{source}}",
          {{(classes != null ? $"\"filter\": {{ \"classes\": [\"{classes}\"] }}," : "")}}
          "groupBy": ["name", "memberRole", "timberSection", "timberGrade", "timberLength"], "groupColumns": false, "count": false, "totals": false,
          "sort": ["timberElementOrder", "name", "memberRoleOrder", "timberSection", { "field": "timberLength", "desc": true }],
          "subtotals": ["timberSection", "timberGrade"],
          "columns": [
            { "field": "name", "heading": "{{elementHeading}}", "aggregate": "first" },
            { "field": "memberRole", "heading": "Member", "aggregate": "first" },
            { "field": "timberSection", "heading": "Section", "aggregate": "first" },
            { "field": "timberGrade", "heading": "Grade", "aggregate": "first" },
            { "field": "timberLength", "heading": "Length (m)", "aggregate": "first", "decimals": 2 },
            { "field": "timberLength", "heading": "Pieces", "aggregate": "rows" },
            { "field": "timberLength", "heading": "Total length (m)", "aggregate": "sum", "decimals": 2 },
            { "field": "timberVolume", "heading": "Volume (m³)", "aggregate": "sum", "decimals": 3 }
          ]
        }
        """;

    /// <summary>The roofs' members as parts, and the field schedule that gives this table (audit S1).</summary>
    public static void RegisterTemplate(IModuleRegistry r)
    {
        r.Service<IPartSource>(new Parts());
        r.Service(ShippedScheduleTemplate.Of("roofTimber", "Roof timber", "roofTimber", TemplateFields("roofTimber", "Roof", IfcClass.Roof)));
    }

    static int Order(string role) => Array.IndexOf(["plate", "rafter", "topChord", "bottomChord", "web", "collar", "purlin", "ridge", "hip", "valley", "trimmer"], role) is var i and >= 0 ? i : 99;

    public ScheduleGrid Grid(IQueryContext q)
    {
        string N(double v, string f = "0.00") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = Rows(q.Model);
        var grid = rows.Select(x => (IReadOnlyList<string>)[x.Roof, x.Role, x.Section, x.Grade, N(x.Length), x.Count.ToString(CultureInfo.InvariantCulture), N(x.Length * x.Count), N(x.Volume, "0.000")]).ToList();
        foreach (var t in rows.GroupBy(x => (x.Section, x.Grade)).OrderBy(g => g.Key.Section))
            grid.Add(["Total", "", t.Key.Section, t.Key.Grade, "", t.Sum(x => x.Count).ToString(CultureInfo.InvariantCulture), N(t.Sum(x => x.Length * x.Count)), N(t.Sum(x => x.Volume), "0.000")]);
        return new ScheduleGrid(Label, ["Roof", "Member", "Section", "Grade", "Length (m)", "Pieces", "Total length (m)", "Volume (m³)"], grid);
    }
}
