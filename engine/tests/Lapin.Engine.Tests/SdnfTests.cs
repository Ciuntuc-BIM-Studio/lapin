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
}
