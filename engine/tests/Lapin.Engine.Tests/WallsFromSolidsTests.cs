using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Mesh;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Wood;

namespace Lapin.Engine.Tests;

/// <summary>
/// Make a layered wall from solids (forum topic 101): a timber-frame house wall modelled in SketchUp as separate parallel
/// solids (cladding, board, insulation with its studs, OSB, gypsum) brought in as references, made one Lapin wall with a
/// type of those layers in their order and thicknesses, its openings through all of them, what differs by more than
/// 1 mm reported, the solids kept hidden behind it; one undo step.
/// </summary>
public class WallsFromSolidsTests
{
    static Fixture F() => new(new ArchitectureModule(), new WoodModule());

    /// <summary>One OBJ box, x0..x1 × y0..y1 × z0..z1, its faces outward (the vertices numbered from <paramref name="at"/>).</summary>
    static string Box(StringBuilder sb, ref int at, double x0, double y0, double z0, double x1, double y1, double z1)
    {
        string N(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        foreach (var (x, y, z) in new[] { (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1) })
            sb.Append($"v {N(x)} {N(y)} {N(z)}\n");
        var b = at;
        int V(int i) => b + i;
        sb.Append($"f {V(1)} {V(4)} {V(3)} {V(2)}\nf {V(5)} {V(6)} {V(7)} {V(8)}\nf {V(1)} {V(2)} {V(6)} {V(5)}\nf {V(3)} {V(4)} {V(8)} {V(7)}\nf {V(1)} {V(5)} {V(8)} {V(4)}\nf {V(2)} {V(3)} {V(7)} {V(6)}\n");
        at += 8;
        return sb.ToString();
    }

    sealed record Piece(string Name, double Y0, double Y1, double X0 = 0, double X1 = 4, double Z0 = 0, double Z1 = 2.7);

    /// <summary>A window's hole through every piece: x 1.0–2.2, sill 0.9, head 2.1.</summary>
    static readonly (double X0, double X1, double Sill, double Head) Window = (1.0, 2.2, 0.9, 2.1);

    /// <summary>The pieces as an OBJ file (Z up), a piece with a hole made of the four boxes round it.</summary>
    static string Obj(IEnumerable<Piece> pieces, bool hole = false, bool windowSolid = false)
    {
        var sb = new StringBuilder();
        var at = 0;
        foreach (var p in pieces)
        {
            sb.Append($"o {p.Name}\n");
            if (!hole) { Box(sb, ref at, p.X0, p.Y0, p.Z0, p.X1, p.Y1, p.Z1); continue; }
            var (h0, h1, sill, head) = Window;
            Box(sb, ref at, p.X0, p.Y0, p.Z0, h0, p.Y1, p.Z1);
            Box(sb, ref at, h1, p.Y0, p.Z0, p.X1, p.Y1, p.Z1);
            Box(sb, ref at, h0, p.Y0, p.Z0, h1, p.Y1, sill);
            Box(sb, ref at, h0, p.Y0, head, h1, p.Y1, p.Z1);
        }
        if (windowSolid)
        {
            sb.Append("o Window 1\n");
            Box(sb, ref at, Window.X0 + 0.01, 0.06, Window.Sill + 0.01, Window.X1 - 0.01, 0.14, Window.Head - 0.01);
        }
        return sb.ToString();
    }

    /// <summary>The Swedish wall of the forum's topic, outside to inside: cladding 22, wind board 9, insulation 145, OSB 12, gypsum 13 (201 mm).</summary>
    static List<Piece> Five(double gap = 0) =>
    [
        new("Träpanel", 0, 0.022),
        new("Vindskydd", 0.022 + gap, 0.031 + gap),
        new("Mineralull", 0.031 + gap, 0.176 + gap),
        new("OSB", 0.176 + gap, 0.188 + gap),
        new("Gips", 0.188 + gap, 0.201 + gap),
    ];

    static List<string> Import(Fixture f, string obj)
    {
        MeshImport.Run(f.Session, Encoding.UTF8.GetBytes(obj), "wall.obj", new JsonObject { ["up"] = "z" }, null, out _);
        return [.. f.Model.Entities.Where(StoredBodies.Has).Select(e => e.Id)];
    }

    static JsonArray Ids(IEnumerable<string> ids) => new([.. ids.Select(x => (JsonNode)x)]);

    static JsonObject Make(Fixture f, IEnumerable<string> ids, string gaps = "air", string originals = "hide", bool frame = false) =>
        (JsonObject)f.Run("architecture.wallFromSolids", new JsonObject { ["ids"] = Ids(ids), ["gaps"] = gaps, ["originals"] = originals, ["frame"] = frame })!;

    static List<(string Material, double Thickness, bool Core)> TypeLayers(Fixture f, string wallId)
    {
        var type = Walls.TypeOf(f.Model, wallId)!;
        return [.. LayerSets.LayersOf(f.Model, type.Id).Select(l => (f.Model.Get(l.Material)!.Name, Math.Round(l.Thickness, 4), l.Core))];
    }

    [Fact]
    public void Five_parallel_boxes_become_one_wall_of_five_layers_in_their_order_and_thicknesses()
    {
        var f = F();
        var ids = Import(f, Obj(Five()));
        // The preview shows it and changes nothing.
        var before = f.Model.Entities.Count();
        var preview = (JsonObject)f.Session.Query("architecture.wallFromSolids", new JsonObject { ["ids"] = Ids(ids) })!;
        Assert.Equal(before, f.Model.Entities.Count());
        var pw = Assert.Single(preview["walls"]!.AsArray())!;
        Assert.Equal(5, pw["layers"]!.AsArray().Count);
        Assert.Empty(pw["differences"]!.AsArray());

        var r = Make(f, ids);
        var wall = (string)r["made"]![0]!["id"]!;
        Assert.Equal(
            [("Träpanel", 0.022, false), ("Vindskydd", 0.009, false), ("Mineralull", 0.145, true), ("OSB", 0.012, false), ("Gips", 0.013, false)],
            TypeLayers(f, wall));
        // On its axis, as long and as high as the solids, standing where they stand: its body is theirs within a millimetre.
        var e = f.Model.Get(wall)!;
        Assert.Equal(4.0, (Walls.End(e) - Walls.Start(e)).Length, 4);
        Assert.Equal(2.7, Walls.HeightOf(f.Model, e), 4);
        var (lo, hi) = f.Bounds(wall);
        Assert.Equal(0, lo.X, 3); Assert.Equal(4, hi.X, 3);
        Assert.Equal(0, lo.Y, 3); Assert.Equal(0.201, hi.Y, 3);
        Assert.Equal(0, lo.Z, 3); Assert.Equal(2.7, hi.Z, 3);
        // Each layer where its solid was: the core's middle on the axis (y = 0.031 + 0.0725).
        Assert.Equal(0.1035, Walls.Start(e).Y, 4);
        var bands = Walls.Section(f.Model, e).OrderBy(b => b.Index).ToList();
        var cladding = bands[0];
        Assert.Equal(0.022, cladding.To - cladding.From, 4);
        Assert.True(Math.Abs((cladding.From + cladding.To) / 2 + Walls.Start(e).Y - 0.011) < 1e-4 || Math.Abs(-(cladding.From + cladding.To) / 2 + Walls.Start(e).Y - 0.011) < 1e-4);
        // The solids kept hidden behind it, in no view and no export; shown again on asking.
        foreach (var id in ids) Assert.True(Superseded.Hidden(f.Model, f.Model.Get(id)!));
        Assert.Equal(5, (int)f.Run("architecture.showOriginalSolids", new JsonObject { ["id"] = wall })!["shown"]!);
        Assert.False(Superseded.Hidden(f.Model, f.Model.Get(ids[0])!));
    }

    [Fact]
    public void A_small_gap_becomes_an_air_layer_or_goes_to_the_thicker_neighbour()
    {
        var f = F();
        var ids = Import(f, Obj(Five(gap: 0.005)));
        var air = Make(f, ids, gaps: "air", originals: "keep");
        Assert.Equal(
            [("Träpanel", 0.022, false), (WallsFromSolids.AirMaterial, 0.005, false), ("Vindskydd", 0.009, false), ("Mineralull", 0.145, true), ("OSB", 0.012, false), ("Gips", 0.013, false)],
            TypeLayers(f, (string)air["made"]![0]!["id"]!));
        f.Session.Undo();
        var absorbed = Make(f, ids, gaps: "absorb", originals: "keep");
        Assert.Equal(
            [("Träpanel", 0.027, false), ("Vindskydd", 0.009, false), ("Mineralull", 0.145, true), ("OSB", 0.012, false), ("Gips", 0.013, false)],
            TypeLayers(f, (string)absorbed["made"]![0]!["id"]!));
        // The cladding is 5 mm thicker than its solid: said.
        var d = Assert.Single(absorbed["walls"]![0]!["differences"]!.AsArray())!;
        Assert.Equal("Träpanel", (string)d["name"]!);
        Assert.Equal(5, (double)d["mm"]!, 1);
    }

    [Fact]
    public void An_opening_through_every_layer_is_the_walls_and_the_window_solid_in_it_a_window()
    {
        var f = F();
        var ids = Import(f, Obj(Five(), hole: true, windowSolid: true));
        var r = Make(f, ids);
        var made = r["made"]![0]!;
        var wall = (string)made["id"]!;
        var opening = Assert.Single(Openings.Of(f.Model, wall));
        var p = Openings.Place(f.Model, opening);
        Assert.Equal(1.6, p.Along, 4);
        Assert.Equal(1.2, p.Width, 4);
        Assert.Equal(0.9, p.Sill, 4);
        Assert.Equal(1.2, p.Height, 4);
        var fill = Openings.FillOf(f.Model, opening.Id);
        Assert.Equal(IfcClass.Window, fill?.Class);
        Assert.Empty(r["walls"]![0]!["differences"]!.AsArray());
        // The window's solid is hidden with the layers.
        var windowSolid = f.Model.Entities.Single(e => e.Name == "Window 1");
        Assert.True(Superseded.Hidden(f.Model, windowSolid));
        // The wall's volume is the solids' (the window's left out): 4 × 2.7 × 0.201 less the hole.
        var volume = f.Session.Geometry(wall)!.Parts.Where(x => x.Key?.StartsWith("layer:") != false).Sum(x => Lapin.Geometry.Measure.Volume(x.Solid));
        Assert.Equal((4 * 2.7 - 1.2 * 1.2) * 0.201, volume, 3);
    }

    [Fact]
    public void A_solid_off_by_more_than_a_millimetre_is_reported()
    {
        var f = F();
        var pieces = Five();
        // The gypsum stops 2 mm short of the wall's end; the OSB a half millimetre (within the tolerance).
        pieces[4] = pieces[4] with { X1 = 3.998 };
        pieces[3] = pieces[3] with { X1 = 3.9995 };
        var ids = Import(f, Obj(pieces));
        var preview = (JsonObject)f.Session.Query("architecture.wallFromSolids", new JsonObject { ["ids"] = Ids(ids) })!;
        var d = Assert.Single(preview["walls"]![0]!["differences"]!.AsArray())!;
        Assert.Equal("Gips", (string)d["name"]!);
        Assert.Equal(2, (double)d["mm"]!, 1);
        Assert.Contains("end", (string)d["what"]!);
    }

    [Fact]
    public void Undone_in_one_step_the_solids_are_back_as_they_were()
    {
        var f = F();
        var ids = Import(f, Obj(Five()));
        var before = f.Model.Entities.Count();
        Make(f, ids, originals: "delete");
        Assert.All(ids, id => Assert.Null(f.Model.Get(id)));
        Assert.Single(f.Model.OfClass(IfcClass.Wall));
        f.Session.Undo();
        Assert.Empty(f.Model.OfClass(IfcClass.Wall));
        Assert.Equal(before, f.Model.Entities.Count());
        Assert.All(ids, id => Assert.True(StoredBodies.Has(f.Model.Get(id)!)));
        Assert.DoesNotContain(f.Model.OfClass(IfcClass.WallType), t => t.Name.Contains("Mineralull"));
    }

    [Fact]
    public void Two_walls_meeting_at_a_corner_are_made_at_once_and_joined()
    {
        var f = F();
        // One along X (y 0–0.2), one along Y standing against its end (x 3.8–4.0, y 0.2–3): three layers each.
        var obj = Obj(
        [
            new("Panel", 0, 0.02), new("Stomme", 0.02, 0.18), new("Gips", 0.18, 0.2),
        ]) + "\n";
        var sb = new StringBuilder(obj);
        var at = 3 * 8;
        foreach (var (name, x0, x1) in new[] { ("Panel B", 3.98, 4.0), ("Stomme B", 3.82, 3.98), ("Gips B", 3.8, 3.82) })
        {
            sb.Append($"o {name}\n");
            Box(sb, ref at, x0, 0.2, 0, x1, 3, 2.7);
        }
        var ids = Import(f, sb.ToString());
        var r = Make(f, ids);
        var walls = r["made"]!.AsArray().Select(x => (string)x!["id"]!).ToList();
        Assert.Equal(2, walls.Count);
        // The structural layer is the core by its name; both walls of one type (their solids were named alike but for the B).
        Assert.All(walls, w => Assert.True(TypeLayers(f, w)[1].Core));
        Assert.NotNull(Joins.Between(f.Model, walls[0], walls[1]));
    }

    [Fact]
    public void Studio_solids_take_their_materials_and_the_concrete_is_the_core()
    {
        var f = new Fixture(new ArchitectureModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Studio.StudioModule());
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        string Solid(double y0, double y1, string material)
        {
            var shape = new JsonObject { ["type"] = "polyline", ["pts"] = new JsonArray(new JsonArray(0, y0), new JsonArray(5, y0), new JsonArray(5, y1), new JsonArray(0, y1)), ["closed"] = true };
            var profile = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = plan, ["shape"] = shape })!["ids"]![0]!;
            var id = (string)f.Run("studio.extrude", new JsonObject { ["profileId"] = profile, ["depth"] = 3 })!["id"]!;
            var mat = (string)f.Run("material.create", new JsonObject { ["name"] = material })!["id"]!;
            f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["material"] = mat } });
            return id;
        }
        var ids = new[] { Solid(0, 0.1, "Insulation"), Solid(0.1, 0.3, "Concrete"), Solid(0.3, 0.315, "Plaster") };
        var r = Make(f, ids);
        var wall = (string)r["made"]![0]!["id"]!;
        Assert.Equal([("Insulation", 0.1, false), ("Concrete", 0.2, true), ("Plaster", 0.015, false)], TypeLayers(f, wall));
        Assert.Equal(3, Walls.HeightOf(f.Model, f.Model.Get(wall)!), 4);
    }

    [Fact]
    public void Studs_in_the_insulation_are_reported_or_made_its_timber_frame()
    {
        var f = F();
        var pieces = Five();
        var sb = new StringBuilder(Obj(pieces));
        var at = pieces.Count * 8;
        // Studs 45 × 145 every 600 mm in the insulation's stretch (the insulation overlapping them, as SketchUp users draw it).
        for (var i = 0; i < 7; i++)
        {
            var x = Math.Min(i * 0.6, 4 - 0.045);
            sb.Append($"o Regel {i + 1}\n");
            Box(sb, ref at, x, 0.031, 0, x + 0.045, 0.176, 2.7);
        }
        var ids = Import(f, sb.ToString());
        var preview = (JsonObject)f.Session.Query("architecture.wallFromSolids", new JsonObject { ["ids"] = Ids(ids) })!;
        var layer = preview["walls"]![0]!["layers"]![2]!;
        Assert.Equal("Mineralull", (string)layer["material"]!);
        Assert.Equal(7, (int)layer["inside"]!);
        Assert.Equal("Regel", (string)layer["insideMaterial"]!);
        Assert.Equal(0.045, (double)layer["studWidth"]!, 4);
        Assert.Equal(0.6, (double)layer["spacing"]!, 3);
        Assert.Equal("45x145 mm", (string)preview["walls"]![0]!["frameSection"]!);

        var r = Make(f, ids, frame: true);
        var wall = (string)r["made"]![0]!["id"]!;
        Assert.True(TypeLayers(f, wall)[2].Core);
        var spec = FrameSpec.Of(Walls.TypeOf(f.Model, wall));
        Assert.NotNull(spec);
        Assert.Equal("45x145 mm", spec!.StudSection);
        Assert.Equal(0.6, spec.Spacing, 3);
    }
}
