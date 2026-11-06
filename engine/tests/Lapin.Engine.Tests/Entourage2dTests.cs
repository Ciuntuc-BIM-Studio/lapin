using System.Diagnostics;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drawing;
using Lapin.Drawing.Export;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Entourage in 2D (decision 341): people, cars and plants drawn in plans as simple symbols and in elevations and
/// sections as one contour each, never their bodies' edges; the view style's <c>entourage</c> (outline by default,
/// abstract, detailed, none) and <c>entourageFill</c>; a thin pen; on sheets and in DXF.
/// </summary>
public sealed class Entourage2dTests
{
    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new CivilModule());
        f.Session.New();
        return f;
    }

    static string Ground(Fixture f) => Spatial.Storeys(f.Model).Single(s => s.Name == "Ground floor").Id;

    static string Person(Fixture f, string kind, double x, double y = 0, double rotation = 0) =>
        (string)f.Run("civil.addEntourage", new JsonObject { ["storeyId"] = Ground(f), ["kind"] = kind, ["x"] = x, ["y"] = y, ["rotation"] = rotation })!["id"]!;

    static string Plant(Fixture f, string species, double x, double y = 0) =>
        (string)f.Run("civil.addPlanting", new JsonObject { ["storeyId"] = Ground(f), ["x"] = x, ["y"] = y, ["species"] = new JsonArray(new JsonObject { ["name"] = species }) })!["id"]!;

    static Entity Plan(Fixture f) => f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = Ground(f) })!["id"]!)!;

    static Entity Elevation(Fixture f, string direction = "south") =>
        f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = direction })!["id"]!)!;

    static ViewDrawing Draw(Fixture f, Entity view)
    {
        var (d, _, problem) = Views.Generate(f.Session.Reader, Views.Find(f.Model, view.Id) ?? view);
        Assert.True(d != null, problem);
        return d!;
    }

    static List<DLine> Of(ViewDrawing d, string id) => d.Lines.Where(l => l.Element == id).ToList();

    static void Style(Fixture f, Entity view, JsonObject a) { a["id"] = view.Id; f.Run("view.setStyle", a); }

    /// <summary>The closed contours the lines make: every end shared by exactly two lines, counted as loops.</summary>
    static int Loops(IReadOnlyList<DLine> lines)
    {
        static (long, long) K(Vec2 p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
        var ends = new Dictionary<(long, long), List<int>>();
        for (var i = 0; i < lines.Count; i++)
            foreach (var p in new[] { lines[i].A, lines[i].B })
            {
                if (!ends.TryGetValue(K(p), out var l)) ends[K(p)] = l = [];
                l.Add(i);
            }
        Assert.All(ends, e => Assert.Equal(2, e.Value.Count));
        var seen = new bool[lines.Count];
        var loops = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (seen[i]) continue;
            loops++;
            var stack = new Stack<int>([i]);
            while (stack.Count > 0)
            {
                var j = stack.Pop();
                if (seen[j]) continue;
                seen[j] = true;
                foreach (var p in new[] { lines[j].A, lines[j].B }) foreach (var k in ends[K(p)]) if (!seen[k]) stack.Push(k);
            }
        }
        return loops;
    }

    static readonly string[] Plants = ["Walnut", "Apple", "Silver fir", "Scots pine", "Lilac (Syringa)", "Boxwood (Buxus)"];

    [Fact]
    public void Each_kind_draws_a_few_segments_in_plan_and_one_contour_in_an_elevation()
    {
        var f = Project();
        var ids = new List<(string Id, string What)>();
        var x = 0.0;
        foreach (var k in Entourage.Kinds) { ids.Add((Person(f, k.Key, x, 0, 30), k.Key)); x += 7; }
        foreach (var sp in Plants) { ids.Add((Plant(f, sp, x), sp)); x += 14; }
        var plan = Draw(f, Plan(f));
        var south = Draw(f, Elevation(f));
        var east = Draw(f, Elevation(f, "east"));
        foreach (var (id, what) in ids)
        {
            var p = Of(plan, id);
            Assert.InRange(p.Count, 4, 32);
            Assert.All(p, l => Assert.Equal(LineRole.Projection, l.Role));
            Assert.DoesNotContain(plan.Fills ?? [], fl => fl.Element == id);
            foreach (var d in new[] { south, east })
            {
                var e = Of(d, id);
                Assert.InRange(e.Count, 6, 48);
                // One contour, nothing inside it: no hidden lines, no edges of the body.
                Assert.Equal(1, Loops(e));
                Assert.All(e, l => Assert.Equal(LineRole.Projection, l.Role));
            }
        }
        // As tall as the figure, standing on the ground.
        var man = Of(south, ids[0].Id).SelectMany(l => new[] { l.A, l.B }).ToList();
        Assert.InRange(man.Min(q => q.Y), -1e-6, 1e-6);
        Assert.InRange(man.Max(q => q.Y), 1.78 * 0.98, 1.78 * 1.02);
        // A car turned 30°: seen from the south its side, as long as the car; from the east its front, as wide.
        var car = ids.Single(i => i.What == "car").Id;
        double Wide(ViewDrawing d) { var xs = Of(d, car).SelectMany(l => new[] { l.A.X, l.B.X }).ToList(); return xs.Max() - xs.Min(); }
        Assert.InRange(Wide(south), 4.6 * 0.95, 4.6 * 1.05);
        Assert.InRange(Wide(east), 1.8 * 0.95, 1.8 * 1.05);
        // The 3D bodies are as they were.
        Assert.Contains(f.Session.Reader.Geometry(car)!.Parts, q => q.Name == "tyres");
    }

    [Fact]
    public void The_view_style_draws_them_abstract_detailed_or_not_at_all_and_an_old_style_draws_outlines()
    {
        var f = Project();
        var man = Person(f, "man", 0);
        var car = Person(f, "car", 6);
        var walnut = Plant(f, "Walnut", 16);
        var plan = Plan(f);
        var elevation = Elevation(f);
        int Count(Entity v, string id) => Of(Draw(f, v), id).Count;
        var outline = (Count(plan, man), Count(plan, walnut), Count(elevation, man), Count(elevation, car), Count(elevation, walnut));

        // Abstract: a person a circle in plan, a head over a body in an elevation; a tree a circle and its trunk.
        Style(f, plan, new JsonObject { ["entourage"] = "abstract" });
        Style(f, elevation, new JsonObject { ["entourage"] = "abstract" });
        Assert.Equal(12, Count(plan, man));
        Assert.True(Count(plan, walnut) <= outline.Item2);
        Assert.Equal(16, Count(elevation, man));
        Assert.Equal(2, Loops(Of(Draw(f, elevation), man)));

        // Detailed: the symbols and bodies drawn before decision 341, many more lines.
        Style(f, plan, new JsonObject { ["entourage"] = "detailed" });
        Style(f, elevation, new JsonObject { ["entourage"] = "detailed" });
        Assert.True(Count(plan, walnut) > 50);
        Assert.True(Count(elevation, man) > 3 * outline.Item3);
        Assert.True(Count(elevation, car) > outline.Item4);

        // None: nothing in 2D, the bodies still in 3D.
        Style(f, plan, new JsonObject { ["entourage"] = "none" });
        Style(f, elevation, new JsonObject { ["entourage"] = "none" });
        foreach (var id in new[] { man, car, walnut })
        {
            Assert.Empty(Of(Draw(f, plan), id));
            Assert.Empty(Of(Draw(f, elevation), id));
            Assert.NotEmpty(f.Session.Reader.Geometry(id)!.Parts);
        }
        Assert.Throws<CommandException>(() => Style(f, plan, new JsonObject { ["entourage"] = "photos" }));

        // Back to the style's: a style saved before decision 341 (no field) draws outlines.
        Style(f, plan, new JsonObject { ["entourage"] = null });
        Assert.Equal("outline", ViewStyles.Style.From(new JsonObject { ["id"] = "old", ["name"] = "Old" }).Entourage);
        Assert.Equal(outline.Item1, Count(plan, man));
        // On the style itself: every view taking it.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["entourage"] = "abstract" });
        Assert.Equal(12, Count(plan, man));
    }

    [Fact]
    public void A_silhouette_takes_the_style_s_fill_and_a_thin_pen()
    {
        var f = Project();
        var walnut = Plant(f, "Walnut", 0);
        var elevation = Elevation(f);
        Assert.DoesNotContain(Draw(f, elevation).Fills ?? [], x => x.Element == walnut);
        Style(f, elevation, new JsonObject { ["entourageFill"] = "#eeeeee" });
        Assert.Contains(Draw(f, elevation).Fills ?? [], x => x.Element == walnut && x.Color == "#eeeeee");
        Assert.Contains(Views.Render(f.Session.Reader, Views.Find(f.Model, elevation.Id)!).Pens, p => p.Fill == "#eeeeee");
        Assert.Throws<CommandException>(() => Style(f, elevation, new JsonObject { ["entourageFill"] = "pale" }));
        // The view says none over a style's colour.
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["entourageFill"] = "#dddddd" });
        Style(f, elevation, new JsonObject { ["entourageFill"] = "none" });
        Assert.DoesNotContain(Draw(f, elevation).Fills ?? [], x => x.Element == walnut);

        // Pen 2 (0.13 mm at 1:100), unless the object styles give the category another.
        var thin = LineStyles.PenWidth(LineStyles.DefaultPenTable(), Views.EntouragePen, 100);
        var seen = Views.Render(f.Session.Reader, Views.Find(f.Model, elevation.Id)!).Pens.Where(p => p.Layer == PenLayer.Seen).ToList();
        Assert.Contains(seen, p => Math.Abs(p.Width - thin) < 1e-9 && p.Segments.Count > 10);
        Assert.Equal("entourage", LineStyles.CategoryOf(Entourage.Class));
        Assert.Equal("entourage", LineStyles.CategoryOf(Plantings.Class));
        f.Run("view.objectStyle", new JsonObject { ["category"] = "entourage", ["role"] = "projection", ["spec"] = new JsonObject { ["pen"] = 6 } });
        var heavy = LineStyles.PenWidth(LineStyles.DefaultPenTable(), 6, 100);
        Assert.Contains(Views.Render(f.Session.Reader, Views.Find(f.Model, elevation.Id)!).Pens, p => p.Layer == PenLayer.Seen && Math.Abs(p.Width - heavy) < 1e-9);
    }

    [Fact]
    public void A_section_draws_what_lies_beyond_its_cut_and_a_sheet_and_its_dxf_print_them()
    {
        var f = Project();
        var beyond = Person(f, "woman", 0, 5);
        var behind = Person(f, "man", 3, -5);
        var section = f.Model.Get((string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-5, 0), ["end"] = new JsonArray(10, 0) })!["id"]!)!;
        var d = Draw(f, section);
        Assert.Equal(1, Loops(Of(d, beyond)));
        Assert.Empty(Of(d, behind));

        var elevation = Elevation(f);
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Elevations", ["number"] = "A-201", ["paper"] = "A3" })!["id"]!;
        f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = elevation.Id, ["x"] = 200, ["y"] = 150 });
        int Thin(PaperPage page) => page.Pens.Where(p => p.Layer == PenLayer.Seen).Sum(p => p.Segments.Count);
        var page = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!);
        var outline = Thin(page);
        Assert.InRange(outline, 2 * 20, 2 * 48 + 40);
        var dxf = System.Text.Encoding.Latin1.GetString(DxfWriter.Page(page, "test"));
        Assert.Contains("SEEN", dxf);
        Style(f, elevation, new JsonObject { ["entourage"] = "detailed" });
        var detailed = Sheets.Compose(f.Session.Reader, f.Model.Get(sheet)!);
        Assert.True(Thin(detailed) > 3 * outline);
        Assert.True(DxfWriter.Page(detailed, "test").Length > 2 * dxf.Length);
    }

    [Fact]
    public void A_site_of_many_trees_draws_quickly_with_a_few_segments_each()
    {
        var f = Project();
        var zone = (string)f.Run("civil.addPlanting", new JsonObject
        {
            ["storeyId"] = Ground(f), ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(100, 0), new JsonArray(100, 60), new JsonArray(0, 60)), ["density"] = 12, ["seed"] = 3,
            ["species"] = new JsonArray(new JsonObject { ["name"] = "Lime (Tilia)" }, new JsonObject { ["name"] = "Silver fir" }, new JsonObject { ["name"] = "Hazel" }),
        })!["id"]!;
        var trees = Plantings.Plants(f.Model, f.Model.Get(zone)!).Count;
        Assert.True(trees > 300, $"{trees} plants");
        var site = f.Model.OfClass(Views.Class).Single(v => SitePlans.Of(v) == SitePlans.Situation);
        var elevation = Elevation(f);
        Draw(f, site);
        var clock = Stopwatch.StartNew();
        var plan = Draw(f, site);
        var e = Draw(f, elevation);
        clock.Stop();
        // The zone's contour (4) and each plant's outline and trunk cross.
        Assert.InRange(Of(plan, zone).Count, trees * 10, trees * 28 + 4);
        Assert.InRange(Of(e, zone).Count, trees * 10, trees * 24);
        Assert.True(clock.Elapsed.TotalSeconds < 10, $"{clock.Elapsed.TotalSeconds:0.0} s");
    }
}
