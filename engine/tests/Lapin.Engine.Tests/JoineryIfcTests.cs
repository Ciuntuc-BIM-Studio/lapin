using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Phases;
using Lapin.Platform;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// How doors and windows open, in IFC (docs/spec-ifc.md, "Deschiderea ușilor și
/// ferestrelor"): the element's local +Y is the way its panels open and left and
/// right are seen looking that way; the type says its OperationType (a door) or
/// PartitioningType (a window) and each panel's operation and position; an
/// element flipped on one axis is an occurrence of the type's mirror image; a
/// Lapin file read back keeps one type.
/// </summary>
public class JoineryIfcTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-joinery-ifc").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture Full() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new CivilModule(), new PhasesModule(), new StudioModule());

    static List<object?> Items(object? o) => o as List<object?> ?? [];
    static string? Enum(object? v) => (v as StepEnum)?.Value;

    /// <summary>A wall along +X with a door, a window, a door flipped by hand and one flipped both ways.</summary>
    static (Fixture F, string Door, string Window, string Hand, string Both) House()
    {
        var f = Full();
        var wall = f.Wall(0, 0, 8, 0);
        string Add(string what, double along) => (string)f.Run($"architecture.add{what}", new JsonObject { ["wallId"] = wall, ["along"] = along })!["id"]!;
        var door = Add("Door", 1);
        var window = Add("Window", 3);
        var hand = Add("Door", 5);
        var both = Add("Door", 7);
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(hand, both), ["axis"] = "x" });
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(both), ["axis"] = "y" });
        return (f, door, window, hand, both);
    }

    [Fact]
    public void A_door_says_its_hand_and_opens_along_its_placements_y()
    {
        var (f, door, window, hand, both) = House();
        var path = Path.Combine(_dir, "house.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        StepEntity Product(string id) => step.OfType("IFCDOOR").Concat(step.OfType("IFCWINDOW")).Single(e => (string?)e[7] == id);
        StepEntity TypeOf(StepEntity e) => step.Get(step.OfType("IFCRELDEFINESBYTYPE").Single(r => Items(r[4]).Contains(new StepRef(e.Id)))[5])!;
        (double X, double Y) RefDirection(StepEntity e)
        {
            var axes = step.Get(step.Get(e[5])![1])!;
            var d = Items(step.Get(axes[2])![0]);
            return ((double)d[0]!, (double)d[1]!);
        }

        // The door as drawn: hinges on the left of someone pushing it open, so IFC's left; its +X along the wall, so +Y (the way it opens) the wall's left.
        var d = Product(door);
        Assert.Equal("SINGLE_SWING_LEFT", Enum(d[11]));
        Assert.Equal((1.0, 0.0), RefDirection(d));
        var type = TypeOf(d);
        Assert.Equal("IFCDOORTYPE", type.Type);
        Assert.Equal("SINGLE_SWING_LEFT", Enum(type[10]));
        var sets = Items(type[5]).Select(step.Get).OfType<StepEntity>().ToList();
        var panel = Assert.Single(sets, s => s.Type == "IFCDOORPANELPROPERTIES");
        Assert.Equal(("SWINGING", "MIDDLE", 1.0), (Enum(panel[5]), Enum(panel[7]), (double)panel[6]!));
        Assert.Single(sets, s => s.Type == "IFCDOORLININGPROPERTIES");
        var spec = Openings.SpecOf(f.Model, door)!;

        // Flipped by hand: the type's mirror image, a right-hand door.
        var h = Product(hand);
        Assert.Equal("SINGLE_SWING_RIGHT", Enum(h[11]));
        var mirror = TypeOf(h);
        Assert.Equal("SINGLE_SWING_RIGHT", Enum(mirror[10]));
        Assert.EndsWith("(mirrored)", (string?)mirror[2]);
        Assert.NotEqual(type.Id, mirror.Id);
        // Flipped both ways: the type turned round, opening the other way.
        var b = Product(both);
        Assert.Equal("SINGLE_SWING_LEFT", Enum(b[11]));
        Assert.Equal(type.Id, TypeOf(b).Id);
        Assert.Equal((-1.0, 0.0), RefDirection(b));
        Assert.Equal(2, step.OfType("IFCDOORTYPE").Count());

        // The window: two panels side by side, tilt-and-turn on the left, side-hung on the right.
        var w = Product(window);
        Assert.Equal("DOUBLE_PANEL_VERTICAL", Enum(w[11]));
        var wSets = Items(TypeOf(w)[5]).Select(step.Get).OfType<StepEntity>().ToList();
        Assert.Equal([("TILTANDTURNLEFTHAND", "LEFT"), ("SIDEHUNGRIGHTHAND", "RIGHT")],
            wSets.Where(s => s.Type == "IFCWINDOWPANELPROPERTIES").Select(s => (Enum(s[4]), Enum(s[5]))).Select(t => (t.Item1!, t.Item2!)));
        var lining = Assert.Single(wSets, s => s.Type == "IFCWINDOWLININGPROPERTIES");
        Assert.Equal(0.5, (double)lining[10]!, 3);
        Assert.Equal(spec.Width, (double)d[9]!, 6);
    }

    [Fact]
    public void A_door_body_is_written_in_its_own_frame()
    {
        var (f, door, _, _, both) = House();
        var path = Path.Combine(_dir, "frames.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        foreach (var id in new[] { door, both })
        {
            var e = step.OfType("IFCDOOR").Single(x => (string?)x[7] == id);
            var body = Items(step.Get(e[6])![2]).Select(step.Get).Single(r => (string?)r![1] == "Body")!;
            var xs = new List<double>();
            foreach (var brep in Items(body[3]).Select(step.Get))
                foreach (var face in Items(step.Get(brep![0])![0]).Select(step.Get))
                    foreach (var bound in Items(face![0]).Select(step.Get))
                        foreach (var p in Items(step.Get(bound![0])![0]).Select(step.Get))
                            xs.Add((double)Items(p![0])[0]!);
            // Along the opening from its left edge: 0 to the door's width, whichever way it stands.
            Assert.InRange(xs.Min(), -0.06, 0.01);
            Assert.InRange(xs.Max(), 0.89, 0.96);
        }
    }

    [Fact]
    public void Lapins_own_file_reads_back_with_one_type_and_its_flips()
    {
        var (f, door, _, hand, both) = House();
        var path = Path.Combine(_dir, "round.ifc");
        IfcExport.Write(f.Session, path);
        var g = Full();
        IfcImport.Run(g.Session, path, null, out _);
        var m = g.Session.Model;
        Assert.Single(m.OfClass(IfcClass.DoorType));
        var type = m.OfClass(IfcClass.DoorType).Single();
        Assert.DoesNotContain("mirrored", type.Name);
        Assert.Null(type.Attrs["LapinMirrorOf"]);
        Assert.Equal(type.Id, m.RelatingOf(hand, IfcClass.RelDefinesByType)!.Id);
        Assert.True(m.Get(hand)!.Bool("flipHand"));
        Assert.False(m.Get(hand)!.Bool("flipSide"));
        Assert.True(m.Get(both)!.Bool("flipSide"));
        Assert.False(m.Get(door)!.Bool("flipHand"));
    }

    /// <summary>What Lapin writes of a type it reads back the same: every door operation, single and paired, and the window partitions.</summary>
    [Fact]
    public void The_ifc_terms_read_back_into_the_same_panels()
    {
        foreach (var op in Joinery.DoorOperations)
        {
            var one = JoinerySpec.DefaultDoor() with { Panels = [new(0, 0, op)] };
            Check(one);
        }
        foreach (var (a, b) in new[] { ("swing-left", "swing-right"), ("swing-left", "swing-left"), ("swing-right", "swing-right"), ("double-acting-left", "double-acting-right"), ("door-sliding-left", "door-sliding-right") })
            Check(JoinerySpec.DefaultDoor() with { Width = 1.6, Columns = [1, 1], Panels = [new(0, 0, a), new(1, 0, b)] });
        Check(JoinerySpec.DefaultDoor() with { Width = 1.3, Columns = [2, 1], Panels = [new(0, 0, "swing-left"), new(1, 0, "fixed")] });
        Check(JoinerySpec.DefaultWindow());
        Check(JoinerySpec.DefaultWindow() with { Columns = [1], Panels = [new(0, 0, "side-right")] });
        Check(JoinerySpec.DefaultWindow() with { Columns = [1, 1, 1], Panels = [new(0, 0, "fixed"), new(1, 0, "tilt-turn-right"), new(2, 0, "bottom-hung")] });
        Check(JoinerySpec.DefaultWindow() with { Columns = [1], Rows = [2, 1], Panels = [new(0, 0, "tilt-turn-left"), new(0, 1, "top-hung")] });
        Check(JoinerySpec.DefaultWindow() with { Columns = [1, 1], Rows = [2, 1], Panels = [new(0, 0, "side-left"), new(1, 0, "side-right"), new(0, 1, "top-hung", ColSpan: 2)] });

        static void Check(JoinerySpec spec)
        {
            var terms = JoineryIfc.Of(spec);
            var back = JoineryIfc.FromIfc(spec with { Columns = [1], Rows = [1], Panels = [] }, terms.Operation, terms.UserDefined, terms.Panels)!;
            Assert.NotNull(back);
            string Says(JoinerySpec s) => string.Join(" ", Joinery.Normalised(s).OrderBy(p => p.Row).ThenBy(p => p.Col).Select(p => $"{p.Col},{p.Row},{p.ColSpan}x{p.RowSpan}:{p.Operation}"));
            Assert.Equal(Says(spec), Says(back));
            // The mirror image is the same type seen the other way: its terms mirrored back are the type's.
            var mirrored = JoineryIfc.Of(spec, mirrored: true);
            Assert.Equal(terms.Operation, IfcHands.Mirror(mirrored.Operation));
        }
    }
}
