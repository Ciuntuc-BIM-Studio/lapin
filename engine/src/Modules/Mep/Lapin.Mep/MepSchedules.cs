using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>The equipment schedule: every node, with its role, system, storey and duty; a table the schedules show and export.</summary>
public sealed class MepEquipmentSchedule : IScheduleSource
{
    public string Kind => "mep-equipment";
    public string Label => "MEP equipment schedule";

    static double Duty(Entity n) => n.NumOrNull("duty") ?? Nodes.RoleOf(n)?.Duty ?? 0;

    /// <summary>
    /// What the schedule shows of a node, as fields, and the field schedule that gives the same table (audit S1): a row per node
    /// of a system, by system and name, the count under the duties. The fixed one's „pcs” under the units is not repeated.
    /// </summary>
    public static void RegisterTemplate(IModuleRegistry r)
    {
        static Entity? System(IModelView m, Entity n) => Nodes.IsNode(n) && m.Get(n.Str("systemId") ?? "") is { Class: Systems.Class } s ? s : null;
        foreach (var cls in Nodes.Classes)
        {
            DerivedFields.Add(cls, new("mepRole", "MEP: role", (m, n) => System(m, n) != null ? Nodes.RoleOf(n)?.Label ?? n.Str("role") : null));
            DerivedFields.Add(cls, new("mepSystem", "MEP: system", (m, n) => System(m, n)?.Name));
            DerivedFields.Add(cls, new("mepDuty", "MEP: duty", (m, n) => System(m, n) != null && Duty(n) > 0 ? Duty(n).ToString("0.##", CultureInfo.InvariantCulture) : null));
            DerivedFields.Add(cls, new("mepDutyUnit", "MEP: duty's unit", (m, n) => System(m, n) != null && Duty(n) > 0 ? Nodes.RoleOf(n)?.DutyUnit : null));
        }
        r.Service(ShippedScheduleTemplate.Of("mep-equipment", "MEP equipment schedule", "mep-equipment", new JsonObject
        {
            ["filter"] = new JsonObject
            {
                ["classes"] = new JsonArray([.. Nodes.Classes.Select(c => (JsonNode)c)]),
                ["conditions"] = new JsonArray(new JsonObject { ["field"] = "mepSystem", ["op"] = "exists" }),
            },
            ["sort"] = new JsonArray("mepSystem", "name"), ["totals"] = "always",
            ["columns"] = JsonNode.Parse("""
                [
                  { "field": "name", "heading": "Name", "aggregate": "first" },
                  { "field": "mepRole", "heading": "Role", "aggregate": "first" },
                  { "field": "mepSystem", "heading": "System", "aggregate": "first" },
                  { "field": "storey.name", "heading": "Storey", "aggregate": "first" },
                  { "field": "mepDuty", "heading": "Duty", "aggregate": "rows" },
                  { "field": "mepDutyUnit", "heading": "Unit", "aggregate": "first" }
                ]
                """),
        }.ToJsonString()));
    }

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
