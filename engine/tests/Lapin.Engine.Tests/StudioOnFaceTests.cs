using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Drawing on a face in 3D (decision 272): a shape drawn on a face is a sketch on the face's reference plane and,
/// pushed or pulled, a solid out of the face or a cut into it, measured by the volumes they must have.
/// </summary>
public class StudioOnFaceTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray J3(Vec3 p) => [p.X, p.Y, p.Z];
    static double Vol(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));
    static string Id(JsonNode? r, string key = "id") => (string)r![key]!;

    /// <summary>A 2 × 2 square on the plan extruded 2 m up: a cube of 8 m³.</summary>
    static (Fixture F, string Cube) Cube()
    {
        var f = F();
        var plan = Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey }));
        var sq = (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(2, 0), J(2, 2), J(0, 2)), ["closed"] = true },
        })!["ids"]![0]!;
        return (f, Id(f.Run("studio.extrude", new JsonObject { ["profileId"] = sq, ["depth"] = 2 })));
    }

    /// <summary>A rectangle on the east face (x = 2) of the cube, 1 × 1 in its middle.</summary>
    static JsonObject EastSquare(string cube, double distance) => new()
    {
        ["id"] = cube, ["point"] = J3(new Vec3(2, 1, 1)), ["normal"] = J3(new Vec3(1, 0, 0)), ["distance"] = distance,
        ["outline"] = new JsonArray(J3(new(2, 0.5, 0.5)), J3(new(2, 1.5, 0.5)), J3(new(2, 1.5, 1.5)), J3(new(2, 0.5, 1.5))),
    };

    [Fact]
    public void A_rectangle_drawn_on_a_side_and_pulled_is_a_solid_on_it_and_pushed_a_cut_into_it()
    {
        var (f, cube) = Cube();
        // Pulled 0.3 out of the east face: a solid of its own, 1 × 1 × 0.3, standing on the face.
        var r = f.Run("studio.onFace", EastSquare(cube, 0.3))!;
        Assert.Equal("solid", (string)r["how"]!);
        var boss = Id(r);
        Assert.Equal(0.3, Vol(f, boss), 6);
        var b = f.Bounds(boss);
        Assert.Equal(2, b.Min.X, 6); Assert.Equal(2.3, b.Max.X, 6);
        Assert.Equal(0.5, b.Min.Z, 6); Assert.Equal(1.5, b.Max.Z, 6);
        Assert.Equal(8, Vol(f, cube), 6);
        // Its sketch lies on a reference plane on the face, kept as a face's (the 3D view does not draw it).
        var plane = f.Model.Get(Id(r, "planeId"))!;
        Assert.Equal(RefPlanes.Kind, plane.Str("kind"));
        Assert.Equal(cube, plane.Str(StudioModule.OnFaceKey));
        Assert.EndsWith(", east face", plane.Name);
        Assert.Equal(plane.Id, f.Model.Get(Id(r, "sketchId"))!.Str("viewId"));
        // Its outer end pushed back 0.1: the extrusion is shallower.
        Assert.Equal("depth", (string)f.Run("studio.pushPull", new JsonObject { ["id"] = boss, ["point"] = J3(new(2.3, 1, 1)), ["normal"] = J3(new Vec3(1, 0, 0)), ["distance"] = -0.1 })!["how"]!);
        Assert.Equal(0.2, Vol(f, boss), 6);

        // Another square on the same face pushed 0.4 in: a cut the cube is less, on the same plane.
        var o = EastSquare(cube, -0.4);
        o["outline"] = new JsonArray(J3(new(2, 0.2, 0.2)), J3(new(2, 0.4, 0.2)), J3(new(2, 0.4, 0.4)), J3(new(2, 0.2, 0.4)));
        o["point"] = J3(new(2, 0.3, 0.3));
        var c = f.Run("studio.onFace", o)!;
        Assert.Equal("cut", (string)c["how"]!);
        Assert.Equal(plane.Id, Id(c, "planeId"));
        Assert.Equal(8 - 0.04 * 0.4, Vol(f, cube), 6);
        // The pocket's floor pushed 0.2 further: the cut is deeper, nothing added to the cube.
        f.Run("studio.pushPull", new JsonObject { ["id"] = cube, ["point"] = J3(new(1.6, 0.3, 0.3)), ["normal"] = J3(new Vec3(1, 0, 0)), ["distance"] = -0.2 });
        Assert.Equal(8 - 0.04 * 0.6, Vol(f, cube), 6);
        Assert.Equal(-0.6, f.Model.Get(Id(c))!.Num("depth"), 6);
    }

    [Fact]
    public void A_circle_drawn_on_the_top_and_pushed_through_is_a_hole()
    {
        var (f, cube) = Cube();
        f.Run("studio.onFace", new JsonObject
        {
            ["id"] = cube, ["point"] = J3(new(1, 1, 2)), ["normal"] = J3(Vec3.UnitZ), ["distance"] = -2.5,
            ["circle"] = new JsonObject { ["center"] = J3(new(1, 1, 2)), ["radius"] = 0.5 },
        });
        Assert.InRange(Vol(f, cube), 8 - Math.PI * 0.25 * 2, 8 - Math.PI * 0.25 * 2 * 0.99);
    }

    [Fact]
    public void A_shape_drawn_on_a_wall_cuts_a_niche_into_it_and_without_a_distance_stays_a_sketch()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var before = Vol(f, wall);
        var b = f.Bounds(wall);
        // A 1 × 1 niche 0.1 deep in the wall's face towards −y.
        var y = b.Min.Y;
        var pick = new JsonObject
        {
            ["id"] = wall, ["point"] = J3(new(3, y, 1)), ["normal"] = J3(new Vec3(0, -1, 0)),
            ["outline"] = new JsonArray(J3(new(2.5, y, 0.5)), J3(new(3.5, y, 0.5)), J3(new(3.5, y, 1.5)), J3(new(2.5, y, 1.5))),
        };
        // No distance: the sketch alone, nothing made.
        var s = f.Run("studio.onFace", (JsonObject)pick.DeepClone())!;
        Assert.Null(s["id"]);
        Assert.Equal(before, Vol(f, wall), 6);
        pick["distance"] = -0.1;
        Assert.Equal("cut", (string)f.Run("studio.onFace", pick)!["how"]!);
        Assert.Equal(before - 0.1, Vol(f, wall), 6);
    }

    /// <summary>
    /// The 3D sketch (decision 374): the face's plane alone for the command line, the same one the shapes then go on;
    /// an open line kept as a sketch on it and refused a push; a closed shape drafted there pulled out by its id.
    /// </summary>
    [Fact]
    public void A_sketch_on_a_face_gives_its_plane_keeps_open_lines_and_pulls_a_drafted_shape()
    {
        var (f, cube) = Cube();
        var pick = new JsonObject { ["id"] = cube, ["point"] = J3(new Vec3(2, 1, 1)), ["normal"] = J3(new Vec3(1, 0, 0)) };
        var only = f.Run("studio.onFace", (JsonObject)pick.DeepClone())!;
        Assert.Null(only["sketchId"]);
        var planeId = Id(only, "planeId");
        Assert.EndsWith(", east face", (string)only["name"]!);
        // Asked again, the same plane.
        Assert.Equal(planeId, Id(f.Run("studio.onFace", (JsonObject)pick.DeepClone()), "planeId"));

        // An open line across the face: a sketch on that plane, never pushed.
        var line = (JsonObject)pick.DeepClone();
        line["open"] = true;
        line["outline"] = new JsonArray(J3(new(2, 0, 1)), J3(new(2, 2, 1)));
        var l = f.Run("studio.onFace", line)!;
        Assert.Equal(planeId, Id(l, "planeId"));
        Assert.IsType<Line>(Drafts.ShapeOf(f.Model.Get(Id(l, "sketchId"))!));
        line["distance"] = 0.2;
        Assert.ThrowsAny<Exception>(() => f.Run("studio.onFace", line));

        // A square drafted on the plane with the command line (its own x, y), then pulled 0.5 by its id: 1 × 1 × 0.5.
        var sq = (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = planeId,
            ["shape"] = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(J(0, 0), J(1, 0), J(1, 1), J(0, 1)), ["closed"] = true },
        })!["ids"]![0]!;
        var pull = (JsonObject)pick.DeepClone();
        pull["sketchId"] = sq;
        pull["distance"] = 0.5;
        var r = f.Run("studio.onFace", pull)!;
        Assert.Equal("solid", (string)r["how"]!);
        Assert.Equal(sq, Id(r, "sketchId"));
        Assert.Equal(0.5, Vol(f, Id(r)), 6);
        var b = f.Bounds(Id(r));
        Assert.Equal(2, b.Min.X, 6); Assert.Equal(2.5, b.Max.X, 6);
        // Pushed in instead: a cut into the cube.
        var sq2 = (string)f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = planeId,
            ["shape"] = new JsonObject { ["type"] = "circle", ["c"] = J(-0.5, -0.5), ["r"] = 0.1 },
        })!["ids"]![0]!;
        pull["sketchId"] = sq2;
        pull["distance"] = -0.3;
        Assert.Equal("cut", (string)f.Run("studio.onFace", pull)!["how"]!);
        Assert.True(Vol(f, cube) < 8 - 1e-4);
    }

    /// <summary>Faces split by sketched lines (decision 375): a square crossed both ways is four regions, a dangling line splits nothing.</summary>
    [Fact]
    public void Lines_across_a_square_split_it_into_regions()
    {
        var sq = new List<(Vec2, Vec2)> { (new(0, 0), new(4, 0)), (new(4, 0), new(4, 4)), (new(4, 4), new(0, 4)), (new(0, 4), new(0, 0)) };
        var cross = new List<(Vec2, Vec2)>(sq) { (new(2, 0), new(2, 4)), (new(0, 1), new(4, 1)) };
        Assert.Equal(4, SketchRegions.Regions(cross).Count);
        var r = SketchRegions.RegionAt(cross, new Vec2(3, 3))!;
        Assert.Equal(2 * 3, Polygon2.SignedArea(r), 9);
        Assert.Equal(2 * 1, Polygon2.SignedArea(SketchRegions.RegionAt(cross, new Vec2(0.5, 0.5))!), 9);
        // A line from an edge that stops inside: no region but the square.
        var dangling = new List<(Vec2, Vec2)>(sq) { (new(2, 0), new(2, 3)) };
        Assert.Single(SketchRegions.Regions(dangling));
        Assert.Equal(16, Polygon2.SignedArea(SketchRegions.RegionAt(dangling, new Vec2(1, 1))!), 9);
    }

    [Fact]
    public void A_line_across_a_face_splits_it_and_push_pull_takes_the_region_clicked()
    {
        var (f, cube) = Cube();
        var east = new Vec3(1, 0, 0);
        var line = new JsonObject { ["id"] = cube, ["point"] = J3(new Vec3(2, 1, 1)), ["normal"] = J3(east), ["open"] = true,
            ["outline"] = new JsonArray(J3(new(2, 0, 0.5)), J3(new(2, 2, 0.5))) };
        f.Run("studio.onFace", line);
        // The face lights up as the region below the line: 2 × 0.5.
        var face = f.Session.Query("studio.faceAt", new JsonObject { ["id"] = cube, ["point"] = J3(new(2, 1, 0.2)), ["normal"] = J3(east) })!;
        Assert.True((bool)face["region"]!);
        Assert.Equal(4, face["outline"]!.AsArray().Count);
        // Pulled 0.3: a solid of that region only.
        var r = f.Run("studio.pushPull", new JsonObject { ["id"] = cube, ["point"] = J3(new(2, 1, 0.2)), ["normal"] = J3(east), ["distance"] = 0.3 })!;
        Assert.Equal("solid", (string)r["how"]!);
        Assert.True((bool)r["region"]!);
        Assert.Equal(2 * 0.5 * 0.3, Vol(f, Id(r)), 6);
        var b = f.Bounds(Id(r));
        Assert.Equal(0, b.Min.Z, 6); Assert.Equal(0.5, b.Max.Z, 6);
        // The region above, pushed in 0.1: a cut of 2 × 1.5 × 0.1.
        var c = f.Run("studio.pushPull", new JsonObject { ["id"] = cube, ["point"] = J3(new(2, 1, 1.5)), ["normal"] = J3(east), ["distance"] = -0.1 })!;
        Assert.Equal("cut", (string)c["how"]!);
        Assert.Equal(8 - 2 * 1.5 * 0.1, Vol(f, cube), 6);
    }

    [Fact]
    public void An_arc_drawn_on_a_face_is_an_arc_through_its_three_points()
    {
        var (f, cube) = Cube();
        var r = f.Run("studio.onFace", new JsonObject
        {
            ["id"] = cube, ["point"] = J3(new Vec3(2, 1, 1)), ["normal"] = J3(new Vec3(1, 0, 0)),
            ["arc"] = new JsonObject { ["start"] = J3(new(2, 0.5, 1)), ["through"] = J3(new(2, 1, 1.5)), ["end"] = J3(new(2, 1.5, 1)) },
        })!;
        var arc = Assert.IsType<Arc>(Drafts.ShapeOf(f.Model.Get(Id(r, "sketchId"))!));
        Assert.Equal(0.5, arc.R, 9);
        Assert.Equal(Math.PI, arc.Sweep, 9);
        Assert.Null(StudioModule.ArcThrough(new(0, 0), new(1, 1), new(2, 2)));
    }

    /// <summary>Follow Me (decision 376): a profile carried along a path where it stands, mitred at the corners.</summary>
    [Fact]
    public void Follow_me_carries_a_profile_along_a_path_with_mitred_corners()
    {
        // A 0.2 square standing across x at the path's start, beside it (left of the path, as an L turns).
        var sq = new List<Vec3> { new(0, 0, 0), new(0, 0.2, 0), new(0, 0.2, 0.2), new(0, 0, 0.2) };
        var straight = Solids.FollowMe(sq, [], [new(0, 0, 0), new(3, 0, 0)]);
        Assert.Equal(0.04 * 3, Measure.Volume(straight.Solid!), 9);
        // An L turning left: the profile's middle runs 2.9 + 2.9 (Pappus, piecewise).
        var l = Solids.FollowMe(sq, [], [new(0, 0, 0), new(3, 0, 0), new(3, 3, 0)]);
        Assert.Equal(0.04 * 5.8, Measure.Volume(l.Solid!), 9);
        // Round a closed 2 × 2 square path from a corner (the profile at its west side's north end): a ring, started where the profile stands.
        var ringProfile = new List<Vec3> { new(-0.1, 2, 0), new(0.1, 2, 0), new(0.1, 2, 0.2), new(-0.1, 2, 0.2) };
        var ring = Solids.FollowMe(ringProfile, [], [new(0, 0, 0), new(2, 0, 0), new(2, 2, 0), new(0, 2, 0), new(0, 0, 0)]);
        Assert.NotNull(ring.Solid);
        Assert.Equal(0.04 * 8, Measure.Volume(ring.Solid!), 9);
        // A path lying in the face's plane sweeps nothing.
        Assert.Null(Solids.FollowMe(sq, [], [new(0, 0, 0), new(0, 3, 0)]).Solid);
    }

    [Fact]
    public void Follow_me_sweeps_a_face_region_along_a_drafted_line_and_guides_are_never_exported()
    {
        var (f, cube) = Cube();
        var south = new Vec3(0, -1, 0);
        // A line across the south side 0.2 below its top: the strip above it is the profile.
        f.Run("studio.onFace", new JsonObject { ["id"] = cube, ["point"] = J3(new(1, 0, 1)), ["normal"] = J3(south), ["open"] = true,
            ["outline"] = new JsonArray(J3(new(0, 0, 1.8)), J3(new(2, 0, 1.8))) });
        var plan = Id(f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey }));
        var path = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = new JsonObject { ["type"] = "line", ["a"] = J(1, 0), ["b"] = J(1, -1) } })!["ids"]![0]!;
        var r = f.Run("studio.followMe", new JsonObject
        {
            ["profile"] = new JsonObject { ["id"] = cube, ["point"] = J3(new(1, 0, 1.9)), ["normal"] = J3(south) },
            ["path"] = new JsonObject { ["id"] = path },
        })!;
        Assert.Equal("followMe", f.Model.Get(Id(r))!.Str("operation"));
        Assert.Equal(2 * 0.2 * 1, Vol(f, Id(r)), 6);
        var b = f.Bounds(Id(r));
        Assert.Equal(-1, b.Min.Y, 6); Assert.Equal(1.8, b.Min.Z, 6);

        // Guides: a line and a point, kept in the project, erased together, never in the IFC file.
        f.Run("studio.addGuide", new JsonObject { ["a"] = J3(new(0, 0, 1)), ["b"] = J3(new(2, 0, 1)) });
        f.Run("studio.addGuide", new JsonObject { ["a"] = J3(new(1, 1, 1)) });
        Assert.Equal(2, f.Model.OfClass(StudioModule.GuideClass).Count());
        var file = Path.Combine(Path.GetTempPath(), $"guides-{Guid.NewGuid():N}.ifc");
        try
        {
            Lapin.Ifc.IfcExport.Write(f.Session, file);
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Guide", text);
            Assert.DoesNotContain(StudioModule.GuideClass, text);
        }
        finally { File.Delete(file); }
        Assert.Equal(2, (int)f.Run("studio.clearGuides", new JsonObject())!["removed"]!);
        Assert.Empty(f.Model.OfClass(StudioModule.GuideClass));
    }
}
