using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Bodies an external program computes (docs/plan-external-geometry.md, L1): element.createBody and element.setBody, one
/// undo step each; a shared body by content; a pinned GlobalId; drawn in plans, measured, copied, exported to IFC and read back.
/// </summary>
public class ExternalBodyTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-external-bodies").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static Fixture Make() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());

    /// <summary>A closed box from (x, y, z) to (x + a, y + b, z + c), as a mesh: 8 points, 12 triangles wound outwards.</summary>
    internal static JsonObject Box(double a, double b, double c, double x = 0, double y = 0, double z = 0)
    {
        double[] p = [x, y, z, x + a, y, z, x + a, y + b, z, x, y + b, z, x, y, z + c, x + a, y, z + c, x + a, y + b, z + c, x, y + b, z + c];
        int[] t = [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7];
        return new JsonObject { ["positions"] = new JsonArray([.. p.Select(v => (JsonNode)v)]), ["indices"] = new JsonArray([.. t.Select(v => (JsonNode)v)]) };
    }

    static JsonArray P(double x, double y, double z) => new(x, y, z);

    static JsonNode? Create(Fixture f, JsonObject body, string cls = "IfcMember", string? gid = null, JsonNode? placement = null, string? owner = "script:test", JsonObject? attrs = null)
    {
        var a = new JsonObject { ["ifcClass"] = cls, ["storey"] = f.Storey, ["body"] = body, ["name"] = "Truss" };
        if (gid != null) a["globalId"] = gid;
        if (placement != null) a["placement"] = placement;
        if (owner != null) a["owner"] = owner;
        if (attrs != null) a["attrs"] = attrs;
        return f.Run("element.createBody", a);
    }

    static double Q(Fixture f, string id, string name) => f.Session.Quantities(id).SelectMany(s => s.Quantities).Single(q => q.Name == name).Value;

    [Fact]
    public void A_body_is_created_set_and_undone_one_step_each()
    {
        var f = Make();
        var r = Create(f, Box(1, 1, 1))!;
        var id = (string)r["id"]!;
        Assert.True((bool)r["closed"]!);
        Assert.Equal(12, (int)r["triangles"]!);
        var e = f.Model.Get(id)!;
        Assert.Equal("IfcMember", e.Class);
        Assert.Equal("script:test", e.Str(ExternalBodies.OwnerKey));
        Assert.True(ExternalBodies.IsOwned(e));
        Assert.Equal(f.Storey, Spatial.StoreyOf(f.Model, id)!.Id);
        Assert.Equal(1, Q(f, id, "NetVolume"), 6);
        Assert.Equal(6, Q(f, id, "NetSurfaceArea"), 6);

        f.Run("element.setBody", new JsonObject { ["id"] = id, ["body"] = Box(2, 1, 1) });
        Assert.Equal(2, Q(f, id, "NetVolume"), 6);
        Assert.Equal(2, f.Bounds(id).Max.X, 6);
        Assert.Equal("Set body", f.Session.History.UndoLabel);
        // The body it had is gone with it: no shared body is left that nothing uses.
        Assert.Single(f.Model.OfClass(StoredBodies.SharedClass));

        f.Session.Undo();
        Assert.Equal(1, Q(f, id, "NetVolume"), 6);
        Assert.Single(f.Model.OfClass(StoredBodies.SharedClass));
        f.Session.Undo();
        Assert.Null(f.Model.Get(id));
        Assert.Empty(f.Model.OfClass(StoredBodies.SharedClass));
    }

    [Fact]
    public void A_pinned_GlobalId_is_kept_and_a_taken_or_malformed_one_refused()
    {
        var f = Make();
        var gid = GlobalId.New();
        Assert.Equal(gid, (string)Create(f, Box(1, 1, 1), gid: gid)!["id"]!);
        var taken = Assert.Throws<CommandException>(() => Create(f, Box(1, 1, 1, 3), gid: gid));
        Assert.Contains("is taken", taken.Message);
        Assert.Throws<CommandException>(() => Create(f, Box(1, 1, 1, 3), gid: "not-a-globalid"));
        // The storey's id is an entity's too.
        Assert.Throws<CommandException>(() => Create(f, Box(1, 1, 1, 3), gid: f.Storey.Length == 22 ? f.Storey : gid));
    }

    [Fact]
    public void The_same_body_is_kept_once_and_a_placement_places_it()
    {
        var f = Make();
        var a = (string)Create(f, Box(1, 1, 1))!["id"]!;
        var b = (string)Create(f, Box(1, 1, 1), placement: new JsonObject { ["at"] = P(5, 0, 1), ["angle"] = 90 })!["id"]!;
        Assert.Single(f.Model.OfClass(StoredBodies.SharedClass));
        var (lo, hi) = f.Bounds(b);
        Assert.Equal(4, lo.X, 6); Assert.Equal(5, hi.X, 6);
        Assert.Equal(0, lo.Y, 6); Assert.Equal(1, hi.Y, 6);
        Assert.Equal(1, lo.Z, 6); Assert.Equal(2, hi.Z, 6);
        // A 4×4 matrix, row major: a move by (0, 10, 0).
        var m = new JsonArray(1, 0, 0, 0, 0, 1, 0, 10, 0, 0, 1, 0, 0, 0, 0, 1);
        f.Run("element.setBody", new JsonObject { ["id"] = a, ["body"] = Box(1, 1, 1), ["placement"] = m });
        Assert.Equal(10, f.Bounds(a).Min.Y, 6);
    }

    [Fact]
    public void Planar_faces_with_a_hole_make_a_body_and_an_open_mesh_is_reported()
    {
        var f = Make();
        // A cube of six faces, the top one with a square hole closed by a well of four faces and a floor at mid height.
        var faces = new JsonArray
        {
            Face([P(0, 0, 0), P(0, 1, 0), P(1, 1, 0), P(1, 0, 0)]),
            Face([P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1)], [P(0.25, 0.25, 1), P(0.25, 0.75, 1), P(0.75, 0.75, 1), P(0.75, 0.25, 1)]),
            Face([P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1)]),
            Face([P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1)]),
            Face([P(1, 1, 0), P(0, 1, 0), P(0, 1, 1), P(1, 1, 1)]),
            Face([P(0, 1, 0), P(0, 0, 0), P(0, 0, 1), P(0, 1, 1)]),
            // The well: its walls face inwards (towards the hole's middle), its floor up.
            Face([P(0.25, 0.25, 1), P(0.25, 0.25, 0.5), P(0.25, 0.75, 0.5), P(0.25, 0.75, 1)]),
            Face([P(0.25, 0.75, 1), P(0.25, 0.75, 0.5), P(0.75, 0.75, 0.5), P(0.75, 0.75, 1)]),
            Face([P(0.75, 0.75, 1), P(0.75, 0.75, 0.5), P(0.75, 0.25, 0.5), P(0.75, 0.25, 1)]),
            Face([P(0.75, 0.25, 1), P(0.75, 0.25, 0.5), P(0.25, 0.25, 0.5), P(0.25, 0.25, 1)]),
            Face([P(0.25, 0.25, 0.5), P(0.75, 0.25, 0.5), P(0.75, 0.75, 0.5), P(0.25, 0.75, 0.5)]),
        };
        var r = Create(f, new JsonObject { ["faces"] = faces })!;
        Assert.True((bool)r["closed"]!);
        Assert.Equal(1 - 0.25 * 0.5, Q(f, (string)r["id"]!, "NetVolume"), 6);

        // A box without its top: drawn, its area measured, no volume.
        var open = Box(1, 1, 1, 3);
        var t = (JsonArray)open["indices"]!;
        for (var k = 0; k < 6; k++) t.RemoveAt(6);
        var o = Create(f, open)!;
        Assert.False((bool)o["closed"]!);
        Assert.NotEmpty(o["notes"]!.AsArray());
        Assert.DoesNotContain(f.Session.Quantities((string)o["id"]!).SelectMany(s => s.Quantities), q => q.Name == "NetVolume");
        // A face off its plane is refused.
        Assert.Throws<CommandException>(() => Create(f, new JsonObject { ["faces"] = new JsonArray(Face([P(0, 0, 0), P(1, 0, 0), P(1, 1, 0.01), P(0, 1, 0)])) }));
    }

    static JsonObject Face(JsonArray[] outer, JsonArray[]? hole = null)
    {
        var o = new JsonObject { ["outer"] = new JsonArray([.. outer]) };
        if (hole != null) o["holes"] = new JsonArray(new JsonArray([.. hole]));
        return o;
    }

    [Fact]
    public void A_mesh_in_millimetres_as_rows_of_three_is_read_as_a_mesh_library_gives_it()
    {
        var f = Make();
        // vertices N×3 and faces M×3, in integer millimetres, placed at (1000, 0, 0) mm.
        var box = Box(1000, 2000, 500);
        var p = ((JsonArray)box["positions"]!).Select(x => (double)x!).ToArray();
        var t = ((JsonArray)box["indices"]!).Select(x => (int)x!).ToArray();
        var body = new JsonObject
        {
            ["vertices"] = new JsonArray([.. Enumerable.Range(0, p.Length / 3).Select(i => (JsonNode)new JsonArray(p[3 * i], p[3 * i + 1], p[3 * i + 2]))]),
            ["faces"] = new JsonArray([.. Enumerable.Range(0, t.Length / 3).Select(i => (JsonNode)new JsonArray(t[3 * i], t[3 * i + 1], t[3 * i + 2]))]),
        };
        var r = f.Run("element.createBody", new JsonObject { ["ifcClass"] = "IfcSlab", ["body"] = body, ["units"] = "mm", ["placement"] = new JsonObject { ["at"] = P(1000, 0, 0) } })!;
        var id = (string)r["id"]!;
        Assert.True((bool)r["closed"]!);
        Assert.Equal(1, Q(f, id, "NetVolume"), 6);
        Assert.Equal(1, f.Bounds(id).Min.X, 6);
        Assert.Equal(2, f.Bounds(id).Max.X, 6);
        Assert.Throws<CommandException>(() => f.Run("element.createBody", new JsonObject { ["body"] = Box(1, 1, 1), ["units"] = "ft" }));
    }

    [Fact]
    public void Bad_input_is_refused_in_plain_words()
    {
        var f = Make();
        Assert.Contains("names no point", Assert.Throws<CommandException>(() => Create(f, new JsonObject { ["positions"] = new JsonArray(0, 0, 0, 1, 0, 0, 0, 1, 0), ["indices"] = new JsonArray(0, 1, 7) })).Message);
        Assert.Contains("metres", Assert.Throws<CommandException>(() => Create(f, Box(20_000, 1, 1))).Message);
        Assert.Throws<CommandException>(() => Create(f, new JsonObject()));
        // A class that is no IFC element: a proxy, saying so; a standard case is its class.
        var r = Create(f, Box(1, 1, 1), cls: "IfcTruss")!;
        Assert.Equal(IfcClass.BuildingElementProxy, f.Model.Get((string)r["id"]!)!.Class);
        Assert.Contains("IfcTruss", r["notes"]![0]!.ToString());
        Assert.Equal("IfcWall", f.Model.Get((string)Create(f, Box(1, 1, 1, 2), cls: "ifcwallstandardcase")!["id"]!)!.Class);
    }

    [Fact]
    public void Its_shape_is_the_programs_but_it_moves_copies_and_deletes_like_any()
    {
        var f = Make();
        var id = (string)Create(f, Box(1, 1, 1))!["id"]!;
        var refused = Assert.Throws<CommandException>(() => f.Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["height"] = 3 } }));
        Assert.Equal(ExternalBodies.Refused, refused.Message);
        Assert.Equal(ExternalBodies.Refused, Assert.Throws<CommandException>(() => f.Run("studio.fromElement", new JsonObject { ["ids"] = new JsonArray(id) })).Message);
        // Moved, then copied: the copy shares the body, under a GlobalId of its own, made by the same script.
        f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "move", ["by"] = new JsonArray(2, 0) });
        Assert.Equal(2, f.Bounds(id).Min.X, 6);
        var copy = (string)f.Run("element.transform", new JsonObject { ["ids"] = new JsonArray(id), ["op"] = "copy", ["by"] = new JsonArray(0, 3) })!["ids"]![0]!;
        Assert.NotEqual(id, copy);
        Assert.True(GlobalId.IsValid(copy));
        Assert.Single(f.Model.OfClass(StoredBodies.SharedClass));
        Assert.Equal("script:test", f.Model.Get(copy)!.Str(ExternalBodies.OwnerKey));
        Assert.Equal(3, f.Bounds(copy).Min.Y, 6);
        // Set again, the copy keeps its id and its place.
        f.Run("element.setBody", new JsonObject { ["id"] = copy, ["body"] = Box(1, 1, 2) });
        Assert.Equal(2, f.Bounds(copy).Max.Z, 6);
        Assert.Equal(3, f.Bounds(copy).Min.Y, 6);
        Assert.Equal(2, f.Model.OfClass(StoredBodies.SharedClass).Count());
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(id, copy) });
        Assert.Empty(f.Model.OfClass(StoredBodies.SharedClass));
        // Only an element a program made takes a body from outside.
        var wall = f.Wall(0, 0, 4, 0, height: 3);
        Assert.Throws<CommandException>(() => f.Run("element.setBody", new JsonObject { ["id"] = wall, ["body"] = Box(1, 1, 1) }));
    }

    [Fact]
    public void The_plan_cuts_it_where_its_storey_is_cut()
    {
        var f = Make();
        Create(f, Box(0.5, 0.5, 3, 2, 1));
        var d = (JsonObject)f.Session.Query("view.drawing", new JsonObject { ["id"] = "plan:" + f.Storey })!;
        var xs = d["pens"]!.AsArray().SelectMany(p => p!["segments"]?.AsArray() ?? []).Select((v, k) => (v, k)).Where(x => x.k % 2 == 0).Select(x => (double)x.v!).ToList();
        Assert.Contains(xs, x => Math.Abs(x - 2) < 1e-6);
        Assert.Contains(xs, x => Math.Abs(x - 2.5) < 1e-6);
    }

    [Theory]
    [InlineData("IFC4", "brep")]
    [InlineData("IFC4", "faceset")]
    [InlineData("IFC4X3", "faceset")]
    public void Exported_to_IFC_and_read_back_it_is_the_same_element(string schema, string geometry)
    {
        var f = Make();
        var gid = GlobalId.New();
        Create(f, Box(1, 2, 3, 4, 5), gid: gid, attrs: new JsonObject { ["span"] = 6.5, ["tag"] = "T1" });
        var pump = (string)Create(f, Box(1, 1, 1, 10), cls: "IfcPump", owner: "Lite program")!["id"]!;
        var path = Path.Combine(_dir, $"bodies-{schema}-{geometry}.ifc");
        IfcExport.Write(f.Session, path, new IfcExportOptions(Schema: schema, Geometry: geometry));
        var text = File.ReadAllText(path);
        Assert.Contains($"IFCMEMBER('{gid}'", text);
        Assert.Contains("'script:test'", text);
        Assert.Contains(geometry == "faceset" ? "IFCTRIANGULATEDFACESET(" : "IFCFACETEDBREP(", text.Replace("IFCPOLYGONALFACESET(", "IFCTRIANGULATEDFACESET("));

        var g = Make();
        IfcImport.Run(g.Session, path, null, out _);
        var e = g.Model.Get(gid)!;
        Assert.Equal("IfcMember", e.Class);
        Assert.Equal("script:test", e.Str(ExternalBodies.OwnerKey));
        Assert.Equal(6.5, Json.Number(e.Attrs[ExternalBodies.ExtraKey]!["span"]));
        Assert.True(ExternalBodies.IsOwned(e));
        var (lo, hi) = g.Bounds(gid);
        Assert.Equal(4, lo.X, 4); Assert.Equal(5, hi.X, 4);
        Assert.Equal(5, lo.Y, 4); Assert.Equal(7, hi.Y, 4);
        Assert.Equal(3, hi.Z - lo.Z, 4);
        Assert.Equal(6, g.Session.Quantities(gid).SelectMany(s => s.Quantities).Single(q => q.Name == "NetVolume").Value, 4);
        // A class Lapin's writer does not name went as a proxy naming it, and came back as itself.
        Assert.Equal("IfcPump", g.Model.Get(pump)!.Class);
        Assert.Equal("Lite program", g.Model.Get(pump)!.Str(ExternalBodies.OwnerKey));
        // And it takes a body from outside again.
        g.Run("element.setBody", new JsonObject { ["id"] = gid, ["body"] = Box(1, 1, 1) });
        Assert.Equal(1, g.Session.Quantities(gid).SelectMany(s => s.Quantities).Single(q => q.Name == "NetVolume").Value, 4);
    }

    [Fact]
    public void Saved_and_opened_it_is_still_the_programs()
    {
        var f = Make();
        var id = (string)Create(f, Box(1, 1, 1))!["id"]!;
        var path = Path.Combine(_dir, "bodies.lapin");
        Lapin.Host.PackageFiles.Save(f.Session, path);
        var g = Make();
        Lapin.Host.PackageFiles.Open(g.Session, path);
        Assert.True(ExternalBodies.IsOwned(g.Model.Get(id)!));
        Assert.Equal(1, g.Session.Quantities(id).SelectMany(s => s.Quantities).Single(q => q.Name == "NetVolume").Value, 6);
    }
}
