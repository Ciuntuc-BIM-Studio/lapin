using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>An imported DXF shown in the 3D view on its view's plane (a tester's report #106, decision 382).</summary>
public class Underlays3dTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static string Dxf(string entities) => string.Join("\n", new[]
    {
        "0", "SECTION", "2", "HEADER", "9", "$INSUNITS", "70", "4", "0", "ENDSEC",
        "0", "SECTION", "2", "ENTITIES", entities, "0", "ENDSEC", "0", "EOF",
    });

    const string LineAndSolid = "0\nLINE\n8\nWALLS\n10\n0\n20\n0\n11\n1000\n21\n500\n0\nSOLID\n8\nFILL\n10\n0\n20\n0\n11\n1000\n21\n0\n12\n0\n22\n1000\n13\n1000\n23\n1000";

    static JsonObject Import(Fixture f, string viewId, string entities, Action<JsonObject>? more = null)
    {
        var a = new JsonObject { ["viewId"] = viewId, ["name"] = "site.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Dxf(entities))) };
        more?.Invoke(a);
        return (JsonObject)f.Run("drafting.importDxf", a)!;
    }

    static List<JsonObject> Batches(Fixture f) => ((JsonArray)f.Session.Query("drafting.underlays3d", [])!).OfType<JsonObject>().ToList();

    static float[] Floats(JsonNode? n)
    {
        var b = Convert.FromBase64String((string)n!);
        var o = new float[b.Length / 4];
        for (var i = 0; i < o.Length; i++) o[i] = BitConverter.ToSingle(b, i * 4);
        return o;
    }

    [Fact]
    public void A_dxf_on_a_plan_lies_at_its_storey_level_by_layer_with_its_hatches_as_fills()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var z = f.Model.Get(f.Storey)!.Num("elevation");
        var r = Import(f, plan, LineAndSolid);
        var importId = (string)r["importId"]!;
        // Recorded on the project, each entity it made named by it, its hatch marked as the file's.
        Assert.Equal("site.dxf", (string?)Underlays3d.Imports(f.Model)[importId]!["file"]);
        Assert.All(Drafts.On(f.Model, plan), e => Assert.Equal(importId, e.Str(Underlays3d.ImportKey)));
        Assert.All(Drafts.On(f.Model, plan), e => Assert.True(e.Bool("dxf")));

        var batches = Batches(f);
        Assert.Equal(2, batches.Count);
        var walls = batches.Single(b => (string?)b["layer"] == "WALLS");
        Assert.Equal(plan, (string?)walls["viewId"]);
        Assert.Equal(importId, (string?)walls["source"]);
        Assert.Equal(f.Storey, (string?)walls["storeyId"]);
        var p = Floats(walls["lines"]);
        Assert.Equal(new[] { 0f, 0f, (float)z, 1f, 0.5f, (float)z }, p);
        Assert.Equal(6, Convert.FromBase64String((string)walls["lineColors"]!).Length);
        // The SOLID: a square in two triangles, its outline drawn by no line of its own.
        var fill = batches.Single(b => (string?)b["layer"] == "FILL");
        Assert.Equal(2, (int)fill["triangles"]!);
        Assert.Equal(0, (int)fill["segments"]!);
        Assert.All(Floats(fill["fills"]).Where((_, i) => i % 3 == 2), v => Assert.Equal((float)z, v));

        // A layer turned off stays off in 3D.
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "WALLS", ["visible"] = false });
        Assert.DoesNotContain(Batches(f), b => (string?)b["layer"] == "WALLS");
        f.Run("drafting.setLayer", new JsonObject { ["name"] = "WALLS", ["visible"] = true });

        // The import turned off in 3D: none of it; on again, all of it; one undo step each.
        f.Run("drafting.setUnderlay3d", new JsonObject { ["id"] = importId, ["shown"] = false });
        Assert.Empty(Batches(f));
        f.Session.Undo(f.Session.Author);
        Assert.Equal(2, Batches(f).Count);
        Assert.Throws<CommandException>(() => f.Run("drafting.setUnderlay3d", new JsonObject { ["id"] = "nope", ["shown"] = false }));
    }

    [Fact]
    public void A_dxf_on_a_section_lies_on_its_cut_plane_and_lines_with_heights_stay_elements()
    {
        var f = F();
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(0, 2), ["end"] = new JsonArray(10, 2) })!["id"]!;
        Import(f, section, "0\nLINE\n8\n0\n10\n0\n20\n0\n11\n1000\n21\n500");
        var b = Assert.Single(Batches(f));
        var p = Floats(b["lines"]);
        // x along the section line, y the height, on the plane through its line (y = 2).
        Assert.Equal(2, p[1], 5); Assert.Equal(2, p[4], 5);
        Assert.Equal(1, Math.Abs(p[3] - p[0]), 5);
        Assert.Equal(0.5, p[5] - p[2], 5);
        var n = b["normal"]!.AsArray();
        Assert.Equal(1, Math.Abs((double)n[1]!), 6);

        // A 3D line (heights from the file) is already an element in space: not drawn twice.
        var g = F();
        var plan = (string)g.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = g.Storey })!["id"]!;
        Import(g, plan, "0\nLINE\n8\n0\n10\n0\n20\n0\n30\n100\n11\n1000\n21\n0\n31\n200");
        Assert.Empty(Batches(g));
        Assert.IsType<JsonArray>(Drafts.On(g.Model, plan).Single().Attrs["heights"]);
    }

    [Fact]
    public void A_linked_dxf_follows_its_link_shown_halftone_and_its_3d_switch()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var r = Import(f, plan, "0\nLINE\n8\n0\n10\n0\n20\n0\n11\n1000\n21\n0", a => a["link"] = true);
        var linkId = (string)r["linkId"]!;
        Assert.Null(r["importId"]);
        var b = Assert.Single(Batches(f));
        Assert.Equal(linkId, (string?)b["source"]);
        f.Run("link.update", new JsonObject { ["id"] = linkId, ["halftone"] = true });
        var grey = Underlays3d.Rgb(Links.Halftone);
        Assert.Equal(new byte[] { grey.R, grey.G, grey.B, grey.R, grey.G, grey.B }, Convert.FromBase64String((string)Assert.Single(Batches(f))["lineColors"]!));
        f.Run("link.update", new JsonObject { ["id"] = linkId, ["in3d"] = false });
        Assert.Empty(Batches(f));
        Assert.False((bool)((JsonArray)f.Session.Query("link.list", [])!)[0]!["in3d"]!);
        f.Run("drafting.setUnderlay3d", new JsonObject { ["id"] = linkId, ["shown"] = true });
        Assert.Single(Batches(f));
        f.Run("link.update", new JsonObject { ["id"] = linkId, ["visible"] = false });
        Assert.Empty(Batches(f));
    }

    [Fact]
    public void Tens_of_thousands_of_lines_come_in_one_batch_quickly()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var sb = new StringBuilder();
        for (var i = 0; i < 20000; i++) sb.Append($"0\nLINE\n8\nGRID\n10\n{i}\n20\n0\n11\n{i}\n21\n1000\n");
        Import(f, plan, sb.ToString().TrimEnd('\n'));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var b = Assert.Single(Batches(f));
        watch.Stop();
        Assert.Equal(20000, (int)b["segments"]!);
        Assert.True(watch.ElapsedMilliseconds < 3000, $"{watch.ElapsedMilliseconds} ms");
    }
}
