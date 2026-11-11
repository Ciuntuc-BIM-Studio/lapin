using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Furniture;
using Lapin.Furniture.Models;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Furniture from CC0 models stretched by zones (docs/plan-furniture.md, Configuratorul flexibil, stage 2; decision
/// 477): the register (every model shipped, CC0, its hash), the stretch (fixed ends exact, the band scaled), the sizes
/// kept between a model's least and most, the slots mapped to roles and project materials, ids kept on edits, the plan
/// from above with a sofa's seats and hung lamps overhead, the front view, IFC there and back, the schedule, and the
/// files kept in their own assembly (a lazy part of the browser's engine).
/// </summary>
public sealed class FurnitureModelTests
{
    static Fixture F() => new(new ArchitectureModule(), new FurnitureModule());

    static string Type(Fixture f, string model, JsonObject? spec = null)
    {
        var s = spec?.DeepClone() as JsonObject ?? [];
        s["model"] = model;
        return (string)f.Run("furniture.createType", new JsonObject { ["kind"] = "model", ["spec"] = s })!["id"]!;
    }

    static string Place(Fixture f, string type, double x = 0) =>
        (string)f.Run("furniture.place", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x"] = x, ["y"] = 0, ["against"] = false })!["id"]!;

    static Spec SpecOf(Fixture f, string type) => new(f.Model.Get(type)!.Attrs["spec"] as JsonObject);

    static (Vec3 Min, Vec3 Max) Bounds(ElementGeometry g)
    {
        var all = g.Parts.Select(p => Measure.Bounds(p.Solid)!.Value).ToList();
        return (new Vec3(all.Min(b => b.Min.X), all.Min(b => b.Min.Y), all.Min(b => b.Min.Z)), new Vec3(all.Max(b => b.Max.X), all.Max(b => b.Max.Y), all.Max(b => b.Max.Z)));
    }

    static string RepoFile(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "engine", "Lapin.slnx"))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }

    [Fact]
    public void The_register_lists_every_model_shipped_each_CC0_with_its_hash()
    {
        var reg = JsonNode.Parse(File.ReadAllText(RepoFile("engine/src/Modules/Interior/Lapin.Furniture.Models/furniture-assets.json")))!;
        var models = reg["models"]!.AsArray();
        Assert.InRange(models.Count, 20, 40);
        var resources = typeof(FurnitureModelFiles).Assembly.GetManifestResourceNames().ToHashSet();
        foreach (var m in models)
        {
            var id = (string)m!["id"]!;
            Assert.Equal("CC0-1.0", (string?)m["licence"]);
            var source = reg["sources"]![(string)m["source"]!]!;
            Assert.Equal("CC0-1.0", (string?)source["licence"]);
            Assert.False(string.IsNullOrWhiteSpace((string?)source["verified"]));
            var bytes = FurnitureModelFiles.Read(id);
            Assert.NotNull(bytes);
            Assert.Equal((string)m["sha256"]!, Convert.ToHexStringLower(SHA256.HashData(bytes!)));
            var thumb = RepoFile((string)m["thumbnail"]!["file"]!);
            Assert.Equal((string)m["thumbnail"]!["sha256"]!, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(thumb))));
            Assert.Contains($"models/{id}.glb", resources);
        }
        // Nothing shipped that the register does not list.
        Assert.Equal(models.Count, resources.Count);
        Assert.Equal(models.Count, FurnitureModels.All.Count);
        Assert.Null(FurnitureModelFiles.Read("../furniture-assets"));
    }

    [Fact]
    public void Stretch_keeps_what_lies_before_and_after_the_zone_and_scales_the_band()
    {
        // 2 m at its own proportions, its zone from 10 % to 90 %, stretched to 3 m: the ends 200 mm each stay.
        (double, double)? z = (0.1, 0.9);
        Assert.Equal(0.05, FurnitureModels.Stretch(0.05, 2, 3, z), 12);
        Assert.Equal(0.2, FurnitureModels.Stretch(0.2, 2, 3, z), 12);
        Assert.Equal(2.8, FurnitureModels.Stretch(1.8, 2, 3, z), 12);
        Assert.Equal(2.95, FurnitureModels.Stretch(1.95, 2, 3, z), 12);
        Assert.Equal(3.0, FurnitureModels.Stretch(2.0, 2, 3, z), 12);
        Assert.Equal(1.5, FurnitureModels.Stretch(1.0, 2, 3, z), 12); // the middle stays the middle
        // Shrunk to 1 m: the ends still 200 mm, the band 600 mm.
        Assert.Equal(0.8, FurnitureModels.Stretch(1.8, 2, 1, z), 12);
        // No zone: the axis scales whole.
        Assert.Equal(0.75, FurnitureModels.Stretch(0.5, 2, 3, null), 12);
    }

    [Fact]
    public void A_sofa_made_wider_keeps_its_arms_and_legs_and_stretches_its_seat()
    {
        var m = FurnitureModels.Find("loungeSofa")!;
        var (w, d, h) = (m.Natural[0], m.Size[1], m.Size[2]);
        var a = FurnitureModels.Stretched(m, w, d, h)!;
        var b = FurnitureModels.Stretched(m, 3.0, d, h)!;
        var (left, right) = (m.Zones[0]!.Value.From * m.Natural[0], (1 - m.Zones[0]!.Value.To) * m.Natural[0]);
        var moved = 0;
        foreach (var ((ra, va, _), (rb, vb, _)) in a.Parts.Zip(b.Parts))
        {
            Assert.Equal(ra, rb);
            for (var i = 0; i < va.Length; i++)
            {
                Assert.Equal(va[i].Y, vb[i].Y, 12);
                Assert.Equal(va[i].Z, vb[i].Z, 12);
                if (va[i].X <= -w / 2 + left + 1e-9) Assert.Equal(va[i].X + w / 2, vb[i].X + 1.5, 9);      // the left arm, as it was from the left end
                else if (va[i].X >= w / 2 - right - 1e-9) { Assert.Equal(w / 2 - va[i].X, 1.5 - vb[i].X, 9); moved++; } // the right arm from the right end
                else Assert.Fail("No corner lies in the sofa's stretch zone.");
            }
        }
        Assert.True(moved > 0);
        // Its legs keep their size: each leg's own width the same at both widths.
        var legsA = a.Parts.Single(p => p.Role == "legs").Vertices;
        var legsB = b.Parts.Single(p => p.Role == "legs").Vertices;
        Assert.Equal(legsA.Where(v => v.X < 0).Max(v => v.X) - legsA.Min(v => v.X), legsB.Where(v => v.X < 0).Max(v => v.X) - legsB.Min(v => v.X), 9);
        // The body is as wide as asked.
        var all = b.Parts.SelectMany(p => p.Vertices).ToList();
        Assert.Equal(3.0, all.Max(v => v.X) - all.Min(v => v.X), 9);
        Assert.Equal(h, all.Max(v => v.Z), 9);
    }

    [Fact]
    public void Every_model_builds_at_its_size_with_a_plan_and_its_roles()
    {
        var f = F();
        var x = 0.0;
        foreach (var m in FurnitureModels.All)
        {
            var type = Type(f, m.Id);
            var s = SpecOf(f, type);
            Assert.Equal(m.Size[0], s.W, 9);
            var id = Place(f, type, x);
            x += 4;
            var g = FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!;
            Assert.NotEmpty(g.Parts);
            Assert.NotEmpty(g.Plan!);
            var (lo, hi) = Bounds(g);
            Assert.Equal(m.Size[0], hi.X - lo.X, 4);
            Assert.Equal(m.Size[1], hi.Y - lo.Y, 4);
            Assert.Equal(m.Size[2], hi.Z - lo.Z, 4);
            Assert.Equal(m.Mount, lo.Z, 4);
            // Its materials: its model's roles, each a project material.
            Assert.Equal(m.Roles.OrderBy(r => r), (s.Kind.Key == "model" ? (f.Model.Get(type)!.Attrs["spec"]!["materials"] as JsonObject)!.Select(p => p.Key) : []).OrderBy(r => r));
            Assert.All(g.Parts, p => Assert.NotNull(p.Material));
        }
    }

    [Fact]
    public void Sizes_stay_between_the_models_least_and_most()
    {
        var f = F();
        var m = FurnitureModels.Find("table")!;
        var type = Type(f, "table", new JsonObject { ["width"] = 20.0, ["height"] = 0.01 });
        var s = SpecOf(f, type);
        Assert.Equal(m.Max[0], s.W, 9);
        Assert.Equal(m.Min[2], s.H, 9);
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["depth"] = 0.0001 } });
        Assert.Equal(m.Min[1], SpecOf(f, type).D, 9);
        // A rug may be a centimetre thick (thinner than the other kinds' 100 mm).
        var rug = Type(f, "rugRectangle");
        Assert.Equal(0.01, SpecOf(f, rug).H, 9);
        // The table's top keeps its thickness when it is made taller: only its legs grow.
        var low = FurnitureModels.Stretched(m, 1.6, 0.9, 0.7)!;
        var high = FurnitureModels.Stretched(m, 1.6, 0.9, 0.9)!;
        double Top(FurnitureModels.Body b) { var z = b.Parts.SelectMany(p => p.Vertices).Select(v => v.Z).Distinct().OrderDescending().ToList(); return z[0] - z[1]; }
        Assert.Equal(Top(low), Top(high), 9);
    }

    [Fact]
    public void A_new_model_brings_its_size_and_its_roles_and_retypes_the_pieces()
    {
        var f = F();
        var type = Type(f, "loungeSofa", new JsonObject { ["materials"] = new JsonObject { ["upholstery"] = "Velvet sage" } });
        var id = Place(f, type);
        var e = f.Model.Get(id)!;
        Assert.Equal("SOFA", e.PredefinedType);
        var g = FurnitureModule.Geometry(e, f.Model)!;
        Assert.Equal(["Upholstery", "Legs"], g.Parts.Select(p => p.Name).OrderByDescending(n => n == "Upholstery").ToArray());
        Assert.Equal(f.Model.OfClass("IfcMaterial").Single(x => x.Name == "Velvet sage").Id, g.Parts.Single(p => p.Name == "Upholstery").Material);
        Assert.Equal("#8fa58a", g.Parts.Single(p => p.Name == "Upholstery").Color);

        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["model"] = "lampRoundFloor" } });
        var s = SpecOf(f, type);
        var lamp = FurnitureModels.Find("lampRoundFloor")!;
        Assert.Equal(lamp.Size[2], s.H, 9);
        Assert.Contains(f.Model.OfClass("IfcMaterial"), x => x.Name == "Lamp shade linen");
        e = f.Model.Get(id)!;
        Assert.Equal("USERDEFINED", e.PredefinedType);
        Assert.Equal("Floor lamp", e.Str("objectType"));
        Assert.Equal("USERDEFINED", f.Model.Get(type)!.PredefinedType);
        Assert.Contains(FurnitureModule.Geometry(e, f.Model)!.Parts, p => p.Name == "Shade");

        Assert.Throws<CommandException>(() => f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["model"] = "nope" } }));
    }

    [Fact]
    public void A_piece_keeps_its_id_when_its_type_changes_and_one_change_is_one_undo_step()
    {
        var f = F();
        var type = Type(f, "loungeSofa");
        var id = Place(f, type);
        f.Run("furniture.updateType", new JsonObject { ["id"] = type, ["spec"] = new JsonObject { ["width"] = 2.6, ["materials"] = new JsonObject { ["legs"] = "Brass" } } });
        Assert.NotNull(f.Model.Get(id));
        Assert.Equal(2.6, Bounds(FurnitureModule.Geometry(f.Model.Get(id)!, f.Model)!) is var (lo, hi) ? hi.X - lo.X : 0, 4);
        f.Session.Undo();
        Assert.Equal(2.0, SpecOf(f, type).W, 9);
        Assert.Equal("Black powder-coated steel", SpecOf(f, type).MaterialName("legs"));
        Assert.NotNull(f.Model.Get(id));
    }

    [Fact]
    public void Its_plan_is_its_body_seen_from_above_with_a_sofas_seats_and_a_pendant_overhead()
    {
        var f = F();
        var sofa = FurnitureModule.Geometry(f.Model.Get(Place(f, Type(f, "loungeSofa", new JsonObject { ["width"] = 2.6 })))!, f.Model)!;
        Assert.All(sofa.Plan!, p => Assert.Contains(p.Kind, new[] { "symbol" }));
        // 2.6 m: the seat band shared in three seats, two lines at 1:50 (not at 1:1000).
        Assert.Equal(2, sofa.Plan!.Count(p => p.MinDetail == PlanDetail.Medium));
        var xs = sofa.Plan!.SelectMany(p => p.Points).Select(p => p.X).ToList();
        Assert.Equal(2.6, xs.Max() - xs.Min(), 3);
        var pendant = FurnitureModule.Geometry(f.Model.Get(Place(f, Type(f, "lampSquareCeiling"), 5))!, f.Model)!;
        Assert.All(pendant.Plan!, p => Assert.Equal("overhead", p.Kind));
        var rug = FurnitureModule.Geometry(f.Model.Get(Place(f, Type(f, "rugRound"), 9))!, f.Model)!;
        Assert.Contains(rug.Plan!, p => p.Points.Count > 8);
    }

    [Fact]
    public void The_preview_shows_the_model_from_the_front_in_its_colours()
    {
        var f = F();
        var r = f.Session.Query("furniture.preview", new JsonObject { ["kind"] = "model", ["spec"] = new JsonObject { ["model"] = "loungeChair", ["width"] = 1.1 } })!;
        Assert.Equal(1.1, (double)r["width"]!, 6);
        var tris = r["elevation"]!.AsArray();
        Assert.NotEmpty(tris);
        Assert.All(tris, t => Assert.Equal(6, t!["points"]!.AsArray().Count));
        Assert.Equal(["upholstery", "legs"], r["roles"]!.AsArray().Select(x => (string)x!).OrderByDescending(x => x == "upholstery"));
        Assert.NotEmpty(r["plan"]!.AsArray());
    }

    [Fact]
    public void Models_go_through_IFC_as_furniture_of_their_type_and_come_back()
    {
        var f = F();
        Place(f, Type(f, "loungeSofa", new JsonObject { ["width"] = 2.4 }));
        Place(f, Type(f, "lampRoundFloor"), 4);
        Place(f, Type(f, "tableRound"), 8);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "x.ifc", new IfcExportOptions());
        var ifc = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("IFCFURNITURETYPE", ifc);
        Assert.Contains(".SOFA.", ifc);
        Assert.Contains("'Floor lamp'", ifc);
        Assert.Contains("IFCFACETEDBREP", ifc);
        using var ms2 = new MemoryStream();
        IfcExport.Write(f.Session, ms2, "x.ifc", new IfcExportOptions(Geometry: "faceset"));
        var faceset = Encoding.UTF8.GetString(ms2.ToArray());
        Assert.True(faceset.Contains("IFCTRIANGULATEDFACESET") || faceset.Contains("IFCPOLYGONALFACESET"));
        var path = Path.Combine(Path.GetTempPath(), $"models-{Guid.NewGuid():N}.ifc");
        File.WriteAllText(path, ifc);
        try
        {
            var g = F();
            IfcImport.Run(g.Session, path, null, out _);
            var pieces = g.Model.OfClass(FurnitureModule.Class).ToList();
            Assert.Equal(3, pieces.Count);
            var specs = pieces.Select(p => new Spec(FurnitureModule.TypeOf(g.Model, p)!.Attrs["spec"] as JsonObject)).ToList();
            Assert.Contains(specs, s => s.Str("model") == "loungeSofa" && Math.Abs(s.W - 2.4) < 1e-9);
            Assert.Contains(specs, s => s.Str("model") == "tableRound");
            Assert.All(pieces, p => Assert.NotEmpty(FurnitureModule.Geometry(p, g.Model)!.Parts));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_furniture_schedule_lists_them_by_model()
    {
        var f = F();
        var type = Type(f, "chair");
        Place(f, type);
        Place(f, type, 1);
        var grid = new FurnitureSchedule().Grid(f.Session.Reader);
        var row = grid.Rows.Single(r => r[0].StartsWith("Wooden chair", StringComparison.Ordinal));
        Assert.Equal("Chairs · Wooden chair", row[1]);
        Assert.Equal("2", row[3]);
    }

    [Fact]
    public void The_model_files_stay_in_their_own_assembly_a_lazy_part_of_the_browser_engine()
    {
        // The module's own assembly holds the register only; the meshes are in Lapin.Furniture.Models.
        Assert.Equal(["furniture-assets.json"], typeof(FurnitureModels).Assembly.GetManifestResourceNames());
        Assert.All(typeof(FurnitureModelFiles).Assembly.GetManifestResourceNames(), n => Assert.EndsWith(".glb", n));
        // The browser's build leaves it (and Lapin.Mesh, which reads it) out of the first load.
        var web = File.ReadAllText(RepoFile("scripts/build-web.mjs"));
        var lazy = web[web.IndexOf("const LAZY = [", StringComparison.Ordinal)..];
        lazy = lazy[..lazy.IndexOf("];", StringComparison.Ordinal)];
        Assert.Contains("'Lapin.Furniture.Models'", lazy);
        Assert.Contains("'Lapin.Mesh'", lazy);
    }
}
