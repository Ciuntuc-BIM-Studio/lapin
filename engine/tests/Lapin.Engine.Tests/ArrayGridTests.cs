using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A grid array (forum 90): copies along two step vectors, columns × rows (and levels along a third), kept as an
/// arrangement like the other kinds; each item keeps its cell, so a new column, row or level count keeps the ids of every
/// cell still in the grid; one undo step each; saved in the package, never written to IFC.
/// </summary>
public class ArrayGridTests
{
    static JsonArray Ids(params string[] ids) => new(ids.Select(x => (JsonNode)x).ToArray());
    static Vec2 Start(Fixture f, string id) => Args.PointOf(f.Model.Get(id)!.Attrs["start"]);
    static void Near(Vec2 want, Vec2 got) => Assert.True((want - got).Length < 1e-6, $"want {want}, got {got}");
    static List<Arrays.Item> Items(Fixture f, string arr) => Arrays.Items(f.Model.Get(arr)!);
    static string At(Fixture f, string arr, int c, int r, int l = 0) => Items(f, arr).Single(i => i.Cell is { } k && k[0] == c && k[1] == r && k[2] == l).Ids.Single();

    static JsonObject Grid(string wall, int columns, int rows) => new()
    {
        ["ids"] = Ids(wall), ["kind"] = "grid", ["count"] = columns, ["rows"] = rows,
        ["step"] = new JsonArray(2, 0, 0), ["rowStep"] = new JsonArray(0, 3, 0),
    };

    [Fact]
    public void A_grid_puts_columns_along_one_step_and_rows_along_the_other()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 1);
        var r = f.Run("element.array", Grid(wall, 3, 2))!;
        var arr = (string)r["array"]!;
        Assert.Equal(5, r["ids"]!.AsArray().Count);
        Assert.Equal(6, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(wall, At(f, arr, 0, 0));
        Near(new Vec2(4, 0), Start(f, At(f, arr, 2, 0)));
        Near(new Vec2(2, 3), Start(f, At(f, arr, 1, 1)));
        Near(new Vec2(4, 3), Start(f, At(f, arr, 2, 1)));
        Assert.Equal("grid", (string)f.Model.Get(arr)!.Attrs["kind"]!);
        var of = f.Session.Query("array.of", new JsonObject { ["id"] = At(f, arr, 1, 1) })!;
        Assert.Equal(2, (int)of["rows"]!);
    }

    [Fact]
    public void More_or_fewer_columns_and_rows_keep_each_cell_s_ids_and_each_change_is_one_undo_step()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 1);
        var arr = (string)f.Run("element.array", Grid(wall, 3, 2))!["array"]!;
        var b10 = At(f, arr, 1, 0);
        var b11 = At(f, arr, 1, 1);
        var b21 = At(f, arr, 2, 1);

        // 3 → 4 columns: every cell already there keeps its id (the second row's too), one more on each row.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 4 });
        Assert.Equal(8, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Equal(b11, At(f, arr, 1, 1));
        Assert.Equal(b21, At(f, arr, 2, 1));
        Near(new Vec2(6, 3), Start(f, At(f, arr, 3, 1)));

        // 2 → 1 row: the second row deleted, the first row's ids kept.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["rows"] = 1 });
        Assert.Equal(4, f.Model.OfClass(IfcClass.Wall).Count());
        Assert.Null(f.Model.Get(b11));
        Assert.Equal(b10, At(f, arr, 1, 0));

        // A new row step moves nothing on the first row; back to 2 rows, they stand 5 m apart.
        f.Run("array.update", new JsonObject { ["id"] = arr, ["rows"] = 2, ["rowStep"] = new JsonArray(0, 5, 0) });
        Near(new Vec2(2, 5), Start(f, At(f, arr, 1, 1)));
        Near(new Vec2(2, 0), Start(f, b10));

        f.Session.Undo();
        Assert.Equal(4, f.Model.OfClass(IfcClass.Wall).Count());
        f.Session.Undo();
        Assert.Equal(b11, At(f, arr, 1, 1));
        f.Session.Undo();
        Assert.Equal(6, f.Model.OfClass(IfcClass.Wall).Count());
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        Assert.Empty(Arrays.All(f.Model));
    }

    [Fact]
    public void Levels_rise_by_the_third_step_and_a_row_turned_into_a_grid_keeps_its_copies()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 1);
        var g = Grid(wall, 2, 2);
        g["levels"] = 3;
        g["levelStep"] = new JsonArray(0, 0, 3);
        var arr = (string)f.Run("element.array", g)!["array"]!;
        Assert.Equal(12, Items(f, arr).Count);
        Assert.Equal(6, Items(f, arr).Single(i => i.Cell![2] == 2 && i.Cell[0] == 1 && i.Cell[1] == 0).At.Z, 9);

        // A row of three, made a grid of 3 × 2: its copies are the first row's.
        var other = f.Wall(20, 0, 20, 1);
        var row = (string)f.Run("element.array", new JsonObject { ["ids"] = Ids(other), ["count"] = 3, ["step"] = new JsonArray(2, 0, 0) })!["array"]!;
        var copies = Items(f, row).Skip(1).Select(i => i.Ids.Single()).ToList();
        f.Run("array.update", new JsonObject { ["id"] = row, ["kind"] = "grid", ["rows"] = 2, ["rowStep"] = new JsonArray(0, 3, 0) });
        Assert.Equal(copies[0], At(f, row, 1, 0));
        Assert.Equal(copies[1], At(f, row, 2, 0));
        Near(new Vec2(24, 3), Start(f, At(f, row, 2, 1)));
    }

    [Fact]
    public void A_grid_s_preview_and_its_checks()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 1);
        var p = f.Session.Query("array.preview", new JsonObject { ["kind"] = "grid", ["count"] = 3, ["rows"] = 2, ["step"] = new JsonArray(1, 0, 0), ["rowStep"] = new JsonArray(0, 1, 0) })!;
        Assert.Equal(6, (int)p["count"]!);
        Assert.Equal(1.0, (double)p["places"]![4]!["x"]!, 9);
        Assert.Equal(1.0, (double)p["places"]![4]!["y"]!, 9);
        Assert.Throws<CommandException>(() => f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["kind"] = "grid", ["count"] = 3, ["rows"] = 2, ["step"] = new JsonArray(1, 0, 0) }));
        Assert.Throws<CommandException>(() => f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["kind"] = "grid", ["count"] = 1, ["rows"] = 1, ["step"] = new JsonArray(1, 0, 0) }));
        Assert.Throws<CommandException>(() => f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["kind"] = "grid", ["count"] = 40, ["rows"] = 30, ["step"] = new JsonArray(1, 0, 0), ["rowStep"] = new JsonArray(0, 1, 0) }));
        // A single column of rows needs no column step.
        f.Run("element.array", new JsonObject { ["ids"] = Ids(wall), ["kind"] = "grid", ["count"] = 1, ["rows"] = 3, ["rowStep"] = new JsonArray(0, 2, 0) });
        Assert.Equal(3, f.Model.OfClass(IfcClass.Wall).Count());
    }

    [Fact]
    public void A_grid_is_kept_in_the_package_and_never_written_to_IFC()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 0, 1);
        var arr = (string)f.Run("element.array", Grid(wall, 3, 2))!["array"]!;
        var b21 = At(f, arr, 2, 1);
        using (var ms = new MemoryStream())
        {
            ProjectPackage.Write(f.Session, ms);
            ms.Position = 0;
            var content = LapinPackage.Read(ms);
            var g = new Fixture(new ArchitectureModule());
            g.Session.Load(content.Entities, content.Relations, null, content.Classes);
            Assert.Equal(b21, At(g, arr, 2, 1));
            g.Run("array.update", new JsonObject { ["id"] = arr, ["count"] = 4 });
            Assert.Equal(b21, At(g, arr, 2, 1));
            Assert.Equal(8, g.Model.OfClass(IfcClass.Wall).Count());
        }
        using (var ms = new MemoryStream())
        {
            IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
            var text = Encoding.UTF8.GetString(ms.ToArray());
            Assert.DoesNotContain(Arrays.Class, text);
            Assert.DoesNotContain("rowStep", text);
            Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(text, "IFCWALL\\(").Count);
        }
    }
}
