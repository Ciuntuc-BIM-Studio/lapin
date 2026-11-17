using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Steel;

/// <summary>
/// The connections' commands (decision 273): connections found from the members' topology, one added by its members and
/// kind, one's parameters changed (set on its own: a hall built again keeps them), and what one is made of.
/// </summary>
public static class NodeCommands
{
    public static void Register(IModuleRegistry r)
    {
        r.Class(new(SteelNodes.Class, "Steel connection", [], "#6f7b88"));
        r.Geometry(SteelNodes.Class, (e, m, _) => SteelNodes.Geometry(e, m));

        // The connections the selected members' topology asks for (all the model's when none is given): {ids?} → {ids}.
        r.Command("findNodes", "Find connections", (ctx, a) =>
        {
            var given = Args.Ids(a).Select(ctx.Model.Get).OfType<Entity>().ToList();
            // A connection or a hall selected stands for its members.
            var scope = new List<Entity>();
            foreach (var e in given)
                if (e.Class == SteelNodes.Class) scope.AddRange(SteelNodes.MembersOf(e).Select(j => ctx.Model.Get(j.Id)).OfType<Entity>());
                else if (e.Class == Halls.Class) scope.AddRange(Halls.Members(ctx.Model, e.Id).Values);
                else scope.Add(e);
            if (given.Count == 0)
                scope = [.. ctx.Model.OfClass(SteelModule.Member), .. ctx.Model.OfClass(IfcClass.Column), .. ctx.Model.OfClass(IfcClass.Beam)];
            var made = new JsonArray();
            foreach (var (kind, members) in SteelNodes.Find(ctx.Model, scope))
                made.Add((JsonNode)SteelNodes.Add(ctx, kind, members, NodeParams.None).Id);
            return new JsonObject { ["ids"] = made };
        });

        // One connection: {kind, members: [{id, end?}], params?} → {id}.
        r.Command("addNode", "Steel connection", (ctx, a) =>
        {
            var kind = (string?)a["kind"] ?? throw new CommandException("A connection needs its kind.");
            var members = (a["members"] as JsonArray)?.OfType<JsonObject>()
                .Select(o => new SteelNodes.Joined((string?)o["id"] ?? "", (string?)o["end"] is "start" or "end" ? (string)o["end"]! : null)).ToList() ?? [];
            var p = a["params"] is JsonObject po ? NodeParams.Patch(NodeParams.None, po) : NodeParams.None;
            return new JsonObject { ["id"] = SteelNodes.Add(ctx, kind, members, p, own: true).Id };
        });

        // A connection's parameters: {id (or ids), params: {plate?, bolt?, grade?, rows?, …}}; a field given as null goes back to its default.
        r.Command("updateNode", "Change connection", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if ((string?)a["id"] is { } one) ids = [one];
            var patch = a["params"] as JsonObject ?? throw new CommandException("Say which of the connection's parameters to change.");
            foreach (var id in ids)
            {
                var node = ctx.Model.Get(id) is { Class: SteelNodes.Class } n ? n : throw new CommandException("Not a steel connection.");
                SteelNodes.Update(ctx, node, patch);
            }
            return null;
        });

        // What a connection is made of: {id} → its kind, members, parameters (given and resolved), parts, welds, footing.
        r.Query("node", (q, a) =>
        {
            var node = q.Model.Get((string?)a["id"] ?? "") is { Class: SteelNodes.Class } n ? n : throw new CommandException("Not a steel connection.");
            return SteelNodes.Info(q.Model, node);
        });

        r.Service<IDeletion>(new SteelDeletion());
        r.Service<IScheduleSource>(new SteelPartsSchedule());
        SteelPartsSchedule.RegisterTemplate(r);
    }
}

/// <summary>
/// Deleting steel (decision 273): a hall goes with its members, its connections and its grid, as a box goes with its
/// slabs; a connection gives its members' ends back the setbacks it wrote; a member deleted takes its connections along
/// (the member itself is the core's to delete).
/// </summary>
sealed class SteelDeletion : IDeletion
{
    public bool Delete(ICommandContext ctx, string id)
    {
        if (ctx.Model.Get(id) is not { } e) return false;
        switch (e.Class)
        {
            case SteelNodes.Class:
                SteelNodes.Release(ctx, e);
                ctx.Remove(id);
                return true;
            case Halls.Class:
                foreach (var n in Halls.Nodes(ctx.Model, id).Values.ToList()) ctx.Remove(n.Id);
                foreach (var mem in Halls.Members(ctx.Model, id).Values.ToList())
                {
                    // A connection made on its own to a hall's member (with a beam of the user's) goes with the member.
                    SteelNodes.DropWith(ctx, mem.Id);
                    ctx.Remove(mem.Id);
                }
                if (e.Str("gridId") is { } grid && ctx.Model.Get(grid) != null) ctx.Remove(grid);
                ctx.Remove(id);
                return true;
            case SteelModule.Member or IfcClass.Column or IfcClass.Beam:
                SteelNodes.DropWith(ctx, id);
                return false;
        }
        return false;
    }
}

/// <summary>
/// The Steel parts schedule (decision 273): the connections' plates, bolts, anchors and anchor plates counted by size and
/// grade with their weight, and their welds by throat with their length.
/// </summary>
public sealed class SteelPartsSchedule : IScheduleSource
{
    public string Kind => "steelParts";
    public string Label => "Steel parts";

    public sealed record Row(string Part, string Size, string Grade, int Count, double Length, double Weight);

    public static List<Row> Rows(IModelView m)
    {
        var parts = new List<(string Part, string Size, string Grade, double Kg)>();
        var welds = new List<SteelNodes.Weld>();
        foreach (var node in m.OfClass(SteelNodes.Class))
        {
            var b = SteelNodes.Build(m, node);
            foreach (var p in b.Parts)
            {
                var props = p.Properties ?? [];
                parts.Add((SteelNodes.P(props, "Part"), SteelNodes.P(props, "Size"), SteelNodes.P(props, "Grade"), Measure.Volume(p.Solid) * SteelModule.SteelDensity));
            }
            welds.AddRange(b.Welds);
        }
        string[] order = ["Plate", "Bolt", "Anchor", "Anchor plate"];
        var o = parts.GroupBy(p => (p.Part, p.Size, p.Grade))
            .OrderBy(g => Array.IndexOf(order, g.Key.Part)).ThenBy(g => g.Key.Size, StringComparer.Ordinal).ThenBy(g => g.Key.Grade, StringComparer.Ordinal)
            .Select(g => new Row(g.Key.Part, g.Key.Size, g.Key.Grade, g.Count(), 0, g.Sum(x => x.Kg))).ToList();
        foreach (var g in welds.GroupBy(w => w.Throat).OrderBy(g => g.Key))
            o.Add(new Row("Weld", $"a{Math.Round(g.Key * 1000).ToString(CultureInfo.InvariantCulture)}", "", g.Count(), g.Sum(w => w.Length), 0));
        return o;
    }

    /// <summary>
    /// A connection's plates, bolts, anchors and welds as parts (<c>steelParts</c>), each with its row of the schedule (the
    /// order it lists them in, by part, size and grade), its weight (a weld: its length).
    /// </summary>
    sealed class Parts : IPartSource
    {
        public string Source => "steelParts";
        public string Label => "Steel connection parts";
        public IReadOnlyList<(string Key, string Label)> Fields =>
        [
            ("steelPartRow", "Row of the parts schedule"), ("steelPart", "Part"), ("steelSize", "Size"), ("steelGrade", "Grade"),
            ("steelWeldLength", "Weld length (m)"), ("steelPartWeight", "Weight (kg)"),
        ];

        public IEnumerable<ElementPart> PartsOf(IQueryContext q, Entity e)
        {
            if (e.Class != SteelNodes.Class) yield break;
            var rows = RowsOf(q.Model);
            double Row(string part, string size, string grade) => rows.TryGetValue((part, size, grade), out var r) ? r : rows.Count;
            var b = SteelNodes.Build(q.Model, e);
            var i = 0;
            foreach (var p in b.Parts)
            {
                var props = p.Properties ?? [];
                var part = SteelNodes.P(props, "Part");
                yield return ElementPart.Of((i++).ToString(CultureInfo.InvariantCulture), ("steelPartRow", Row(part, SteelNodes.P(props, "Size"), SteelNodes.P(props, "Grade"))), ("steelPart", part),
                    ("steelSize", SteelNodes.P(props, "Size")), ("steelGrade", SteelNodes.P(props, "Grade")), ("steelPartWeight", Measure.Volume(p.Solid) * SteelModule.SteelDensity));
            }
            foreach (var w in b.Welds)
            {
                var size = $"a{Math.Round(w.Throat * 1000).ToString(CultureInfo.InvariantCulture)}";
                yield return ElementPart.Of((i++).ToString(CultureInfo.InvariantCulture), ("steelPartRow", Row("Weld", size, "")), ("steelPart", "Weld"), ("steelSize", size), ("steelWeldLength", w.Length));
            }
        }
    }

    /// <summary>Each row of the schedule by its part, size and grade: its place in the list (worked out once per edit).</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IModelView, Tuple<long, Dictionary<(string, string, string), double>>> _rows = [];

    static Dictionary<(string, string, string), double> RowsOf(IModelView m)
    {
        if (_rows.TryGetValue(m, out var c) && c.Item1 == m.Revision) return c.Item2;
        var d = new Dictionary<(string, string, string), double>();
        foreach (var (r, i) in Rows(m).Select((r, i) => (r, i))) d.TryAdd((r.Part, r.Size, r.Grade), i);
        _rows.AddOrUpdate(m, Tuple.Create(m.Revision, d));
        return d;
    }

    /// <summary>
    /// The schedule as a field schedule: a row per part, size and grade, its count, its welds' length and its weight. Its totals
    /// line counts the welds too (the fixed schedule counts the pieces only) and leaves the length blank when there are no welds.
    /// </summary>
    public const string TemplateFields = """
        {
          "source": "steelParts", "groupBy": ["steelPartRow"], "groupColumns": false, "count": false, "totals": "always",
          "sort": ["steelPartRow"],
          "columns": [
            { "field": "steelPart", "heading": "Part", "aggregate": "first" },
            { "field": "steelSize", "heading": "Size", "aggregate": "first" },
            { "field": "steelGrade", "heading": "Grade", "aggregate": "first" },
            { "field": "steelPart", "heading": "Count", "aggregate": "rows" },
            { "field": "steelWeldLength", "heading": "Length (m)", "aggregate": "sum", "decimals": 2 },
            { "field": "steelPartWeight", "heading": "Weight (kg)", "aggregate": "sum", "decimals": 1 }
          ]
        }
        """;

    /// <summary>The connections' parts as parts and this schedule as a template (offered beside it; its button stays the fixed one, for its count).</summary>
    public static void RegisterTemplate(IModuleRegistry r)
    {
        r.Service<IPartSource>(new Parts());
        r.Service(ShippedScheduleTemplate.Of("steelParts", "Steel parts", null, TemplateFields));
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        string N(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = Rows(q.Model);
        var grid = rows.Select(r => (IReadOnlyList<string>)[r.Part, r.Size, r.Grade, r.Count.ToString(CultureInfo.InvariantCulture), r.Length > 0 ? N(r.Length, "0.00") : "", r.Weight > 0 ? N(r.Weight, "0.0") : ""]).ToList();
        grid.Add(["Total", "", "", rows.Where(r => r.Part != "Weld").Sum(r => r.Count).ToString(CultureInfo.InvariantCulture), N(rows.Sum(r => r.Length), "0.00"), N(rows.Sum(r => r.Weight), "0.0")]);
        return new ScheduleGrid(Label, ["Part", "Size", "Grade", "Count", "Length (m)", "Weight (kg)"], grid);
    }
}
