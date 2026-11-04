using System.Globalization;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// The gas and fuel oil bill of materials (decision 285), system by system:
/// the pipes by material and size with their length (of which the risers,
/// the vertical lengths), the bends and tees they make, the sleeves through
/// walls, the warning tape over the buried pipes, the fittings, valves,
/// meters and boxes by role, and the appliances on the system's runs (a
/// heating system's gas boiler too) with their type and consumption, summed.
/// Quantities, not a design: the sizes and pressure drops are mep-pro's.
/// </summary>
public sealed class GasSchedule : IScheduleSource
{
    public string Kind => "mep-gas";
    public string Label => "Gas and fuel oil bill of materials";

    /// <summary>A run's lengths as built: along its centreline, and of that what stands vertical (the risers).</summary>
    public static (double Length, double Vertical, double Plan) Lengths(IModelView m, Entity run)
    {
        var line = Runs.Centreline(m, run, Runs.SettingOf(m, run), out _);
        double length = 0, vertical = 0, plan = 0;
        for (var i = 1; i < line.Count; i++)
        {
            var d = line[i] - line[i - 1];
            length += d.Length;
            var flat = d.Xy.Length;
            plan += flat;
            if (flat < 1e-6) vertical += Math.Abs(d.Z);
        }
        return (length, vertical, plan);
    }

    /// <summary>A run's bends: the turns of its centreline of 15° or more (a buried PE pipe is laid bent: none).</summary>
    public static int Bends(IModelView m, Entity run)
    {
        var s = Runs.SettingOf(m, run);
        if (s.Material == "pe") return 0;
        var line = Runs.Centreline(m, run, s, out _);
        var bends = 0;
        for (var i = 1; i + 1 < line.Count; i++)
        {
            var a = line[i] - line[i - 1]; var b = line[i + 1] - line[i];
            if (a.Length < 1e-9 || b.Length < 1e-9) continue;
            if (Vec3.Dot(a.Normalized(), b.Normalized()) < Math.Cos(15 * Math.PI / 180)) bends++;
        }
        return bends;
    }

    /// <summary>The nodes a system's runs reach (an appliance of another system, a heating system's gas boiler, among them), and its own.</summary>
    public static List<Entity> NodesOf(IModelView m, Entity system)
    {
        var o = Nodes.Of(m, system.Id).ToList();
        var seen = o.Select(n => n.Id).ToHashSet();
        foreach (var run in Runs.Of(m, system.Id))
            foreach (var end in new[] { "start", "end" })
                if (Runs.EndOf(m, run, end) is { NodeId: { } id } && seen.Add(id) && m.Get(id) is { } n && Nodes.IsNode(n)) o.Add(n);
        return o;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var system in Systems.All(m).Where(s => Services.IsFuel(Systems.Service(s))).OrderBy(s => s.Name))
        {
            var service = Systems.Service(system);
            var d = Systems.Of(system);
            var name = service == Services.Gas
                ? $"{system.Name} ({(d.Gas == "lpg" ? "LPG" : "natural gas")}, {(d.Pressure == "medium" ? "medium" : "low")} pressure)"
                : system.Name;
            void Row(string group, string item, string material, string size, double qty, string unit, string format = "0.##") =>
                rows.Add([name, group, item, material, size, N(qty, format), unit]);

            // Pipes by material and size: length, of which vertical; bends and tees; sleeves; warning tape.
            var runs = Runs.Of(m, system.Id).ToList();
            foreach (var g in runs.GroupBy(r => (Material: Runs.SettingOf(m, r).Material ?? "", Size: Math.Round(Runs.SettingOf(m, r).Size * 1000))).OrderBy(g => g.Key.Material).ThenBy(g => g.Key.Size))
            {
                var label = RunMaterials.Get(g.Key.Material)?.Label ?? g.Key.Material;
                var size = N(g.Key.Size, "0");
                double length = 0, vertical = 0;
                int bends = 0, tees = 0;
                foreach (var run in g)
                {
                    var pipes = Runs.SettingOf(m, run).Pair ? 2 : 1;
                    var (l, v, _) = Lengths(m, run);
                    length += l * pipes; vertical += v * pipes;
                    bends += Bends(m, run) * pipes;
                    tees += Runs.BranchesOf(m, run.Id).Count() * pipes;
                }
                Row("Pipes", "Pipe", label, size, length, "m");
                if (vertical > 1e-6) Row("Pipes", "of which risers", label, size, vertical, "m");
                if (bends > 0) Row("Fittings", "Bend", label, size, bends, "pcs", "0");
                if (tees > 0) Row("Fittings", "Tee", label, size, tees, "pcs", "0");
            }
            foreach (var g in runs.SelectMany(r => Sleeves.Of(m, r, Runs.SettingOf(m, r), Runs.Centreline(m, r, Runs.SettingOf(m, r), out _))).GroupBy(x => Math.Round(x.Diameter * 1000)).OrderBy(g => g.Key))
                Row("Fittings", "Wall sleeve", "Steel", N(g.Key, "0"), g.Count(), "pcs", "0");
            var tape = runs.Where(r => r.Attrs["warningTape"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<bool>(out var t) && t).Sum(r => Lengths(m, r).Plan);
            if (tape > 1e-6) Row("Pipes", "Warning tape", "", "", tape, "m");

            // Fittings, valves, meters, boxes, appliances by role; the appliances with their type and consumption.
            double consumption = 0, duty = 0;
            foreach (var g in NodesOf(m, system).Select(n => (Node: n, Role: Nodes.RoleOf(n)!)).GroupBy(x => (x.Role.Id, Flue: Nodes.Flue(x.Node, x.Role))).OrderBy(g => g.First().Role.Label))
            {
                var role = g.First().Role;
                var appliance = role.KindIn(service) == "endpoint" && Nodes.Consumption(g.First().Node, role) > 0;
                var group = appliance ? "Appliances" : role.KindIn(service) is "inline" or "rise" ? "Fittings and valves" : "Equipment";
                var item = role.Label + (appliance && g.Key.Flue is { } f ? $" (type {f})" : "");
                Row(group, item, "", "", g.Count(), "pcs", "0");
                if (!appliance) continue;
                foreach (var (n, r) in g)
                {
                    consumption += Nodes.Consumption(n, r);
                    duty += n.NumOrNull("duty") ?? r.Duty;
                }
            }
            if (consumption > 0)
            {
                Row("Appliances", "Installed consumption", "", "", consumption, service == Services.FuelOil ? "L/h" : "m³/h");
                Row("Appliances", "Installed heat output", "", "", duty / 1000, "kW", "0.#");
            }
        }
        return new ScheduleGrid(Label, ["System", "Group", "Item", "Material", "Size (mm)", "Quantity", "Unit"], rows);
    }
}
