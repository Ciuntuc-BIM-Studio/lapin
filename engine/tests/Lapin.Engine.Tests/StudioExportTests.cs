using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Lapin.Geometry;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Mesh;
using Lapin.Mesh.Fragments;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Studio's solids in every export (docs/plan-studio-thick.md, decision 192): a ring, a box with its bottom and top, a wall strip, an
/// extrusion, a sweep, a revolution and a loft, each sketched on a plan of an upper storey, a section cut at an angle, an elevation with a
/// depth, a developed section's second piece and a tilted reference plane, written as IFC (B-rep and face sets), Fragments, glTF, USD and
/// COLLADA, then read back: each body at its world position with its volume, under its class and type, a box's parts in its assembly.
/// </summary>
public class StudioExportTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(double x, double y) => [x, y];
    static JsonArray J3(double x, double y, double z) => [x, y, z];
    static string Draw(Fixture f, string view, JsonObject shape) => (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shape"] = shape })!["ids"]![0]!;
    static JsonObject Rect(double x0, double y0, double x1, double y1) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(J(x0, y0), J(x1, y0), J(x1, y1), J(x0, y1)), ["closed"] = true };
    static JsonObject Poly(bool closed, params (double X, double Y)[] pts) =>
        new() { ["type"] = "polyline", ["pts"] = new JsonArray(pts.Select(p => (JsonNode)J(p.X, p.Y)).ToArray()), ["closed"] = closed };
    static JsonObject Circle(double x, double y, double r) => new() { ["type"] = "circle", ["c"] = J(x, y), ["r"] = r };
    static string Make(Fixture f, string op, string profile, JsonObject? more = null)
    {
        var a = new JsonObject { ["profileId"] = profile };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("studio." + op, a)!["id"]!;
    }

    /// <summary>What the model holds of a solid before it is exported: its world bounds and volume, the class it is exported as, its type, its assembly.</summary>
    sealed record Source(string Id, string Name, Vec3 Min, Vec3 Max, double Volume, string Class, string? Type, string? Assembly);

    sealed record Scene(Fixture F, List<Source> Sources, string Assembly, string Box, string Bottom, string Top);

    /// <summary>
    /// Every tool on every kind of plane: a ring, a box and a wall strip on the first floor's plan (the ring lifted by an offset), a box on a
    /// section cut at an angle to x, a ring on the south elevation (its plane at a depth), a wall strip on the second piece of a jogged
    /// section, and on a reference plane tilted by bearing, tilt and spin a typed box with its bottom and top, an extrusion, a sweep, a
    /// revolution and a loft to a second plane.
    /// </summary>
    static Scene Build()
    {
        var f = F();
        var first = (string)f.Run("storey.create", new JsonObject { ["name"] = "First floor", ["elevation"] = 3 })!["id"]!;
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = first })!["id"]!;
        var ids = new List<string>();
        ids.Add(Make(f, "ring", Draw(f, plan, Circle(2, 2, 1)), new() { ["thickness"] = 0.15, ["height"] = 1.2, ["offset"] = 0.2, ["name"] = "Ring on plan" }));
        var boxPlan = Make(f, "box", Draw(f, plan, Rect(5, 0, 8, 2)), new() { ["thickness"] = 0.2, ["height"] = 1, ["bottom"] = true, ["top"] = true, ["bottomThickness"] = 0.12, ["topThickness"] = 0.08, ["name"] = "Box on plan" });
        ids.Add(boxPlan);
        ids.AddRange(Boxes.SlabsOf(f.Model, boxPlan).Select(s => s.Id));
        ids.Add(Make(f, "wall", Draw(f, plan, Poly(false, (10, 0), (13, 1), (13, 4))), new() { ["thickness"] = 0.2, ["height"] = 0.9, ["justify"] = "right", ["name"] = "Strip on plan" }));
        // A section from (0, −6) to (8, −2): its cut plane at an angle to x.
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(0, -6), ["end"] = J(8, -2) })!["id"]!;
        ids.Add(Make(f, "box", Draw(f, section, Rect(1, 0.5, 3, 2)), new() { ["thickness"] = 0.1, ["height"] = 0.4, ["name"] = "Box on section" }));
        // The south elevation of a wall, its plane 0.6 m in front of the wall's face.
        f.Wall(20, 10, 26, 10);
        var elevation = (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!;
        var ringElev = Make(f, "ring", Draw(f, elevation, Circle(1, 1.5, 0.6)), new() { ["thickness"] = 0.1, ["height"] = 0.3, ["name"] = "Ring on elevation" });
        f.Run("element.update", new JsonObject { ["id"] = elevation, ["attrs"] = new JsonObject { ["planeDepth"] = f.Model.Get(elevation)!.Num("planeDepth") - 0.6 } });
        ids.Add(ringElev);
        // A jogged section: its second piece runs north on x = 30 from station 4.
        var jogged = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["pts"] = new JsonArray(J(26, 20), J(30, 20), J(30, 28)) })!["id"]!;
        ids.Add(Make(f, "wall", Draw(f, jogged, Poly(false, (5, 0.5), (7, 0.5), (7, 2))), new() { ["thickness"] = 0.15, ["height"] = 0.35, ["name"] = "Strip on jogged section" }));
        // A reference plane turned and tilted every way; a second one parallel to it for the loft.
        var plane = (string)f.Run("view.create", new JsonObject { ["kind"] = "plane", ["points"] = new JsonArray(J3(40, 0, 1), J3(42, 1, 1), J3(40, 0.5, 3)) })!["id"]!;
        f.Run("element.update", new JsonObject { ["id"] = plane, ["attrs"] = new JsonObject { ["bearing"] = 30, ["tilt"] = 35, ["spin"] = 20 } });
        var box = Make(f, "box", Draw(f, plane, Rect(0, 0, 2, 1)), new() { ["thickness"] = 0.1, ["height"] = 0.5, ["bottom"] = true, ["top"] = true, ["bottomThickness"] = 0.1, ["topThickness"] = 0.06, ["name"] = "Box on plane" });
        f.Run("studio.setType", new JsonObject { ["ids"] = new JsonArray(box), ["name"] = "Thin wall" });
        var slabs = Boxes.SlabsOf(f.Model, box).ToList();
        f.Run("studio.setType", new JsonObject { ["ids"] = new JsonArray(slabs.Select(s => (JsonNode)s.Id).ToArray()), ["name"] = "Thin slab" });
        var bottom = slabs.Single(s => s.Str("side") == "bottom").Id;
        var top = slabs.Single(s => s.Str("side") == "top").Id;
        ids.AddRange([box, bottom, top]);
        ids.Add(Make(f, "extrude", Draw(f, plane, Rect(3, 0, 4, 1.5)), new() { ["depth"] = 0.7, ["class"] = "MEMBER", ["name"] = "Extrusion on plane" }));
        ids.Add((string)f.Run("studio.sweep", new JsonObject { ["profileId"] = Draw(f, plane, Rect(-3, 0, -2.6, 0.3)), ["pathId"] = Draw(f, plane, Poly(false, (-3, 2), (-1, 2), (-1, 4))), ["name"] = "Sweep on plane" })!["id"]!);
        ids.Add((string)f.Run("studio.revolve", new JsonObject { ["profileId"] = Draw(f, plane, Rect(6, 0.5, 6.5, 1.5)), ["axisId"] = Draw(f, plane, new JsonObject { ["type"] = "line", ["a"] = J(5.5, 0), ["b"] = J(5.5, 2) }), ["angle"] = 180, ["name"] = "Revolution on plane" })!["id"]!);
        var plane2 = (string)f.Run("view.create", new JsonObject { ["kind"] = "plane", ["fromView"] = plane, ["offset"] = 1.5 })!["id"]!;
        var p1 = Draw(f, plane, Rect(8, 0, 9, 1));
        var p2 = Draw(f, plane2, Circle(8.5, 0.5, 0.4));
        ids.Add((string)f.Run("studio.loft", new JsonObject { ["profileId"] = p1, ["profileIds"] = new JsonArray(p1, p2), ["name"] = "Loft between planes" })!["id"]!);

        var sources = ids.Select(id =>
        {
            var e = f.Model.Get(id)!;
            var (lo, hi) = f.Bounds(id);
            return new Source(id, e.Name, lo, hi, f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid)), MeshScene.ExportClass(e),
                f.Model.RelatingOf(id, IfcClass.RelDefinesByType)?.Name, Boxes.AssemblyOf(f.Model, id)?.Id);
        }).ToList();
        Assert.Equal(15, sources.Count);
        Assert.All(sources, s => Assert.True(s.Volume > 1e-4, $"{s.Name} has a body"));
        return new Scene(f, sources, Boxes.AssemblyOf(f.Model, box)!.Id, box, bottom, top);
    }

    /// <summary>The bounds and the volume of a triangle soup (positions xyz, model coordinates), closed, by the divergence theorem.</summary>
    static (Vec3 Min, Vec3 Max, double Volume) Measure3(IReadOnlyList<double> p, IReadOnlyList<int> ix)
    {
        Vec3 lo = new(double.MaxValue, double.MaxValue, double.MaxValue), hi = new(double.MinValue, double.MinValue, double.MinValue);
        for (var i = 0; i + 2 < p.Count; i += 3)
        {
            lo = new Vec3(Math.Min(lo.X, p[i]), Math.Min(lo.Y, p[i + 1]), Math.Min(lo.Z, p[i + 2]));
            hi = new Vec3(Math.Max(hi.X, p[i]), Math.Max(hi.Y, p[i + 1]), Math.Max(hi.Z, p[i + 2]));
        }
        double v = 0;
        for (var t = 0; t + 2 < ix.Count; t += 3)
        {
            Vec3 a = new(p[ix[t] * 3], p[ix[t] * 3 + 1], p[ix[t] * 3 + 2]), b = new(p[ix[t + 1] * 3], p[ix[t + 1] * 3 + 1], p[ix[t + 1] * 3 + 2]), c = new(p[ix[t + 2] * 3], p[ix[t + 2] * 3 + 1], p[ix[t + 2] * 3 + 2]);
            v += Vec3.Dot(a, Vec3.Cross(b, c)) / 6;
        }
        return (lo, hi, Math.Abs(v));
    }

    static void Same(Source s, Vec3 lo, Vec3 hi, double volume, string where, double tol = 1e-3)
    {
        Assert.True((lo - s.Min).Length < tol && (hi - s.Max).Length < tol, $"{s.Name} in {where}: bounds {lo}–{hi}, expected {s.Min}–{s.Max}");
        Assert.True(Math.Abs(volume - s.Volume) < s.Volume * 0.005 + 1e-6, $"{s.Name} in {where}: volume {volume}, expected {s.Volume}");
    }

    static void SameInModel(Scene sc, Fixture g, string where)
    {
        foreach (var s in sc.Sources)
        {
            var e = g.Model.Get(s.Id);
            Assert.True(e != null, $"{s.Name} is missing after {where}");
            Assert.Equal(s.Class, e!.Class);
            var geo = g.Session.Geometry(s.Id);
            Assert.True(geo is { Parts.Count: > 0 }, $"{s.Name} has no body after {where}: {string.Join("; ", geo?.Diagnostics ?? [])}");
            var (lo, hi) = g.Bounds(s.Id);
            Same(s, lo, hi, geo!.Parts.Sum(p => Measure.Volume(p.Solid)), where);
        }
    }

    // ─── IFC ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("brep")]
    [InlineData("faceset")]
    public void Solids_on_every_plane_come_back_from_IFC_in_place_with_class_type_and_assembly(string geometry)
    {
        var sc = Build();
        var dir = Directory.CreateTempSubdirectory("lapin-studio-ifc").FullName;
        try
        {
            var path = Path.Combine(dir, "studio.ifc");
            var r = IfcExport.Write(sc.F.Session, path, new IfcExportOptions(Geometry: geometry));
            Assert.DoesNotContain(r.Notes, n => n.Contains("not exported"));
            var text = File.ReadAllText(path);
            // Classes and types as IFC names them; the two boxes with parts as assemblies.
            Assert.Equal(8, text.Split("=IFCWALL(").Length - 1); // seven Studio walls and the elevation's host wall
            Assert.Contains("=IFCWALLTYPE(", text);
            Assert.Contains("'Thin wall'", text);
            Assert.Contains("=IFCSLABTYPE(", text);
            Assert.Contains("'Thin slab'", text);
            Assert.Contains("=IFCMEMBER(", text);
            Assert.Equal(2, text.Split("=IFCELEMENTASSEMBLY(").Length - 1);

            var g = F();
            var result = IfcImport.Run(g.Session, path, null, out _);
            Assert.DoesNotContain(result.Notes, n => n.Contains("left out"));
            SameInModel(sc, g, "IFC " + geometry);
            // Back as references (their sketches are not in the file), or recognised from their triangles (a box's bottom, a
            // slab: decision 210), typed, grouped as they were.
            foreach (var s in sc.Sources)
            {
                var e = g.Model.Get(s.Id)!;
                Assert.True(StoredBodies.Has(e) || StoredBodies.InstanceOf(e) != null || e.Attrs["imported"] != null, $"{s.Name} is a reference or recognised");
                // A recognised element takes the type recognition makes when the file named none.
                if (s.Type != null || e.Attrs["imported"] == null) Assert.Equal(s.Type, g.Model.RelatingOf(s.Id, IfcClass.RelDefinesByType)?.Name);
                var asm = g.Model.RelatingOf(s.Id, IfcClass.RelAggregates);
                Assert.Equal(s.Assembly, asm?.Id);
                if (asm != null)
                {
                    Assert.Equal("IfcElementAssembly", asm.Class);
                    Assert.Equal(3, g.Model.RelatedOf(asm.Id, IfcClass.RelAggregates).Count());
                    Assert.NotNull(Spatial.StoreyOf(g.Model, asm.Id));
                }
                Assert.Equal(Spatial.StoreyOf(sc.F.Model, s.Id)!.Name, Spatial.StoreyOf(g.Model, s.Id)!.Name);
            }
            // The same file imported into the project it came from leaves the live solids and their assembly alone: no reference over them.
            var before = sc.F.Model.Entities.Select(e => e.Id).ToHashSet();
            IfcImport.Run(sc.F.Session, path, null, out _);
            Assert.DoesNotContain(sc.F.Model.Entities, e => !before.Contains(e.Id) && e.Class is IfcClass.Wall or IfcClass.Slab or "IfcMember" or IfcClass.BuildingElementProxy or "IfcElementAssembly");
            Assert.All(sc.Sources, s => Assert.NotNull(sc.F.Model.Get(s.Id)!.Str("operation")));
            Assert.Equal(sc.Assembly, Boxes.AssemblyOf(sc.F.Model, sc.Box)!.Id);
            Assert.Null(sc.F.Model.Get(sc.Assembly)!.Attrs["reference"]);
            // And exported again from the re-import, the assembly is written once more with its three parts.
            var again = Path.Combine(dir, "again.ifc");
            IfcExport.Write(g.Session, again);
            Assert.Equal(2, File.ReadAllText(again).Split("=IFCELEMENTASSEMBLY(").Length - 1);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ─── Fragments ────────────────────────────────────────────────────────

    [Fact]
    public void Solids_on_every_plane_come_back_from_Fragments_in_place_and_a_box_decomposes_into_its_parts()
    {
        var sc = Build();
        var scene = MeshScene.Build(sc.F.Session, new MeshExportOptions("frag", Origin: "internal"));
        var bytes = FragWriter.Write(scene);
        var m = FragReader.Read(bytes);
        // The assembly: an item without geometry, decomposed by its three parts, which decompose it.
        var asmIndex = Array.IndexOf(m.Guids, sc.Assembly);
        Assert.True(asmIndex >= 0, "the assembly has its GlobalId");
        var asmLocal = m.GuidsItems[asmIndex];
        Assert.Equal("IFCELEMENTASSEMBLY", m.Categories[Array.IndexOf(m.LocalIds, asmLocal)]);
        Assert.DoesNotContain(m.Meshes.MeshesItems, i => m.LocalIds[i] == asmLocal);
        var parts = m.RelationsOf(asmLocal)["IsDecomposedBy"];
        Assert.Equal(3, parts.Count);
        foreach (var id in new[] { sc.Box, sc.Bottom, sc.Top })
        {
            var local = m.GuidsItems[Array.IndexOf(m.Guids, id)];
            Assert.Contains(local, parts);
            Assert.Contains(asmLocal, m.RelationsOf(local)["Decomposes"]);
            Assert.NotEmpty(m.RelationsOf(local)["ContainedInStructure"]);
        }
        Assert.Equal("IFCWALL", m.Categories[Array.IndexOf(m.LocalIds, m.GuidsItems[Array.IndexOf(m.Guids, sc.Box)])]);
        Assert.Equal("IFCSLAB", m.Categories[Array.IndexOf(m.LocalIds, m.GuidsItems[Array.IndexOf(m.Guids, sc.Top)])]);
        Assert.Equal("Thin wall", (string?)m.AttributesOf(Array.IndexOf(m.LocalIds, m.GuidsItems[Array.IndexOf(m.Guids, sc.Box)]))["ObjectType"]);
        // Read back: every solid where it was, its class.
        var g = F();
        var r = FragImport.Run(g.Session, bytes, "studio.frag", out _);
        Assert.Equal(0, r.Skipped);
        SameInModel(sc, g, "Fragments");
    }

    // ─── glTF ─────────────────────────────────────────────────────────────

    static IEnumerable<JsonElement> Arr(JsonElement e, string name) => e.TryGetProperty(name, out var a) ? a.EnumerateArray() : [];

    static float[] Floats(JsonElement root, byte[] bin, int accessor, int size)
    {
        var a = Arr(root, "accessors").ElementAt(accessor);
        var v = Arr(root, "bufferViews").ElementAt(a.GetProperty("bufferView").GetInt32());
        var (offset, count) = (v.GetProperty("byteOffset").GetInt32(), a.GetProperty("count").GetInt32() * size);
        return Enumerable.Range(0, count).Select(i => BitConverter.ToSingle(bin, offset + i * 4)).ToArray();
    }

    static int[] Ints(JsonElement root, byte[] bin, int accessor)
    {
        var a = Arr(root, "accessors").ElementAt(accessor);
        var v = Arr(root, "bufferViews").ElementAt(a.GetProperty("bufferView").GetInt32());
        var offset = v.GetProperty("byteOffset").GetInt32();
        return Enumerable.Range(0, a.GetProperty("count").GetInt32()).Select(i => (int)BitConverter.ToUInt32(bin, offset + i * 4)).ToArray();
    }

    [Fact]
    public void Solids_on_every_plane_are_in_place_in_glTF_and_a_box_is_a_node_over_its_parts()
    {
        var sc = Build();
        var (bytes, _) = MeshExport.Write(sc.F.Session, new MeshExportOptions("glb", Origin: "internal"));
        var (doc, bin) = GltfWriter.ReadGlb(bytes);
        var root = doc.RootElement;
        var nodes = Arr(root, "nodes").ToList();
        int IndexOf(string gid) => nodes.FindIndex(n => n.TryGetProperty("extras", out var x) && x.TryGetProperty("globalId", out var g) && g.GetString() == gid);
        foreach (var s in sc.Sources)
        {
            var node = nodes[IndexOf(s.Id)];
            Assert.Equal(s.Class, node.GetProperty("extras").GetProperty("ifcClass").GetString());
            Assert.Equal(s.Type, node.GetProperty("extras").TryGetProperty("type", out var t) ? t.GetString() : null);
            var mesh = Arr(root, "meshes").ElementAt(node.GetProperty("mesh").GetInt32());
            var pos = new List<double>();
            var ix = new List<int>();
            foreach (var prim in Arr(mesh, "primitives").Where(p => !p.TryGetProperty("mode", out var mode) || mode.GetInt32() == 4))
            {
                var baseIndex = pos.Count / 3;
                var p = Floats(root, bin, prim.GetProperty("attributes").GetProperty("POSITION").GetInt32(), 3);
                // Y up back to Z up: the model's (x, y, z) was written (x, z, −y).
                for (var i = 0; i + 2 < p.Length; i += 3) { pos.Add(p[i]); pos.Add(-p[i + 2]); pos.Add(p[i + 1]); }
                ix.AddRange(Ints(root, bin, prim.GetProperty("indices").GetInt32()).Select(i => i + baseIndex));
            }
            var (lo, hi, vol) = Measure3(pos, ix);
            Same(s, lo, hi, vol, "glTF");
        }
        // The assembly: a node without a mesh whose children are the box and its two slabs, under the storey.
        var asm = nodes[IndexOf(sc.Assembly)];
        Assert.False(asm.TryGetProperty("mesh", out _));
        Assert.Equal("IfcElementAssembly", asm.GetProperty("extras").GetProperty("ifcClass").GetString());
        var children = Arr(asm, "children").Select(c => c.GetInt32()).ToList();
        Assert.Equal(new[] { IndexOf(sc.Box), IndexOf(sc.Bottom), IndexOf(sc.Top) }.Order(), children.Order());
        var storey = nodes.Single(n => n.TryGetProperty("extras", out var x) && x.TryGetProperty("ifcClass", out var c) && c.GetString() == "IfcBuildingStorey" && Arr(n, "children").Any(k => k.GetInt32() == IndexOf(sc.Assembly)));
        Assert.DoesNotContain(IndexOf(sc.Box), Arr(storey, "children").Select(c => c.GetInt32()));
    }

    // ─── USD ──────────────────────────────────────────────────────────────

    static List<double> Numbers(string text)
    {
        var o = new List<double>();
        foreach (var token in text.Split([' ', ',', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries))
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) o.Add(v);
        return o;
    }

    [Fact]
    public void Solids_on_every_plane_are_in_place_in_USD_and_a_box_holds_its_parts()
    {
        var sc = Build();
        var usda = Encoding.UTF8.GetString(UsdWriter.Usda(MeshScene.Build(sc.F.Session, new MeshExportOptions("usda", Origin: "internal"))));
        var lines = usda.Split('\n');
        // The Xform carrying a GlobalId: from its def line to the closing brace at the def's indent.
        (int Start, int End, int Indent) Prim(string gid)
        {
            var at = Array.FindIndex(lines, l => l.Contains($"lapin:globalId = \"{gid}\""));
            Assert.True(at > 0, $"{gid} is in the USD");
            var start = at;
            while (!lines[start].TrimStart().StartsWith("def Xform", StringComparison.Ordinal)) start--;
            var indent = lines[start].Length - lines[start].TrimStart().Length;
            var end = start + 1;
            while (!(lines[end].Length - lines[end].TrimStart().Length == indent && lines[end].Trim() == "}")) end++;
            return (start, end, indent);
        }
        foreach (var s in sc.Sources)
        {
            var (start, end, _) = Prim(s.Id);
            var body = lines[start..(end + 1)];
            Assert.Contains(body, l => l.Contains($"lapin:ifcClass = \"{s.Class}\""));
            if (s.Type != null) Assert.Contains(body, l => l.Contains($"lapin:type = \"{s.Type}\""));
            var pos = new List<double>();
            var ix = new List<int>();
            for (var i = start; i <= end; i++)
            {
                if (lines[i].TrimStart().StartsWith("point3f[] points = ", StringComparison.Ordinal))
                {
                    var baseIndex = pos.Count / 3;
                    pos.AddRange(Numbers(lines[i][lines[i].IndexOf("= ", StringComparison.Ordinal)..]));
                    // Its indices came just before (faceVertexIndices precedes points in the Mesh).
                    var idx = Array.FindLastIndex(lines, i, l => l.TrimStart().StartsWith("int[] faceVertexIndices = ", StringComparison.Ordinal));
                    ix.AddRange(Numbers(lines[idx][(lines[idx].IndexOf('[', lines[idx].IndexOf("= ", StringComparison.Ordinal)))..]).Select(v => (int)v + baseIndex));
                }
            }
            var (lo, hi, vol) = Measure3(pos, ix);
            Same(s, lo, hi, vol, "USD");
        }
        var (aStart, aEnd, aIndent) = Prim(sc.Assembly);
        Assert.Contains(lines[aStart..aEnd], l => l.Contains("lapin:ifcClass = \"IfcElementAssembly\""));
        foreach (var id in new[] { sc.Box, sc.Bottom, sc.Top })
        {
            var (start, _, indent) = Prim(id);
            Assert.True(start > aStart && start < aEnd && indent == aIndent + 4, $"{id} is an Xform inside the assembly's");
        }
    }

    // ─── COLLADA ──────────────────────────────────────────────────────────

    [Fact]
    public void Solids_on_every_plane_are_in_place_in_COLLADA_and_a_box_is_a_node_over_its_parts()
    {
        var sc = Build();
        var (bytes, _) = MeshExport.Write(sc.F.Session, new MeshExportOptions("dae", Origin: "internal"));
        var doc = XDocument.Parse(Encoding.UTF8.GetString(bytes));
        XNamespace ns = "http://www.collada.org/2005/11/COLLADASchema";
        XElement NodeOf(string gid) => doc.Descendants(ns + "node").Single(n => n.Element(ns + "extra")?.Element(ns + "technique")?.Element(ns + "globalId")?.Value == gid);
        var geometries = doc.Descendants(ns + "geometry").ToDictionary(g => (string)g.Attribute("id")!);
        foreach (var s in sc.Sources)
        {
            var node = NodeOf(s.Id);
            var tech = node.Element(ns + "extra")!.Element(ns + "technique")!;
            Assert.Equal(s.Class, tech.Element(ns + "ifcClass")!.Value);
            Assert.Equal(s.Type, tech.Element(ns + "type")?.Value);
            var url = (string)node.Element(ns + "instance_geometry")!.Attribute("url")!;
            var geometry = geometries[url[1..]];
            var positions = Numbers(geometry.Descendants(ns + "float_array").Single(a => ((string)a.Attribute("id")!).EndsWith("-positions-array", StringComparison.Ordinal)).Value);
            var ix = geometry.Descendants(ns + "triangles").SelectMany(t => Numbers(t.Element(ns + "p")!.Value)).Select(v => (int)v).ToList();
            var (lo, hi, vol) = Measure3(positions, ix);
            Same(s, lo, hi, vol, "COLLADA");
        }
        var asm = NodeOf(sc.Assembly);
        Assert.Null(asm.Element(ns + "instance_geometry"));
        Assert.Equal("IfcElementAssembly", asm.Element(ns + "extra")!.Element(ns + "technique")!.Element(ns + "ifcClass")!.Value);
        var children = asm.Elements(ns + "node").Select(n => n.Element(ns + "extra")!.Element(ns + "technique")!.Element(ns + "globalId")!.Value).ToList();
        Assert.Equal(new[] { sc.Box, sc.Bottom, sc.Top }.Order(), children.Order());
    }
}
