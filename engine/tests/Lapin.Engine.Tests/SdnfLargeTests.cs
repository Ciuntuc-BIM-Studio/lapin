using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Steel.Sdnf;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// A tester's report #148: a large SDNF file left the window busy with nothing to show. A frame of thousands of members,
/// plates and holes comes in within a time that grows with the file (each beam reads only the columns round it, the
/// storeys and types are related in one go), its stages told as it goes, stopped when asked; its protocol counts each
/// packet and says what was left out and why; a record that cannot be read names its line.
/// </summary>
public class SdnfLargeTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    const string Title = """
        Packet 00
        "SDNF Version 3.0"
        "Firm"
        "Client"
        "Big"
        "Project"
        "11/11/26" "10:00"
        1 "Other"
        ""
        0
        """;

    static void Member(StringBuilder o, int id, string type, string size, int cardinal, (double, double, double) up, (double, double, double) a, (double, double, double) b)
    {
        o.Append(Inv, $"{id} {cardinal} 0 0 \"{type}\" \"M{id}\" 0\n");
        o.Append(Inv, $"\"{size}\" \"S355\" 0 0 0\n");
        o.Append(Inv, $"{up.Item1} {up.Item2} {up.Item3} {a.Item1} {a.Item2} {a.Item3} {b.Item1} {b.Item2} {b.Item3} 0 0\n");
        o.Append("0 0\n0 0 0 0 0 0\n0 0 0 0 0 0 0 0 0 0 0 0\n0 \"\" 0 \"11/11/26\" \"10:00\" \"11/11/26\" \"10:00\" 0 0\n0 0 0 0 0 0 0 0 0 0 0 0\n0 0 0 0 0 0 0\n0 0 0 0 0 0\n");
    }

    /// <summary>
    /// A frame <paramref name="nx"/> × <paramref name="ny"/> bays of 6 m, <paramref name="levels"/> storeys of 4 m: columns
    /// (HEB 300), beams both ways (IPE 400, IPE 300) and a brace a bay along the first line; a base plate under each
    /// ground-floor column with four round holes. <paramref name="extra"/> members after them, as they are.
    /// </summary>
    public static (string Text, int Members, int Plates, int Holes) Frame(int nx, int ny, int levels, Action<StringBuilder, Func<int>>? extra = null)
    {
        const double sp = 6000, h = 4000;
        var members = new StringBuilder();
        var n = 0;
        for (var k = 0; k < levels; k++)
        {
            double z0 = k * h, z1 = (k + 1) * h;
            for (var i = 0; i < nx; i++)
                for (var j = 0; j < ny; j++)
                    Member(members, ++n, "column", "HEB 300", 5, (0, 1, 0), (i * sp, j * sp, z0), (i * sp, j * sp, z1));
            for (var i = 0; i + 1 < nx; i++)
                for (var j = 0; j < ny; j++)
                    Member(members, ++n, "beam", "IPE 400", 8, (0, 0, 1), (i * sp, j * sp, z1), ((i + 1) * sp, j * sp, z1));
            for (var i = 0; i < nx; i++)
                for (var j = 0; j + 1 < ny; j++)
                    Member(members, ++n, "beam", "IPE 300", 8, (0, 0, 1), (i * sp, j * sp, z1), (i * sp, (j + 1) * sp, z1));
            for (var i = 0; i + 1 < nx; i++)
                Member(members, ++n, "brace", "CHS 88.9x4", 5, (0, 0, 1), (i * sp, 0, z0), ((i + 1) * sp, 0, z1));
        }
        extra?.Invoke(members, () => ++n);

        var plates = new StringBuilder();
        var holes = new StringBuilder();
        var (np, nh) = (0, 0);
        for (var i = 0; i < nx; i++)
            for (var j = 0; j < ny; j++)
            {
                var p = 100_000 + ++np;
                var (x, y) = (i * sp, j * sp);
                plates.Append(Inv, $"{p} 0 0 0 \"base\" 0\n\"PL{p}\" \"S235\" 20 5 0 0\n");
                foreach (var z in new[] { 0, -20 })
                    foreach (var (dx, dy, flag) in new[] { (-200, -200, 1), (200, -200, 1), (200, 200, 1), (-200, 200, 1), (-200, -200, 0) })
                        plates.Append(Inv, $"{x + dx} {y + dy} {z} {flag}\n");
                plates.Append("0 \"\" 0 \"11/11/26\" \"10:00\" \"11/11/26\" \"10:00\" 0 0\n0 0 0\n0 0 0 0 0\n0 0 0 0 0 0\n");
                foreach (var (dx, dy) in new[] { (-150, -150), (150, -150), (150, 150), (-150, 150) })
                    holes.Append(Inv, $"{500_000 + ++nh} {p}\n2 20 1\n{x + dx} {y + dy} 0 13\n{x + dx} {y + dy} -20 13\n0 0 0 0\n0 0 0\n");
            }
        var text = new StringBuilder(Title).Append('\n')
            .Append(Inv, $"Packet 10\n\"millimeters\" {n}\n").Append(members)
            .Append(Inv, $"Packet 20\n\"millimeters\" \"millimeters\" {np}\n").Append(plates)
            .Append(Inv, $"Packet 22\n\"millimeters\" \"millimeters\" {nh}\n").Append(holes);
        return (text.ToString(), n, np, nh);
    }

    static JsonObject Import(Fixture f, string text) => (JsonObject)f.Run("steel.importSdnf", new JsonObject { ["name"] = "big.sdnf", ["text"] = text })!;

    /// <summary>
    /// 3,537 members, 400 plates and 1,600 holes in well under a minute (a few seconds on a laptop; it took minutes, growing with
    /// the square of the frame), every one of them in, one undo step; a beam built from the columns round it, not from all 1,200.
    /// </summary>
    [Fact]
    public void ALargeFrameComesInQuicklyAndWhole()
    {
        var (text, members, plates, holes) = Frame(20, 20, 3);
        Assert.Equal(3537, members);
        var f = F();
        var stages = new List<(string Stage, int Done, int Total)>();
        f.Session.Progress = (s, d, t) => { lock (stages) stages.Add((s, d, t)); };
        var clock = Stopwatch.StartNew();
        var r = Import(f, text);
        clock.Stop();
        Assert.True(clock.Elapsed.TotalSeconds < 60, $"{clock.Elapsed.TotalSeconds:0.0} s");

        Assert.Equal(members, (int)r["made"]!);
        Assert.Equal(plates, (int)r["plates"]!);
        Assert.Equal(holes, (int)r["holes"]!);
        Assert.Equal(0, (int)r["skipped"]!);
        Assert.Equal(1200, (int)r["byClass"]![IfcClass.Column]!);
        Assert.Equal(2280, (int)r["byClass"]![IfcClass.Beam]!);
        Assert.Empty((JsonArray)r["skips"]!);
        var packets = ((JsonArray)r["packets"]!).Select(x => (JsonObject)x!).ToDictionary(x => (string)x["packet"]!);
        Assert.Equal((members, members, members), ((int)packets["10"]["announced"]!, (int)packets["10"]["read"]!, (int)packets["10"]["imported"]!));
        Assert.Equal((plates, plates), ((int)packets["20"]["read"]!, (int)packets["20"]["imported"]!));
        Assert.Equal((holes, holes), ((int)packets["22"]["read"]!, (int)packets["22"]["imported"]!));

        // One undo step; the storeys and types each one relationship.
        Assert.Equal(members + plates, f.Model.OfClass(IfcClass.Column).Count() + f.Model.OfClass(IfcClass.Beam).Count() + f.Model.OfClass(SteelModule.Member).Count() + f.Model.OfClass("IfcPlate").Count());
        foreach (var s in Spatial.Storeys(f.Model))
            Assert.True(f.Model.RelationsOf(s.Id, IfcClass.RelContainedInSpatialStructure).Count(x => x.Relating == s.Id) <= 1);

        // A beam reads the columns round it, not all of them: the dependency the build notes is a handful, whatever the frame.
        var beam = f.Model.OfClass(IfcClass.Beam).First();
        var columnsRead = f.Session.Reader.DependenciesOf(beam.Id).Count(id => f.Model.Get(id)?.Class == IfcClass.Column);
        Assert.InRange(columnsRead, 1, 12);
        Assert.NotNull(f.Session.Reader.Geometry(beam.Id));

        // Its stages, each ending at its total.
        Assert.Contains(stages, x => x.Stage == "Reading the file");
        Assert.Contains(stages, x => x.Stage == "Bringing the members in" && x.Done == members && x.Total == members);
        Assert.Contains(stages, x => x.Stage == "Bringing the plates in" && x.Done == plates);
        Assert.Contains(stages, x => x.Stage == "Building the model");

        f.Session.Undo(f.Session.Author);
        Assert.Empty(f.Model.OfClass(IfcClass.Beam));
    }

    /// <summary>
    /// The protocol: each packet's count, read and imported; a packet holding fewer items than it announces said; the members
    /// left out grouped by why (twice in the file, shorter than a millimetre), with their numbers and lines; a profile no
    /// library has, a hole naming no plate, loads and a packet SDNF does not define, said.
    /// </summary>
    [Fact]
    public void TheProtocolCountsEachPacketAndSaysWhatWasLeftOutAndWhy()
    {
        var (text, members, plates, holes) = Frame(3, 2, 1, (o, next) =>
        {
            Member(o, next(), "beam", "IPE 300", 8, (0, 0, 1), (0, 0, 2000), (0, 0, 2000.0004));
            Member(o, next(), "beam", "W12X50", 8, (0, 0, 1), (0, 1000, 2000), (3000, 1000, 2000));
            next();
            Member(o, 1, "column", "HEB 300", 5, (0, 1, 0), (0, 0, 0), (0, 0, 4000));
        });
        // The members' packet announces one more than it holds; a hole names a plate the file does not have; loads; packet 70.
        text = text.Replace($"\"millimeters\" {members}\n", $"\"millimeters\" {members + 1}\n");
        text = text.Replace($"\"millimeters\" \"millimeters\" {holes}\n", $"\"millimeters\" \"millimeters\" {holes + 1}\n777 999\n2 20 1\n0 0 0 5\n0 0 -20 5\n0 0 0 0\n0 0 0\n");
        text += "Packet 30\n0\nPacket 70\n1 2 3\n";
        var f = F();
        var r = Import(f, text);

        var read = members; // the duplicate is read, then left out
        Assert.Equal(read - 2, (int)r["made"]!);
        Assert.Equal(2, (int)r["skipped"]!);
        var packets = ((JsonArray)r["packets"]!).Select(x => (JsonObject)x!).ToList();
        Assert.Equal(["10", "20", "22", "30", "70"], packets.Select(x => (string)x["packet"]!));
        var p10 = packets[0];
        Assert.Equal(members + 1, (int)p10["announced"]!);
        Assert.Equal(read, (int)p10["read"]!);
        Assert.Equal(read - 2, (int)p10["imported"]!);
        Assert.Equal(11, (int)p10["line"]!);
        Assert.Equal(holes, (int)packets[2]["imported"]!);

        var skips = ((JsonArray)r["skips"]!).Select(x => (JsonObject)x!).ToDictionary(x => (string)x["reason"]!);
        var twice = skips["members in the file twice (the second left out)"];
        Assert.Equal(1, (int)twice["count"]!);
        var at = text.Split('\n').ToList().FindLastIndex(l => l.StartsWith("1 5 0 0 \"column\"")) + 1;
        Assert.Equal($"member 1 (line {at})", (string)((JsonArray)twice["items"]!)[0]!);
        Assert.Equal(1, (int)skips["members shorter than a millimetre"]["count"]!);
        Assert.Equal(1, (int)skips["holes naming a plate or member the file does not have"]["count"]!);
        Assert.StartsWith("hole 777 in 999 (line ", (string)((JsonArray)skips["holes naming a plate or member the file does not have"]["items"]!)[0]!);

        var notes = ((JsonArray)r["notes"]!).Select(x => (string)x!).ToList();
        Assert.Contains(notes, x => x.Contains($"announces {members + 1} members but holds {read}"));
        Assert.Contains(notes, x => x.Contains("packet 30") && x.Contains("loads"));
        Assert.Contains(notes, x => x.Contains("Packet 70") && x.Contains("not one SDNF defines"));
        Assert.Contains(notes, x => x.Contains("W12X50"));
        Assert.Contains(notes, x => x.StartsWith("Left out, members shorter than a millimetre: 1 — member "));
        Assert.True((long)r["ms"]! >= 0);
        Assert.True((int)r["lines"]! > 0);
    }

    /// <summary>A record that cannot be read stops the import with its line, packet and item; nothing of the file comes in.</summary>
    [Fact]
    public void AMalformedRecordIsToldWithItsLine()
    {
        var (text, _, _, _) = Frame(2, 1, 1);
        var lines = text.Split('\n');
        // The second member's third record: its start's x a word.
        var second = Array.FindIndex(lines, l => l.StartsWith("2 5 0 0 \"column\""));
        var bad = second + 2;
        var fields = lines[bad].Split(' ');
        fields[3] = "x12";
        lines[bad] = string.Join(' ', fields);
        var f = F();
        var before = f.Model.Entities.Count();
        var ex = Assert.Throws<CommandException>(() => Import(f, string.Join('\n', lines)));
        Assert.Contains($"Line {bad + 1}, packet 10, member 2 of ", ex.Message);
        Assert.Contains("start's x is „x12”, not a number", ex.Message);
        Assert.Equal(before, f.Model.Entities.Count());

        // A plate packet ending before its last record: the line where it ends.
        var (plated, _, _, _) = Frame(1, 1, 1);
        var cut = plated[..plated.IndexOf("Packet 22", StringComparison.Ordinal)].TrimEnd('\n').Split('\n');
        var shorter = string.Join('\n', cut[..^7]);
        var ex2 = Assert.Throws<CommandException>(() => Import(F(), shorter));
        Assert.Contains($"Line {cut.Length - 7}, packet 20", ex2.Message);
        Assert.Contains("ends before its last record", ex2.Message);

        // The reader says the same, by its line and packet.
        var fe = Assert.Throws<SdnfFormatException>(() => SdnfReader.Read(string.Join('\n', lines)));
        Assert.Equal(bad + 1, fe.Line);
        Assert.Equal("10", fe.Packet);
    }

    /// <summary>Stopped (cancel.long sets the session's token): nothing of the file comes in, and the reason says so.</summary>
    [Fact]
    public void ACancelledImportLeavesTheProjectAsItWas()
    {
        var (text, _, _, _) = Frame(4, 4, 1);
        var f = F();
        var before = f.Model.Entities.Count();
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        f.Session.Cancellation = stop.Token;
        var ex = Assert.Throws<CommandException>(() => Import(f, text));
        Assert.StartsWith("Cancelled", ex.Message);
        Assert.Equal(before, f.Model.Entities.Count());
        f.Session.Cancellation = default;
        Assert.Equal(16, (int)Import(f, text)["byClass"]![IfcClass.Column]!);
    }
}
