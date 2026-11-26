using Lapin.Platform;
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
        // An element labelled with a product of the PBS says which (its own code, else its type's), so the findings read by trade.
        var pbs = Breakdowns.Pbs(c.Model);
        var titles = pbs == null ? null : Classifications.Items(pbs).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.First().Title);
        string Of(ElementFacts f) => pbs != null && Breakdowns.CodeOf(c.Model, f.Element, pbs.Id) is { } code
            ? $"{NameOf(f)} ({pbs.Name} {code}{(titles!.GetValueOrDefault(code) is { Length: > 0 } t ? " " + t : "")})" : NameOf(f);
        foreach (var f in none)
            yield return new CdeFinding(Id, CdeFinding.Warn, $"{Of(f)} has no task in the work schedule.", f.Element.Id);
        foreach (var (f, left) in partial)
            yield return new CdeFinding(Id, CdeFinding.Warn, $"{Of(f)}: {string.Join(", ", left.Select(l => l.Name))} {(left.Count == 1 ? "has" : "have")} no task in the work schedule.", f.Element.Id);
    }
}
