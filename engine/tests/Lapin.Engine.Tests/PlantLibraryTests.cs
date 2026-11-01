using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// The planting library (decision 234, docs/spec-site.md): Romanian fruit
/// trees, conifers and shrubs with their Latin and Romanian names, each drawn
/// in plan by its family's symbol (made by Lapin from a seed, as wide as the
/// crown) and in 3D by its family's shape; old projects' species still resolve.
/// </summary>
public sealed class PlantLibraryTests
{
    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new CivilModule());
        f.Session.New();
        return f;
    }

    static string Storey(Fixture f, string name) => Spatial.Storeys(f.Model).Single(s => s.Name == name).Id;

    static readonly string[] Drawn = ["fruit", "broadleaf", "fir", "pine", "shrub", "hedge"];

    static int Segments(IEnumerable<SymbolStroke> s) => s.Sum(x => x.Fill ? 0 : x.Points.Count - (x.Closed ? 0 : 1));

    [Fact]
    public void The_library_has_the_Romanian_fruit_trees_conifers_and_shrubs_with_their_names()
    {
        var c = Plantings.Catalog;
        foreach (var (latin, ro, family, type) in new[]
        {
            ("Malus domestica", "Măr", "fruit", "tree"), ("Pyrus communis", "Păr", "fruit", "tree"), ("Prunus domestica", "Prun", "fruit", "tree"),
            ("Prunus avium", "Cireș", "fruit", "tree"), ("Prunus cerasus", "Vișin", "fruit", "tree"), ("Prunus armeniaca", "Cais", "fruit", "tree"),
            ("Juglans regia", "Nuc", "broadleaf", "tree"), ("Cydonia oblonga", "Gutui", "fruit", "tree"),
            ("Abies alba", "Brad", "fir", "tree"), ("Picea abies", "Molid", "fir", "tree"), ("Pinus sylvestris", "Pin silvestru", "pine", "tree"), ("Pinus nigra", "Pin negru", "pine", "tree"),
            ("Corylus avellana", "Alun", "shrub", "bush"), ("Cornus mas", "Corn", "shrub", "bush"), ("Sambucus nigra", "Soc", "shrub", "bush"), ("Rosa canina", "Măceș", "shrub", "bush"),
            ("Syringa vulgaris", "Liliac", "shrub", "bush"), ("Hippophae rhamnoides", "Cătină", "shrub", "bush"), ("Ribes rubrum", "Coacăz", "shrub", "bush"),
        })
        {
            var s = Assert.Single(c, x => x.Latin == latin);
            Assert.Equal((ro, family, type), (s.Ro, s.DrawnAs, s.Type));
            Assert.True(s.Height > 0 && s.Crown > 0);
        }
        var walnut = c.Single(x => x.Latin == "Juglans regia");
        Assert.Equal((15.0, 12.0), (walnut.Height, walnut.Crown));
        // Lilac is the old entry, now named.
        Assert.Equal("Lilac (Syringa)", c.Single(x => x.Latin == "Syringa vulgaris").Name);
        Assert.Equal(c.Count, c.Select(x => x.Name).Distinct().Count());
        // The interface's catalogue carries the names and the family.
        var json = Plantings.SpeciesJson(c);
        var apple = json.OfType<JsonObject>().Single(o => (string?)o["latin"] == "Malus domestica");
        Assert.Equal(("Apple", "Măr", "fruit"), ((string?)apple["name"], (string?)apple["ro"], (string?)apple["family"]));
        Assert.Null(json.OfType<JsonObject>().Single(o => (string?)o["name"] == "Lawn")["latin"]);
    }

    [Fact]
    public void An_old_projects_species_resolve_by_name_and_a_species_unknown_draws_by_its_type()
    {
        // As format 2 and the first plantings wrote them: no Latin, Romanian or family.
        var lime = Plantings.Parse(new JsonObject { ["name"] = "Lime (Tilia)", ["type"] = "tree", ["height"] = 9, ["crown"] = 6, ["color"] = "#4f8a3a", ["share"] = 0.4 })!;
        Assert.Equal(("Tilia cordata", "Tei", "broadleaf", 0.4), (lime.Latin, lime.Ro, lime.DrawnAs, lime.Share));
        var pine = Plantings.Parse(new JsonObject { ["name"] = "Pine (Pinus)", ["type"] = "tree" })!;
        Assert.Equal("pine", pine.DrawnAs);
        // Sizes kept per planting, over the library's.
        var big = Plantings.Parse(new JsonObject { ["name"] = "Walnut", ["crown"] = 16 })!;
        Assert.Equal((16.0, 15.0, "Juglans regia"), (big.Crown, big.Height, big.Latin));
        Assert.Equal("shrub", Plantings.Parse(new JsonObject { ["name"] = "My bush", ["type"] = "bush" })!.DrawnAs);
        Assert.Equal("broadleaf", Plantings.Parse(new JsonObject { ["name"] = "My tree" })!.DrawnAs);
        Assert.Equal("fir", Plantings.Parse(new JsonObject { ["name"] = "My fir", ["family"] = "fir" })!.DrawnAs);
    }

    [Fact]
    public void Each_familys_symbol_is_the_same_from_the_same_seed_as_wide_as_the_crown_and_its_own()
    {
        var signatures = new Dictionary<string, string>();
        foreach (var family in Drawn)
        {
            var a = PlantSymbols.Symbol(family, 3, 42);
            var b = PlantSymbols.Symbol(family, 3, 42);
            Assert.Equal(Key(a), Key(b));
            Assert.NotEqual(Key(a), Key(PlantSymbols.Symbol(family, 3, 43)));
            var far = a.SelectMany(s => s.Points).Max(p => p.Length);
            Assert.InRange(far, 3 * 0.9, 3 * 1.1);
            var n = Segments(a);
            Assert.InRange(n, 12, 150);
            // A small-scale plan keeps the outline: every symbol has one that is not a detail.
            Assert.Contains(a, s => s.Closed && !s.Fill && !s.Detail);
            signatures[family] = $"{a.Count}:{n}:{a.Count(s => s.Fill)}:{a.Count(s => !s.Closed)}";
        }
        Assert.Equal(Drawn.Length, signatures.Values.Distinct().Count());
        // A conifer's star is spiky, a fruit tree's crown is soft: the fir's outline swings far deeper in.
        static double Depth(List<SymbolStroke> s) { var o = s.First(x => x.Closed && !x.Fill && !x.Detail).Points; return o.Min(p => p.Length) / o.Max(p => p.Length); }
        Assert.True(Depth(PlantSymbols.Symbol("fir", 3, 1)) < 0.75 && Depth(PlantSymbols.Symbol("fruit", 3, 1)) > 0.75);
        Assert.Contains(PlantSymbols.Symbol("broadleaf", 3, 1), s => s.Fill);
        Assert.Empty(PlantSymbols.Symbol("ground", 3, 1));
    }

    static string Key(IEnumerable<SymbolStroke> s) =>
        string.Join("|", s.Select(x => string.Join(";", x.Points.Select(p => FormattableString.Invariant($"{p.X:0.######},{p.Y:0.######}")))));

    [Fact]
    public void A_walnut_and_a_fir_draw_their_symbols_in_a_plan_and_in_the_site_plan()
    {
        var f = Project();
        var ground = Storey(f, "Ground floor");
        var walnut = (string)f.Run("civil.addPlanting", new JsonObject { ["storeyId"] = ground, ["x"] = 0, ["y"] = 0, ["species"] = new JsonArray(new JsonObject { ["name"] = "Walnut" }) })!["id"]!;
        var fir = (string)f.Run("civil.addPlanting", new JsonObject { ["storeyId"] = ground, ["x"] = 15, ["y"] = 0, ["species"] = new JsonArray(new JsonObject { ["name"] = "Silver fir" }) })!["id"]!;
        Assert.Equal("Juglans regia", Plantings.SpeciesOf(f.Model.Get(walnut)!)[0].Latin);
        var plan = Views.Find(f.Model, "plan:" + ground)!;
        var site = f.Model.OfClass(Views.Class).Single(v => SitePlans.Of(v) == SitePlans.Situation);
        foreach (var view in new[] { plan, site })
        {
            var (d, _, problem) = Views.Generate(f.Session.Reader, view);
            Assert.True(d != null, problem);
            var w = d!.Lines.Where(l => l.Element == walnut).ToList();
            var t = d.Lines.Where(l => l.Element == fir).ToList();
            Assert.InRange(w.Count, 60, 150);
            Assert.InRange(t.Count, 60, 150);
            // As wide as the crown: the walnut 12 m, the fir 7 m.
            Assert.InRange(w.SelectMany(l => new[] { l.A, l.B }).Max(p => p.Length), 6 * 0.9, 6 * 1.1);
            Assert.InRange(t.SelectMany(l => new[] { l.A, l.B }).Max(p => (p - new Vec2(15, 0)).Length), 3.5 * 0.9, 3.5 * 1.1);
            // The walnut's dark core, filled black.
            Assert.Contains(d.Fills ?? [], x => x.Element == walnut && x.Color == "#000000");
            var pens = Views.Render(f.Session.Reader, view).Pens;
            Assert.Contains(pens, p => p.Fill == "#000000" && p.Loops.Count > 0);
            Assert.True(pens.Sum(p => p.Segments.Count) >= w.Count + t.Count);
        }
        // A location plan (1:2000) keeps the crowns' outlines alone.
        var location = f.Model.OfClass(Views.Class).Single(v => SitePlans.Of(v) == SitePlans.LocationPlan);
        var (ld, _, _) = Views.Generate(f.Session.Reader, location);
        // The outline's lobes follow the plant's seed (its id's here, new each run): fewer lines than the site plan's, whatever they are.
        var full = Views.Generate(f.Session.Reader, site).Drawing!.Lines.Count(l => l.Element == walnut);
        Assert.InRange(ld!.Lines.Count(l => l.Element == walnut), 30, full - 10);
        Assert.DoesNotContain(ld.Fills ?? [], x => x.Element == walnut);
        WriteSample();
    }

    [Fact]
    public void Each_family_has_its_body_in_3D_with_a_trunk_and_a_crown()
    {
        foreach (var sp in Plantings.Catalog.GroupBy(s => (s.DrawnAs, s.Type)).Select(g => g.First()))
        {
            var body = Plantings.Body(sp, new Vec2(0, 0), 10, sp.Height, sp.Crown / 2);
            Assert.NotEmpty(body);
            var (min, max) = Measure.Bounds(Paths.Combine(body, "planting"))!.Value;
            Assert.InRange(min.Z, 10 - 1e-6, 10 + 1e-6);
            Assert.InRange(max.Z - 10, sp.Height * 0.95, sp.Height * 1.001);
            if (sp.Type != "grass") Assert.InRange(max.X - min.X, sp.Crown * 0.85, sp.Crown * 1.001);
            Assert.All(body, s => Assert.True(Measure.Volume(s) > 0, $"{sp.Name}: a closed body"));
            var faces = body.Sum(s => s.Faces.Length);
            Assert.True(faces <= 160, $"{sp.Name}: {faces} faces");
            Assert.Equal(sp.Type == "tree" ? 2 : 1, body.Count);
        }
        // A fir is a narrow cone: its crown at mid-height is less than half again as wide as at its top quarter.
        var fir = Plantings.Catalog.Single(s => s.Latin == "Abies alba");
        var crown = Plantings.Body(fir, new Vec2(0, 0), 0, fir.Height, fir.Crown / 2)[1];
        double WidthAt(double z) => crown.Vertices.Where(v => Math.Abs(v.Z - z) < fir.Height * 0.1).Select(v => Math.Sqrt(v.X * v.X + v.Y * v.Y)).DefaultIfEmpty(0).Max();
        Assert.True(WidthAt(fir.Height * 0.95) < WidthAt(fir.Height * 0.2));
    }

    /// <summary>
    /// The picture to look at: one of each species in a row by group, as the 1:100 plan draws it, written as an SVG
    /// when LAPIN_PLANT_SVG names a file (the decision's visual check).
    /// </summary>
    static void WriteSample()
    {
        if (Environment.GetEnvironmentVariable("LAPIN_PLANT_SVG") is not { Length: > 0 } path) return;
        var f = Project();
        var ground = Storey(f, "Ground floor");
        double x = 0, y = 0, row = 0;
        string? group = null;
        foreach (var sp in Plantings.Catalog.Where(s => s.Type != "grass"))
        {
            var g = sp.DrawnAs is "fir" or "pine" ? "c" : sp.Type == "tree" ? "t" : "s";
            if (group != null && g != group) { x = 0; y -= row + 3; row = 0; }
            group = g;
            x += sp.Crown / 2;
            f.Run("civil.addPlanting", new JsonObject { ["storeyId"] = ground, ["x"] = x, ["y"] = y - sp.Crown / 2, ["seed"] = 11 + (int)(x * 7), ["species"] = new JsonArray(new JsonObject { ["name"] = sp.Name }) });
            x += sp.Crown / 2 + 1.5;
            row = Math.Max(row, sp.Crown);
        }
        var pens = Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + ground)!).Pens;
        var pts = pens.SelectMany(p => p.Segments.SelectMany(s => new[] { s.A, s.B }).Concat(p.Loops.SelectMany(l => l))).ToList();
        double x0 = pts.Min(p => p.X) - 1, x1 = pts.Max(p => p.X) + 1, y0 = pts.Min(p => p.Y) - 1, y1 = pts.Max(p => p.Y) + 1;
        const double px = 40;
        string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        string P(Vec2 p) => $"{N((p.X - x0) * px)},{N((y1 - p.Y) * px)}";
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='{N((x1 - x0) * px)}' height='{N((y1 - y0) * px)}'><rect width='100%' height='100%' fill='white'/>");
        foreach (var p in pens)
        {
            foreach (var l in p.Loops) svg.Append($"<polygon points='{string.Join(" ", l.Select(P))}' fill='{p.Fill ?? "none"}'/>");
            if (p.Segments.Count > 0) svg.Append($"<path d='{string.Join("", p.Segments.Select(s => $"M{P(s.A)}L{P(s.B)}"))}' stroke='{p.Stroke ?? "#000"}' stroke-width='{N(Math.Max(0.6, p.Width * px / 10))}' fill='none' stroke-linecap='round'/>");
        }
        File.WriteAllText(path, svg.Append("</svg>").ToString());
    }
}
