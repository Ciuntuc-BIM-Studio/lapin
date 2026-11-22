using Lapin.Analysis.Design;
using Lapin.Analysis.Seismic;

namespace Lapin.Analysis.Solver;

/// <summary>
/// What the design checks (S4, docs/plan-fem.md §12) read of an analysis: a member's internal forces along it in every ultimate
/// combination (and the seismic combination's bounds, when given), each with the duration of its shortest action (timber's
/// kmod), and the members' deflections in each case (timber's wfin).
/// </summary>
public sealed partial class AnalysisRun
{
    sealed record DesignSets(List<(string Key, string Name, List<(int Case, double Factor)> Terms)> Sets, List<State> States, List<string> Durations, List<bool> Seismic);
    DesignSets? _uls;

    /// <summary>The duration class of a case's action (EN 1995-1-1 Table 2.2, §12.4): permanent, long, medium, short, instantaneous.</summary>
    public static string DurationOf(LoadCase c, string snow = "short") =>
        c.Permanent ? "permanent"
        : c.Wind ? "instantaneous"
        : c.ActionSource == "EARTHQUAKE_E" || !c.Variable ? "instantaneous"
        : c.Takes(LoadCases.Snow) || c.Category == Categories.Snow || c.ActionSource == "SNOW_S" ? snow
        : c.Category == "E1" ? "long"
        : "medium";

    /// <summary>The snow's duration class for timber's kmod (short by default; medium for a mountain site, the project's choice). Set before the forces are first asked.</summary>
    public string SnowDuration { get; set; } = "short";

    public static readonly string[] Durations = ["permanent", "long", "medium", "short", "instantaneous"];

    DesignSets Uls()
    {
        lock (_gate)
        {
            if (_uls != null) return _uls;
            var sel = Select(new Ask(null, null, "uls"));
            var states = States(sel);
            var durations = new List<string>();
            var seismic = new List<bool>();
            foreach (var (key, _, terms) in sel.Sets)
            {
                var isSeismic = key.StartsWith("seismic/");
                seismic.Add(isSeismic);
                if (isSeismic) { durations.Add("instantaneous"); continue; }
                var shortest = 0;
                foreach (var (ci, k) in Underlying(terms))
                {
                    if (Math.Abs(k) < 1e-12) continue;
                    var c = Report.Cases.First(x => x.Id == Solution!.Cases[ci].Id);
                    shortest = Math.Max(shortest, Array.IndexOf(Durations, DurationOf(c, SnowDuration)));
                }
                durations.Add(Durations[shortest]);
            }
            return _uls = new DesignSets(sel.Sets, states, durations, seismic);
        }
    }

    /// <summary>A member's internal forces in each ultimate combination (and seismic bound): at 21 points and where the extremes are.</summary>
    public List<ForceSet> MemberForces(int member)
    {
        var u = Uls();
        var l = Problem.Members[member].L;
        var o = new List<ForceSet>();
        for (var i = 0; i < u.States.Count; i++)
        {
            var s = u.States[i];
            var xs = Enumerable.Range(0, 21).Select(k => l * k / 20).ToList();
            foreach (var ex in MemberExtremes(member, s.Elements)) { xs.Add(ex.XMax); xs.Add(ex.XMin); }
            var secs = xs.Select(x => Math.Clamp(x, 0, l)).Distinct().Order().Select(x => MemberAt(member, s, x)).ToList();
            o.Add(new ForceSet(u.Sets[i].Key, u.Sets[i].Name, secs, l) { Duration = u.Durations[i], Seismic = u.Seismic[i] });
        }
        return o;
    }

    /// <summary>
    /// A member's deflection (m, square to its axis from the chord between its ends) at 21 points in each case, by the case's
    /// id: what timber's final deflection combines with kdef (§12.4).
    /// </summary>
    public Dictionary<string, double[]> MemberCaseDeflections(int member)
    {
        var l = Problem.Members[member].L;
        var o = new Dictionary<string, double[]>();
        for (var ci = 0; ci < Solution!.Cases.Count; ci++)
        {
            var els = Solution.Cases[ci].Elements;
            var ends = MemberEnds(member, els);
            var w = new double[21];
            for (var i = 0; i <= 20; i++)
            {
                var x = l * i / 20;
                var (_, v, ww) = MemberDeflection(member, els, x);
                var t = x / l;
                var (rv, rw) = (v - (ends[1] + (ends[7] - ends[1]) * t), ww - (ends[2] + (ends[8] - ends[2]) * t));
                w[i] = Math.Sqrt(rv * rv + rw * rw) * (Math.Abs(rw) >= Math.Abs(rv) ? Math.Sign(rw) : Math.Sign(rv));
            }
            o[Solution.Cases[ci].Id] = w;
        }
        return o;
    }

    /// <summary>The ultimate combinations (and the seismic bounds) as factors on the cases, the seismic run when its bounds are in.</summary>
    public (List<(string Key, string Name, List<(int Case, double Factor)> Terms)> Sets, List<string> Durations, List<bool> Seismic, SeismicRun? Run) UlsTerms()
    {
        var u = Uls();
        var run = u.States.FirstOrDefault(s => s.Seismic != null)?.Seismic?.Run;
        return (u.Sets, u.Durations, u.Seismic, run);
    }

    /// <summary>The characteristic SLS combinations as factors on the cases.</summary>
    public List<(string Key, string Name, List<(int Case, double Factor)> Terms)> CharacteristicTerms()
    {
        int CaseIndex(string id) => Solution!.Cases.FindIndex(c => c.Id == id);
        return Combinations.Where(c => c.Kind == UserCombinations.SlsCharacteristic)
            .Select(c => (c.Key, c.Name, c.Factors.Select(f => (CaseIndex(f.Case.Id), f.Value)).Where(x => x.Item1 >= 0).ToList())).ToList();
    }

    /// <summary>The shells' surfaces and nodes, in the order the nodal values below come.</summary>
    public (string Group, int Node)[] ShellKeys() => Keys();

    /// <summary>The shells' recovered values at their nodes in each ultimate combination (and seismic bound).</summary>
    public List<ShellForces[]> UlsShellNodal()
    {
        var sel = Select(new Ask(null, null, "uls"));
        return SetsNodal(sel).Select(x => x.F).ToList();
    }
}
