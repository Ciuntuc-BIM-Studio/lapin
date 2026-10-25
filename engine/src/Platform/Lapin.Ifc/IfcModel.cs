using Lapin.Geometry;

namespace Lapin.Ifc;

/// <summary>A rigid placement (with scale, for mapped items): p ↦ O + X·p.x + Y·p.y + Z·p.z.</summary>
public readonly record struct Xf(Vec3 X, Vec3 Y, Vec3 Z, Vec3 O)
{
    public static readonly Xf Identity = new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), Vec3.Zero);
    public Vec3 Apply(Vec3 p) => O + X * p.X + Y * p.Y + Z * p.Z;
    public Vec3 Dir(Vec3 d) => X * d.X + Y * d.Y + Z * d.Z;
    /// <summary>This after <paramref name="inner"/>: inner's result placed by this.</summary>
    public Xf Then(Xf inner) => new(Dir(inner.X), Dir(inner.Y), Dir(inner.Z), Apply(inner.O));
}

/// <summary>A triangle mesh being built: vertices shared within one item, so it closes into a solid.</summary>
public sealed class MeshBuilder
{
    public List<Vec3> V { get; } = [];
    public List<(int A, int B, int C)> T { get; } = [];

    public int Add(Vec3 p) { V.Add(p); return V.Count - 1; }

    /// <summary>A planar polygon (with holes) by vertex indices, triangulated about its normal.</summary>
    public void Polygon(int[] outer, int[][] holes)
    {
        if (outer.Length < 3) return;
        var n = VecMath.NewellNormal([.. outer.Select(i => V[i])]);
        if (n.Length < 1e-12) return;
        foreach (var t in Triangulator.Face(V, outer, holes.Where(h => h.Length >= 3).ToArray(), n.Normalized())) T.Add(t);
    }

    public void Transform(Xf xf) { for (var i = 0; i < V.Count; i++) V[i] = xf.Apply(V[i]); }

    /// <summary>
    /// Vertices at the same place (within 0,01 mm) made one, and the triangles
    /// left without area dropped: a shell written with a point per face (some
    /// programs do) closes into a solid this way.
    /// </summary>
    public MeshBuilder Welded()
    {
        var o = new MeshBuilder();
        var at = new Dictionary<(long, long, long), int>();
        var map = V.Select(p =>
        {
            var k = ((long)Math.Round(p.X * 1e5), (long)Math.Round(p.Y * 1e5), (long)Math.Round(p.Z * 1e5));
            return at.TryGetValue(k, out var i) ? i : at[k] = o.Add(p);
        }).ToArray();
        foreach (var (a, b, c) in T)
            if (map[a] != map[b] && map[b] != map[c] && map[a] != map[c]) o.T.Add((map[a], map[b], map[c]));
        return o;
    }

    /// <summary>Closed: every edge between exactly two triangles (a surface, open, is drawn but not a solid).</summary>
    public bool Closed()
    {
        var count = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in T)
            foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                count[p < q ? (p, q) : (q, p)] = count.GetValueOrDefault(p < q ? (p, q) : (q, p)) + 1;
        return T.Count >= 4 && count.Values.All(n => n == 2);
    }

    /// <summary>A solid's faces as triangles (its vertices kept).</summary>
    public static MeshBuilder FromSolid(Solid s)
    {
        var m = new MeshBuilder();
        m.V.AddRange(s.Vertices);
        foreach (var f in s.Faces) m.T.AddRange(Triangulator.Face(s.Vertices, f.Outer, f.Holes, f.Normal));
        return m;
    }

    public void Append(MeshBuilder m)
    {
        var b = V.Count;
        V.AddRange(m.V);
        T.AddRange(m.T.Select(t => (t.A + b, t.B + b, t.C + b)));
    }
}

/// <summary>
/// A read IFC file (IFC2X3 or IFC4) understood: its units, the placements and
/// shapes of its products as triangle meshes in metres, what contains them
/// (their storey), their property sets and quantities, their colours and the
/// openings that void them. Geometry is evaluated on demand and kept.
/// </summary>
public sealed partial class IfcModel
{
    public StepFile File { get; }
    public string ProjectName { get; }
    readonly double _len = 1;   // metres per length unit
    readonly double _angle = 1; // radians per plane angle unit
    readonly Dictionary<int, Xf> _placements = [];
    readonly Dictionary<int, (string? Name, double Elevation)> _container = []; // product → its storey
    readonly Dictionary<int, List<int>> _openings = [];                          // element → its openings
    readonly Dictionary<int, List<int>> _definitions = [];                       // product → its property sets / quantities
    readonly Dictionary<int, string> _colours = [];                              // representation item → #rrggbb

    public sealed record Product(int Id, string Class, string GlobalId, string? Name, string? ObjectType, string? Tag, string? Storey, double Elevation, Vec3 At);

    public List<Product> Products { get; } = [];

    /// <summary>Metres per length unit of the file.</summary>
    public double Scale => _len;

    /// <summary>The file's storeys, from the lowest: name and elevation (m).</summary>
    public List<(string Name, double Elevation)> Storeys { get; } = [];

    static readonly HashSet<string> NotElements = ["IFCOPENINGELEMENT", "IFCSITE", "IFCBUILDING", "IFCBUILDINGSTOREY", "IFCANNOTATION", "IFCGRID", "IFCVIRTUALELEMENT", "IFCPROJECT"];

    public IfcModel(StepFile file)
    {
        File = file;
        var project = file.OfType("IFCPROJECT").FirstOrDefault();
        ProjectName = Str(project?[2]) ?? Str(project?[5]) ?? "";
        (_len, _angle) = Units(project);

        foreach (var r in file.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE"))
        {
            var s = file.Get(r[5]);
            var storey = s == null ? (null, 0.0) : (Str(s[2]), s.Type == "IFCBUILDINGSTOREY" ? Num(s[9]) * _len : 0);
            foreach (var e in Refs(r[4])) _container[e] = storey;
        }
        // Spaces are aggregated into their storey; parts aggregated into an element (a stair's flights, a curtain wall's panels) are where it is.
        foreach (var r in file.OfType("IFCRELAGGREGATES"))
        {
            var whole = file.Get(r[4]);
            var at = whole?.Type == "IFCBUILDINGSTOREY" ? (Str(whole[2]), Num(whole[9]) * _len) : _container.TryGetValue(Ref(r[4]), out var w) ? w : ((string?, double)?)null;
            if (at is { } a) foreach (var part in Refs(r[5])) _container.TryAdd(part, a);
        }
        foreach (var r in file.OfType("IFCRELVOIDSELEMENT"))
            (_openings.TryGetValue(Ref(r[4]), out var l) ? l : _openings[Ref(r[4])] = []).Add(Ref(r[5]));
        foreach (var r in file.OfType("IFCRELDEFINESBYPROPERTIES"))
            foreach (var o in Refs(r[4])) (_definitions.TryGetValue(o, out var l) ? l : _definitions[o] = []).Add(Ref(r[5]));
        foreach (var s in file.OfType("IFCSTYLEDITEM"))
            if (s[0] is StepRef item && Colour(s[1]) is { } c) _colours[item.Id] = c;

        Storeys.AddRange(file.OfType("IFCBUILDINGSTOREY").Select(s => (Str(s[2]) ?? Str(s[7]) ?? "Level", Num(s[9]) * _len)).OrderBy(s => s.Item2));
        foreach (var e in file.Entities.Values)
        {
            if (NotElements.Contains(e.Type) || e.Args.Count < 7 || e[0] is not string gid || gid.Length != 22 || file.Get(e[6]) is not { Type: "IFCPRODUCTDEFINITIONSHAPE" } shape) continue;
            // Only an axis or a footprint (a layered wall whose body is in its parts): nothing to show.
            if (!Items(shape[2]).Any(r => r[1] is not string id || !NotBody.Contains(id.ToUpperInvariant()))) continue;
            var (storey, elevation) = _container.GetValueOrDefault(e.Id);
            Products.Add(new Product(e.Id, ClassName(e.Type), gid, Str(e[2]), Str(e[4]), e.Args.Count > 7 ? Str(e[7]) : null, storey, elevation, Placement(e[5]).O));
        }
    }

    /// <summary>IFCWALLSTANDARDCASE → IfcWallStandardCase, as IFC writes its names.</summary>
    public static string ClassName(string upper)
    {
        foreach (var known in Known) if (string.Equals(known, upper, StringComparison.OrdinalIgnoreCase)) return known;
        return "Ifc" + upper[3..4] + upper[4..].ToLowerInvariant();
    }

    static readonly string[] Known =
    [
        "IfcWall", "IfcWallStandardCase", "IfcSlab", "IfcRoof", "IfcBeam", "IfcColumn", "IfcDoor", "IfcWindow", "IfcStair", "IfcStairFlight",
        "IfcRamp", "IfcRampFlight", "IfcRailing", "IfcCovering", "IfcCurtainWall", "IfcPlate", "IfcMember", "IfcFooting", "IfcPile",
        "IfcBuildingElementProxy", "IfcFurnishingElement", "IfcFurniture", "IfcFlowSegment", "IfcFlowTerminal", "IfcFlowFitting",
        "IfcDistributionElement", "IfcGeographicElement", "IfcReinforcingBar", "IfcChimney", "IfcShadingDevice", "IfcTransportElement",
        "IfcBuildingElementPart", "IfcElementAssembly", "IfcDiscreteAccessory", "IfcMechanicalFastener", "IfcFastener", "IfcFlowController",
    ];

    // ─── Values ──────────────────────────────────────────────────────────────

    public static string? Str(object? v) => v switch { string s => s, StepTyped t => Str(t.Value), _ => null };
    public static double Num(object? v) => v switch { double d => d, long l => l, StepTyped t => Num(t.Value), _ => 0 };
    static int Ref(object? v) => v is StepRef r ? r.Id : -1;
    static IEnumerable<int> Refs(object? v) => v is List<object?> l ? l.OfType<StepRef>().Select(r => r.Id) : [];
    List<StepEntity> Items(object? v) => v is List<object?> l ? [.. l.Select(File.Get).OfType<StepEntity>()] : [];

    (double Len, double Angle) Units(StepEntity? project)
    {
        double len = 1, angle = 1;
        var units = File.Get(project?[8]);
        foreach (var u in Items(units?[0]))
        {
            var type = (u[1] as StepEnum)?.Value ?? (u.Type == "IFCSIUNIT" ? (u[1] as StepEnum)?.Value : null);
            double Factor()
            {
                if (u.Type == "IFCSIUNIT")
                    return (u[2] as StepEnum)?.Value switch { "MILLI" => 1e-3, "CENTI" => 1e-2, "DECI" => 0.1, "KILO" => 1e3, _ => 1 };
                if (u.Type == "IFCCONVERSIONBASEDUNIT" && File.Get(u[3]) is { } m)
                    return Num(m[0]) * (File.Get(m[1]) is { Type: "IFCSIUNIT" } si && (si[2] as StepEnum)?.Value == "MILLI" ? 1e-3 : 1);
                return 1;
            }
            if (type == "LENGTHUNIT") len = Factor();
            else if (type == "PLANEANGLEUNIT") angle = Factor();
        }
        return (len, angle);
    }

    /// <summary>A surface style's colour, through IFC2X3's presentation style assignments.</summary>
    string? Colour(object? styles)
    {
        foreach (var s in Items(styles))
        {
            if (s.Type == "IFCPRESENTATIONSTYLEASSIGNMENT") { if (Colour(s[0]) is { } c) return c; continue; }
            if (s.Type != "IFCSURFACESTYLE") continue;
            foreach (var r in Items(s[2]))
                if (File.Get(r[0]) is { Type: "IFCCOLOURRGB" } rgb)
                    return $"#{(int)Math.Round(Num(rgb[1]) * 255):x2}{(int)Math.Round(Num(rgb[2]) * 255):x2}{(int)Math.Round(Num(rgb[3]) * 255):x2}";
        }
        return null;
    }

    // ─── Properties ──────────────────────────────────────────────────────────

    /// <summary>A product's property sets and quantity sets: set name → property → value as text.</summary>
    public Dictionary<string, Dictionary<string, string>> Properties(int product)
    {
        var o = new Dictionary<string, Dictionary<string, string>>();
        foreach (var d in _definitions.GetValueOrDefault(product) ?? [])
        {
            if (File.Entities.GetValueOrDefault(d) is not { } set) continue;
            var props = new Dictionary<string, string>();
            if (set.Type == "IFCPROPERTYSET")
                foreach (var p in Items(set[4]))
                    props[Str(p[0]) ?? "?"] = p.Type switch
                    {
                        "IFCPROPERTYSINGLEVALUE" => Text(p[2]),
                        "IFCPROPERTYENUMERATEDVALUE" or "IFCPROPERTYLISTVALUE" => p[2] is List<object?> l ? string.Join(", ", l.Select(Text)) : "",
                        "IFCPROPERTYBOUNDEDVALUE" => $"{Text(p[3])} – {Text(p[2])}",
                        _ => p.Type.Replace("IFCPROPERTY", "").ToLowerInvariant(),
                    };
            else if (set.Type == "IFCELEMENTQUANTITY")
                foreach (var q in Items(set[5]))
                    props[Str(q[0]) ?? "?"] = Text(q[3]);
            else continue;
            o[Str(set[2]) ?? set.Type] = props;
        }
        return o;
    }

    static string Text(object? v) => v switch
    {
        StepTyped { Value: StepEnum e } => e.Value switch { "T" => "yes", "F" => "no", _ => e.Value.ToLowerInvariant() },
        StepTyped t => Text(t.Value),
        StepEnum e => e.Value,
        double d => Math.Round(d, 4).ToString(System.Globalization.CultureInfo.InvariantCulture),
        long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
        string s => s,
        null => "",
        _ => v.ToString() ?? "",
    };

    // ─── Placements ──────────────────────────────────────────────────────────

    Vec3 Point(object? r)
    {
        var p = File.Get(r);
        if (p?[0] is not List<object?> c) return Vec3.Zero;
        return new Vec3(Num(c.ElementAtOrDefault(0)) * _len, Num(c.ElementAtOrDefault(1)) * _len, c.Count > 2 ? Num(c[2]) * _len : 0);
    }

    Vec3? Direction(object? r)
    {
        if (File.Get(r)?[0] is not List<object?> c) return null;
        var d = new Vec3(Num(c.ElementAtOrDefault(0)), Num(c.ElementAtOrDefault(1)), c.Count > 2 ? Num(c[2]) : 0);
        return d.Length < 1e-12 ? null : d.Normalized();
    }

    /// <summary>An IfcAxis2Placement (2D or 3D) as a transform.</summary>
    Xf Axis2(object? r)
    {
        var a = File.Get(r);
        if (a == null) return Xf.Identity;
        var o = Point(a[0]);
        if (a.Type == "IFCAXIS2PLACEMENT2D")
        {
            var x2 = Direction(a[1]) ?? new Vec3(1, 0, 0);
            return new Xf(x2, new Vec3(-x2.Y, x2.X, 0), new Vec3(0, 0, 1), o);
        }
        var z = Direction(a[1]) ?? new Vec3(0, 0, 1);
        var x = Direction(a[2]) ?? (Math.Abs(z.X) < 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0));
        x = (x - z * Vec3.Dot(x, z)).Normalized();
        return new Xf(x, Vec3.Cross(z, x), z, o);
    }

    /// <summary>An object placement, relative to what it is placed in, as world coordinates.</summary>
    public Xf Placement(object? r)
    {
        if (r is not StepRef id) return Xf.Identity;
        if (_placements.TryGetValue(id.Id, out var known)) return known;
        _placements[id.Id] = Xf.Identity; // a cycle stops here
        var p = File.Get(r);
        var xf = p?.Type == "IFCLOCALPLACEMENT" ? Placement(p[0]).Then(Axis2(p[1])) : Xf.Identity;
        return _placements[id.Id] = xf;
    }

    Xf Operator(StepEntity? t)
    {
        if (t == null) return Xf.Identity;
        var x = Direction(t[0]) ?? new Vec3(1, 0, 0);
        var is3 = t.Type.Contains("3D");
        var z = is3 ? Direction(t[4]) ?? new Vec3(0, 0, 1) : new Vec3(0, 0, 1);
        var y = Direction(t[1]) ?? Vec3.Cross(z, x);
        var s = t[3] is null ? 1 : Num(t[3]);
        double sy = t.Type.Contains("NONUNIFORM") && t[5] != null ? Num(t[5]) : s, sz = t.Type.Contains("NONUNIFORM") && t[6] != null ? Num(t[6]) : s;
        return new Xf(x * s, y * sy, z * sz, Point(t[2]));
    }

    // ─── Shapes ──────────────────────────────────────────────────────────────

    static readonly HashSet<string> NotBody = ["AXIS", "FOOTPRINT", "BOX", "ANNOTATION", "PROFILE", "SURFACE", "REFERENCE", "LIGHTSOURCE"];

    /// <summary>
    /// A product's body as meshes in world coordinates, each with its colour;
    /// the openings that void it, as meshes too. With booleans, a clipped or
    /// subtracted body is cut; without, it is its first operand.
    /// </summary>
    public (List<(MeshBuilder Mesh, string? Colour)> Body, List<MeshBuilder> Openings) Shape(int product, ISolidBooleans? booleans = null)
    {
        var e = File.Entities[product];
        var body = Body(e, booleans);
        var openings = (_openings.GetValueOrDefault(product) ?? []).Select(o => File.Entities.GetValueOrDefault(o)).OfType<StepEntity>()
            .SelectMany(o => Body(o, booleans).Select(b => b.Mesh)).ToList();
        return (body, openings);
    }

    List<(MeshBuilder Mesh, string? Colour)> Body(StepEntity e, ISolidBooleans? booleans)
    {
        var o = new List<(MeshBuilder Mesh, string? Colour)>();
        var place = Placement(e[5]);
        foreach (var rep in Items(File.Get(e[6])?[2]))
        {
            if (rep[1] is string id && NotBody.Contains(id.ToUpperInvariant())) continue;
            foreach (var item in Items(rep[3])) Item(item, place, o, null, booleans);
        }
        return o;
    }

    void Item(StepEntity item, Xf at, List<(MeshBuilder Mesh, string? Colour)> o, string? inherited, ISolidBooleans? booleans)
    {
        var colour = _colours.GetValueOrDefault(item.Id) ?? inherited;
        if (item.Type == "IFCMAPPEDITEM")
        {
            var map = File.Get(item[0]);
            var xf = at.Then(Operator(File.Get(item[1]))).Then(Axis2(map?[0]));
            foreach (var sub in Items(File.Get(map?[1])?[3])) Item(sub, xf, o, colour, booleans);
            return;
        }
        if (item.Type is "IFCBOOLEANRESULT" or "IFCBOOLEANCLIPPINGRESULT")
        {
            var op = (item[0] as StepEnum)?.Value;
            var first = new List<(MeshBuilder Mesh, string? Colour)>();
            if (File.Get(item[1]) is { } a) Item(a, at, first, colour, booleans);
            if (op == "UNION") { o.AddRange(first); if (File.Get(item[2]) is { } b) Item(b, at, o, colour, booleans); return; }
            // Cut by the second operand where the booleans are; else the first operand as it is.
            var cutter = booleans == null || first.Count == 0 ? null : Cutter(File.Get(item[2]), at, first.SelectMany(f => f.Mesh.V).ToList(), booleans);
            foreach (var (mesh, c) in first)
            {
                var solid = cutter == null ? null : SolidFromMesh.Build(mesh.Welded().V, mesh.Welded().T);
                if (solid == null) { o.Add((mesh, c)); continue; }
                var cut = op == "INTERSECTION" ? booleans!.Intersection(solid, cutter!) : booleans!.Difference(solid, [cutter!]);
                if (cut != null) o.Add((MeshBuilder.FromSolid(cut), c));
            }
            return;
        }
        var m = Solid(item);
        if (m == null || m.T.Count == 0) return;
        m.Transform(at);
        o.Add((m, colour));
    }

    /// <summary>
    /// The second operand of a boolean as a solid, in world coordinates: a
    /// half-space as a block far larger than the body it cuts (on the side of
    /// its material: against its plane's normal when it agrees), bounded by its
    /// polygon when it has one; any other solid as it is.
    /// </summary>
    Solid? Cutter(StepEntity? s, Xf at, List<Vec3> body, ISolidBooleans booleans)
    {
        if (s == null) return null;
        if (s.Type is "IFCHALFSPACESOLID" or "IFCBOXEDHALFSPACE" or "IFCPOLYGONALBOUNDEDHALFSPACE")
        {
            var size = 0.0;
            foreach (var p in body) size = Math.Max(size, (p - body[0]).Length);
            var big = 2 * size + 10;
            var plane = at.Then(Axis2(File.Get(s[0])?[0]));
            var agrees = (s[1] as StepEnum)?.Value != "F";
            List<Vec3> square = [new(-big, -big, 0), new(big, -big, 0), new(big, big, 0), new(-big, big, 0)];
            var block = Prism(square, [], new Vec3(0, 0, big));
            if (agrees) block.Transform(new Xf(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0, 0, -big)));
            block.Transform(plane);
            var half = SolidFromMesh.Build(block.V, block.T);
            if (s.Type != "IFCPOLYGONALBOUNDEDHALFSPACE" || half == null) return half;
            var outline = Curve(File.Get(s[3]));
            if (outline.Count < 3) return half;
            var prism = Prism(outline, [], new Vec3(0, 0, 2 * big));
            prism.Transform(at.Then(Axis2(s[2])).Then(new Xf(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0, 0, -big))));
            return SolidFromMesh.Build(prism.V, prism.T) is { } bound ? booleans.Intersection(half, bound) : half;
        }
        var m = Solid(s);
        if (m == null) return null;
        m.Transform(at);
        var w = m.Welded();
        return SolidFromMesh.Build(w.V, w.T);
    }

    MeshBuilder? Solid(StepEntity s) => s.Type switch
    {
        "IFCEXTRUDEDAREASOLID" or "IFCEXTRUDEDAREASOLIDTAPERED" => Extrusion(s),
        "IFCFACETEDBREP" or "IFCFACETEDBREPWITHVOIDS" => Faces(Items(File.Get(s[0])?[0])),
        "IFCSHELLBASEDSURFACEMODEL" => Surfaces(Items(s[0]).SelectMany(sh => Items(sh[0]))),
        "IFCFACEBASEDSURFACEMODEL" => Surfaces(Items(s[0]).SelectMany(sh => Items(sh[0]))),
        "IFCTRIANGULATEDFACESET" => TriangulatedSet(s),
        "IFCPOLYGONALFACESET" => PolygonalSet(s),
        "IFCREVOLVEDAREASOLID" => Revolution(s),
        "IFCSWEPTDISKSOLID" => Tube(s),
        "IFCADVANCEDBREP" or "IFCADVANCEDBREPWITHVOIDS" => AdvancedFaces(Items(File.Get(s[0])?[0])),
        _ => null,
    };

    /// <summary>A surface model's faces: plain ones as polygons, advanced ones on their surfaces.</summary>
    MeshBuilder Surfaces(IEnumerable<StepEntity> faces)
    {
        var all = faces.ToList();
        var m = Faces(all.Where(f => f.Type != "IFCADVANCEDFACE"));
        m.Append(AdvancedFaces(all.Where(f => f.Type == "IFCADVANCEDFACE")));
        return m;
    }

    MeshBuilder Faces(IEnumerable<StepEntity> faces)
    {
        var m = new MeshBuilder();
        var index = new Dictionary<int, int>();
        int[] Loop(StepEntity bound)
        {
            var loop = File.Get(bound[0]);
            var ids = loop?.Type == "IFCPOLYLOOP" ? Refs(loop[0]).ToArray() : [];
            var ring = ids.Select(p => index.TryGetValue(p, out var k) ? k : index[p] = m.Add(Point(new StepRef(p)))).ToArray();
            return (bound[1] as StepEnum)?.Value == "F" ? [.. ring.Reverse()] : ring;
        }
        foreach (var f in faces)
        {
            var bounds = Items(f[0]);
            var outer = bounds.FirstOrDefault(b => b.Type == "IFCFACEOUTERBOUND") ?? bounds.FirstOrDefault();
            if (outer == null) continue;
            m.Polygon(Loop(outer), [.. bounds.Where(b => b != outer).Select(Loop)]);
        }
        return m;
    }

    List<Vec3> PointList(object? r) =>
        File.Get(r)?[0] is List<object?> rows
            ? [.. rows.OfType<List<object?>>().Select(c => new Vec3(Num(c.ElementAtOrDefault(0)) * _len, Num(c.ElementAtOrDefault(1)) * _len, c.Count > 2 ? Num(c[2]) * _len : 0))]
            : [];

    static int[] Indices(object? v) => v is List<object?> l ? [.. l.Select(x => (int)Num(x))] : [];

    MeshBuilder TriangulatedSet(StepEntity s)
    {
        var m = new MeshBuilder();
        foreach (var p in PointList(s[0])) m.Add(p);
        var pn = Indices(s[4]);
        int At(int i) => (pn.Length > 0 ? pn[i - 1] : i) - 1;
        if (s[3] is List<object?> tris)
            foreach (var t in tris.OfType<List<object?>>().Select(Indices).Where(t => t.Length == 3))
                m.T.Add((At(t[0]), At(t[1]), At(t[2])));
        return m;
    }

    MeshBuilder PolygonalSet(StepEntity s)
    {
        var m = new MeshBuilder();
        foreach (var p in PointList(s[0])) m.Add(p);
        var pn = Indices(s[3]);
        int[] At(int[] ix) => [.. ix.Select(i => (pn.Length > 0 ? pn[i - 1] : i) - 1)];
        foreach (var f in Items(s[2]))
            m.Polygon(At(Indices(f[0])), f.Type == "IFCINDEXEDPOLYGONALFACEWITHVOIDS" && f[1] is List<object?> inner ? [.. inner.Select(i => At(Indices(i)))] : []);
        return m;
    }

    MeshBuilder? Extrusion(StepEntity s)
    {
        var areas = Profile(File.Get(s[0]));
        if (areas.Count == 0) return null;
        var dir = Direction(s[2]) ?? new Vec3(0, 0, 1);
        var depth = Num(s[3]) * _len;
        var m = new MeshBuilder();
        foreach (var (outer, holes) in areas) m.Append(Prism(outer, holes, dir * depth));
        m.Transform(Axis2(s[1]));
        return m;
    }

    /// <summary>A profile swept along a vector: two caps and a quad on every edge, outward.</summary>
    static MeshBuilder Prism(List<Vec3> outer, List<List<Vec3>> holes, Vec3 v)
    {
        var m = new MeshBuilder();
        if (v.Z < 0) { v = -v; } // the same body, swept the other way from the other cap
        static List<Vec3> Oriented(List<Vec3> loop, bool ccw) => Area(loop) > 0 == ccw ? loop : [.. Enumerable.Reverse(loop)];
        var loops = new List<List<Vec3>> { Oriented(outer, true) };
        loops.AddRange(holes.Select(h => Oriented(h, false)));
        var bottom = loops.Select(l => l.Select(m.Add).ToArray()).ToList();
        var top = loops.Select(l => l.Select(p => m.Add(p + v)).ToArray()).ToList();
        m.Polygon([.. bottom[0].Reverse()], [.. bottom.Skip(1).Select(h => h.Reverse().ToArray())]);
        m.Polygon(top[0], [.. top.Skip(1)]);
        for (var k = 0; k < loops.Count; k++)
            for (var i = 0; i < bottom[k].Length; i++)
            {
                int a0 = bottom[k][i], b0 = bottom[k][(i + 1) % bottom[k].Length], a1 = top[k][i], b1 = top[k][(i + 1) % top[k].Length];
                m.T.Add((a0, b0, b1)); m.T.Add((a0, b1, a1));
            }
        return m;
    }

    static double Area(List<Vec3> l) { double a = 0; for (var i = 0; i < l.Count; i++) { var (p, q) = (l[i], l[(i + 1) % l.Count]); a += p.X * q.Y - q.X * p.Y; } return a / 2; }

    MeshBuilder? Revolution(StepEntity s)
    {
        var areas = Profile(File.Get(s[0]));
        if (areas.Count == 0) return null;
        var axis = File.Get(s[2]);
        var origin = Point(axis?[0]);
        var dir = Direction(axis?[1]) ?? new Vec3(0, 1, 0);
        var angle = Num(s[3]) * _angle;
        var steps = Math.Max(3, (int)Math.Ceiling(Math.Abs(angle) / (Math.PI / 12)));
        var m = new MeshBuilder();
        var ring = areas[0].Outer;
        var rows = Enumerable.Range(0, steps + 1).Select(k => ring.Select(p => m.Add(Rotate(p, origin, dir, angle * k / steps))).ToArray()).ToList();
        for (var k = 0; k < steps; k++)
            for (var i = 0; i < ring.Count; i++)
            {
                int a0 = rows[k][i], b0 = rows[k][(i + 1) % ring.Count], a1 = rows[k + 1][i], b1 = rows[k + 1][(i + 1) % ring.Count];
                m.T.Add((a0, b0, b1)); m.T.Add((a0, b1, a1));
            }
        if (Math.Abs(angle) < 2 * Math.PI - 1e-6) { m.Polygon(rows[0], []); m.Polygon([.. rows[^1].Reverse()], []); }
        m.Transform(Axis2(s[1]));
        return m;
    }

    static Vec3 Rotate(Vec3 p, Vec3 o, Vec3 axis, double a)
    {
        var v = p - o;
        var c = Math.Cos(a); var sn = Math.Sin(a);
        return o + v * c + Vec3.Cross(axis, v) * sn + axis * (Vec3.Dot(axis, v) * (1 - c));
    }

    MeshBuilder? Tube(StepEntity s)
    {
        var path = Curve(File.Get(s[0]));
        var r = Num(s[1]) * _len;
        if (path.Count < 2 || r <= 0) return null;
        const int sides = 8;
        var m = new MeshBuilder();
        var rings = new List<int[]>();
        for (var i = 0; i < path.Count; i++)
        {
            var t = (path[Math.Min(i + 1, path.Count - 1)] - path[Math.Max(i - 1, 0)]).Normalized();
            var u = Vec3.Cross(t, Math.Abs(t.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0)).Normalized();
            var w = Vec3.Cross(t, u);
            rings.Add([.. Enumerable.Range(0, sides).Select(k => m.Add(path[i] + u * (r * Math.Cos(2 * Math.PI * k / sides)) + w * (r * Math.Sin(2 * Math.PI * k / sides))))]);
        }
        for (var i = 0; i + 1 < rings.Count; i++)
            for (var k = 0; k < sides; k++)
            {
                int a0 = rings[i][k], b0 = rings[i][(k + 1) % sides], a1 = rings[i + 1][k], b1 = rings[i + 1][(k + 1) % sides];
                m.T.Add((a0, b0, b1)); m.T.Add((a0, b1, a1));
            }
        m.Polygon([.. rings[0].Reverse()], []);
        m.Polygon(rings[^1], []);
        return m;
    }

    // ─── Profiles and curves ─────────────────────────────────────────────────

    /// <summary>A profile as areas (an outer loop and its holes) in its plane, z = 0.</summary>
    List<(List<Vec3> Outer, List<List<Vec3>> Holes)> Profile(StepEntity? p)
    {
        if (p == null) return [];
        List<(List<Vec3>, List<List<Vec3>>)> One(List<Vec3> outer, params List<Vec3>[] holes) => outer.Count >= 3 ? [(outer, [.. holes.Where(h => h.Count >= 3)])] : [];
        List<Vec3> Placed(List<Vec3> pts) { var xf = Axis2(p[2]); return [.. pts.Select(xf.Apply)]; }
        List<Vec3> Rect(double w, double h) => [new(-w / 2, -h / 2, 0), new(w / 2, -h / 2, 0), new(w / 2, h / 2, 0), new(-w / 2, h / 2, 0)];
        List<Vec3> Circle(double r, double r2 = -1) => [.. Enumerable.Range(0, 24).Select(k => new Vec3(r * Math.Cos(k * Math.PI / 12), (r2 < 0 ? r : r2) * Math.Sin(k * Math.PI / 12), 0))];
        double L(int i) => Num(p[i]) * _len;
        switch (p.Type)
        {
            case "IFCRECTANGLEPROFILEDEF": case "IFCROUNDEDRECTANGLEPROFILEDEF": return One(Placed(Rect(L(3), L(4))));
            case "IFCRECTANGLEHOLLOWPROFILEDEF": return One(Placed(Rect(L(3), L(4))), Placed(Rect(L(3) - 2 * L(5), L(4) - 2 * L(5))));
            case "IFCCIRCLEPROFILEDEF": return One(Placed(Circle(L(3))));
            case "IFCCIRCLEHOLLOWPROFILEDEF": return One(Placed(Circle(L(3))), Placed(Circle(L(3) - L(4))));
            case "IFCELLIPSEPROFILEDEF": return One(Placed(Circle(L(3), L(4))));
            case "IFCISHAPEPROFILEDEF":
            {
                double w = L(3) / 2, d = L(4) / 2, t = L(5) / 2, f = L(6);
                return One(Placed([new(-w, -d, 0), new(w, -d, 0), new(w, -d + f, 0), new(t, -d + f, 0), new(t, d - f, 0), new(w, d - f, 0), new(w, d, 0), new(-w, d, 0), new(-w, d - f, 0), new(-t, d - f, 0), new(-t, -d + f, 0), new(-w, -d + f, 0)]));
            }
            case "IFCTSHAPEPROFILEDEF":
            {
                double d = L(3), w = L(4) / 2, t = L(5) / 2, f = L(6);
                return One(Placed([new(-t, -d / 2, 0), new(t, -d / 2, 0), new(t, d / 2 - f, 0), new(w, d / 2 - f, 0), new(w, d / 2, 0), new(-w, d / 2, 0), new(-w, d / 2 - f, 0), new(-t, d / 2 - f, 0)]));
            }
            case "IFCLSHAPEPROFILEDEF":
            {
                double d = L(3), w = p[4] is null ? d : L(4), t = L(5);
                return One(Placed([new(-w / 2, -d / 2, 0), new(w / 2, -d / 2, 0), new(w / 2, -d / 2 + t, 0), new(-w / 2 + t, -d / 2 + t, 0), new(-w / 2 + t, d / 2, 0), new(-w / 2, d / 2, 0)]));
            }
            case "IFCUSHAPEPROFILEDEF":
            {
                double d = L(3) / 2, w = L(4) / 2, t = L(5), f = L(6);
                return One(Placed([new(-w, -d, 0), new(w, -d, 0), new(w, -d + f, 0), new(-w + t, -d + f, 0), new(-w + t, d - f, 0), new(w, d - f, 0), new(w, d, 0), new(-w, d, 0)]));
            }
            case "IFCARBITRARYCLOSEDPROFILEDEF": return One(Curve(File.Get(p[2])));
            case "IFCARBITRARYPROFILEDEFWITHVOIDS": return One(Curve(File.Get(p[2])), [.. Items(p[3]).Select(Curve)]);
            case "IFCCOMPOSITEPROFILEDEF": return [.. Items(p[2]).SelectMany(Profile)];
            case "IFCDERIVEDPROFILEDEF":
            {
                var t = Operator(File.Get(p[3]));
                return [.. Profile(File.Get(p[2])).Select(a => (a.Outer.Select(t.Apply).ToList(), a.Holes.Select(h => h.Select(t.Apply).ToList()).ToList()))];
            }
            default: return [];
        }
    }

    /// <summary>A curve as points (arcs every 15°), without its closing repeat.</summary>
    List<Vec3> Curve(StepEntity? c)
    {
        var o = new List<Vec3>();
        if (c == null) return o;
        switch (c.Type)
        {
            case "IFCPOLYLINE": o.AddRange(Refs(c[0]).Select(r => Point(new StepRef(r)))); break;
            case "IFCINDEXEDPOLYCURVE":
            {
                var pts = PointList(c[0]);
                if (c[1] is not List<object?> segs) { o.AddRange(pts); break; }
                foreach (var seg in segs.OfType<StepTyped>())
                {
                    var ix = Indices(seg.Value).Select(i => pts[i - 1]).ToList();
                    if (seg.Type == "IFCARCINDEX" && ix.Count == 3) o.AddRange(Arc3(ix[0], ix[1], ix[2]));
                    else o.AddRange(ix);
                }
                break;
            }
            case "IFCCOMPOSITECURVE":
                foreach (var seg in Items(c[0]))
                {
                    var part = Curve(File.Get(seg[2]));
                    if ((seg[1] as StepEnum)?.Value == "F") part.Reverse();
                    o.AddRange(part);
                }
                break;
            case "IFCTRIMMEDCURVE": o.AddRange(Trimmed(c)); break;
            case "IFCBSPLINECURVEWITHKNOTS" or "IFCRATIONALBSPLINECURVEWITHKNOTS": o.AddRange(SplineCurve(c)); break;
            case "IFCCIRCLE": { var xf = Axis2(c[0]); var r = Num(c[1]) * _len; o.AddRange(Enumerable.Range(0, 24).Select(k => xf.Apply(new Vec3(r * Math.Cos(k * Math.PI / 12), r * Math.Sin(k * Math.PI / 12), 0)))); break; }
            case "IFCELLIPSE": { var xf = Axis2(c[0]); double a = Num(c[1]) * _len, b = Num(c[2]) * _len; o.AddRange(Enumerable.Range(0, 24).Select(k => xf.Apply(new Vec3(a * Math.Cos(k * Math.PI / 12), b * Math.Sin(k * Math.PI / 12), 0)))); break; }
        }
        var clean = new List<Vec3>();
        foreach (var p in o) if (clean.Count == 0 || (clean[^1] - p).Length > 1e-7) clean.Add(p);
        if (clean.Count > 2 && (clean[0] - clean[^1]).Length < 1e-7) clean.RemoveAt(clean.Count - 1);
        return clean;
    }

    IEnumerable<Vec3> Trimmed(StepEntity c)
    {
        var basis = File.Get(c[0]);
        var sense = (c[3] as StepEnum)?.Value != "F";
        Vec3? Cart(object? trim) => trim is List<object?> l && l.OfType<StepRef>().FirstOrDefault() is { Id: > 0 } r ? Point(r) : null;
        double? Param(object? trim) => trim is List<object?> l && l.OfType<StepTyped>().FirstOrDefault(t => t.Type == "IFCPARAMETERVALUE") is { } t ? Num(t) : null;
        if (basis?.Type is "IFCCIRCLE")
        {
            var xf = Axis2(basis[0]);
            var r = Num(basis[1]) * _len;
            double Angle(object? trim) => Param(trim) is { } v ? v * _angle
                : Cart(trim) is { } p ? Math.Atan2(Vec3.Dot(p - xf.O, xf.Y), Vec3.Dot(p - xf.O, xf.X)) : 0;
            double a1 = Angle(c[1]), a2 = Angle(c[2]);
            if (sense) { while (a2 <= a1) a2 += 2 * Math.PI; } else { while (a2 >= a1) a2 -= 2 * Math.PI; }
            var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(a2 - a1) / (Math.PI / 12)));
            return Enumerable.Range(0, n + 1).Select(k => { var a = a1 + (a2 - a1) * k / n; return xf.Apply(new Vec3(r * Math.Cos(a), r * Math.Sin(a), 0)); });
        }
        var ends = new[] { Cart(c[1]), Cart(c[2]) }.OfType<Vec3>().ToList();
        return sense ? ends : Enumerable.Reverse(ends);
    }

    /// <summary>The arc through three points, every 15° or so.</summary>
    static IEnumerable<Vec3> Arc3(Vec3 a, Vec3 b, Vec3 c)
    {
        double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (Math.Abs(d) < 1e-12) return [a, b, c];
        double ux = ((a.X * a.X + a.Y * a.Y) * (b.Y - c.Y) + (b.X * b.X + b.Y * b.Y) * (c.Y - a.Y) + (c.X * c.X + c.Y * c.Y) * (a.Y - b.Y)) / d;
        double uy = ((a.X * a.X + a.Y * a.Y) * (c.X - b.X) + (b.X * b.X + b.Y * b.Y) * (a.X - c.X) + (c.X * c.X + c.Y * c.Y) * (b.X - a.X)) / d;
        double r = Math.Sqrt((a.X - ux) * (a.X - ux) + (a.Y - uy) * (a.Y - uy));
        double A(Vec3 p) => Math.Atan2(p.Y - uy, p.X - ux);
        double t0 = A(a), t1 = A(b), t2 = A(c);
        static double Ccw(double from, double to) { var x = to - from; while (x < 0) x += 2 * Math.PI; return x; }
        var ccw = Ccw(t0, t1) < Ccw(t0, t2);
        var sweep = ccw ? Ccw(t0, t2) : -Ccw(t2, t0);
        var n = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 12)));
        return Enumerable.Range(0, n + 1).Select(k => new Vec3(ux + r * Math.Cos(t0 + sweep * k / n), uy + r * Math.Sin(t0 + sweep * k / n), a.Z));
    }
}
