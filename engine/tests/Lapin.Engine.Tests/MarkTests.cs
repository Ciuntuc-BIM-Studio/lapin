using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Shop drawings, stage S3 (docs/plan-shop-drawings.md, decision 583): marks of identical parts. Copies turned and moved share a
/// mark; a mirror image has its own; editing one of many gives it a new mark, editing the only one keeps it; points rounded to
/// 0.5 mm; an old project's steel schedule marks kept by the first numbering; the sheet's number is the mark.
/// </summary>
public class MarkTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());

    static string Id(JsonNode? n) => (string)n!["id"]!;

    static string Member(Fixture f, Vec3 a, Vec3 b) =>
        Id(f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = a.X, ["y1"] = a.Y, ["z1"] = a.Z, ["x2"] = b.X, ["y2"] = b.Y, ["z2"] = b.Z }));

    static string? Mark(Fixture f, string id) => Marks.MarkOf(f.Model, f.Model.Get(id)!);

    static void Number(Fixture f, params string[] ids) => f.Run("marks.number", new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()) });

    /// <summary>A member 4 m long and its 19 copies, turned and moved every way, one of them drawn from its other end.</summary>
    static List<string> Twenty(Fixture f)
    {
        var ids = new List<string> { Member(f, new Vec3(0, 0, 1), new Vec3(4, 0, 1)) };
        f.Run("steel.addHoles", new JsonObject { ["id"] = ids[0], ["station"] = 0.2, ["count"] = 3, ["pitch"] = 0.1 });
        for (var i = 1; i < 19; i++)
        {
            var copy = f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(ids[0]), ["op"] = "rotate", ["center"] = new JsonArray(10, 10), ["angle"] = i * 23, ["copy"] = true })!;
            var id = (string)copy["ids"]![0]!;
            f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(i * 0.7, -i * 1.3) });
            ids.Add(id);
        }
        // Drawn end for end: its holes near its end, the same part turned round.
        var reversed = Member(f, new Vec3(4, 20, 1), new Vec3(0, 20, 1));
        f.Run("steel.addHoles", new JsonObject { ["id"] = reversed, ["station"] = 3.6, ["count"] = 3, ["pitch"] = 0.1 });
        ids.Add(reversed);
        return ids;
    }

    [Fact]
    public void Twenty_copies_turned_and_moved_share_one_mark_with_a_quantity_of_twenty()
    {
        var f = F();
        var ids = Twenty(f);
        f.Run("marks.number", new JsonObject());
        var marks = ids.Select(id => Mark(f, id)).Distinct().ToList();
        var mark = Assert.Single(marks);
        Assert.Equal("M1", mark);
        Assert.Equal("20", TagText.Value(f.Model, f.Model.Get(ids[5])!, "qty"));
        Assert.Equal("M1", TagText.Value(f.Model, f.Model.Get(ids[5])!, "mark"));
        // Numbered again: nothing changes.
        f.Run("marks.number", new JsonObject());
        Assert.All(ids, id => Assert.Equal("M1", Mark(f, id)));
        Assert.Single(Marks.Register(f.Model));
    }

    [Fact]
    public void A_mirror_image_has_its_own_mark()
    {
        var f = F();
        var a = Member(f, new Vec3(0, 0, 1), new Vec3(4, 0, 1));
        var b = Member(f, new Vec3(0, 5, 1), new Vec3(4, 5, 1));
        // A hole in the top flange near the start, on one side of the web; the other member's on the other side: mirror images.
        f.Run("steel.addHoles", new JsonObject { ["id"] = a, ["station"] = 0.3, ["face"] = "top", ["across"] = 0.03 });
        f.Run("steel.addHoles", new JsonObject { ["id"] = b, ["station"] = 0.3, ["face"] = "top", ["across"] = -0.03 });
        Number(f, a, b);
        Assert.NotEqual(Mark(f, a), Mark(f, b));
        // The first one turned end for end and upside down is still itself.
        var c = Member(f, new Vec3(4, 9, 1), new Vec3(0, 9, 1));
        f.Run("steel.addHoles", new JsonObject { ["id"] = c, ["station"] = 3.7, ["face"] = "top", ["across"] = -0.03 });
        Number(f, c);
        Assert.Equal(Mark(f, a), Mark(f, c));
    }

    [Fact]
    public void Editing_one_of_twenty_gives_it_a_new_mark_and_editing_the_only_one_keeps_it()
    {
        var f = F();
        var ids = Twenty(f);
        var lone = Member(f, new Vec3(0, 30, 1), new Vec3(6, 30, 1));
        f.Run("marks.number", new JsonObject());
        var loneMark = Mark(f, lone);
        Assert.Equal("M1", loneMark);
        Assert.Equal("M2", Mark(f, ids[0]));
        f.Run("steel.addHoles", new JsonObject { ["id"] = ids[7], ["station"] = 2, ["count"] = 2, ["pitch"] = 0.1 });
        f.Run("steel.addHoles", new JsonObject { ["id"] = lone, ["station"] = 1 });
        f.Run("marks.number", new JsonObject());
        Assert.Equal("M3", Mark(f, ids[7]));
        Assert.All(ids.Where(id => id != ids[7]), id => Assert.Equal("M2", Mark(f, id)));
        Assert.Equal("19", TagText.Value(f.Model, f.Model.Get(ids[0])!, "qty"));
        Assert.Equal(loneMark, Mark(f, lone));
        // The part deleted: its mark free, kept until renumbered.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(ids[7]) });
        f.Run("marks.number", new JsonObject());
        Assert.Contains(Marks.Register(f.Model), x => x.Mark == "M3" && x.Ids.Count == 0);
        f.Run("marks.renumber", new JsonObject());
        Assert.DoesNotContain(Marks.Register(f.Model), x => x.Mark == "M3");
        Assert.Equal(["M1", "M2"], Marks.Register(f.Model).Select(x => x.Mark).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Points_are_rounded_to_half_a_millimetre()
    {
        var f = F();
        var a = Member(f, new Vec3(0, 0, 1), new Vec3(4, 0, 1));
        var near = Member(f, new Vec3(0, 3, 1), new Vec3(4.0002, 3, 1));
        var far = Member(f, new Vec3(0, 6, 1), new Vec3(4.001, 6, 1));
        Number(f, a, near, far);
        Assert.Equal(Mark(f, a), Mark(f, near));
        Assert.NotEqual(Mark(f, a), Mark(f, far));
    }

    [Fact]
    public void An_old_projects_steel_schedule_marks_stay_when_it_is_first_numbered()
    {
        var f = F();
        f.Run("steel.addHall", new JsonObject { ["storeyId"] = f.Storey });
        var before = SteelMembersSchedule.Rows(f.Model, f.Session.Geometry).Select(r => (r.Mark, r.Profile, r.Length, r.Count)).ToList();
        Assert.NotEmpty(before);
        f.Run("marks.number", new JsonObject());
        var after = SteelMembersSchedule.Rows(f.Model, f.Session.Geometry).Select(r => (r.Mark, r.Profile, r.Length, r.Count)).ToList();
        Assert.Equal(before, after);
        // The register holds those marks for the hall's members: each of the schedule's marks is a part's, every part's mark of its kind.
        // A schedule's mark that held mirror images (a left and a right rafter) keeps it for the first; the other takes a mark of its own.
        var schedule = SteelMembersSchedule.Marked(f.Model, f.Session.Geometry, false).Marks;
        foreach (var g in schedule.GroupBy(kv => kv.Value)) Assert.Contains(g, kv => Mark(f, kv.Key) == g.Key);
        foreach (var (id, mark) in schedule) Assert.Equal(mark.TrimEnd("0123456789".ToCharArray()), Mark(f, id)!.TrimEnd("0123456789".ToCharArray()));
    }

    [Fact]
    public void A_drawing_numbers_its_part_and_its_sheet_is_its_mark_and_a_turned_frame_is_kept_per_mark()
    {
        var f = F();
        var a = Member(f, new Vec3(0, 0, 1), new Vec3(4, 0, 1));
        var b = Member(f, new Vec3(0, 3, 1), new Vec3(4, 3, 1));
        var made = (JsonObject)f.Run("drawing.create", new JsonObject { ["element"] = a })!;
        var sheet = f.Model.Get((string)made["sheet"]!)!;
        Assert.Equal("M1", sheet.Str("number"));
        Assert.Contains("M1", sheet.Name);
        Number(f, b);
        Assert.Equal("M1", Mark(f, b));
        var rows = ShopDrawings.DataRows(f.Session.Reader, f.Model.Get(a)!);
        Assert.Contains(rows, r => r.Label == "QUANTITY" && r.Value == "2");
        // The mark's frame turned a quarter: both parts drawn the same new way, nothing written on them.
        var x0 = ElementFrames.Of(f.Session.Reader, f.Model.Get(b)!)!.X;
        var attrs = f.Model.Get(b)!.Attrs.ToJsonString();
        f.Run("marks.setFrame", new JsonObject { ["id"] = a, ["turn"] = 1 });
        var turned = ElementFrames.Of(f.Session.Reader, f.Model.Get(b)!)!;
        Assert.True(Math.Abs(Vec3.Dot(turned.X, x0)) < 1e-9);
        Assert.Equal(attrs, f.Model.Get(b)!.Attrs.ToJsonString());
        // The signature does not turn with it: the parts keep their mark.
        f.Run("marks.number", new JsonObject());
        Assert.Equal("M1", Mark(f, b));
    }
}
