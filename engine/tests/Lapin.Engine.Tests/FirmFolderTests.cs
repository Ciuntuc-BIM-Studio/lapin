using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;

namespace Lapin.Engine.Tests;

/// <summary>A firm's folder for every kind of library (docs/plan-user-extensibility.md §4.2): written and read back by kind, other files left out, a guest never reaching it.</summary>
public sealed class FirmFolderTests
{
    [Fact]
    public void A_firm_s_folder_holds_every_kind_of_library_and_lists_them_by_kind()
    {
        var dir = Directory.CreateTempSubdirectory("lapin-firm-").FullName;
        try
        {
            var f = new Fixture(new ArchitectureModule(), new DraftingModule());
            var d = new Lapin.Protocol.EngineDispatcher(f.Session, Lapin.Host.PackageFiles.Create());
            JsonNode Call(string method, JsonObject p)
            {
                var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString());
                var o = JsonNode.Parse(Lapin.Protocol.BinaryFrame.HeaderText(reply))!;
                return o["error"] is { } e ? throw new Exception(e.ToJsonString()) : o["result"]!;
            }
            var symbols = (string)f.Session.Query("symbols.export", new JsonObject { ["ids"] = new JsonArray(Lapin.Platform.BuiltinSymbols.Prefix + "north-arrow") })!["text"]!;
            var written = (string)Call("library.writeFolder", new JsonObject { ["path"] = dir, ["name"] = "Office: arrows", ["kind"] = "lapinsymbols", ["text"] = symbols })["file"]!;
            Assert.EndsWith("Office_ arrows.lapinsymbols", written);
            Call("library.writeFolder", new JsonObject { ["path"] = dir, ["name"] = "office", ["kind"] = "lin", ["text"] = "*BORDER,b\nA,5,-2\n" });
            File.WriteAllText(Path.Combine(dir, "notes.txt"), "not a library");
            var all = Call("library.readFolder", new JsonObject { ["path"] = dir })["files"]!.AsArray();
            Assert.Equal(["lapinsymbols", "lin"], all.Select(x => (string)x!["kind"]!).Order().ToArray());
            var only = Call("library.readFolder", new JsonObject { ["path"] = dir, ["kinds"] = new JsonArray("lapinsymbols") })["files"]!.AsArray();
            Assert.Equal(symbols, (string)Assert.Single(only)!["text"]!);
            Assert.Throws<Exception>(() => Call("library.writeFolder", new JsonObject { ["path"] = dir, ["name"] = "x", ["kind"] = "exe", ["text"] = "" }));
            Assert.NotNull(Lapin.Protocol.EngineDispatcher.AllowedRemote("library.readFolder", new JsonObject { ["path"] = dir }, "editor"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
