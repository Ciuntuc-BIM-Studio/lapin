using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Furniture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Furniture (docs/plan-furniture.md): every kind a closed body of its size with its plan symbol; placed against a wall's
/// face and clear of the wall across a corner; mirrored to its other hand; chairs round a table; a kitchen along a wall;
/// a type's change on every piece; the schedules; IFC as IfcFurniture of an IfcFurnitureType, read back native.
/// </summary>
public sealed class FurnitureTests
{
    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule());

    static string Place(Fixture f, JsonObject a) { a["storeyId"] ??= f.Storey; return (string)f.Run("furniture.place", a)!["id"]!; }

    static (Vec3 Min, Vec3 Max) Bounds(Fixture f, string id)
    {
        var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    static Spec SpecOf(Fixture f, string id) => new(FurnitureModule.TypeOf(f.Model, f.Model.Get(id)!)!.Attrs["spec"] as JsonObject);

    [Fact]
    public void Every_kind_is_a_closed_body_of_its_size_with_a_plan_symbol()
    {
        var f = F();
        var x = 0.0;
        foreach (var k in FurnitureKinds.All)
        {
            var id = Place(f, new JsonObject { ["kind"] = k.Key, ["x"] = x, ["y"] = 0, ["against"] = false });
            var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
            Assert.NotEmpty(g.Parts);
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{k.Key}: {p.Name} is closed"));
            Assert.All(g.Parts, p => Assert.NotNull(p.Material));
            Assert.True(g.Parts.Sum(p => p.Solid.Faces.Length) < 6000, $"{k.Key}: low-poly");
            Assert.NotEmpty(g.Plan!);
            var (min, max) = Bounds(f, id);
            Assert.InRange(min.Z, -0.001, 0.001);
            // As wide as its type (a round table: its diameter), within its handles and a duvet's overhang.
            Assert.InRange(max.X - min.X, k.Width - 0.01, k.Width + 0.03);
            var s = SpecOf(f, id);
            var depth = k.Key switch { "sofa" when s.Str("chaise") != "none" => k.Depth + s.Num("chaiseLength", 0), "cornerSofa" => s.Num("cornerLength", 0), _ => k.Depth };
            Assert.InRange(max.Y - min.Y, depth - 0.01, depth + 0.07);
            // As high as its type: the kitchen's tall unit and taps stand above its worktop.
            if (k.Key != "kitchen") Assert.InRange(max.Z, k.Height - 0.02, k.Height + 0.15);
            x += 4;
        }
        // A type's name says its kind and size; the materials by role are the project's.
        var sofa = f.Model.OfClass(FurnitureModule.TypeClass).First(t => new Spec(t.Attrs["spec"] as JsonObject).Kind.Key == "sofa");
        Assert.Equal("Sofa 2200 × 950", sofa.Name);
        Assert.NotNull(Materials.Find(f.Model, "Upholstery fabric grey"));
    }

    [Fact]
    public void A_piece_clicked_near_a_wall_backs_onto_its_face_and_clears_the_wall_across_the_corner()
    {
        var f = F();
        var south = f.Wall(0, 0, 6, 0);
        var west = f.Wall(0, 0, 0, 5);
        var bands = Walls.Section(f.Model, f.Model.Get(south)!);
        var face = bands.Max(b => b.To); // the room is to the wall's left (+y)
        // Clicked in the room, near the south wall: its back on the face, its front to the north (turned 180°).
        var bed = Place(f, new JsonObject { ["kind"] = "bed", ["x"] = 3, ["y"] = 0.9 });
        var e = f.Model.Get(bed)!;
        Assert.Equal(180, e.Num("rotation"), 6);
        var (min, max) = Bounds(f, bed);
        Assert.InRange(min.Y - face, 0, 0.005);
        Assert.InRange((min.X + max.X) / 2, 2.99, 3.01);
        // Clicked by the corner: slid along the south wall until it is clear of the west one.
        var sofa = Place(f, new JsonObject { ["kind"] = "sofa", ["x"] = 0.4, ["y"] = 0.7 });
        var westFaces = Walls.Section(f.Model, f.Model.Get(west)!);
        var westMaxX = -westFaces.Min(b => b.From); // the west wall runs +y: its left normal is −x
        var (smin, _) = Bounds(f, sofa);
        Assert.True(smin.X >= westMaxX - 1e-3, $"the sofa stands clear of the west wall ({smin.X} ≥ {westMaxX})");
        Assert.InRange(smin.Y - face, 0, 0.005);
        // Far from any wall, or asked not to: where it was clicked, as it was turned.
        var table = Place(f, new JsonObject { ["kind"] = "table", ["x"] = 3, ["y"] = 3, ["rotation"] = 90 });
        Assert.Equal(new[] { 3.0, 3.0 }, f.Model.Get(table)!.Attrs["at"]!.AsArray().Select(v => (double)v!).ToArray());
        Assert.Equal(90, f.Model.Get(table)!.Num("rotation"), 6);
        var free = (JsonObject)f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["kind"] = "armchair", ["x"] = 3, ["y"] = 0.8, ["against"] = false })!;
        Assert.False((bool)free["against"]!);
    }

    [Fact]
    public void A_mirror_takes_a_piece_to_its_other_hand_with_its_front_where_it_was()
    {
        var f = F();
        var type = (string)f.Run("furniture.createType", new JsonObject { ["kind"] = "cornerSofa" })!["id"]!;
        var sofa = Place(f, new JsonObject { ["typeId"] = type, ["x"] = 0, ["y"] = 0, ["against"] = false });
        // The corner on the left: it comes forward (−y) on −x.
        var (min, max) = Bounds(f, sofa);
        var spec = SpecOf(f, sofa);
        double Front(string id) { var (lo, _) = Bounds(f, id); return lo.Y; }
        var g = FurnitureModule.Geometry(f.Model.Get(sofa)!, f.Model)!;
        var leftFront = g.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.Y < -spec.D / 2 - 0.1).Average(v => v.X);
        Assert.True(leftFront < 0);
        // Mirrored about the y axis: the corner now on +x, still coming forward.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(sofa), ["op"] = "mirror", ["a"] = new JsonArray(0, -5), ["b"] = new JsonArray(0, 5) });
        var m = f.Model.Get(sofa)!;
        Assert.True(m.Bool("mirror"));
        g = FurnitureModule.Geometry(m, f.Model)!;
        Assert.True(g.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.Y < -spec.D / 2 - 0.1).Average(v => v.X) > 0);
        Assert.Equal(min.Y, Front(sofa), 6);
        // The same as a type of the other hand.
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["corner"] = "right" } });
        g = FurnitureModule.Geometry(f.Model.Get(sofa)!, f.Model)!;
        Assert.True(g.Parts.SelectMany(p => p.Solid.Vertices).Where(v => v.Y < -spec.D / 2 - 0.1).Average(v => v.X) < 0);
        // Flip in place does it too, and back.
        f.Run("furniture.flip", new JsonObject { ["ids"] = new JsonArray(sofa) });
        Assert.False(f.Model.Get(sofa)!.Bool("mirror"));
        Assert.Equal(max.Z, Bounds(f, sofa).Max.Z, 6);
    }

    [Fact]
    public void Chairs_go_round_a_table_facing_it()
    {
        var f = F();
        var table = Place(f, new JsonObject { ["kind"] = "table", ["x"] = 5, ["y"] = 5, ["rotation"] = 30, ["against"] = false });
        var ids = f.Run("furniture.chairsAround", new JsonObject { ["tableId"] = table })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        // 1600 × 900: two on each long side, one at each end.
        Assert.Equal(6, ids.Count);
        foreach (var id in ids)
        {
            var c = f.Model.Get(id)!;
            var at = Args.PointOf(c.Attrs["at"]);
            var r = c.Num("rotation") * Math.PI / 180;
            var front = new Vec2(Math.Sin(r), -Math.Cos(r)); // its local −y in the world
            var toTable = (new Vec2(5, 5) - at).Normalized();
            Assert.True(Vec2.Dot(front, toTable) > 0.7, $"{c.Name} faces the table");
        }
        // A round table of 1200: five round it.
        var round = (string)f.Run("furniture.createType", new JsonObject { ["kind"] = "table", ["spec"] = new JsonObject { ["shape"] = "round", ["width"] = 1.2 } })!["id"]!;
        var rt = Place(f, new JsonObject { ["typeId"] = round, ["x"] = 0, ["y"] = 0, ["against"] = false });
        var around = f.Run("furniture.chairsAround", new JsonObject { ["tableId"] = rt })!["ids"]!.AsArray();
        Assert.Equal(6, around.Count);
        Assert.All(around, x => Assert.InRange(Args.PointOf(f.Model.Get((string)x!)!.Attrs["at"]).Length, 0.6, 1.0));
        // Not round a bed.
        var bed = Place(f, new JsonObject { ["kind"] = "bed", ["x"] = 10, ["y"] = 0, ["against"] = false });
        Assert.ThrowsAny<Exception>(() => f.Run("furniture.chairsAround", new JsonObject { ["tableId"] = bed }));
    }

    [Fact]
    public void A_kitchen_drawn_along_a_wall_backs_onto_it_with_its_sink_hob_and_tall_unit()
    {
        var f = F();
        f.Wall(0, 4, 6, 4);
        var face = Walls.Section(f.Model, f.Model.Get(f.Model.OfClass(IfcClass.Wall).Single().Id)!).Min(b => b.From);
        var k = (string)f.Run("furniture.kitchen", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 1, ["y1"] = 3.6, ["x2"] = 4.6, ["y2"] = 3.6 })!["id"]!;
        var e = f.Model.Get(k)!;
        Assert.Equal(3.6, e.Num("length"), 6);
        Assert.Equal("USERDEFINED", e.PredefinedType);
        Assert.Equal("Kitchen", e.Str("objectType"));
        var (min, max) = Bounds(f, k);
        // Its back on the wall's face (the room is to the south: the wall's right), as long as drawn, the tall unit above the worktop.
        Assert.InRange(4 + face - max.Y, 0, 0.005);
        Assert.InRange(max.X - min.X, 3.59, 3.61);
        Assert.InRange(max.Z, 2.1, 2.3);
        var g = FurnitureModule.Geometry(e, f.Model)!;
        Assert.Contains(g.Parts, p => p.Name == "Glass");
        Assert.Contains(g.Plan!, i => i.Kind == "overhead");
        // Stretched by its length: the units are re-divided.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(k), ["attrs"] = new JsonObject { ["length"] = 2.4 } });
        var (min2, max2) = Bounds(f, k);
        Assert.InRange(max2.X - min2.X, 2.39, 2.41);
    }

    [Fact]
    public void A_type_changed_changes_every_piece_of_it_and_checks_its_values()
    {
        var f = F();
        var a = Place(f, new JsonObject { ["kind"] = "wardrobe", ["x"] = 0, ["y"] = 0, ["against"] = false });
        var b = Place(f, new JsonObject { ["kind"] = "wardrobe", ["x"] = 4, ["y"] = 0, ["against"] = false });
        var type = FurnitureModule.TypeOf(f.Model, f.Model.Get(a)!)!.Id;
        Assert.Equal(type, FurnitureModule.TypeOf(f.Model, f.Model.Get(b)!)!.Id);
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["name"] = "Wardrobe hall", ["spec"] = new JsonObject { ["width"] = 2.4, ["doors"] = 4, ["opening"] = "sliding", ["materials"] = new JsonObject { ["fronts"] = "Mirror glass" } } });
        foreach (var id in new[] { a, b })
        {
            var (min, max) = Bounds(f, id);
            Assert.InRange(max.X - min.X, 2.39, 2.43);
            Assert.Equal("Wardrobe hall", f.Model.Get(id)!.Name);
        }
        Assert.NotNull(Materials.Find(f.Model, "Mirror glass"));
        Assert.Contains(FurnitureModule.Geometry(f.Model.Get(a)!, f.Model)!.Parts, p => p.Material == Materials.Find(f.Model, "Mirror glass")!.Id);
        Assert.ThrowsAny<Exception>(() => f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["opening"] = "folding" } }));
        Assert.ThrowsAny<Exception>(() => f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["doors"] = 20 } }));
        Assert.ThrowsAny<Exception>(() => f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["seats"] = 2 } }));
        // Another type of the kind, given to one of them.
        var dup = (string)f.Run("furniture.duplicateType", new JsonObject { ["id"] = type })!["id"]!;
        f.Run("furniture.updateType", new JsonObject { ["id"] = dup, ["spec"] = new JsonObject { ["width"] = 1.0, ["doors"] = 2 } });
        f.Run("furniture.setType", new JsonObject { ["ids"] = new JsonArray(b), ["typeId"] = dup });
        Assert.InRange(Bounds(f, b).Max.X - Bounds(f, b).Min.X, 0.99, 1.03);
        Assert.Equal("Wardrobe hall copy", f.Model.Get(b)!.Name);
    }

    [Fact]
    public void Plans_draw_the_symbols_and_elevations_the_bodies()
    {
        var f = F();
        var sofa = Place(f, new JsonObject { ["kind"] = "sofa", ["x"] = 0, ["y"] = 0, ["against"] = false });
        var wardrobe = Place(f, new JsonObject { ["kind"] = "wardrobe", ["x"] = 4, ["y"] = 0, ["against"] = false });
        var plan = Views.Find(f.Model, "plan:" + f.Storey)!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, plan);
        Assert.True(d != null, problem);
        Assert.Contains(d!.Lines, l => l.Element == sofa);
        // The wardrobe, taller than the cut, is drawn by its symbol (its hanging rail dashed), not cut.
        Assert.Contains(d.Lines, l => l.Element == wardrobe);
        var elevation = f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!)!;
        var (ed, _, eproblem) = Views.Generate(f.Session.Reader, elevation);
        Assert.True(ed != null, eproblem);
        Assert.Contains(ed!.Lines, l => l.Element == sofa);
    }

    [Fact]
    public void The_schedules_count_furniture_by_type_and_by_room()
    {
        var f = F();
        f.Wall(0, 0, 5, 0); f.Wall(5, 0, 5, 4); f.Wall(5, 4, 0, 4); f.Wall(0, 4, 0, 0);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2, ["name"] = "Living" })!["id"]!;
        Place(f, new JsonObject { ["kind"] = "sofa", ["x"] = 2.5, ["y"] = 0.8 });
        Place(f, new JsonObject { ["kind"] = "armchair", ["x"] = 1, ["y"] = 2, ["against"] = false });
        Place(f, new JsonObject { ["kind"] = "armchair", ["x"] = 4, ["y"] = 2, ["against"] = false });
        Place(f, new JsonObject { ["kind"] = "armchair", ["x"] = 9, ["y"] = 2, ["against"] = false });
        var q = f.Session.Reader;
        var grid = new FurnitureSchedule().Grid(q);
        var arm = grid.Rows.Single(r => r[1] == "Armchair");
        Assert.Equal("3", arm[3]);
        Assert.Contains("Living", arm[4]);
        Assert.Equal("4", grid.Rows[^1][3]);
        var byRoom = new FurnitureByRoom().Grid(q);
        var number = f.Model.Get(room)!.Str("number");
        Assert.Equal($"{number} Living", byRoom.Rows[0][0]);
        Assert.Equal("(no room)", byRoom.Rows[^1][0]);
        Assert.Equal("1", byRoom.Rows[^1][3]);
    }

    [Fact]
    public void IFC_writes_furniture_of_its_type_and_reads_it_back_native()
    {
        var f = F();
        Place(f, new JsonObject { ["kind"] = "sofa", ["x"] = 0, ["y"] = 0, ["against"] = false });
        Place(f, new JsonObject { ["kind"] = "sofa", ["x"] = 3, ["y"] = 0, ["against"] = false });
        Place(f, new JsonObject { ["kind"] = "wardrobe", ["x"] = 6, ["y"] = 0, ["against"] = false });
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(3, ifc.Split("IFCFURNITURE(").Length - 1);
        Assert.Contains(".SOFA.", ifc);
        Assert.Contains("'Wardrobe'", ifc);
        Assert.Equal(2, ifc.Split("IFCFURNITURETYPE(").Length - 1);
        Assert.Contains(".NOTDEFINED.,.SOFA.)", ifc);
        Assert.Contains("'Upholstery fabric grey'", ifc);
        var path = Path.Combine(Path.GetTempPath(), $"furniture-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var pieces = g.Model.OfClass(FurnitureModule.Class).ToList();
            Assert.Equal(3, pieces.Count);
            Assert.All(pieces, p => Assert.NotNull(FurnitureModule.TypeOf(g.Model, p)));
            Assert.Equal(2, g.Model.OfClass(FurnitureModule.TypeClass).Count());
            Assert.All(pieces, p => Assert.NotEmpty(FurnitureModule.Geometry(p, g.Model)!.Parts));
        }
        finally { File.Delete(path); }
    }
}
