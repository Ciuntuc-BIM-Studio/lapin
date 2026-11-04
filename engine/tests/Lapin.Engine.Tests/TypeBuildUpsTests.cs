using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Schedules;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// The testers' reports #65–#67: the type assemblies schedule draws each type's build-up after its table (on screen,
/// on sheets and in the PDF, the same pages), at its scale, switched on and off; and a schedule's export written where
/// the desktop's save dialog says.
/// </summary>
public sealed class TypeBuildUpsTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-buildups-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    static Fixture F()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("architecture.createWallType", new JsonObject
        {
            ["name"] = "WT1 Exterior", ["mark"] = "WT1",
            ["layers"] = new JsonArray(
                new JsonObject { ["material"] = "Render", ["thickness"] = 0.015 },
                new JsonObject { ["material"] = "EPS insulation", ["thickness"] = 0.1, ["name"] = "Thermal insulation" },
                new JsonObject { ["material"] = "Brick", ["thickness"] = 0.25, ["core"] = true },
                new JsonObject { ["material"] = "Plaster", ["thickness"] = 0.015 }),
        });
        f.Run("architecture.createSlabType", new JsonObject
        {
            ["name"] = "F1 Ground floor",
            ["layers"] = new JsonArray(new JsonObject { ["material"] = "Screed", ["thickness"] = 0.05 }, new JsonObject { ["material"] = "Concrete", ["thickness"] = 0.15, ["core"] = true }),
        });
        return f;
    }

    static JsonObject Layout(Fixture f, string id) => (JsonObject)f.Session.Query("schedule.layout", new JsonObject { ["id"] = id })!;
    static List<string> Texts(JsonObject layout) => ((JsonArray)layout["pens"]!).SelectMany(p => p!["texts"] as JsonArray ?? []).Select(t => (string)t!["text"]!).ToList();

    /// <summary>The cut lines of a card's strip (40 mm long), their heights from the top one down.</summary>
    static List<double> StripLines(IReadOnlyList<Lapin.Drawing.Views.Pen> pens, double width, string title = "Wall · WT1 · WT1 Exterior")
    {
        // The card's strip starts 7 mm right of its frame, its title 3 mm.
        var x0 = pens.SelectMany(p => p.Texts).Single(t => t.Text == title).At.X - 3 + 7;
        return pens.Where(p => p.Layer == "SCHEDULE-SYMBOLS" && Math.Abs(p.Width - width) < 1e-9).SelectMany(p => p.Segments)
            .Where(s => Math.Abs(s.A.Y - s.B.Y) < 1e-9 && Math.Abs(Math.Min(s.A.X, s.B.X) - x0) < 1e-6 && Math.Abs(Math.Abs(s.B.X - s.A.X) - 40) < 1e-6).Select(s => s.A.Y).ToList();
    }

    [Fact]
    public void A_new_type_assemblies_schedule_draws_each_types_build_up_after_its_table()
    {
        var f = F();
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "types" })!["id"]!;
        var layout = Layout(f, id);
        Assert.Equal([TypeBuildUps.Drawing], ((JsonArray)layout["drawings"]!).Select(x => (string)x!));
        var texts = Texts(layout);
        Assert.Contains("Type assemblies", texts);
        Assert.Contains("Build-ups", texts);
        Assert.Contains("Wall · WT1 · WT1 Exterior", texts);
        Assert.Contains("Floor · F1 Ground floor", texts);
        // Each layer named as the table names it: its number or its name, its material, its thickness; the total beside the strip.
        Assert.Contains("1  Render  15", texts);
        Assert.Contains("Thermal insulation  EPS insulation  100", texts);
        Assert.Contains("3  Brick  250", texts);
        Assert.Contains("380", texts);
        Assert.Contains("200", texts);
        // The materials' hatches: the brick and the concrete are hatched as the section hatches them.
        Assert.Contains(((JsonArray)layout["pens"]!), p => (string?)p!["layer"] == "SCHEDULE-HATCH" && ((JsonArray)p["segments"]!).Count > 0);
    }

    [Fact]
    public void The_build_up_is_cut_to_the_schedules_scale_its_core_drawn_heavier()
    {
        var f = F();
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "types" })!["id"]!;
        var s = f.Model.Get(id)!;
        var q = new Q(f);
        var pens = new ScheduleEntity().Pages(q, s, 380, null)[0].Pens;
        // The wall at 1:50: 15 + 100 + 250 + 15 mm are 0.3, 2, 5 and 0.3 mm on paper; the faces and the core's heavy, the other joints light.
        var heavy = StripLines(pens, 0.35);
        var light = StripLines(pens, 0.18);
        var wall = heavy.Concat(light).Order().ToList();
        Assert.Equal([0, 0.3, 2.3, 7.3, 7.6], wall.Select(y => Math.Round(y - wall[0], 6)));
        // Heavy: the two faces and the core's two (between the insulation and the brick, the brick and the plaster).
        Assert.Equal([0, 2.3, 7.3, 7.6], heavy.Order().Select(y => Math.Round(y - wall[0], 6)));

        // At 1:20 the same wall is 19 mm deep.
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["scale"] = 20 });
        pens = new ScheduleEntity().Pages(q, f.Model.Get(id)!, 380, null)[0].Pens;
        var ys = StripLines(pens, 0.35).Concat(StripLines(pens, 0.18)).Order().ToList();
        Assert.Equal(19, Math.Round(ys[4] - ys[0], 6));
    }

    [Fact]
    public void Switched_off_the_schedule_is_its_table_alone_and_one_made_before_stays_so()
    {
        var f = F();
        var id = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "types" })!["id"]!;
        var with = Layout(f, id);
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["drawings"] = new JsonArray() });
        var without = Layout(f, id);
        Assert.DoesNotContain("Build-ups", Texts(without));
        Assert.True((double)with["h"]! > (double)without["h"]! + 20);
        // A schedule kept from before (no drawings) is a plain table until asked.
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["drawings"] = null });
        Assert.DoesNotContain("Build-ups", Texts(Layout(f, id)));
        // On a sheet's pages, broken to its height, the build-ups follow the table.
        f.Run("architecture.updateSchedule", new JsonObject { ["id"] = id, ["drawings"] = new JsonArray((JsonNode)TypeBuildUps.Drawing) });
        var pages = new ScheduleEntity().Pages(new Q(f), f.Model.Get(id)!, 380, 260);
        Assert.Contains(pages.SelectMany(p => p.Pens).SelectMany(p => p.Texts), t => t.Text == "Build-ups");
    }

    [Fact]
    public void An_export_is_written_where_the_save_dialog_says_and_never_for_a_guest()
    {
        var f = F();
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        var path = Path.Combine(_dir, "Type assemblies.csv");
        var bytes = System.Text.Encoding.UTF8.GetBytes("Kind,Type\nWall,WT1 Exterior\n");
        JsonObject Call(JsonObject p, Caller? caller = null)
        {
            var (reply, _) = caller == null ? d.Handle(new JsonObject { ["id"] = 1, ["method"] = "file.write", ["params"] = p }.ToJsonString())
                : d.Handle(new JsonObject { ["id"] = 1, ["method"] = "file.write", ["params"] = p }.ToJsonString(), caller);
            return JsonNode.Parse(BinaryFrame.HeaderText(reply))!.AsObject();
        }
        var r = Call(new JsonObject { ["path"] = path, ["data"] = Convert.ToBase64String(bytes) });
        Assert.Null(r["error"]);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.NotNull(Call(new JsonObject { ["path"] = path, ["data"] = "not base64!" })["error"]);
        var other = Path.Combine(_dir, "guest.csv");
        Assert.NotNull(Call(new JsonObject { ["path"] = other, ["data"] = Convert.ToBase64String(bytes) }, new Caller("p1", "editor"))["error"]);
        Assert.False(File.Exists(other));
    }

    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }
}
