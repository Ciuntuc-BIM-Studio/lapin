using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>The equipment schedule: every node, with its role, system, storey and duty; a table the schedules show and export.</summary>
public sealed class MepEquipmentSchedule : IScheduleSource
{
    public string Kind => "mep-equipment";
    public string Label => "MEP equipment schedule";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rows = new List<IReadOnlyList<string>>();
        foreach (var system in Systems.All(m).OrderBy(s => s.Name))
            foreach (var n in Nodes.Of(m, system.Id).OrderBy(n => n.Name))
            {
                var role = Nodes.RoleOf(n);
                var duty = n.NumOrNull("duty") ?? role?.Duty ?? 0;
                rows.Add([n.Name, role?.Label ?? n.Str("role") ?? "", system.Name, Spatial.StoreyOf(m, n.Id)?.Name ?? "",
                    duty > 0 ? duty.ToString("0.##", CultureInfo.InvariantCulture) : "", duty > 0 ? role?.DutyUnit ?? "" : ""]);
            }
        if (rows.Count > 0) rows.Add(["Total", "", "", "", rows.Count.ToString(CultureInfo.InvariantCulture), "pcs"]);
        return new ScheduleGrid(Label, ["Name", "Role", "System", "Storey", "Duty", "Unit"], rows);
    }
}

/// <summary>The run schedule: every run with its system, size, length and material (mep-pro's adds the demand each carries and the size that would take it).</summary>
public sealed class MepRunSchedule : IScheduleSource
{
    public string Kind => "mep-runs";
    public string Label => "MEP run schedule";

    /// <summary>A run's length as built (the centreline, its jogs included).</summary>
    public static double Length(IModelView m, Entity run, Runs.Setting s)
    {
        var line = Runs.Centreline(m, run, s, out _);
        double length = 0;
        for (var i = 1; i < line.Count; i++) length += (line[i] - line[i - 1]).Length;
        return length;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = new List<IReadOnlyList<string>>();
        double total = 0, pipes = 0;
        foreach (var system in Systems.All(m).OrderBy(s => s.Name))
            foreach (var run in Runs.Of(m, system.Id).OrderBy(r => r.Name))
            {
                var s = Runs.SettingOf(m, run);
                var length = Length(m, run, s);
                total += length;
                pipes += s.Pair ? 2 * length : length;
                rows.Add([run.Name, system.Name, Spatial.StoreyOf(m, run.Id)?.Name ?? "", N(s.Size * 1000, "0"), s.Pair ? "2" : "1", N(length), N(s.Pair ? 2 * length : length),
                    RunMaterials.Get(s.Material)?.Label ?? s.Material ?? ""]);
            }
        if (rows.Count > 0) rows.Add(["Total", "", "", "", "", N(total), N(pipes), ""]);
        return new ScheduleGrid(Label, ["Name", "System", "Storey", "Size (mm)", "Pipes", "Length (m)", "Pipe length (m)", "Material"], rows);
    }
}
