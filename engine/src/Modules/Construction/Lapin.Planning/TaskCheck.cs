using Lapin.Platform.Cde;

namespace Lapin.Planning;

/// <summary>
/// The CDE check "Elements with a task" (docs/plan-4d5d.md, stage 2): once the work schedule takes elements, each element
/// built or demolished by the works that no task takes is a warning, and so is one some of whose layers no task takes (a
/// wall built in a task and never plastered). A click on a finding selects the element. Existing elements that stay need none.
/// </summary>
public sealed class TaskCheck : ICdeRule
{
    public string Id => "planningTasks";
    public string Label => "Elements with a task";
    public bool Blocking => false;

    static string NameOf(ElementFacts f) => f.Element.Name is { Length: > 0 } n ? n : f.Element.Class.Replace("Ifc", "");

    public IEnumerable<CdeFinding> Run(CdeCheckContext c)
    {
        var p = Plan.Read(c.Model);
        if (!p.Rows.Any(t => t.Links)) yield break;
        var (none, partial) = TaskElements.Resolve(c.Query, p).Unlinked();
        foreach (var f in none)
            yield return new CdeFinding(Id, CdeFinding.Warn, $"{NameOf(f)} has no task in the work schedule.", f.Element.Id);
        foreach (var (f, left) in partial)
            yield return new CdeFinding(Id, CdeFinding.Warn, $"{NameOf(f)}: {string.Join(", ", left.Select(l => l.Name))} {(left.Count == 1 ? "has" : "have")} no task in the work schedule.", f.Element.Id);
    }
}
