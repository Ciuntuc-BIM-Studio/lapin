using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// What a selected door's or window's temporary dimensions measure to (a tester's report, decision 511): along its host
/// wall, metres from the wall's start, the faces, core faces and axis of the walls that meet it, the host's own ends as its
/// joins leave them, and its other openings.
/// </summary>
public class OpeningReferenceTests
{
    static JsonObject Refs(Fixture f, JsonObject a) => f.Session.Query("architecture.openingReferences", a)!.AsObject();
    static double[] Nums(JsonNode? n) => n!.AsArray().Select(x => (double)x!).OrderBy(x => x).ToArray();
    static JsonObject WallIn(JsonObject r, string id) => r["walls"]!.AsArray().Select(w => w!.AsObject()).Single(w => (string?)w["id"] == id);

    static string Brick(Fixture f) => f.Type("Brick 300", ("Brick", 0.3, true));

    [Fact]
    public void A_window_measures_to_the_faces_of_the_walls_that_meet_its_wall_and_to_its_wall_s_ends()
    {
        var f = new Fixture();
        var t = Brick(f);
        var host = f.Wall(0, 0, 6, 0, t);
        var corner = f.Wall(6, 0, 6, 4, t);                 // an L at the host's end, to its left (+y)
        var tee = f.Wall(3, 0, 3, 4, t);                    // a T on its left face
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = host, ["along"] = 1.5 })!["id"]!;
        var r = Refs(f, new JsonObject { ["id"] = window });

        Assert.Equal(host, (string?)r["wallId"]);
        Assert.Null(r["curved"]);
        Assert.Equal(6, (double)r["length"]!, 6);
        Assert.Equal(1.5, (double)r["opening"]!["along"]!, 9);
        Assert.Equal(0.15, (double)r["faces"]!["left"]!, 6);
        Assert.Equal(-0.15, (double)r["faces"]!["right"]!, 6);

        // The T: its faces 150 mm each side of its axis at 3 m, on both of the host's faces (it stands square to it); it meets the left one only.
        var tw = WallIn(r, tee);
        Assert.Equal(new[] { "left" }, tw["sides"]!.AsArray().Select(x => (string)x!).ToArray());
        Assert.Equal(new[] { 2.85, 3.15 }, Nums(tw["left"]!["faces"]).Select(x => Math.Round(x, 6)).ToArray());
        Assert.Equal(3, (double)tw["left"]!["axis"]!, 6);
        Assert.Equal(new[] { 2.85, 3.15 }, Nums(tw["right"]!["faces"]).Select(x => Math.Round(x, 6)).ToArray());
        Assert.Null(tw["left"]!["core"]);                    // one layer: the core is the whole wall

        // The corner: its inner face at 5.85, its outer one at 6.15 (the host's outer corner).
        var cw = WallIn(r, corner);
        Assert.Equal(new[] { 5.85, 6.15 }, Nums(cw["left"]!["faces"]).Select(x => Math.Round(x, 6)).ToArray());

        // The host's ends as its joins leave them: no join at its start; at the corner its left face stops at the corner's inner face.
        var endsLeft = Nums(r["ends"]!["left"]);
        Assert.Equal(0, endsLeft[0], 6);
        Assert.InRange(endsLeft[1], 5.85 - 1e-6, 6.15 + 1e-6);
        var endsRight = Nums(r["ends"]!["right"]);
        Assert.InRange(endsRight[1], 5.85 - 1e-6, 6.15 + 1e-6);
    }

    [Fact]
    public void Walls_that_do_not_meet_the_host_and_walls_along_it_are_left_out()
    {
        var f = new Fixture();
        var t = Brick(f);
        var host = f.Wall(0, 0, 6, 0, t);
        var apart = f.Wall(3, 1, 3, 4, t);                  // square to it, a metre short of it
        var along = f.Wall(6, 0, 9, 0, t);                  // in line: the host's end, not a face across it
        var parallel = f.Wall(0, 0.3, 6, 0.3, t);           // beside it, touching its face
        var r = Refs(f, new JsonObject { ["wallId"] = host });
        var ids = r["walls"]!.AsArray().Select(w => (string?)w!["id"]).ToList();
        Assert.DoesNotContain(apart, ids);
        Assert.DoesNotContain(along, ids);
        Assert.DoesNotContain(parallel, ids);
        Assert.Null(r["opening"]);
    }

    [Fact]
    public void A_wall_crossing_at_an_angle_is_measured_where_its_faces_cross_each_face_of_the_host()
    {
        var f = new Fixture();
        var t = Brick(f);
        var host = f.Wall(0, 0, 6, 0, t);
        // At 45° through (3, 0), running on past both faces.
        var slant = f.Wall(2, -1, 4, 1, t);
        var r = Refs(f, new JsonObject { ["wallId"] = host });
        var w = WallIn(r, slant);
        Assert.Equal(2, w["sides"]!.AsArray().Count);
        // On the left face (y = 0.15) its axis crosses at x = 3.15, its faces 0.15·√2 either side of it.
        var d = 0.15 * Math.Sqrt(2);
        Assert.Equal(3.15, (double)w["left"]!["axis"]!, 6);
        Assert.Equal(new[] { 3.15 - d, 3.15 + d }, Nums(w["left"]!["faces"]), new Tol());
        Assert.Equal(2.85, (double)w["right"]!["axis"]!, 6);
        Assert.Equal(new[] { 2.85 - d, 2.85 + d }, Nums(w["right"]!["faces"]), new Tol());
    }

    [Fact]
    public void A_layered_wall_offers_its_core_faces_and_the_other_openings_are_listed()
    {
        var f = new Fixture();
        var plain = Brick(f);
        var layered = f.Type("Plastered brick", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.02, false));
        var host = f.Wall(0, 0, 8, 0, plain);
        var tee = f.Wall(4, 0, 4, 3, layered);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = host, ["along"] = 2 })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = host, ["along"] = 6 })!["id"]!;
        var r = Refs(f, new JsonObject { ["id"] = door });
        var faces = Nums(WallIn(r, tee)["left"]!["faces"]);
        var core = Nums(WallIn(r, tee)["left"]!["core"]);
        Assert.Equal(0.29, faces[1] - faces[0], 6);
        Assert.Equal(0.25, core[1] - core[0], 6);
        Assert.Equal(0.02, core[0] - faces[0], 6);
        var others = r["openings"]!.AsArray().Select(o => o!.AsObject()).ToList();
        var w = Assert.Single(others);
        Assert.Equal(window, (string?)w["id"]);
        Assert.Equal(6, (double)w["along"]!, 9);
        Assert.True((double)w["width"]! > 0);
        // Asked by the opening rather than the door: the same answer.
        var opening = (string)Refs(f, new JsonObject { ["id"] = door })["opening"]!["id"]!;
        Assert.Equal(2, (double)Refs(f, new JsonObject { ["id"] = opening })["opening"]!["along"]!, 9);
    }

    [Fact]
    public void A_curved_wall_is_measured_along_its_arc_each_face_by_its_own_radius()
    {
        var f = new Fixture();
        var t = Brick(f);
        // An arc from (0, 0) to (8, 0) rising 2 m to its left (+y): radius 5 about (4, −3); its left face (outside) at 5.15, its right at 4.85.
        var host = f.Wall(0, 0, 8, 0, t);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = host, ["arc"] = 2 });
        // A straight wall across its top, along the radius there.
        var across = f.Wall(4, 0.5, 4, 4, t);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = host, ["along"] = 2 })!["id"]!;
        var r = Refs(f, new JsonObject { ["id"] = window });

        var length = 5 * 4 * Math.Atan(0.5);
        Assert.True((bool)r["curved"]!);
        Assert.Equal(length, (double)r["length"]!, 6);
        Assert.Equal(5, (double)r["radius"]!, 6);
        Assert.Equal(new[] { 4.0, -3.0 }, r["centre"]!.AsArray().Select(x => Math.Round((double)x!, 6)).ToArray());
        Assert.Equal(2, (double)r["rise"]!, 9);
        Assert.Equal(5.15 / 5, (double)r["scale"]!["left"]!, 6);
        Assert.Equal(4.85 / 5, (double)r["scale"]!["right"]!, 6);
        // Free ends: the axis's own, on both faces.
        Assert.Equal(new[] { 0, Math.Round(length, 6) }, Nums(r["ends"]!["left"]).Select(x => Math.Round(x, 6)).ToArray());

        // The crossing wall: its axis at the top (half the arc along), its faces where x = 4 ± 0.15 meets each face's circle.
        var w = WallIn(r, across);
        Assert.Equal(length / 2, (double)w["left"]!["axis"]!, 6);
        var dl = 5 * Math.Asin(0.15 / 5.15);
        Assert.Equal(new[] { length / 2 - dl, length / 2 + dl }, Nums(w["left"]!["faces"]), new Tol());
        var dr = 5 * Math.Asin(0.15 / 4.85);
        Assert.Equal(new[] { length / 2 - dr, length / 2 + dr }, Nums(w["right"]!["faces"]), new Tol());
    }

    [Fact]
    public void An_opening_tells_its_bottom_top_and_sill_against_its_storey_s_level_for_sections()
    {
        var f = new Fixture();
        var host = f.Wall(0, 0, 6, 0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = host, ["along"] = 2 })!["id"]!;
        var r = Refs(f, new JsonObject { ["id"] = window });
        Assert.Equal(0, (double)r["levels"]!["storey"]!, 9);
        Assert.Equal(0, (double)r["levels"]!["base"]!, 9);
        var o = r["opening"]!;
        Assert.Equal(window, (string?)o["fill"]);
        Assert.Equal(0.9, (double)o["sill"]!, 9);
        Assert.Equal(0.9, (double)o["bottom"]!, 9);
        Assert.Equal(2.4, (double)o["top"]!, 9);
        Assert.Equal(1, (double)r["scale"]!["left"]!, 9);
        // The sill moved as the section's dimension does it: the window's own sill, through architecture.moveOpening.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = window, ["sill"] = 1.1 });
        Assert.Equal(1.1, (double)Refs(f, new JsonObject { ["id"] = window })["opening"]!["bottom"]!, 9);
        Assert.Throws<CommandException>(() => Refs(f, new JsonObject { ["id"] = "nothing" }));
    }

    sealed class Tol : IEqualityComparer<double>
    {
        public bool Equals(double a, double b) => Math.Abs(a - b) < 1e-6;
        public int GetHashCode(double x) => 0;
    }
}
