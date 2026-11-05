using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

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
}
