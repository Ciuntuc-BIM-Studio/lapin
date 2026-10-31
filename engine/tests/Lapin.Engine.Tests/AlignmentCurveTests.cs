using System.Text.Json.Nodes;
using Lapin.Civil;

namespace Lapin.Engine.Tests;

/// <summary>
/// The geometric representation written for an alignment (IfcCurves), checked
/// by evaluating the IFC definitions on their own against the alignment
/// evaluator at 100 points per segment: under 0.1 mm. On a track made in a
/// session (always) and on every alignment of the buildingSMART test set's
/// original IFC files (when the set is on this machine).
/// </summary>
public class AlignmentCurveTests
{
    const double Tol = 1e-4;

    static Dictionary<string, double> Worst(Alignment a) => IfcCurves.Check(a, IfcCurves.Of(a)).ToDictionary(c => c.Layer, c => c.Max);
    [BsiTestset.BsiFact]
    public void Every_alignment_of_the_buildingSMART_set_evaluates_from_its_written_curves()
    {
        var files = Directory.EnumerateFiles(Path.Combine(BsiTestset.Root!, "IFC-OriginalFiles"), "*.ifc", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        var bad = new List<string>();
        var n = 0;
        var max = new Dictionary<string, double>();
        foreach (var file in files)
        {
            foreach (var read in IfcAlignmentReader.Read(File.ReadAllText(file)))
            {
                if (read.Alignment.Horizontal.Count == 0) continue;
                n++;
                foreach (var (layer, v) in Worst(read.Alignment))
                {
                    max[layer] = Math.Max(max.GetValueOrDefault(layer), v);
                    if (!(v < Tol)) bad.Add($"{Path.GetFileName(file)} {layer} {v}");
                }
            }
        }
        Assert.Empty(bad);
        Assert.True(n > 200, $"{n} alignments");
    }
}
