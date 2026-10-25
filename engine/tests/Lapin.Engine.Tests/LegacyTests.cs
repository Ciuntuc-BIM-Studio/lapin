using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Host;
using Lapin.Protocol;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Old Lapin projects (format 2) open: the user's test model (two storeys,
/// three walls of a layered type, a floor, a column) rebuilt natively.
/// </summary>
public class LegacyTests
{
    static string Old => Path.Combine(AppContext.BaseDirectory, "Fixtures", "old-format2.lapin");

    [Fact]
    public void An_old_package_is_told_from_format_3_and_read_as_rows()
    {
        Assert.True(LegacyPackage.Is(Old));
        var old = LegacyPackage.Read(Old);
        Assert.Equal(["Ground Floor", "Level 1"], old.Storeys.Select(s => (string?)s["name"]));
        Assert.Equal(3, old.Of("wall").Count());
        Assert.Single(old.Of("slab"));
        Assert.Single(old.Of("column"));
        Assert.Equal("wall", (string?)old.Types.Single()["kind"]);
    }

    [Fact]
    public void An_old_project_opens_as_a_native_one_unsaved_and_without_undo()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new DraftingModule());
        var r = LegacyImport.Run(f.Session, LegacyPackage.Read(Old));
        var m = f.Model;
        Assert.Equal([("Ground Floor", 0.0, 3.0), ("Level 1", 3.0, 3.0)], Spatial.Storeys(m).Select(s => (s.Name, s.Num("elevation"), s.Num("height"))));
        var walls = m.OfClass(IfcClass.Wall).ToList();
        Assert.Equal(3, walls.Count);
        // The layered type: render 20, brick 250 (the core), plaster 15.
        var type = Walls.TypeOf(m, walls[0].Id)!;
        Assert.Equal("Wall WT1", type.Name);
        Assert.Equal(0.285, Walls.Section(m, walls[0]).Sum(b => b.To - b.From), 9);
        Assert.Equal("Brick", m.Get(Walls.Section(m, walls[0]).Single(b => b.Core).Material!)!.Name);
        // The walls meet: joined at their corners, as drawn now.
        Assert.Contains(m.Relations, x => x.Class == Joins.Class);
        Assert.Single(m.OfClass(IfcClass.Slab));
        Assert.Single(m.OfClass(IfcClass.Column));
        Assert.Equal(0.4, Lapin.Structure.Section.From(m.OfClass(IfcClass.ColumnType).Single().Attrs["section"])!.Width, 9);
        Assert.False(f.Session.History.CanUndo);
        Assert.Null(f.Session.Path);
        Assert.All(r.Notes, n => Assert.DoesNotContain("could not", n));
    }

    [Fact]
    public void Its_sections_come_on_their_line_and_its_sheets_with_their_views()
    {
        // The fixture with a section drawn on a plan and a sheet that shows it, as the old app wrote them.
        var path = Path.Combine(Path.GetTempPath(), $"lapin-old-{Guid.NewGuid():N}.lapin");
        File.Copy(Old, path);
        try
        {
            using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update))
            {
                JsonObject Read(string n) { using var r = new StreamReader(zip.GetEntry(n)!.Open()); return (JsonObject)JsonNode.Parse(r.ReadToEnd())!; }
                void Write(string n, JsonNode j) { zip.GetEntry(n)?.Delete(); using var w = new StreamWriter(zip.CreateEntry(n).Open()); w.Write(j.ToJsonString()); }
                var views = Read("project/views.json");
                views["views"]!.AsArray().Add(new JsonObject
                {
                    ["id"] = "view-sec", ["kind"] = "section", ["name"] = "A-A",
                    ["plane"] = new JsonObject { ["axis"] = "front", ["offset"] = 0, ["flipped"] = false },
                    ["marker"] = new JsonObject { ["a"] = new JsonArray(1, 0, -2), ["b"] = new JsonArray(6, 0, -2) },
                });
                Write("project/views.json", views);
                Write("project/sheets.json", new JsonObject { ["sheets"] = new JsonArray(new JsonObject
                {
                    ["id"] = "s1", ["number"] = "A-201", ["name"] = "Sections", ["paper"] = "A3", ["orientation"] = "landscape",
                    ["titleBlock"] = new JsonObject { ["drawn"] = "I.C." },
                    ["viewports"] = new JsonArray(new JsonObject { ["id"] = "vp1", ["viewId"] = "view-sec", ["x"] = 150, ["y"] = 120, ["scale"] = 50, ["width"] = 0, ["height"] = 0, ["center"] = null }),
                }) });
            }
            var f = new Fixture(new ArchitectureModule(), new Lapin.Structure.StructureModule());
            var r = LegacyImport.Run(f.Session, LegacyPackage.Read(path));
            var section = f.Model.OfClass(Views.Class).Single(v => v.Str("kind") == "section");
            Assert.Equal("A-A", section.Name);
            Assert.Equal([1.0, 2.0], section.Attrs["start"]!.AsArray().Select(x => (double)x!));
            Assert.Equal(50, section.Num("scale"));
            var sheet = f.Model.OfClass(Sheets.Class).Single();
            Assert.Equal("A-201", sheet.Str("number"));
            Assert.Equal("I.C.", (string?)sheet.Attrs["titleBlock"]!["drawn"]);
            Assert.Equal(section.Id, Sheets.Viewports(sheet).Single().ViewId);
            Assert.Contains(r.Notes, n => n.Contains("1 sheet(s)"));
        }
        finally { File.Delete(path); }
    }
}
