using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Civil;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The vegetation's maquette style (decision 818): a view draws the plants coloured (as before, the default) or as an
/// architectural model's, neutral faceted blobs; chosen per view through its style, so the 3D, the mesh exports and the
/// drawings agree.
/// </summary>
public sealed class PlantingMaquetteTests(ITestOutputHelper output)
{
    static Fixture Site() => new(new CivilModule());

    static JsonArray Square(double x0, double y0, double side) =>
        new(new JsonArray(x0, y0), new JsonArray(x0 + side, y0), new JsonArray(x0 + side, y0 + side), new JsonArray(x0, y0 + side));

    /// <summary>One plant of every species of the library, side by side, and a zone of the default mix.</summary>
    static List<string> Garden(Fixture f)
    {
        var ids = new List<string>();
        for (var i = 0; i < Plantings.Catalog.Count; i++)
            ids.Add((string)f.Run("civil.addPlanting", new JsonObject { ["x"] = i * 20, ["y"] = 0, ["seed"] = 11 + i, ["species"] = Plantings.SpeciesJson([Plantings.Catalog[i]]) })!["id"]!);
        ids.Add((string)f.Run("civil.addPlanting", new JsonObject { ["points"] = Square(0, 40, 30), ["density"] = 4, ["seed"] = 7 })!["id"]!);
        return ids;
    }

    /// <summary>A geometry's parts as bytes: name, colour, key, every vertex (to 1e-9 m) and every face's loops and normal.</summary>
    static string Hash(IEnumerable<ElementGeometry?> gs)
    {
        var sb = new StringBuilder();
        string N(double v) => Math.Round(v, 9).ToString("R", CultureInfo.InvariantCulture);
        foreach (var g in gs)
        {
            sb.Append('#');
            foreach (var p in g?.Parts ?? [])
            {
                sb.Append(p.Name).Append('|').Append(p.Color).Append('|').Append(p.Key).Append('|').Append(p.Solid.Tag).Append('\n');
                foreach (var v in p.Solid.Vertices) sb.Append(N(v.X)).Append(',').Append(N(v.Y)).Append(',').Append(N(v.Z)).Append(';');
                foreach (var fc in p.Solid.Faces)
                {
                    sb.Append('[').Append(string.Join(",", fc.Outer)).Append(']');
                    foreach (var h in fc.Holes) sb.Append('(').Append(string.Join(",", h)).Append(')');
                    sb.Append(N(fc.Normal.X)).Append(',').Append(N(fc.Normal.Y)).Append(',').Append(N(fc.Normal.Z)).Append(fc.Tag);
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>The coloured plants as they were before the maquette style: taken on the commit before it (origin/main 3d62908aa).</summary>
    const string ColouredGolden = "BDED266653821CAD4F4E535352D9C086CA3BD80B79959D87CBFB10243E639C89";

    [Fact]
    public void Coloured_plants_are_exactly_what_they_were()
    {
        var f = Site();
        var ids = Garden(f);
        var hash = Hash(ids.Select(f.Session.Geometry));
        output.WriteLine(hash);
        Assert.Equal(ColouredGolden, hash);
    }

    static Fixture Project()
    {
        var f = new Fixture(new ArchitectureModule(), new CivilModule());
        f.Session.New();
        return f;
    }

    /// <summary>One plant of a species, by its name in the library.</summary>
    static string Plant(Fixture f, Species sp, double x, int seed, double y = 0) =>
        (string)f.Run("civil.addPlanting", new JsonObject { ["x"] = x, ["y"] = y, ["seed"] = seed, ["species"] = Plantings.SpeciesJson([sp]) })!["id"]!;

    [Fact]
    public void Coloured_geometry_says_it_has_a_maquette_body_and_the_maquette_is_built_only_when_asked()
    {
        var f = Site();
        var id = Plant(f, Plantings.Catalog[0], 0, 3);
        var g = f.Session.Geometry(id)!;
        Assert.True(g.VegetationShape);
        // The render holds the coloured body alone: one part per species, in its colour.
        Assert.Equal(Plantings.Catalog[0].Color, Assert.Single(g.Parts).Color);
        var m = f.Session.MaquetteGeometry(id)!;
        Assert.NotSame(g, m);
        Assert.Same(m, f.Session.MaquetteGeometry(id));
        Assert.Same(g, f.Session.Geometry(id));
        // The quantities are the coloured geometry's, whatever a view draws.
        Assert.Equal(1, f.Session.Quantities(id).First(q => q.Part == null).Quantities.Single(q => q.Name == "Count").Value, 9);
    }

    /// <summary>Every edge of a part used by two faces, once each way (closed shells, consistently turned), every face planar with its own normal.</summary>
    static void ClosedAndFlat(Solid s)
    {
        var uses = new Dictionary<(int, int), int>();
        foreach (var fc in s.Faces)
        {
            Assert.Empty(fc.Holes);
            for (var k = 0; k < fc.Outer.Length; k++)
            {
                var e = (fc.Outer[k], fc.Outer[(k + 1) % fc.Outer.Length]);
                uses[e] = uses.GetValueOrDefault(e) + 1;
            }
            var p0 = s.Vertices[fc.Outer[0]];
            Assert.Equal(1, fc.Normal.Length, 9);
            foreach (var i in fc.Outer) Assert.InRange(Vec3.Dot(s.Vertices[i] - p0, fc.Normal), -1e-9, 1e-9);
        }
        foreach (var ((a, b), n) in uses)
        {
            Assert.Equal(1, n);
            Assert.Equal(1, uses.GetValueOrDefault((b, a)));
        }
        // Flat shaded: every triangle the tessellator makes carries its face's normal, none averaged with its neighbours'.
        var t = Tessellator.Tessellate(s);
        for (var i = 0; i + 2 < t.Indices.Length; i += 3)
        {
            var (a, b, c) = (t.Indices[i], t.Indices[i + 1], t.Indices[i + 2]);
            Vec3 P(int k) => new(t.Positions[3 * k], t.Positions[3 * k + 1], t.Positions[3 * k + 2]);
            Vec3 N(int k) => new(t.Normals[3 * k], t.Normals[3 * k + 1], t.Normals[3 * k + 2]);
            var geo = Vec3.Cross(P(b) - P(a), P(c) - P(a));
            if (geo.Length < 1e-12) continue;
            Assert.True(Vec3.Dot(geo.Normalized(), N(a)) > 0.999, "a face's own normal");
            Assert.Equal(N(a).X, N(b).X, 6); Assert.Equal(N(a).Y, N(c).Y, 6);
        }
    }

    [Fact]
    public void Every_family_is_a_closed_flat_shaded_body_within_its_face_budget_and_the_species_size()
    {
        var f = Site();
        var kinds = new HashSet<string>();
        for (var i = 0; i < Plantings.Catalog.Count; i++)
        {
            var sp = Plantings.Catalog[i];
            foreach (var seed in new[] { 1, 2, 17, 12345 })
            {
                var id = Plant(f, sp, i * 30, seed, seed * 3);
                var plant = Assert.Single(Plantings.Plants(f.Model, f.Model.Get(id)!));
                var g = f.Session.MaquetteGeometry(id)!;
                var kind = PlantMaquette.Kind(sp);
                kinds.Add(kind);
                var faces = g.Parts.Sum(p => p.Solid.Faces.Length);
                Assert.InRange(faces, 1, PlantMaquette.Budget(kind));
                foreach (var p in g.Parts)
                {
                    ClosedAndFlat(p.Solid);
                    // Neutral: the crowns off-white, the trunks a little darker, never the species' green.
                    Assert.Contains(p.Color, new[] { PlantMaquette.CrownColor, PlantMaquette.TrunkColor });
                }
                // Inside the species' height and crown, as the plant is scaled.
                var (h, r) = (sp.Height * plant.Scale, sp.Crown / 2 * plant.Scale);
                foreach (var v in g.Parts.SelectMany(p => p.Solid.Vertices))
                {
                    Assert.InRange(v.Z, plant.Z - 1e-9, plant.Z + Math.Max(h, 0.02) + 1e-9);
                    Assert.True((new Vec2(v.X, v.Y) - plant.At).Length <= r + 1e-9, $"{sp.Name}: inside its crown");
                }
                // A tree has its trunk apart; a bush, a hedge plant and grass have none.
                Assert.Equal(sp.Type == "tree", g.Parts.Any(p => p.Color == PlantMaquette.TrunkColor));
            }
        }
        Assert.Equal(["broadleaf", "fir", "fruit", "ground", "hedge", "pine", "shrub"], kinds.Order());
    }

    [Fact]
    public void Silhouettes_tell_the_families_apart()
    {
        var f = Site();
        (double Base, double Width, double Height, int Pieces) Shape(Species sp)
        {
            var id = Plant(f, sp, 0, 5);
            var g = f.Session.MaquetteGeometry(id)!;
            var crown = g.Parts.First(p => p.Color == PlantMaquette.CrownColor).Solid;
            var b = Measure.Bounds(crown)!.Value;
            var pieces = crown.Faces.Length;
            return (b.Min.Z / sp.Height, (b.Max.X - b.Min.X) / sp.Crown, (b.Max.Z - b.Min.Z) / sp.Height, pieces);
        }
        Species Of(string name) => Plantings.Catalog.First(s => s.Name == name);
        var broadleaf = Shape(Of("Lime (Tilia)"));
        var fruit = Shape(Of("Apple"));
        var pine = Shape(Of("Scots pine"));
        var fir = Shape(Of("Silver fir"));
        // A pine's crown is high on its bare trunk; a fruit tree's lower than a broadleaf's; a fir's from near the ground.
        Assert.True(pine.Base > 0.55, $"pine {pine.Base}");
        Assert.True(fruit.Base < broadleaf.Base);
        Assert.True(fir.Base < 0.15);
        // The blobs: a broadleaf 4 to 7 icosahedra, a fir 3 or 4 cones of six sides and a base.
        Assert.InRange(broadleaf.Pieces, 4 * 20, 7 * 20);
        Assert.InRange(fir.Pieces, 3 * 7, 4 * 7);
    }

    [Fact]
    public void The_same_seed_grows_the_same_maquette_and_neighbours_differ()
    {
        string Garden1(Fixture f)
        {
            f.Run("civil.addPlanting", new JsonObject { ["points"] = Square(0, 0, 30), ["density"] = 6, ["seed"] = 42 });
            return f.Model.OfClass(Plantings.Class).Single().Id;
        }
        var (a, b) = (Site(), Site());
        var (za, zb) = (Garden1(a), Garden1(b));
        Assert.Equal(Hash([a.Session.MaquetteGeometry(za)]), Hash([b.Session.MaquetteGeometry(zb)]));
        // Two plants of one species side by side, seeds apart: their crowns are not one copied.
        var f = Site();
        var one = f.Session.MaquetteGeometry(Plant(f, Plantings.Catalog[0], 0, 1))!;
        var two = f.Session.MaquetteGeometry(Plant(f, Plantings.Catalog[0], 0, 2, 0))!;
        Assert.NotEqual(Hash([one]), Hash([two]));
        // Another seed on the zone: another wood.
        var c = Site();
        c.Run("civil.addPlanting", new JsonObject { ["points"] = Square(0, 0, 30), ["density"] = 6, ["seed"] = 43 });
        Assert.NotEqual(Hash([a.Session.MaquetteGeometry(za)]), Hash([c.Session.MaquetteGeometry(c.Model.OfClass(Plantings.Class).Single().Id)]));
    }

    [Fact]
    public void The_view_resolver_takes_the_vegetation_from_the_view_over_its_style_and_old_styles_stay_coloured()
    {
        var f = Project();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        VisibilityGraphics Vg(string id) => VisibilityGraphics.Of(f.Model, id == "3d" ? GraphicsSets.As3dView(f.Model) : f.Model.Get(id)!);
        Assert.Equal("coloured", Vg(plan).Vegetation);
        Assert.Equal("coloured", Vg("3d").Vegetation);
        // A style saved without it reads coloured, and a coloured style saves as before (no new field).
        Assert.Equal("coloured", ViewStyles.Style.From(new JsonObject { ["id"] = "x", ["name"] = "Old" }).Vegetation);
        Assert.False(ViewStyles.Defaults[0].ToJson().ContainsKey("vegetation"));
        // The view's own.
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["vegetation"] = "maquette" });
        Assert.True(Vg(plan).Maquette);
        Assert.False(Vg("3d").Maquette);
        f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["vegetation"] = null });
        Assert.False(Vg(plan).Maquette);
        // Its style's: every view of the style, {3D} among them, unless one says otherwise.
        var style = ViewStyles.StyleOf(f.Model, f.Model.Get(plan)!).Id;
        f.Run("viewStyle.update", new JsonObject { ["id"] = style, ["vegetation"] = "maquette" });
        Assert.True(Vg(plan).Maquette);
        Assert.True(Vg("3d").Maquette);
        Assert.Equal("maquette", (string?)ViewStyles.All(f.Model).First(s => s.Id == style).ToJson()["vegetation"]);
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["vegetation"] = "coloured" });
        Assert.False(Vg("3d").Maquette);
        Assert.Throws<CommandException>(() => f.Run("view.setStyle", new JsonObject { ["id"] = plan, ["vegetation"] = "pink" }));
    }

    [Fact]
    public void A_3D_view_in_the_maquette_style_gets_the_plants_maquette_bodies_and_a_coloured_one_nothing()
    {
        var f = Project();
        var tree = Plant(f, Plantings.Catalog[0], 0, 3);
        Assert.Empty(f.Session.FineBodies(null, null).Items);
        Assert.Equal("coloured", f.Session.FineBodies(null, null).Vegetation);
        f.Run("view.setStyle", new JsonObject { ["id"] = "3d", ["vegetation"] = "maquette", ["detail"] = 350 });
        var bodies = f.Session.FineBodies(null, null);
        Assert.Equal("maquette", bodies.Vegetation);
        var item = Assert.Single(bodies.Items);
        Assert.Equal(tree, item.NodeId);
        Assert.Equal([PlantMaquette.CrownColor, PlantMaquette.TrunkColor], item.Parts.Select(p => p.Color));
        Assert.Equal(["species:0", "species:0:trunk"], item.Parts.Select(p => p.Key));
        // Asked again for nothing new: nothing.
        Assert.Empty(f.Session.FineBodies(null, []).Items);
        // A saved 3D view keeps its own: the {3D}'s maquette is not its.
        var saved = (string)f.Run("view.create", new JsonObject { ["kind"] = "3d" })!["id"]!;
        Assert.Empty(f.Session.FineBodies(saved, null).Items);
    }

    static (double R, double G, double B) Rgb(string hex) => (Convert.ToInt32(hex[1..3], 16) / 255.0, Convert.ToInt32(hex[3..5], 16) / 255.0, Convert.ToInt32(hex[5..7], 16) / 255.0);

    [Fact]
    public void A_mesh_export_of_the_view_draws_its_plants_as_the_view_does()
    {
        var f = Project();
        Plant(f, Plantings.Catalog[0], 0, 3);
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "3d" })!["id"]!;
        bool Neutral(MeshScene s) => s.Materials.Any(mm => Math.Abs(mm.R - Rgb(PlantMaquette.CrownColor).R) < 0.01 && Math.Abs(mm.G - Rgb(PlantMaquette.CrownColor).G) < 0.01);
        var coloured = MeshScene.Build(f.Session, new MeshExportOptions(ViewId: view));
        Assert.False(Neutral(coloured));
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["vegetation"] = "maquette" });
        var maquette = MeshScene.Build(f.Session, new MeshExportOptions(ViewId: view));
        Assert.True(Neutral(maquette));
        Assert.NotEqual(coloured.TriangleCount, maquette.TriangleCount);
        // The whole model, no view: the plants as they are, coloured.
        Assert.False(Neutral(MeshScene.Build(f.Session, new MeshExportOptions())));
    }

    [Fact]
    public void An_elevation_drawing_its_entourage_detailed_draws_the_maquette_bodies_edges()
    {
        var f = Project();
        var tree = Plant(f, Plantings.Catalog[0], 0, 3);
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        f.Run("view.setStyle", new JsonObject { ["id"] = elevation, ["entourage"] = "detailed" });
        int Lines() => Views.Generate(f.Session.Reader, Views.Find(f.Model, elevation)!).Drawing!.Lines.Count(l => l.Element == tree);
        var coloured = Lines();
        f.Run("view.setStyle", new JsonObject { ["id"] = elevation, ["vegetation"] = "maquette" });
        var maquette = Lines();
        Assert.True(coloured > 0 && maquette > 0);
        Assert.NotEqual(coloured, maquette);
        // Outlines (the default): the silhouette, whatever the style of the bodies.
        f.Run("view.setStyle", new JsonObject { ["id"] = elevation, ["entourage"] = null });
        var outline = Lines();
        f.Run("view.setStyle", new JsonObject { ["id"] = elevation, ["vegetation"] = null });
        Assert.Equal(Lines(), outline);
    }

    /// <summary>What a big zone costs (reported in docs/plan-site-works.md).</summary>
    [Fact]
    public void A_zone_of_two_thousand_plants_costs_this_much()
    {
        var f = Site();
        var mix = new[] { "Lime (Tilia)", "Apple", "Scots pine", "Silver fir", "Hazel", "Thuja" }.Select(n => Plantings.Catalog.First(s => s.Name == n) with { Share = 1 }).ToList();
        var zone = (string)f.Run("civil.addPlanting", new JsonObject { ["points"] = Square(0, 0, 400), ["density"] = 1.25, ["seed"] = 9, ["species"] = Plantings.SpeciesJson(mix) })!["id"]!;
        var coloured = f.Session.Geometry(zone)!;
        // Each built afresh, outside the session's cache, as alike as can be.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _ = f.Session.Geometry(zone, new GeometryOptions());
        var tc = clock.ElapsedMilliseconds;
        clock.Restart();
        _ = f.Session.Geometry(zone, new GeometryOptions(Maquette: true));
        var tm = clock.ElapsedMilliseconds;
        var maquette = f.Session.MaquetteGeometry(zone)!;
        clock.Restart();
        var tris = maquette.Parts.Sum(p => p.Tessellate().Indices.Length / 3);
        var tt = clock.ElapsedMilliseconds;
        var trisColoured = coloured.Parts.Sum(p => p.Tessellate().Indices.Length / 3);
        var plants = Plantings.Plants(f.Model, f.Model.Get(zone)!);
        Assert.InRange(plants.Count, 1900, 2000);
        var perFamily = string.Join(", ", mix.Select((sp, i) =>
        {
            var n = plants.Count(p => p.Species.Name == sp.Name);
            var faces = maquette.Parts.Where(p => p.Key == $"species:{i}" || p.Key == $"species:{i}:trunk").Sum(p => p.Solid.Faces.Length);
            return $"{PlantMaquette.Kind(sp)} {(n == 0 ? 0 : (double)faces / n):0.#}";
        }));
        output.WriteLine($"{plants.Count} plants: coloured {coloured.Parts.Sum(p => p.Solid.Faces.Length)} faces, {trisColoured} triangles, built in {tc} ms; maquette {maquette.Parts.Sum(p => p.Solid.Faces.Length)} faces, {tris} triangles, built in {tm} ms, tessellated in {tt} ms; faces per plant: {perFamily}");
        Assert.True(tris < 2.5 * trisColoured, "a maquette costs the render about what the coloured plants do");
    }
}
