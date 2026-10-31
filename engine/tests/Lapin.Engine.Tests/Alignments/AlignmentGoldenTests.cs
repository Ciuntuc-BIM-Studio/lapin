using Lapin.Alignments;

namespace Lapin.Engine.Tests;

/// <summary>
/// Golden test against the buildingSMART IFC-Rail alignment test set
/// (bSI-RailwayRoom/IFC-Rail-Unit-Test-Reference-Code, alignment_testset):
/// every point list in ToolboxProcessed is compared with the evaluator run on
/// the semantic segments read from the matching IFC file. Tolerance 0.1 mm.
///
/// The set is local only (no licence on the repo, so no fixture is committed):
/// <c>BUBBLE_BSI_ALIGNMENT_TESTSET</c> (or Lapin's <c>LAPIN_BSI_ALIGNMENT_TESTSET</c>) names its folder, else the copy BubbleBIM
/// Standalone keeps beside this repo is used; without either the tests are skipped.
///
/// What the lists hold (checked against the IFC parameters):
///  - horizontal lists: [Curve Length] [Y-Coordinate] [X-Coordinate] — the "Y"
///    column is IFC x (it grows with s along StartDirection 0) and the "X" column
///    is IFC y. For CUBIC "Curve Length" is the parent cubic's x parameter;
///  - -2CS lists (cant): [U] [V], U the distance along, V the cant of the raised
///    rail — one rail stays at 0, and V = left + right;
///  - VerticalAlignment lists: [index] [distance along] [y] [z], the last row
///    repeated — except CLOTHOID, whose lists hold only the horizontal LINE's two end points.
/// </summary>
public class AlignmentGoldenTests
{
    const double Tol = 1e-4;

    internal static readonly string? Root = Locate();
    static string Lists => Path.Combine(Root!, "ToolboxProcessed");

    static string? Locate()
    {
        foreach (var name in new[] { "BUBBLE_BSI_ALIGNMENT_TESTSET", "LAPIN_BSI_ALIGNMENT_TESTSET" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } env && Directory.Exists(Path.Combine(env, "ToolboxProcessed"))) return env;
        // Sibling repo: …/GitHub/BubbleBIM-Standalone/data/references/infra/open-source/IFC-Rail-Unit-Test-Reference-Code/alignment_testset
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
        public BsiFactAttribute() { if (Root == null) Skip = "The buildingSMART alignment test set is not on this machine (BUBBLE_BSI_ALIGNMENT_TESTSET)."; }
    }

    static List<string> Walk() => Directory.EnumerateFiles(Lists, "*.txt", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();

    static List<double[]> Rows(string file) => File.ReadAllLines(file).Skip(2).Select(l => l.Trim()).Where(l => l.Length > 0)
        .Select(l => l.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries).Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToList();

    /// <summary>The IFC file with the semantic parameters of a point list.</summary>
    static string? IfcFor(string list)
    {
        var rel = Path.GetRelativePath(Lists, list).Replace('\\', '/');
        var group = rel.Split('/')[0];
        var baseName = Path.GetFileNameWithoutExtension(list);
        if (baseName.EndsWith("-H")) baseName = baseName[..^2]; else if (baseName.EndsWith("-2CS")) baseName = baseName[..^4];
        string[] candidates = group switch
        {
            "Extra" => [Path.Combine(Root!, "IFC-WithGeneratedGeometry", $"GENERATED__CantAlignment_Extra_{baseName.Replace("CantAlignment_", "")}-1.ifc")],
            "CAVB" => [Path.Combine(Root!, "IFC-OriginalFiles", "CAVB", baseName + ".ifc")],
            _ => [Path.Combine(Root!, "IFC-OriginalFiles", group, baseName + ".ifc")],
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    static Alignment ReadAlignment(string ifc) => IfcAlignmentReader.Read(File.ReadAllText(ifc))[0].Alignment;

    static string Name(string list) => Path.GetRelativePath(Lists, list);

    [BsiFact]
    public void Finds_the_380_lists_and_an_IFC_file_for_each()
    {
        var lists = Walk();
        Assert.Equal(380, lists.Count);
        Assert.Empty(lists.Where(l => IfcFor(l) == null).Select(Name));
    }

    [BsiFact]
    public void Horizontal_lists_fall_on_the_evaluator_within_a_tenth_of_a_millimetre()
    {
        var failed = new List<string>();
        var worst = (Err: 0.0, Name: "");
        var cases = 0;
        foreach (var list in Walk().Where(l => !l.EndsWith("-2CS.txt") && !l.Contains("/VerticalAlignment/")))
        {
            var a = ReadAlignment(IfcFor(list)!);
            var prep = a.Prepared();
            double err = 0;
            foreach (var row in Rows(list))
            {
                var p = prep.Horizontal.At(row[0]);
                err = Math.Max(err, Math.Sqrt((p.X - row[1]) * (p.X - row[1]) + (p.Y - row[2]) * (p.Y - row[2])));
            }
            cases++;
            if (err > worst.Err) worst = (err, Name(list));
            if (err > Tol) failed.Add($"{Name(list)}: {err:0.###e+00} m");
        }
        Assert.True(cases > 200, $"only {cases} horizontal cases");
        Assert.Empty(failed);
        Assert.True(worst.Err < Tol, $"worst {worst.Name} {worst.Err}");
    }

    [BsiFact]
    public void Vertical_lists_fall_on_the_evaluator()
    {
        var failed = new List<string>();
        foreach (var list in Walk().Where(l => l.Contains("/VerticalAlignment/")))
        {
            var a = ReadAlignment(IfcFor(list)!);
            var prep = a.Prepared();
            var pts = Rows(list);
            double err = 0;
            if (pts[0].Length == 4)
            {
                // [index] [distance along] [y = 0] [z], the last row repeated.
                foreach (var row in pts)
                {
                    var p = prep.At(row[1]);
                    err = Math.Max(err, Math.Sqrt((p.Y - row[2]) * (p.Y - row[2]) + (p.Z!.Value - row[3]) * (p.Z!.Value - row[3])));
                }
            }
            else
            {
                // CLOTHOID: no profile in the list, only the horizontal LINE's two end points (listed twice, Curve Length column 0).
                PlanPoint[] ends = [prep.Horizontal.At(0), prep.Horizontal.At(prep.Length)];
                for (var i = 0; i < pts.Count; i++)
                {
                    var e = ends[i % 2];
                    err = Math.Max(err, Math.Sqrt((e.X - pts[i][1]) * (e.X - pts[i][1]) + (e.Y - pts[i][2]) * (e.Y - pts[i][2])));
                }
            }
            if (err > Tol) failed.Add($"{Name(list)}: {err:0.###e+00} m");
        }
        Assert.Empty(failed);
    }

    [BsiFact]
    public void Cant_lists_fall_on_the_evaluator()
    {
        var failed = new List<string>();
        foreach (var list in Walk().Where(l => l.EndsWith("-2CS.txt")))
        {
            var a = ReadAlignment(IfcFor(list)!);
            var layer = a.Cant!;
            double err = 0;
            foreach (var row in Rows(list))
            {
                var c = Cant.Evaluate(layer, row[0])!.Value;
                err = Math.Max(err, Math.Abs(c.CantLeft + c.CantRight - row[1]));
            }
            // Known inconsistent input: the CONSTANTCANT files give different start and end cants (0 → 0.16);
            // the list joins the two end values. D = const per the spec, so the start value holds (continuity flags the jump).
            var seg = layer.Segments[0];
            var known = seg.Type == CantType.ConstantCant && (seg.EndLeft != seg.StartCantLeft || seg.EndRight != seg.StartCantRight);
            if (err > Tol && !known) failed.Add($"{Name(list)}: {err:0.###e+00} m");
        }
        Assert.Empty(failed);
    }
}
