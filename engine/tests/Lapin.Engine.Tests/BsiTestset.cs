namespace Lapin.Engine.Tests;

/// <summary>
/// The buildingSMART IFC-Rail alignment test set (bSI-RailwayRoom/IFC-Rail-Unit-Test-Reference-Code,
/// alignment_testset), local only: <c>LAPIN_BSI_ALIGNMENT_TESTSET</c> (or the core's
/// <c>BUBBLE_BSI_ALIGNMENT_TESTSET</c>) names its folder, else the copy BubbleBIM Standalone keeps
/// beside this repo is used. The evaluator's golden tests against it live with the alignments, in
/// bubble-core; here the set's IFC files feed the export and the import.
/// </summary>
static class BsiTestset
{
    internal static readonly string? Root = Locate();

    static string? Locate()
    {
        foreach (var name in new[] { "LAPIN_BSI_ALIGNMENT_TESTSET", "BUBBLE_BSI_ALIGNMENT_TESTSET" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } env && Directory.Exists(Path.Combine(env, "ToolboxProcessed"))) return env;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "BubbleBIM-Standalone", "data", "references", "infra", "open-source", "IFC-Rail-Unit-Test-Reference-Code", "alignment_testset");
            if (Directory.Exists(Path.Combine(candidate, "ToolboxProcessed"))) return candidate;
        }
        return null;
    }

    /// <summary>A fact that runs only when the reference set is on this machine.</summary>
    internal sealed class BsiFactAttribute : FactAttribute
    {
        public BsiFactAttribute() { if (Root == null) Skip = "The buildingSMART alignment test set is not on this machine (LAPIN_BSI_ALIGNMENT_TESTSET)."; }
    }
}
