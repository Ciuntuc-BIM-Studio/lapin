using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Sunshades (report #56): every kind builds closed parts on a window, along a wall and free on a facade; on a window it
/// is sized to the opening past its jambs and follows it; the noon helper gives the projection that shades a window at
/// noon on 21 June (45° N: the sun 68.4° high); plans draw a canopy overhead and fins cut; schedules and quantities count
/// them; IFC writes IfcShadingDevice of an IfcShadingDeviceType and reads them back native; types change their sunshades;
/// styles start types and the project keeps its own.
/// </summary>
public sealed class SunshadeTests
{
    const double Along = 3;

    static (Fixture F, string Wall, string Window) Setup()
    {
        var f = new Fixture();
        var wall = f.Wall(0, 0, 6, 0);
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = Along })!["id"]!;
        return (f, wall, window);
    }

    static string On(Fixture f, string window, JsonObject what) { what["hostId"] = window; return (string)f.Run("architecture.addSunshade", what)!["id"]!; }

    static (Vec3 Min, Vec3 Max) Bounds(Fixture f, string id)
    {
        var all = f.Session.Geometry(id)!.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    [Fact]
    public void Every_kind_builds_closed_parts_on_a_window_along_a_wall_and_free()
    {
        foreach (var k in Sunshades.Kinds)
        {
            var (f, wall, window) = Setup();
            var ids = new[]
            {
                On(f, window, new JsonObject { ["kind"] = k.Key }),
                (string)f.Run("architecture.addSunshade", new JsonObject { ["kind"] = k.Key, ["wallId"] = wall, ["x1"] = 0.3, ["y1"] = 0, ["x2"] = 1.5, ["y2"] = 0 })!["id"]!,
                (string)f.Run("architecture.addSunshade", new JsonObject { ["kind"] = k.Key, ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 5, ["x2"] = 2, ["y2"] = 5 })!["id"]!,
            };
            foreach (var id in ids)
            {
                var g = f.Session.Geometry(id)!;
                Assert.True(g.Parts.Count > 0, $"{k.Key}: {string.Join(", ", g.Diagnostics ?? [])}");
                Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{k.Key}: {p.Name} is closed"));
                Assert.All(g.Parts, p => Assert.NotNull(p.Material));
                Assert.NotEmpty(g.Plan!);
                var e = f.Model.Get(id)!;
                Assert.Equal(k.Predefined, e.PredefinedType);
                Assert.Equal(k.ObjectType, e.Str("objectType"));
            }
        }
    }

    [Fact]
    public void On_a_window_it_is_sized_to_the_opening_and_follows_it()
    {
        var (f, wall, window) = Setup();
        var canopy = On(f, window, new JsonObject { ["kind"] = "canopy" });
        var spec = Openings.SpecOf(f.Model, window)!;
        var s = Sunshades.SpecOf(f.Model, f.Model.Get(canopy)!);
        var (min, max) = Bounds(f, canopy);
        // Past each jamb by its overhangs, over the head by its gap, out from the exterior face (−y) by its projection.
        Assert.Equal(spec.Width + s.Num("overhangLeft") + s.Num("overhangRight"), max.X - min.X, 6);
        Assert.Equal(Along - spec.Width / 2 - s.Num("overhangLeft"), min.X, 6);
        Assert.Equal(spec.SillHeight + spec.Height + s.Num("gap"), min.Z, 6);
        var face = Walls.Section(f.Model, f.Model.Get(wall)!).Min(b => b.From);
        Assert.Equal(face, max.Y, 6);
        Assert.Equal(s.Num("projection"), face - min.Y, 6);
        // The window moved along its wall: the canopy goes with it.
        f.Run("architecture.moveOpening", new JsonObject { ["id"] = Openings.OpeningOf(f.Model, window)!.Id, ["along"] = 4 });
        Assert.Equal(4 - spec.Width / 2 - s.Num("overhangLeft"), Bounds(f, canopy).Min.X, 6);
        // Its type changed: every sunshade of it follows; its own projection wins over the type's.
        var type = Sunshades.TypeOf(f.Model, f.Model.Get(canopy)!)!.Id;
        f.Run("architecture.updateSunshadeType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["projection"] = 1.4 } });
        Assert.Equal(1.4, face - Bounds(f, canopy).Min.Y, 6);
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(canopy), ["attrs"] = new JsonObject { ["projection"] = 0.7 } });
        Assert.Equal(0.7, face - Bounds(f, canopy).Min.Y, 6);
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateSunshadeType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["projection"] = 99 } }));
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateSunshadeType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["surface"] = "straw" } }));
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.updateSunshadeType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["blades"] = 3 } }));
        // Louvres across the window: from below its sill (none) to above its head.
        var louvres = On(f, window, new JsonObject { ["kind"] = "louvres" });
        var ls = Sunshades.SpecOf(f.Model, f.Model.Get(louvres)!);
        var lb = Bounds(f, louvres);
        Assert.InRange(lb.Min.Z, spec.SillHeight - ls.Num("bladeDepth") / 2, spec.SillHeight + ls.Num("spacing"));
        Assert.Equal(spec.SillHeight + spec.Height + ls.Num("above"), lb.Max.Z, 6);
        // The other face, asked for: out to +y.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(louvres), ["attrs"] = new JsonObject { ["flip"] = true } });
        Assert.True(Bounds(f, louvres).Min.Y > 0);
    }

    [Fact]
    public void The_noon_helper_gives_the_depth_that_shades_a_window_on_21_June()
    {
        // At 45° N the sun stands 90 − 45 + 23.44 = 68.44° high at noon on 21 June; a south facade (outward −y, north +y).
        var (sun, _) = Sunshades.Noon(new DateOnly(2026, 6, 21), 45, 25);
        Assert.InRange(sun.Altitude, 68.3, 68.6);
        Assert.InRange(sun.Azimuth, 178, 182);
        var dir = Sunshades.Towards(sun, 0);
        // A window 1.5 m high under a canopy 150 mm over its head: 1.65 / tan 68.44° = 0.652 m.
        var (depth, side, profile) = Sunshades.DepthFor(dir, new Vec2(0, -1), new Vec2(1, 0), 1.65);
        Assert.InRange(depth, 0.645, 0.66);
        Assert.InRange(side, 0, 0.02);
        Assert.InRange(profile, 68.3, 68.6);
        // Falling outwards it reaches further down: shorter.
        Assert.True(Sunshades.DepthFor(dir, new Vec2(0, -1), new Vec2(1, 0), 1.65, Math.Tan(10 * Math.PI / 180)).Depth < depth);
        // A north facade is in its own shade at noon.
        Assert.Equal(0, Sunshades.DepthFor(dir, new Vec2(0, 1), new Vec2(1, 0), 1.65).Depth);

        // In a project: its place (Bucharest when it has none), the window's own height and the canopy's gap.
        var (f, _, window) = Setup();
        var canopy = On(f, window, new JsonObject { ["kind"] = "canopy" });
        var r = (JsonObject)f.Run("architecture.shadeAtNoon", new JsonObject { ["ids"] = new JsonArray(canopy) })!["results"]![0]!;
        var lat = Sun.Place(f.Model).Lat;
        var spec = Openings.SpecOf(f.Model, window)!;
        var gap = Sunshades.SpecOf(f.Model, f.Model.Get(canopy)!).Num("gap");
        var expected = (spec.Height + gap) / Math.Tan((90 - lat + 23.44) * Math.PI / 180);
        Assert.InRange(Json.Number(r["depth"])!.Value, expected - 0.01, expected + 0.01);
        var own = f.Model.Get(canopy)!.Num("projection");
        Assert.InRange(own - Json.Number(r["depth"])!.Value, 0, 0.0101);
        // Only over a window, and only a canopy, an awning or a pergola.
        var fins = On(f, window, new JsonObject { ["kind"] = "fins" });
        Assert.ThrowsAny<Exception>(() => f.Run("architecture.shadeAtNoon", new JsonObject { ["ids"] = new JsonArray(fins) }));
    }

    [Fact]
    public void Plans_draw_a_canopy_overhead_and_fins_cut_and_elevations_draw_them()
    {
        var (f, wall, window) = Setup();
        var canopy = On(f, window, new JsonObject { ["kind"] = "canopy" });
        var fins = (string)f.Run("architecture.addSunshade", new JsonObject { ["kind"] = "fins", ["wallId"] = wall, ["x1"] = 0.2, ["y1"] = 0, ["x2"] = 1.4, ["y2"] = 0 })!["id"]!;
        Assert.All(f.Session.Geometry(canopy)!.Plan!.Where(i => i.Kind != "hit"), i => Assert.Equal("overhead", i.Kind));
        Assert.Contains(f.Session.Geometry(fins)!.Plan!, i => i.Kind == "heavy");
        var plan = Views.Find(f.Model, "plan:" + f.Storey)!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, plan);
        Assert.True(d != null, problem);
        Assert.Contains(d!.Lines, l => l.Element == canopy);
        Assert.Contains(d.Lines, l => l.Element == fins);
        var elevation = f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!)!;
        var (ed, _, eproblem) = Views.Generate(f.Session.Reader, elevation);
        Assert.True(ed != null, eproblem);
        Assert.Contains(ed!.Lines, l => l.Element == canopy);
    }

    [Fact]
    public void Schedules_and_quantities_count_them()
    {
        var (f, wall, window) = Setup();
        On(f, window, new JsonObject { ["kind"] = "canopy" });
        f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 1 });
        var second = f.Model.OfClass(IfcClass.Window).Single(w => w.Id != window).Id;
        var c2 = On(f, second, new JsonObject { ["kind"] = "canopy" });
        f.Run("architecture.addSunshade", new JsonObject { ["kind"] = "louvres", ["wallId"] = wall, ["x1"] = 4.2, ["y1"] = 0, ["x2"] = 5.8, ["y2"] = 0 });
        var q = f.Session.Quantities(c2).Single(s => s.Name == Sunshades.QuantitySet).Quantities;
        var spec = Openings.SpecOf(f.Model, second)!;
        var s = Sunshades.SpecOf(f.Model, f.Model.Get(c2)!);
        var len = spec.Width + s.Num("overhangLeft") + s.Num("overhangRight");
        Assert.Equal(len, q.Single(x => x.Name == "Length").Value, 6);
        Assert.Equal(len * s.Num("projection"), q.Single(x => x.Name == "Area").Value, 6);
        Assert.True(q.Single(x => x.Name == "NetVolume").Value > 0);
        var grid = new SunshadeSchedule().Grid(f.Session.Reader);
        var canopyRow = grid.Rows.Single(r => r[1] == "Canopy");
        Assert.Equal("2", canopyRow[5]);
        Assert.Contains(f.Model.Get(window)!.Name, canopyRow[2]);
        Assert.Equal("Louvres (brise-soleil)", grid.Rows[1][1]);
        Assert.Equal("3", grid.Rows[^1][5]);
        // A schedule of them is made like any other.
        var sched = (string)f.Run("architecture.createSchedule", new JsonObject { ["kind"] = "sunshades" })!["id"]!;
        Assert.NotNull(f.Model.Get(sched));
    }

    [Fact]
    public void IFC_writes_shading_devices_of_their_types_and_reads_them_back_native()
    {
        var (f, wall, window) = Setup();
        On(f, window, new JsonObject { ["kind"] = "awning" });
        On(f, window, new JsonObject { ["kind"] = "canopy" });
        f.Run("architecture.addSunshade", new JsonObject { ["kind"] = "louvres", ["wallId"] = wall, ["x1"] = 4.2, ["y1"] = 0, ["x2"] = 5.8, ["y2"] = 0 });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(3, ifc.Split("IFCSHADINGDEVICE(").Length - 1);
        Assert.Equal(3, ifc.Split("IFCSHADINGDEVICETYPE(").Length - 1);
        Assert.Contains(".AWNING.", ifc);
        Assert.Contains("'Canopy'", ifc);
        Assert.Contains("'LOUVER'", ifc);
        var path = Path.Combine(Path.GetTempPath(), $"shade-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = new Fixture();
            IfcImport.Run(g.Session, path, null, out _);
            var shades = g.Model.OfClass(Sunshades.Class).ToList();
            Assert.Equal(3, shades.Count);
            Assert.All(shades, x => Assert.NotNull(Sunshades.TypeOf(g.Model, x)));
            Assert.All(shades, x => Assert.NotEmpty(g.Session.Geometry(x.Id)!.Parts));
            // The one on the window still stands on it.
            Assert.Contains(shades, x => x.Str("host") is { } h && g.Model.Get(h)?.Class == IfcClass.Window);
        }
        finally { File.Delete(path); }

        // IFC 4.3 has louvres of their own.
        using var ms43 = new MemoryStream();
        IfcExport.Write(f.Session, ms43, "x.ifc", new IfcExportOptions { Schema = "IFC4X3_ADD2" });
        Assert.Contains(".LOUVER.", Encoding.UTF8.GetString(ms43.ToArray()));
    }

    [Fact]
    public void Styles_start_types_and_the_project_keeps_its_own()
    {
        var (f, _, window) = Setup();
        foreach (var (id, label, kind, _) in Sunshades.Styles)
        {
            var t = (string)f.Run("architecture.createSunshadeType", new JsonObject { ["style"] = id })!["id"]!;
            var type = f.Model.Get(t)!;
            Assert.StartsWith(label, type.Name);
            Assert.Equal(kind, new ShadeSpec(type.Attrs["spec"] as JsonObject).Kind.Key);
            var sh = On(f, window, new JsonObject { ["typeId"] = t });
            Assert.NotEmpty(f.Session.Geometry(sh)!.Parts);
        }
        Assert.NotNull(Materials.Find(f.Model, "Corten steel"));
        var glass = f.Model.OfClass(Sunshades.TypeClass).Single(t => t.Name.StartsWith("Glass canopy", StringComparison.Ordinal));
        var styleId = (string)f.Run("architecture.saveSunshadeStyle", new JsonObject { ["typeId"] = glass.Id, ["name"] = "Entrance glass" })!["styleId"]!;
        var made = (string)f.Run("architecture.createSunshadeType", new JsonObject { ["style"] = styleId })!["id"]!;
        Assert.Equal("glass", new ShadeSpec(f.Model.Get(made)!.Attrs["spec"] as JsonObject).Str("surface"));
        f.Run("architecture.renameSunshadeStyle", new JsonObject { ["styleId"] = styleId, ["name"] = "Entrance" });
        Assert.Equal("Entrance", (string?)ProjectStyles.All(f.Model, Sunshades.StylesKey).Single()["label"]);
        f.Run("architecture.deleteSunshadeStyle", new JsonObject { ["styleId"] = styleId });
        Assert.Empty(ProjectStyles.All(f.Model, Sunshades.StylesKey));
        // A kind changed on a type starts from that kind's defaults; its sunshades take the kind's IFC type.
        var some = f.Model.OfClass(Sunshades.Class).First();
        var st = Sunshades.TypeOf(f.Model, some)!.Id;
        f.Run("architecture.updateSunshadeType", new JsonObject { ["id"] = st, ["spec"] = new JsonObject { ["kind"] = "awning" } });
        Assert.All(f.Model.RelatedOf(st, IfcClass.RelDefinesByType), e => Assert.Equal("AWNING", e.PredefinedType));
    }
}
