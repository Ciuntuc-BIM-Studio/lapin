using System.Text.Json.Nodes;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Scripting;

namespace Lapin.Engine.Tests;

/// <summary>
/// The helpers the audit of 22.11.2026 made of copies: one rule for file names (the whole Windows set, on every system),
/// one atomic write, one SHA-256 spelling and one script trust hash; and quantities.many saying when it is asked too much.
/// </summary>
public sealed class SharedHelpersTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-helpers-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    [Fact]
    public void A_file_name_keeps_nothing_windows_refuses_on_any_system()
    {
        Assert.Equal("a_b_c_d_e_f_g_h_i_j", FileNames.Stem("a<b>c:d\"e/f\\g|h?i*j", "x"));
        Assert.Equal("Plan.dxf", FileNames.Of("  Plan  ", "dxf", "x"));
        Assert.Equal("Draft", FileNames.Stem("Draft. . ", "x"));
        Assert.Equal("x", FileNames.Stem(" \t ", "x"));
        Assert.Equal("a_b", FileNames.Stem("a\u0001b", "x"));
        Assert.Equal("con_", FileNames.Stem("con", "x"));
        Assert.Equal("nul_.txt", FileNames.Of("nul", "txt", "x"));
        Assert.Equal("ab", FileNames.Stem("a*b", "x", replacement: ""));
        Assert.Equal(120, FileNames.Stem(new string('a', 300), "x").Length);
        // The callers that let "*?<>| through on a Mac before.
        Assert.Equal("Schedule_1_.lapinschedule", FileNames.Of("Schedule<1>", "lapinschedule", "schedule"));
        Assert.Equal("A_B_.pdf", Lapin.Platform.Documents.DocumentExport.FileName("A?B*", "pdf"));
    }

    [Fact]
    public void An_atomic_write_replaces_the_file_whole_and_a_failed_one_leaves_it_as_it_was()
    {
        var path = Path.Combine(_dir, "a.json");
        AtomicFile.WriteText(path, "one");
        AtomicFile.WriteText(path, "two");
        Assert.Equal("two", File.ReadAllText(path));
        Assert.Throws<InvalidOperationException>(() => AtomicFile.Write(path, s => { s.Write("half"u8); throw new InvalidOperationException("disk full"); }));
        Assert.Equal("two", File.ReadAllText(path));
        Assert.Equal(["a.json"], Directory.GetFiles(_dir).Select(f => Path.GetFileName(f)!));
    }

    [Fact]
    public void One_sha256_spelling_and_one_script_trust_hash()
    {
        const string code = "print('hello')\n";
        var hex = Hashes.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(code));
        Assert.Matches("^[0-9a-f]{64}$", hex);
        Assert.Equal(hex, Hashes.Sha256Hex(code));
        Assert.Equal(hex, Hashes.Script(code));
        Assert.Equal(hex, ScriptHost.HashOf(code));
        Assert.Equal(hex, ScriptingModule.HashOf(code));
        Assert.Equal(hex, Hashes.Sha256Hex(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(code))));
    }

    [Fact]
    public void Quantities_many_says_when_it_is_asked_more_than_it_answers()
    {
        var f = new Fixture();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var ids = new JsonArray(Enumerable.Range(0, EngineDispatcher.MaxQuantityIds + 1).Select(i => (JsonNode)$"id{i}").ToArray());
        var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = "quantities.many", ["params"] = new JsonObject { ["ids"] = ids } }.ToJsonString());
        var r = JsonNode.Parse(BinaryFrame.HeaderText(reply))!.AsObject();
        Assert.Contains("at most 5000", (string?)r["error"]);
        var (ok, _) = d.Handle(new JsonObject { ["id"] = 2, ["method"] = "quantities.many", ["params"] = new JsonObject { ["ids"] = new JsonArray("nothing") } }.ToJsonString());
        Assert.Null((string?)JsonNode.Parse(BinaryFrame.HeaderText(ok))!["error"]);
    }
}
