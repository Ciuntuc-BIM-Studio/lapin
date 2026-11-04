using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// The wall sweeps schedule (decision 298): the project's sweeps and reveals added up by profile, kind and material (and,
/// as the schedule asks, <c>groupBy</c> ["storey"] or ["type"], by storey or by wall type first): how many, their pieces
/// along the faces, their length and their volume (for a reveal the volume it takes out of the wall); a total for the
/// sweeps and one for the reveals, never one for both.
/// </summary>
public sealed class WallSweepSchedule : IScheduleSource
{
    public const string KindId = "wallSweeps";
    public string Kind => KindId;
    public string Label => "Wall sweeps";

    public sealed record Row(string Group, string Profile, string Mode, string Material, int Count, int Pieces, double Length, double Volume);

    /// <summary>What the rows are grouped by first: "storey", "type" or nothing.</summary>
    public static string? GroupOf(Entity? schedule) =>
        schedule?.Attrs["groupBy"] is JsonArray a && a.Select(x => (string?)x).FirstOrDefault(x => x is "storey" or "type") is { } g ? g : null;

    public static List<Row> Rows(IQueryContext q, string? groupBy = null)
    {
        var m = q.Model;
        var items = new List<(string Group, string Profile, string Mode, string Material, int Pieces, double Length, double Volume, double Order)>();
        foreach (var s in m.OfClass(WallSweeps.Class))
        {
            var wall = WallSweeps.HostOf(m, s);
            var storey = Spatial.StoreyOf(m, s.Id) ?? (wall != null ? Spatial.StoreyOf(m, wall.Id) : null);
            var group = groupBy switch
            {
                "storey" => storey?.Name ?? "",
                "type" => wall != null ? Walls.TypeOf(m, wall.Id)?.Name ?? "" : "",
                _ => "",
            };
            var order = groupBy == "storey" ? storey?.Num("elevation") ?? 0 : 0;
            var cut = s.Str("mode") == "cut";
            var qs = q.Quantities(s.Id).FirstOrDefault(x => x.Name == "Lapin_WallSweepQuantities")?.Quantities ?? [];
            double Q(string n) => qs.FirstOrDefault(x => x.Name == n)?.Value ?? 0;
            var profile = Profiles.Of(m, s)?.Name is { Length: > 0 } pn ? pn : "";
            var material = cut ? "" : m.Get(s.Str("material") ?? "")?.Name ?? "";
            items.Add((group, profile, cut ? "Reveal" : "Sweep", material, (int)Q("Pieces"), Q("Length"), Q(cut ? "RemovedVolume" : "Volume"), order));
        }
        var cmp = StringComparer.OrdinalIgnoreCase;
        return items.GroupBy(x => (x.Group, x.Profile, x.Mode, x.Material))
            .OrderBy(g => g.First().Order).ThenBy(g => g.Key.Group, cmp).ThenBy(g => g.Key.Mode == "Sweep" ? 0 : 1).ThenBy(g => g.Key.Profile, cmp).ThenBy(g => g.Key.Material, cmp)
            .Select(g => new Row(g.Key.Group, g.Key.Profile, g.Key.Mode, g.Key.Material, g.Count(), g.Sum(x => x.Pieces), g.Sum(x => x.Length), g.Sum(x => x.Volume)))
            .ToList();
    }

    public ScheduleGrid Grid(IQueryContext q) => Table(q, null);
    public ScheduleGrid Grid(IQueryContext q, Entity schedule) => Table(q, GroupOf(schedule));

    static ScheduleGrid Table(IQueryContext q, string? groupBy)
    {
        static string N(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);
        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        var rows = Rows(q, groupBy);
        var head = groupBy == null ? new List<string>() : [groupBy == "storey" ? "Storey" : "Wall type"];
        List<string> Lead(string group) => groupBy == null ? [] : [group];
        var grid = rows.Select(r => (IReadOnlyList<string>)[.. Lead(r.Group), r.Profile, r.Mode, r.Material, I(r.Count), I(r.Pieces), N(r.Length, "0.00"), N(r.Volume, "0.0000")]).ToList();
        foreach (var t in rows.GroupBy(r => r.Mode).OrderBy(g => g.Key == "Sweep" ? 0 : 1))
            grid.Add([.. Lead(""), t.Key == "Sweep" ? "Total sweeps" : "Total reveals", t.Key, "", I(t.Sum(r => r.Count)), I(t.Sum(r => r.Pieces)), N(t.Sum(r => r.Length), "0.00"), N(t.Sum(r => r.Volume), "0.0000")]);
        return new ScheduleGrid("Wall sweeps", [.. head, "Profile", "Kind", "Material", "Count", "Pieces", "Length (m)", "Volume (m³)"], grid);
    }
}
