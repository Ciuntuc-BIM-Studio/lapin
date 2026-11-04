using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// Two versions' work schedules compared (docs/plan-4d5d.md, stage 5: Manage → Versions → Compare says, beside the
/// elements, the difference in time): the finish and the working days, the status date and how far along the works are,
/// then each task added, removed, moved (its start, by working days), lengthened or shortened, or whose finish moved. The
/// dates are the critical path's on each side, read from the schedule as each version keeps it (its tasks' own progress
/// fields; the elements' marks need the version's quantities and are left to the project as it is).
/// </summary>
public sealed class ScheduleCompare : IVersionCompare
{
    static string D(DateOnly d) => d.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

    public VersionDiffSection? Compare(IModelView a, IModelView b)
    {
        var pa = Plan.Read(a);
        var pb = Plan.Read(b);
        if (pa.Rows.Count == 0 && pb.Rows.Count == 0) return null;
        var (ra, _) = PlanningModule.Solve(pa);
        var (rb, _) = PlanningModule.Solve(pb);
        var cal = pb.Calendar;
        DateOnly Finish(Plan p, CpmResult r) => r.Times.Count > 0 ? r.Times.Values.Max(t => t.Finish) : p.Start;
        var fa = Finish(pa, ra);
        var fb = Finish(pb, rb);
        var facts = new List<(string, string?, string?)>();
        if (pa.Rows.Count > 0 && pb.Rows.Count > 0 && fa != fb) facts.Add(("Finish", D(fa), D(fb)));
        var wa = pa.Calendar.WorkingDays(pa.Calendar.NextWorking(pa.Start), ra.Finish);
        var wb = cal.WorkingDays(cal.NextWorking(pb.Start), rb.Finish);
        if (wa != wb) facts.Add(("Working days", $"{wa}", $"{wb}"));
        if (pa.DataDate != pb.DataDate) facts.Add(("Status date", pa.DataDate is { } x ? D(x) : null, pb.DataDate is { } y ? D(y) : null));
        if (pa.Rows.Count != pb.Rows.Count) facts.Add(("Tasks", $"{pa.Rows.Count}", $"{pb.Rows.Count}"));

        var rows = new List<VersionDiffRow>();
        foreach (var t in pb.Rows)
        {
            var tb = rb.Times[t.Id];
            if (!pa.ById.TryGetValue(t.Id, out var was)) { rows.Add(new(t.Id, t.Name, "added", null, $"{D(tb.Start)} – {D(tb.Finish)}")); continue; }
            var ta = ra.Times[t.Id];
            var summary = pb.IsSummary(t.Id);
            var da = pa.IsSummary(t.Id) ? ta.Duration : was.Milestone ? 0 : was.Duration;
            var db = summary ? tb.Duration : t.Milestone ? 0 : t.Duration;
            if (ta.Start != tb.Start) rows.Add(new(t.Id, t.Name, "moved", D(ta.Start), D(tb.Start), cal.WorkingDays(ta.Start, tb.Start)));
            if (da != db) rows.Add(new(t.Id, t.Name, "duration", $"{da} d", $"{db} d", db - da));
            else if (ta.Finish != tb.Finish && ta.Start == tb.Start) rows.Add(new(t.Id, t.Name, "finish", D(ta.Finish), D(tb.Finish), cal.WorkingDays(ta.Finish.AddDays(1), tb.Finish.AddDays(1))));
            var ca = pa.ProgressOf(t.Id).Complete;
            var cb = pb.ProgressOf(t.Id).Complete;
            if (Math.Abs(ca - cb) > 0.05 && !summary) rows.Add(new(t.Id, t.Name, "progress", $"{Math.Round(ca, 1)} %", $"{Math.Round(cb, 1)} %"));
        }
        foreach (var t in pa.Rows.Where(t => !pb.ById.ContainsKey(t.Id)))
            rows.Add(new(null, t.Name, "removed", $"{D(ra.Times[t.Id].Start)} – {D(ra.Times[t.Id].Finish)}"));
        if (facts.Count == 0 && rows.Count == 0) return null;
        return new VersionDiffSection("schedule", "Work schedule", facts, rows);
    }
}
