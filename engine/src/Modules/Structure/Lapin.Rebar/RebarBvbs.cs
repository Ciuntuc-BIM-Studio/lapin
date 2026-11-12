using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// The bending schedule by the standard and the bending machines' file (docs/plan-user-extensibility.md §6.4). The schedule: each
/// mark's shape code (the user's shape's own code, else Lapin's), its dimensions A, B, C… (mm, the legs between corners), its count,
/// length and mass. The file: BVBS's (Bundesverband Bausoftware, „Datenaustausch von Bewehrungsdaten”, version 3.1, its public
/// guideline), a BF2D record a mark — header (project, plan, index, position, length, count, weight, diameter, steel, mandrel),
/// geometry (each leg's length on the outside, its bend's angle), checksum (96 − the sum of the record's characters to its C,
/// modulo 32) — one a line, CR LF, in a <c>.abs</c> file. Bars laid by a type (stirrups, column bars) have no drawn shape here
/// and are left out, said so.
/// </summary>
public static class RebarBvbs
{
    static string I(double v) => Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
    static string D(double v, string f = "0.###") => v.ToString(f, CultureInfo.InvariantCulture);

    /// <summary>A text as BVBS takes it: its separator and line ends left out.</summary>
    static string Clean(string? s) => (s ?? "").Replace("@", " ").Replace("\r", " ").Replace("\n", " ").Trim();

    /// <summary>The checksum of a record up to and with its C: 96 − (the sum of its characters' codes) mod 32.</summary>
    public static int Checksum(string upToC) => 96 - upToC.Sum(c => (int)c) % 32;

    /// <summary>
    /// A bar's legs as BVBS writes them (2D, outside dimensions): its nominal corners (hooks as legs), each leg's axis length plus half
    /// the diameter at each end that is a bend, then the angle it turns by at its end (°, counter-clockwise positive; the last 0). A
    /// 180° hook a half turn and its tail.
    /// </summary>
    public static List<(double LengthMm, double Turn)> Legs(RebarSpec s)
    {
        if (s.Shape == "dot") return [(s["length"] * 1000, 0)];
        var pts = RebarShapes.Nominal(s);
        var (start, end) = RebarShapes.HooksOf(s);
        var legs = new List<(double, double)>();
        var half = s.Diameter / 2;
        if (start is { Angle: >= RebarShapes.HalfTurn } h0) legs.Add(((h0.Length + half) * 1000, 180));
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var len = (pts[i + 1] - pts[i]).Length;
            if (len < 1e-9) continue;
            var bends = (i > 0 || start is { Angle: >= RebarShapes.HalfTurn } ? 1 : 0) + (i + 2 < pts.Count || end is { Angle: >= RebarShapes.HalfTurn } ? 1 : 0);
            double turn = 0;
            if (i + 2 < pts.Count)
            {
                var (u, v) = (pts[i + 1] - pts[i], pts[i + 2] - pts[i + 1]);
                turn = Math.Atan2(u.X * v.Y - u.Y * v.X, Vec2.Dot(u, v)) * 180 / Math.PI;
            }
            else if (end is { Angle: >= RebarShapes.HalfTurn }) turn = 180;
            legs.Add(((len + bends * half) * 1000, Math.Round(turn, 2)));
        }
        if (end is { Angle: >= RebarShapes.HalfTurn } h1) legs.Add(((h1.Length + half) * 1000, 0));
        return legs;
    }

    /// <summary>One mark's BF2D record, its checksum and CR LF.</summary>
    public static string Record(string project, string plan, string index, int mark, RebarSpec s, int count, string steel)
    {
        var length = RebarShapes.Length(s);
        var weight = length * RebarShapes.MassPerMetre(s.Diameter);
        var sb = new StringBuilder("BF2D@H");
        sb.Append('j').Append(Clean(project)).Append('@')
          .Append('r').Append(Clean(plan)).Append('@')
          .Append('i').Append(Clean(index)).Append('@')
          .Append('p').Append(mark.ToString(CultureInfo.InvariantCulture)).Append('@')
          .Append('l').Append(I(length * 1000)).Append('@')
          .Append('n').Append(count.ToString(CultureInfo.InvariantCulture)).Append('@')
          .Append('e').Append(D(weight)).Append('@')
          .Append('d').Append(I(s.Diameter * 1000)).Append('@')
          .Append('g').Append(Clean(steel)).Append('@')
          .Append('s').Append(I(RebarShapes.RadiusOf(s) * 2 * 1000)).Append('@')
          .Append("v@");
        sb.Append('G');
        foreach (var (len, turn) in Legs(s)) sb.Append('l').Append(I(len)).Append("@w").Append(D(turn, "0.##")).Append('@');
        sb.Append('C');
        var sum = Checksum(sb.ToString());
        sb.Append(sum.ToString(CultureInfo.InvariantCulture)).Append('@');
        return sb.ToString();
    }

    /// <summary>The marks drawn (bars and distributions), each its first bar's shape, all its bars counted.</summary>
    static List<(int Mark, RebarSpec Spec, int Count, Entity First)> Marks(IModelView m)
    {
        bool Drawn(Entity e) => !e.Bool("excludeSchedule") && Views.Find(m, e.Str("viewId") ?? "") != null;
        var o = new List<(int Mark, RebarSpec Spec, int Count, Entity First)>();
        foreach (var e in m.OfClass(Rebars.Class).Where(Drawn)) o.Add((Rebars.Mark(e), Rebars.SpecOf(e), Rebars.Pieces(e), e));
        foreach (var e in m.OfClass(Rebars.RunClass).Where(Drawn))
        {
            var s = Rebars.SpecOf(e);
            var (_, positions, _, _) = Rebars.RunOf(e, s);
            o.Add((Rebars.Mark(e), s, positions.Count * Rebars.Count(e), e));
        }
        return [.. o.GroupBy(x => x.Mark).OrderBy(g => g.Key).Select(g => (g.Key, g.First().Spec, g.Sum(x => x.Count), g.First().First))];
    }

    static string CodeOf(RebarSpec s, Entity e) =>
        e.Attrs["shapeDef"] is JsonObject d ? $"{(string?)d["standard"]} {(string?)d["code"]}".Trim() : s.Shape;

    public static void Register(IModuleRegistry r)
    {
        // The bending schedule by the standard: mark, Ø, shape code, A…, count, length, total, mass, one row a mark.
        r.Query("bendingSchedule", (q, _) =>
        {
            var marks = Marks(q.Model);
            var most = marks.Count == 0 ? 0 : marks.Max(x => RebarShapeData.Dims(x.Spec).Count);
            var dims = Enumerable.Range(0, Math.Min(most, 8)).Select(i => ((char)('A' + i)).ToString()).ToList();
            var rows = new JsonArray();
            foreach (var (mark, s, count, first) in marks)
            {
                var d = RebarShapeData.Dims(s);
                var len = RebarShapes.Length(s);
                var row = new JsonArray((JsonNode)mark, (JsonNode)Math.Round(s.Diameter * 1000), (JsonNode)CodeOf(s, first));
                for (var i = 0; i < dims.Count; i++) row.Add((JsonNode?)(i < d.Count ? d[i].Mm : null));
                row.Add((JsonNode)count); row.Add((JsonNode)Math.Round(len, 3)); row.Add((JsonNode)Math.Round(len * count, 3)); row.Add((JsonNode)Math.Round(len * count * RebarShapes.MassPerMetre(s.Diameter), 2));
                rows.Add((JsonNode)row);
            }
            return new JsonObject
            {
                ["columns"] = new JsonArray(new[] { "Mark", "Ø (mm)", "Shape" }.Concat(dims.Select(x => x + " (mm)")).Concat(["No.", "Length (m)", "Total (m)", "Mass (kg)"]).Select(c => (JsonNode)c).ToArray()),
                ["rows"] = rows,
            };
        });

        // The marks as a BVBS file (.abs): a BF2D record a mark; the plan its first bar's view, the index given.
        r.Query("exportBvbs", (q, a) =>
        {
            var m = q.Model;
            var project = Spatial.Project(m)?.Name ?? "";
            var sb = new StringBuilder();
            var count = 0;
            foreach (var (mark, s, n, first) in Marks(m))
            {
                var plan = Views.Find(m, first.Str("viewId") ?? "")?.Name ?? "";
                sb.Append(Record(project, (string?)a["plan"] ?? plan, (string?)a["index"] ?? "", mark, s, n, first.Str("steel") ?? RebarShapes.DefaultSteel)).Append("\r\n");
                count++;
            }
            var laid = TypeStirrups.Entries(m).Count() + ColumnBars.Entries(m).Count();
            if (count == 0) throw new CommandException(laid > 0 ? "Only bars laid by types: they have no drawn shape to write." : "There are no bars to write.");
            return new JsonObject { ["text"] = sb.ToString(), ["file"] = (Clean(project) is { Length: > 0 } p ? p : "bars") + ".abs", ["records"] = count, ["leftOut"] = laid };
        });
    }
}
