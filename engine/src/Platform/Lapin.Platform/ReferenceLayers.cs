using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The layers of a reference from an IFC file (a linked or imported wall, slab or roof that stays a reference), read from
/// the material its file associated with it (<see cref="IfcSource"/>: an IfcMaterialLayerSetUsage or an IfcMaterialLayerSet),
/// so Visibility / Graphics can hide a link's finishes as it hides a native wall's (decision 400): each layer a subcategory
/// (core, finishes, insulation, membranes, other layers) by its IFC Category, else by its material's name.
/// <para>
/// The body the file gave is split by the layers' thicknesses along the layer direction (<see cref="AxisKey"/>: the element's
/// local Y for AXIS2, Z for AXIS3, kept by the import), fitted to the body's extent along it: a part per layer keyed
/// <c>layer:&lt;n&gt;</c> as a native element's, so the subcategories, the view styles' layers modes and the drawings' layer
/// lines treat it alike. Nothing of the split is stored: it is worked out when the reference is built (on opening, on a
/// link's reload). A reference imported before the axis was kept splits when its direction can be told from the body (a
/// slab's is up; a wall's across its length, and only for a symmetric build-up, the side being unknown).
/// </para>
/// </summary>
public static class ReferenceLayers
{
    /// <summary>The layer direction in world coordinates (3 numbers), written by the IFC import for a reference with a layer set.</summary>
    public const string AxisKey = "layerAxis";

    /// <summary>The IFC class an IfcBuildingElementPart reference belonged to in its file (a wall's plaster as a part of its own): its category's.</summary>
    public const string PartOfKey = "partOf";

    /// <summary>One layer as the file gives it: its name, material, thickness in the file's length unit, its subcategory and whether it is the core.</summary>
    public sealed record Layer(string Name, string Material, double Thickness, string Sub, bool Core);

    /// <summary>A reference's layers, the direction they are laid in (AXIS1/2/3), and whether they run the negative way.</summary>
    public sealed record Set(IReadOnlyList<Layer> Layers, string Direction, bool Negative, bool Usage);

    sealed class Box(Set? value) { public readonly Set? Value = value; }
    static readonly ConditionalWeakTable<Entity, Box> _sets = [];

    /// <summary>Whether an element is a reference from a file (its body came with it).</summary>
    public static bool IsReference(Entity e) => e.Attrs["reference"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    /// <summary>A reference's layers from its file's material, null when it has no layer set (or is not a reference).</summary>
    public static Set? Of(IModelView m, Entity e)
    {
        if (!IsReference(e)) return null;
        return _sets.GetValue(e, x => new Box(Read(m, x))).Value;
    }

    /// <summary>The material snapshot the reference's file associated with it ({t, a}), or null.</summary>
    static JsonObject? MaterialSnap(IModelView m, Entity e) =>
        (string?)e.Attrs[IfcSource.Key]?["material"]?["record"] is { } rec && m.Get(rec)?.Attrs["snap"] is JsonObject snap ? snap : null;

    static Set? Read(IModelView m, Entity e)
    {
        if (MaterialSnap(m, e) is not { } snap) return null;
        var t = (string?)snap["t"];
        var a = snap["a"] as JsonArray;
        JsonObject? set;
        string direction;
        var negative = false;
        var usage = false;
        if (t == "IFCMATERIALLAYERSETUSAGE")
        {
            set = a?.ElementAtOrDefault(0) as JsonObject;
            direction = Enum(a?.ElementAtOrDefault(1)) ?? DefaultDirection(e.Class);
            negative = Enum(a?.ElementAtOrDefault(2)) == "NEGATIVE";
            usage = true;
        }
        else if (t == "IFCMATERIALLAYERSET") { set = snap; direction = DefaultDirection(e.Class); }
        else return null;
        if ((string?)set?["t"] != "IFCMATERIALLAYERSET" || set["a"]?.AsArray().ElementAtOrDefault(0) is not JsonArray items) return null;
        var raw = new List<(string Name, string Material, double Thickness, string? Category)>();
        foreach (var item in items.OfType<JsonObject>())
        {
            if ((string?)item["t"] != "IFCMATERIALLAYER" || item["a"] is not JsonArray la) continue;
            var mat = la.ElementAtOrDefault(0) as JsonObject;
            var matName = mat?["a"]?.AsArray().ElementAtOrDefault(0) is { } mn ? Text(mn) : null;
            var matCategory = mat?["a"]?.AsArray().ElementAtOrDefault(2) is { } mc ? Text(mc) : null;
            var thickness = Real(la.ElementAtOrDefault(1)) ?? 0;
            var name = Text(la.ElementAtOrDefault(3));
            var category = Text(la.ElementAtOrDefault(5)) ?? matCategory;
            raw.Add((name ?? matName ?? $"Layer {raw.Count + 1}", matName ?? name ?? "Material", Math.Max(0, thickness), category));
        }
        if (raw.Count == 0) return null;
        var subs = raw.Select(r => SubOf(r.Category, r.Material, r.Name)).ToList();
        // The core: the layer the file marks (Lapin writes "Core"; IFC4's LoadBearing), else the thickest that is no finish, insulation or membrane.
        var core = raw.FindIndex(r => r.Category is { } c && (c.Equals("Core", StringComparison.OrdinalIgnoreCase) || c.Equals("LoadBearing", StringComparison.OrdinalIgnoreCase)));
        if (core < 0)
        {
            var best = -1.0;
            for (var i = 0; i < raw.Count; i++)
                if (subs[i] == "otherLayers" && raw[i].Thickness > best) { best = raw[i].Thickness; core = i; }
        }
        var layers = raw.Select((r, i) => new Layer(r.Name, r.Material, r.Thickness, i == core ? "core" : subs[i], i == core)).ToList();
        return new Set(layers, direction, negative, usage);
    }

    static string DefaultDirection(string cls) => cls is IfcClass.Wall or "IfcWallStandardCase" or "IfcCurtainWall" ? "AXIS2" : "AXIS3";

    /// <summary>
    /// A layer's subcategory by its IFC Category (Finish, Insulation, Membrane…), else by its material's name, as
    /// <see cref="Materials.IsFinish"/> tells a native material's finish.
    /// </summary>
    public static string SubOf(string? category, string material, string? name = null)
    {
        var c = category?.Trim().ToLowerInvariant();
        switch (c)
        {
            case "finish": case "finishes": case "finishing": return "finishes";
            case "insulation": case "thermalinsulation": case "acousticinsulation": return "insulation";
            case "membrane": case "airbarrier": case "vapourbarrier": case "vaporbarrier": case "waterproofing": return "membranes";
            case "core": case "loadbearing": return "otherLayers";
        }
        var probe = new Entity("", IfcClass.Material, material, new JsonObject());
        if (Materials.IsFinish(probe) || name != null && Materials.IsFinish(new Entity("", IfcClass.Material, name, new JsonObject()))) return "finishes";
        var n = MaterialLook.Fold(material + " " + name);
        bool Has(params string[] w) => w.Any(x => n.Contains(x, StringComparison.Ordinal));
        // German too (Putz, Fliesen, Gips; Dämmung; Folie, Abdichtung): files from the DACH offices are common.
        if (Has("putz", "fliese", "gipsplatte", "gipskarton", " gips", "parkett", "anstrich")) return "finishes";
        if (Has("insulation", "insulat", "xps", "eps", "wool", "vata", "polistiren", "polystyren", "pir ", "pur ", "izolat", "dammung", "daemmung", "isolier")) return "insulation";
        if (Has("membrane", "membran", "foil", "folie", "barrier", "bariera", "hidroizol", "waterproof", "bitum", "abdichtung", "dampfbremse")) return "membranes";
        return "otherLayers";
    }

    /// <summary>
    /// The subcategory a whole reference falls in by its one material (a plaster IfcCovering, a wall's plaster brought as
    /// an IfcBuildingElementPart, a one-layer set): finishes, insulation or membranes; null for anything else (it goes with
    /// its category).
    /// </summary>
    public static string? WholeSub(IModelView m, Entity e)
    {
        if (!IsReference(e) || MaterialSnap(m, e) is not { } snap) return null;
        var t = (string?)snap["t"];
        var a = snap["a"] as JsonArray;
        string? sub = null;
        switch (t)
        {
            case "IFCMATERIAL":
                sub = SubOf(Text(a?.ElementAtOrDefault(2)), Text(a?.ElementAtOrDefault(0)) ?? "");
                break;
            case "IFCMATERIALLAYERSETUSAGE" or "IFCMATERIALLAYERSET":
                if (Of(m, e) is { Layers.Count: 1 } one) sub = one.Layers[0].Sub == "core" ? SubOf(null, one.Layers[0].Material, one.Layers[0].Name) : one.Layers[0].Sub;
                break;
            case "IFCMATERIALCONSTITUENTSET":
            {
                // Every constituent of one subcategory: the element is of it.
                var subs = (a?.ElementAtOrDefault(2) as JsonArray)?.OfType<JsonObject>().Select(c =>
                {
                    var ca = c["a"] as JsonArray;
                    var mat = ca?.ElementAtOrDefault(2) as JsonObject;
                    return SubOf(Text(ca?.ElementAtOrDefault(3)) ?? Text(mat?["a"]?.AsArray().ElementAtOrDefault(2)), Text(mat?["a"]?.AsArray().ElementAtOrDefault(0)) ?? "", Text(ca?.ElementAtOrDefault(0)));
                }).Distinct().ToList();
                if (subs is { Count: 1 }) sub = subs[0];
                break;
            }
            case "IFCMATERIALLIST":
            {
                var subs = (a?.ElementAtOrDefault(0) as JsonArray)?.OfType<JsonObject>().Select(x => SubOf(Text(x["a"]?.AsArray().ElementAtOrDefault(2)), Text(x["a"]?.AsArray().ElementAtOrDefault(0)) ?? "")).Distinct().ToList();
                if (subs is { Count: 1 }) sub = subs[0];
                break;
            }
        }
        return sub is "finishes" or "insulation" or "membranes" ? sub : null;
    }

    /// <summary>The category a reference counts in: its class's, else (an IfcBuildingElementPart) the category of the element it was part of.</summary>
    public static string? CategoryOf(Entity e) =>
        Lapin.Drawing.LineStyles.CategoryOf(e.Class) ?? (e.Str(PartOfKey) is { } host ? Lapin.Drawing.LineStyles.CategoryOf(host) : null);

    /// <summary>The subcategory of a reference's part by its key (<c>layer:&lt;n&gt;</c>), or null.</summary>
    public static string? SubOfPart(IModelView m, Entity e, string partKey)
    {
        if (!partKey.StartsWith("layer:", StringComparison.Ordinal) || !int.TryParse(partKey.AsSpan(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return null;
        return Of(m, e) is { } s && i >= 0 && i < s.Layers.Count ? s.Layers[i].Sub : null;
    }

    // ------------------------------------------------------------ the split

    /// <summary>The length units a file's thicknesses may be in (m, mm, cm, in, ft), tried against the body's extent.</summary>
    static readonly double[] Units = [1, 0.001, 0.01, 0.0254, 0.3048];

    /// <summary>How far the layers' total may be off the body's extent and still be fitted to it.</summary>
    const double FitTolerance = 0.003;

    /// <summary>The direction the layers lie in, world: the import's, else told from the body (a slab's up; a wall's across its length).</summary>
    static (Vec3 Axis, bool Known)? AxisOf(Entity e, Set set, IReadOnlyList<GeometryPart> parts, bool footprint = false)
    {
        if (!footprint && e.Attrs[AxisKey] is JsonArray ax && ax.Count == 3 && new Vec3(Json.Number(ax[0]) ?? 0, Json.Number(ax[1]) ?? 0, Json.Number(ax[2]) ?? 0) is var v && v.Length > 1e-9)
            return (v.Normalized(), true);
        if (!footprint && set.Direction == "AXIS3") return (new Vec3(0, 0, 1), true);
        if (set.Direction != "AXIS2") return null;
        // A wall's thickness across its plan's long direction: the footprint's principal axis, turned a quarter.
        double sxx = 0, sxy = 0, syy = 0, mx = 0, my = 0;
        var n = 0;
        foreach (var p in parts) foreach (var q in p.Solid.Vertices) { mx += q.X; my += q.Y; n++; }
        if (n < 3) return null;
        mx /= n; my /= n;
        foreach (var p in parts) foreach (var q in p.Solid.Vertices) { var dx = q.X - mx; var dy = q.Y - my; sxx += dx * dx; sxy += dx * dy; syy += dy * dy; }
        var angle = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        return (new Vec3(-Math.Sin(angle), Math.Cos(angle), 0), false);
    }

    /// <summary>
    /// A reference's parts split into its layers (keyed <c>layer:&lt;n&gt;</c>, named and coloured by their materials, the
    /// material's name as theirs), or the parts as they were when they cannot be: no layer set, no booleans, a body whose
    /// extent the layers do not fill, a wall's side unknown for a build-up that is not symmetric.
    /// </summary>
    public static IReadOnlyList<GeometryPart> Split(IModelView m, Entity e, IReadOnlyList<GeometryPart> parts, IServices? services)
    {
        if (parts.Count == 0 || Of(m, e) is not { } set || set.Layers.Count < 2 || parts.Any(p => !p.HasSolid && p.Mesh != null)) return parts;
        if (services?.Get<ISolidBooleans>() is not { } booleans) return parts;
        if (AxisOf(e, set, parts) is not var (axis, known)) return parts;
        // Without the side the file laid them from, only a build-up that reads the same both ways.
        if (!known && !Symmetric(set)) return parts;
        var fit = Fit(parts, axis, set);
        // A wall whose placement does not run along it: across its plan instead, on the side its placement's axis points to.
        if (fit == null && set.Direction == "AXIS2" && AxisOf(e, set with { Direction = "AXIS2" }, parts, footprint: true) is var (across, _))
        {
            var dot = Vec3.Dot(across, axis);
            if (Math.Abs(dot) > 0.3 || Symmetric(set)) { axis = dot < 0 ? across * -1 : across; fit = Fit(parts, axis, set); }
        }
        if (fit is not var (lo, hi, bmin, bmax)) return parts;
        var extent = hi - lo;
        var total = set.Layers.Sum(l => l.Thickness);
        // The bands across the body, from the side the layers start: the low one running the positive way.
        var scale = extent / total;
        var bands = new List<(int Index, double From, double To)>();
        var at = 0.0;
        for (var i = 0; i < set.Layers.Count; i++)
        {
            var th = set.Layers[i].Thickness * scale;
            if (th > 1e-6) bands.Add((i, set.Negative ? hi - at - th : lo + at, set.Negative ? hi - at : lo + at + th));
            at += th;
        }
        if (bands.Count < 2) return parts;
        var reach = (bmax - bmin).Length + 1;
        var centre = (bmin + bmax) * 0.5;
        var o = new List<GeometryPart>();
        foreach (var (index, from, to) in bands)
        {
            var layer = set.Layers[index];
            // The outer bands reach past the body, so its faces on them are kept whole.
            var a = Math.Abs(from - lo) < 1e-7 ? from - 1 : from;
            var b = Math.Abs(to - hi) < 1e-7 ? to + 1 : to;
            var slab = Slab(axis, centre, a, b, reach);
            var pieces = new List<Solid>();
            foreach (var p in parts)
            {
                Solid? cut;
                try { cut = booleans.Intersection(p.Solid, slab, $"layer:{index}"); }
                catch (Exception) { return parts; }
                if (cut is { Faces.Length: > 0 }) pieces.Add(cut);
            }
            if (pieces.Count == 0) continue;
            var solid = pieces.Count == 1 ? pieces[0] : Join(pieces);
            var color = Materials.DefaultColor(layer.Material) is var c && c != "#c9ced6" ? c : parts[0].Color;
            o.Add(new GeometryPart(layer.Name, color, solid, layer.Material, $"layer:{index}"));
        }
        return o.Count >= 2 ? o : parts;
    }

    /// <summary>The body's extent along the axis and its box, when the layers' total (in one of the file's possible units) fills that extent.</summary>
    static (double Lo, double Hi, Vec3 Min, Vec3 Max)? Fit(IReadOnlyList<GeometryPart> parts, Vec3 axis, Set set)
    {
        double lo = double.MaxValue, hi = double.MinValue;
        Vec3 bmin = new(double.MaxValue, double.MaxValue, double.MaxValue), bmax = new(double.MinValue, double.MinValue, double.MinValue);
        foreach (var p in parts)
            foreach (var q in p.Solid.Vertices)
            {
                var d = Vec3.Dot(q, axis);
                lo = Math.Min(lo, d); hi = Math.Max(hi, d);
                bmin = new(Math.Min(bmin.X, q.X), Math.Min(bmin.Y, q.Y), Math.Min(bmin.Z, q.Z));
                bmax = new(Math.Max(bmax.X, q.X), Math.Max(bmax.Y, q.Y), Math.Max(bmax.Z, q.Z));
            }
        var extent = hi - lo;
        var total = set.Layers.Sum(l => l.Thickness);
        if (!(extent > 1e-4) || !(total > 0)) return null;
        var best = Units.Min(u => Math.Abs(total * u - extent));
        return best <= Math.Max(FitTolerance, extent * 0.01) ? (lo, hi, bmin, bmax) : null;
    }

    static bool Symmetric(Set s)
    {
        var l = s.Layers;
        for (int i = 0, j = l.Count - 1; i < j; i++, j--)
            if (Math.Abs(l[i].Thickness - l[j].Thickness) > 1e-9 || l[i].Sub != l[j].Sub) return false;
        return true;
    }

    /// <summary>A box between two planes across <paramref name="axis"/> (distances <paramref name="a"/> to <paramref name="b"/>), wide enough for the body.</summary>
    static Solid Slab(Vec3 axis, Vec3 centre, double a, double b, double reach)
    {
        var u = Vec3.Cross(axis, Math.Abs(axis.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0)).Normalized();
        var w = Vec3.Cross(axis, u).Normalized();
        var c0 = centre - axis * Vec3.Dot(centre, axis);
        Vec3 P(double d, double su, double sw) => c0 + axis * d + u * (su * reach) + w * (sw * reach);
        Vec3[] v = [P(a, -1, -1), P(a, 1, -1), P(a, 1, 1), P(a, -1, 1), P(b, -1, -1), P(b, 1, -1), P(b, 1, 1), P(b, -1, 1)];
        // Faces wound outward (axis, u, w right-handed).
        int[][] faces = [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]];
        return SolidBuilder.Make(v, faces.Select(f => new FaceInput(f)));
    }

    /// <summary>Several pieces as one solid (their faces side by side; they do not overlap).</summary>
    static Solid Join(List<Solid> pieces)
    {
        var verts = new List<Vec3>();
        var faces = new List<Face>();
        foreach (var s in pieces)
        {
            var off = verts.Count;
            verts.AddRange(s.Vertices);
            foreach (var f in s.Faces) faces.Add(f with { Outer = [.. f.Outer.Select(i => i + off)], Holes = [.. f.Holes.Select(h => h.Select(i => i + off).ToArray())] });
        }
        return new Solid([.. verts], [.. faces]);
    }

    // ------------------------------------------------------------ STEP text

    static string? Enum(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 2 && s[0] == '.' && s[^1] == '.' ? s[1..^1].ToUpperInvariant() : null;

    /// <summary>A STEP string ('…', '' for a quote) as text; null for $ or anything else.</summary>
    static string? Text(JsonNode? n)
    {
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s)) return null;
        s = s.Trim();
        if (s.StartsWith("IFC", StringComparison.Ordinal) && s.IndexOf('(') is var p && p > 0 && s.EndsWith(')')) s = s[(p + 1)..^1];
        if (s.Length < 2 || s[0] != '\'' || s[^1] != '\'') return null;
        var t = Unescape(s[1..^1].Replace("''", "'"));
        return t.Length > 0 ? t : null;
    }

    /// <summary>STEP's escapes as the IFC writers use them: \X2\…\X0\ (UTF-16 in hex), \X\hh (Latin-1), \\.</summary>
    static string Unescape(string s)
    {
        if (!s.Contains('\\')) return s;
        var b = new System.Text.StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\') { b.Append(s[i]); continue; }
            if (string.CompareOrdinal(s, i, "\\X2\\", 0, 4) == 0)
            {
                var end = s.IndexOf("\\X0\\", i + 4, StringComparison.Ordinal);
                if (end < 0) break;
                for (var k = i + 4; k + 3 < end; k += 4)
                    if (int.TryParse(s.AsSpan(k, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u)) b.Append((char)u);
                i = end + 3;
                continue;
            }
            if (string.CompareOrdinal(s, i, "\\X\\", 0, 3) == 0 && i + 4 < s.Length && int.TryParse(s.AsSpan(i + 3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var l))
            {
                b.Append((char)l);
                i += 4;
                continue;
            }
            if (i + 1 < s.Length && s[i + 1] == '\\') { b.Append('\\'); i++; continue; }
            b.Append(s[i]);
        }
        return b.ToString();
    }

    /// <summary>A STEP real (20., 2.E-2, IFCLENGTHMEASURE(0.02)) as a number.</summary>
    static double? Real(JsonNode? n)
    {
        if (n is not JsonValue v || !v.TryGetValue<string>(out var s)) return Json.Number(n);
        s = s.Trim();
        if (s.IndexOf('(') is var p && p > 0 && s.EndsWith(')')) s = s[(p + 1)..^1];
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
