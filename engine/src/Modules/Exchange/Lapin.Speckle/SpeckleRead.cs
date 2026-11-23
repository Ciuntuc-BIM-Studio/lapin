using System.Collections;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Speckle.Objects.Data;
using Speckle.Objects.Geometry;
using Speckle.Objects.Other;
using Speckle.Sdk.Models;
using Speckle.Sdk.Models.Collections;
using Speckle.Sdk.Models.Instances;
using Speckle.Sdk.Models.Proxies;
using SpeckleUnits = Speckle.Sdk.Common.Units;
using SMesh = Speckle.Objects.Geometry.Mesh;

namespace Lapin.Speckle;

/// <summary>One colour's triangles of a received object, in metres, Z up: its colour, opacity and render material's name.</summary>
public sealed record ReceivedPart(string Color, double Opacity, string? Material, List<Vec3> Vertices, List<(int A, int B, int C)> Triangles);

/// <summary>A level the version names (a <c>LevelProxy</c>, or an object's own level): its name, elevation in metres, and its GlobalId when Lapin sent it.</summary>
public sealed record ReceivedLevel(string Name, double? Elevation, string? GlobalId);

/// <summary>
/// One object of a version as it comes in: its Speckle ids, name, type and the IFC class it is taken as, the collections it
/// stands in, its properties (as sets), and its geometry: its own triangles in the version's frame (metres), or an instance of
/// a definition (<see cref="Instance"/>: the definition's id and its twelve numbers), or lines only (curves, drawn in plan).
/// </summary>
public sealed record ReceivedElement(string? ApplicationId, string? ObjectId, string Name, string? SpeckleType, string IfcClass, string Path, JsonObject Properties,
    List<ReceivedPart> Parts, (string Definition, double[] Xf)? Instance, List<Plan2dItem>? Lines, ReceivedLevel? Level);

/// <summary>A definition several instances share (an <c>InstanceDefinitionProxy</c>): its triangles once, in its own frame, metres.</summary>
public sealed record ReceivedDefinition(string Id, string Name, List<ReceivedPart> Parts);

/// <summary>A version read: its objects, the definitions they share, what was left out (by Speckle type) and its notes.</summary>
public sealed record SpeckleReceived(List<ReceivedElement> Elements, Dictionary<string, ReceivedDefinition> Definitions, Dictionary<string, int> Skipped,
    List<string> Notes, string? RootApplicationId, string? Origin)
{
    public int SkippedCount => Skipped.Values.Sum();
    public int Triangles => Elements.Sum(e => e.Parts.Sum(p => p.Triangles.Count)) + Definitions.Values.Sum(d => d.Parts.Sum(p => p.Triangles.Count));
}

/// <summary>
/// A Speckle version's object tree read into what Lapin brings in (docs/plan-speckle.md, S2), no network and no model: every
/// object with geometry (a v3 <c>DataObject</c> and its kin, an older connector's typed object, a bare mesh or Brep, a block
/// instance) found under the collections, with its meshes (its <c>displayValue</c>; a Brep's too) turned into triangles in
/// metres, coloured by the version's render materials (<c>renderMaterialProxies</c>, else an object's own <c>renderMaterial</c>,
/// else its <c>colorProxies</c>), its properties kept as sets, its level from the <c>levelProxies</c> (or its own level).
/// Instances (<c>InstanceProxy</c>) of a definition (<c>InstanceDefinitionProxy</c>) share the definition's triangles; nested
/// ones are baked into their definition. Curves (lines, polylines, arcs, circles, polycurves, a NURBS curve's polyline) are
/// kept as plan lines when the object has nothing else. Anything else (points, text, objects with no geometry) is counted
/// as left out, by its Speckle type.
/// </summary>
public static class SpeckleRead
{
    /// <summary>The version's own lists beside its objects: read for what they say, never taken as objects.</summary>
    static readonly HashSet<string> ProxyLists = ["renderMaterialProxies", "levelProxies", "colorProxies", "groupProxies", "instanceDefinitionProxies", "definitionGeometry"];

    /// <summary>What an object's members hold that is never a property: its geometry and the SDK's own.</summary>
    static readonly HashSet<string> NotProperties = ["displayValue", "@displayValue", "elements", "@elements", "renderMaterial", "@renderMaterial", "bbox", "vertices", "faces",
        "colors", "textureCoordinates", "vertexNormals", "id", "applicationId", "speckle_type", "totalChildrenCount", "transform", "definitionId", "maxDepth", "units",
        "properties", "name", "value", "start", "end", "domain", "segments", "plane", "Surfaces", "Curve3D", "Curve2D", "Vertices", "Edges", "Loops", "Trims", "Faces",
        "SurfacesValue", "Curve3DValues", "Curve2DValues", "VerticesValue", "EdgesValue", "LoopsValue", "TrimsValue", "FacesValue", "level", "__closure", "__parents"];

    const int MaxDepth = 64;
    const string Grey = "#c9ced6";

    public static SpeckleReceived Read(Base root, CancellationToken ct = default) => new Reader(root, ct).Run();

    /// <summary>A Speckle unit's metres (millimetres 0.001, feet 0.3048); metres when it names none Speckle knows.</summary>
    public static double Metres(string? units, List<string>? notes = null)
    {
        if (string.IsNullOrWhiteSpace(units) || units == SpeckleUnits.None) return 1;
        try { return SpeckleUnits.GetConversionFactor(units, SpeckleUnits.Meters); }
        catch (Exception)
        {
            notes?.Add($"„{units}” is not a unit Speckle knows: read as metres.");
            return 1;
        }
    }

    /// <summary>
    /// An instance's transform (Speckle's 4×4: column vectors, its translation in its last column, in <paramref name="units"/>)
    /// as a stored body's twelve numbers: the images of X, Y, Z and of the origin, in metres. A matrix written the other way
    /// (its translation in its last row) is read so too.
    /// </summary>
    public static double[] Xf(global::Speckle.DoubleNumerics.Matrix4x4 m, double metres)
    {
        var rowMajorTranslation = m.M14 == 0 && m.M24 == 0 && m.M34 == 0 && (m.M41 != 0 || m.M42 != 0 || m.M43 != 0);
        return rowMajorTranslation
            ? [m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33, m.M41 * metres, m.M42 * metres, m.M43 * metres]
            : [m.M11, m.M21, m.M31, m.M12, m.M22, m.M32, m.M13, m.M23, m.M33, m.M14 * metres, m.M24 * metres, m.M34 * metres];
    }

    /// <summary><paramref name="outer"/> after <paramref name="inner"/>: a nested instance placed in its definition, then the definition placed.</summary>
    public static double[] Compose(double[] outer, double[] inner)
    {
        Vec3 Dir(double x, double y, double z) => new(outer[0] * x + outer[3] * y + outer[6] * z, outer[1] * x + outer[4] * y + outer[7] * z, outer[2] * x + outer[5] * y + outer[8] * z);
        var ax = Dir(inner[0], inner[1], inner[2]);
        var ay = Dir(inner[3], inner[4], inner[5]);
        var az = Dir(inner[6], inner[7], inner[8]);
        var o = StoredBodies.Apply(outer, new Vec3(inner[9], inner[10], inner[11]));
        return [ax.X, ax.Y, ax.Z, ay.X, ay.Y, ay.Z, az.X, az.Y, az.Z, o.X, o.Y, o.Z];
    }

    static readonly Regex IfcName = new("^Ifc[A-Z][A-Za-z]+$", RegexOptions.Compiled);

    /// <summary>What an IFC class read from the version may be as a reference: never the spatial structure, an opening, a space or a type.</summary>
    static readonly HashSet<string> NotAReference = [IfcClass.Project, IfcClass.Site, IfcClass.Building, IfcClass.BuildingStorey, IfcClass.OpeningElement, IfcClass.Space,
        IfcClass.Grid, IfcClass.Annotation, "IfcGroup", "IfcZone", "IfcVirtualElement", "IfcVoidingFeature", StoredBodies.SharedClass];

    /// <summary>Revit's categories (as its connectors name them) as IFC classes; the rest a building element proxy.</summary>
    static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Walls"] = IfcClass.Wall, ["Floors"] = IfcClass.Slab, ["Roofs"] = IfcClass.Roof, ["Doors"] = IfcClass.Door, ["Windows"] = IfcClass.Window,
        ["Columns"] = IfcClass.Column, ["Structural Columns"] = IfcClass.Column, ["Structural Framing"] = IfcClass.Beam, ["Structural Foundations"] = IfcClass.Footing,
        ["Stairs"] = IfcClass.Stair, ["Ramps"] = IfcClass.Ramp, ["Railings"] = "IfcRailing", ["Ceilings"] = "IfcCovering", ["Furniture"] = "IfcFurniture",
        ["Curtain Panels"] = "IfcPlate", ["Curtain Wall Mullions"] = "IfcMember", ["Plumbing Fixtures"] = "IfcSanitaryTerminal", ["Topography"] = IfcClass.GeographicElement,
        ["Toposolid"] = IfcClass.GeographicElement,
    };

    /// <summary>The IFC class an object is taken as: the one it names (Lapin's "IFC class", an IFC connector's type), its Revit category's, else a proxy.</summary>
    internal static string ClassOf(Base b, IReadOnlyDictionary<string, object?>? properties, IReadOnlyList<string> path)
    {
        string? Text(object? v) => v as string;
        foreach (var key in new[] { "IFC class", "ifcType", "IfcType", "ifc_type", "ifcClass" })
        {
            var named = Text(properties?.GetValueOrDefault(key)) ?? Text(Member(b, key));
            if (named != null && IfcName.IsMatch(named)) return NotAReference.Contains(named) ? IfcClass.BuildingElementProxy : named;
            if (named != null && named.StartsWith("IFC", StringComparison.Ordinal) && named.Length > 3)
            {
                var cls = "Ifc" + char.ToUpperInvariant(named[3]) + named[4..].ToLowerInvariant();
                if (Categories.Values.Contains(cls) || cls == IfcClass.Wall) return cls;
            }
        }
        var category = Text(properties?.GetValueOrDefault("category")) ?? Text(Member(b, "category"));
        if (category != null && Categories.TryGetValue(category, out var c)) return c;
        // Lapin's own send puts each class in a collection named by it.
        if (path.Count > 0 && IfcName.IsMatch(path[^1]) && !NotAReference.Contains(path[^1])) return path[^1];
        return IfcClass.BuildingElementProxy;
    }

    /// <summary>A member by name, typed or dynamic, or null (never the SDK's exception for a name it does not hold).</summary>
    internal static object? Member(Base b, string key)
    {
        try { return b[key]; } catch (Exception) { return null; }
    }

    static IEnumerable<Base> Bases(object? v) => v switch
    {
        null => [],
        Base b => [b],
        string => [],
        IEnumerable e => e.Cast<object?>().SelectMany(Bases),
        _ => [],
    };

    sealed class Reader(Base root, CancellationToken ct)
    {
        readonly List<ReceivedElement> elements = [];
        readonly Dictionary<string, ReceivedDefinition> definitions = [];
        readonly Dictionary<string, int> skipped = [];
        readonly List<string> notes = [];
        /// <summary>Every object of the version with an applicationId: what definitions and proxies name.</summary>
        readonly Dictionary<string, Base> byApp = [];
        readonly Dictionary<string, InstanceDefinitionProxy> defProxies = [];
        /// <summary>The objects a definition holds: drawn through its instances, never as objects of their own.</summary>
        readonly HashSet<string> defMembers = [];
        readonly Dictionary<string, RenderMaterial> materialOf = [];
        readonly Dictionary<string, int> colourOf = [];
        readonly Dictionary<string, ReceivedLevel> levelOf = [];
        readonly Dictionary<string, ReceivedLevel> levelByName = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> unitNotes = [];
        int lostFaces;

        void Skip(string what, int n = 1) => skipped[what] = skipped.GetValueOrDefault(what) + n;

        public SpeckleReceived Run()
        {
            SpeckleSdk.Ensure();
            Index(root, 0);
            foreach (var p in Bases(Member(root, "instanceDefinitionProxies")).OfType<InstanceDefinitionProxy>())
            {
                if (p.applicationId is not { Length: > 0 } id) continue;
                defProxies[id] = p;
                foreach (var o in p.objects ?? []) defMembers.Add(o);
            }
            foreach (var p in Bases(Member(root, "renderMaterialProxies")).OfType<RenderMaterialProxy>())
                if (p.value != null) foreach (var o in p.objects ?? []) materialOf.TryAdd(o, p.value);
            foreach (var p in Bases(Member(root, "colorProxies")).OfType<ColorProxy>())
                foreach (var o in p.objects ?? []) colourOf.TryAdd(o, p.value);
            foreach (var p in Bases(Member(root, "levelProxies")).OfType<LevelProxy>())
            {
                if (LevelOfProxy(p) is not { } level) continue;
                levelByName.TryAdd(level.Name, level);
                foreach (var o in p.objects ?? []) levelOf.TryAdd(o, level);
            }
            Walk(root, [], 0, isRoot: true);
            if (lostFaces > 0) notes.Add($"{lostFaces} face(s) naming vertices a mesh does not have were left out.");
            var rootProps = Member(root, "properties") as IDictionary<string, object?>;
            return new SpeckleReceived(elements, definitions, skipped, notes, root.applicationId, (rootProps != null && rootProps.TryGetValue("origin", out var og) ? og as string : null));
        }

        /// <summary>Every object by its applicationId, wherever it stands (definitions keep theirs outside the collections).</summary>
        void Index(Base b, int depth)
        {
            if (depth > MaxDepth) return;
            if (b.applicationId is { Length: > 0 } id) byApp.TryAdd(id, b);
            foreach (var (key, value) in Members(b))
            {
                if (key is "renderMaterialProxies" or "levelProxies" or "colorProxies" or "groupProxies" or "instanceDefinitionProxies") continue;
                if (value is string || value is IDictionary) continue;
                foreach (var child in Bases(value)) Index(child, depth + 1);
            }
        }

        static IEnumerable<KeyValuePair<string, object?>> Members(Base b)
        {
            Dictionary<string, object?> all;
            try { all = b.GetMembers(DynamicBaseMemberType.Instance | DynamicBaseMemberType.Dynamic)!; }
            catch (Exception) { yield break; }
            foreach (var kv in all) yield return kv;
        }

        ReceivedLevel? LevelOfProxy(LevelProxy p)
        {
            var v = p.value;
            if (v == null) return null;
            var props = v.properties ?? [];
            var units = props.GetValueOrDefault("units") as string ?? Member(v, "units") as string;
            var elevation = Number(props.GetValueOrDefault("elevation")) ?? Number(Member(v, "elevation"));
            var name = v.name is { Length: > 0 } n ? n : "Level";
            return new ReceivedLevel(name, elevation * (units != null ? Metres(units, notes) : ModelMetres()), props.GetValueOrDefault("GlobalId") as string);
        }

        double? modelMetres;
        /// <summary>The version's unit where a level names none: the first mesh's.</summary>
        double ModelMetres() => modelMetres ??= Metres(FirstMeshUnits(root, 0));

        static string? FirstMeshUnits(Base b, int depth)
        {
            if (depth > MaxDepth) return null;
            if (b is SMesh m) return m.units;
            foreach (var (_, v) in Members(b))
                foreach (var c in Bases(v)) if (FirstMeshUnits(c, depth + 1) is { } u) return u;
            return null;
        }

        static double? Number(object? v) => v switch
        {
            double d => d, float f => f, int i => i, long l => l, decimal m => (double)m,
            IDictionary<string, object?> d when d.TryGetValue("value", out var x) => Number(x),
            _ => null,
        };

        void Walk(Base b, List<string> path, int depth, bool isRoot = false)
        {
            if (depth > MaxDepth) return;
            ct.ThrowIfCancellationRequested();
            if (b.applicationId is { } aid && defMembers.Contains(aid)) return;
            if (b is Collection c)
            {
                var inner = isRoot ? path : [.. path, c.name ?? ""];
                foreach (var e in c.elements ?? []) if (e != null) Walk(e, inner, depth + 1);
                // A version's own lists (proxies, definitions) beside the root's elements are read above, never walked.
                if (!isRoot) foreach (var (key, value) in Members(b))
                    if (key.StartsWith('@') && !ProxyLists.Contains(key.TrimStart('@'))) foreach (var x in Bases(value)) Walk(x, inner, depth + 1);
                return;
            }
            if (b is LevelProxy or RenderMaterialProxy or ColorProxy or InstanceDefinitionProxy or GroupProxy or RenderMaterial) return;
            if (Geometric(b))
            {
                Element(b, path);
                // An object's own parts (a Revit wall's hosted elements, an assembly's parts) come in beside it.
                foreach (var key in new[] { "elements", "@elements" })
                    foreach (var child in Bases(Member(b, key))) Walk(child, path, depth + 1);
                return;
            }
            // A container of an older connector (a commit's root holding "@Walls", "@Floors"…): its lists walked under their names.
            var any = false;
            foreach (var (key, value) in Members(b))
            {
                if (value is string || NotProperties.Contains(key) && key is not ("elements" or "@elements") || ProxyLists.Contains(key.TrimStart('@'))) continue;
                var children = Bases(value).ToList();
                if (children.Count == 0) continue;
                any = true;
                var inner = isRoot || key is "elements" or "@elements" ? path : [.. path, key.TrimStart('@')];
                foreach (var child in children) Walk(child, inner, depth + 1);
            }
            if (!any && !isRoot) Skip(ShortType(b));
        }

        static string ShortType(Base b)
        {
            var t = b.speckle_type ?? b.GetType().Name;
            var last = t.Split(':')[^1];
            return last.Split('.')[^1];
        }

        static bool Geometric(Base b) => b is SMesh or InstanceProxy or Line or Polyline or Curve or Polycurve or Arc or Circle
            || Bases(Member(b, "displayValue")).Any() || Bases(Member(b, "@displayValue")).Any();

        /// <summary>An object's geometry gathered: triangles, the instances it shows, and its curves.</summary>
        sealed class Gathered
        {
            public readonly List<ReceivedPart> Parts = [];
            public readonly List<(string Def, double[] Xf)> Instances = [];
            public readonly List<List<Vec3>> Curves = [];
            public readonly HashSet<string> Left = [];
        }

        void Element(Base b, List<string> path)
        {
            var g = new Gathered();
            var owners = new List<string?> { b.applicationId };
            Gather(b, g, owners, 0, top: true);
            var name = Name(b);
            var props = b is DataObject d ? d.properties : Member(b, "properties") as Dictionary<string, object?>;
            var cls = ClassOf(b, props, path);
            var sets = Sets(b, props, path);

            ReceivedElement Make(List<ReceivedPart> parts, (string, double[])? instance, List<Plan2dItem>? lines) =>
                new(b.applicationId, b.id, name, b.speckle_type, cls, string.Join(" / ", path), sets, parts, instance, lines, LevelOf(b));

            foreach (var l in g.Left) Skip(l);
            if (g.Parts.Count == 0 && g.Instances.Count == 1)
            {
                // Its definition read once, for every instance of it.
                Definition(g.Instances[0].Def, 0);
                elements.Add(Make([], g.Instances[0], null));
                return;
            }
            // Instances among other geometry: baked into the object's own triangles.
            foreach (var (def, xf) in g.Instances)
                if (Definition(def, 0) is { } dd)
                    foreach (var p in dd.Parts) g.Parts.Add(p with { Vertices = p.Vertices.Select(v => StoredBodies.Apply(xf, v)).ToList() });
            if (g.Parts.Count > 0)
            {
                if (g.Curves.Count > 0) Skip("Curve (beside a body)", g.Curves.Count);
                elements.Add(Make(g.Parts, null, null));
                return;
            }
            if (g.Curves.Count > 0)
            {
                var colour = ColourOf(owners, null) is var (c, _, _) ? c : null;
                elements.Add(Make([], null, g.Curves.Where(x => x.Count >= 2).Select(x => new Plan2dItem(Plan2dItem.Line, false, [x.Select(p => p.Xy).ToList()], colour)).ToList()));
                return;
            }
            Skip(ShortType(b));
        }

        string Name(Base b)
        {
            if (b is DataObject { name: { Length: > 0 } n }) return n;
            foreach (var key in new[] { "name", "Name", "family", "type", "category" })
                if (Member(b, key) is string { Length: > 0 } s) return s;
            if (b is InstanceProxy ip && defProxies.TryGetValue(ip.definitionId ?? "", out var def) && def.name is { Length: > 0 } dn) return dn;
            return ShortType(b);
        }

        ReceivedLevel? LevelOf(Base b)
        {
            if (b.applicationId is { } id && levelOf.TryGetValue(id, out var l)) return l;
            switch (Member(b, "level"))
            {
                case string s when levelByName.TryGetValue(s, out var byName): return byName;
                case string s: return new ReceivedLevel(s, null, null);
                case Base lb:
                    var units = Member(lb, "units") as string;
                    var name = Member(lb, "name") as string ?? "Level";
                    var elevation = Number(Member(lb, "elevation"));
                    return levelByName.TryGetValue(name, out var known) ? known : new ReceivedLevel(name, elevation * (units != null ? Metres(units, notes) : ModelMetres()), null);
            }
            return null;
        }

        /// <summary>
        /// An object's geometry: a mesh as itself, an instance by its definition, a curve as points; an object by its
        /// <c>displayValue</c> (a Brep's, a data object's), and what that holds in turn. <paramref name="owners"/>: the
        /// applicationIds a material may be given by, the nearest first.
        /// </summary>
        void Gather(Base b, Gathered g, List<string?> owners, int depth, bool top = false)
        {
            if (depth > MaxDepth) return;
            switch (b)
            {
                case SMesh m:
                    if (MeshPart(m, owners) is { } p) g.Parts.Add(p);
                    return;
                case InstanceProxy ip:
                    if (ip.definitionId is { Length: > 0 } def && defProxies.ContainsKey(def))
                    {
                        double[] xf;
                        try { xf = Xf(ip.transform, Metres(ip.units, notes)); }
                        catch (Exception) { g.Left.Add("InstanceProxy (no transform)"); return; }
                        g.Instances.Add((def, xf));
                    }
                    else g.Left.Add("InstanceProxy (no definition)");
                    return;
                case Line or Polyline or Curve or Polycurve or Arc or Circle:
                    if (Points(b, depth) is { Count: >= 2 } pts) g.Curves.Add(pts);
                    else g.Left.Add(ShortType(b));
                    return;
            }
            var display = Bases(Member(b, "displayValue")).Concat(Bases(Member(b, "@displayValue"))).ToList();
            if (display.Count == 0)
            {
                if (!top) g.Left.Add(ShortType(b));
                return;
            }
            var inner = b.applicationId is { } id && !top ? [id, .. owners] : owners;
            // An object's own material (an older connector's) when its meshes have none.
            if (Member(b, "renderMaterial") is RenderMaterial own && b.applicationId is { } oid) materialOf.TryAdd(oid, own);
            foreach (var x in display) Gather(x, g, inner, depth + 1);
        }

        /// <summary>A Speckle mesh as triangles in metres: its faces ([n, i1…in], n 0 or 1 meaning 3 or 4 in old meshes) fanned.</summary>
        ReceivedPart? MeshPart(SMesh m, List<string?> owners)
        {
            var units = m.units;
            if (string.IsNullOrEmpty(units) && unitNotes.Add("mesh without units")) notes.Add("Some meshes name no unit: read as metres.");
            var k = Metres(units, notes);
            var vs = m.vertices ?? [];
            var n = vs.Count / 3;
            if (n < 3) return null;
            var vertices = new List<Vec3>(n);
            for (var i = 0; i < n; i++) vertices.Add(new Vec3(vs[3 * i] * k, vs[3 * i + 1] * k, vs[3 * i + 2] * k));
            var tris = new List<(int, int, int)>();
            var f = m.faces ?? [];
            for (var i = 0; i < f.Count;)
            {
                var count = f[i] switch { 0 => 3, 1 => 4, var c => c };
                if (count < 3 || i + count >= f.Count) { lostFaces++; break; }
                var ok = true;
                for (var j = 1; j <= count; j++) if (f[i + j] < 0 || f[i + j] >= n) ok = false;
                if (ok) for (var j = 2; j < count; j++) tris.Add((f[i + 1], f[i + j], f[i + j + 1]));
                else lostFaces++;
                i += count + 1;
            }
            if (tris.Count == 0) return null;
            var (colour, opacity, material) = ColourOf([m.applicationId, .. owners], m) ?? (Grey, 1.0, null);
            return new ReceivedPart(colour, opacity, material, vertices, tris);
        }

        /// <summary>The colour a mesh takes: its own render material, the nearest owner's, an older connector's on the mesh, else a colour proxy's.</summary>
        (string Colour, double Opacity, string? Material)? ColourOf(IEnumerable<string?> ids, SMesh? mesh)
        {
            var list = ids.OfType<string>().ToList();
            foreach (var id in list)
                if (materialOf.TryGetValue(id, out var rm)) return Of(rm);
            if (mesh != null && Member(mesh, "renderMaterial") is RenderMaterial own) return Of(own);
            foreach (var id in list)
                if (colourOf.TryGetValue(id, out var argb)) return (Hex(argb), ((argb >> 24) & 0xFF) is var a && a > 0 ? a / 255.0 : 1, null);
            return null;
            static (string, double, string?) Of(RenderMaterial rm) => (Hex(rm.diffuse), Math.Clamp(rm.opacity, 0, 1), rm.name);
        }

        static string Hex(int argb) => $"#{(argb >> 16) & 0xFF:x2}{(argb >> 8) & 0xFF:x2}{argb & 0xFF:x2}";

        /// <summary>A definition's triangles in its own frame, once; the instances it holds baked in (to a depth of 8).</summary>
        ReceivedDefinition? Definition(string id, int depth)
        {
            if (definitions.TryGetValue(id, out var known)) return known;
            if (depth > 8 || !defProxies.TryGetValue(id, out var proxy)) return null;
            var g = new Gathered();
            foreach (var o in proxy.objects ?? [])
                if (byApp.TryGetValue(o, out var obj)) Gather(obj, g, [obj.applicationId, id], 0);
            foreach (var (inner, xf) in g.Instances)
                if (Definition(inner, depth + 1) is { } d)
                    foreach (var p in d.Parts) g.Parts.Add(p with { Vertices = p.Vertices.Select(v => StoredBodies.Apply(xf, v)).ToList() });
            if (g.Curves.Count > 0) Skip("Curve (in a definition)", g.Curves.Count);
            var made = new ReceivedDefinition(id, proxy.name is { Length: > 0 } n ? n : "Definition", g.Parts);
            definitions[id] = made;
            return made;
        }

        /// <summary>A curve's points in metres: a line's ends, a polyline's points, a NURBS curve's display polyline, an arc or a circle in Lapin's chords.</summary>
        List<Vec3>? Points(Base b, int depth)
        {
            if (depth > MaxDepth) return null;
            switch (b)
            {
                case Line l when l.start != null && l.end != null:
                    return [P(l.start, l.units), P(l.end, l.units)];
                case Polyline pl when pl.value is { Count: >= 6 } v:
                {
                    var k = Metres(pl.units, notes);
                    var pts = new List<Vec3>();
                    for (var i = 0; i + 2 < v.Count; i += 3) pts.Add(new Vec3(v[i] * k, v[i + 1] * k, v[i + 2] * k));
                    if (pl.closed && pts.Count > 2) pts.Add(pts[0]);
                    return pts;
                }
                case Curve c when c.displayValue != null:
                    return Points(c.displayValue, depth + 1);
                case Polycurve pc:
                {
                    var pts = new List<Vec3>();
                    foreach (var s in pc.segments ?? [])
                        if (s is Base sb && Points(sb, depth + 1) is { } part)
                            pts.AddRange(pts.Count > 0 && Vec3.DistanceSquared(pts[^1], part[0]) < 1e-12 ? part.Skip(1) : part);
                    return pts;
                }
                case Arc a when a.startPoint != null && a.midPoint != null && a.endPoint != null:
                    return ArcThrough(P(a.startPoint, a.units), P(a.midPoint, a.units), P(a.endPoint, a.units));
                case Circle ci when ci.plane is { origin: not null, xdir: not null, ydir: not null } pl:
                {
                    var k = Metres(ci.units, notes);
                    var o = P(pl.origin, ci.units);
                    var x = new Vec3(pl.xdir.x, pl.xdir.y, pl.xdir.z).Normalized();
                    var y = new Vec3(pl.ydir.x, pl.ydir.y, pl.ydir.z).Normalized();
                    var r = ci.radius * k;
                    var n = Chords.Circle(r);
                    var pts = new List<Vec3>(n + 1);
                    for (var i = 0; i <= n; i++) { var t = 2 * Math.PI * i / n; pts.Add(o + x * (r * Math.Cos(t)) + y * (r * Math.Sin(t))); }
                    return pts;
                }
            }
            return null;
        }

        Vec3 P(global::Speckle.Objects.Geometry.Point p, string? fallbackUnits)
        {
            var k = Metres(p.units ?? fallbackUnits, notes);
            return new Vec3(p.x * k, p.y * k, p.z * k);
        }

        /// <summary>The arc through three points in Lapin's chords (<see cref="Chords"/>), or the three points when they are in line.</summary>
        static List<Vec3> ArcThrough(Vec3 a, Vec3 m, Vec3 b)
        {
            var ab = m - a;
            var ac = b - a;
            var n = Vec3.Cross(ab, ac);
            var nn = Vec3.Dot(n, n);
            if (nn < 1e-18) return [a, m, b];
            var centre = a + (Vec3.Cross(n, ab) * Vec3.Dot(ac, ac) + Vec3.Cross(ac, n) * Vec3.Dot(ab, ab)) * (1 / (2 * nn));
            var r = (a - centre).Length;
            var u = (a - centre).Normalized();
            var axis = n.Normalized();
            var v = Vec3.Cross(axis, u);
            double Angle(Vec3 p) { var d = p - centre; var t = Math.Atan2(Vec3.Dot(d, v), Vec3.Dot(d, u)); return t < 0 ? t + 2 * Math.PI : t; }
            var sweep = Angle(b);
            var count = Chords.Count(r, sweep);
            var pts = new List<Vec3>(count + 1);
            for (var i = 0; i <= count; i++) { var t = sweep * i / count; pts.Add(centre + u * (r * Math.Cos(t)) + v * (r * Math.Sin(t))); }
            return pts;
        }

        /// <summary>
        /// An object's properties as Lapin's property sets: <c>Speckle</c> holds its ids, type, collections and plain values; each
        /// group of its properties (Revit's "Parameters", Lapin's "Property sets"…) a set named "Speckle · group", deeper levels
        /// in an item's name ("Dimensions › Length"); a parameter written {value, units} as its value. Never a name starting
        /// with Pset_ (IFC keeps it for its standard sets).
        /// </summary>
        JsonObject Sets(Base b, IDictionary<string, object?>? props, List<string> path)
        {
            var sets = new JsonObject();
            var main = new JsonObject();
            if (b.applicationId != null) main["applicationId"] = b.applicationId;
            if (b.id != null) main["Speckle id"] = b.id;
            if (b.speckle_type != null) main["Speckle type"] = b.speckle_type;
            if (path.Count > 0) main["Collection"] = string.Join(" / ", path);
            sets["Speckle"] = main;
            void Add(string key, object? value)
            {
                if (Plain(value) is { } plain) { main[key] = plain; return; }
                var set = new JsonObject();
                Flatten(set, "", value, 0);
                if (set.Count > 0) sets[SetName(key, sets)] = set;
            }
            foreach (var (key, value) in props ?? new Dictionary<string, object?>()) Add(key, value);
            foreach (var (key, value) in Members(b))
            {
                if (NotProperties.Contains(key) || ProxyLists.Contains(key.TrimStart('@')) || key.StartsWith("__", StringComparison.Ordinal)) continue;
                if (value is Base vb && Geometric(vb) || value is IEnumerable e and not string and not IDictionary && Bases(e).Any(Geometric)) continue;
                Add(key.TrimStart('@'), value);
            }
            return sets;
        }

        static string SetName(string key, JsonObject sets)
        {
            var name = $"Speckle · {key}";
            for (var i = 2; sets.ContainsKey(name); i++) name = $"Speckle · {key} ({i})";
            return name;
        }

        /// <summary>A value as a plain JSON one (text, number, yes/no; a list of plain values as text), or null when it is a group.</summary>
        static JsonNode? Plain(object? v) => v switch
        {
            null => null,
            string s => s,
            bool x => x,
            int i => i,
            long l => l,
            double d when double.IsFinite(d) => Math.Round(d, 9),
            float f when float.IsFinite(f) => Math.Round(f, 6),
            decimal m => (double)m,
            IDictionary<string, object?> d when d.TryGetValue("value", out var x) && Plain(x) is { } pv && d.Keys.All(k => k is "value" or "units" or "name" or "internalDefinitionName" or "id" or "applicationId" or "speckle_type") => pv,
            IEnumerable e and not IDictionary and not Base when e.Cast<object?>().ToList() is var list && list.All(x => x is null or string or bool or int or long or double or float) =>
                string.Join(", ", list.Select(x => x is double dd ? Math.Round(dd, 9).ToString(System.Globalization.CultureInfo.InvariantCulture) : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture))),
            _ => null,
        };

        static void Flatten(JsonObject set, string prefix, object? v, int depth)
        {
            if (depth > 8 || set.Count > 2000) return;
            IEnumerable<KeyValuePair<string, object?>> pairs = v switch
            {
                IDictionary<string, object?> d => d,
                Base b => Members(b).Where(kv => !NotProperties.Contains(kv.Key) || kv.Key == "name"),
                IEnumerable e and not string => e.Cast<object?>().Select((x, i) => new KeyValuePair<string, object?>((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), x)),
                _ => [],
            };
            foreach (var (k, x) in pairs)
            {
                if (k is "speckle_type" or "id" or "applicationId" or "totalChildrenCount") continue;
                var name = prefix.Length == 0 ? k : $"{prefix} › {k}";
                if (Plain(x) is { } plain)
                {
                    // A parameter's units beside its name: "Length (m)".
                    var units = x is IDictionary<string, object?> pd && pd.TryGetValue("units", out var pu) && pu is string { Length: > 0 } u ? $" ({u})" : "";
                    set[name + units] = plain;
                }
                else if (x != null) Flatten(set, name, x, depth + 1);
            }
        }
    }
}
