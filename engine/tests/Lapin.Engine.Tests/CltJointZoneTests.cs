using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// Audit 15.11.2026, M3: a CLT joint moved back before one opening's zone (the opening and its edge distance) could land in
/// the zone of the opening before it, when the two zones overlap. The overlapping zones are merged first, as the frames'
/// panels do.
/// </summary>
public class CltJointZoneTests
{
    [Fact]
    public void A_joint_moved_back_before_an_opening_never_lands_in_the_zone_of_the_one_before_it()
    {
        var spec = CltSpec.Wall with { MaxLength = 5 };
        // Zones 1.7–3.3 and 2.9–5.3 (0.3 m each side): the joint at 5 moved back to 2.9 stood inside the first.
        (string, double, double)[] openings = [("A", 2.0, 3.0), ("B", 3.2, 5.0)];
        var (joints, warnings) = Clt.Joints(spec, 0, 12, openings);
        Assert.Empty(warnings);
        foreach (var (_, l, r) in openings) Assert.All(joints, j => Assert.False(j > l - 0.3 + 1e-9 && j < r + 0.3 - 1e-9, $"joint at {j} in {l}–{r}"));
        Assert.Equal(1.7, joints[0], 9);
        // Every panel within the longest.
        var ends = joints.Prepend(0.0).Append(12.0).ToList();
        Assert.All(ends.Zip(ends.Skip(1)), p => Assert.InRange(p.Second - p.First, 1e-6, 5 + 1e-9));
    }

    [Fact]
    public void Two_openings_together_wider_than_a_panel_are_told()
    {
        var spec = CltSpec.Wall with { MaxLength = 5 };
        var (_, warnings) = Clt.Joints(spec, 0, 12, [("A", 1.0, 3.5), ("B", 3.6, 6.0)]);
        Assert.Contains(warnings, w => w.Contains("A + B"));
    }
}
