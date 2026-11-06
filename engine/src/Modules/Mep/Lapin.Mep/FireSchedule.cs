using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// The fire protection bill of materials (a tester's request, report #89), system by system: for a detection and alarm
/// system, its panels, detectors, call points and sounders by kind, the loop's cable by material with its length, and
/// whether each loop is closed back to its panel; for a hydrant system, its reserve, pump sets, hydrants, inlets and
/// sprinklers, the pipes by material and size with their length (of which the risers), the bends and tees. Quantities,
/// not a design: the coverage and the hydraulics are checked by the designer (mep-pro's checks help).
/// </summary>
public sealed class FireSchedule : IScheduleSource
{
    public string Kind => "mep-fire";
    public string Label => "Fire protection bill of materials";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var system in Systems.All(m).Where(s => Services.IsFire(Systems.Service(s))).OrderBy(s => s.Name))
        {
            var service = Systems.Service(system);
            void Row(string group, string item, string material, string size, double qty, string unit, string format = "0.##") =>
                rows.Add([system.Name, group, item, material, size, N(qty, format), unit]);

            // The devices and equipment by role (and the coverage a detector or a hydrant is drawn with).
            foreach (var g in Nodes.Of(m, system.Id).Select(n => (Node: n, Role: Nodes.RoleOf(n)!)).GroupBy(x => x.Role.Id).OrderBy(g => g.First().Role.Label))
            {
                var role = g.First().Role;
                var group = role.KindIn(service) == "source" ? "Equipment" : role.KindIn(service) is "inline" or "rise" ? "Fittings and valves" : service == Services.FireAlarm ? "Devices" : "Terminals";
                Row(group, role.Label, "", "", g.Count(), "pcs", "0");
            }

            // The runs: a loop's cable by material; pipes by material and size, of which risers, bends and tees.
            var runs = Runs.Of(m, system.Id).ToList();
            foreach (var g in runs.GroupBy(r => (Material: Runs.SettingOf(m, r).Material ?? "", Size: Math.Round(Runs.SettingOf(m, r).Size * 1000))).OrderBy(g => g.Key.Material).ThenBy(g => g.Key.Size))
            {
                var label = RunMaterials.Get(g.Key.Material)?.Label ?? g.Key.Material;
                double length = 0, vertical = 0;
                int bends = 0, tees = 0;
                foreach (var run in g)
                {
                    var (l, v, _) = GasSchedule.Lengths(m, run);
                    length += l; vertical += v;
                    bends += GasSchedule.Bends(m, run);
                    tees += Runs.BranchesOf(m, run.Id).Count();
                }
                if (service == Services.FireAlarm)
                {
                    Row("Cable", "Loop cable", label, "", length, "m");
                    continue;
                }
                var size = N(g.Key.Size, "0");
                Row("Pipes", "Pipe", label, size, length, "m");
                if (vertical > 1e-6) Row("Pipes", "of which risers", label, size, vertical, "m");
                if (bends > 0) Row("Fittings and valves", "Bend", label, size, bends, "pcs", "0");
                if (tees > 0) Row("Fittings and valves", "Tee", label, size, tees, "pcs", "0");
            }

            // Each panel's loop: closed back to its return, or open.
            if (service == Services.FireAlarm)
                foreach (var panel in Nodes.Of(m, system.Id).Where(n => Nodes.RoleOf(n)?.Id == "firePanel").OrderBy(n => n.Name))
                    rows.Add([system.Name, "Loops", panel.Name, "", "", Runs.PortTaken(m, panel, "return") ? "closed" : "open", ""]);
        }
        return new ScheduleGrid(Label, ["System", "Group", "Item", "Material", "Size (mm)", "Quantity", "Unit"], rows);
    }
}
