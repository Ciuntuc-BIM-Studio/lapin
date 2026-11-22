using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Forum topics 104 and 102 (Enrique, an architect): a dog-leg concrete stair (two flights and a half landing) drawn as his
/// sketch asks. The handrail runs parallel to the pitch line at the railing's height h over the nosings, starts half a tread
/// before the first nosing (dropping there to its post on the floor), ends half a tread past the last one level at h over the
/// floor, and turns at the landing at h over it with no gap and never lower. The slab is continuous: each flight's soffit runs
/// into the landing's, with no notch, no step and no overlapping volume. The first flight may stand on a footing of its own.
/// And each railing is selected on its own (stairId#railing:side): its side, height and kind, deleted alone.
/// </summary>
public class StairDogLegTests
{
    const double H = 0.9;

    static (Fixture F, string Stair, string Type) DogLeg(Func<StairSpec, StairSpec>? change = null)
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        var spec = StairSpec.Default("Dog-leg") with { Shape = "u", Turn = "landing", Railing = new StairRailing("both", H, 0.04, 0.03, 1.2) };
        if (change != null) spec = change(spec);
        var type = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = spec.ToJson() })!["id"]!;
        var stair = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["x2"] = 1, ["y2"] = 0, ["turn"] = "left" })!["id"]!;
        return (f, stair, type);
    }

    static Resolved Solved(Fixture f, string stair) => StairBuilder.Resolve(f.Model, f.Model.Get(stair)!)!.Value.Resolved;

    static List<Tri> Tris(Solid s) => Measure.Triangles(s).Select(t => Tri.Make(s.Vertices[t.A], s.Vertices[t.B], s.Vertices[t.C])).ToList();

    /// <summary>A part's shells, each its triangles (a railing is many bars side by side, lapped at the joints).</summary>
    static List<List<Tri>> Shells(Solid s)
    {
        var root = Enumerable.Range(0, s.Vertices.Length).ToArray();
        int Find(int x) { while (root[x] != x) x = root[x] = root[root[x]]; return x; }
        foreach (var face in s.Faces) foreach (var v in face.Outer) root[Find(v)] = Find(face.Outer[0]);
        var tris = Measure.Triangles(s).ToList();
        return tris.GroupBy(t => Find(t.A)).Select(g => g.Select(t => Tri.Make(s.Vertices[t.A], s.Vertices[t.B], s.Vertices[t.C])).ToList()).ToList();
    }

    /// <summary>The lowest concrete over a point: a ray up from far below, its first hit on the flights and landings.</summary>
    static double Underside(ElementGeometry g, double x, double y)
    {
        var from = new Vec3(x, y, -10);
        var best = double.PositiveInfinity;
        foreach (var p in g.Parts.Where(p => p.Key!.StartsWith("flight:", StringComparison.Ordinal) || p.Key.StartsWith("landing:", StringComparison.Ordinal)))
            foreach (var t in Tris(p.Solid))
                if (Collisions.RayDistance(from, new Vec3(0, 0, 1), t) is { } d && d < best) best = d;
        return from.Z + best;
    }

    [Fact]
    public void The_dog_leg_s_slab_is_continuous_its_soffits_join_the_landing_s_with_no_notch_and_no_overlap()
    {
        var (f, id, _) = DogLeg();
        var r = Solved(f, id);
        Assert.Equal([9, 9], r.Flights.Select(x => x.Risers));
        var (h, b) = (r.RiserHeight, r.Tread);
        var spec = StairSpec.Default();
        var (fin, t, waist) = (spec.Finish.TreadThickness, spec.Construction.LandingThickness, spec.Construction.Waist);
        var wv = waist * Math.Sqrt(h * h + b * b) / b;
        var lz = 9 * h;                                   // the landing's finished top, 1.50
        var under = lz - fin - t;                         // its soffit, 1.32
        var uEnd = 8 * b;                                 // the first flight's last riser: the landing's edge
        Assert.Equal(1.5, lz, 9);
        Assert.Equal(1.32, under, 9);
        var g = f.Session.Geometry(id)!;

        // The first flight's soffit runs on under the landing until it meets the landing's soffit: there, past the landing's edge
        // by a tread and the difference of the waists, (9h − t + wv)·b/h − 8b; no notch at the landing's edge.
        var meet = (9 * h - t + wv) * b / h;
        Assert.Equal(b + (wv - t) * b / h, meet - uEnd, 9);
        Assert.InRange(meet - uEnd, b, b + 0.1);
        for (var x = 0.3; x < uEnd + r.LandingLength - 0.05; x += 0.0137)
        {
            var expected = Math.Min(x * h / b - wv - fin, under);
            Assert.Equal(expected, Underside(g, x, 0.21), 6);
        }
        // The second flight leaves the landing the same way: under the landing its soffit, then the flight's from where the two meet.
        var from = (wv - t) * b / h;                       // past the landing's edge, along the second flight
        Assert.True(from > 0);
        for (var x = uEnd + r.LandingLength - 0.05; x > 0.3; x -= 0.0137)
        {
            var u = uEnd - x;                              // along the second flight from its first riser
            var expected = u <= 0 ? under : Math.Max(under, lz + u * h / b - wv - fin);
            Assert.Equal(expected, Underside(g, x, 1.21), 6);
        }

        // No volume is in two parts: the flights and the landing meet face to face.
        var flight0 = Tris(g.Parts.Single(p => p.Key == "flight:0").Solid).ToArray();
        var flight1 = Tris(g.Parts.Single(p => p.Key == "flight:1").Solid).ToArray();
        var landing = Tris(g.Parts.Single(p => p.Key == "landing:0").Solid).ToArray();
        var inLanding = 0;
        for (var x = uEnd - 0.6; x < uEnd + 1.0; x += 0.0331)
            for (var y = -0.49; y < 1.5; y += 0.0457)
                for (var z = 1.0; z < 1.55; z += 0.0213)
                {
                    var p = new Vec3(x, y, z);
                    var inside = Collisions.Inside(p, landing);
                    if (inside) inLanding++;
                    Assert.False(inside && Collisions.Inside(p, flight0), $"{p} in the landing and the first flight");
                    Assert.False(inside && Collisions.Inside(p, flight1), $"{p} in the landing and the second flight");
                    Assert.False(Collisions.Inside(p, flight0) && Collisions.Inside(p, flight1), $"{p} in both flights");
                }
        Assert.True(inLanding > 100);
        // And the volumes add up as the profile says: the first flight gains the wedge under the landing.
        var wedge = 0.5 * (meet - uEnd) * ((under) - (uEnd * h / b - wv - fin)) * r.Width;
        var straight = StairBuilder.FlightProfile(r, r.Flights[0], fin, waist)!;
        Assert.Equal(Area(straight) * r.Width + wedge, Measure.Volume(g.Parts.Single(p => p.Key == "flight:0").Solid), 6);
    }

    static double Area(List<(double U, double Z)> ring)
    {
        var a = 0.0;
        for (var i = 0; i < ring.Count; i++) { var (p, q) = (ring[i], ring[(i + 1) % ring.Count]); a += p.U * q.Z - q.U * p.Z; }
        return Math.Abs(a) / 2;
    }

    [Fact]
    public void The_dog_leg_s_handrail_runs_at_h_over_the_nosings_from_half_a_tread_before_the_first_to_half_a_tread_past_the_last()
    {
        var (f, id, _) = DogLeg();
        var r = Solved(f, id);
        var (h, b, w) = (r.RiserHeight, r.Tread, r.Width);
        var stair = f.Model.Get(id)!;
        const double inset = 0.05;
        foreach (var side in new[] { "left", "right" })
        {
            var line = Assert.Single(StairBuilder.RailingLines(f.Model, stair, side));
            var y0 = side == "left" ? w / 2 - inset : -(w / 2 - inset);
            // It starts half a tread before the first nosing, on the pitch line (h/2 over the floor there).
            Assert.Equal(-b / 2, line[0].P.X, 9);
            Assert.Equal(y0, line[0].P.Y, 9);
            Assert.Equal(h / 2, line[0].Z, 9);
            // Up the first flight on the pitch line: the first nosing at h, the last (the landing's edge) at the landing.
            Assert.Equal((0.0, h), (line[1].P.X, line[1].Z));
            Assert.Equal(8 * b, line[2].P.X, 9);
            Assert.Equal(9 * h, line[2].Z, 9);
            // It ends half a tread past the last nosing, level with the floor above.
            var last = r.Flights[1];
            var top = last.Start + last.Dir * (8 * b + b / 2);
            Assert.Equal(top.X, line[^1].P.X, 9);
            Assert.Equal(3.0, line[^1].Z, 9);
            Assert.Equal(3.0, line[^2].Z, 9);
            Assert.Equal(last.Start.X - 8 * b, line[^2].P.X, 9);
            // Each point on a flight lies on its pitch line: h over the nosing line.
            foreach (var fl in r.Flights)
                foreach (var (p, z) in line)
                {
                    var u = Vec2.Dot(p - fl.Start, fl.Dir);
                    var v = Vec2.Dot(p - fl.Start, fl.Left);
                    if (Math.Abs(Math.Abs(v) - (w / 2 - inset)) < 1e-9 && u > 1e-9 && u < 8 * b - 1e-9) Assert.Equal(fl.Z0 + h + u * h / b, z, 9);
                }
            // Over the landing it never runs under the landing's level (its handrail never under h), and it steps up onto the second
            // flight half a tread before its first nosing: the same point twice, from the landing's level to the pitch line (h/2 up).
            var landing = r.Landings[0].Outline;
            Assert.All(line.Where(x => Polygon2.Contains(landing, x.P)), x => Assert.True(x.Z >= 9 * h - 1e-9, $"{x} under the landing's level"));
            var step = Enumerable.Range(0, line.Count - 1).Single(i => (line[i + 1].P - line[i].P).Length < 1e-9);
            Assert.Equal(9 * h, line[step].Z, 9);
            Assert.Equal(9 * h + h / 2, line[step + 1].Z, 9);
            Assert.Equal(8 * b + b / 2, line[step].P.X, 9);
            var s2 = r.Flights[1].Start + r.Flights[1].Left * ((side == "left" ? 1 : -1) * (w / 2 - inset));
            Assert.Equal(s2.Y, line[step].P.Y, 9);
            // Round the outside of the turn (the right side, turning left) it follows the landing's far edge; inside, it crosses the
            // well level, half a tread in.
            if (side == "right") Assert.Contains(line, x => Math.Abs(x.P.X - (8 * b + r.LandingLength - inset)) < 1e-9 && Math.Abs(x.Z - 9 * h) < 1e-9);
            else Assert.Contains(line, x => Math.Abs(x.P.X - (8 * b + b / 2)) < 1e-9 && Math.Abs(x.P.Y - y0) < 1e-9 && Math.Abs(x.Z - 9 * h) < 1e-9);
        }

        // The handrail itself: a bar at h over every point of the line, with no gap at the joints (sampled every 5 mm).
        var g = f.Session.Geometry(id)!;
        foreach (var side in new[] { "left", "right" })
        {
            var shells = Shells(g.Parts.Single(p => p.Key == $"railing:{side}").Solid).Select(x => x.ToArray()).ToList();
            var line = StairBuilder.RailingLines(f.Model, stair, side)[0];
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var (a, c) = (new Vec3(line[i].P.X, line[i].P.Y, line[i].Z + H), new Vec3(line[i + 1].P.X, line[i + 1].P.Y, line[i + 1].Z + H));
                var n = Math.Max(2, (int)Math.Ceiling((c - a).Length / 0.005));
                for (var k = 0; k <= n; k++)
                {
                    var q = a + (c - a) * ((double)k / n) + new Vec3(0.0011, 0.0007, 0.0003);
                    Assert.True(shells.Any(sh => Collisions.Inside(q, sh)), $"{side}: the handrail has a gap at {q}");
                }
            }
            // The first post stands on the floor half a tread before the first riser and reaches the handrail there (h + h/2).
            var y0 = side == "left" ? w / 2 - inset : -(w / 2 - inset);
            var post = shells.Select(sh => sh.SelectMany(x => new[] { x.A, x.B, x.C }).ToList())
                .Where(v => v.Max(p => p.X) - v.Min(p => p.X) < 0.05 && v.Max(p => p.Y) - v.Min(p => p.Y) < 0.05 && v.Max(p => p.Z) - v.Min(p => p.Z) > 0.5)
                .Single(v => Math.Abs(v.Average(p => p.X) + b / 2) < 1e-6 && Math.Abs(v.Average(p => p.Y) - y0) < 1e-6);
            Assert.Equal(0, post.Min(p => p.Z), 6);
            Assert.Equal(h / 2 + H, post.Max(p => p.Z), 6);
        }
    }

    [Fact]
    public void A_dog_leg_may_stand_on_a_footing_of_its_own_under_its_first_tread()
    {
        var (f, id, _) = DogLeg(s => s with { Construction = s.Construction with { FootingDepth = 0.5, FootingWidth = 0.3 } });
        var g = f.Session.Geometry(id)!;
        var footing = g.Parts.Single(p => p.Key == "footing");
        var (min, max) = Measure.Bounds(footing.Solid)!.Value;
        Assert.Equal((0.0, 0.3), (Math.Round(min.X, 9), Math.Round(max.X, 9)));
        Assert.Equal(-0.03, max.Z, 9);              // under the flight's concrete, a finish under the floor
        Assert.Equal(-0.53, min.Z, 9);
        Assert.Equal(1.0 * 0.3 * 0.5, Measure.Volume(footing.Solid), 9);
        // Without one, none.
        var (f2, id2, _) = DogLeg();
        Assert.DoesNotContain(f2.Session.Geometry(id2)!.Parts, p => p.Key == "footing");
    }

    static List<string> MarkIds(Fixture f) =>
        ((JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = "plan:" + f.Storey })!)["marks"]!.AsArray().Select(m => (string)m!["id"]!).ToList();

    [Fact]
    public void Each_railing_of_a_stair_is_selected_changed_and_deleted_on_its_own()
    {
        var (f, id, _) = DogLeg();
        var left = StairBuilder.RailingId(id, "left");
        var right = StairBuilder.RailingId(id, "right");
        Assert.Equal((id, "left"), StairBuilder.ParseRailingId(left));
        Assert.Null(StairBuilder.ParseRailingId(id));
        Assert.Null(StairBuilder.ParseRailingId(id + "#railing:middle"));
        // In plan each railing is a mark of its own, which a click selects.
        var marks = MarkIds(f);
        Assert.Contains(left, marks);
        Assert.Contains(right, marks);

        // The right one higher, the left a handrail on the wall: brackets, no posts.
        f.Run("architecture.updateStairRailing", new JsonObject { ["id"] = right, ["height"] = 1.1 });
        f.Run("architecture.updateStairRailing", new JsonObject { ["id"] = left, ["kind"] = "handrail" });
        var stair = f.Model.Get(id)!;
        var spec = StairBuilder.SpecOf(f.Model, stair);
        Assert.Equal(1.1, StairBuilder.RailingOn(stair, spec, "right")!.Value.Rail.Height, 9);
        Assert.Equal("balustrade", StairBuilder.RailingOn(stair, spec, "right")!.Value.Kind);
        Assert.Equal(H, StairBuilder.RailingOn(stair, spec, "left")!.Value.Rail.Height, 9);
        Assert.Equal("handrail", StairBuilder.RailingOn(stair, spec, "left")!.Value.Kind);
        var g = f.Session.Geometry(id)!;
        var r = Solved(f, id);
        // Its posts reach 1.10 m over what they stand on: the last, on the floor above, to 4.10.
        var rightPosts = Shells(g.Parts.Single(p => p.Key == "railing:right").Solid).Select(sh => sh.SelectMany(x => new[] { x.A, x.B, x.C }).ToList())
            .Where(v => v.Max(p => p.X) - v.Min(p => p.X) < 0.05 && v.Max(p => p.Y) - v.Min(p => p.Y) < 0.05 && v.Max(p => p.Z) - v.Min(p => p.Z) > 0.5).ToList();
        Assert.Equal(3.0 + 1.1, rightPosts.Max(v => v.Max(p => p.Z)), 6);
        var leftPart = g.Parts.Single(p => p.Key == "railing:left");
        Assert.Contains(leftPart.Properties!, x => x == ("Kind", "handrail"));
        // A wall handrail stands on nothing: its lowest point is a bracket under the bar, not a post on a tread.
        var leftMin = Measure.Bounds(leftPart.Solid)!.Value.Min.Z;
        Assert.True(leftMin > r.RiserHeight + 0.3, $"the wall handrail reaches down to {leftMin}");

        // To IFC: two IfcRailing in the stair, a balustrade and a handrail, whose GlobalIds stay when the stair is rebuilt.
        string[] Rails()
        {
            var path = Path.Combine(Path.GetTempPath(), $"lapin-dogleg-{Guid.NewGuid():N}.ifc");
            try
            {
                IfcExport.Write(f.Session, path);
                return StepFile.Read(path).OfType("IFCRAILING").Select(e => $"{e[0]} {((StepEnum)e[8]!).Value}").OrderBy(x => x).ToArray();
            }
            finally { File.Delete(path); }
        }
        var before = Rails();
        Assert.True(before.Length == 2, string.Join(" | ", before));
        Assert.True(before.Any(x => x.EndsWith("BALUSTRADE", StringComparison.Ordinal)), string.Join(" | ", before));
        Assert.Contains(before, x => x.EndsWith("HANDRAIL", StringComparison.Ordinal));
        f.Run("architecture.updateStair", new JsonObject { ["id"] = id, ["width"] = 1.2 });
        Assert.Equal(before, Rails());

        // Delete on the left railing: the stair keeps the right one, and forgets the left's own settings; undone, it comes back.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(left) });
        stair = f.Model.Get(id)!;
        Assert.Equal("right", stair.Str("railingSides"));
        Assert.Null(StairBuilder.RailingOn(stair, spec, "left"));
        Assert.Null((stair.Attrs["railings"] as JsonObject)?["left"]);
        Assert.DoesNotContain(f.Session.Geometry(id)!.Parts, p => p.Key == "railing:left");
        Assert.DoesNotContain(left, MarkIds(f));
        f.Session.Undo();
        Assert.Contains(f.Session.Geometry(id)!.Parts, p => p.Key == "railing:left");
        // Put off and back on from its own settings.
        f.Run("architecture.updateStairRailing", new JsonObject { ["id"] = id, ["side"] = "right", ["on"] = false });
        Assert.Equal("left", f.Model.Get(id)!.Str("railingSides"));
        f.Run("architecture.updateStairRailing", new JsonObject { ["id"] = id, ["side"] = "right", ["on"] = true });
        Assert.Equal("both", f.Model.Get(id)!.Str("railingSides"));
        // A setting set to null is the type's again.
        f.Run("architecture.updateStairRailing", new JsonObject { ["id"] = right, ["height"] = null });
        Assert.Null((f.Model.Get(id)!.Attrs["railings"] as JsonObject)?["right"]);
        Assert.Throws<CommandException>(() => f.Run("architecture.updateStairRailing", new JsonObject { ["id"] = id + "#railing:middle", ["height"] = 1.0 }));
    }

    [Fact]
    public void A_project_saved_before_builds_its_stairs_with_both_railings_as_before_without_settings_of_their_own()
    {
        // A stair with no `railings` of its own (every stair saved before) takes the type's railing on both sides, as balustrades.
        var (f, id, _) = DogLeg();
        var stair = f.Model.Get(id)!;
        Assert.Null(stair.Attrs["railings"]);
        var spec = StairBuilder.SpecOf(f.Model, stair);
        foreach (var side in new[] { "left", "right" })
        {
            var (rail, kind) = StairBuilder.RailingOn(stair, spec, side)!.Value;
            Assert.Equal(spec.Railing with { Sides = "both" }, rail);
            Assert.Equal("balustrade", kind);
        }
    }

    [Fact]
    public void The_railing_length_is_the_3D_length_of_the_railings_built_landing_turn_and_extensions_included()
    {
        // Audit 22.11.2026 §3.6: the quantity summed the flights' pitch only, 2.4 m or more short at a landing.
        var (f, id, _) = DogLeg();
        var stair = f.Model.Get(id)!;
        var r = Solved(f, id);
        var built = 0.0;
        foreach (var side in new[] { "left", "right" })
            foreach (var line in StairBuilder.RailingLines(f.Model, stair, side))
                for (var k = 0; k + 1 < line.Count; k++)
                    built += Math.Sqrt((line[k + 1].P - line[k].P).Length * (line[k + 1].P - line[k].P).Length + (line[k + 1].Z - line[k].Z) * (line[k + 1].Z - line[k].Z));
        var pitchOnly = 2 * r.Flights.Sum(fl => (fl.Risers - 1) * Math.Sqrt(r.Tread * r.Tread + r.RiserHeight * r.RiserHeight));
        var q = f.Session.Quantities(id).Single(s => s.Name == "Lapin_StairQuantities").Quantities.Single(x => x.Name == "RailingLength").Value;
        Assert.Equal(built, q, 9);
        Assert.True(q > pitchOnly + 2 * r.Tread, $"{q} vs the pitch alone {pitchOnly}");
    }
}
