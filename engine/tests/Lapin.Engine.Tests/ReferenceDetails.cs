using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The reference details of the material graphics (docs/plan-material-graphics.md, stage 0, decision 670): small projects
/// built from commands, as a user draws them, each with the views that show what its materials draw. Each view is made
/// at 1:20, 1:50 and 1:100 (<see cref="Scales"/>). They are the fixtures of <see cref="MaterialGraphicsReferenceTests"/>
/// and, saved as .lapin projects with <c>LAPIN_REFERENCE_PDF=1</c>, what the user guide's scenes open (stage 8). Their
/// builders are the engine's (<see cref="MaterialReferenceDetails"/>, decision 673: the standards' sample sheets draw them too).
/// </summary>
static class ReferenceDetails
{
    public static readonly int[] Scales = MaterialReferenceDetails.Scales;

    /// <summary>A detail: its name, its project, and its views (a name and view.create's arguments, without the scale).</summary>
    public sealed record Detail(string Name, string Title, Fixture F, List<(string Name, JsonObject Args)> Views);

    public static IEnumerable<Func<Detail>> All => MaterialReferenceDetails.All.Select(d => (Func<Detail>)(() => Make(d.Build)));

    /// <summary>A detail built in a project of its own, with <paramref name="before"/> run first (a standard applied).</summary>
    public static Detail Make(Func<Session, MaterialReferenceDetails.Detail> build, Action<Fixture>? before = null)
    {
        var f = New();
        before?.Invoke(f);
        var d = build(f.Session);
        return new Detail(d.Name, d.Title, f, d.Views);
    }

    static Fixture New()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        // Every layer drawn in plans, finishes too (a new project's plans leave them out, decision 500).
        f.EveryLayerInPlans();
        return f;
    }
}
