using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Report #133, a tester's request: custom curtain wall panels saved as reusable types and given to chosen cells, and a cell
/// replaced by a curtain wall door (single or double, swinging or sliding, its size, place, hand and frame profile) with glazing
/// and framing round it when it is smaller than the cell; panels and doors keeping to their cells when the grid changes, and the
/// door a real IfcDoor for tags, schedules and IFC.
/// </summary>
public class CurtainCellTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-cw-cells").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    /// <summary>A type of bays at most 1.5 m wide and one transom at 2.6 m, so the bottom cells are door high.</summary>
    static string DoorHigh(Fixture f, string? along = null) =>
        (string)f.Run("architecture.createCurtainWallType", new JsonObject
        {
            ["name"] = "Shopfront",
            ["spec"] = new JsonObject
            {
                ["u"] = JsonNode.Parse(along ?? """{"mode":"max","spacing":1.5}"""),
                ["v"] = JsonNode.Parse("""{"mode":"offsets","offsets":[2.6]}"""),
            },
        })!["id"]!;

    /// <summary>A 6 m curtain wall, 3 m high, of the type: four bays of 1.5 m, two rows (0–2.6, 2.6–3).</summary>
    static string Curtain(Fixture f, string type) =>
        (string)f.Run("architecture.addCurtainWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0, ["height"] = 3, ["typeId"] = type })!["id"]!;

    static (Vec3 A, Vec3 B) Part(Fixture f, string id, string key) => Measure.Bounds(f.Session.Geometry(id)!.Parts.Single(p => p.Key == key).Solid)!.Value;
    static HashSet<string> Keys(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Select(p => p.Key!).ToHashSet();
    static JsonArray Arr(params string[] keys) => new(keys.Select(k => (JsonNode)k).ToArray());

    static string PanelType(Fixture f, JsonObject panel, string name = "Spandrel") =>
        (string)f.Run("architecture.createCurtainPanelType", new JsonObject { ["name"] = name, ["panel"] = panel })!["id"]!;

    [Fact]
    public void A_panel_type_is_saved_once_and_given_to_chosen_cells_the_rest_keeping_the_type_s()
    {
        var f = new Fixture();
        var id = Curtain(f, DoorHigh(f));
        var plain = Part(f, id, "panel:2:0");
        // A framed spandrel: a 50 × 80 mm frame round a 30 mm opaque infill set 15 mm back, 20 mm out of the axis.
        var spandrel = PanelType(f, new JsonObject { ["kind"] = "framed", ["thickness"] = 0.03, ["offset"] = 0.02, ["frameWidth"] = 0.05, ["frameDepth"] = 0.08, ["inset"] = 0.015, ["infill"] = "solid" });
        var type = f.Model.Get(spandrel)!;
        Assert.Equal(CurtainCells.PanelTypeClass, type.Class);
        Assert.Equal("CURTAIN_PANEL", type.PredefinedType);
        f.Run("architecture.setCurtainPanels", new JsonObject { ["id"] = id, ["keys"] = Arr("panel:1:0", "panel:1:1"), ["typeId"] = spandrel });
        var keys = Keys(f, id);
        Assert.Contains("frame:1:0", keys);
        Assert.Contains("frame:1:1", keys);
        // The frame: 80 deep across the wall, centred 20 mm out (the wall's right is −y for a wall along +x).
        var (fa, fb) = Part(f, id, "frame:1:0");
        Assert.Equal(0.08, fb.Y - fa.Y, 6);
        Assert.Equal(-0.02, (fa.Y + fb.Y) / 2, 6);
        // The infill inside the frame, 30 thick, its outer face 15 mm behind the frame's.
        var (ia, ib) = Part(f, id, "panel:1:0");
        Assert.Equal(0.03, ib.Y - ia.Y, 6);
        Assert.Equal(fa.Y + 0.015, ia.Y, 6);
        Assert.Equal(fa.X + 0.05, ia.X, 6);
        // The cells not chosen are the type's.
        Assert.Equal(plain, Part(f, id, "panel:2:0"));
        Assert.DoesNotContain("frame:2:0", keys);
        // Changing the panel type changes every cell of it; one undo step each.
        f.Run("architecture.updateCurtainPanelType", new JsonObject { ["id"] = spandrel, ["panel"] = new JsonObject { ["kind"] = "solid", ["thickness"] = 0.04 } });
        Assert.DoesNotContain("frame:1:0", Keys(f, id));
        Assert.Equal(0.04, Part(f, id, "panel:1:0").B.Y - Part(f, id, "panel:1:0").A.Y, 6);
        f.Session.Undo();
        Assert.Contains("frame:1:0", Keys(f, id));
        // Every panel by its role, and back to the type's.
        f.Run("architecture.setCurtainPanels", new JsonObject { ["id"] = id, ["keys"] = Arr("panel"), ["typeId"] = spandrel });
        Assert.Equal(8, Keys(f, id).Count(k => k.StartsWith("frame:")));
        f.Run("architecture.setCurtainPanels", new JsonObject { ["id"] = id, ["keys"] = Arr("panel", "panel:1:0", "panel:1:1"), ["clear"] = true });
        Assert.Null(f.Model.Get(id)!.Attrs[CurtainCells.PanelTypesKey]);
        Assert.DoesNotContain(Keys(f, id), k => k.StartsWith("frame:"));
    }

    [Fact]
    public void An_empty_panel_leaves_its_cell_open_and_a_bad_panel_type_is_refused()
    {
        var f = new Fixture();
        var id = Curtain(f, DoorHigh(f));
        var open = PanelType(f, new JsonObject { ["kind"] = "empty" }, "Open");
        f.Run("architecture.setCurtainPanels", new JsonObject { ["id"] = id, ["keys"] = Arr("panel:0:1"), ["typeId"] = open });
        Assert.DoesNotContain("panel:0:1", Keys(f, id));
        var panels = f.Session.Quantities(id).First(x => x.Name == "Lapin_CurtainWallParts").Quantities.Single(x => x.Name == "Panels").Value;
        Assert.Equal(7, panels);
        Assert.ThrowsAny<Exception>(() => PanelType(f, new JsonObject { ["kind"] = "glass", ["thickness"] = 0 }, "Nothing"));
        Assert.ThrowsAny<Exception>(() => PanelType(f, new JsonObject { ["kind"] = "framed", ["thickness"] = 0.05, ["frameDepth"] = 0.04 }, "Thin frame"));
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.setCurtainPanels", new JsonObject { ["id"] = id, ["keys"] = Arr("panel:9:9"), ["typeId"] = open }));
    }

    [Fact]
    public void Panels_keep_their_cells_when_the_grid_changes()
    {
        var f = new Fixture();
        var cwType = DoorHigh(f);
        var id = Curtain(f, cwType);
        var spandrel = PanelType(f, new JsonObject { ["kind"] = "solid", ["thickness"] = 0.03 });
        f.Run("architecture.setCurtainPanels", new JsonObject { ["id"] = id, ["keys"] = Arr("panel:3:0"), ["typeId"] = spandrel });
        // Three bays along: cell 3 is gone, its setting kept; four again, it is back.
        f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = cwType, ["spec"] = new JsonObject { ["u"] = new JsonObject { ["mode"] = "count", ["count"] = 3 }, ["v"] = JsonNode.Parse("""{"mode":"offsets","offsets":[2.6]}""") } });
        Assert.DoesNotContain("panel:3:0", Keys(f, id));
        Assert.Equal(spandrel, (string?)f.Model.Get(id)!.Attrs[CurtainCells.PanelTypesKey]!["panel:3:0"]);
        f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = cwType, ["spec"] = new JsonObject { ["u"] = new JsonObject { ["mode"] = "count", ["count"] = 4 }, ["v"] = JsonNode.Parse("""{"mode":"offsets","offsets":[2.6]}""") } });
        Assert.Equal(0.03, Part(f, id, "panel:3:0").B.Y - Part(f, id, "panel:3:0").A.Y, 6);
    }

    static string Door(Fixture f, string id, string key, JsonObject? more = null)
    {
        var a = new JsonObject { ["id"] = id, ["keys"] = Arr(key) };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addCurtainDoor", a)!["ids"]![0]!;
    }

    [Fact]
    public void A_door_replaces_a_cell_fills_it_down_to_the_floor_and_is_a_real_door()
    {
        var f = new Fixture();
        var id = Curtain(f, DoorHigh(f));
        var door = Door(f, id, "panel:1:0");
        var d = f.Model.Get(door)!;
        Assert.Equal(IfcClass.Door, d.Class);
        Assert.Equal(id, f.Model.RelatingOf(door, IfcClass.RelAggregates)!.Id);
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, door)!.Id);
        Assert.Equal(IfcClass.DoorType, f.Model.RelatingOf(door, IfcClass.RelDefinesByType)!.Class);
        // The cell's panel and the transom under it are gone; the rest stay.
        var keys = Keys(f, id);
        Assert.DoesNotContain("panel:1:0", keys);
        Assert.DoesNotContain("transom:1:0", keys);
        Assert.Contains("panel:1:1", keys);
        Assert.Contains("transom:0:0", keys);
        // The door fills the cell between its mullions, from the floor to the transom.
        var (a, b) = f.Bounds(door);
        Assert.Equal(1.525, a.X, 6);
        Assert.Equal(2.975, b.X, 6);
        Assert.Equal(0, a.Z, 6);
        Assert.Equal(2.575, b.Z, 6);
        var spec = Openings.SpecOf(f.Model, door)!;
        Assert.Equal(1.45, spec.Width, 6);
        Assert.Equal(2.575, spec.Height, 6);
        // A plan symbol with its swing, as any door's.
        Assert.Contains(f.Session.Geometry(door)!.Plan!, p => p.Kind == "hit");
        // Two doors never share a cell.
        Assert.ThrowsAny<Exception>(() => Door(f, id, "panel:1:0"));
        // The top cell of a boundary or the upper row is no place for one either way: the upper row's cell is too low.
        Assert.ThrowsAny<Exception>(() => Door(f, id, "panel:2:1"));
    }

    [Fact]
    public void A_door_smaller_than_its_cell_has_glazing_and_framing_round_it()
    {
        var f = new Fixture();
        var id = Curtain(f, DoorHigh(f));
        var type = (string)f.Run("architecture.createCurtainDoorType", new JsonObject { ["leaves"] = 1, ["operation"] = "swing", ["width"] = 1.0, ["height"] = 2.2 })!["id"]!;
        var door = Door(f, id, "panel:1:0", new JsonObject { ["typeId"] = type, ["fit"] = false });
        var keys = Keys(f, id);
        Assert.DoesNotContain("panel:1:0", keys);
        // A transom at the door's head across the cell, the glass over it; a mullion and a pane each side.
        Assert.Contains("transom:1:0:head", keys);
        Assert.Contains("panel:1:0:head", keys);
        Assert.Contains("mullion:1:0:left", keys);
        Assert.Contains("mullion:1:0:right", keys);
        Assert.Contains("panel:1:0:left", keys);
        Assert.Contains("panel:1:0:right", keys);
        var (a, b) = f.Bounds(door);
        Assert.Equal(1.0, b.X - a.X, 6);
        Assert.Equal(2.25, (a.X + b.X) / 2, 6);
        Assert.Equal(2.2, b.Z - a.Z, 6);
        Assert.Equal(2.2, Part(f, id, "transom:1:0:head").A.Z, 6);
        Assert.Equal(a.X, Part(f, id, "mullion:1:0:left").B.X, 6);
        // The sill transom stays beside the door, cut away under it.
        Assert.Contains("transom:1:0:left", keys);
        Assert.Equal(a.X - 0.05, Part(f, id, "transom:1:0:left").B.X, 6);
        // To the left edge of the cell: no mullion or pane on that side.
        f.Run("architecture.updateCurtainDoor", new JsonObject { ["id"] = door, ["align"] = "left" });
        keys = Keys(f, id);
        Assert.DoesNotContain("mullion:1:0:left", keys);
        Assert.Contains("panel:1:0:right", keys);
        Assert.Equal(1.525, f.Bounds(door).Min.X, 6);
        // An offset that leaves a strip too narrow for glass is refused.
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateCurtainDoor", new JsonObject { ["id"] = door, ["offset"] = 0.03 }));
        // A door wider than its cell is refused.
        var wide = (string)f.Run("architecture.createCurtainDoorType", new JsonObject { ["leaves"] = 2, ["operation"] = "sliding", ["width"] = 2.0, ["height"] = 2.2 })!["id"]!;
        Assert.ThrowsAny<Exception>(() => Door(f, id, "panel:2:0", new JsonObject { ["typeId"] = wide, ["fit"] = false }));
        // The surround glazing takes the cell's own panel type.
        var spandrel = PanelType(f, new JsonObject { ["kind"] = "solid", ["thickness"] = 0.03 });
        f.Run("architecture.setCurtainPanels", new JsonObject { ["id"] = id, ["keys"] = Arr("panel:1:0"), ["typeId"] = spandrel });
        Assert.Equal(0.03, Part(f, id, "panel:1:0:head").B.Y - Part(f, id, "panel:1:0:head").A.Y, 6);
    }

    [Fact]
    public void A_door_keeps_its_cell_its_id_and_its_settings_when_the_grid_changes()
    {
        var f = new Fixture();
        var cwType = DoorHigh(f);
        var id = Curtain(f, cwType);
        var door = Door(f, id, "panel:1:0");
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = Arr(door), ["axis"] = "x" });
        // Three bays of 2 m: the door is the second bay's, 2 m wide less the mullions.
        f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = cwType, ["spec"] = new JsonObject { ["u"] = new JsonObject { ["mode"] = "count", ["count"] = 3 }, ["v"] = JsonNode.Parse("""{"mode":"offsets","offsets":[2.6]}""") } });
        Assert.NotNull(f.Model.Get(door));
        Assert.Equal("panel:1:0", f.Model.Get(door)!.Str("cell"));
        Assert.True(f.Model.Get(door)!.Bool("flipHand"));
        var (a, b) = f.Bounds(door);
        Assert.Equal(2.025, a.X, 6);
        Assert.Equal(3.975, b.X, 6);
        Assert.DoesNotContain("panel:1:0", Keys(f, id));
        // Down to one bay, the cell is gone: the door says so and keeps everything; back to four, it is there again.
        f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = cwType, ["spec"] = new JsonObject { ["u"] = new JsonObject { ["mode"] = "count", ["count"] = 1 }, ["v"] = JsonNode.Parse("""{"mode":"offsets","offsets":[2.6]}""") } });
        Assert.NotEmpty(f.Session.Geometry(door)!.Diagnostics!);
        Assert.Empty(f.Session.Geometry(door)!.Parts);
        f.Run("architecture.updateCurtainWallType", new JsonObject { ["id"] = cwType, ["spec"] = new JsonObject { ["u"] = new JsonObject { ["mode"] = "count", ["count"] = 4 }, ["v"] = JsonNode.Parse("""{"mode":"offsets","offsets":[2.6]}""") } });
        Assert.Equal(1.525, f.Bounds(door).Min.X, 6);
        Assert.True(f.Model.Get(door)!.Bool("flipHand"));
    }

    [Fact]
    public void A_door_moves_to_another_cell_goes_with_its_wall_and_goes_when_its_wall_is_deleted()
    {
        var f = new Fixture();
        var id = Curtain(f, DoorHigh(f));
        var door = Door(f, id, "panel:1:0");
        // Moved along by a bay: into the next cell.
        f.Run("element.transform", new JsonObject { ["ids"] = Arr(door), ["op"] = "move", ["by"] = new JsonArray(1.5, 0) });
        Assert.Equal("panel:2:0", f.Model.Get(door)!.Str("cell"));
        Assert.Contains("panel:1:0", Keys(f, id));
        Assert.DoesNotContain("panel:2:0", Keys(f, id));
        // Copied: a second door in the cell after.
        f.Run("element.transform", new JsonObject { ["ids"] = Arr(door), ["op"] = "copy", ["by"] = new JsonArray(1.5, 0) });
        Assert.Equal(2, f.Model.RelatedOf(id, IfcClass.RelAggregates).Count(e => e.Class == IfcClass.Door));
        // The wall moved: its doors follow it.
        var before = f.Bounds(door).Min.Y;
        f.Run("element.transform", new JsonObject { ["ids"] = Arr(id), ["op"] = "move", ["by"] = new JsonArray(0, 2) });
        Assert.Equal(before + 2, f.Bounds(door).Min.Y, 6);
        // The wall copied: its doors with it.
        var copy = (string)f.Run("element.transform", new JsonObject { ["ids"] = Arr(id), ["op"] = "copy", ["by"] = new JsonArray(0, 5) })!["ids"]![0]!;
        Assert.Equal(2, f.Model.RelatedOf(copy, IfcClass.RelAggregates).Count(e => e.Class == IfcClass.Door));
        // Deleted: its doors with it, and undo brings them back.
        f.Run("element.delete", new JsonObject { ["ids"] = Arr(id) });
        Assert.Null(f.Model.Get(door));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(door));
        // The door deleted alone: the cell is glazed again.
        f.Run("element.delete", new JsonObject { ["ids"] = Arr(door) });
        Assert.Contains("panel:2:0", Keys(f, id));
    }

    [Fact]
    public void A_door_type_changed_refits_and_a_type_that_no_longer_fits_is_refused()
    {
        var f = new Fixture();
        var id = Curtain(f, DoorHigh(f));
        var type = (string)f.Run("architecture.createCurtainDoorType", new JsonObject { ["leaves"] = 2, ["operation"] = "sliding", ["width"] = 1.2, ["height"] = 2.2 })!["id"]!;
        var spec = JoinerySpec.From(f.Model.Get(type)!.Attrs["spec"])!;
        Assert.Equal(["door-sliding-left", "door-sliding-right"], spec.Panels.Select(p => p.Operation));
        Assert.Equal("DOUBLE_DOOR_SLIDING", Joinery.DoorOperation(spec).Type);
        var door = Door(f, id, "panel:1:0", new JsonObject { ["typeId"] = type, ["fit"] = false });
        Assert.Equal(1.2, f.Bounds(door).Max.X - f.Bounds(door).Min.X, 6);
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = type, ["spec"] = (spec with { Width = 1.6 }).ToJson() }));
        f.Run("architecture.updateJoineryType", new JsonObject { ["id"] = type, ["spec"] = (spec with { Width = 1.1 }).ToJson() });
        Assert.Equal(1.1, f.Bounds(door).Max.X - f.Bounds(door).Min.X, 6);
        // A new door type with its door, its frame from a library profile, in one step.
        var frame = new JsonObject { ["name"] = "Alu 60x80", ["width"] = 0.06, ["depth"] = 0.08, ["kind"] = "rect" };
        var types = f.Model.OfClass(IfcClass.DoorType).Count();
        var other = Door(f, id, "panel:2:0", new JsonObject { ["door"] = new JsonObject { ["leaves"] = 1, ["operation"] = "swing", ["width"] = 0.9, ["height"] = 2.1, ["frameSection"] = frame } });
        Assert.Equal(types + 1, f.Model.OfClass(IfcClass.DoorType).Count());
        var made = JoinerySpec.From(f.Model.RelatingOf(other, IfcClass.RelDefinesByType)!.Attrs["spec"])!;
        Assert.Equal(0.06, made.Frame.Width, 6);
        Assert.Equal(0.9, f.Bounds(other).Max.X - f.Bounds(other).Min.X, 6);
        f.Session.Undo();
        Assert.Null(f.Model.Get(other));
        Assert.Equal(types, f.Model.OfClass(IfcClass.DoorType).Count());
    }

    [Fact]
    public void A_curtain_wall_door_is_scheduled_at_its_size_and_exported_as_an_ifc_door_part_of_its_wall()
    {
        var f = new Fixture();
        var id = Curtain(f, DoorHigh(f));
        var door = Door(f, id, "panel:1:0");
        var entry = Lapin.Architecture.Schedules.Schedules.Joinery(f.Model, "door").Entries.Single(e => e.Occurrences.Any(o => o.Id == door));
        Assert.Equal(1.45, entry.Width, 6);
        Assert.Equal(2.575, entry.Height, 6);
        Assert.Equal(f.Model.RelatingOf(door, IfcClass.RelDefinesByType)!.Id, entry.TypeKey);
        var path = Path.Combine(_dir, "cw-door.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Read(path);
        var ifcDoor = Assert.Single(file.OfType("IFCDOOR"));
        var cw = Assert.Single(file.OfType("IFCCURTAINWALL"));
        static List<object?> Items(object? o) => o as List<object?> ?? [];
        var wholes = file.OfType("IFCRELAGGREGATES").Where(r => Items(r[5]).Contains(new StepRef(ifcDoor.Id))).ToList();
        Assert.Equal(new StepRef(cw.Id), Assert.Single(wholes)[4]);
        Assert.DoesNotContain(file.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE"), r => Items(r[4]).Contains(new StepRef(ifcDoor.Id)));
        // Its overall size is the cell's.
        Assert.Equal(2.575, Convert.ToDouble(ifcDoor[8]), 6);
        Assert.Equal(1.45, Convert.ToDouble(ifcDoor[9]), 6);
        Assert.Single(file.OfType("IFCDOORTYPE"));
    }
}
