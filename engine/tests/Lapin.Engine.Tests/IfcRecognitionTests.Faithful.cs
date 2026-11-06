using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// An import brings the file as it is (docs/spec-ifc.md, Recunoașterea, Fidel fișierului): a recognised wall and its doors
/// and windows get nothing of Lapin's the file does not have (no lintel, no window boards, no threshold, no reveal), each a
/// setting of their types to turn on afterwards; and their openings are the file's IfcOpeningElements, not their fills' size.
/// Walls drawn in Lapin keep their lintels.
/// </summary>
public partial class IfcRecognitionTests
{
    static readonly IfcImportOptions Reporting = new(Fidelity: new FidelityOptions(FidelityOptions.Report));

    /// <summary>A brick wall 5 m long, 300 thick, 2.8 high, with one opening of the given box (mm) filled by a door or window of the given size.</summary>
    string WallWithFill(string name, string fillClass, (double Along, double Sill, double W, double H) opening, (double W, double H) fill)
    {
        var x = new Foreign();
        var pl = x.Place(0, 0, 0);
        var wall = x.Element("IFCWALL", "faithful", "Faithful", pl, x.Shape(x.Box(2500, 0, 0, 5000, 300, 2800)), E("STANDARD"));
        x.Material(wall, x.Brick);
        var op = x.Element("IFCOPENINGELEMENT", "faithful-op", "Opening", x.Place(0, 0, 0, 0, pl), x.Shape(x.Box(opening.Along, 0, opening.Sill, opening.W, 500, opening.H)), E("OPENING"));
        var door = fillClass == "IFCDOOR";
        var made = x.Element(fillClass, "faithful-fill", door ? "D1" : "F1", x.Place(0, 0, 0, 0, pl),
            x.Shape(x.Box(opening.Along, 0, opening.Sill, fill.W, 100, fill.H)), R(fill.H), R(fill.W),
            door ? E("DOOR") : E("WINDOW"), door ? E("SINGLE_SWING_LEFT") : E("SINGLE_PANEL"), Null);
        x.F.Add("IFCRELVOIDSELEMENT", x.G("faithful-v"), Null, Null, Null, wall, op);
        x.F.Add("IFCRELFILLSELEMENT", x.G("faithful-f"), Null, Null, Null, op, made);
        return x.Write(Path.Combine(_dir, name));
    }

    [Fact]
    public void A_recognised_masonry_wall_with_a_window_gets_no_lintel_and_the_files_opening()
    {
        var f = Full();
        // The file's opening 1300 × 1600 at sill 850 round a window of 1200 × 1500.
        var r = IfcImport.Run(f.Session, WallWithFill("window.ifc", "IFCWINDOW", (2500, 850, 1300, 1600), (1200, 1500)), Reporting, out _);
        var id = Guid("foreign-test", "faithful");
        var wall = f.Model.Get(id)!;
        Assert.Equal(IfcClass.Wall, wall.Class);
        var p = Assert.Single(Openings.PlacedOn(f.Model, id));
        Assert.Equal((1.3, 1.6, 0.85), (p.Width, p.Height, p.Sill));
        Assert.Equal((1.2, 1.5), (p.Spec!.Width, p.Spec.Height));
        Assert.Equal(0.85, p.FillSill, 9);
        // No lintel: the wall's type says none, and so does the window's; no window boards, no threshold, no reveal, no U-value the file does not give.
        Assert.DoesNotContain(f.Session.Geometry(id)!.Parts, x => x.Key!.StartsWith("lintel:"));
        var wallType = f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!;
        Assert.Equal("none", LintelRule.From(wallType.Attrs["lintels"])!.Mode);
        Assert.Equal("none", p.Spec.Lintel!.Mode);
        Assert.Equal(new JoineryBoard(0, 0), p.Spec.Board);
        Assert.Equal("none", p.Spec.Details!.Reveal!.Mode);
        Assert.Null(p.Spec.ThermalTransmittance);
        Assert.DoesNotContain(f.Session.Geometry(p.Fill!.Id)!.Parts, x => x.Key is "board" or "threshold" || x.Key!.StartsWith("reveal") || x.Key.StartsWith("sill"));
        // The wall is the file's: its volume, the opening's hole out.
        var check = CheckOf(r, id);
        Assert.True(check.Pass, check.Reason);
        Assert.Equal(5 * 0.3 * 2.8 - 1.3 * 1.6 * 0.3, check.NativeVolume!.Value, 6);
        // The user may turn Lapin's lintels on afterwards, on the type.
        f.Run("architecture.updateWallType", new JsonObject { ["id"] = wallType.Id, ["lintels"] = LintelRule.Default.ToJson() });
        // The window's own rule (none) still wins over the wall's: it is the window type's setting to change too.
        Assert.DoesNotContain(f.Session.Geometry(id)!.Parts, x => x.Key!.StartsWith("lintel:"));
    }

    [Fact]
    public void A_door_in_an_opening_larger_than_itself_keeps_the_openings_size()
    {
        var f = Full();
        // The file's opening 1000 × 2200 round a door of 900 × 2100.
        var r = IfcImport.Run(f.Session, WallWithFill("door.ifc", "IFCDOOR", (2000, 0, 1000, 2200), (900, 2100)), Reporting, out _);
        var id = Guid("foreign-test", "faithful");
        var p = Assert.Single(Openings.PlacedOn(f.Model, id));
        Assert.Equal((1.0, 2.2, 0.0), (p.Width, p.Height, p.Sill));
        Assert.Equal((0.9, 2.1), (p.Spec!.Width, p.Spec.Height));
        Assert.Equal(1.0, p.Opening.Num(Openings.HoleWidthKey), 9);
        Assert.Equal(0, p.Spec.Threshold);
        var check = CheckOf(r, id);
        Assert.True(check.Pass, check.Reason);
        Assert.Equal(5 * 0.3 * 2.8 - 1.0 * 2.2 * 0.3, check.NativeVolume!.Value, 6);
        // Another type: the opening is its type's size again, the door where it stood.
        var other = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = JoinerySpec.DefaultDoor("Plain 900").ToJson() })!["id"]!;
        f.Run("architecture.setJoineryType", new JsonObject { ["id"] = p.Fill!.Id, ["typeId"] = other });
        var q = Assert.Single(Openings.PlacedOn(f.Model, id));
        Assert.Equal((0.9, 2.1), (q.Width, q.Height));
        Assert.Null(q.Opening.Attrs[Openings.HoleWidthKey]);
    }

    [Fact]
    public void A_door_on_the_finished_floor_in_parts_cut_down_to_the_slab_has_the_parts_hole_and_stands_where_the_file_puts_it()
    {
        var f = Full();
        var x = new Foreign();
        // Three parts 3 m long, 2.7 m high, each cut from its foot to 2320 between 1000 and 1890 (ArchiCAD's door through a
        // wall on the slab); the opening element and the door 890 × 2100 from the finished floor at 220.
        (double, double)[] u = [(0, 0), (1000, 0), (1000, 2320), (1890, 2320), (1890, 0), (3000, 0), (3000, 2700), (0, 2700)];
        string? wallPl = null;
        var wall = PartsWall(x, "floor-door", 0, 0, 0, 3000, pl =>
        {
            wallPl = pl;
            return [Part(x, "fd-a", "Board", pl, u, 0, 25), Part(x, "fd-b", "Insulation", pl, u, 25, 105), Part(x, "fd-c", "Board", pl, u, 105, 130)];
        });
        var op = x.Element("IFCOPENINGELEMENT", "fd-op", "Opening", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(1445, 65, 220, 890, 400, 2100)), E("OPENING"));
        var door = x.Element("IFCDOOR", "fd-door", "D1", x.Place(0, 0, 0, 0, wallPl), x.Shape(x.Box(1445, 65, 220, 890, 60, 2100)), "2100.", "890.", E("DOOR"), E("SINGLE_SWING_LEFT"), Null);
        x.F.Add("IFCRELVOIDSELEMENT", x.G("fd-v"), Null, Null, Null, wall, op);
        x.F.Add("IFCRELFILLSELEMENT", x.G("fd-f"), Null, Null, Null, op, door);
        var r = IfcImport.Run(f.Session, x.Write(Path.Combine(_dir, "floor-door.ifc")), Reporting, out _);
        var id = Guid("foreign-test", "floor-door");
        Assert.Equal(IfcClass.Wall, f.Model.Get(id)!.Class);
        var p = Assert.Single(Openings.PlacedOn(f.Model, id));
        Assert.Equal((0.89, 2.32, 0.0), (p.Width, p.Height, p.Sill));
        Assert.Equal(0.22, p.FillSill, 9);
        Assert.Equal((0.89, 2.1), (p.Spec!.Width, p.Spec.Height));
        var check = CheckOf(r, id);
        Assert.True(check.Pass, check.Reason);
        // The door's body stands on the finished floor, not on the slab.
        Assert.Equal(0.22, f.Session.Geometry(p.Fill!.Id)!.Parts.Min(x => Measure.Bounds(x.Solid)!.Value.Min.Z), 6);
    }

    [Fact]
    public void A_wall_drawn_in_Lapin_still_gets_its_lintel()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3 });
        Assert.Single(f.Session.Geometry(wall)!.Parts, x => x.Key!.StartsWith("lintel:"));
        var p = Assert.Single(Openings.PlacedOn(f.Model, wall));
        Assert.Equal((p.Spec!.Width, p.Spec.Height, p.Sill), (p.Width, p.Height, p.FillSill));
    }
}
