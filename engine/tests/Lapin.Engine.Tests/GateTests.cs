using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Gates;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Gates (testers' report #57): every kind builds closed bodies as wide as its piers with its plan symbol; a sliding gate
/// parks in a zone as long as its leaf, on its hand; a gate clicked by a wall stands on its axis and cuts it; the type's
/// change and its articles; the schedule; IFC as an IfcDoor GATE of an IfcDoorType with its operation, read back native.
/// </summary>
public sealed class GateTests
{
    static Fixture F() => new(new ArchitectureModule());

    static string Place(Fixture f, JsonObject a) { a["storeyId"] ??= f.Storey; return (string)f.Run("architecture.placeGate", a)!["id"]!; }

    static (Vec3 Min, Vec3 Max) Bounds(ElementGeometry g)
    {
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    static double Volume(Fixture f, string id) => ((IQueryContext)f.Session.Reader).Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void Every_kind_builds_closed_bodies_between_its_piers_with_its_plan_symbol()
    {
        var f = F();
        var y = 0.0;
        foreach (var k in GateKinds.All)
        {
            var id = Place(f, new JsonObject { ["kind"] = k.Key, ["x"] = 0, ["y"] = y });
            var e = f.Model.Get(id)!;
            var g = Gates.Geometry(e, f.Model)!;
            var s = Gates.SpecOf(f.Model, e);
            Assert.NotEmpty(g.Parts);
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{k.Key}: {p.Name} is closed"));
            Assert.All(g.Parts, p => Assert.NotNull(p.Material));
            Assert.Contains(g.Parts, p => p.Key == "gate:frame");
            Assert.Contains(g.Parts, p => p.Key == "gate:pier");
            Assert.Contains(g.Plan!, i => i.Kind == "symbol");
            Assert.Contains(g.Plan!, i => i.Kind == "heavy");
            var (min, max) = Bounds(g);
            // From the outer faces of its piers (a sliding leaf runs on past them, to where it parks).
            if (!k.Slides) Assert.InRange(max.X - min.X, s.Overall - 0.001, s.Overall + 0.08);
            else Assert.True(max.X - min.X >= s.Overall);
            Assert.InRange(min.Z, -0.001, 0.001);
            Assert.True(max.Z >= s.Height, $"{k.Key}: as high as its type");
            Assert.Equal("GATE", e.PredefinedType);
            y += 4;
        }
        // A type's name says its kind and size; its materials are the project's.
        var sliding = f.Model.OfClass(Gates.TypeClass).First(t => new GateSpec(t.Attrs["spec"] as JsonObject).Kind.Key == "sliding");
        Assert.Equal("Sliding gate 4000 × 1600", sliding.Name);
        Assert.NotNull(Materials.Find(f.Model, "Steel painted anthracite"));
        Assert.NotNull(Materials.Find(f.Model, "Brick facing"));
    }

    [Fact]
    public void A_sliding_gate_parks_in_a_zone_as_long_as_its_leaf_on_its_hand()
    {
        foreach (var kind in new[] { "sliding", "cantilever" })
        {
            var f = F();
            var id = Place(f, new JsonObject { ["kind"] = kind, ["x"] = 0, ["y"] = 0 });
            var s = Gates.SpecOf(f.Model, f.Model.Get(id)!);
            var zone = s.ParkingZone!.Value;
            Assert.Equal(s.LeafLength, zone.X1 - zone.X0, 9);
            Assert.Equal(kind == "sliding" ? s.Width + 0.2 : s.Width * 1.45 + 0.1, s.LeafLength, 9);
            // In plan: the zone dashed, as long as the leaf, on the right (+x) of the opening.
            var dashed = Gates.Geometry(f.Model.Get(id)!, f.Model)!.Plan!.Where(i => i.Kind == "dashed" && i.Closed).ToList();
            var rect = Assert.Single(dashed);
            Assert.Equal(s.LeafLength, rect.Points.Max(p => p.X) - rect.Points.Min(p => p.X), 6);
            Assert.True(rect.Points.Min(p => p.X) >= s.Width / 2 - 1e-6);
            // On the left hand: the zone on the other side.
            f.Run("architecture.flipGate", new JsonObject { ["ids"] = new JsonArray(id), ["axis"] = "hand" });
            var left = Gates.Geometry(f.Model.Get(id)!, f.Model)!.Plan!.Single(i => i.Kind == "dashed" && i.Closed);
            Assert.True(left.Points.Max(p => p.X) <= -s.Width / 2 + 1e-6);
            var preview = (JsonObject)f.Session.Query("architecture.gatePreview", new JsonObject { ["kind"] = kind })!;
            Assert.Equal(s.LeafLength, Json.Number(preview["parking"])!.Value, 5);
        }
    }

    [Fact]
    public void A_gate_clicked_by_a_wall_stands_on_its_axis_and_cuts_it()
    {
        var f = F();
        var wall = f.Wall(0, 0, 12, 0);
        var before = Volume(f, wall);
        var r = f.Run("architecture.placeGate", new JsonObject { ["storeyId"] = f.Storey, ["kind"] = "doubleSwing", ["x"] = 6, ["y"] = 0.8 })!;
        Assert.Equal(wall, (string?)r["on"]);
        var gate = f.Model.Get((string)r["id"]!)!;
        Assert.Equal(0, Args.PointOf(gate.Attrs["at"]).Y, 6);
        Assert.Equal(0, gate.Num("rotation"), 6);
        Assert.Equal(wall, Gates.HostOf(f.Model, gate)?.Id);
        var s = Gates.SpecOf(f.Model, gate);
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        var thickness = bands.Max(b => b.To) - bands.Min(b => b.From);
        var cut = s.Overall * thickness * f.Model.Get(wall)!.Num("height", Walls.DefaultHeight);
        Assert.Equal(before - cut, Volume(f, wall), 2);
        // Two points: the gate between them, along them.
        var two = f.Model.Get(Place(f, new JsonObject { ["kind"] = "pedestrian", ["x"] = 20, ["y"] = 0, ["x2"] = 20, ["y2"] = 4 }))!;
        Assert.Equal(90, two.Num("rotation"), 6);
        Assert.Equal(2, Args.PointOf(two.Attrs["at"]).Y, 6);
        Assert.Null(Gates.HostOf(f.Model, two));
    }

    [Fact]
    public void A_gate_clicked_by_a_parcel_edge_stands_on_it()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Civil.CivilModule());
        f.Run("civil.addFlatTerrain", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = -5, ["y1"] = -5, ["x2"] = 30, ["y2"] = 30, ["elevation"] = 0 });
        f.Run("civil.setParcel", new JsonObject { ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(20, 0), new JsonArray(20, 15), new JsonArray(0, 15)) });
        var r = f.Run("architecture.placeGate", new JsonObject { ["storeyId"] = f.Storey, ["kind"] = "sliding", ["x"] = 20.6, ["y"] = 7 })!;
        Assert.NotNull((string?)r["on"]);
        var gate = f.Model.Get((string)r["id"]!)!;
        Assert.Equal(20, Args.PointOf(gate.Attrs["at"]).X, 6);
        Assert.Equal(90, gate.Num("rotation"), 6);
    }

    [Fact]
    public void A_type_change_is_seen_on_every_gate_and_its_articles_follow_the_motor()
    {
        var f = F();
        var a = Place(f, new JsonObject { ["kind"] = "doubleSwing", ["x"] = 0, ["y"] = 0 });
        var b = Place(f, new JsonObject { ["kind"] = "doubleSwing", ["x"] = 0, ["y"] = 5 });
        var type = Gates.TypeOf(f.Model, f.Model.Get(a)!)!.Id;
        Assert.Equal("DOUBLE_DOOR_SINGLE_SWING", f.Model.Get(type)!.Str("ifcOperation"));
        Assert.Single((JsonArray)f.Model.Get(type)!.Attrs["articles"]!);
        f.Run("architecture.updateGateType", new JsonObject { ["id"] = type, ["name"] = "Main gate", ["spec"] = new JsonObject
        {
            ["width"] = 4.2, ["top"] = "arched", ["rise"] = 0.4, ["infill"] = "solid", ["motor"] = true, ["piers"] = "steel", ["pierSize"] = 0.12,
            ["materials"] = new JsonObject { ["infill"] = "Larch wood" },
        } });
        foreach (var id in new[] { a, b })
        {
            var (min, max) = Bounds(Gates.Geometry(f.Model.Get(id)!, f.Model)!);
            Assert.InRange(max.X - min.X, 4.2 + 0.24 - 0.001, 4.2 + 0.24 + 0.03);
            Assert.True(max.Z > 1.6 + 0.39, "the arch rises over the leaves");
            Assert.Equal("Main gate", f.Model.Get(id)!.Name);
        }
        Assert.NotNull(Materials.Find(f.Model, "Larch wood"));
        Assert.Contains(Gates.Geometry(f.Model.Get(a)!, f.Model)!.Parts, p => p.Key == "gate:motor");
        var articles = (JsonArray)f.Model.Get(type)!.Attrs["articles"]!;
        Assert.Equal(["LAP-GATE", "LAP-GATEMOT"], articles.Select(x => (string)x!["code"]!).ToArray());
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateGateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["infill"] = "glass" } }));
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateGateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["width"] = 30 } }));
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateGateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["seats"] = 2 } }));
        // A built-in style made into a project type, given to one of them.
        var style = (string)f.Run("architecture.createGateType", new JsonObject { ["style"] = "wroughtIron" })!["id"]!;
        Assert.Equal("Wrought iron double gate", f.Model.Get(style)!.Name);
        f.Run("architecture.setGateType", new JsonObject { ["ids"] = new JsonArray(b), ["typeId"] = style });
        Assert.True(Gates.SpecOf(f.Model, f.Model.Get(b)!).Bool("spears"));
        Assert.Equal("Wrought iron double gate", f.Model.Get(b)!.Name);
    }

    [Fact]
    public void The_schedule_counts_gates_by_type()
    {
        var f = F();
        Place(f, new JsonObject { ["kind"] = "sliding", ["x"] = 0, ["y"] = 0 });
        Place(f, new JsonObject { ["kind"] = "pedestrian", ["x"] = 0, ["y"] = 5 });
        Place(f, new JsonObject { ["kind"] = "pedestrian", ["x"] = 0, ["y"] = 9 });
        Place(f, new JsonObject { ["kind"] = "swingWicket", ["x"] = 0, ["y"] = 14 });
        var grid = new GateSchedule().Grid(f.Session.Reader);
        Assert.Equal(["Sliding gate", "Pedestrian gate", "Gate with side pedestrian door"], grid.Rows.Take(3).Select(r => r[1]).ToArray());
        Assert.Equal("2", grid.Rows[1][8]);
        Assert.Equal("3600 + 1000 × 1600", grid.Rows[2][2]);
        Assert.Equal("2 + 1", grid.Rows[2][3]);
        Assert.Equal("yes", grid.Rows[0][7]);
        Assert.Equal("4", grid.Rows[^1][8]);
    }

    [Fact]
    public void IFC_writes_gates_as_doors_of_type_gate_and_reads_them_back_native()
    {
        var f = F();
        Place(f, new JsonObject { ["kind"] = "doubleSwing", ["x"] = 0, ["y"] = 0 });
        Place(f, new JsonObject { ["kind"] = "doubleSwing", ["x"] = 0, ["y"] = 5 });
        Place(f, new JsonObject { ["kind"] = "sliding", ["x"] = 0, ["y"] = 10 });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(3, ifc.Split("IFCDOOR(").Length - 1);
        Assert.Equal(3, ifc.Split(".GATE.,").Length - 1 - 2);
        Assert.Equal(2, ifc.Split("IFCDOORTYPE(").Length - 1);
        Assert.Contains(".DOUBLE_DOOR_SINGLE_SWING.", ifc);
        Assert.Contains(".SLIDING_TO_LEFT.", ifc);
        Assert.Contains("'Pset_DoorCommon'", ifc);
        Assert.Contains("IFCDOOR('", ifc);
        // Its overall size: the clear width and height of its type.
        Assert.Contains("1.6,3.6,.GATE.", ifc);
        var path = Path.Combine(Path.GetTempPath(), $"gates-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var gates = g.Model.OfClass(Gates.Class).ToList();
            Assert.Equal(3, gates.Count);
            Assert.All(gates, x => Assert.NotNull(Gates.TypeOf(g.Model, x)));
            Assert.Equal(2, g.Model.OfClass(Gates.TypeClass).Count());
            Assert.All(gates, x => Assert.NotEmpty(Gates.Geometry(x, g.Model)!.Parts));
            Assert.Empty(g.Model.OfClass(IfcClass.Door));
        }
        finally { File.Delete(path); }
    }
}
