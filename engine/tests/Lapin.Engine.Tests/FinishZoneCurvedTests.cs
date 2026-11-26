using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The finish painter on a curved wall (forum topic 288, docs/plan-finish-painter.md): a face of an arc wall painted, its
/// zone laid in the face's developed frame (u the length along the face, v up it, w out of it), its layers swept round the
/// curve by their thickness, chorded by the one rule (<see cref="Chords"/>), its openings cut out, measured on the face
/// itself, exported as IfcCovering, split, and following its wall.
/// </summary>
public class FinishZoneCurvedTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-finish-curved-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    // The default wall type: render 20 mm on the right, a 250 mm core, plaster 15 mm on the left.
    const double Left = 0.125 + 0.015, Right = 0.125 + 0.02, H = 3.0, R = 5.0;
    const double Etics = 0.005 + 0.10 + 0.01;
    static readonly double Quarter = Math.PI / 2, L = R * Quarter;
    // The left face looks into the arc's centre (its radius the axis's less the plaster's side), the right face out of it.
    static readonly double RLeft = R - Left, RRight = R + Right;

    static string Library(Fixture f, string name) => (string)f.Run("architecture.surfaceFinishTypeFromLibrary", new JsonObject { ["name"] = name })!["id"]!;

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    static double Material(Fixture f, string id, string material) =>
        f.Session.Geometry(id)!.Parts.Where(p => f.Model.Get(p.Material ?? "")?.Name == material).Sum(p => Measure.Volume(p.Solid));

    static double Quantity(Fixture f, string id, string name) =>
        f.Session.Quantities(id).Single(q => q.Name == "Qto_CoveringBaseQuantities").Quantities.Single(q => q.Name == name).Value;

    static double Net(Fixture f, string id) => Quantity(f, id, "NetArea");

    static JsonArray P(double x, double y, double z) => new(x, y, z);

    /// <summary>A point on the circle of <paramref name="radius"/> round the origin, <paramref name="degrees"/> from +x, at z.</summary>
    static JsonArray Polar(double radius, double degrees, double z, double dx = 0, double dy = 0)
    {
        var a = degrees * Math.PI / 180;
        return P(dx + radius * Math.Cos(a), dy + radius * Math.Sin(a), z);
    }

    /// <summary>A quarter circle of radius 5 round the origin, from (5, 0) to (0, 5), counter-clockwise (bulging to the right of its chord), 3 m high.</summary>
    static string ArcWall(Fixture f) => (string)f.Run("architecture.addWall", new JsonObject
    {
        ["storeyId"] = f.Storey, ["x1"] = R, ["y1"] = 0, ["x2"] = 0, ["y2"] = R, ["arc"] = -R * (1 - Math.Cos(Math.PI / 4)), ["height"] = H,
    })!["id"]!;

    /// <summary>A 900 × 2100 door half way along the arc.</summary>
    static Openings.Placed Door(Fixture f, string wall)
    {
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = L / 2, ["width"] = 0.9, ["height"] = 2.1 });
        var door = Openings.PlacedOn(f.Model, wall).Single();
        Assert.Equal(0.9, door.Width, 6);
        Assert.Equal(2.1, door.Height, 6);
        return door;
    }

    /// <summary>
    /// The opening as the face shows it: a curved wall's opening is cut radially (<see cref="Openings.ArcCutter"/>), so on a
    /// face of <paramref name="radius"/> it is as wide as the door scaled to that radius.
    /// </summary>
    static double HoleOn(double radius, Openings.Placed door, double height) => door.Width * radius / R * height;

    static double Radius(Vec3 v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);

    [Fact]
    public void A_curved_walls_outer_face_painted_with_etics_follows_the_curve_net_of_its_door_and_goes_to_ifc_as_a_cladding()
    {
        var f = new Fixture();
        var wall = ArcWall(f);
        var door = Door(f, wall);
        var etics = Library(f, "ETICS EPS 100");
        var zone = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Right", ["whole"] = true, ["finishType"] = etics })!["id"]!;
        var e = f.Model.Get(zone)!;
        Assert.Equal("Face.Right", e.Str("face"));
        Assert.Equal("CLADDING", e.PredefinedType);
        Assert.Equal("IfcCovering", e.Str("exportAs"));

        // Measured on the face itself: its developed length (the arc at the face's radius) by its height, the door out.
        var gross = Quarter * RRight * H;
        var net = gross - HoleOn(RRight, door, door.Height);
        Assert.Equal(gross, Quantity(f, zone, "GrossArea"), 6);
        Assert.Equal(net, Net(f, zone), 6);

        // Its layers swept round the curve from the face outward, each a ring's sector less the door's.
        var g = f.Session.Geometry(zone)!;
        Assert.Null(g.Diagnostics);
        var verts = g.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.All(verts, v => Assert.InRange(Radius(v), RRight - Chords.Tolerance - 1e-9, RRight + Etics + 1e-9));
        Assert.Equal(RRight + Etics, verts.Max(Radius), 6);
        Assert.InRange(verts.Min(Radius), RRight - Chords.Tolerance - 1e-9, RRight + 1e-9);
        Assert.Equal(0, f.Bounds(zone).Min.Z, 6);
        Assert.Equal(H, f.Bounds(zone).Max.Z, 6);
        // Chorded by the one rule, as the wall itself: its corners on the wall's stations, and at the door's sides.
        var stations = Chords.Count(R, Quarter) + 1;
        var angles = verts.Select(v => Math.Round(Math.Atan2(v.Y, v.X), 5)).Distinct().Count();
        Assert.Equal(stations + 2, angles);
        double Ring(double r0, double r1, double h) => (r1 * r1 - r0 * r0) / 2 * h;
        var doorAngle = door.Width / R;
        double Layer(double d0, double t) => Ring(RRight + d0, RRight + d0 + t, Quarter * H) - Ring(RRight + d0, RRight + d0 + t, doorAngle * door.Height);
        // The chords' few hundredths of a percent short of the ring's sector (a chord turning Δθ keeps 1 − Δθ²/6 of its arc's segment).
        static void Close(double expected, double actual) => Assert.True(Math.Abs(expected - actual) / expected < 5e-4, $"{actual:0.######} is not {expected:0.######}");
        Close(Layer(0.005, 0.10), Material(f, zone, "EPS insulation"));
        Close(Layer(0, Etics), Volume(f, zone));

        // Each layer measured by material, on the face's area.
        var layers = f.Session.Quantities(zone).Where(q => q.Name == "Lapin_FinishLayerQuantities").ToList();
        Assert.Equal(["Adhesive mortar", "EPS insulation", "Render"], layers.Select(q => q.PartName!));
        Assert.All(layers, q => Assert.Equal(net, q.Quantities.Single(x => x.Name == "NetArea").Value, 6));
        Assert.Equal(Material(f, zone, "EPS insulation"), layers[1].Quantities.Single(x => x.Name == "NetVolume").Value, 6);

        // IFC: a covering, CLADDING, covering its wall.
        var path = Path.Combine(_dir, "curved.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        var covering = Assert.Single(step.OfType("IFCCOVERING"));
        Assert.Equal(new StepEnum("CLADDING"), covering[8]);
        var rel = Assert.Single(step.OfType("IFCRELCOVERSBLDGELEMENTS"));
        Assert.Equal(step.OfType("IFCWALL").Single().Id, ((StepRef)rel[4]!).Id);
    }

    [Fact]
    public void A_curved_walls_inner_face_is_picked_split_and_painted_and_its_zones_follow_the_wall_and_undo()
    {
        var f = new Fixture();
        var wall = ArcWall(f);
        var door = Door(f, wall);

        // Picked in 3D (a point on the face, the normal into the centre) and in a plan (x, y only).
        var face = (JsonObject)f.Session.Query("architecture.finishFaceAt", new JsonObject { ["id"] = wall, ["point"] = Polar(RLeft, 20, 0.5), ["normal"] = Polar(-1, 20, 0) })!;
        Assert.Equal("Face.Left", (string?)face["role"]);
        // Lit along the curve: the face's outline follows it, every point on the face.
        var outline = ((JsonArray)face["face"]!).Select(n => new Vec3((double)n![0]!, (double)n[1]!, (double)n[2]!)).ToList();
        Assert.True(outline.Count > 8);
        Assert.All(outline, v => Assert.Equal(RLeft, Radius(v), 2));
        var plan = (JsonObject)f.Session.Query("architecture.finishFaceAt", new JsonObject { ["id"] = wall, ["point"] = new JsonArray(RRight * Math.Cos(0.3), RRight * Math.Sin(0.3)) })!;
        Assert.Equal("Face.Right", (string?)plan["role"]);

        // Split at 1.2 m: tiles below (picked on the face), plaster above.
        f.Run("architecture.splitFinishFace", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Left", ["height"] = 1.2 });
        var tiles = Library(f, "Wall tiles");
        var plaster = Library(f, "Lime plaster 25");
        var low = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["point"] = Polar(RLeft, 20, 0.5), ["normal"] = Polar(-1, 20, 0), ["finishType"] = tiles })!["id"]!;
        Assert.Equal("Face.Left", f.Model.Get(low)!.Str("face"));
        var rest = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["face"] = "Face.Left", ["rest"] = true, ["finishType"] = plaster })!["id"]!;
        var lowNet = Quarter * RLeft * 1.2 - HoleOn(RLeft, door, 1.2);
        var restNet = Quarter * RLeft * (H - 1.2) - HoleOn(RLeft, door, door.Height - 1.2);
        Assert.Equal(lowNet, Net(f, low), 6);
        Assert.Equal(restNet, Net(f, rest), 6);
        Assert.Equal(1.2, f.Bounds(low).Max.Z, 6);
        Assert.Equal(1.2, f.Bounds(rest).Min.Z, 6);
        // The tiles inside the face, towards the centre.
        var verts = f.Session.Geometry(low)!.Parts.SelectMany(p => p.Solid.Vertices).ToList();
        Assert.All(verts, v => Assert.InRange(Radius(v), RLeft - 0.015 - Chords.Tolerance, RLeft + 1e-9));
        Assert.InRange(verts.Min(Radius), RLeft - 0.015 - Chords.Tolerance - 1e-9, RLeft - 0.015 + 1e-9);

        // A panel drawn on the face (a closed shape from 60° to 70°, 1.5 m to 2.5 m up), painted with stone.
        f.Run("architecture.splitFinishFace", new JsonObject
        {
            ["hostId"] = wall, ["point"] = Polar(RLeft, 65, 2), ["normal"] = Polar(-1, 65, 0), ["closed"] = true,
            ["points"] = new JsonArray(Polar(RLeft, 60, 1.5), Polar(RLeft, 70, 1.5), Polar(RLeft, 70, 2.5), Polar(RLeft, 60, 2.5)),
        });
        var stone = Library(f, "Stone cladding 30");
        var panel = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["point"] = Polar(RLeft, 65, 2), ["normal"] = Polar(-1, 65, 0), ["finishType"] = stone })!["id"]!;
        var panelArea = 10 * Math.PI / 180 * RLeft * 1.0;
        Assert.Equal(panelArea, Net(f, panel), 6);
        Assert.Equal(restNet - panelArea, Net(f, rest), 6);
        // The eyedropper reads it there.
        var at = (JsonObject)f.Session.Query("architecture.finishFaceAt", new JsonObject { ["id"] = wall, ["point"] = Polar(RLeft, 65, 2), ["normal"] = Polar(-1, 65, 0) })!;
        Assert.Equal(panel, (string?)at["zoneId"]);
        Assert.Equal(stone, (string?)at["finishType"]);

        // The wall moved a metre east and north: the zones go with it, measured the same.
        var before = f.Bounds(low);
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["x1"] = R + 1, ["y1"] = 1, ["x2"] = 1, ["y2"] = R + 1 });
        Assert.Equal(before.Min.X + 1, f.Bounds(low).Min.X, 6);
        Assert.Equal(before.Max.Y + 1, f.Bounds(low).Max.Y, 6);
        Assert.Equal(lowNet, Net(f, low), 6);
        Assert.Equal(panelArea, Net(f, panel), 6);
        // Made higher: the rest above the tiles grows with it, the tiles stay to 1.2 m.
        f.Run("architecture.updateWall", new JsonObject { ["id"] = wall, ["height"] = 3.5 });
        Assert.Equal(restNet - panelArea + Quarter * RLeft * 0.5, Net(f, rest), 6);
        Assert.Equal(lowNet, Net(f, low), 6);
        f.Session.Undo();
        f.Session.Undo();
        Assert.Equal(before.Min.X, f.Bounds(low).Min.X, 6);
        Assert.Equal(restNet - panelArea, Net(f, rest), 6);

        // Cleared below: the rest of the face takes it.
        f.Run("architecture.clearFinish", new JsonObject { ["hostId"] = wall, ["point"] = Polar(RLeft, 20, 0.5), ["normal"] = Polar(-1, 20, 0) });
        Assert.Null(f.Model.Get(low));
        Assert.Equal(Quarter * RLeft * H - HoleOn(RLeft, door, door.Height) - panelArea, Net(f, rest), 6);

        // The wall deleted takes its zones and splits.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(wall) });
        Assert.Empty(f.Model.OfClass(FinishZones.Class));
        Assert.Empty(f.Model.OfClass(FinishZones.SplitClass));
    }

    [Fact]
    public void A_straight_wall_abutting_a_curved_walls_face_splits_it()
    {
        var f = new Fixture();
        var wall = ArcWall(f);
        // A wall from the centre out to the left face at 45°: it meets the face there.
        f.Wall(0, 0, RLeft * Math.Cos(Math.PI / 4), RLeft * Math.Sin(Math.PI / 4), height: H);
        var tiles = Library(f, "Wall tiles");
        var zone = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = wall, ["point"] = Polar(RLeft, 20, 1), ["normal"] = Polar(-1, 20, 0), ["finishType"] = tiles })!["id"]!;
        // Only the part of the face below 45° (less the abutting wall's half) is painted.
        Assert.True(Net(f, zone) < Quarter / 2 * RLeft * H);
        Assert.True(Net(f, zone) > Quarter / 2 * RLeft * H - 0.2 * H);
        Assert.All(f.Session.Geometry(zone)!.Parts.SelectMany(p => p.Solid.Vertices), v => Assert.True(Math.Atan2(v.Y, v.X) < Math.PI / 4));
    }

    [Fact]
    public void A_curved_facade_meeting_a_straight_one_at_an_outer_corner_closes_it_with_a_mitre()
    {
        var f = new Fixture();
        var arc = ArcWall(f);
        // From the arc's end, square to its tangent there (west), to the south: a left turn, the outer corner on the right.
        var straight = f.Wall(0, R, 0, 1, height: H);
        var etics = Library(f, "ETICS EPS 100");
        var a = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = arc, ["face"] = "Face.Right", ["whole"] = true, ["finishType"] = etics })!["id"]!;
        var b = (string)f.Run("architecture.paintFinish", new JsonObject { ["hostId"] = straight, ["face"] = "Face.Right", ["whole"] = true, ["finishType"] = etics })!["id"]!;
        // The curved facade's finish runs past the arc's end to the corner, its outer layer to the corner of the two.
        Assert.Equal(-Right - Etics, f.Bounds(a).Min.X, 4);
        Assert.Equal(R + Right + Etics, f.Bounds(a).Max.Y, 4);
        // The two meet on the mitre: nothing twice.
        var booleans = f.Session.Reader.Services.Get<ISolidBooleans>()!;
        var ga = f.Session.Geometry(a)!.Parts;
        var gb = f.Session.Geometry(b)!.Parts;
        var overlap = ga.SelectMany(p => gb.Select(q => booleans.Intersection(p.Solid, q.Solid))).OfType<Solid>().Sum(Measure.Volume);
        Assert.True(overlap < 1e-6, $"The facades overlap by {overlap:0.######} m³");
        // Past the arc's end, straight on along its tangent to the corner: the render's thickness and the mitre's half of the
        // corner's square (the chords' few hundredths of a percent short of the ring's sector left out).
        var past = ga.Sum(p => Measure.Volume(p.Solid)) - Quarter * (Math.Pow(RRight + Etics, 2) - RRight * RRight) / 2 * H;
        Assert.Equal((Right * Etics + Etics * Etics / 2) * H, past, 2);
    }
}
