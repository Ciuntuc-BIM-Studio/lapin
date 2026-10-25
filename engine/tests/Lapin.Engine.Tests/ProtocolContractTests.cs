using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Protocol;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// The frames a trimmed engine (the browser's) writes, with the generated
/// contracts alone, are byte for byte the desktop's: every reply and diff of
/// a session that draws, measures, queries and exports.
/// </summary>
public class ProtocolContractTests
{
    [Fact]
    public void A_session_s_frames_come_out_the_same_by_the_generated_contracts_alone()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule());
        var g = new Fixture(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule());
        var desktop = new EngineDispatcher(f.Session, PackageFiles.Create());
        var trimmed = new EngineDispatcher(g.Session, PackageFiles.Create()) { ContractsOnly = true };
        string[] calls =
        [
            """{"id":1,"method":"session.info"}""",
            """{"id":2,"method":"doc.execute","params":{"command":"architecture.addWall","args":{"x1":0,"y1":0,"x2":5,"y2":0}}}""",
            """{"id":3,"method":"doc.execute","params":{"command":"architecture.addWall","args":{"x1":5,"y1":0,"x2":5,"y2":4,"arc":1}}}""",
            """{"id":4,"method":"doc.execute","params":{"command":"structure.addBeam","args":{"x1":0,"y1":2,"x2":5,"y2":2}}}""",
            """{"id":5,"method":"doc.query","params":{"name":"cutOrder.list"}}""",
            """{"id":6,"method":"doc.snapshot"}""",
            """{"id":7,"method":"doc.undo"}""",
            """{"id":8,"method":"recovery.list"}""",
            """{"id":9,"method":"doc.execute","params":{"command":"architecture.flipWall","args":{"id":"nope"}}}""",
            """{"id":10,"method":"project.exportIfc"}""",
        ];
        foreach (var call in calls)
        {
            var (a, da) = desktop.Handle(call);
            var (b, db) = trimmed.Handle(call);
            // The ids the two sessions make differ: compare with them taken out.
            Assert.Equal(Normalise(a, f, g), Normalise(b, f, g));
            Assert.Equal(da == null, db == null);
            if (da != null) Assert.Equal(Normalise(desktop.DiffFrame(da), f, g), Normalise(trimmed.DiffFrame(db!), f, g));
        }
        // The quantities of an element, by the trimmed dispatcher.
        var wall = g.Model.OfClass("IfcWall").First().Id;
        var (q, _) = trimmed.Handle(new JsonObject { ["id"] = 11, ["method"] = "quantities.of", ["params"] = new JsonObject { ["id"] = wall } }.ToJsonString());
        Assert.Contains("Qto_WallBaseQuantities", BinaryFrame.HeaderText(q));
    }

    /// <summary>The header as text, the ids and the dates a session makes for itself taken out; the buffers as they are.</summary>
    static string Normalise(byte[] frame, Fixture f, Fixture g)
    {
        var header = BinaryFrame.HeaderText(frame);
        header = System.Text.RegularExpressions.Regex.Replace(header, "\"[0-9A-Za-z_$]{22}\"", "\"<id>\"");
        header = System.Text.RegularExpressions.Regex.Replace(header, "\\d{4}-\\d{2}-\\d{2}T[0-9:.]+Z?", "<date>");
        // An exported file names the temporary file it was written to.
        header = System.Text.RegularExpressions.Regex.Replace(header, "\"data\":\"[^\"]*\"", "\"data\":\"<file>\"");
        var (_, buffers) = BinaryFrame.Decode(frame);
        return header + "|" + Convert.ToBase64String(buffers.ToArray());
    }
}
