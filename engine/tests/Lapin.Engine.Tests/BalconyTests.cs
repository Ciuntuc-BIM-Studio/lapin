using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Balconies;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Balconies (a tester's report, #96): every built-in style builds closed parts on a wall's face; a loggia cuts its
/// recess into its wall and stands its back wall, following the loggia and going with it; the railing's length by its
/// sides; stacked on the storeys above in one step, on posts down to the balcony below; project styles saved, saved
/// again on every balcony of theirs, renamed and deleted; the schedule; IFC as an element assembly read back native.
/// </summary>
public sealed class BalconyTests
{
    /// <summary>A ground floor, a first floor at 3 m and a second at 6 m; a wall of 25 cm brick along y = 0 on each.</summary>
    static (Fixture F, string Wall, string First, string Second) Building()
    {
        var f = new Fixture(new ArchitectureModule());
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3.0, ["height"] = 3.0 })!["id"]!;
        var second = (string)f.Run("storey.create", new JsonObject { ["name"] = "Second floor", ["elevation"] = 6.0, ["height"] = 3.0 })!["id"]!;
        var type = f.Type("Brick 25", ("Brick", 0.25, true));
        var wall = f.Wall(0, 0, 10, 0, type);
        return (f, wall, first, second);
    }

    static string Add(Fixture f, string style, double x, double y, JsonObject? extra = null, string? storey = null)
    {
        var a = new JsonObject { ["storeyId"] = storey ?? f.Storey, ["style"] = style, ["x"] = x, ["y"] = y };
        foreach (var (k, v) in extra ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("architecture.addBalcony", a)!["id"]!;
    }

    static ElementGeometry Geo(Fixture f, string id) => f.Session.Geometry(id) ?? throw new InvalidOperationException("no geometry");

    [Fact]
    public void Every_style_builds_closed_parts_on_the_wall_face_with_a_plan()
    {
        var (f, wall, _, _) = Building();
        var x = 1.5;
        foreach (var s in BalconySpec.Styles)
        {
            var id = s.Id == "mezzanine"
                ? (string)f.Run("architecture.addBalcony", new JsonObject { ["storeyId"] = f.Storey, ["style"] = s.Id, ["x1"] = 0, ["y1"] = 8, ["x2"] = 5, ["y2"] = 8 })!["id"]!
                : Add(f, s.Id, s.Id == "corner" ? 9.5 : x, -0.5);
            if (s.Id != "corner") x += s.Id == "juliet" ? 2.2 : 0;
            var b = f.Model.Get(id)!;
            Assert.Equal((string?)s.Fields()["kind"], b.Str("kind"));
            var g = Geo(f, id);
            Assert.NotEmpty(g.Parts);
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{s.Id}: {p.Name} is closed"));
            Assert.Contains(g.Parts, p => p.Key is "balcony-railing" or "balcony-parapet");
            Assert.NotEmpty(g.Plan!);
            // Hosted by the wall, the face at y = −0.125, out to −y; the mezzanine along its line.
            if (s.Id != "mezzanine")
            {
                Assert.Equal(wall, b.Str("wallId"));
                var slab = g.Parts.First(p => p.Key == (s.Id == "loggia" ? "balcony-slab" : "balcony-slab"));
                var box = Measure.Bounds(slab.Solid)!.Value;
                if (s.Id == "loggia") Assert.True(box.Max.Y > 0.125 + 1, "a loggia's floor goes in behind the face");
                else if (s.Id == "corner") Assert.InRange(box.Max.Y, 1.8, 1.9); // its return runs back along the side wall
                else Assert.InRange(box.Max.Y, -0.125 - 0.081, -0.125 + 1e-6);
                Assert.True(box.Min.Y < -0.125 - (s.Id == "loggia" ? 0.05 : 0.2), $"{s.Id} stands out of the face");
            }
            // Its support, by its style.
            var support = s.Id is "loggia" or "mezzanine" ? "none" : (string?)s.Fields()["support"];
            if (support == "brackets") Assert.Contains(g.Parts, p => p.Key == "balcony-brackets");
            if (support == "hangers") Assert.Contains(g.Parts, p => p.Key == "balcony-hangers");
            Assert.NotEmpty(f.Session.Reader.Quantities(id));
            x += 3.4;
            if (x > 8) x = 1.5;
            f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        }
        Assert.Empty(f.Model.OfClass(BalconyModule.Class));
        // The project took a type for each style used.
        Assert.Equal(BalconySpec.Styles.Count, f.Model.OfClass(BalconyModule.TypeClass).Count());
    }

    [Fact]
    public void A_juliet_balcony_centres_on_the_door_it_is_clicked_by()
    {
        var (f, wall, _, _) = Building();
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 4.0 });
        var id = Add(f, "juliet", 4.3, -0.4);
        var b = f.Model.Get(id)!;
        Assert.Equal(4.0, b.Num("along"), 6);
        Assert.Equal(0.9 + 0.2, b.Num("width"), 6);
        Assert.Equal("brackets", b.Str("support"));
        Assert.Equal(2, BalconyBuilder.Build(f.Model, b)!.Brackets.Count);
    }

    [Fact]
    public void A_loggia_cuts_its_wall_stands_its_back_wall_follows_and_goes_with_it()
    {
        var (f, wall, _, _) = Building();
        var volume = Geo(f, wall).Parts.Sum(p => Measure.Volume(p.Solid));
        var id = Add(f, "loggia", 5, -0.5);
        var recess = BalconyModule.RecessOf(f.Model, id)!;
        Assert.Equal(wall, Openings.WallOf(f.Model, recess.Id)!.Id);
        Assert.Equal(3.0, recess.Num("width"), 6);
        Assert.Equal(2.6, recess.Num("height"), 6);
        Assert.Equal(5.0, recess.Num("along"), 6);
        // The wall has lost the recess: 3 × 2.6 × 0.25.
        var cut = Geo(f, wall).Parts.Sum(p => Measure.Volume(p.Solid));
        Assert.Equal(volume - 3 * 2.6 * 0.25, cut, 3);
        // The back wall, of the host's type, its face the loggia's depth behind the host's, between the side walls.
        var back = BalconyModule.BackWallOf(f.Model, id)!;
        Assert.Equal(Walls.TypeOf(f.Model, wall)!.Id, Walls.TypeOf(f.Model, back.Id)!.Id);
        Assert.Equal(1.6, Walls.Start(back).Y, 6);
        Assert.Equal(5 - 1.5 - 0.2, Math.Min(Walls.Start(back).X, Walls.End(back).X), 6);
        var g = Geo(f, id);
        Assert.Contains(g.Parts, p => p.Key == "balcony-cheeks");
        Assert.Contains(g.Parts, p => p.Key == "balcony-ceiling");
        // Wider and deeper in its properties: the recess and the back wall follow.
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(id), ["attrs"] = new JsonObject { ["width"] = 4.0, ["depth"] = 2.0 } });
        Assert.Equal(4.0, BalconyModule.RecessOf(f.Model, id)!.Num("width"), 6);
        Assert.Equal(2.0, Walls.Start(BalconyModule.BackWallOf(f.Model, id)!).Y, 6);
        // Slid along the wall by its grip.
        f.Run("architecture.updateBalcony", new JsonObject { ["id"] = id, ["x"] = 6.0, ["y"] = -1 });
        Assert.Equal(6.0, BalconyModule.RecessOf(f.Model, id)!.Num("along"), 6);
        // No longer a loggia: no recess, no back wall.
        f.Run("architecture.updateBalcony", new JsonObject { ["id"] = id, ["style"] = "cantilever" });
        Assert.Null(BalconyModule.RecessOf(f.Model, id));
        Assert.Null(BalconyModule.BackWallOf(f.Model, id));
        f.Run("architecture.updateBalcony", new JsonObject { ["id"] = id, ["style"] = "loggia" });
        Assert.NotNull(BalconyModule.RecessOf(f.Model, id));
        // Deleted: the recess and the back wall go with it.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Empty(f.Model.OfClass(IfcClass.OpeningElement));
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        Assert.Equal(volume, Geo(f, wall).Parts.Sum(p => Measure.Volume(p.Solid)), 6);
    }

    [Fact]
    public void The_railing_runs_along_the_sides_and_front_or_the_front_alone()
    {
        var (f, _, _, _) = Building();
        var id = Add(f, "cantilever", 5, -0.5);
        var l = BalconyBuilder.Build(f.Model, f.Model.Get(id)!)!;
        // 3.0 × 1.5, 50 mm in from the edges and from the wall: 1.40 + 2.90 + 1.40.
        Assert.Equal(5.7, l.RailLength, 6);
        var q = f.Session.Reader.Quantities(id).Single(s => s.Name == "Qto_RailingBaseQuantities");
        Assert.Equal(5.7, q.Quantities.Single().Value, 6);
        Assert.Equal(4.5, f.Session.Reader.Quantities(id).Single(s => s.Name == "Qto_SlabBaseQuantities").Quantities.Single(x => x.Name == "GrossArea").Value, 6);
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(id), ["attrs"] = new JsonObject { ["railSides"] = "front" } });
        Assert.Equal(2.9, BalconyBuilder.Build(f.Model, f.Model.Get(id)!)!.RailLength, 6);
        // A corner balcony: its front round the corner and its two free ends.
        var corner = Add(f, "corner", 9.6, -0.5);
        var c = BalconyBuilder.Build(f.Model, f.Model.Get(corner)!)!;
        Assert.Equal(new[] { "wall", "wall", "side", "front", "front", "side" }.Order(), c.Roles.Order());
        Assert.Single(c.Rails);
        Assert.Equal(3.0 * 1.5 + 1.5 * (1.5 + 2.0), c.Area, 6);
    }

    [Fact]
    public void Stacked_on_the_storeys_above_in_one_step_on_posts_down_to_the_balcony_below()
    {
        var (f, _, first, second) = Building();
        var type = Walls.TypeOf(f.Model, f.Model.OfClass(IfcClass.Wall).First().Id)!.Id;
        string WallOn(string storey) => (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 0, ["typeId"] = type })!["id"]!;
        var (w1, w2) = (WallOn(first), WallOn(second));
        var id = Add(f, "stacked", 5, -0.5, storey: first);
        Assert.Equal(w1, f.Model.Get(id)!.Str("wallId"));
        // On the first floor the posts stand on the ground (±0.00) and reach the soffit: 3 m less the finish, the slab and the soffit.
        var posts = Measure.Bounds(Geo(f, id).Parts.Single(p => p.Key == "balcony-posts").Solid)!.Value;
        Assert.Equal(0, posts.Min.Z, 6);
        Assert.Equal(3 - 0.05 - 0.2 - 0.015, posts.Max.Z, 6);
        // One step: undone, only the first-floor balcony is left; done again.
        f.Run("architecture.stackBalcony", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Equal(2, f.Model.OfClass(BalconyModule.Class).Count());
        f.Session.Undo();
        Assert.Single(f.Model.OfClass(BalconyModule.Class));
        f.Run("architecture.stackBalcony", new JsonObject { ["ids"] = new JsonArray(id) });
        var on2 = f.Model.OfClass(BalconyModule.Class).Single(b => Spatial.StoreyOf(f.Model, b.Id)?.Id == second);
        Assert.Equal(id, f.Model.OfClass(BalconyModule.Class).Single(b => Spatial.StoreyOf(f.Model, b.Id)?.Id == first).Id);
        Assert.Equal(f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Id, f.Model.RelatingOf(on2.Id, IfcClass.RelDefinesByType)!.Id);
        // The copy hangs on the second floor's wall.
        Assert.Equal(w2, on2.Str("wallId"));
        var p2 = Measure.Bounds(Geo(f, on2.Id).Parts.Single(p => p.Key == "balcony-posts").Solid)!.Value;
        Assert.Equal(3, p2.Min.Z, 6);
        // Stacked again: the storey above already has it, so nothing is added.
        f.Run("architecture.stackBalcony", new JsonObject { ["ids"] = new JsonArray(id) });
        Assert.Equal(2, f.Model.OfClass(BalconyModule.Class).Count());
    }

    [Fact]
    public void Stacked_loggias_cut_the_wall_of_their_own_storey()
    {
        var (f, wall, first, _) = Building();
        var type = Walls.TypeOf(f.Model, wall)!.Id;
        var upper = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = first, ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 0, ["typeId"] = type })!["id"]!;
        var id = Add(f, "loggia", 5, -0.5);
        f.Run("architecture.stackBalcony", new JsonObject { ["ids"] = new JsonArray(id), ["storeyIds"] = new JsonArray(first) });
        var copy = f.Model.OfClass(BalconyModule.Class).Single(b => b.Id != id);
        Assert.Equal(upper, copy.Str("wallId"));
        Assert.Equal(upper, Openings.WallOf(f.Model, BalconyModule.RecessOf(f.Model, copy.Id)!.Id)!.Id);
        Assert.Equal(wall, Openings.WallOf(f.Model, BalconyModule.RecessOf(f.Model, id)!.Id)!.Id);
        Assert.Equal(first, Spatial.StoreyOf(f.Model, BalconyModule.BackWallOf(f.Model, copy.Id)!.Id)!.Id);
    }

    [Fact]
    public void Project_styles_are_saved_saved_again_on_their_balconies_renamed_and_deleted()
    {
        var (f, _, _, _) = Building();
        var a = Add(f, "cantilever", 2, -0.5);
        var b = Add(f, "cantilever", 7, -0.5);
        var sid = (string)f.Run("architecture.saveBalconyStyle", new JsonObject { ["id"] = a, ["name"] = "Studio balcony", ["attrs"] = new JsonObject { ["depth"] = 1.2, ["railStyle"] = "cable" } })!["styleId"]!;
        Assert.Equal("Studio balcony", f.Model.Get(sid)!.Name);
        Assert.Equal(sid, f.Model.RelatingOf(a, IfcClass.RelDefinesByType)!.Id);
        Assert.Equal(1.2, f.Model.Get(a)!.Num("depth"), 6);
        f.Run("architecture.updateBalcony", new JsonObject { ["id"] = b, ["style"] = sid });
        Assert.Equal("cable", f.Model.Get(b)!.Str("railStyle"));
        // Saved again from the first: the second follows.
        f.Run("architecture.saveBalconyStyle", new JsonObject { ["id"] = a, ["name"] = "Studio balcony", ["styleId"] = sid, ["attrs"] = new JsonObject { ["railStyle"] = "glass" } });
        Assert.Equal("glass", f.Model.Get(b)!.Str("railStyle"));
        f.Run("architecture.renameBalconyStyle", new JsonObject { ["styleId"] = sid, ["name"] = "Glass 1200" });
        Assert.Equal("Glass 1200", f.Model.Get(sid)!.Name);
        f.Run("architecture.deleteBalconyStyle", new JsonObject { ["styleId"] = sid });
        Assert.Null(f.Model.Get(sid));
        Assert.Equal("glass", f.Model.Get(b)!.Str("railStyle"));
        Assert.Null(f.Model.RelatingOf(b, IfcClass.RelDefinesByType));
        // The configurator's preview: the plan, a section and a summary, as the draft would be.
        var p = (JsonObject)f.Session.Query("architecture.balconyPreview", new JsonObject { ["id"] = b, ["style"] = "brackets" })!;
        Assert.Null((string?)p["problem"]);
        Assert.Equal(8, p["outline"]!.AsArray().Count);
        Assert.Contains(p["section"]!["items"]!.AsArray(), i => (string?)i!["role"] == "brackets");
        Assert.Equal("brackets", (string?)p["attrs"]!["support"]);
    }

    [Fact]
    public void The_schedule_lists_each_balcony_with_its_area_and_railing_and_totals()
    {
        var (f, _, first, _) = Building();
        Add(f, "cantilever", 2, -0.5);
        Add(f, "stacked", 7, -0.5, storey: first);
        var grid = new BalconySchedule().Grid(f.Session.Reader);
        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal("cantilever", grid.Rows[0][3]);
        Assert.Equal("4.50", grid.Rows[0][6]);
        Assert.Equal("5.70", grid.Rows[0][7]);
        Assert.Equal("posts", grid.Rows[1][9]);
        Assert.Equal("Total", grid.Rows[2][0]);
        Assert.Equal((4.5 + 3.6 * 1.8).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), grid.Rows[2][6]);
    }

    [Fact]
    public void IFC_writes_a_balcony_as_an_element_assembly_and_reads_it_back_native()
    {
        var (f, wall, _, _) = Building();
        var cantilever = Add(f, "cantilever", 2, -0.5);
        var loggia = Add(f, "loggia", 7, -0.5);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(2, ifc.Split("IFCELEMENTASSEMBLY(").Length - 1);
        Assert.Contains("'Balcony',.USERDEFINED.)", ifc.Replace("$,", ""));
        Assert.Contains("IFCELEMENTASSEMBLYTYPE(", ifc);
        Assert.Contains(".FLOOR.", ifc);
        Assert.Contains(".GUARDRAIL.", ifc);
        Assert.Contains(".FLOORING.", ifc);
        Assert.Contains("'Lapin_Intent'", ifc);
        var path = Path.Combine(Path.GetTempPath(), $"balcony-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = new Fixture(new ArchitectureModule());
            IfcImport.Run(g.Session, path, null, out _);
            var back = g.Model.OfClass(BalconyModule.Class).ToList();
            Assert.Equal(2, back.Count);
            var c = g.Model.Get(cantilever)!;
            Assert.Equal("cantilever", c.Str("kind"));
            Assert.Equal(wall, c.Str("wallId"));
            Assert.NotNull(g.Model.RelatingOf(c.Id, IfcClass.RelDefinesByType));
            Assert.Equal(Geo(f, cantilever).Parts.Count, Geo(g, cantilever).Parts.Count);
            Assert.Equal(5.7, BalconyBuilder.Build(g.Model, c)!.RailLength, 6);
            // The loggia's recess comes back in its wall, and its back wall.
            Assert.Equal(wall, Openings.WallOf(g.Model, BalconyModule.RecessOf(g.Model, loggia)!.Id)!.Id);
            Assert.NotNull(BalconyModule.BackWallOf(g.Model, loggia));
            // No part came back as an element of its own.
            Assert.Empty(g.Model.OfClass("IfcRailing"));
            Assert.Empty(g.Model.OfClass(IfcClass.Slab));
        }
        finally { File.Delete(path); }
    }
}
