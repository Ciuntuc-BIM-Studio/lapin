using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Placed symbols in the 3D view (issue #103): a DXF's blocks kept as symbols stand on their view's plane with the rest of
/// the DXF's drawing (the import's 3D switch), where the same file exploded stands; a symbol saved from drafting stands
/// there as the drafting did.
/// </summary>
public sealed class DxfSymbol3dTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static JsonArray J(double x, double y) => new(x, y);

    static string Plan(Fixture f, double elevation)
    {
        var storey = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = elevation })!["id"]!;
        return (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey, ["scale"] = 100 })!["id"]!;
    }

    // A block TABLE (mm, base point (500, 0)): a line on layer 0, a polyline with a bulged segment, a BYBLOCK circle.
    static readonly string Blocks = string.Join("\n",
        "0", "BLOCK", "2", "TABLE", "8", "0", "10", "500", "20", "0",
        "0", "LINE", "8", "0", "10", "500", "20", "0", "11", "1500", "21", "0",
        "0", "LWPOLYLINE", "8", "0", "90", "3", "70", "0", "10", "500", "20", "0", "42", "1", "10", "500", "20", "1000", "10", "1500", "20", "1000",
        "0", "CIRCLE", "8", "0", "62", "0", "10", "1000", "20", "500", "40", "200",
        "0", "ENDBLK");

    // Inserted on layer FURN at (5000, 2000), turned 90°, red.
    const string Insert = "0\nINSERT\n8\nFURN\n2\nTABLE\n62\n1\n10\n5000\n20\n2000\n50\n90";

    static string Dxf() => string.Join("\n", new[]
    {
        "0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1015", "9", "$INSUNITS", "70", "4", "0", "ENDSEC",
        "0", "SECTION", "2", "BLOCKS", Blocks, "0", "ENDSEC",
        "0", "SECTION", "2", "ENTITIES", Insert, "0", "ENDSEC", "0", "EOF",
    });

    static JsonObject Import(Fixture f, string plan, bool symbols)
    {
        var a = new JsonObject { ["viewId"] = plan, ["name"] = "furniture.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Dxf())) };
        if (symbols) a["blocks"] = "symbols";
        return (JsonObject)f.Run("drafting.importDxf", a)!;
    }

    static List<JsonObject> Batches(Fixture f) => ((JsonArray)f.Session.Query("drafting.underlays3d", [])!).OfType<JsonObject>().ToList();

    /// <summary>The batches' segments as pairs of points.</summary>
    static List<(Vec3 A, Vec3 B)> Segments(IEnumerable<JsonObject> batches)
    {
        var o = new List<(Vec3, Vec3)>();
        foreach (var b in batches)
        {
            var bytes = Convert.FromBase64String((string)b["lines"]!);
            var v = new float[bytes.Length / 4];
            for (var i = 0; i < v.Length; i++) v[i] = BitConverter.ToSingle(bytes, i * 4);
            for (var i = 0; i + 5 < v.Length; i += 6) o.Add((new Vec3(v[i], v[i + 1], v[i + 2]), new Vec3(v[i + 3], v[i + 4], v[i + 5])));
        }
        return o;
    }

    static (Vec3 Lo, Vec3 Hi) Box(IEnumerable<Vec3> pts)
    {
        var l = pts.ToList();
        return (new Vec3(l.Min(p => p.X), l.Min(p => p.Y), l.Min(p => p.Z)), new Vec3(l.Max(p => p.X), l.Max(p => p.Y), l.Max(p => p.Z)));
    }

    static bool Near(Vec3 a, Vec3 b, double tol = 1e-4) => (a - b).Length < tol;

    [Fact]
    public void A_dxf_block_kept_as_a_symbol_stands_in_3D_where_the_same_block_exploded_stands()
    {
        var f = F();
        var plan = Plan(f, 3);
        var r = Import(f, plan, symbols: true);
        Assert.Equal(1, (int)r["instances"]!);
        var importId = (string)r["importId"]!;
        var instance = Symbols.Instances(f.Model).Single();
        Assert.True(instance.Bool("dxf"));
        Assert.Equal(importId, instance.Str(Underlays3d.ImportKey));

        // In 3D with the DXF's drawing, on its layer, in the insert's colour, at the plan's level.
        var batch = Assert.Single(Batches(f));
        Assert.Equal("FURN", (string?)batch["layer"]);
        Assert.Equal(importId, (string?)batch["source"]);
        var segs = Segments([batch]);
        Assert.NotEmpty(segs);
        Assert.All(segs, s => { Assert.Equal(3, s.A.Z, 4); Assert.Equal(3, s.B.Z, 4); });
        // The line from the base point, 1 m along the block's +x, turned 90° at (5, 2): from (5, 2) up to (5, 3).
        Assert.Contains(segs, s => Near(s.A, new Vec3(5, 2, 3)) && Near(s.B, new Vec3(5, 3, 3)));
        var colors = Convert.FromBase64String((string)batch["lineColors"]!);
        Assert.Equal(new byte[] { 255, 0, 0 }, colors[..3]);
        // Not drawn twice: the instance has no element geometry of its own.
        Assert.Null(f.Session.Geometry(instance.Id));

        // The same file exploded (the default): the same place and size, about as many segments.
        var g = F();
        var gplan = Plan(g, 3);
        Import(g, gplan, symbols: false);
        var exploded = Segments(Batches(g));
        var (lo, hi) = Box(segs.SelectMany(s => new[] { s.A, s.B }));
        var (elo, ehi) = Box(exploded.SelectMany(s => new[] { s.A, s.B }));
        Assert.True(Near(lo, elo, 1e-3) && Near(hi, ehi, 1e-3), $"symbol {lo}–{hi}, exploded {elo}–{ehi}");
        Assert.InRange(segs.Count, exploded.Count * 0.8, exploded.Count * 1.25);

        // The import's 3D switch takes it out and back, one undo step.
        f.Run("drafting.setUnderlay3d", new JsonObject { ["id"] = importId, ["shown"] = false });
        Assert.Empty(Batches(f));
        f.Session.Undo(f.Session.Author);
        Assert.Single(Batches(f));

        // Moved, it goes with its instance.
        f.Run("drafting.setSymbol", new JsonObject { ["id"] = instance.Id, ["at"] = J(8, 2) });
        Assert.Contains(Segments(Batches(f)), s => Near(s.A, new Vec3(8, 2, 3)) && Near(s.B, new Vec3(8, 3, 3)));
    }

    [Fact]
    public void A_symbol_saved_from_drafting_stands_in_3D_as_its_drafting_did_and_moves_with_its_instance()
    {
        var f = F();
        var plan = Plan(f, 3);
        var ids = f.Run("drafting.draw", new JsonObject
        {
            ["viewId"] = plan,
            ["shapes"] = new JsonArray(new JsonObject { ["type"] = "line", ["a"] = J(1, 1), ["b"] = J(2, 1) }, new JsonObject { ["type"] = "circle", ["c"] = J(1, 1), ["r"] = 0.3 }),
        })!["ids"]!.AsArray().Select(x => (string)x!).ToList();
        var before = ids.SelectMany(id => f.Session.Geometry(id)!.Lines!).ToList();

        foreach (var scale in new[] { "model", "annotation" })
        {
            var made = f.Run("drafting.toSymbol", new JsonObject
            {
                ["viewId"] = plan, ["ids"] = new JsonArray([.. ids.Select(x => (JsonNode)x)]), ["base"] = J(1, 1), ["name"] = "Pile", ["scale"] = scale, ["replace"] = false,
            })!;
            var instance = (string)f.Run("drafting.placeSymbol", new JsonObject { ["viewId"] = plan, ["symbolId"] = (string)made["symbolId"]!, ["at"] = J(1, 1) })!["id"]!;
            var lines = f.Session.Geometry(instance)?.Lines;
            Assert.NotNull(lines);
            Assert.Equal(before.Count, lines!.Count);
            Assert.All(lines.SelectMany(l => l), p => Assert.Equal(3, p.Z, 9));
            Assert.Contains(lines, l => l.Count == 2 && Near(l[0], new Vec3(1, 1, 3)) && Near(l[1], new Vec3(2, 1, 3)));
            var ring = lines.Single(l => l.Count > 2);
            Assert.All(ring, p => Assert.Equal(0.3, Math.Sqrt((p.X - 1) * (p.X - 1) + (p.Y - 1) * (p.Y - 1)), 6));

            // Moved and turned, it goes with its instance.
            f.Run("drafting.setSymbol", new JsonObject { ["id"] = instance, ["at"] = J(4, 1), ["angle"] = 90 });
            Assert.Contains(f.Session.Geometry(instance)!.Lines!, l => l.Count == 2 && Near(l[0], new Vec3(4, 1, 3)) && Near(l[1], new Vec3(4, 2, 3)));
        }

        // On a sheet: no 3D.
        var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Details", ["number"] = "A-501", ["paper"] = "A3" })!["id"]!;
        var symbolId = Symbols.Instances(f.Model).First().Str("symbolId")!;
        var onSheet = (string)f.Run("drafting.placeSymbol", new JsonObject { ["sheetId"] = sheet, ["symbolId"] = symbolId, ["at"] = J(100, 100) })!["id"]!;
        Assert.Null(f.Session.Geometry(onSheet));
    }
}
