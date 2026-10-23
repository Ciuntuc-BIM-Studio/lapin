using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>
/// Ported from the old Lapin, same cases and numbers:
/// foundation.e2e.test.ts ("a stem section keeps its core on the axis") and
/// authored-defaults.test.ts ("a layered wall's axis runs through its structural layer").
/// </summary>
public class LayerRulesTests
{
    static MaterialLayer L(double t, bool core = false) => new("m", t, core);

    [Fact]
    public void A_stem_section_keeps_its_core_on_the_axis()
    {
        // Layers from the right face: 0.02 + 0.1 + 0.005, then the 0.3 core.
        var s = Layers.Section([L(0.02), L(0.1), L(0.005), L(0.3, core: true)]);
        Assert.Equal(0.425, s.Thickness, 9);
        Assert.Equal(-0.275, s.LayerOffset, 9);
        Assert.Equal(0.2125 - 0.275, s.BodyOffset, 9);
    }

    [Fact]
    public void A_layered_walls_axis_runs_through_its_structural_layer()
    {
        // Plaster 20 mm, brick 250 mm (the thickest: the core), insulation 100 mm, from the right face.
        var s = Layers.Section([L(0.02), L(0.25), L(0.1)]);
        Assert.Equal(0.37, s.Thickness, 6);
        Assert.Equal(0.04, s.BodyOffset, 6);       // the body shifts 40 mm to the left of the axis
        Assert.Equal(-0.145, s.LayerOffset, 6);    // the layers start 145 mm right of the axis
    }

    [Fact]
    public void A_marked_core_wins_over_the_thickest()
    {
        Assert.Equal(-0.07, Layers.Section([L(0.02), L(0.1, core: true), L(0.25)]).LayerOffset, 9);
        Assert.Equal(1, Layers.CoreIndex([L(0.02), L(0.1, core: true), L(0.25)]));
        Assert.Equal(2, Layers.CoreIndex([L(0.02), L(0.1), L(0.25)]));
    }

    [Fact]
    public void Bands_run_from_the_offset_one_way_or_the_other()
    {
        IReadOnlyList<MaterialLayer> layers = [L(0.02), L(0.25), L(0.1)];
        var positive = Layers.Bands(layers, new LayerUsage("set", false, -0.145));
        Assert.Equal(-0.145, positive[0].From, 9);
        Assert.Equal(0.225, positive[2].To, 9);
        var negative = Layers.Bands(layers, new LayerUsage("set", true, 0.145));
        Assert.Equal(0.125, negative[0].From, 9);   // the first layer now on the left face
        Assert.Equal(0.145, negative[0].To, 9);
        Assert.Equal(-0.225, negative[2].From, 9);
    }
}
