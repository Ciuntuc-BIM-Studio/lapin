using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.SiteWorks;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// The audit of 15.11.2026: wrong arguments answered with a clear refusal, not an internal error or gigabytes asked —
/// a configurator preview's sample bounded (§3c), a rounded balcony under 5 cm deep (M1), a sketch conversion with
/// nothing selected, a negative sketch reference and a section written with too many digits (M7).
/// </summary>
public class BadArgumentTests
{
    [Fact]
    public void A_preview_s_sample_is_bounded()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new PhasesModule(), new CivilModule(), new SiteWorksModule());
        Assert.Throws<CommandException>(() => f.Session.Query("sitework.scaffoldPreview", new JsonObject { ["length"] = 1e9, ["height"] = 6 }));
        Assert.Throws<CommandException>(() => f.Session.Query("sitework.scaffoldPreview", new JsonObject { ["length"] = -1 }));
        Assert.Throws<CommandException>(() => f.Session.Query("sitework.formworkPreview", new JsonObject { ["kind"] = "upright", ["width"] = 1e8, ["height"] = 3 }));
        Assert.Throws<CommandException>(() => f.Session.Query("sitework.formworkPreview", new JsonObject { ["kind"] = "upright", ["width"] = 3, ["height"] = 1e8 }));
        // A NaN is no number: the default sample.
        Assert.NotNull(f.Session.Query("sitework.scaffoldPreview", new JsonObject { ["length"] = "NaN", ["height"] = 6 }));
        Assert.NotNull(f.Session.Query("sitework.formworkPreview", new JsonObject { ["kind"] = "upright", ["width"] = 3, ["height"] = 3 }));

        var w = new Fixture(new ArchitectureModule(), new StudioModule(), new WoodModule());
        Assert.NotNull(((JsonObject)w.Session.Query("wood.preview", new JsonObject { ["length"] = 1e9 })!)["problem"]);
        Assert.NotNull(((JsonObject)w.Session.Query("wood.preview", new JsonObject { ["height"] = 1e9 })!)["problem"]);
        Assert.NotNull(((JsonObject)w.Session.Query("wood.cltPreview", new JsonObject { ["length"] = 1e9 })!)["problem"]);
        Assert.Null(((JsonObject)w.Session.Query("wood.preview", new JsonObject { ["length"] = 6 })!)["problem"]);
        Assert.Null(((JsonObject)w.Session.Query("wood.cltPreview", new JsonObject { ["length"] = 12 })!)["problem"]);
    }

    [Fact]
    public void A_rounded_balcony_under_5_cm_deep_is_built_with_the_roundest_corner_it_allows()
    {
        var f = new Fixture(new ArchitectureModule());
        var type = f.Type("Brick 25", ("Brick", 0.25, true));
        f.Wall(0, 0, 10, 0, type);
        foreach (var depth in new[] { 0.0, 0.03, 0.05, 0.051 })
        {
            var id = (string)f.Run("architecture.addBalcony", new JsonObject { ["storeyId"] = f.Storey, ["style"] = "rounded", ["x"] = 3, ["y"] = -0.5, ["outline"] = "rounded", ["depth"] = depth })!["id"]!;
            var g = f.Session.Geometry(id) ?? throw new InvalidOperationException($"no geometry at {depth}");
            Assert.NotEmpty(g.Parts);
            Assert.NotEmpty(g.Plan!);
        }
    }

    [Fact]
    public void Converting_no_sketch_is_refused()
    {
        var f = new Fixture(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
        Assert.Throws<CommandException>(() => f.Run("steel.convertSketch", new JsonObject { ["ids"] = new JsonArray() }));
        Assert.Throws<CommandException>(() => f.Run("steel.convertSketch", new JsonObject()));
    }

    [Fact]
    public void A_negative_sketch_reference_is_none()
    {
        var p = new Polyline([new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1)], false);
        Assert.Null(SketchConstraints.PointOf(p, "vertex:-1"));
        Assert.Null(SketchConstraints.SegmentOf(p, "edge:-1"));
        Assert.Equal(new Vec2(1, 0), SketchConstraints.PointOf(p, "vertex:1"));
        Assert.NotNull(SketchConstraints.SegmentOf(p, "edge:1"));
        Assert.Null(SketchConstraints.SegmentOf(p, "edge:2"));
    }

    [Fact]
    public void A_section_written_with_too_many_digits_is_the_fallback()
    {
        var (w, d) = Roof.Section("8x16", (1, 1));
        Assert.Equal(0.08, w, 9); Assert.Equal(0.16, d, 9);
        (w, d) = Roof.Section("45x195 mm", (1, 1));
        Assert.Equal(0.045, w, 9); Assert.Equal(0.195, d, 9);
        Assert.Equal((1.0, 2.0), Roof.Section("99999999999x16", (1, 2)));
        Assert.Equal((1.0, 2.0), Roof.Section("8x" + new string('9', 400) + " mm", (1, 2)));
    }
}
