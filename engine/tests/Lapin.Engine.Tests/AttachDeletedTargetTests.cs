using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Roofs;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Studio;
using Xunit;

namespace Lapin.Engine.Tests;

/// <summary>
/// A wall attached to an element that is deleted (docs/plan-attach-to.md, stage A0): the target is taken out of its
/// <c>roofIds</c> in the delete's undo step, and a wall left naming nothing stands at its own height with a Problem, never
/// rising to every roof.
/// </summary>
public class AttachDeletedTargetTests
{
    static JsonArray P(double x, double y) => new(x, y);
    static string[] Ids(JsonNode? r) => [.. ((JsonArray)r!["ids"]!).Select(n => (string)n!)];
    static string[]? RoofIds(Fixture f, string wall) => f.Model.Get(wall)!.Attrs["roofIds"] is JsonArray a ? [.. a.Select(x => (string)x!)] : null;
    static IReadOnlyList<string> Problems(Fixture f, string id) => f.Session.Geometry(id)!.Diagnostics ?? [];

    [Fact]
    public void A_wall_attached_to_a_deleted_body_stays_at_its_own_height_under_another_storeys_roof()
    {
        var f = new Fixture(new ArchitectureModule(), new DraftingModule(), new StudioModule());
        var upper = (string)f.Run("storey.create", [])!["id"]!;            // +3.00
        var top = (string)f.Run("storey.create", [])!["id"]!;              // +6.00
        string Plan(string storey) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey })!["id"]!;
        string Draw(string view, double x0, double x1) => (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = view,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(P(x0, 0), P(x1, 0), P(x1, 1), P(x0, 1)), ["closed"] = true },
        })!["ids"]![0]!;
        var a = Draw(Plan(upper), 0, 2);
        var b = Draw(Plan(top), 8, 10);
        var body = (string)f.Run("studio.loft", new JsonObject { ["profileId"] = a, ["profileIds"] = new JsonArray(a, b) })!["id"]!;
        var name = f.Model.Get(body)!.Name;
        // A roof on the storey above covers the wall: in auto it is not the wall's storey's, so it never counts.
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = upper, ["points"] = new JsonArray(P(-3, -4), P(14, -4), P(14, 5), P(-3, 5)) });
        var wall = f.Wall(-1, 0.5, 12, 0.5);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(body) });
        Assert.Equal(6, f.Bounds(wall).Max.Z, 2);

        f.Run("element.delete", new JsonObject { ["id"] = body });
        // Its own 3.00: it does not rise to the roof over it, as an unnamed attachment would.
        Assert.Equal(3, f.Bounds(wall).Max.Z, 6);
        Assert.Null(RoofIds(f, wall));
        Assert.Equal("none", f.Model.Get(wall)!.Str("roofTop"));
        Assert.Contains(Problems(f, wall), p => p.Contains($"„{name}”"));

        // One undo brings the body back and the wall attached to it, as it was.
        f.Session.Undo();
        Assert.Equal([body], RoofIds(f, wall)!);
        Assert.Equal("attach", f.Model.Get(wall)!.Str("roofTop"));
        Assert.Equal(6, f.Bounds(wall).Max.Z, 2);
        Assert.DoesNotContain(Problems(f, wall), p => p.Contains("attached"));
        f.Session.Redo();
        Assert.Equal(3, f.Bounds(wall).Max.Z, 6);

        // Detached by hand, the problem is gone with what it lost.
        f.Run("architecture.detachFromRoof", new JsonObject { ["ids"] = new JsonArray(wall) });
        Assert.DoesNotContain(Problems(f, wall), p => p.Contains("attached"));
    }

    [Fact]
    public void A_wall_attached_to_a_split_roof_names_both_pieces_and_keeps_the_other_when_one_is_deleted()
    {
        var f = new Fixture();
        var attic = (string)f.Run("storey.create", new JsonObject { ["name"] = "Attic" })!["id"]!;   // +3.00
        var roof = (string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = attic, ["points"] = new JsonArray(P(0, 0), P(10, 0), P(10, 6), P(0, 6)) })!["id"]!;
        f.Run("architecture.updateRoof", new JsonObject { ["id"] = roof, ["attrs"] = new JsonObject { ["shape"] = "gable", ["ridgeDirection"] = "x" } });
        var wall = f.Wall(1, 3, 9, 3);
        f.Run("architecture.attachToRoof", new JsonObject { ["ids"] = new JsonArray(wall), ["roofIds"] = new JsonArray(roof) });
        var whole = f.Bounds(wall).Max.Z;
        Assert.True(whole > 6, $"attached, it rises to the ridge, not {whole}");
        // Split: the wall names both pieces (RoofPieces), as before.
        var pieces = Ids(f.Run("element.splitOutline", new JsonObject { ["id"] = roof, ["a"] = P(5, -1), ["b"] = P(5, 7), ["keep"] = P(1, 1) }));
        Assert.Equal(pieces.OrderBy(x => x), RoofIds(f, wall)!.OrderBy(x => x));
        // One piece deleted: the wall follows the other, still attached, with nothing wrong.
        var gone = pieces[1];
        f.Run("element.delete", new JsonObject { ["id"] = gone });
        Assert.Equal([pieces[0]], RoofIds(f, wall)!);
        Assert.Equal("attach", f.Model.Get(wall)!.Str("roofTop"));
        Assert.DoesNotContain(Problems(f, wall), p => p.Contains("attached"));
        Assert.Equal(whole, f.Bounds(wall).Max.Z, 2);
        f.Session.Undo();
        Assert.Equal(pieces.OrderBy(x => x), RoofIds(f, wall)!.OrderBy(x => x));
    }

    [Fact]
    public void A_wall_naming_only_gone_targets_takes_no_roof_and_says_so()
    {
        // A model left so by an older build (or a removal that told no one): the named ids are gone.
        var f = new Fixture();
        var attic = (string)f.Run("storey.create", new JsonObject { ["name"] = "Attic" })!["id"]!;
        f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = attic, ["points"] = new JsonArray(P(0, 0), P(10, 0), P(10, 6), P(0, 6)) });
        var wall = f.Model.Get(f.Wall(1, 3, 9, 3))!;
        var attrs = wall.CloneAttrs();
        attrs["roofTop"] = "attach";
        attrs["roofIds"] = new JsonArray("gone-id");
        var dangling = wall.With(attrs: attrs);
        Assert.Empty(RoofTop.AttachedRoofs(dangling, f.Model));
        Assert.Contains("gone-id", WallTops.Problem(f.Model, dangling));
        // Naming none, an attached wall still takes every roof (unchanged).
        attrs.Remove("roofIds");
        Assert.Single(RoofTop.AttachedRoofs(wall.With(attrs: attrs), f.Model));
    }
}
