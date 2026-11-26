using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// A door's or window's details in a curved wall (forum topic 288): the reveal's finish with its jambs on the lines across the
/// wall (radial, where the hole is cut) and its head following the arc, at the wall's real faces; the sills and the surround
/// along the arc; the wall cut to make room; measured along the arc. A straight wall's details come out bit for bit as before.
/// </summary>
public class CurvedRevealTests(ITestOutputHelper output)
{
    const double R = 5, T = 0.28, Plaster = 0.015;

    static string Plastered(Fixture f) => f.Type("Plastered brick", ("Plaster", Plaster, false), ("Brick", 0.25, true), ("Plaster", Plaster, false));

    /// <summary>A quarter circle of radius 5 round the origin, from (5, 0) to (0, 5), bulging away from the centre (its right, the exterior, outside).</summary>
    static string QuarterWall(Fixture f, string type)
    {
        var rise = -R * (1 - Math.Cos(Math.PI / 4));
        return (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = R, ["y1"] = 0, ["x2"] = 0, ["y2"] = R, ["arc"] = rise, ["typeId"] = type })!["id"]!;
    }

    static GeometryPart? Part(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.FirstOrDefault(p => p.Key == key);

    static double Radius(Vec3 v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);
    static double Angle(Vec3 v) => Math.Atan2(v.Y, v.X);

    /// <summary>A sector's area as the chord rule lays it: <paramref name="n"/> chords over the angle.</summary>
    static double ChordedSector(double r, double angle, int n) => n * 0.5 * r * r * Math.Sin(angle / n);

    [Fact]
    public void A_door_in_a_quarter_circle_wall_of_radius_5_gets_its_reveal_radial_at_the_jambs_and_curved_over_the_head()
    {
        var f = new Fixture();
        var wall = QuarterWall(f, Plastered(f));
        var w = f.Model.Get(wall)!;
        var c = PathCurve.Of(w);
        Assert.Equal(R, c.Radius, 9);
        Assert.Equal(0, c.Centre.Length, 9);
        var along = c.Length / 2; // at 45°
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = along })!["id"]!;
        var spec = Openings.SpecOf(f.Model, door)!;
        Assert.Equal(0.9, spec.Width, 9);
        Assert.Equal(2.1, spec.Height, 9);
        var placed = Openings.Place(f.Model, Openings.OpeningOf(f.Model, door)!);
        var z0 = Spatial.Base(f.Model, w) + placed.FillSill;

        var (yFE, yFI) = (spec.Frame.Offset + spec.Frame.Depth / 2, spec.Frame.Offset - spec.Frame.Depth / 2);
        var phi = spec.Width / 2 / R; // half the opening's angle: its sides on the radii at 45° ± φ
        var mid = Math.PI / 4;
        var n = Chords.Count(R, 2 * phi);
        // The exterior (the wall's right, away from the centre) at 5.14, the interior at 4.86; the frame flat, its faces on lines
        // square to the 45° radius, R + y from the centre.
        foreach (var (key, flat, face) in new[] { ("reveal:outside", yFE, R + T / 2), ("reveal:inside", yFI, R - T / 2) })
        {
            var part = Part(f, door, key)!;
            Assert.Equal("Plaster", f.Model.Get(part.Material!)!.Name);
            var df = R + flat;
            var depth = Math.Abs(face - df / Math.Cos(phi)); // along the jamb's radius, from the frame's plane to the face
            var head = Math.Abs(ChordedSector(face, 2 * phi, n) - df * df * Math.Tan(phi)); // the soffit in plan: the arc's sector less the frame's triangle
            var expected = 2 * Plaster * depth * (spec.Height + Plaster) + Plaster * head;
            output.WriteLine($"{key}: depth {depth * 1000:0.000} mm, soffit {head:0.000000} m², volume {Measure.Volume(part.Solid):0.000000000} m³ (expected {expected:0.000000000})");
            Assert.Equal(expected, Measure.Volume(part.Solid), 9);
            var b = Measure.Bounds(part.Solid)!.Value;
            Assert.Equal(z0, b.Min.Z, 9);
            Assert.Equal(z0 + spec.Height + Plaster, b.Max.Z, 9);
            // What it leaves clear: nothing of it inside the radii at 45° ± φ under the head, so the opening stays 900 wide on the axis.
            var vs = part.Solid.Vertices;
            var under = vs.Where(v => v.Z < z0 + spec.Height - 1e-9).ToList();
            Assert.All(under, v => Assert.True(Math.Abs(Angle(v) - mid) >= phi - 1e-9));
            Assert.Contains(under, v => Math.Abs(Math.Abs(Angle(v) - mid) - phi) < 1e-9);
            // Out to the wall's face, on the arc.
            Assert.Contains(vs, v => Math.Abs(Radius(v) - face) < 1e-9 && Math.Abs(Angle(v) - mid) < phi);
        }
        // The quantities: the jambs' radial faces and the soffit.
        var q = f.Session.Quantities(door).Single(s => s.Name == "Lapin_Details").Quantities.ToDictionary(x => x.Name, x => x.Value);
        var dOut = R + T / 2 - (R + yFE) / Math.Cos(phi);
        Assert.Equal(2 * spec.Height * dOut + (ChordedSector(R + T / 2, 2 * phi, n) - (R + yFE) * (R + yFE) * Math.Tan(phi)), q["RevealAreaOutside"], 9);
    }

    [Fact]
    public void The_curved_wall_gives_way_to_the_reveal_and_keeps_its_core_at_the_hole()
    {
        var f = new Fixture();
        var wall = QuarterWall(f, Plastered(f));
        var w = f.Model.Get(wall)!;
        var c = PathCurve.Of(w);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = c.Length / 2 })!["id"]!;
        var booleans = f.Session.Catalog.Get<ISolidBooleans>()!;
        var wallParts = f.Session.Geometry(wall)!.Parts;
        foreach (var key in new[] { "reveal:outside", "reveal:inside" })
        {
            var reveal = Part(f, door, key)!.Solid;
            // Nothing of the wall's layers where the finish is (the lintel over the hole is not cut by a door's details, on a straight wall neither).
            foreach (var wp in wallParts.Where(p => p.Key?.StartsWith("lintel", StringComparison.Ordinal) != true))
                Assert.True((booleans.Intersection(wp.Solid, reveal) is { } x ? Measure.Volume(x) : 0) < 1e-9, $"{wp.Name} overlaps {key}");
        }
        // The plaster gives up the finish's room each side; the brick only round the frame's own hole (and behind the skins).
        var plain = new Fixture();
        var bare = QuarterWall(plain, Plastered(plain));
        plain.Run("architecture.addWallOpening", new JsonObject { ["wallId"] = bare, ["along"] = c.Length / 2, ["sill"] = 0, ["width"] = 0.9, ["height"] = 2.1 });
        double Volume(Fixture x, string id, string name) => x.Session.Geometry(id)!.Parts.Where(p => p.Name == name).Sum(p => Measure.Volume(p.Solid));
        Assert.True(Volume(f, wall, "Plaster") < Volume(plain, bare, "Plaster") - 1e-4);
        var revealSolids = new[] { Part(f, door, "reveal:outside")!.Solid, Part(f, door, "reveal:inside")!.Solid };
        var reveals = revealSolids.Sum(Measure.Volume);
        // What the layers lost beyond the bare hole is the reveal's volume, less what the lintel over the hole had already taken of it.
        var lintels = wallParts.Where(p => p.Key?.StartsWith("lintel", StringComparison.Ordinal) == true).ToList();
        var inLintel = revealSolids.Sum(r => lintels.Sum(l => booleans.Intersection(l.Solid, r) is { } x ? Measure.Volume(x) : 0));
        var lost = Volume(plain, bare, "Plaster") + Volume(plain, bare, "Brick") - Volume(f, wall, "Plaster") - Volume(f, wall, "Brick");
        output.WriteLine($"reveals {reveals:0.000000000} m³ ({inLintel:0.000000000} under the lintel), the layers lost beyond the hole {lost:0.000000000} m³");
        // (to 2 cm³: the reveal's head lays the face's arc in its own chords, the wall in its own, each within a millimetre of it)
        Assert.True(Math.Abs(reveals - inLintel - lost) < 2e-6);
    }

    [Fact]
    public void A_window_in_a_curved_wall_gets_its_sills_and_surround_along_the_arc()
    {
        var f = new Fixture();
        var wall = QuarterWall(f, Plastered(f));
        var c = PathCurve.Of(f.Model.Get(wall)!);
        var details = new JoineryDetails(
            SillInside: new JoinerySill(true, 0.04, 0.03, 0.02, 0, "Timber"),
            SillOutside: new JoinerySill(true, 0.05, 0.02, 0.03, 10, "Granite"),
            Surround: new JoinerySurround(true, 0.1, 0.02, true, "Render"),
            Shutters: new JoineryShutters(true));
        var typeId = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultWindow("Dressed") with { Details = details }).ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = c.Length / 2, ["typeId"] = typeId })!["id"]!;
        var spec = Openings.SpecOf(f.Model, window)!;
        var (ext, inte) = (R + T / 2, R - T / 2);
        var mid = Math.PI / 4;
        var phi = spec.Width / 2 / R;

        // The exterior sill out past the face by its projection (5.14 + 0.05), its ends 30 mm past the jambs along the axis.
        var outside = Part(f, window, "sill:outside")!;
        Assert.Equal(ext + 0.05, outside.Solid.Vertices.Max(Radius), 9);
        Assert.Equal(phi + 0.03 / R, outside.Solid.Vertices.Max(v => Math.Abs(Angle(v) - mid)), 9);
        // Its front on the arc, in chords: every vertex out past the face is on it, at more stations than a flat sill's two ends.
        var front = outside.Solid.Vertices.Where(v => Radius(v) > ext + 1e-6).ToList();
        Assert.All(front, v => Assert.Equal(ext + 0.05, Radius(v), 9));
        Assert.True(front.Select(v => Math.Round(Angle(v), 9)).Distinct().Count() > 2);
        // The interior one in by its projection, from the frame's plane.
        var inside = Part(f, window, "sill:inside")!;
        Assert.Equal(inte - 0.04, inside.Solid.Vertices.Min(Radius), 9);
        // The surround on the facade, 20 mm proud of it, its inner edges on the opening's radii.
        var surround = Part(f, window, "surround")!;
        Assert.Equal(ext + 0.02, surround.Solid.Vertices.Max(Radius), 9);
        Assert.Equal(ext, surround.Solid.Vertices.Min(Radius), 9);
        Assert.Equal(phi + 0.1 / R, surround.Solid.Vertices.Max(v => Math.Abs(Angle(v) - mid)), 9);
        // Flat pieces (shutters) are left off a curved facade, in 3D and in plan.
        Assert.Null(Part(f, window, "shutters"));
        // Measured along the face each is laid on.
        var q = f.Session.Quantities(window).Single(s => s.Name == "Lapin_Details").Quantities.ToDictionary(x => x.Name, x => x.Value);
        Assert.Equal((spec.Width + 0.06) * ext / R, q["SillOutsideLength"], 9);
        Assert.Equal((spec.Width + 0.04) * inte / R, q["SillInsideLength"], 9);
        Assert.Equal(((spec.Width + 0.2) * (spec.Height + 0.2) - spec.Width * spec.Height) * ext / R, q["SurroundArea"], 9);
        Assert.False(q.ContainsKey("ShutterArea"));
        // The wall gives way to the sills.
        var booleans = f.Session.Catalog.Get<ISolidBooleans>()!;
        foreach (var wp in f.Session.Geometry(wall)!.Parts)
            foreach (var s in new[] { outside, inside })
            {
                // (to a sliver of 0.1 cm³: the sloped sill's faces between its stations are split as the wall's are not)
                var v = booleans.Intersection(wp.Solid, s.Solid) is { } x ? Measure.Volume(x) : 0;
                Assert.True(v < 1e-7, $"{wp.Name} overlaps {s.Key} by {v:E3} m³");
            }
    }

    [Fact]
    public void The_curved_walls_reveal_is_cut_in_plans_and_sections_and_goes_to_ifc()
    {
        var f = new Fixture();
        var wall = QuarterWall(f, Plastered(f));
        var c = PathCurve.Of(f.Model.Get(wall)!);
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = c.Length / 2 })!["id"]!;
        var parts = f.Session.Geometry(door)!.Parts;
        Assert.Contains(parts, p => p.Key == "reveal:outside");
        // IFC: the door's body carries the two finishes, as on a straight wall (a shell each, beside the door's own).
        static int Shells(Fixture x)
        {
            using var ms = new MemoryStream();
            Lapin.Ifc.IfcExport.Write(x.Session, ms, "x.ifc", new Lapin.Ifc.IfcExportOptions());
            var ifc = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Contains("IFCDOOR(", ifc);
            return ifc.Split("IFCCLOSEDSHELL(").Length - 1;
        }
        var g = new Fixture();
        var gw = QuarterWall(g, Plastered(g));
        var noneType = (string)g.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultDoor("Unlined") with { Details = new JoineryDetails(new JoineryReveal("none")) }).ToJson() })!["id"]!;
        g.Run("architecture.addDoor", new JsonObject { ["wallId"] = gw, ["along"] = c.Length / 2, ["typeId"] = noneType });
        Assert.Equal(Shells(g) + 2, Shells(f));
    }

    // ------------------------------------------------ a straight wall: as before, bit for bit

    /// <summary>Every number a door's or window's geometry, its wall's and its quantities give, hashed.</summary>
    static string Fingerprint(Fixture f, params string[] ids)
    {
        var sb = new StringBuilder();
        static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        foreach (var id in ids)
        {
            var g = f.Session.Geometry(id)!;
            foreach (var p in g.Parts)
            {
                sb.Append(p.Key).Append('|').Append(p.Name).Append('|').Append(p.Color).Append('|').Append(f.Model.Get(p.Material ?? "")?.Name).Append('\n');
                foreach (var v in p.Solid.Vertices) sb.Append(N(v.X)).Append(',').Append(N(v.Y)).Append(',').Append(N(v.Z)).Append(';');
                foreach (var face in p.Solid.Faces) sb.Append(string.Join(",", face.Outer)).Append('/');
                sb.Append('\n');
            }
            foreach (var item in g.Plan ?? []) { sb.Append(item.Kind).Append(item.Closed).Append(':'); foreach (var q in item.Points) sb.Append(N(q.X)).Append(',').Append(N(q.Y)).Append(';'); sb.Append('\n'); }
            foreach (var s in f.Session.Quantities(id))
                foreach (var q in s.Quantities) sb.Append(s.Name).Append('.').Append(q.Name).Append('=').Append(N(q.Value)).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    [Fact]
    public void A_straight_walls_details_are_the_same_as_before_bit_for_bit()
    {
        using var _ = Fixture.Seeded(288);
        var f = new Fixture();
        var type = f.Type("Rendered", ("Render", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var wall = f.Wall(0, 0, 6, 0, type);
        var details = new JoineryDetails(
            SillInside: new JoinerySill(true, 0.04, 0.03, 0.02, 0, "Timber"),
            SillOutside: new JoinerySill(true, 0.05, 0.02, 0.03, 10, "Granite"),
            Surround: new JoinerySurround(true, 0.1, 0.02, true, "Render"),
            Shutters: new JoineryShutters(true));
        var typeId = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultWindow("Dressed") with { Details = details }).ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 4, ["typeId"] = typeId })!["id"]!;
        f.Run("architecture.flipJoinery", new JsonObject { ["ids"] = new JsonArray(window), ["axis"] = "x" });
        var doorType = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultDoor("Stepped") with { Details = new JoineryDetails(Threshold: new JoineryThreshold(true, 0.03, "Granite")) }).ToJson() })!["id"]!;
        var door = (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1.2, ["typeId"] = doorType })!["id"]!;
        var print = Fingerprint(f, window, door, wall);
        output.WriteLine(print);
        Assert.Equal(StraightPrint, print);
    }

    /// <summary>Taken from the build before the curved walls' details (forum topic 288).</summary>
    const string StraightPrint = "C79A93E4E967EAE91425EBF3CAFFEE0EFBEF6880DD9EB1A5B9B94B3939970558";
}
