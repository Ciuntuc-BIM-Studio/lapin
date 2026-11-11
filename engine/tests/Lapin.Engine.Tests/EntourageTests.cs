using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Entourage (docs/plan-visualisation.md, stage 4): people and cars after BimGo's figures, standing on the ground,
/// turned, in rows; drawn in plans and elevations; left out of an IFC export unless asked for.
/// </summary>
public sealed class EntourageTests
{
    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new CivilModule());
        f.Session.New();
        return f;
    }

    static string Ground(Fixture f) => Spatial.Storeys(f.Model).Single(s => s.Name == "Ground floor").Id;

    static string Add(Fixture f, JsonObject a) { a["storeyId"] ??= Ground(f); return (string)f.Run("civil.addEntourage", a)!["id"]!; }

    static (Vec3 Min, Vec3 Max) Bounds(Fixture f, string id)
    {
        var g = Entourage.Geometry(f.Model.Get(id)!, f.Model)!;
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    [Fact]
    public void Each_kind_stands_as_tall_as_it_should_with_closed_bodies()
    {
        var f = Project();
        var x = 0.0;
        foreach (var k in Entourage.Kinds)
        {
            var id = Add(f, new JsonObject { ["kind"] = k.Key, ["x"] = x, ["y"] = 0 });
            var g = Entourage.Geometry(f.Model.Get(id)!, f.Model)!;
            Assert.All(g.Parts, p => Assert.True(Measure.Volume(p.Solid) > 0, $"{k.Key}: {p.Name} is closed"));
            var (min, max) = Bounds(f, id);
            Assert.InRange(min.Z, -0.01, 0.01);
            Assert.InRange(max.Z, k.Height * 0.97, k.Height * 1.02);
            if (k.Category == "car")
            {
                Assert.InRange(max.X - min.X, k.Length * 0.97, k.Length * 1.03);
                Assert.InRange(max.Y - min.Y, k.Width * 0.97, k.Width * 1.05);
                Assert.Contains(g.Parts, p => p.Name == "glass");
                Assert.Contains(g.Parts, p => p.Name == "tyres");
            }
            else
            {
                // As wide as shoulders and arms (0.4–0.6 m for an adult), the head under its hair.
                Assert.InRange(max.X - min.X, 0.25, 0.75);
                Assert.Contains(g.Parts, p => p.Name == "skin");
                Assert.Contains(g.Parts, p => p.Name == "hair");
            }
            Assert.True(g.Parts.Sum(p => p.Solid.Faces.Length) < 4000, $"{k.Key}: low-poly");
            Assert.NotEmpty(g.Plan!);
            x += 6;
        }
        // Its own height scales it, its colour clothes it.
        var tall = Add(f, new JsonObject { ["kind"] = "man", ["x"] = 0, ["y"] = 10, ["height"] = 1.95, ["color"] = "#aa2222" });
        Assert.InRange(Bounds(f, tall).Max.Z, 1.95 * 0.97, 1.95 * 1.02);
        Assert.Contains(Entourage.Geometry(f.Model.Get(tall)!, f.Model)!.Parts, p => p.Name == "top" && p.Color == "#aa2222");
    }

    [Fact]
    public void A_car_turns_with_its_rotation_and_stands_on_the_terrain()
    {
        var f = Project();
        f.Run("civil.addTerrain", new JsonObject { ["storeyId"] = Ground(f), ["points"] = new JsonArray(new JsonArray(-50, -50, 2), new JsonArray(50, -50, 2), new JsonArray(50, 50, 2), new JsonArray(-50, 50, 2)) });
        var car = Add(f, new JsonObject { ["kind"] = "car", ["x"] = 10, ["y"] = 5, ["rotation"] = 90 });
        var (min, max) = Bounds(f, car);
        Assert.InRange(min.Z, 2 - 0.01, 2 + 0.01);
        // Turned a quarter: its length along y.
        Assert.InRange(max.Y - min.Y, 4.6 * 0.97, 4.6 * 1.03);
        Assert.InRange(max.X - min.X, 1.8 * 0.97, 1.8 * 1.05);
        Assert.InRange((min.X + max.X) / 2, 9.9, 10.1);
        // Its front (the head lights) towards +y.
        var lights = Entourage.Geometry(f.Model.Get(car)!, f.Model)!.Parts.Single(p => p.Name == "lights").Solid;
        Assert.Contains(lights.Vertices, v => v.Y > 5 + 2.2);
        // Raised above the ground (on a deck).
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(car), ["attrs"] = new JsonObject { ["offset"] = 3.0 } });
        Assert.InRange(Bounds(f, car).Min.Z, 5 - 0.01, 5 + 0.01);
    }

    [Fact]
    public void A_row_parks_cars_across_a_line_and_walks_people_along_it()
    {
        var f = Project();
        var cars = f.Run("civil.addEntourage", new JsonObject { ["storeyId"] = Ground(f), ["kind"] = "car", ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)) })!["ids"]!.AsArray();
        Assert.Equal(5, cars.Count); // every 2.5 m: 0, 2.5, 5, 7.5, 10
        var e = f.Model.Get((string)cars[2]!)!;
        Assert.Equal(90, e.Num("rotation"), 6);
        Assert.Equal(5, Args.PointOf(e.Attrs["at"]!.AsArray()).X, 6);
        Assert.Equal("Car 3", e.Name);
        var people = f.Run("civil.addEntourage", new JsonObject { ["storeyId"] = Ground(f), ["kind"] = "woman", ["points"] = new JsonArray(new JsonArray(0, 5), new JsonArray(0, 11)), ["spacing"] = 2 })!["ids"]!.AsArray();
        Assert.Equal(4, people.Count);
        // Along +y: the figure faces +y (its local −y turned by 180°).
        Assert.Equal(180, f.Model.Get((string)people[0]!)!.Num("rotation"), 6);
        // One undo step takes the whole row away.
        f.Session.Undo();
        Assert.Equal(5, f.Model.OfClass(Entourage.Class).Count());
    }

    [Fact]
    public void People_and_cars_draw_in_plans_and_elevations()
    {
        var f = Project();
        var man = Add(f, new JsonObject { ["kind"] = "man", ["x"] = 0, ["y"] = 0 });
        var car = Add(f, new JsonObject { ["kind"] = "suv", ["x"] = 5, ["y"] = 0 });
        var plan = Views.Find(f.Model, "plan:" + Ground(f))!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, plan);
        Assert.True(d != null, problem);
        Assert.Contains(d!.Lines, l => l.Element == man);
        Assert.Contains(d.Lines, l => l.Element == car);
        var elevation = f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!)!;
        var (ed, _, eproblem) = Views.Generate(f.Session.Reader, elevation);
        Assert.True(ed != null, eproblem);
        Assert.Contains(ed!.Lines, l => l.Element == man);
        Assert.Contains(ed.Lines, l => l.Element == car);
    }

    [Fact]
    public void An_IFC_export_leaves_entourage_out_unless_asked()
    {
        var f = Project();
        var man = Add(f, new JsonObject { ["kind"] = "man", ["x"] = 0, ["y"] = 0 });
        f.Run("architecture.addWall", new JsonObject { ["storeyId"] = Ground(f), ["x1"] = 0, ["y1"] = 3, ["x2"] = 5, ["y2"] = 3 });
        Assert.Contains(man, ModelSurvey.Excluded(f.Session, new JsonObject()));
        Assert.DoesNotContain(man, ModelSurvey.Excluded(f.Session, new JsonObject { ["entourage"] = true }));
        // The survey shows it unticked, under its own name; chosen, it goes.
        var kind = ModelSurvey.Of(f.Session)["groups"]!.AsArray().SelectMany(g => g!["kinds"]!.AsArray()).Single(k => (string?)k!["key"] == Entourage.Class)!;
        Assert.True((bool)kind["off"]!);
        Assert.Equal("Entourage", (string?)kind["label"]);
        Assert.DoesNotContain(man, ModelSurvey.Excluded(f.Session, new JsonObject { ["kinds"] = new JsonArray(Entourage.Class, IfcClass.Wall) }));

        string Ifc(JsonObject p)
        {
            using var ms = new MemoryStream();
            var ex = ModelSurvey.Excluded(f.Session, p);
            IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions(Exclude: ex.Count > 0 ? ex : null));
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        Assert.DoesNotContain("IFCBUILDINGELEMENTPROXY", Ifc(new JsonObject()));
        var with = Ifc(new JsonObject { ["entourage"] = true });
        Assert.Contains("IFCBUILDINGELEMENTPROXY", with);
        Assert.Contains("'Entourage'", with);
        Assert.Contains(".USERDEFINED.", with);
    }

    const string OnePixel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    static string Picture(Fixture f, string name = "Cut-out") =>
        (string)f.Run("image.add", new JsonObject { ["name"] = name, ["mime"] = "image/png", ["data"] = OnePixel })!["id"]!;

    [Fact]
    public void A_kind_of_the_project_is_made_from_a_picture_and_a_figure_carries_it()
    {
        var f = Project();
        var image = Picture(f);
        var id = (string)f.Run("civil.addEntourageKind", new JsonObject
        {
            ["imageId"] = image, ["name"] = "Cyclist", ["height"] = 1.8, ["width"] = 0.6, ["category"] = "person",
        })!["id"]!;
        var kind = f.Model.Get(id)!;
        Assert.Equal(Entourage.KindClass, kind.Class);
        Assert.Equal("Cyclist", kind.Name);
        Assert.Equal(image, kind.Str("image"));

        var figure = Add(f, new JsonObject { ["kind"] = id, ["x"] = 1, ["y"] = 2 });
        Assert.Equal(id, f.Model.Get(figure)!.Str("kind"));
        Assert.Equal("Cyclist 1", f.Model.Get(figure)!.Name);

        var resolved = Entourage.PictureOf(f.Model, f.Model.Get(figure)!)!;
        Assert.Equal("Cyclist", resolved.Label);
        Assert.Equal(1.8, resolved.Height);
        Assert.Equal(0.6, resolved.Width);
    }

    [Fact]
    public void A_figure_of_a_picture_kind_is_a_card_as_wide_and_as_tall_as_its_kind()
    {
        var f = Project();
        var kind = (string)f.Run("civil.addEntourageKind", new JsonObject
        {
            ["imageId"] = Picture(f), ["height"] = 1.9, ["width"] = 0.8,
        })!["id"]!;
        var id = Add(f, new JsonObject { ["kind"] = kind, ["x"] = 0, ["y"] = 0, ["rotation"] = 0 });
        var g = Entourage.Geometry(f.Model.Get(id)!, f.Model)!;
        Assert.Single(g.Parts);
        Assert.Equal("picture", g.Parts[0].Name);
        Assert.True(Measure.Volume(g.Parts[0].Solid) > 0);
        var (min, max) = Bounds(f, id);
        Assert.InRange(min.Z, -0.01, 0.01);
        Assert.InRange(max.Z, 1.9 * 0.97, 1.9 * 1.03);
        Assert.InRange(max.X - min.X, 0.8 * 0.97, 0.8 * 1.03);

        var taller = Add(f, new JsonObject { ["kind"] = kind, ["x"] = 2, ["y"] = 0, ["height"] = 2.4 });
        Assert.InRange(Bounds(f, taller).Max.Z, 2.4 * 0.97, 2.4 * 1.03);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(37)]
    public void A_picture_kind_draws_its_card_in_the_plan(double rotation)
    {
        var f = Project();
        var kind = (string)f.Run("civil.addEntourageKind", new JsonObject
        {
            ["imageId"] = Picture(f), ["height"] = 1.9, ["width"] = 0.8,
        })!["id"]!;
        var figure = Add(f, new JsonObject { ["kind"] = kind, ["x"] = 4, ["y"] = 2, ["rotation"] = rotation });
        var geometry = Entourage.Geometry(f.Model.Get(figure)!, f.Model)!;
        var symbol = Assert.Single(geometry.Plan!, p => p.In(Entourage2d.Outline));
        Assert.DoesNotContain(geometry.Plan!, p => p.In("none"));
        Assert.True(symbol.Closed);
        Assert.Equal(4, symbol.Points.Count);
        var a = rotation * Math.PI / 180;
        var local = symbol.Points.Select(p => new Vec2((p.X - 4) * Math.Cos(a) + (p.Y - 2) * Math.Sin(a), -(p.X - 4) * Math.Sin(a) + (p.Y - 2) * Math.Cos(a))).ToList();
        Assert.Equal(0.8, local.Max(p => p.X) - local.Min(p => p.X), 6);
        Assert.Equal(0.04, local.Max(p => p.Y) - local.Min(p => p.Y), 6);
        var plan = Views.Find(f.Model, "plan:" + Ground(f))!;
        var (drawing, _, problem) = Views.Generate(f.Session.Reader, plan);
        Assert.True(drawing != null, problem);
        Assert.Contains(drawing!.Lines, l => l.Element == figure);
    }

    [Fact]
    public void A_kind_can_be_renamed_and_resized_after_it_is_made()
    {
        var f = Project();
        var kind = (string)f.Run("civil.addEntourageKind", new JsonObject
        {
            ["imageId"] = Picture(f), ["name"] = "First", ["height"] = 1.6, ["width"] = 0.5,
        })!["id"]!;
        var figure = Add(f, new JsonObject { ["kind"] = kind, ["x"] = 0, ["y"] = 0 });

        f.Run("element.rename", new JsonObject { ["id"] = kind, ["name"] = "Cyclist" });
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(kind), ["attrs"] = new JsonObject { ["height"] = 2.1, ["width"] = 0.9 } });

        Assert.Equal("Cyclist", f.Model.Get(kind)!.Name);
        Assert.Equal("Cyclist", Entourage.PictureOf(f.Model, f.Model.Get(figure)!)!.Label);
        var (min, max) = Bounds(f, figure);
        Assert.InRange(max.Z, 2.1 * 0.97, 2.1 * 1.03);
        Assert.InRange(max.X - min.X, 0.9 * 0.97, 0.9 * 1.03);
    }

    [Fact]
    public void A_kind_needs_a_picture_of_the_project()
    {
        var f = Project();
        Assert.Throws<CommandException>(() => f.Run("civil.addEntourageKind", new JsonObject { ["imageId"] = "nowhere" }));
        Assert.Throws<CommandException>(() => f.Run("civil.addEntourage", new JsonObject { ["kind"] = "nowhere", ["x"] = 0, ["y"] = 0 }));
    }

    [Fact]
    public void A_picture_is_kept_once_however_many_figures_use_it_and_undo_takes_the_kind_away()
    {
        var f = Project();
        var image = Picture(f);
        var kind = (string)f.Run("civil.addEntourageKind", new JsonObject { ["imageId"] = image })!["id"]!;
        Add(f, new JsonObject { ["kind"] = kind, ["x"] = 0, ["y"] = 0 });
        Add(f, new JsonObject { ["kind"] = kind, ["x"] = 2, ["y"] = 0 });
        Assert.Single(f.Model.OfClass(Images.Class));
        Assert.Equal(2, f.Model.OfClass(Entourage.Class).Count());

        f.Session.Undo();
        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(f.Model.Get(kind));
        Assert.Empty(f.Model.OfClass(Entourage.Class));
    }

    [Fact]
    public void A_kind_and_its_figures_come_back_after_saving_and_reopening()
    {
        var f = Project();
        var image = Picture(f);
        var kind = (string)f.Run("civil.addEntourageKind", new JsonObject { ["imageId"] = image, ["name"] = "Van" })!["id"]!;
        var figure = Add(f, new JsonObject { ["kind"] = kind, ["x"] = 3, ["y"] = 4 });
        var files = new ProjectFiles((_, _) => throw new InvalidOperationException("no files"), (_, _, _, _, _) => throw new InvalidOperationException("no files"));
        var (bytes, _) = files.SaveBytes(f.Session, "entourage.lapin");

        var g = Project();
        files.OpenBytes(g.Session, "entourage.lapin", bytes);
        Assert.Equal(Entourage.KindClass, g.Model.Get(kind)!.Class);
        Assert.Equal(image, g.Model.Get(kind)!.Str("image"));
        Assert.Equal("Van", g.Model.Get(kind)!.Name);
        Assert.Equal(kind, g.Model.Get(figure)!.Str("kind"));
    }

    // ---- Picture kinds: deleting, the picture, exports (PR #21's follow-up, decision 468) ----

    static string PictureKind(Fixture f, string name = "Cyclist", string? image = null) =>
        (string)f.Run("civil.addEntourageKind", new JsonObject { ["imageId"] = image ?? Picture(f), ["name"] = name })!["id"]!;

    static void Delete(Fixture f, params string[] ids) => f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]) });

    [Fact]
    public void A_kind_figures_use_is_not_deleted_and_says_how_many_use_it()
    {
        var f = Project();
        var kind = PictureKind(f);
        var a = Add(f, new JsonObject { ["kind"] = kind, ["x"] = 0, ["y"] = 0 });
        Add(f, new JsonObject { ["kind"] = kind, ["x"] = 2, ["y"] = 0 });
        var refused = Assert.Throws<CommandException>(() => Delete(f, kind));
        Assert.Contains("2 figure(s)", refused.Message);
        Assert.Contains("Cyclist", refused.Message);
        Assert.NotNull(f.Model.Get(kind));
        Assert.Equal(2, f.Model.OfClass(Entourage.Class).Count(x => x.Str("kind") == kind));

        // One left: still refused; the kind with its last figure, in either order, goes.
        Delete(f, f.Model.OfClass(Entourage.Class).Single(x => x.Id != a).Id);
        Assert.Contains("1 figure(s)", Assert.Throws<CommandException>(() => Delete(f, kind)).Message);
        Delete(f, kind, a);
        Assert.Null(f.Model.Get(kind));
        Assert.Empty(f.Model.OfClass(Entourage.Class));
    }

    [Fact]
    public void A_kind_deleted_takes_its_picture_unless_something_else_uses_it_and_undo_brings_both_back()
    {
        var f = Project();
        var image = Picture(f);
        var kind = PictureKind(f, image: image);
        Delete(f, kind);
        Assert.Null(f.Model.Get(kind));
        Assert.Null(f.Model.Get(image));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(kind));
        Assert.NotNull(f.Model.Get(image));

        // Two kinds of one picture: the first deleted leaves it to the second.
        var second = PictureKind(f, "Second", image);
        Delete(f, kind);
        Assert.NotNull(f.Model.Get(image));
        Assert.Equal(image, f.Model.Get(second)!.Str("image"));
    }

    [Fact]
    public void The_picture_a_kind_shows_is_not_deleted_nor_dropped_by_a_material_letting_it_go()
    {
        var f = Project();
        var image = Picture(f, "Tree cut-out");
        var kind = PictureKind(f, image: image);
        var refused = Assert.Throws<CommandException>(() => Delete(f, image));
        Assert.Contains("Cyclist", refused.Message);
        Assert.NotNull(f.Model.Get(image));

        // A material that wore the same image and lets it go leaves it to the kind.
        var mat = f.Model.OfClass(IfcClass.Material).FirstOrDefault()?.Id
            ?? (string)f.Run("material.create", new JsonObject { ["name"] = "Board" })!["id"]!;
        f.Run("material.update", new JsonObject { ["id"] = mat, ["textureImage"] = image });
        f.Run("material.update", new JsonObject { ["id"] = mat, ["textureImage"] = null });
        Assert.NotNull(f.Model.Get(image));

        // Deleted with its kind, the picture goes.
        Delete(f, image, kind);
        Assert.Null(f.Model.Get(image));
        Assert.Null(f.Model.Get(kind));
    }

    [Fact]
    public void A_kind_is_made_from_a_file_in_one_undo_step_and_its_group_is_checked()
    {
        var f = Project();
        var made = f.Run("civil.addEntourageKind", new JsonObject
        {
            ["data"] = OnePixel, ["mime"] = "image/png", ["name"] = "Van", ["height"] = 2.1, ["width"] = 4.8, ["category"] = "car",
        })!;
        var kind = f.Model.Get((string)made["id"]!)!;
        var image = f.Model.Get((string)made["imageId"]!)!;
        Assert.Equal(Images.Class, image.Class);
        Assert.Equal(image.Id, kind.Str("image"));
        Assert.Equal("car", kind.Str("category"));
        Assert.Equal(4.8, kind.Num("width"));

        // A row of a car kind is spaced as cars are.
        var row = (JsonArray)f.Run("civil.addEntourage", new JsonObject { ["kind"] = kind.Id, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(10, 0)), ["storeyId"] = Ground(f) })!["ids"]!;
        Assert.Equal(5, row.Count);

        Assert.Throws<CommandException>(() => f.Run("civil.addEntourageKind", new JsonObject { ["imageId"] = image.Id, ["category"] = "vehicle" }));
        Assert.Throws<CommandException>(() => f.Run("civil.addEntourageKind", new JsonObject { ["data"] = OnePixel, ["mime"] = "image/jpeg" }));

        f.Session.Undo();
        f.Session.Undo();
        Assert.Null(f.Model.Get(kind.Id));
        Assert.Null(f.Model.Get(image.Id));
    }

    [Fact]
    public void A_figure_is_turned_into_a_picture_kind_in_its_properties_and_back()
    {
        var f = Project();
        var kind = PictureKind(f);
        var man = Add(f, new JsonObject { ["kind"] = "man", ["x"] = 0, ["y"] = 0 });
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(man), ["attrs"] = new JsonObject { ["kind"] = kind } });
        Assert.Equal(kind, f.Model.Get(man)!.Str("kind"));
        Assert.Equal("picture", Assert.Single(Entourage.Geometry(f.Model.Get(man)!, f.Model)!.Parts).Name);
        Assert.Equal(Entourage.PictureHeight, Entourage.HeightOf(f.Model, f.Model.Get(man)!));
        f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(man), ["attrs"] = new JsonObject { ["kind"] = "woman" } });
        Assert.Equal("woman", f.Model.Get(man)!.Str("kind"));
        Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["ids"] = new JsonArray(man), ["attrs"] = new JsonObject { ["kind"] = "nowhere" } }));
        Assert.Equal("woman", f.Model.Get(man)!.Str("kind"));
    }

    [Fact]
    public void A_picture_figure_draws_a_flat_rectangle_in_elevations_and_nothing_when_the_style_draws_no_entourage()
    {
        var f = Project();
        var kind = (string)f.Run("civil.addEntourageKind", new JsonObject { ["imageId"] = Picture(f), ["height"] = 1.9, ["width"] = 0.8 })!["id"]!;
        var figure = Add(f, new JsonObject { ["kind"] = kind, ["x"] = 0, ["y"] = 0, ["rotation"] = 0 });
        var g = Entourage.Geometry(f.Model.Get(figure)!, f.Model)!;
        var outline = Assert.Single(g.Silhouettes!, s => s.Mode == Entourage2d.Outline);
        var side = Assert.Single(outline.Side).Points;
        Assert.Equal(0.8, side.Max(p => p.X) - side.Min(p => p.X), 6);
        Assert.Equal(1.9, side.Max(p => p.Y) - side.Min(p => p.Y), 6);

        var elevation = f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!)!;
        var (d, _, problem) = Views.Generate(f.Session.Reader, elevation);
        Assert.True(d != null, problem);
        var lines = d!.Lines.Where(l => l.Element == figure).ToList();
        Assert.Equal(4, lines.Count);
        var xs = lines.SelectMany(l => new[] { l.A.X, l.B.X }).ToList();
        Assert.Equal(0.8, xs.Max() - xs.Min(), 3);
    }

    [Fact]
    public void An_IFC_export_writes_a_picture_figure_as_a_proxy_and_never_the_kind_or_its_picture()
    {
        var f = Project();
        var image = Picture(f, "PictureXyz");
        var kind = (string)f.Run("civil.addEntourageKind", new JsonObject { ["imageId"] = image, ["name"] = "KindQwerty" })!["id"]!;
        Add(f, new JsonObject { ["kind"] = kind, ["x"] = 0, ["y"] = 0, ["name"] = "Figure one" });
        using var ms = new MemoryStream();
        var ex = ModelSurvey.Excluded(f.Session, new JsonObject { ["entourage"] = true });
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions(Exclude: ex.Count > 0 ? ex : null));
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("IFCBUILDINGELEMENTPROXY", ifc);
        Assert.Contains("'Figure one'", ifc);
        Assert.DoesNotContain("KindQwerty", ifc);
        Assert.DoesNotContain("PictureXyz", ifc);
        Assert.DoesNotContain(Entourage.KindClass, ifc, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(OnePixel[..24], ifc);
        // The kind's id is named only by the figure's own intent (Lapin_Intent's kind), never as an entity of the file.
        Assert.Equal([$"IFCPROPERTYSINGLEVALUE('kind',$,IFCLABEL('{kind}'),$);"], ifc.Split('\n').Where(l => l.Contains(kind)).Select(l => l[(l.IndexOf('=') + 1)..].Trim()));
    }
}
