using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Geometry;
using Lapin.Host;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topic 140 (decision 664): a door's or window's width and height are the element's own, over its type's. One type
/// (its construction, profiles, materials, data) placed at many sizes, each element linked to it; the type's size the default
/// of new ones and of every element without its own (an older project unchanged).
/// </summary>
public sealed class InstanceJoinerySizeTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-joinery-size").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static (Fixture F, string Wall, string Type) Setup()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 12, 0, height: 3);
        // The Finnish MSE window of the topic: one type, 1200 × 1200 by default.
        var spec = JoinerySpec.DefaultWindow("MSE") with { Width = 1.2, Height = 1.2 };
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        return (f, wall, type);
    }

    static string Window(Fixture f, string wall, string type, double along, double? width = null, double? height = null)
    {
        var a = new JsonObject { ["wallId"] = wall, ["along"] = along, ["typeId"] = type };
        if (width != null) a["width"] = width;
        if (height != null) a["height"] = height;
        return (string)f.Run("architecture.addWindow", a)!["id"]!;
    }

    static double WallVolume(Fixture f, string wall) => f.Session.Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));

    static (double W, double H) Extent(Fixture f, string id)
    {
        // The frame's: the type's construction at the element's size.
        var b = Measure.Bounds(f.Session.Geometry(id)!.Parts.First(p => p.Key == "frame").Solid)!.Value;
        return (b.Max.X - b.Min.X, b.Max.Z - b.Min.Z);
    }

    [Fact]
    public void One_type_places_windows_at_three_sizes_each_linked_to_it()
    {
        var (f, wall, type) = Setup();
        var a = Window(f, wall, type, 1.5);
        var b = Window(f, wall, type, 5, 0.9, 1.5);
        var c = Window(f, wall, type, 9, 1.8, 1.2);
        foreach (var id in new[] { a, b, c }) Assert.Equal(type, f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Id);
        Assert.Single(f.Model.OfClass(IfcClass.WindowType));
        // The type's own size is not kept on the element: it follows the type there.
        Assert.Null(f.Model.Get(a)!.Attrs[Openings.OwnWidthKey]);
        Assert.Null(f.Model.Get(c)!.Attrs[Openings.OwnHeightKey]);
        Assert.Equal(1.8, f.Model.Get(c)!.Num(Openings.OwnWidthKey), 9);
        // Every consumer reads the one resolver: the spec, the opening, the body, the quantities.
        foreach (var (id, w, h) in new[] { (a, 1.2, 1.2), (b, 0.9, 1.5), (c, 1.8, 1.2) })
        {
            var spec = Openings.SpecOf(f.Model, id)!;
            Assert.Equal((w, h), (spec.Width, spec.Height));
            var placed = Openings.Place(f.Model, Openings.OpeningOf(f.Model, id)!);
            Assert.Equal((w, h), (placed.Width, placed.Height));
            var (ew, eh) = Extent(f, id);
            Assert.Equal(w, ew, 3);
            Assert.True(eh >= h - 1e-6 && eh < h + 0.1, $"{id}: height {eh} for {h}");
            var q = f.Session.Quantities(id).Single(s => s.Name == "Qto_WindowBaseQuantities").Quantities;
            Assert.Equal(w, q.Single(x => x.Name == "Width").Value, 9);
            Assert.Equal(h, q.Single(x => x.Name == "Height").Value, 9);
            Assert.Equal(w * h, q.Single(x => x.Name == "Area").Value, 9);
        }
    }

    [Fact]
    public void The_schedule_lists_the_type_once_per_size()
    {
        var (f, wall, type) = Setup();
        Window(f, wall, type, 1.5);
        Window(f, wall, type, 5, 0.9, 1.5);
        Window(f, wall, type, 9, 0.9, 1.5);
        var entries = Schedules.Joinery(f.Model, "window").Entries;
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Width == 1.2 && e.Height == 1.2 && e.Total == 1);
        Assert.Contains(entries, e => Math.Abs(e.Width - 0.9) < 1e-9 && Math.Abs(e.Height - 1.5) < 1e-9 && e.Total == 2 && e.Name == "MSE 900×1500");
    }

    [Fact]
    public void Its_size_set_cuts_the_wall_anew_in_one_undo_step_and_keeps_its_GlobalIds()
    {
        var (f, wall, type) = Setup();
        var plain = WallVolume(f, wall);
        var w = Window(f, wall, type, 5);
        var opening = Openings.OpeningOf(f.Model, w)!.Id;
        var at1200 = WallVolume(f, wall);
        f.Run("architecture.setJoinerySize", new JsonObject { ["id"] = w, ["width"] = 1.8 });
        Assert.Equal((1.8, 1.2), (Openings.SpecOf(f.Model, w)!.Width, Openings.SpecOf(f.Model, w)!.Height));
        var at1800 = WallVolume(f, wall);
        // 0.6 m more of the hole, 1.2 m high, through the wall: more taken out.
        Assert.True(plain - at1800 > (plain - at1200) + 0.1, $"{plain} {at1200} {at1800}");
        Assert.Equal(1.8, Extent(f, w).W, 3);
        Assert.Equal(opening, Openings.OpeningOf(f.Model, w)!.Id);
        f.Session.Undo();
        Assert.Null(f.Model.Get(w)!.Attrs[Openings.OwnWidthKey]);
        Assert.Equal(at1200, WallVolume(f, wall), 6);
    }

    [Fact]
    public void Reset_brings_it_back_to_its_types_size()
    {
        var (f, wall, type) = Setup();
        var w = Window(f, wall, type, 5, 0.9, 1.5);
        f.Run("architecture.setJoinerySize", new JsonObject { ["ids"] = new JsonArray(w), ["reset"] = true });
        var e = f.Model.Get(w)!;
        Assert.False(Openings.HasOwnSize(e));
        Assert.Equal((1.2, 1.2), (Openings.SpecOf(f.Model, w)!.Width, Openings.SpecOf(f.Model, w)!.Height));
        // A size equal to the type's is the type's: not kept.
        f.Run("architecture.setJoinerySize", new JsonObject { ["id"] = w, ["width"] = 1.2 });
        Assert.False(Openings.HasOwnSize(f.Model.Get(w)!));
        Assert.Throws<CommandException>(() => f.Run("architecture.setJoinerySize", new JsonObject { ["id"] = w }));
    }

    [Fact]
    public void A_size_that_does_not_fit_its_wall_or_its_construction_is_refused()
    {
        var (f, wall, type) = Setup();
        var w = Window(f, wall, type, 5);
        Window(f, wall, type, 7);
        // Over its neighbour (along 7, 1200 wide: from 6.4).
        Assert.Throws<CommandException>(() => f.Run("architecture.setJoinerySize", new JsonObject { ["id"] = w, ["width"] = 3.0 }));
        // Above the wall's top.
        Assert.Throws<CommandException>(() => f.Run("architecture.setJoinerySize", new JsonObject { ["id"] = w, ["height"] = 2.5 }));
        // Too small for its sashes.
        Assert.Throws<CommandException>(() => f.Run("architecture.setJoinerySize", new JsonObject { ["id"] = w, ["width"] = 0.2 }));
        Assert.Throws<CommandException>(() => Window(f, wall, type, 10, 0.2));
        Assert.False(Openings.HasOwnSize(f.Model.Get(w)!));
    }

    [Fact]
    public void A_changed_type_refits_the_elements_of_its_size_and_keeps_the_others_own()
    {
        var (f, wall, type) = Setup();
        var plain = Window(f, wall, type, 2);
        var own = Window(f, wall, type, 6, 0.9, 1.5);
        var next = JoinerySpec.From(f.Model.Get(type)!.Attrs["spec"])! with { Width = 1.0, Height = 1.0, Mark = "M" };
        f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = type, ["spec"] = next.ToJson() });
        Assert.Equal((1.0, 1.0), (Openings.SpecOf(f.Model, plain)!.Width, Openings.SpecOf(f.Model, plain)!.Height));
        Assert.Equal((0.9, 1.5), (Openings.SpecOf(f.Model, own)!.Width, Openings.SpecOf(f.Model, own)!.Height));
        Assert.Equal("M", Openings.SpecOf(f.Model, own)!.Mark);
        // Another type: its own size goes with it.
        var other = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = JoinerySpec.DefaultWindow("Other").ToJson() })!["id"]!;
        f.Run("architecture.setJoineryType", new JsonObject { ["ids"] = new JsonArray(own), ["typeId"] = other });
        Assert.Equal((0.9, 1.5), (Openings.SpecOf(f.Model, own)!.Width, Openings.SpecOf(f.Model, own)!.Height));
    }

    [Fact]
    public void The_openings_grip_resizes_its_door_or_window()
    {
        var (f, wall, type) = Setup();
        var w = Window(f, wall, type, 5);
        var opening = Openings.OpeningOf(f.Model, w)!.Id;
        f.Run("architecture.updateWallOpening", new JsonObject { ["id"] = opening, ["width"] = 1.5 });
        Assert.Equal(1.5, f.Model.Get(w)!.Num(Openings.OwnWidthKey), 9);
        Assert.Equal(1.5, Openings.Place(f.Model, f.Model.Get(opening)!).Width, 9);
        // The opening keeps no size of its own: it is its window's.
        Assert.Null(f.Model.Get(opening)!.Attrs["width"]);
    }

    [Fact]
    public void A_door_takes_its_own_size_too()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 8, 0, height: 3);
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = JoinerySpec.DefaultDoor("D1").ToJson() })!["id"]!;
        var d = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 2, ["typeId"] = type, ["width"] = 1.0 })!["id"]!;
        var d2 = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 5, ["typeId"] = type })!["id"]!;
        f.Run("architecture.setJoinerySize", new JsonObject { ["id"] = d2, ["height"] = 2.3 });
        Assert.Equal((1.0, 2.1), (Openings.SpecOf(f.Model, d)!.Width, Openings.SpecOf(f.Model, d)!.Height));
        Assert.Equal((0.9, 2.3), (Openings.SpecOf(f.Model, d2)!.Width, Openings.SpecOf(f.Model, d2)!.Height));
        Assert.Equal(1.0, Extent(f, d).W, 2);
    }

    [Fact]
    public void An_element_without_its_own_size_is_its_types_as_before()
    {
        var (f, wall, type) = Setup();
        var w = Window(f, wall, type, 5);
        var typeSpec = JoinerySpec.From(f.Model.Get(type)!.Attrs["spec"])!;
        Assert.Equal(typeSpec.ToJson().ToJsonString(), Openings.SpecOf(f.Model, w)!.ToJson().ToJsonString());
        Assert.False(Openings.HasOwnSize(f.Model.Get(w)!));
    }

    [Fact]
    public void Saved_and_opened_again_each_keeps_its_size()
    {
        var (f, wall, type) = Setup();
        var w = Window(f, wall, type, 5, 0.9, 1.5);
        var path = Path.Combine(_dir, "sizes.lapin");
        PackageFiles.Save(f.Session, path);
        var g = new Fixture();
        PackageFiles.Open(g.Session, path);
        Assert.Equal((0.9, 1.5), (Openings.SpecOf(g.Model, w)!.Width, Openings.SpecOf(g.Model, w)!.Height));
    }

    [Fact]
    public void IFC_writes_each_occurrences_own_overall_size_and_reads_it_back()
    {
        var (f, wall, type) = Setup();
        Window(f, wall, type, 2);
        Window(f, wall, type, 6, 0.9, 1.5);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(2, ifc.Split("IFCWINDOW(").Length - 1);
        var path = Path.Combine(_dir, "sizes.ifc");
        File.WriteAllText(path, ifc);
        var g = new Fixture();
        IfcImport.Run(g.Session, path, null, out _);
        var sizes = g.Model.OfClass(IfcClass.Window).Select(e => Openings.SpecOf(g.Model, e.Id)!).Select(s => (Math.Round(s.Width, 4), Math.Round(s.Height, 4))).OrderBy(x => x).ToList();
        Assert.Equal([(0.9, 1.5), (1.2, 1.2)], sizes);
    }

    [Fact]
    public void Another_type_is_checked_where_the_window_stands_its_hole_s_offset_counted()
    {
        // Audit 22.11.2026 §3.6: the fit was checked at the hole's sill, before the type's change moved it up by the offset.
        var (f, wall, type) = Setup();
        var w = Window(f, wall, type, 3);
        var opening = Openings.OpeningOf(f.Model, w)!;
        // An imported file's hole: down to 0.5 m, the window 1.0 m over its bottom (so standing at 1.5 m as before).
        f.Session.Edit("hole", e =>
        {
            var a = opening.CloneAttrs();
            a["sill"] = 0.5; a[Openings.FillOffsetKey] = 1.0; a[Openings.HoleHeightKey] = 2.2;
            e.Replace(opening.With(attrs: a));
        });
        Assert.Equal(1.5, Openings.Place(f.Model, f.Model.Get(opening.Id)!).FillSill, 9);
        // 1.6 m high: from the hole's sill (0.5) it would fit the 3 m wall, from where it stands (1.5) it does not.
        var tall = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultWindow("Tall") with { Width = 1.2, Height = 1.6 }).ToJson() })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("architecture.setJoineryType", new JsonObject { ["ids"] = new JsonArray(w), ["typeId"] = tall }));
    }
}
