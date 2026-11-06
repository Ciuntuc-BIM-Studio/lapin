using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Mep.Fans;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Coordination;

namespace Lapin.Engine.Tests;

/// <summary>
/// Exhaust fans (testers' report #55): every kind builds a closed body with its plan symbol; a wall fan sits on the wall's
/// face and cuts a round provision through it; a ceiling fan's duct runs to an outlet and follows the fan; the type's change
/// on every fan; the fan schedule and the rooms' extract check; IFC as IfcFan of an IfcFanType with Pset_FanTypeCommon,
/// read back native.
/// </summary>
public sealed class ExhaustFanTests
{
    static Fixture F() => new(new ArchitectureModule(), new MepModule());

    static string Place(Fixture f, JsonObject a) { a["storeyId"] ??= f.Storey; return (string)f.Run("mep.placeFan", a)!["id"]!; }

    static (Vec3 Min, Vec3 Max) Bounds(ElementGeometry g)
    {
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    [Fact]
    public void Every_kind_builds_a_closed_body_with_its_plan_symbol()
    {
        var f = F();
        f.Wall(0, 0, 30, 0);
        var x = 1.0;
        foreach (var k in FanKinds.All)
        {
            var upright = k.Mountings[0] is "wall" or "window";
            var id = Place(f, new JsonObject { ["kind"] = k.Key, ["x"] = x, ["y"] = upright ? 0.4 : 3 });
            var e = f.Model.Get(id)!;
            var g = ExhaustFans.Geometry(e, f.Model)!;
            Assert.NotEmpty(g.Parts);
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{k.Key}: {p.Name} is closed"));
            Assert.All(g.Parts, p => Assert.NotNull(p.Material));
            Assert.Contains(g.Plan!, i => i.Kind == "symbol");
            Assert.Equal(k.Mountings[0], e.Str("mounting"));
            Assert.Equal(k.Predefined, e.PredefinedType);
            var (min, max) = Bounds(g);
            if (upright)
            {
                // On the wall's face (the room is on its left, +y), its middle at 2.1 m (a window fan's at 1.9 m).
                Assert.Equal(180, e.Num("rotation"), 6);
                Assert.InRange(max.Z, e.Num("height"), e.Num("height") + 0.4);
            }
            else Assert.InRange(min.Z, e.Num("height") - 0.2, e.Num("height"));
            x += 2;
        }
        // A type's name says its kind, size and airflow; the finishes are the project's materials.
        var wall = f.Model.OfClass(ExhaustFans.TypeClass).First(t => new FanSpec(t.Attrs["spec"] as JsonObject).Kind.Key == "wallAxial");
        Assert.Equal("Wall fan (axial) Ø100, 95 m³/h", wall.Name);
        Assert.NotNull(Materials.Find(f.Model, "Plastic white (fan)"));
        // A wall fan needs a wall.
        Assert.ThrowsAny<Exception>(() => Place(f, new JsonObject { ["kind"] = "wallAxial", ["x"] = 5, ["y"] = 6 }));
    }

    [Fact]
    public void A_wall_fan_cuts_a_round_provision_through_its_wall()
    {
        var f = F();
        var wall = f.Wall(0, 0, 6, 0);
        var before = f.Model.Get(wall)!;
        var volume = ((IQueryContext)f.Session.Reader).Geometry(wall)!.Parts.Sum(p => Measure.Volume(p.Solid));
        var fan = Place(f, new JsonObject { ["kind"] = "wallAxial", ["x"] = 3, ["y"] = 0.3 });
        var provisions = Provisions.Of(f.Model, f.Session.Catalog, f.Model.Get(wall)!);
        var p = Assert.Single(provisions, x => x.SourceId == fan);
        Assert.Equal(OpeningVoids.Round, p.Shape);
        Assert.Equal(0.12, p.Width, 6);
        // Through the wall: the wall's body has lost the sleeve's volume.
        var after = ((IQueryContext)f.Session.Reader).Geometry(wall)!.Parts.Sum(x => Measure.Volume(x.Solid));
        Assert.True(after < volume - 0.0015, $"the wall is cut ({volume} → {after})");
        // The sleeve runs through the whole wall: the fan's body reaches its far face.
        var bands = Walls.Section(f.Model, before);
        var thickness = bands.Max(b => b.To) - bands.Min(b => b.From);
        var (min, _) = Bounds(ExhaustFans.Geometry(f.Model.Get(fan)!, f.Model)!);
        Assert.InRange(min.Y, bands.Min(b => b.From) - 0.05, bands.Min(b => b.From) + 0.01);
        Assert.True(thickness > 0.1);
        // A window fan sits in its glazing: no provision.
        Place(f, new JsonObject { ["kind"] = "window", ["x"] = 1, ["y"] = 0.3 });
        Assert.Single(Provisions.Of(f.Model, f.Session.Catalog, f.Model.Get(wall)!));
    }

    [Fact]
    public void A_ceiling_fan_ducts_to_an_outlet_that_follows_it()
    {
        var f = F();
        f.Wall(0, 0, 0, 5);
        var fan = Place(f, new JsonObject { ["kind"] = "ceilingDuct", ["x"] = 2, ["y"] = 2, ["height"] = 2.5 });
        Assert.ThrowsAny<Exception>(() => f.Run("mep.fanDuct", new JsonObject { ["id"] = Place(f, new JsonObject { ["kind"] = "wallAxial", ["x"] = 0.3, ["y"] = 3 }), ["x"] = -1, ["y"] = 3 }));
        var r = f.Run("mep.fanDuct", new JsonObject { ["id"] = fan, ["x"] = -0.6, ["y"] = 2, ["outlet"] = "wall" })!;
        var run = f.Model.Get((string)r["ductId"]!)!;
        var system = f.Model.Get((string)r["systemId"]!)!;
        Assert.Equal("Bathroom extract", system.Name);
        Assert.Equal(Services.ExtractAir, Systems.Service(system));
        Assert.Equal("IfcDuctSegment", run.Class);
        Assert.Contains(ExhaustFans.Geometry(f.Model.Get(fan)!, f.Model)!.Parts, p => p.Key == "fan:outlet");
        // The duct leaves the spigot at the fan's height and goes through the wall (the system cuts what it crosses).
        Assert.Contains(Provisions.Of(f.Model, f.Session.Catalog), p => p.SourceId == run.Id);
        // Moved, the fan takes the duct's start with it; raised, the duct's level.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(fan), ["op"] = "move", ["by"] = new JsonArray(0, 1) });
        var (start, _, level) = ExhaustFans.Spigot(f.Model, f.Model.Get(fan)!, ExhaustFans.SpecOf(f.Model, f.Model.Get(fan)!));
        var pts = Runs.Points(f.Model.Get(run.Id)!);
        Assert.Equal(start.Y, pts[0].Y, 5);
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(fan), ["attrs"] = new JsonObject { ["height"] = 2.3 } });
        Assert.Equal(2.39, Json.Number(((JsonArray)f.Model.Get(run.Id)!.Attrs["levels"]!)[0])!.Value, 5);
        Assert.True(level > 2.5);
        // A roof outlet: a pipe and its cowl over the duct's end.
        f.Run("mep.fanDuct", new JsonObject { ["id"] = fan, ["x"] = 2, ["y"] = 6, ["outlet"] = "roof", ["outletRise"] = 1.5 });
        Assert.Null(f.Model.Get(run.Id));
        var (_, max) = Bounds(ExhaustFans.Geometry(f.Model.Get(fan)!, f.Model)!);
        Assert.True(max.Z > 2.3 + 1.5, $"the cowl stands over the roof ({max.Z})");
    }

    [Fact]
    public void A_type_change_is_seen_on_every_fan_and_its_mounting_is_its_kinds()
    {
        var f = F();
        f.Wall(0, 0, 8, 0);
        var a = Place(f, new JsonObject { ["kind"] = "wallAxial", ["x"] = 2, ["y"] = 0.3 });
        var b = Place(f, new JsonObject { ["kind"] = "wallAxial", ["x"] = 5, ["y"] = 0.3 });
        var type = ExhaustFans.TypeOf(f.Model, f.Model.Get(a)!)!.Id;
        f.Run("mep.updateFanType", new JsonObject { ["id"] = type, ["name"] = "Bath fan", ["spec"] = new JsonObject { ["diameter"] = "150", ["grille"] = "square", ["faceSize"] = 0.24, ["finish"] = "stainless", ["control"] = "humidityTimer" } });
        foreach (var id in new[] { a, b })
        {
            var g = ExhaustFans.Geometry(f.Model.Get(id)!, f.Model)!;
            var (min, max) = Bounds(g);
            Assert.InRange(max.X - min.X, 0.239, 0.27);
            Assert.Equal("Bath fan", f.Model.Get(id)!.Name);
            Assert.Contains(g.Parts, p => p.Material == Materials.Find(f.Model, "Stainless steel brushed")!.Id);
        }
        Assert.Equal(0.17, Provisions.Of(f.Model, f.Session.Catalog).First(p => p.SourceId == a).Width, 6);
        Assert.ThrowsAny<Exception>(() => f.Run("mep.updateFanType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["mounting"] = "ceiling" } }));
        Assert.ThrowsAny<Exception>(() => f.Run("mep.updateFanType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["diameter"] = "90" } }));
        Assert.ThrowsAny<Exception>(() => f.Run("mep.updateFanType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["airflow"] = 5 } }));
        // A built-in type made from its preset, given to one of them.
        var preset = (string)f.Run("mep.createFanType", new JsonObject { ["preset"] = "wall100h" })!["id"]!;
        Assert.Equal("Wall fan Ø100, humidity sensor", f.Model.Get(preset)!.Name);
        f.Run("mep.setFanType", new JsonObject { ["ids"] = new JsonArray(b), ["typeId"] = preset });
        Assert.Equal("humidity", ExhaustFans.SpecOf(f.Model, f.Model.Get(b)!).Control);
        // A ceiling type is not for a wall fan.
        var ceiling = (string)f.Run("mep.createFanType", new JsonObject { ["kind"] = "ceilingDuct" })!["id"]!;
        Assert.ThrowsAny<Exception>(() => f.Run("mep.setFanType", new JsonObject { ["ids"] = new JsonArray(b), ["typeId"] = ceiling }));
    }

    [Fact]
    public void The_schedules_list_fans_and_check_the_rooms_extract()
    {
        var f = F();
        f.Wall(0, 0, 3, 0); f.Wall(3, 0, 3, 2.5); f.Wall(3, 2.5, 0, 2.5); f.Wall(0, 2.5, 0, 0);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 1.5, ["y"] = 1.2, ["name"] = "Bathroom", ["function"] = "Bath" })!["id"]!;
        Place(f, new JsonObject { ["kind"] = "wallAxial", ["x"] = 1.5, ["y"] = 0.3 });
        var q = f.Session.Reader;
        var grid = new FanSchedule().Grid(q);
        Assert.Equal("Wall fan (axial)", grid.Rows[0][2]);
        Assert.Contains("Bathroom", grid.Rows[0][1]);
        Assert.Equal("95", grid.Rows[0][4]);
        Assert.Equal("wall", grid.Rows[0][5]);
        Assert.Equal("Total", grid.Rows[^1][0]);
        var check = new FanRoomCheck().Grid(q);
        var row = Assert.Single(check.Rows);
        var volume = double.Parse(row[2], System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Math.Round(Math.Max(54, 8 * volume)), double.Parse(row[4], System.Globalization.CultureInfo.InvariantCulture), 0);
        Assert.Equal("95", row[5]);
        Assert.Equal(8 * volume > 95 ? "Too little" : "OK", row[6]);
        Assert.Equal(54, FanRoomCheck.Required(5, "Bath"));
        Assert.Equal(160, FanRoomCheck.Required(20, "Bath"));
        Assert.NotNull(f.Model.Get(room));
    }

    [Fact]
    public void IFC_writes_fans_of_their_type_and_reads_them_back_native()
    {
        var f = F();
        f.Wall(0, 0, 8, 0);
        Place(f, new JsonObject { ["kind"] = "wallAxial", ["x"] = 2, ["y"] = 0.3 });
        Place(f, new JsonObject { ["kind"] = "wallAxial", ["x"] = 4, ["y"] = 0.3 });
        Place(f, new JsonObject { ["kind"] = "ceilingLight", ["x"] = 4, ["y"] = 3 });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(3, ifc.Split("IFCFAN(").Length - 1);
        Assert.Equal(2, ifc.Split("IFCFANTYPE(").Length - 1);
        Assert.Contains(".PROPELLORAXIAL.", ifc);
        Assert.Contains(".CENTRIFUGALRADIAL.", ifc);
        Assert.Contains("'Pset_FanTypeCommon'", ifc);
        Assert.Contains("IFCVOLUMETRICFLOWRATEMEASURE(0.026388889)", ifc);
        // The wall fans' sleeves are openings of the wall.
        Assert.Equal(2, ifc.Split("'Provision opening'").Length - 1);
        var path = Path.Combine(Path.GetTempPath(), $"fans-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var fans = g.Model.OfClass(ExhaustFans.Class).ToList();
            Assert.Equal(3, fans.Count);
            Assert.All(fans, x => Assert.NotNull(ExhaustFans.TypeOf(g.Model, x)));
            Assert.Equal(2, g.Model.OfClass(ExhaustFans.TypeClass).Count());
            Assert.All(fans, x => Assert.NotEmpty(ExhaustFans.Geometry(x, g.Model)!.Parts));
            // The fans cut their walls again, so the file's openings are not made twice.
            Assert.Equal(2, Provisions.Of(g.Model, g.Session.Catalog).Count(p => p.Cuts));
        }
        finally { File.Delete(path); }
    }
}
