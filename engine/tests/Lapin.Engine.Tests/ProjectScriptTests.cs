using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Scripting;

namespace Lapin.Engine.Tests;

/// <summary>
/// A project's Python scripts (docs/plan-python-scripting.md, stage 3; decision 489): LapinScript entities saved, renamed
/// and deleted by the scripting module's commands, one undo step each; written as scripts/&lt;name&gt; in the .lapin beside
/// the model and read back; other tools' files in scripts/ kept; an edition without the module keeps them as they came;
/// a guest never puts one in.
/// </summary>
public sealed class ProjectScriptTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-project-scripts-").FullName;
    string PathOf(string name) => Path.Combine(_dir, name);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    const string Code = "for x in range(3):\n    lapin.architecture.add_wall(x1=x, y1=0, x2=x, y2=3)\n";

    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new ScriptingModule());
        f.Run("storey.create", []);
        return f;
    }

    static JsonObject Save(Fixture f, string name, string code, string? id = null)
    {
        var a = new JsonObject { ["name"] = name, ["code"] = code };
        if (id != null) a["id"] = id;
        return f.Run("scripting.save", a)!.AsObject();
    }

    static IEnumerable<Entity> Scripts(Fixture f) => f.Model.OfClass(ScriptingModule.Class);

    static Dictionary<string, byte[]> Entries(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.ToDictionary(e => e.FullName, e => { using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); });
    }

    static JsonObject ManifestOf(string path) => (JsonObject)JsonNode.Parse(Entries(path)["manifest.json"])!;

    [Fact]
    public void Saving_renaming_and_deleting_a_script_are_one_undo_step_each()
    {
        var f = Project();
        var r = Save(f, "Room walls", Code);
        Assert.True((bool)r["created"]!);
        Assert.Equal("Room walls.py", (string)r["name"]!);   // .py added
        Assert.Equal(ScriptingModule.HashOf(Code), (string)r["sha256"]!);
        Assert.Equal(ScriptHost.HashOf(Code), (string)r["sha256"]!);   // the same hash the host keeps trust by
        Assert.Equal("Save script", f.Session.History.UndoLabel);
        var id = (string)r["id"]!;
        var e = Assert.Single(Scripts(f));
        Assert.Equal(Code, e.Str("code"));
        Assert.Equal("python", e.Str("language"));

        // Its code changed, by id: the same script.
        Save(f, "Room walls.py", Code + "print('done')\n", id);
        Assert.Equal(Code + "print('done')\n", Assert.Single(Scripts(f)).Str("code"));
        Assert.Equal(ScriptingModule.HashOf(Code + "print('done')\n"), Assert.Single(Scripts(f)).Str("sha256"));
        f.Session.Undo();
        Assert.Equal(Code, Assert.Single(Scripts(f)).Str("code"));

        f.Run("scripting.rename", new JsonObject { ["id"] = id, ["name"] = "Walls" });
        Assert.Equal("Walls.py", Assert.Single(Scripts(f)).Name);
        Assert.Equal("Rename script", f.Session.History.UndoLabel);

        f.Run("scripting.delete", new JsonObject { ["id"] = id });
        Assert.Empty(Scripts(f));
        Assert.Equal("Delete script", f.Session.History.UndoLabel);
        f.Session.Undo();
        Assert.Equal("Walls.py", Assert.Single(Scripts(f)).Name);
        f.Session.Undo();
        f.Session.Undo();
        Assert.Empty(Scripts(f));
        f.Session.Redo();
        Assert.Equal("Room walls.py", Assert.Single(Scripts(f)).Name);
    }

    [Fact]
    public void A_scripts_name_is_a_file_name_and_never_taken_twice_whatever_its_case()
    {
        var f = Project();
        var id = (string)Save(f, "walls.py", Code)["id"]!;
        Assert.Contains("already", Assert.Throws<CommandException>(() => Save(f, "WALLS.py", "x = 1")).Message);
        foreach (var bad in new[] { "a/b.py", "..py", ".hidden.py", "", "x\\y" })
            Assert.Throws<CommandException>(() => Save(f, bad, "x = 1"));
        var other = (string)Save(f, "other", "x = 1")["id"]!;
        Assert.Throws<CommandException>(() => f.Run("scripting.rename", new JsonObject { ["id"] = other, ["name"] = "Walls.PY" }));
        // The queries: the list without the code, one script with it.
        var list = f.Session.Query("scripting.list", [])!.AsArray();
        Assert.Equal(["other.py", "walls.py"], list.Select(x => (string)x!["name"]!).ToArray());
        Assert.Null(list[0]!["code"]);
        Assert.Equal(Code, (string)f.Session.Query("scripting.get", new JsonObject { ["id"] = id })!["code"]!);
        Assert.Equal(Code, (string)f.Session.Query("scripting.get", new JsonObject { ["name"] = "WALLS.py" })!["code"]!);
    }

    [Fact]
    public void A_script_changes_through_its_own_commands_only()
    {
        var f = Project();
        var id = (string)Save(f, "walls.py", Code)["id"]!;
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["code"] = "import os" } }));
        Assert.Throws<CommandException>(() => f.Run("element.rename", new JsonObject { ["id"] = id, ["name"] = "x.py" }));
        Assert.Equal(Code, Assert.Single(Scripts(f)).Str("code"));
        Assert.Equal(ScriptingModule.HashOf(Code), Assert.Single(Scripts(f)).Str("sha256"));
        // Deleted like any content (Undo brings it back).
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Empty(Scripts(f));
    }

    [Fact]
    public void A_scripts_text_is_in_the_package_as_scripts_name_and_comes_back_with_it()
    {
        var f = Project();
        Save(f, "Room walls.py", Code);
        Save(f, "ă î.py", "print('ș')\n");
        var path = PathOf("house.lapin");
        PackageFiles.Save(f.Session, path);

        var entries = Entries(path);
        Assert.Equal(Code, Encoding.UTF8.GetString(entries["scripts/Room walls.py"]));
        Assert.Equal("print('ș')\n", Encoding.UTF8.GetString(entries["scripts/ă î.py"]));
        // Listed as the module's own parts, with their hashes; not as another tool's.
        var parts = ManifestOf(path)["parts"]!.AsArray();
        var part = parts.Single(p => (string?)p!["path"] == "scripts/Room walls.py")!;
        Assert.Equal("scripting", (string?)part["module"]);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(entries["scripts/Room walls.py"])), (string?)part["sha256"]);
        Assert.False(ManifestOf(path).ContainsKey("foreign"));

        var g = Project();
        PackageFiles.Open(g.Session, path);
        Assert.Equal(["Room walls.py", "ă î.py"], Scripts(g).Select(e => e.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(Code, Scripts(g).Single(e => e.Name == "Room walls.py").Str("code"));
        // A script renamed: its old file goes with the next save.
        g.Run("scripting.rename", new JsonObject { ["id"] = Scripts(g).Single(e => e.Name == "Room walls.py").Id, ["name"] = "Walls.py" });
        PackageFiles.Save(g.Session, path);
        Assert.DoesNotContain("scripts/Room walls.py", Entries(path).Keys);
        Assert.Equal(Code, Encoding.UTF8.GetString(Entries(path)["scripts/Walls.py"]));
    }

    [Fact]
    public void Another_tools_files_in_scripts_are_kept_beside_the_projects_and_a_script_of_the_same_name_is_lapins()
    {
        var f = Project();
        Save(f, "walls.py", Code);
        var path = PathOf("tooled.lapin");
        var files = PackageFiles.Create();
        files.Save(f.Session, path);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            void Add(string name, string text) { using var s = zip.CreateEntry(name).Open(); s.Write(Encoding.UTF8.GetBytes(text)); }
            Add("scripts/tool.py", "print('a tool')\n");
            Add("scripts/notes.txt", "kept");
        }
        // Read back: the tool's files are foreign, Lapin's own script is not.
        var foreign = PackageForeign.Of(path);
        Assert.Equal(["scripts/notes.txt", "scripts/tool.py"], foreign.Parts.Select(p => p.Path).ToArray());
        Assert.Equal(["scripts/walls.py"], foreign.ModuleParts.Select(p => p.Path).ToArray());

        var g = Project();
        files.Open(g.Session, path);
        Assert.Single(Scripts(g));   // the tool's file is no script of the project's
        // A project script named as the tool's file: Lapin's own is what the package holds from now on.
        Save(g, "tool.py", "print('the project')\n");
        files.Save(g.Session, null);
        var entries = Entries(path);
        Assert.Equal("print('the project')\n", Encoding.UTF8.GetString(entries["scripts/tool.py"]));
        Assert.Equal("kept", Encoding.UTF8.GetString(entries["scripts/notes.txt"]));
        Assert.Equal(["scripts/notes.txt"], ManifestOf(path)["foreign"]!.AsArray().Select(p => (string)p!["path"]!).ToArray());
    }

    [Fact]
    public void An_edition_without_the_module_keeps_the_scripts_and_their_files_as_they_came()
    {
        var f = Project();
        Save(f, "walls.py", Code);
        var path = PathOf("absent.lapin");
        var files = PackageFiles.Create();
        files.Save(f.Session, path);

        var without = new Fixture(new ArchitectureModule());
        var other = PackageFiles.Create();
        other.Open(without.Session, path);
        without.Wall(0, 0, 5, 0);
        other.Save(without.Session, null);
        var entries = Entries(path);
        Assert.Equal(Code, Encoding.UTF8.GetString(entries["scripts/walls.py"]));
        Assert.Equal("scripting", (string?)ManifestOf(path)["parts"]!.AsArray().Single(p => (string?)p!["path"] == "scripts/walls.py")!["module"]);
        Assert.Equal("scripting", LapinPackage.Read(path).Classes[ScriptingModule.Class]);

        // With the module again: the script as it was.
        var back = Project();
        PackageFiles.Open(back.Session, path);
        Assert.Equal(Code, Assert.Single(Scripts(back)).Str("code"));
    }

    [Fact]
    public void A_version_keeps_the_scripts()
    {
        var f = Project();
        var id = (string)Save(f, "walls.py", Code)["id"]!;
        var build = ProjectPackage.Build(f.Session);
        Assert.Contains(build.Manifest.Parts, p => p.Path == "scripts/walls.py" && p.Module == "scripting");
        Save(f, "walls.py", "x = 2\n", id);
        Assert.NotEqual(build.Manifest.Parts.Single(p => p.Path == "scripts/walls.py").Sha256, ProjectPackage.Build(f.Session).Manifest.Parts.Single(p => p.Path == "scripts/walls.py").Sha256);
    }

    [Fact]
    public void A_guest_never_puts_a_script_in_the_project_alone_or_in_a_batch()
    {
        var f = Project();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var guest = new Caller("guest-1", "editor");
        string? Error(string method, JsonObject p)
        {
            var (reply, _) = d.Handle(new JsonObject { ["id"] = 1, ["method"] = method, ["params"] = p }.ToJsonString(), guest);
            var h = BinaryFrame.Decode(reply).Header.RootElement;
            return h.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        var save = new JsonObject { ["command"] = "scripting.save", ["args"] = new JsonObject { ["name"] = "x.py", ["code"] = "import os" } };
        Assert.Equal("Only the host can do that.", Error("doc.execute", save));
        var batch = new JsonObject
        {
            ["command"] = "edit.batch",
            ["args"] = new JsonObject { ["steps"] = new JsonArray(new JsonObject { ["command"] = "scripting.save", ["args"] = new JsonObject { ["name"] = "x.py", ["code"] = "import os" } }) },
        };
        Assert.Equal("Only the host can do that.", Error("doc.execute", batch));
        foreach (var c in new[] { "scripting.rename", "scripting.delete" })
            Assert.Equal("Only the host can do that.", Error("doc.execute", new JsonObject { ["command"] = c, ["args"] = new JsonObject { ["id"] = "x" } }));
        Assert.Empty(Scripts(f));
        // A guest reads the project's scripts like the rest of the model.
        Assert.Null(Error("doc.query", new JsonObject { ["name"] = "scripting.list" }));
    }
}
