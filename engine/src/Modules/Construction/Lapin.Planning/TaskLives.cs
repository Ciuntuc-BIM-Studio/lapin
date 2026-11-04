using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>A temporary element's time on site: from the day it goes up to the last day it stands, and the tasks it lives by.</summary>
public sealed record TemporaryTime(string Element, string Kind, DateOnly From, DateOnly To, IReadOnlyList<string> Tasks);

/// <summary>
/// The temporaries on the schedule (docs/plan-4d5d.md, stage 3): a formwork or a scaffold has no task, its dates come from
/// its hosts' tasks (<see cref="ITaskLife"/> says which hosts): up when the first task building a host starts; a formwork
/// struck its strip-after working days after the task casting its host's core finishes, a scaffold down when the last task
/// on its walls (their finishes too) finishes. Derived, never stored; switched off by kind on the schedule.
/// </summary>
public static class TaskLives
{
    /// <summary>Whether the schedule ties a kind of temporary to its hosts (the generator's switches; on when never set).</summary>
    public static bool Tied(Plan p, string kind)
    {
        var spec = p.Schedule?.Attrs["generator"] as System.Text.Json.Nodes.JsonObject;
        var key = kind == "formwork" ? "formwork" : "scaffolding";
        return spec?[key] is not System.Text.Json.Nodes.JsonValue v || !v.TryGetValue<bool>(out var b) || b;
    }

    public static List<TemporaryTime> Of(Plan p, CpmResult r, TaskElements links, IServices services, Lapin.Model.IModelView m)
    {
        var o = new List<TemporaryTime>();
        var lives = services.All<ITaskLife>().SelectMany(l => l.Temporaries(m)).ToList();
        if (lives.Count == 0) return o;
        // Each element's tasks with the layers they take of it.
        var byElement = new Dictionary<string, List<(string Task, ulong Mask)>>();
        foreach (var (task, own) in links.ByTask)
            foreach (var (id, mask) in own) (byElement.TryGetValue(id, out var l) ? l : byElement[id] = []).Add((task, mask));
        foreach (var life in lives)
        {
            if (!Tied(p, life.Kind)) continue;
            var tasks = life.Hosts.SelectMany(h => byElement.GetValueOrDefault(h) ?? []).Where(x => r.Times.ContainsKey(x.Task)).ToList();
            if (tasks.Count == 0) continue;
            var from = tasks.Min(x => r.Times[x.Task].Start);
            DateOnly to;
            if (life.UntilLast) to = tasks.Max(x => r.Times[x.Task].Finish);
            else
            {
                // The task casting the host: the one taking its core (an element without layers is all core).
                var casting = new List<(string Task, ulong Mask)>();
                foreach (var h in life.Hosts)
                {
                    var core = links.Facts(h)?.Layers.FirstOrDefault(l => l.Core)?.Index ?? 0;
                    casting.AddRange((byElement.GetValueOrDefault(h) ?? []).Where(x => r.Times.ContainsKey(x.Task) && core < 64 && (x.Mask & (1UL << core)) != 0));
                }
                if (casting.Count == 0) casting = tasks;
                var end = casting.MaxBy(x => r.Times[x.Task].Finish).Task;
                var t = r.Times[end];
                to = life.StripAfter <= 0 ? t.Finish : p.Calendar.Offset(t.EarlyFinish > t.EarlyStart ? t.EarlyFinish : t.EarlyStart.AddDays(1), life.StripAfter).AddDays(-1);
            }
            o.Add(new(life.Element, life.Kind, from, to < from ? from : to, tasks.Select(x => x.Task).Distinct().ToList()));
        }
        return o;
    }
}
