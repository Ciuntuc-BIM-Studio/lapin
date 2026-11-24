using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Speckle;
using Lapin.Structure;
using Microsoft.Extensions.DependencyInjection;
using Speckle.Objects.Data;
using Speckle.Objects.Other;
using Speckle.Sdk;
using Speckle.Sdk.Api;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using SMesh = Speckle.Objects.Geometry.Mesh;

namespace Lapin.Engine.Tests;

/// <summary>
/// Recognising a Speckle version's columns, beams, doors and windows (docs/plan-speckle.md), under the same fidelity gate as
/// its walls and slabs, no network: a rectangular concrete column and beam made native with their sections; a steel column
/// and beam of no parametric section made with their bodies' own outlines; a door in a recognised wall that no configurator
/// makes as its body is staying a reference in the wall's native opening; and a column moved, then removed, by later versions.
/// </summary>
// One at a time: Speckle's SDK keeps one type loader, which setting it up again resets.
[Collection("Speckle SDK")]
public sealed class SpeckleMembersTests
{
    const string Server = "https://speckle.test";

    static (Fixture F, SpeckleModule Module) Project()
    {
        var module = new SpeckleModule();
        return (new Fixture(new ArchitectureModule(), new StructureModule(), module), module);
    }

    /// <summary>
    /// A prism in millimetres: <paramref name="outline"/> (in its own u, v) swept from <paramref name="from"/> to <paramref name="to"/>,
    /// <paramref name="place"/> taking (u, v, w) to x, y, z; its caps triangulated (the outline may be concave).
    /// </summary>
    static SMesh Sweep(string appId, (double U, double V)[] outline, double from, double to, Func<double, double, double, Vec3> place)
    {
        var n = outline.Length;
        double area = 0;
        for (var i = 0; i < n; i++) { var j = (i + 1) % n; area += outline[i].U * outline[j].V - outline[j].U * outline[i].V; }
        var o = area < 0 ? [.. outline.Reverse()] : outline;
        var v = new List<double>();
        foreach (var w in new[] { from, to })
            foreach (var (u, vv) in o) { var p = place(u, vv, w); v.AddRange([p.X, p.Y, p.Z]); }
        var tris = Triangulator.Face([.. o.Select(p => new Vec3(p.U, p.V, 0))], [.. Enumerable.Range(0, n)], null, Vec3.UnitZ);
        var f = new List<int>();
        foreach (var (a, b, c) in tris) f.AddRange([3, a, c, b, 3, n + a, n + b, n + c]);
        for (var i = 0; i < n; i++) f.AddRange([4, i, (i + 1) % n, n + (i + 1) % n, n + i]);
        // The place may mirror (u, v, w) → (x, y, z): the faces then turned the other way, so the solid's outside stays outside.
        var p0 = place(0, 0, 0);
        var mirrored = Vec3.Dot(Vec3.Cross(place(1, 0, 0) - p0, place(0, 1, 0) - p0), place(0, 0, 1) - p0) < 0;
        if (mirrored)
            for (var i = 0; i < f.Count;)
            {
                var k = f[i];
                f.Reverse(i + 1, k);
                i += k + 1;
            }
        return new SMesh { applicationId = appId + "/mesh", units = "mm", vertices = v, faces = f };
    }

    static (double, double)[] Rect(double w, double d) => [(-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d / 2), (-w / 2, d / 2)];

    /// <summary>A wide-flange I, h deep, b wide, web tw, flanges tf, about its centre (no root radii: an outline, not a catalogue profile).</summary>
    static (double, double)[] I(double h, double b, double tw, double tf) =>
        [(-b / 2, -h / 2), (b / 2, -h / 2), (b / 2, -h / 2 + tf), (tw / 2, -h / 2 + tf), (tw / 2, h / 2 - tf), (b / 2, h / 2 - tf), (b / 2, h / 2),
         (-b / 2, h / 2), (-b / 2, h / 2 - tf), (-tw / 2, h / 2 - tf), (-tw / 2, -h / 2 + tf), (-b / 2, -h / 2 + tf)];

    static DataObject Obj(string appId, string category, string type, SMesh body) => new()
    {
        applicationId = appId, name = $"{category} - {type}", displayValue = [body],
        properties = new() { ["category"] = category, ["type"] = type },
    };

    /// <summary>A column standing at (x, y) from z0 to z1 (mm), its section given about its centre.</summary>
    static DataObject Column(string appId, (double, double)[] section, double x, double y, double z0 = 0, double z1 = 3000, string type = "Concrete-Rectangular-Column: 400 x 400mm") =>
        Obj(appId, "Structural Columns", type, Sweep(appId, section, z0, z1, (u, v, w) => new Vec3(x + u, y + v, w)));

    /// <summary>A beam along x from x0 to x1 at y, its top at <paramref name="top"/> (mm), its section given (width across, depth up) about its centre.</summary>
    static DataObject Beam(string appId, (double, double)[] section, double depth, double x0, double x1, double y, double top, string type) =>
        Obj(appId, "Structural Framing", type, Sweep(appId, section, x0, x1, (u, v, w) => new Vec3(w, y + u, top - depth / 2 + v)));

    static RootCollection Version(params Base[] objects)
    {
        SpeckleSdk.Ensure();
        var root = new RootCollection { name = "Structure", applicationId = "structure-root", elements = [new Collection { name = "Level 1", elements = [.. objects] }] };
        var level = new DataObject { name = "Level 1", displayValue = [], properties = [] };
        level["elevation"] = 0.0;
        level["units"] = "mm";
        root["levelProxies"] = new List<LevelProxy> { new() { applicationId = "level-1", value = level, objects = [.. objects.Select(o => o.applicationId!)] } };
        return root;
    }

#pragma warning disable CS0618
    static async Task<Base> RoundTrip(Base root)
    {
        var ops = SpeckleSdk.Services.GetRequiredService<IOperations>();
        return await ops.DeserializeAsync(ops.Serialize(root));
    }
#pragma warning restore CS0618

    static JsonObject Receive(Fixture f, SpeckleModule module, Base root, string version)
    {
        module.Prime(new SpeckleFetched(new SpeckleSource(Server, "p1", "m1", "Structure", version, "revit"), root));
        return f.Run("speckle.receive", new JsonObject { ["projectId"] = "p1", ["modelId"] = "m1", ["versionId"] = version, ["recognize"] = true })!.AsObject();
    }

    static Entity? ByApp(Fixture f, string app) => f.Model.Entities.SingleOrDefault(e => (string?)(e.Attrs[SpeckleReceive.Key] as JsonObject)?["applicationId"] == app && e.Class != IfcClass.BuildingStorey);
    static bool Native(Entity e) => !StoredBodies.Has(e);
    static double Volume(Fixture f, string id) => f.Session.Reader.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public async Task A_rectangular_column_and_beam_come_in_native_with_their_sections()
    {
        var (f, module) = Project();
        var r = Receive(f, module, await RoundTrip(Version(
            Column("c1", Rect(400, 400), 5000, 5000),
            Beam("b1", Rect(300, 600), 600, 0, 6000, 2000, 3000, "Concrete-Rectangular Beam: 300 x 600mm"))), "v1");
        Assert.Equal(2, (int)r["recognized"]!);
        var c = ByApp(f, "c1")!;
        Assert.Equal(IfcClass.Column, c.Class);
        Assert.True(Native(c));
        var (lo, hi) = f.Bounds(c.Id);
        Assert.Equal(4.8, lo.X, 3); Assert.Equal(5.2, hi.X, 3); Assert.Equal(0, lo.Z, 3); Assert.Equal(3.0, hi.Z, 3);
        var b = ByApp(f, "b1")!;
        Assert.Equal(IfcClass.Beam, b.Class);
        Assert.True(Native(b));
        (lo, hi) = f.Bounds(b.Id);
        Assert.Equal(6.0, hi.X - lo.X, 3); Assert.Equal(0.3, hi.Y - lo.Y, 3); Assert.Equal(2.4, lo.Z, 3); Assert.Equal(3.0, hi.Z, 3);
        // Provenance kept, and they cut nothing they meet (a column's priority the wall's).
        Assert.Equal("native", (string?)c.Attrs[SpeckleReceive.Key]!["recognition"]);
        Assert.Equal(50, c.Num("cutPriority"));
    }

    [Fact]
    public async Task A_steel_column_and_beam_of_no_parametric_section_come_in_with_their_bodies_own_outlines()
    {
        var (f, module) = Project();
        var shape = I(260, 260, 8, 12);
        var r = Receive(f, module, await RoundTrip(Version(
            Column("c1", shape, 3000, 1000, type: "W-Wide Flange-Column: W10X49"),
            Beam("b1", I(400, 180, 8, 13), 400, 0, 5000, 4000, 3000, "W-Wide Flange: W16X36"))), "v1");
        Assert.Equal(2, (int)r["recognized"]!);
        var c = ByApp(f, "c1")!;
        Assert.Equal(IfcClass.Column, c.Class);
        var area = (260 * 12 * 2 + (260 - 24) * 8) / 1e6;
        Assert.Equal(area * 3, Volume(f, c.Id), 6);
        Assert.Equal("outline", (string?)f.Model.RelatingOf(c.Id, IfcClass.RelDefinesByType)!.Attrs["section"]!["kind"]);
        var b = ByApp(f, "b1")!;
        Assert.Equal(IfcClass.Beam, b.Class);
        Assert.Equal((180 * 13 * 2 + (400 - 26) * 8) / 1e6 * 5, Volume(f, b.Id), 6);
        // A column turned 30° comes in turned.
        var (g, module2) = Project();
        var turned = Column("c2", shape, 0, 0);
        var a = Math.PI / 6;
        turned.displayValue = [Sweep("c2", shape, 0, 3000, (u, v, w) => new Vec3(u * Math.Cos(a) - v * Math.Sin(a), u * Math.Sin(a) + v * Math.Cos(a), w))];
        Receive(g, module2, await RoundTrip(Version(turned)), "v1");
        var c2 = ByApp(g, "c2")!;
        Assert.True(Native(c2));
        // Its plan read to 0.1 mm across the turn: within the gate, not to the cubic millimetre.
        Assert.True(Math.Abs(Volume(g, c2.Id) / (area * 3) - 1) < 0.005);
    }

    /// <summary>
    /// A round column as Revit facets it (24 sides, no corner on the axes: 397 mm across one way for a 400 mm column): Lapin's
    /// circle, chorded by its own rule, is over 1 mm off those facets, so it comes in as the 24-sided polygon it is, exactly.
    /// </summary>
    [Fact]
    public async Task A_faceted_round_column_comes_in_as_the_polygon_its_facets_are()
    {
        var (f, module) = Project();
        var ring = Enumerable.Range(0, 24).Select(i => (200 * Math.Cos((i + 0.5) * Math.PI / 12), 200 * Math.Sin((i + 0.5) * Math.PI / 12))).ToArray();
        Receive(f, module, await RoundTrip(Version(Column("c1", ring, 500, 500, type: "Circular-Column"))), "v1");
        var c = ByApp(f, "c1")!;
        Assert.True(Native(c));
        var area = 0.5 * 24 * 0.2 * 0.2 * Math.Sin(Math.PI / 12);
        Assert.Equal(area * 3, Volume(f, c.Id), 6);
    }

    /// <summary>A beam running 40 mm into the columns at its ends, as Revit's meshes show it: native, its whole length, the columns too.</summary>
    [Fact]
    public async Task A_beam_running_into_its_columns_keeps_its_length()
    {
        var (f, module) = Project();
        var r = Receive(f, module, await RoundTrip(Version(
            Column("c1", Rect(400, 400), 0, 0, 0, 3600), Column("c2", Rect(400, 400), 6000, 0, 0, 3600),
            Beam("b1", Rect(300, 600), 600, 160, 5840, 0, 3000, "Concrete-Rectangular Beam: 300 x 600mm"))), "v1");
        Assert.Equal(3, (int)r["recognized"]!);
        var b = ByApp(f, "b1")!;
        var (lo, hi) = f.Bounds(b.Id);
        Assert.Equal(0.16, lo.X, 4); Assert.Equal(5.84, hi.X, 4);
        Assert.True(b.Bool(Beams.ThroughColumnsKey));
    }

    /// <summary>
    /// Beams as Revit sends a family's instances (snowdon structural's framing): one definition's mesh, each beam an instance of it
    /// placed by its transform. Recognised together, each is measured where it stands.
    /// </summary>
    [Fact]
    public async Task Beams_that_are_instances_of_one_definition_come_in_native_each_where_it_stands()
    {
        var (f, module) = Project();
        var def = Sweep("beam-def", Rect(300, 600), 0, 5000, (u, v, w) => new Vec3(w, u, -300 + v));
        global::Speckle.DoubleNumerics.Matrix4x4 At(double x, double y, double z) => new(1, 0, 0, x, 0, 1, 0, y, 0, 0, 1, z, 0, 0, 0, 1);
        var beams = new[] { (1000.0, 0.0, 3000.0), (1000.0, 4000.0, 3000.0), (2000.0, 8000.0, 2500.0) }.Select((p, i) => (Base)new DataObject
        {
            applicationId = $"b{i}", name = "Structural Framing - Concrete-Rectangular Beam: 300 x 600mm",
            properties = new() { ["category"] = "Structural Framing", ["type"] = "300 x 600mm" },
            displayValue = [new global::Speckle.Sdk.Models.Instances.InstanceProxy { applicationId = $"b{i}/i", definitionId = "beam-type", transform = At(p.Item1, p.Item2, p.Item3), units = "mm", maxDepth = 0 }],
        }).ToArray();
        var root = Version(beams);
        root.elements.Add(new Collection { name = "definitionGeometry", elements = [def] });
        root["instanceDefinitionProxies"] = new List<global::Speckle.Sdk.Models.Instances.InstanceDefinitionProxy> { new() { applicationId = "beam-type", name = "Beam", objects = ["beam-def/mesh"], maxDepth = 0 } };
        var r = Receive(f, module, await RoundTrip(root), "v1");
        Assert.True(3 == (int)r["recognized"]!, r["recognition"]!.ToJsonString());
        var (lo, hi) = f.Bounds(ByApp(f, "b2")!.Id);
        Assert.Equal(2.0, lo.X, 4); Assert.Equal(7.0, hi.X, 4); Assert.Equal(1.9, lo.Z, 4); Assert.Equal(2.5, hi.Z, 4);
    }

    /// <summary>
    /// Two steel beams of one box, one type name, and different sections (a web 8 mm and one 14 mm): each its own native type,
    /// each its own body. A type made for one outline is never another's, though their labels are the same.
    /// </summary>
    [Fact]
    public async Task Beams_of_one_box_and_type_name_but_other_sections_take_types_of_their_own()
    {
        var (f, module) = Project();
        var r = Receive(f, module, await RoundTrip(Version(
            Beam("b1", I(400, 180, 8, 13), 400, 0, 5000, 0, 3000, "W16X36"),
            Beam("b2", I(400, 180, 14, 13), 400, 0, 5000, 3000, 3000, "W16X36"))), "v1");
        Assert.Equal(2, (int)r["recognized"]!);
        Assert.Equal((180 * 13 * 2 + 374 * 8) / 1e6 * 5, Volume(f, ByApp(f, "b1")!.Id), 6);
        Assert.Equal((180 * 13 * 2 + 374 * 14) / 1e6 * 5, Volume(f, ByApp(f, "b2")!.Id), 6);
        var types = new[] { "b1", "b2" }.Select(a => f.Model.RelatingOf(ByApp(f, a)!.Id, IfcClass.RelDefinesByType)!).ToList();
        Assert.NotEqual(types[0].Id, types[1].Id);
        Assert.StartsWith("W16X36", types[0].Name);
    }

    [Fact]
    public async Task A_door_no_configurator_makes_stays_a_reference_in_its_recognised_walls_native_opening()
    {
        var (f, module) = Project();
        // The wall's body with the door's hole through it (900 × 2100 from the floor), a prism across its thickness.
        var hole = new (double, double)[] { (0, 0), (1000, 0), (1000, 2100), (1900, 2100), (1900, 0), (4000, 0), (4000, 2800), (0, 2800) };
        var wall = Obj("w1", "Walls", "Generic - 200mm", Sweep("w1", hole, 0, 200, (u, v, w) => new Vec3(u, w, v)));
        // The door: a slab of a leaf in the hole, nothing a configurator makes (no frame, no lining).
        var door = Obj("d1", "Doors", "Single-Flush: 900 x 2100mm", Sweep("d1", Rect(900, 2100), -30, 30, (u, v, w) => new Vec3(1450 + u, 100 + w, 1050 + v)));
        var r = Receive(f, module, await RoundTrip(Version(wall, door)), "v1");
        var w1 = ByApp(f, "w1")!;
        Assert.True(Native(w1));
        Assert.Equal(IfcClass.Wall, w1.Class);
        var opening = Assert.Single(f.Model.RelatedOf(w1.Id, IfcClass.RelVoidsElement));
        Assert.Empty(f.Model.RelatedOf(opening.Id, IfcClass.RelFillsElement));
        var d1 = ByApp(f, "d1")!;
        Assert.True(StoredBodies.Has(d1));
        Assert.Equal(IfcClass.Door, d1.Class);
        Assert.Equal(1, (int)r["recognition"]!["kept"]![SpeckleRecognition.FillDiffers]!);
        // The wall's body is the received one, hole and all.
        Assert.Equal((4 * 2.8 - 0.9 * 2.1) * 0.2, Volume(f, w1.Id), 6);
        Assert.NotNull(Groups.Of(f.Model, d1.Id));
        Assert.Equal(Spatial.StoreyOf(f.Model, w1.Id)!.Id, Spatial.StoreyOf(f.Model, d1.Id)!.Id);
    }

    [Fact]
    public async Task A_column_moved_comes_in_again_native_with_its_id_and_one_removed_goes()
    {
        var (f, module) = Project();
        Receive(f, module, await RoundTrip(Version(Column("c1", Rect(400, 400), 0, 0), Column("c2", Rect(400, 400), 6000, 0))), "v1");
        var c1 = ByApp(f, "c1")!.Id;
        var c2 = ByApp(f, "c2")!.Id;
        var r = Receive(f, module, await RoundTrip(Version(Column("c1", Rect(400, 400), 1000, 500), Column("c2", Rect(400, 400), 6000, 0))), "v2");
        Assert.Equal(1, (int)r["updated"]!);
        Assert.Equal(1, (int)r["unchanged"]!);
        var moved = f.Model.Get(c1)!;
        Assert.True(Native(moved));
        Assert.Equal(1.0, (f.Bounds(c1).Item1.X + f.Bounds(c1).Item2.X) / 2, 3);
        Assert.Equal(0.5, (f.Bounds(c1).Item1.Y + f.Bounds(c1).Item2.Y) / 2, 3);
        Receive(f, module, await RoundTrip(Version(Column("c1", Rect(400, 400), 1000, 500))), "v3");
        Assert.Null(f.Model.Get(c2));
        Assert.True(Native(f.Model.Get(c1)!));
    }
}
