using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The audit of 15.11.2026, G4: a rebar zone's step is 10 mm at least, as the plain step; a bar's count and its array
/// are bounded (<see cref="Rebars.MaxCount"/>, <see cref="Rebars.MaxArray"/>, <see cref="Rebars.MaxArrayCopies"/>), and
/// what an older model holds beyond them is read clamped, so a value once stored never stalls the engine at a redraw.
/// </summary>
public class RebarLimitsTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    static string Section(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(8, 0) })!["id"]!;

    static JsonObject Run(string view, JsonArray? zones = null)
    {
        var o = new JsonObject { ["viewId"] = view, ["start"] = new JsonArray(0, 0), ["end"] = new JsonArray(1, 0), ["step"] = 0.1, ["params"] = new JsonObject { ["a"] = 0.4, ["b"] = 0.6 } };
        if (zones != null) o["zones"] = zones;
        return o;
    }

    [Fact]
    public void A_zone_s_step_under_10_mm_is_refused_and_a_stored_one_is_ignored()
    {
        Assert.Null(Runs.Zone.From(new JsonObject { ["length"] = 1, ["step"] = 1e-9 }));
        Assert.Null(Runs.Zone.From(new JsonObject { ["length"] = 1, ["step"] = "NaN" }));
        Assert.Equal(new Runs.Zone(1, 0.01), Runs.Zone.From(new JsonObject { ["length"] = 1, ["step"] = 0.01 }));
        var f = F();
        var view = Section(f);
        Assert.Throws<CommandException>(() => f.Run("rebar.addRun", Run(view, new JsonArray(new JsonObject { ["length"] = 0.5, ["step"] = 1e-9 }))));
        var fine = Run(view);
        fine["step"] = 0.001;
        Assert.Throws<CommandException>(() => f.Run("rebar.addRun", fine));
        var id = (string)f.Run("rebar.addRun", Run(view, new JsonArray(new JsonObject { ["length"] = 0.5, ["step"] = 0.05 })))!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("rebar.update", new JsonObject { ["id"] = id, ["zones"] = new JsonArray(new JsonObject { ["length"] = 0.5, ["step"] = 0.0001 }) }));
    }

    [Fact]
    public void A_distribution_lays_a_bounded_number_of_positions_however_fine_its_step()
    {
        Assert.Equal(Runs.MaxPositions, Runs.Positions(1e6, 1e-6).Count);
        Assert.True(Runs.Positions(1e6, 0.1, [new Runs.Zone(1, 1e-9), new Runs.Zone(1, 1e-9)]).Count <= Runs.MaxPositions + 3);   // each zone keeps one step at least, and the end
        // Sane values lay as before.
        Assert.Equal(11, Runs.Positions(1.0, 0.1).Count);
    }

    [Fact]
    public void A_bar_s_count_and_array_are_bounded()
    {
        var f = F();
        var view = Section(f);
        JsonObject Bar() => new() { ["viewId"] = view, ["shape"] = "straight", ["at"] = new JsonArray(0, 0), ["params"] = new JsonObject { ["length"] = 1.0 } };
        var id = (string)f.Run("rebar.add", Bar())!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("rebar.update", new JsonObject { ["id"] = id, ["count"] = 1e12 }));
        Assert.Throws<CommandException>(() => f.Run("rebar.update", new JsonObject { ["id"] = id, ["count"] = "NaN" }));
        Assert.Throws<CommandException>(() => f.Run("rebar.update", new JsonObject { ["id"] = id, ["array"] = new JsonObject { ["nx"] = 1e9, ["dx"] = 0.1 } }));
        Assert.Throws<CommandException>(() => f.Run("rebar.update", new JsonObject { ["id"] = id, ["array"] = new JsonObject { ["nx"] = 500, ["dx"] = 0.1, ["ny"] = 500, ["dy"] = 0.1 } }));
        f.Run("rebar.update", new JsonObject { ["id"] = id, ["count"] = 4, ["array"] = new JsonObject { ["nx"] = 3, ["dx"] = 0.1, ["ny"] = 2, ["dy"] = 0.1 } });
        Assert.Equal(24, Rebars.Pieces(f.Model.Get(id)!));
    }

    [Fact]
    public void Counts_stored_beyond_the_bounds_are_read_clamped_and_the_pieces_never_overflow()
    {
        var e = new Entity("b", Rebars.Class, "bar", new JsonObject
        {
            ["count"] = 1e12,
            ["array"] = new JsonObject { ["nx"] = 1e9, ["ny"] = 1e9, ["dx"] = 0.1, ["dy"] = 0.1 },
            ["across"] = new JsonObject { ["count"] = 1e9, ["mode"] = "cover" },
        });
        Assert.Equal(Rebars.MaxCount, Rebars.Count(e));
        var (nx, _, ny, _) = Rebars.Array(e);
        Assert.True(nx <= Rebars.MaxArray && ny <= Rebars.MaxArray && nx * ny <= Rebars.MaxArrayCopies);
        Assert.Equal(Rebar3D.Across.MaxCount, Rebar3D.Across.Of(e)!.Count);
        Assert.True(Rebars.Pieces(e) > 0);
    }
}
