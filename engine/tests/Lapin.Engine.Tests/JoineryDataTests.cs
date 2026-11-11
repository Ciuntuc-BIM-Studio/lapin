using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Architecture.Schedules;

namespace Lapin.Engine.Tests;

/// <summary>Testers' report #76: the exterior sill at a slope cuts the wall; the frame's position in the wall; the type's data.</summary>
public class JoineryDataTests
{
    static string Rendered(Fixture f) => f.Type("Rendered", ("Render", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));

    static double WallVolume(Fixture f, string wall) => f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));

    static string Add(Fixture f, string wall, double slope, bool flip = false)
    {
        var spec = JoinerySpec.DefaultWindow("S" + slope) with { Details = new JoineryDetails(SillOutside: new JoinerySill(true, 0.05, 0.03, 0.03, slope, null)) };
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var id = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["typeId"] = type })!["id"]!;
        if (flip) f.Run("architecture.flipJoinery", new JsonObject { ["id"] = id, ["axis"] = "y" });
        return id;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_sloped_exterior_sill_takes_the_wedge_over_it_out_of_the_wall(bool flipped)
    {
        double Removed(double slope)
        {
            var f = new Fixture();
            var wall = f.Wall(0, 0, 6, 0, Rendered(f));
            var plain = WallVolume(f, wall);
            Add(f, wall, slope, flipped);
            return plain - WallVolume(f, wall);
        }
        var flat = Removed(0);
        var sloped = Removed(15);
        // The wedge over the sill, from the frame's outer face to the wall's: its length (0.1325 - 0.035) x the sill's width x half the drop at the face.
        Assert.True(sloped > flat + 0.0005, $"flat {flat}, sloped {sloped}");
    }

    // ---- the frame's position in the wall ---------------------------------

    static (Fixture F, string Wall) Rendered_wall()
    {
        var f = new Fixture();
        return (f, f.Wall(0, 0, 6, 0, Rendered(f)));
    }

    static string Typed(Fixture f, string wall, JoineryPosition? position, double along = 3)
    {
        var spec = JoinerySpec.DefaultWindow("P") with { Position = position };
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        return (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = along, ["typeId"] = type })!["id"]!;
    }

    /// <summary>The frame's outer face across the wall, as its distance from the wall's exterior face (the right one: the lowest Y).</summary>
    static double FrameBackFromExterior(Fixture f, string wall, string window)
    {
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        var exterior = bands.Min(b => b.From);
        var frame = f.Session.Geometry(window)!.Parts.First(p => p.Key == "frame");
        return Measure.Bounds(frame.Solid)!.Value.Min.Y - exterior;
    }

    [Fact]
    public void The_frame_sits_a_value_back_from_the_exterior_finish_face_the_core_or_the_centre()
    {
        var (f, wall) = Rendered_wall();
        // 50 mm from the exterior wall finish: the frame's outer face is 0.05 behind the render's face.
        Assert.Equal(0.05, FrameBackFromExterior(f, wall, Typed(f, wall, new JoineryPosition("exterior", 0.05), 1)), 6);
        // From the core: behind the render's 20 mm too.
        Assert.Equal(0.02 + 0.03, FrameBackFromExterior(f, wall, Typed(f, wall, new JoineryPosition("core", 0.03), 3)), 6);
        // From the centre: the frame's centre 0.04 inside the middle plane (the wall is 285 mm: its middle is 142.5 mm in).
        var centre = Typed(f, wall, new JoineryPosition("centre", 0.04), 5);
        Assert.Equal(0.285 / 2 + 0.04 - 0.07 / 2, FrameBackFromExterior(f, wall, centre), 6);
    }

    [Fact]
    public void An_element_can_have_its_own_position_and_gives_it_up()
    {
        var (f, wall) = Rendered_wall();
        var window = Typed(f, wall, new JoineryPosition("exterior", 0.05));
        f.Run("architecture.setJoineryPosition", new JsonObject { ["ids"] = new JsonArray(window), ["from"] = "exterior", ["distance"] = 0.1 });
        Assert.Equal(0.1, FrameBackFromExterior(f, wall, window), 6);
        f.Run("architecture.setJoineryPosition", new JsonObject { ["ids"] = new JsonArray(window), ["clear"] = true });
        Assert.Equal(0.05, FrameBackFromExterior(f, wall, window), 6);
        Assert.Throws<CommandException>(() => f.Run("architecture.setJoineryPosition", new JsonObject { ["ids"] = new JsonArray(window), ["from"] = "nowhere", ["distance"] = 0 }));
    }

    [Fact]
    public void A_flipped_window_keeps_its_position_from_the_exterior()
    {
        var (f, wall) = Rendered_wall();
        var window = Typed(f, wall, new JoineryPosition("exterior", 0.05));
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(window), ["axis"] = "y" });
        // Its exterior is now the wall's left face: the frame's outer face is 0.05 back from the highest Y.
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        var frame = f.Session.Geometry(window)!.Parts.First(p => p.Key == "frame");
        Assert.Equal(0.05, bands.Max(b => b.To) - Measure.Bounds(frame.Solid)!.Value.Max.Y, 6);
    }

    // ---- the data ---------------------------------------------------------

    [Fact]
    public void The_data_is_free_text_on_the_type_with_the_elements_own_over_it_and_the_fire_rating_in_the_standard_set()
    {
        var (f, wall) = Rendered_wall();
        var window = Typed(f, wall, null);
        var type = f.Model.RelatingOf(window, IfcClass.RelDefinesByType)!;
        f.Run("architecture.setJoineryData", new JsonObject
        {
            ["typeId"] = type.Id,
            ["data"] = new JsonObject { ["uValue"] = "0,9 W/m²K", ["gValue"] = "0.52", ["safetyGlass"] = "laminated inside, tempered outside", ["fireRating"] = "EI 45", ["fittings"] = "Roto NX" },
            ["extras"] = new JsonArray(new JsonObject { ["name"] = "Sauna window", ["value"] = "yes" }),
        });
        var ps = ((JsonArray)f.Model.Get(type.Id)!.Attrs["params"]!).OfType<JsonObject>().ToList();
        Assert.Equal(6, ps.Count);
        Assert.Equal("Pset_WindowCommon", (string)ps.Single(p => (string)p["name"]! == "FireRating")["pset"]!);
        Assert.All(ps.Where(p => (string)p["name"]! != "FireRating"), p => Assert.Equal("Lapin_Joinery", (string)p["pset"]!));
        // Tags read them by name; the element's own value wins.
        Assert.Equal("0,9 W/m²K", TagText.Value(f.Model, f.Model.Get(window)!, "U-value"));
        Assert.Equal("EI 45", TagText.Value(f.Model, f.Model.Get(window)!, "Fire rating"));
        f.Run("architecture.setJoineryData", new JsonObject { ["ids"] = new JsonArray(window), ["data"] = new JsonObject { ["fireRating"] = "EI 60" } });
        Assert.Equal("EI 60", TagText.Value(f.Model, f.Model.Get(window)!, "Fire rating"));
        Assert.Equal("EI 45", TagText.Value(f.Model, f.Model.Get(type.Id)!, "Fire rating"));
        // Taking it off gives the type's back; the extras are replaced as a list.
        f.Run("architecture.setJoineryData", new JsonObject { ["ids"] = new JsonArray(window), ["data"] = new JsonObject { ["fireRating"] = "" } });
        Assert.Equal("EI 45", TagText.Value(f.Model, f.Model.Get(window)!, "Fire rating"));
        f.Run("architecture.setJoineryData", new JsonObject { ["typeId"] = type.Id, ["extras"] = new JsonArray(new JsonObject { ["name"] = "Frosted glass", ["value"] = "yes" }) });
        Assert.Equal("yes", TagText.Value(f.Model, f.Model.Get(window)!, "Frosted glass"));
        Assert.Equal("", TagText.Value(f.Model, f.Model.Get(window)!, "Sauna window"));
        Assert.Throws<CommandException>(() => f.Run("architecture.setJoineryData", new JsonObject { ["typeId"] = type.Id, ["extras"] = new JsonArray(new JsonObject { ["name"] = "u-value", ["value"] = "1" }) }));
        // One undo step each.
        f.Session.Undo();
        Assert.Equal("yes", TagText.Value(f.Model, f.Model.Get(window)!, "Sauna window"));
    }

    [Fact]
    public void The_schedule_shows_the_data()
    {
        var (f, wall) = Rendered_wall();
        var window = Typed(f, wall, null);
        var type = f.Model.RelatingOf(window, IfcClass.RelDefinesByType)!;
        f.Run("architecture.setJoineryData", new JsonObject { ["typeId"] = type.Id, ["data"] = new JsonObject { ["uValue"] = "0,9", ["fittings"] = "Roto NX" } });
        var data = Lapin.Architecture.Schedules.Schedules.Joinery(f.Model, "window");
        var entry = data.Entries.Single(e => e.Kind == "window");
        Assert.Equal("0,9", ScheduleLayout.UValue(entry));
        Assert.Contains("param:Fittings", ScheduleLayout.ParamRows(data));
        Assert.DoesNotContain("param:U-value", ScheduleLayout.ParamRows(data));
    }

    [Fact]
    public void Ifc_gets_the_standard_names_when_the_u_value_reads_as_a_number_and_the_rest_in_a_set_of_Lapin()
    {
        var (f, wall) = Rendered_wall();
        var window = Typed(f, wall, null);
        var type = f.Model.RelatingOf(window, IfcClass.RelDefinesByType)!;
        f.Run("architecture.setJoineryData", new JsonObject
        {
            ["typeId"] = type.Id,
            ["data"] = new JsonObject { ["uValue"] = "0,9 W/m²K", ["fireRating"] = "EI 45", ["safetyGlass"] = "tempered both sides" },
        });
        var path = Path.Combine(Path.GetTempPath(), $"joinery-data-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var text = File.ReadAllText(path);
            Assert.Contains("IFCTHERMALTRANSMITTANCEMEASURE(0.9)", text);
            Assert.Contains("IFCNORMALISEDRATIOMEASURE(", text);
            Assert.Contains("'Pset_WindowCommon'", text);
            Assert.Contains("'FireRating'", text);
            Assert.Contains("'Lapin_Joinery'", text);
            Assert.Contains("'Safety glass'", text);
            Assert.Contains("tempered both sides", text);
        }
        finally { File.Delete(path); }
        // A text that is no number writes no ThermalTransmittance, and is kept as text.
        f.Run("architecture.setJoineryData", new JsonObject { ["typeId"] = type.Id, ["data"] = new JsonObject { ["uValue"] = "see test report" } });
        var path2 = Path.Combine(Path.GetTempPath(), $"joinery-data-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path2);
            var text = File.ReadAllText(path2);
            Assert.DoesNotContain("IFCTHERMALTRANSMITTANCEMEASURE", text);
            Assert.Contains("see test report", text);
        }
        finally { File.Delete(path2); }
    }
}
