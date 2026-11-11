using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Steel.Sdnf;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// SDNF 3.0 (docs/sdnf.md, decision 463, a tester's report #141): the file written as Intergraph's packets lay out (AVEVA's
/// Structural Design guide, SDNF technical information, reproduces the records), read back into an empty project to a tenth
/// of a millimetre, and read again into the project it came from, which updates its elements by their ids.
/// </summary>
public class SdnfTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    /// <summary>A column, a concrete column, a beam between two columns, a sloping brace turned 30°, an angle flipped, a grid.</summary>
    static Fixture Frame()
    {
        var f = F();
        var hea = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = "HEA 200" })!["id"]!;
        var rc = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.4 } })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = hea, ["x"] = 0, ["y"] = 0, ["rotation"] = 0 });
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = hea, ["x"] = 6, ["y"] = 0, ["rotation"] = 90 });
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = rc, ["x"] = 0, ["y"] = 5, ["rotation"] = 15 });
        var ipe = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = "IPE 300" })!["id"]!;
        f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = ipe, ["x1"] = 0, ["y1"] = 0, ["x2"] = 6, ["y2"] = 0 });
        var chs = (string)f.Run("steel.createType", new JsonObject { ["section"] = "CHS 88.9×4" })!["id"]!;
        f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = chs, ["x1"] = 0, ["y1"] = 0.2, ["z1"] = 0.3, ["x2"] = 6, ["y2"] = 0.2, ["z2"] = 2.7, ["rotation"] = 30, ["predefinedType"] = "BRACE" });
        var angle = (string)f.Run("steel.createType", new JsonObject { ["section"] = "L 80×80×8" })!["id"]!;
        var l = (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = angle, ["x1"] = 1, ["y1"] = 3, ["z1"] = 2, ["x2"] = 5, ["y2"] = 4, ["z2"] = 2, ["insertion"] = "top" })!["id"]!;
        f.Run("steel.flip", new JsonObject { ["ids"] = new JsonArray((JsonNode)l) });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = 0, ["y1"] = -1, ["x2"] = 0, ["y2"] = 7, ["tag"] = "1" });
        f.Run("drafting.addAxis", new JsonObject { ["x1"] = -1, ["y1"] = 0, ["x2"] = 7, ["y2"] = 0, ["tag"] = "A" });
        return f;
    }

    static string Export(Fixture f, JsonObject? args = null)
    {
        var r = f.Session.Query("steel.sdnfExport", args ?? [])!;
        return Encoding.ASCII.GetString(Convert.FromBase64String((string)r["data"]!));
    }

    static JsonObject Import(Fixture f, string text) => (JsonObject)f.Run("steel.importSdnf", new JsonObject { ["name"] = "test.sdnf", ["text"] = text })!;

    static IEnumerable<Entity> Linear(IModelView m) => m.OfClass(IfcClass.Column).Concat(m.OfClass(IfcClass.Beam)).Concat(m.OfClass(SteelModule.Member));

    /// <summary>Every packet's records as the specification lays them out: the title's ten, a member's ten of fixed field counts, a plate's, a grid's.</summary>
    [Fact]
    public void TheFileHasTheSpecificationsPacketLayout()
    {
        var f = Frame();
        var text = Export(f);
        var lines = text.Split('\n').Where(l => l.Length > 0).ToList();
        Assert.All(lines, l => Assert.True(l.Length <= 132 && l.All(c => c < 128), l));
        var records = lines.Where(l => !l.StartsWith('#')).ToList();

        // Packet 00: header, version, firm, client, structure, project, date and time, revision and issue code, design code, the loads' count.
        Assert.Equal("Packet 00", records[0]);
        Assert.Equal("\"SDNF Version 3.0\"", records[1]);
        Assert.Equal(2, SdnfReader.Fields(records[6]).Count);
        Assert.Equal(["0", "Lapin"], SdnfReader.Fields(records[7]));
        Assert.Equal("0", records[9]);

        // Packet 10: its units and count, then ten records each, of 7, 5, 11, 2, 6, 12, 9, 12, 7 and 6 fields.
        Assert.Equal("Packet 10", records[10]);
        var head = SdnfReader.Fields(records[11]);
        Assert.Equal("millimeters", head[0]);
        var n = int.Parse(head[1]);
        Assert.Equal(6, n);
        int[] widths = [7, 5, 11, 2, 6, 12, 9, 12, 7, 6];
        for (var i = 0; i < n; i++)
            for (var k = 0; k < 10; k++)
                Assert.Equal(widths[k], SdnfReader.Fields(records[12 + 10 * i + k]).Count);
        var p20 = records.IndexOf("Packet 20");
        Assert.Equal(12 + 10 * n, p20);
        Assert.Equal(["millimeters", "millimeters", "0"], SdnfReader.Fields(records[p20 + 1]));

        // Packet 50: the grid's set, its two lines (a label, a start, an end).
        var p50 = records.IndexOf("Packet 50");
        Assert.Equal(p20 + 2, p50);
        Assert.Equal("1", records[p50 + 1]);
        Assert.Equal(3, SdnfReader.Fields(records[p50 + 2]).Count);
        Assert.Equal(["1", "0", "-1000", "0", "0", "7000", "0"], SdnfReader.Fields(records[p50 + 3]));

        // The members as the file has them.
        var file = SdnfReader.Read(text);
        var columns = file.Members.Where(x => x.Type == "Column").ToList();
        Assert.Equal(3, columns.Count);
        var turned = columns.Single(x => Math.Abs(x.Start.X - 6) < 1e-9);
        Assert.Equal(5, turned.Cardinal);
        Assert.Equal("HEA 200", turned.Size);
        Assert.Equal(new Vec3(6, 0, 0), turned.Start);
        Assert.Equal(new Vec3(6, 0, 3), turned.End);
        // Turned a quarter: the section's up (its web) along −x.
        Assert.True((turned.Orientation - new Vec3(-1, 0, 0)).Length < 1e-6);
        Assert.Equal("300x400", columns.Single(x => x.Start.Y > 4).Size);
        var beam = file.Members.Single(x => x.Type == "Beam");
        Assert.Equal(8, beam.Cardinal);
        Assert.Equal("IPE 300", beam.Size);
        Assert.Equal("Steel", beam.Grade);
        // Its clear span between the columns' faces (HEA 200: the first's web, 3.25 mm off its axis; the turned one's flange, 95 mm), the columns' centres its nodes.
        Assert.Equal(0.00325, beam.Start.X, 9);
        Assert.Equal(5.905, beam.End.X, 9);
        Assert.Equal(3, beam.Start.Z, 9);
        Assert.Equal(-0.00325, beam.StartOffset.X, 9);
        Assert.Equal(0.095, beam.EndOffset.X, 9);
        Assert.Equal("Brace", file.Members.Single(x => x.Size == "CHS 88.9x4").Type);
        var l = file.Members.Single(x => x.Size == "L 80x80x8");
        Assert.True(l.MirrorY);
        Assert.Equal(8, l.Cardinal);
        // Each member's GlobalId in a comment.
        Assert.Equal(6, file.LapinIds.Count(k => k.Key.StartsWith("member ")));
        Assert.All(file.Members, x => Assert.NotNull(f.Model.Get(file.LapinIds[$"member {x.Id}"])));
    }

    /// <summary>Exported, then imported into an empty project: the same members, classes, profiles, places and turns, to a tenth of a millimetre.</summary>
    [Fact]
    public void ARoundTripGivesTheSameMembers()
    {
        var a = Frame();
        var text = Export(a);
        var b = F();
        var report = Import(b, text);
        Assert.Equal(6, (int)report["made"]!);
        Assert.Equal(0, (int)report["updated"]!);
        Assert.Empty((JsonArray)report["unknownProfiles"]!);
        foreach (var e in Linear(a.Model))
        {
            var other = b.Model.Get(e.Id);
            Assert.NotNull(other);
            Assert.Equal(e.Class, other!.Class);
            Assert.Equal(e.PredefinedType, other.PredefinedType);
            var (x, y) = (MemberAxes.Of(a.Model, e)!, MemberAxes.Of(b.Model, other)!);
            Assert.Equal(x.Section.Label, y.Section.Label);
            Assert.Equal(x.Section.Kind, y.Section.Kind);
            Assert.True((x.Start - y.Start).Length < 1e-4, $"{e.Name}: {x.Start} → {y.Start}");
            Assert.True((x.End - y.End).Length < 1e-4, $"{e.Name}: {x.End} → {y.End}");
            Assert.True((x.Frame.V - y.Frame.V).Length < 1e-6 && (x.Frame.W - y.Frame.W).Length < 1e-6, $"{e.Name}: turned otherwise");
            Assert.Equal(x.Flipped, y.Flipped);
            Assert.Equal(x.Insertion, y.Insertion);
            // The bodies too.
            var (ba, bb) = (Measure.Bounds(a.Session.Geometry(e.Id)!.Parts[0].Solid)!.Value, Measure.Bounds(b.Session.Geometry(e.Id)!.Parts[0].Solid)!.Value);
            Assert.True((ba.Min - bb.Min).Length < 1e-4 && (ba.Max - bb.Max).Length < 1e-4, $"{e.Name}: body moved");
        }
        // The beam on its nodes again (the columns' centres), its type's profile and material.
        var beam = b.Model.OfClass(IfcClass.Beam).Single();
        Assert.Equal(new Vec2(0, 0), Joins.Start(beam));
        Assert.Equal(new Vec2(6, 0), Joins.End(beam));
        Assert.Equal("Steel", b.Model.Get((string)b.Model.RelatingOf(beam.Id, IfcClass.RelDefinesByType)!.Attrs["material"]!)!.Name);
        // The grid by its id, its axes.
        var grid = a.Model.OfClass(IfcClass.Grid).Single();
        Assert.Equal(2, ((JsonArray)b.Model.Get(grid.Id)!.Attrs["axes"]!).Count);
        // One undo step takes it all away.
        b.Session.Undo(b.Session.Author);
        Assert.Empty(Linear(b.Model));
    }

    /// <summary>Read again into the project it came from: the elements updated by their ids, none added; a column moved since goes back.</summary>
    [Fact]
    public void AReimportUpdatesTheSameElements()
    {
        var f = Frame();
        var text = Export(f);
        var count = f.Model.Entities.Count();
        var column = f.Model.OfClass(IfcClass.Column).First(c => Args.PointOf(c.Attrs["at"]) == new Vec2(0, 5));
        f.Run("structure.updateColumn", new JsonObject { ["id"] = column.Id, ["x"] = 1, ["y"] = 6 });
        var report = Import(f, text);
        Assert.Equal(0, (int)report["made"]!);
        Assert.Equal(6, (int)report["updated"]!);
        Assert.Equal(count, f.Model.Entities.Count());
        Assert.Equal(new Vec2(0, 5), Args.PointOf(f.Model.Get(column.Id)!.Attrs["at"]));
        Assert.Equal(15, f.Model.Get(column.Id)!.Num("rotation"), 6);
        // A sloping column in the file: made again as a member under its id.
        var lines = text.Split('\n').ToList();
        var file = SdnfReader.Read(text);
        var id = file.Members.First(x => x.Type == "Column" && x.Start.Y > 4).Id;
        var at = lines.FindIndex(x => x.StartsWith($"{id} "));
        var r3 = SdnfReader.Fields(lines[at + 2]);
        r3[6] = "1000";
        lines[at + 2] = string.Join(' ', r3);
        var again = Import(f, string.Join('\n', lines));
        Assert.Equal(1, (int)again["remade"]!);
        Assert.Equal(SteelModule.Member, f.Model.Get(column.Id)!.Class);
        Assert.Equal(count, f.Model.Entities.Count() - (f.Model.OfClass(SteelModule.MemberType).Count() - 2));
    }

    /// <summary>
    /// Another program's file (version 3.0 in feet, as the specification's examples; version 2.0's six records): imported twice,
    /// the second time updates; an unknown profile kept by its name with a note; a cardinal point off the centre places the
    /// section beside its line.
    /// </summary>
    [Fact]
    public void AForeignFileComesInAndAgainUpdates()
    {
        const string v3 = """
            Packet 00
            "SDNF Version 3.0"
            "ABB"
            "XYZ"
            "Building 1"
            "Project XYZ"
            "14/11/97" "09:30"
            16 "StruCad"
            "Design Code name"
            1
            1 1 "Live and Wind"
            Packet10
            "feet" 2
            00100020 8 0 1 "beam" "" 1
            "W12X50" "A36" 0 0 0
            0 0 1.0 10 0 10 10 12.5 10 0 0
            0 0
            0 0 0 0 0 0
            0 0 0 0 1 1 0 0 0 0 1 1
            0 "" 3 "11/18/01" "13:12" "12/01/01" "8:00" 1 0
            12 001 002 0 12 003 004 0 0 1 2 3
            0 30 0.08 1 0 1.5 1.5
            1 2 0 0 0 0
            # a column standing on cardinal point 1
            00100021 1 0 1 "column" "C1" 1
            "HEB 200" "S355" 0 0 0
            0 1 0 0 0 0 0 0 10 0 0
            0 0
            0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0 0 0 0
            0 "" 3 "11/18/01" "13:12" "12/01/01" "8:00" 1 0
            0 0 0 0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0
            0 0 0 0 0 0
            Packet 20
            "feet" "inches" 1
            002 -1 1 1 "slab" 1
            "" "A36" 1 5 3.0 0
            0 0 0 1
            1 0 0 1
            1 1 0 1
            0 1 0 1
            0 0 0 0
            0 0 -0.083333 1
            1 0 -0.083333 1
            1 1 -0.083333 1
            0 1 -0.083333 1
            0 0 -0.083333 0
            0 "" 3 "11/18/01" "13:12" "12/01/01" "8:00" 1 0
            1 2 3
            0 30 0.08 1 0
            1 2 0 0 0 0
            """;
        var f = F();
        var r = Import(f, v3);
        Assert.Equal(2, (int)r["made"]!);
        Assert.Equal(1, (int)r["plates"]!);
        Assert.Contains("W12X50", ((JsonArray)r["unknownProfiles"]!).Select(x => (string)x!));
        Assert.Contains(((JsonArray)r["notes"]!).Select(x => (string)x!), x => x.Contains("released ends"));
        var beam = f.Model.OfClass(IfcClass.Beam).Single();
        Assert.Equal("W12X50", MemberAxes.Of(f.Model, beam)!.Section.Label);
        Assert.Equal(new Vec2(10 * 0.3048, 0), Joins.Start(beam));
        Assert.Equal(new Vec2(10 * 0.3048, 12.5 * 0.3048), Joins.End(beam));
        Assert.Equal(10 * 0.3048, Beams.Top(f.Model, beam), 9);
        // Cardinal point 1, the section's bottom left on the line: its centre half its width and depth inside, its web up y.
        var column = f.Model.OfClass(IfcClass.Column).Single();
        Assert.Equal("C1", column.Name);
        var at = Args.PointOf(column.Attrs["at"]);
        Assert.Equal(0.1, at.X, 9);
        Assert.Equal(0.1, at.Y, 9);
        Assert.Equal("S355", f.Model.Get((string)f.Model.RelatingOf(column.Id, IfcClass.RelDefinesByType)!.Attrs["material"]!)!.Name);
        var plate = f.Model.OfClass("IfcPlate").Single();
        var box = Measure.Bounds(f.Session.Geometry(plate.Id)!.Parts[0].Solid)!.Value;
        Assert.Equal(0.3048, box.Max.X - box.Min.X, 4);
        Assert.Equal(0.0254, box.Max.Z - box.Min.Z, 4);

        var count = f.Model.Entities.Count();
        var again = Import(f, v3);
        Assert.Equal(0, (int)again["made"]!);
        Assert.Equal(2, (int)again["updated"]!);
        Assert.Equal(count, f.Model.Entities.Count());

        // Version 2.0: the title without its version line, six records a member.
        const string v2 = """
            Packet 00
            "Firm"
            "Client"
            "Structure"
            "Project"
            "11/19/91" "13:12"
            18 "issue Code"
            "Design Code Name"
            0
            Packet 10
            "meters" 1
            22 5 0 1 "Brace" "B_101" 14
            "RHS 150x100x6" "S275" 90.0 0 0
            0 0 1 0 0 0 4 3 2 0 0
            0 0
            0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0 0 0 0
            """;
        var g = F();
        var r2 = Import(g, v2);
        Assert.Equal(1, (int)r2["made"]!);
        var brace = g.Model.OfClass(SteelModule.Member).Single();
        Assert.Equal("BRACE", brace.PredefinedType);
        var ax = MemberAxes.Of(g.Model, brace)!;
        Assert.Equal("RHS 150×100×6", ax.Section.Label);
        Assert.True((ax.End - new Vec3(4, 3, 2)).Length < 1e-9);
        // Turned 90° from up, right-handed about its axis.
        var u = new Vec3(4, 3, 2).Normalized();
        var up = (Vec3.UnitZ - u * Vec3.Dot(Vec3.UnitZ, u)).Normalized();
        Assert.True((ax.Frame.W - Vec3.Cross(u, up)).Length < 1e-6);
    }

    /// <summary>A portal frame hall: its connections' plates as packet 20; into an empty project they come in as plates, into their own project they are left to the connections.</summary>
    [Fact]
    public void AHallsPlatesGoOutAndComeBack()
    {
        var f = F();
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["secondary"] = false } });
        var text = Export(f);
        var file = SdnfReader.Read(text);
        Assert.NotEmpty(file.Plates);
        Assert.All(file.Plates, p => Assert.True(p.Thickness > 0 && p.Top.Count >= 5 && p.Top.Count == p.Bottom.Count && p.Top[^1].Flag == 0));
        var empty = F();
        var r = Import(empty, text);
        Assert.Equal(file.Plates.Count, (int)r["plates"]!);
        Assert.Equal(file.Members.Count, (int)r["made"]!);
        var own = Import(f, text);
        Assert.Equal(0, (int)own["plates"]!);
        Assert.Equal(0, (int)own["made"]!);
    }

    // ------------------------------------------------------------ decision 480: curved members, holes, bolts

    /// <summary>
    /// Curved beams as curved members (packet 60, eleven records, the arc the third: centre, start angle, sweep, the plane's
    /// normal): one between two columns (its clear span, its nodes the columns' centres), one free; back into an empty project
    /// as curved beams on the same arcs to a tenth of a millimetre.
    /// </summary>
    [Fact]
    public void ACurvedBeamGoesOutAsACurvedMemberAndComesBack()
    {
        var a = F();
        var hea = (string)a.Run("structure.createColumnType", new JsonObject { ["section"] = "HEA 200" })!["id"]!;
        a.Run("structure.addColumn", new JsonObject { ["storeyId"] = a.Storey, ["typeId"] = hea, ["x"] = 0, ["y"] = 0, ["rotation"] = 0 });
        a.Run("structure.addColumn", new JsonObject { ["storeyId"] = a.Storey, ["typeId"] = hea, ["x"] = 8, ["y"] = 0, ["rotation"] = 0 });
        var ipe = (string)a.Run("structure.createBeamType", new JsonObject { ["section"] = "IPE 300" })!["id"]!;
        a.Run("structure.addBeam", new JsonObject { ["storeyId"] = a.Storey, ["typeId"] = ipe, ["x1"] = 0, ["y1"] = 0, ["x2"] = 8, ["y2"] = 0, ["arc"] = 2 });
        a.Run("structure.addBeam", new JsonObject { ["storeyId"] = a.Storey, ["typeId"] = ipe, ["x1"] = 0, ["y1"] = 10, ["x2"] = 6, ["y2"] = 10, ["arc"] = -1.5 });
        var text = Export(a);

        // The layout: packet 60 after the others, its unit and count, eleven records a member, the arc's eight fields.
        var records = text.Split('\n').Where(l => l.Length > 0 && !l.StartsWith('#')).ToList();
        var p60 = records.IndexOf("Packet 60");
        Assert.True(p60 > records.IndexOf("Packet 20"));
        Assert.Equal(["millimeters", "2"], SdnfReader.Fields(records[p60 + 1]));
        Assert.Equal(p60 + 2 + 22, records.Count);
        int[] widths = [7, 5, 8, 11, 2, 6, 12, 9, 12, 7, 6];
        for (var i = 0; i < 2; i++)
            for (var k = 0; k < 11; k++)
                Assert.Equal(widths[k], SdnfReader.Fields(records[p60 + 2 + 11 * i + k]).Count);
        var file = SdnfReader.Read(text);
        var trimmed = file.Members.Single(x => x.Arc != null && x.Start.Y < 5);
        // The arc through (0, 0) and (8, 0) rising 2 m to the left: radius 5 m about (4, −3), turning clockwise seen from above.
        Assert.True((trimmed.Arc!.Centre - new Vec3(4, -3, 3)).Length < 1e-9);
        Assert.True((trimmed.Arc.Normal - new Vec3(0, 0, -1)).Length < 1e-9);
        Assert.Equal(5, (trimmed.Start - trimmed.Arc.Centre).Length, 6);
        Assert.Equal(8, trimmed.Cardinal);
        // Its clear span stops at the columns' faces; the offsets go on to their centres.
        Assert.True(trimmed.Start.X > 0.003 && trimmed.End.X < 7.997);
        Assert.True((trimmed.Start + trimmed.StartOffset - new Vec3(0, 0, 3)).Length < 1e-6);
        Assert.Equal(0, file.Members.Count(x => x.Arc == null && x.Type == "Beam"));
        // Start angle and sweep say the same arc as the ends.
        var free = file.Members.Single(x => x.Arc != null && x.Start.Y > 5).Arc!;
        Assert.True((free.Normal - Vec3.UnitZ).Length < 1e-9);
        var end = SdnfArc.AngleOf(free.Centre, free.Normal, file.Members.Single(x => x.Arc == free).End);
        Assert.Equal(0, Math.IEEERemainder(free.StartAngle + free.Sweep - end, 360), 4);

        var b = F();
        var report = Import(b, text);
        Assert.Equal(4, (int)report["made"]!);
        Assert.DoesNotContain(((JsonArray)report["notes"]!).Select(x => (string)x!), x => x.Contains("curved", StringComparison.OrdinalIgnoreCase));
        foreach (var beam in a.Model.OfClass(IfcClass.Beam))
        {
            var other = b.Model.Get(beam.Id);
            Assert.NotNull(other);
            Assert.Equal(IfcClass.Beam, other!.Class);
            var (x, y) = (PathCurve.Of(beam), PathCurve.Of(other));
            Assert.True((x.Start - y.Start).Length < 1e-4 && (x.End - y.End).Length < 1e-4, $"{x.Start}–{x.End} → {y.Start}–{y.End}");
            Assert.Equal(x.Radius, y.Radius, 4);
            Assert.True((x.Centre - y.Centre).Length < 1e-4);
            Assert.Equal(x.Rise, y.Rise, 4);
            Assert.Equal(Beams.Top(a.Model, beam), Beams.Top(b.Model, other), 4);
        }
        // Read again into its own project: updated, nothing added.
        var count = a.Model.Entities.Count();
        var again = Import(a, text);
        Assert.Equal(4, (int)again["updated"]!);
        Assert.Equal(count, a.Model.Entities.Count());
    }

    /// <summary>
    /// Another program's curved members, in millimetres: one level, its line through its section's centre (cardinal point 5),
    /// comes in as a curved beam whose top centre runs on the same arc 150 mm higher; one arching in a vertical plane is left
    /// out and said.
    /// </summary>
    [Fact]
    public void AForeignCurvedMemberComesInLevelOrIsSaid()
    {
        const string text = """
            Packet 00
            "SDNF Version 3.0"
            "Firm"
            "Client"
            "Canopy"
            "Project"
            "11/11/26" "10:00"
            1 "Other"
            ""
            0
            Packet 10
            "millimeters" 0
            Packet 60
            "millimeters" 2
            31 5 0 0 "beam" "R1" 0
            "IPE 300" "S355" 0 0 0
            0 0 3000 0 90 0 0 1
            0 0 1 5000 0 3000 0 5000 3000 0 0
            0 0
            0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0 0 0 0
            0 "" 0 "11/11/26" "10:00" "11/11/26" "10:00" 0 0
            0 0 0 0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0
            0 0 0 0 0 0
            32 5 0 0 "rafter" "A1" 0
            "IPE 300" "S355" 0 0 0
            0 0 0 0 90 0 -1 0
            0 1 0 4000 0 0 0 0 4000 0 0
            0 0
            0 0 0 0 0 0
            0 0 0 0 0 0 0 0 0 0 0 0
            0 "" 0 "11/11/26" "10:00" "11/11/26" "10:00" 0 0
            0 0 0 0 0 0 0 0 0 0 0 0
            0 0 0 0 0 0 0
            0 0 0 0 0 0
            """;
        var f = F();
        var r = Import(f, text);
        Assert.Equal(1, (int)r["made"]!);
        Assert.Equal(1, (int)r["skipped"]!);
        Assert.Contains(((JsonArray)r["notes"]!).Select(x => (string)x!), x => x.Contains("Curved member 32") && x.Contains("level"));
        var beam = f.Model.OfClass(IfcClass.Beam).Single();
        Assert.Equal("R1", beam.Name);
        var c = PathCurve.Of(beam);
        Assert.Equal(new Vec2(5, 0), c.Start);
        Assert.True((c.End - new Vec2(0, 5)).Length < 1e-9);
        Assert.Equal(5, c.Radius, 6);
        Assert.True(c.Centre.Length < 1e-6);
        // Counter-clockwise from (5, 0) to (0, 5): the arc bulges right of its chord, out from the centre.
        Assert.True(c.Rise < 0);
        Assert.Equal(3.15, Beams.Top(f.Model, beam), 6);
    }

    /// <summary>
    /// Holes (packet 22, AVEVA's records: its number and parent plate's; periphery, depth, vertices per face; the loops;
    /// two more): a round hole and a rectangular one in a plate, a round one through a beam's web (its parent the beam, as
    /// Lapin writes them); a recess and a hole of no plate are said and left out. Cut in the bodies, out again with their
    /// numbers, back into an empty project in the same places to a tenth of a millimetre.
    /// </summary>
    [Fact]
    public void HolesInAPlateAndAMemberGoOutAndComeBack()
    {
        var f = F();
        var r = Import(f, HoledFile(withMemberHole: true));
        Assert.Equal(1, (int)r["plates"]!);
        Assert.Equal(3, (int)r["holes"]!);
        var notes = ((JsonArray)r["notes"]!).Select(x => (string)x!).ToList();
        Assert.Contains(notes, x => x.Contains("Hole 24") && x.Contains("recess"));
        Assert.Contains(notes, x => x.Contains("99") && x.Contains("does not have"));
        Assert.Contains(notes, x => x.Contains("no bolts"));

        // The plate's body: 400 × 300 × 20 less a Ø26 hole (by the chord rule) and a 60 × 20 one.
        var plate = f.Model.OfClass("IfcPlate").Single();
        var solid = f.Session.Geometry(plate.Id)!.Parts[0].Solid;
        var ring = MemberHoles.Ring(Vec3.Zero, Vec3.UnitZ, 0.013);
        var circle = Math.Abs(Polygon2.SignedArea([.. ring.Select(p => p.Xy)]));
        Assert.True(Math.Abs(0.4 * 0.3 * 0.02 - circle * 0.02 - 0.06 * 0.02 * 0.02 - Measure.Volume(solid)) < 2e-7, $"{Measure.Volume(solid)}");
        // The beam's web hole, kept on it in its frame.
        var beam = f.Model.OfClass(IfcClass.Beam).Single();
        var hole = Assert.Single(MemberHoles.Of(beam));
        Assert.Equal(23, hole.Number);
        Assert.Equal(0.011, hole.Radius, 9);
        var withHole = Measure.Volume(f.Session.Geometry(beam.Id)!.Parts[0].Solid);

        // Out again: the three holes with their numbers, the plate's under its own number, the beam's under the beam's.
        var text = Export(f);
        var file = SdnfReader.Read(text);
        Assert.Equal(3, file.Holes.Count);
        var plateNumber = file.Plates.Single().Id;
        Assert.Equal(2, plateNumber);
        Assert.Equal([21L, 22L], file.Holes.Where(h => h.Parent == plateNumber).Select(h => h.Id).Order());
        var webHole = file.Holes.Single(h => h.Id == 23);
        Assert.Equal(file.Members.Single().Id, webHole.Parent);
        Assert.Equal(2, webHole.Periphery);
        Assert.Equal(0.011, webHole.Top[0].Flag, 6);
        Assert.True((webHole.Top[0].P - new Vec3(3, -0.01, 3)).Length < 1e-4 && (webHole.Bottom[0].P - new Vec3(3, 0.01, 3)).Length < 1e-4, $"{webHole.Top[0].P} {webHole.Bottom[0].P}");
        var rect = file.Holes.Single(h => h.Id == 22);
        Assert.Equal(0, rect.Periphery);
        Assert.Equal(5, rect.Top.Count);
        Assert.True((rect.Top[1].P - new Vec3(0.26, 0.14, 0.02)).Length < 1e-4);

        // Into an empty project: the same holes in the same places.
        var g = F();
        var back = Import(g, text);
        Assert.Equal(3, (int)back["holes"]!);
        var b2 = g.Model.OfClass(IfcClass.Beam).Single();
        var (ax1, ax2) = (MemberAxes.Of(f.Model, beam)!, MemberAxes.Of(g.Model, b2)!);
        var (h1, h2) = (MemberHoles.Of(beam)[0], MemberHoles.Of(b2)[0]);
        Assert.True((MemberHoles.World(ax1, MemberHoles.Origin(f.Model, beam, ax1), h1.Top[0]) - MemberHoles.World(ax2, MemberHoles.Origin(g.Model, b2, ax2), h2.Top[0])).Length < 1e-4);
        Assert.Equal(h1.Radius, h2.Radius, 6);
        var p2 = g.Model.OfClass("IfcPlate").Single();
        Assert.True(Math.Abs(Measure.Volume(solid) - Measure.Volume(g.Session.Geometry(p2.Id)!.Parts[0].Solid)) < 2e-7);
        Assert.Equal(withHole, Measure.Volume(g.Session.Geometry(b2.Id)!.Parts[0].Solid), 8);
        // IFC: the hole an opening of the beam's swept body.
        var swept = new SteelModule().BodyOf(g.Model, b2);
        Assert.NotNull(swept);
        Assert.Single(swept!.Openings);
    }

    /// <summary>The same file read again: the holes replaced, not added; read without the beam's hole, the beam has none and its web is whole again.</summary>
    [Fact]
    public void AReimportReplacesHolesRatherThanAddingThem()
    {
        var f = F();
        Import(f, HoledFile(withMemberHole: true));
        var beam = f.Model.OfClass(IfcClass.Beam).Single();
        var plate = f.Model.OfClass("IfcPlate").Single();
        var withHole = Measure.Volume(f.Session.Geometry(beam.Id)!.Parts[0].Solid);
        var plateVolume = Measure.Volume(f.Session.Geometry(plate.Id)!.Parts[0].Solid);
        var count = f.Model.Entities.Count();

        var again = Import(f, HoledFile(withMemberHole: true));
        Assert.Equal(3, (int)again["holes"]!);
        Assert.Equal(count, f.Model.Entities.Count());
        Assert.Single(MemberHoles.Of(f.Model.Get(beam.Id)!));
        Assert.Equal(2, ((JsonArray)((JsonObject)f.Model.Get(plate.Id)!.Attrs[SdnfImport.Key]!)["holes"]!).Count);
        Assert.Equal(plateVolume, Measure.Volume(f.Session.Geometry(plate.Id)!.Parts[0].Solid), 9);

        var without = Import(f, HoledFile(withMemberHole: false));
        Assert.Equal(2, (int)without["holes"]!);
        Assert.Empty(MemberHoles.Of(f.Model.Get(beam.Id)!));
        var whole = Measure.Volume(f.Session.Geometry(beam.Id)!.Parts[0].Solid);
        // The web's Ø22 hole through IPE 300's 7.1 mm web.
        var circle = Math.Abs(Polygon2.SignedArea([.. MemberHoles.Ring(Vec3.Zero, Vec3.UnitZ, 0.011).Select(p => p.Xy)]));
        Assert.Equal(circle * 0.0071, whole - withHole, 7);
        // One undo step takes the import back: the hole again.
        f.Session.Undo(f.Session.Author);
        Assert.Single(MemberHoles.Of(f.Model.Get(beam.Id)!));
    }

    /// <summary>
    /// Bolts: SDNF has no packet for them, so Lapin writes the holes its connections' bolts and anchors make in their plates
    /// (round, 2 mm over an M24 and smaller, 3 mm over larger), and reads holes as holes, saying the bolts did not come.
    /// </summary>
    [Fact]
    public void BoltsGoOutAsTheirHolesAndComeBackAsHoles()
    {
        var f = F();
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey, ["attrs"] = new JsonObject { ["secondary"] = false } });
        var file = SdnfReader.Read(Export(f));
        Assert.NotEmpty(file.Holes);
        var plates = file.Plates.ToDictionary(p => p.Id);
        foreach (var h in file.Holes)
        {
            Assert.True(plates.ContainsKey(h.Parent), $"hole {h.Id} names no plate");
            Assert.Equal(2, h.Periphery);
            var d = Math.Round(h.Top[0].Flag * 2 * 1000, 6);
            Assert.True(d is 14 or 18 or 22 or 26 or 30 or 33, $"Ø{d}");
            Assert.Equal(plates[h.Parent].Thickness, (h.Bottom[0].P - h.Top[0].P).Length, 6);
        }
        // Every base plate has its anchors' holes.
        var nodes = f.Model.OfClass(SteelNodes.Class).Where(n => SteelNodes.KindOf(n) == "base").ToList();
        Assert.NotEmpty(nodes);
        Assert.True(file.Holes.Count >= nodes.Sum(n => SteelNodes.Build(f.Model, n).Spec.Anchors));

        var empty = F();
        var r = Import(empty, Export(f));
        Assert.Equal(file.Holes.Count, (int)r["holes"]!);
        Assert.Contains(((JsonArray)r["notes"]!).Select(x => (string)x!), x => x.Contains("no bolts"));
        Assert.DoesNotContain(empty.Model.Entities, e => e.Name.StartsWith("Bolt"));
    }

    /// <summary>A plate 400 × 300 × 20 mm with a Ø26 hole, a 60 × 20 one, a recess and a hole of no plate; an IPE 300 beam with a Ø22 hole through its web.</summary>
    static string HoledFile(bool withMemberHole) => $$"""
        Packet 00
        "SDNF Version 3.0"
        "Firm"
        "Client"
        "Frame"
        "Project"
        "11/11/26" "10:00"
        1 "Other"
        ""
        0
        Packet 10
        "millimeters" 1
        7 5 0 0 "beam" "B7" 0
        "IPE 300" "S355" 0 0 0
        0 0 1 0 0 3000 6000 0 3000 0 0
        0 0
        0 0 0 0 0 0
        0 0 0 0 0 0 0 0 0 0 0 0
        0 "" 0 "11/11/26" "10:00" "11/11/26" "10:00" 0 0
        0 0 0 0 0 0 0 0 0 0 0 0
        0 0 0 0 0 0 0
        0 0 0 0 0 0
        Packet 20
        "millimeters" "millimeters" 1
        2 0 0 0 "base" 0
        "PL1" "S235" 20 5 0 0
        0 0 20 1
        400 0 20 1
        400 300 20 1
        0 300 20 1
        0 0 20 0
        0 0 0 1
        400 0 0 1
        400 300 0 1
        0 300 0 1
        0 0 0 0
        0 "" 0 "11/11/26" "10:00" "11/11/26" "10:00" 0 0
        0 0 0
        0 0 0 0 0
        0 0 0 0 0 0
        Packet 22
        "millimeters" "millimeters" {{(withMemberHole ? 5 : 4)}}
        21 2
        2 20 1
        100 100 20 13
        100 100 0 13
        0 0 0 0
        0 0 0
        22 2
        0 20 5
        200 140 20 1
        260 140 20 1
        260 160 20 1
        200 160 20 1
        200 140 20 0
        200 140 0 1
        260 140 0 1
        260 160 0 1
        200 160 0 1
        200 140 0 0
        0 0 0 0
        0 0 0
        24 2
        2 5 1
        300 60 20 10
        300 60 15 10
        0 0 0 0
        0 0 0
        25 99
        2 20 1
        50 50 20 5
        50 50 0 5
        0 0 0 0
        0 0 0
        {{(withMemberHole ? "23 7\n2 20 1\n3000 -10 3000 11\n3000 10 3000 11\n0 0 0 0\n0 0 0" : "")}}
        """;
}
