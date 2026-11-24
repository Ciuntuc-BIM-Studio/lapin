using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Furniture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Sanitary fixtures (forum topic #245, docs/plan-furniture.md, Obiectele sanitare): the furniture module's Sanitary kinds — a WC,
/// a bidet, a wash basin, a vanity unit, a shower, a bath, a kitchen sink and bathroom accessories — each a closed body of its
/// type's size with its plan symbol and its materials; placed with its back on a wall's face; written to IFC as
/// IfcSanitaryTerminal of an IfcSanitaryTerminalType with its predefined type and its connection points (IfcDistributionPort:
/// cold and hot water, waste), the accessories as IfcFurniture; read back native; a type change one undo step on every piece.
/// </summary>
public sealed class SanitaryTests
{
    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule());

    static string Place(Fixture f, JsonObject a) { a["storeyId"] ??= f.Storey; return (string)f.Run("furniture.place", a)!["id"]!; }

    static (Vec3 Min, Vec3 Max) Bounds(Fixture f, string id)
    {
        var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    static string NewType(Fixture f, string kind, JsonObject? spec = null) =>
        (string)f.Run("furniture.createType", new JsonObject { ["kind"] = kind, ["spec"] = spec })!["id"]!;

    static readonly (string Kind, string Class, string Predefined)[] Expected =
    [
        ("toilet", "IfcSanitaryTerminal", "TOILETPAN"),
        ("bidet", "IfcSanitaryTerminal", "BIDET"),
        ("washbasin", "IfcSanitaryTerminal", "WASHHANDBASIN"),
        ("vanity", "IfcSanitaryTerminal", "WASHHANDBASIN"),
        ("shower", "IfcSanitaryTerminal", "SHOWER"),
        ("bath", "IfcSanitaryTerminal", "BATH"),
        ("kitchenSink", "IfcSanitaryTerminal", "SINK"),
        ("bathAccessory", "IfcFurniture", "USERDEFINED"),
    ];

    [Fact]
    public void Every_sanitary_kind_is_a_closed_body_of_its_size_with_its_plan_symbol_and_class()
    {
        var f = F();
        var x = 0.0;
        foreach (var (key, cls, pre) in Expected)
        {
            var k = FurnitureKinds.Find(key)!;
            Assert.Equal("Sanitary", k.Group);
            Assert.Equal(cls, k.IfcClass);
            var id = Place(f, new JsonObject { ["kind"] = key, ["x"] = x, ["y"] = 0, ["against"] = false });
            var e = f.Model.Get(id)!;
            Assert.Equal(pre, e.PredefinedType);
            // Kept as Lapin's furniture in the project, written as its IFC class: the element says so, its type too.
            Assert.Equal(cls == "IfcFurniture" ? null : cls, e.Str("exportAs"));
            var g = FurnitureModule.Geometry(e, f.Model)!;
            Assert.NotEmpty(g.Parts);
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{key}: {p.Name} is closed"));
            Assert.All(g.Parts, p => Assert.NotNull(p.Material));
            Assert.True(g.Parts.Sum(p => p.Solid.Faces.Length) < 6000, $"{key}: low-poly");
            Assert.True(g.Plan!.Count(p => p.Kind == "symbol") >= 2, $"{key}: an outline and its symbol's lines");
            var (min, max) = Bounds(f, id);
            Assert.InRange(max.X - min.X, k.Width - 0.01, k.Width + 0.03);
            Assert.InRange(max.Y - min.Y, k.Depth - 0.01, k.Depth + 0.03);
            // Its height is its rim's (a seat's, a basin's, a bath's), its taps, cistern or shower head standing over it.
            Assert.True(max.Z >= Spatial.Elevation(f.Model, e) + k.Height - 0.02, $"{key}: up to its height");
            x += 3;
        }
        // The materials by role are the project's: the ceramic, the taps' chrome.
        Assert.NotNull(Materials.Find(f.Model, "Sanitary ceramic white"));
        Assert.NotNull(Materials.Find(f.Model, "Chrome"));
        Assert.Equal("WC 360 × 540", f.Model.OfClass(FurnitureModule.TypeClass).First(t => new Spec(t.Attrs["spec"] as JsonObject).Kind.Key == "toilet").Name);
    }

    [Fact]
    public void A_wall_hung_wc_hangs_and_a_floor_standing_one_stands_with_its_cistern_over_its_pan()
    {
        var f = F();
        var hung = Place(f, new JsonObject { ["kind"] = "toilet", ["x"] = 0, ["y"] = 0, ["against"] = false });
        var (hmin, hmax) = Bounds(f, hung);
        Assert.True(hmin.Z > 0.05, "a wall-hung pan clears the floor");
        // The seat at the type's height; the concealed cistern's flush plate on the wall over it.
        var seat = FurnitureModule.Geometry(f.Model.Get(hung)!, f.Model)!.Parts.Single(p => p.Key == "furniture:seat");
        Assert.InRange(Measure.Bounds(seat.Solid)!.Value.Max.Z, 0.399, 0.401);
        Assert.InRange(hmax.Z, 1.1, 1.2);
        var floor = NewType(f, "toilet", new JsonObject { ["mount"] = "floor", ["cistern"] = "closeCoupled", ["depth"] = 0.65, ["width"] = 0.38 });
        var standing = Place(f, new JsonObject { ["typeId"] = floor, ["x"] = 3, ["y"] = 0, ["against"] = false });
        var (smin, smax) = Bounds(f, standing);
        Assert.InRange(smin.Z, -0.001, 0.001);
        Assert.True(smax.Z > 0.75, "the close-coupled cistern stands over the pan");
        // A floor-standing pan drains through the floor, a wall-hung one into the wall behind.
        var hungWaste = Sanitary.Ports(new Spec(FurnitureModule.TypeOf(f.Model, f.Model.Get(hung)!)!.Attrs["spec"] as JsonObject)).Single(p => p.Key == "waste");
        Assert.Equal(new Vec3(0, 1, 0), hungWaste.Dir);
        Assert.Equal(0.11, hungWaste.Size, 6);
        var floorWaste = Sanitary.Ports(new Spec(f.Model.Get(floor)!.Attrs["spec"] as JsonObject)).Single(p => p.Key == "waste");
        Assert.Equal(new Vec3(0, 0, -1), floorWaste.Dir);
    }

    [Fact]
    public void A_shower_has_its_tray_drain_and_glass_and_a_quadrant_tray_is_rounded()
    {
        var f = F();
        var door = NewType(f, "shower", new JsonObject { ["enclosure"] = "door" });
        var none = NewType(f, "shower", new JsonObject { ["enclosure"] = "none" });
        var quadrant = NewType(f, "shower", new JsonObject { ["shape"] = "quadrant", ["enclosure"] = "door" });
        var a = Place(f, new JsonObject { ["typeId"] = door, ["x"] = 0, ["y"] = 0, ["against"] = false });
        var b = Place(f, new JsonObject { ["typeId"] = none, ["x"] = 3, ["y"] = 0, ["against"] = false });
        var c = Place(f, new JsonObject { ["typeId"] = quadrant, ["x"] = 6, ["y"] = 0, ["against"] = false });
        var ga = FurnitureModule.Geometry(f.Model.Get(a)!, f.Model)!;
        var gb = FurnitureModule.Geometry(f.Model.Get(b)!, f.Model)!;
        Assert.Contains(ga.Parts, p => p.Key == "furniture:screen");
        Assert.DoesNotContain(gb.Parts, p => p.Key == "furniture:screen");
        // The door's swing is drawn in plan (from 1:50), the tray's cross to its drain at every scale.
        Assert.Contains(ga.Plan!, p => p.MinDetail > PlanDetail.Coarse);
        // The quadrant's tray: less than the square's area, its curve through Chords (no chord further than 1 mm from it).
        double TrayArea(string id) => Measure.Volume(FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!.Parts.Single(p => p.Key == "furniture:ceramic").Solid);
        Assert.True(TrayArea(c) < TrayArea(b) * 0.9);
        Assert.True(TrayArea(c) > TrayArea(b) * 0.7);
    }

    [Fact]
    public void A_kitchen_sink_draws_its_bowls_and_drainer_and_a_basin_its_bowl_tap_and_waste()
    {
        var f = F();
        var two = NewType(f, "kitchenSink", new JsonObject { ["bowls"] = 2, ["drainer"] = "right", ["width"] = 1.16 });
        var sink = Place(f, new JsonObject { ["typeId"] = two, ["x"] = 0, ["y"] = 0, ["against"] = false });
        var g = FurnitureModule.Geometry(f.Model.Get(sink)!, f.Model)!;
        // Two bowls, each with its waste; the drainer's ribs.
        Assert.Equal(2, Sanitary.Ports(new Spec(f.Model.Get(two)!.Attrs["spec"] as JsonObject)).Count(p => p.Service == "drainage"));
        Assert.True(g.Plan!.Count(p => p.Kind == "symbol" && !p.Closed) >= 3, "the drainer's ribs");
        var basin = Place(f, new JsonObject { ["kind"] = "washbasin", ["x"] = 3, ["y"] = 0, ["against"] = false });
        var ports = Sanitary.Ports(new Spec(FurnitureModule.TypeOf(f.Model, f.Model.Get(basin)!)!.Attrs["spec"] as JsonObject));
        Assert.Equal(["cold", "hot", "waste"], ports.Select(p => p.Key).ToArray());
        Assert.Equal(["coldWater", "hotWater", "drainage"], ports.Select(p => p.Service).ToArray());
        // A pedestal basin stands on the floor; a wall-hung one hangs.
        Assert.True(Bounds(f, basin).Min.Z > 0.5);
        var ped = NewType(f, "washbasin", new JsonObject { ["mount"] = "pedestal" });
        Assert.InRange(Bounds(f, Place(f, new JsonObject { ["typeId"] = ped, ["x"] = 6, ["y"] = 0, ["against"] = false })).Min.Z, -0.001, 0.001);
    }

    [Fact]
    public void A_fixture_clicked_near_a_wall_backs_onto_its_face_with_its_ports_on_it()
    {
        var f = F();
        var south = f.Wall(0, 0, 6, 0);
        var face = Walls.Section(f.Model, f.Model.Get(south)!).Max(b => b.To);
        var wc = Place(f, new JsonObject { ["kind"] = "toilet", ["x"] = 2, ["y"] = 0.6 });
        var e = f.Model.Get(wc)!;
        Assert.Equal(180, e.Num("rotation"), 6);
        var (min, _) = Bounds(f, wc);
        Assert.InRange(min.Y - face, 0, 0.005);
        // Its ports in the project's frame, turned with it: the waste into the wall it backs onto (−y), at the wall's face.
        var waste = Sanitary.PortsOf(f.Model, e).Single(p => p.Key == "waste");
        Assert.InRange(waste.At.Y - face, -0.005, 0.005);
        Assert.InRange(waste.Dir.Y, -1.0001, -0.9999);
        Assert.InRange(waste.At.X, 1.999, 2.001);
    }

    [Fact]
    public void Each_kind_has_common_sizes_marked_indicative_that_a_type_takes()
    {
        var f = F();
        var catalog = FurnitureKinds.Catalog().Select(n => n!.AsObject()).Where(k => (string?)k["group"] == "Sanitary").ToList();
        Assert.Equal(8, catalog.Count);
        foreach (var k in catalog)
        {
            var sizes = k["sizes"]!.AsArray();
            Assert.True(sizes.Count >= 2, $"{k["key"]}: common sizes");
            Assert.True((bool)k["sizesIndicative"]!);
            var type = NewType(f, (string)k["key"]!);
            foreach (var s in sizes)
            {
                f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = s!["spec"]!.DeepClone() });
                var spec = new Spec(f.Model.Get(type)!.Attrs["spec"] as JsonObject);
                Assert.Equal((double)s["spec"]!["width"]!, spec.W, 6);
            }
        }
    }

    [Fact]
    public void An_accessory_is_furniture_named_by_its_item_and_takes_its_size_when_the_item_changes()
    {
        var f = F();
        var type = NewType(f, "bathAccessory", new JsonObject { ["item"] = "grabBar" });
        var bar = Place(f, new JsonObject { ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["against"] = false });
        Assert.Equal("Grab bar", f.Model.Get(bar)!.Str("objectType"));
        Assert.InRange(Bounds(f, bar).Min.Z, 0.7, 0.9);
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["item"] = "mirror" } });
        Assert.Equal("Mirror", f.Model.Get(bar)!.Str("objectType"));
        var spec = new Spec(f.Model.Get(type)!.Attrs["spec"] as JsonObject);
        Assert.Equal(0.8, spec.H, 6);
        Assert.Contains(FurnitureModule.Geometry(f.Model.Get(bar)!, f.Model)!.Parts, p => p.Key == "furniture:mirror");
    }

    [Fact]
    public void A_type_change_is_one_undo_step_and_keeps_the_pieces_and_their_ids()
    {
        var f = F();
        var type = NewType(f, "washbasin");
        var a = Place(f, new JsonObject { ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["against"] = false });
        var b = Place(f, new JsonObject { ["typeId"] = type, ["x"] = 2, ["y"] = 0, ["against"] = false });
        var before = Bounds(f, a).Min.Z;
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["mount"] = "pedestal", ["width"] = 0.65 } });
        Assert.InRange(Bounds(f, a).Min.Z, -0.001, 0.001);
        Assert.InRange(Bounds(f, b).Max.X - Bounds(f, b).Min.X, 0.64, 0.68);
        f.Session.Undo();
        Assert.Equal(before, Bounds(f, a).Min.Z, 6);
        Assert.NotNull(f.Model.Get(a));
        Assert.NotNull(f.Model.Get(b));
        // Placing is one step too.
        f.Session.Undo();
        Assert.Null(f.Model.Get(b));
        Assert.NotNull(f.Model.Get(a));
    }

    [Fact]
    public void IFC_writes_sanitary_terminals_of_their_types_with_ports_and_reads_them_back_native()
    {
        var f = F();
        foreach (var (key, _, _) in Expected)
            Place(f, new JsonObject { ["kind"] = key, ["x"] = Array.FindIndex(Expected, x => x.Kind == key) * 3.0, ["y"] = 0, ["against"] = false });
        Place(f, new JsonObject { ["kind"] = "toilet", ["x"] = 0, ["y"] = 4, ["against"] = false });
        var wcId = f.Model.OfClass(FurnitureModule.Class).First(e => e.PredefinedType == "TOILETPAN").Id;
        var ifc = Export(f);
        Assert.Equal(8, Count(ifc, "IFCSANITARYTERMINAL("));
        Assert.Equal(7, Count(ifc, "IFCSANITARYTERMINALTYPE("));
        foreach (var p in new[] { "TOILETPAN", "BIDET", "WASHHANDBASIN", "SHOWER", "BATH", "SINK" }) Assert.Contains($".{p}.", ifc);
        // The accessory stays furniture, named by its item; its type a furniture type.
        Assert.Equal(1, Count(ifc, "IFCFURNITURE("));
        Assert.Equal(1, Count(ifc, "IFCFURNITURETYPE("));
        Assert.Contains("'Towel rail'", ifc);
        // The fixtures' connection points: cold and hot water in, waste out.
        Assert.Contains("IFCDISTRIBUTIONPORT(", ifc);
        Assert.Contains(".DOMESTICCOLDWATER.", ifc);
        Assert.Contains(".DOMESTICHOTWATER.", ifc);
        Assert.Contains(".WASTEWATER.", ifc);
        Assert.Contains("'Sanitary ceramic white'", ifc);
        // GlobalIds stay: a type changed, the element is written under the same GlobalId.
        var gid = wcId;
        Assert.True(GlobalId.IsValid(gid));
        Assert.Contains($"'{gid}'", ifc);
        f.Run("furniture.updateType", new JsonObject { ["id"] = FurnitureModule.TypeOf(f.Model, f.Model.Get(wcId)!)!.Id, ["spec"] = new JsonObject { ["mount"] = "floor" } });
        Assert.Contains($"'{gid}'", Export(f));

        var path = Path.Combine(Path.GetTempPath(), $"sanitary-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var pieces = g.Model.OfClass(FurnitureModule.Class).ToList();
            Assert.Equal(9, pieces.Count);
            Assert.All(pieces, p => Assert.NotNull(FurnitureModule.TypeOf(g.Model, p)));
            Assert.Equal(8, g.Model.OfClass(FurnitureModule.TypeClass).Count());
            Assert.All(pieces, p => Assert.NotEmpty(FurnitureModule.Geometry(p, g.Model)!.Parts));
            Assert.Equal(8, pieces.Count(p => p.Str("exportAs") == "IfcSanitaryTerminal"));
            Assert.Contains(pieces, p => p.PredefinedType == "BIDET" && new Spec(FurnitureModule.TypeOf(g.Model, p)!.Attrs["spec"] as JsonObject).Kind.Key == "bidet");
            // Written again, they are sanitary terminals as before.
            Assert.Equal(8, Count(Export(g), "IFCSANITARYTERMINAL("));
        }
        finally { File.Delete(path); }
    }

    static string Export(Fixture f)
    {
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    static int Count(string s, string what) => s.Split(what).Length - 1;
}
