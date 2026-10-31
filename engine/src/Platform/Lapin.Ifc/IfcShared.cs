using Lapin.Geometry;

namespace Lapin.Ifc;

/// <summary>
/// Bodies shared between products (docs/plan-instancing.md): a type's map
/// every occurrence places, or one product definition shape many products
/// use. Such a body is evaluated once, in its own coordinates, and each
/// product keeps only where it stands.
/// </summary>
public sealed partial class IfcModel
{
    Dictionary<string, int>? _uses;

    /// <summary>
    /// What <paramref name="product"/>'s body is shared by, if anything: a key
    /// (<c>map:</c> a representation map with the mapped item's colour, or
    /// <c>maps:</c> several, each with its colour and where it stands from the first, or
    /// <c>shape:</c> a product definition shape) used by two or more products,
    /// and where this one places it. Not for a product with openings (cut,
    /// its body is its own), nor one placed mirrored (drawn inside out by a
    /// shared mesh).
    /// </summary>
    public (string Key, Xf At)? SharedKey(int product)
    {
        if (File.Entities.GetValueOrDefault(product) is not { } e || e.Args.Count < 7) return null;
        if (_openings.ContainsKey(product)) return null;
        if (KeyOf(e) is not { } k) return null;
        _uses ??= CountUses();
        if (_uses.GetValueOrDefault(k.Key) < 2) return null;
        var (x, y, z) = (k.At.X, k.At.Y, k.At.Z);
        if (Vec3.Dot(Vec3.Cross(x, y), z) <= 0) return null;
        return k;
    }

    (string Key, Xf At)? KeyOf(StepEntity e)
    {
        if (File.Get(e[6]) is not { Type: "IFCPRODUCTDEFINITIONSHAPE" } shape) return null;
        var items = BodyItems(shape).ToList();
        if (items.Count == 0) return null;
        var place = Placement(e[5]);
        // One mapped item: the type's map, wherever this occurrence puts it.
        if (items is [{ Type: "IFCMAPPEDITEM" } mapped] && File.Get(mapped[0]) is { } map)
            return ($"map:{map.Id}:{_colours.GetValueOrDefault(mapped.Id)}", place.Then(Operator(File.Get(mapped[1]))).Then(Axis2(map[0])));
        // Several mapped items (a machine of many bodies, each its own map, as BIMCamel writes one): the same maps
        // standing the same way to one another are the same body, placed where the first one stands.
        if (items.Count > 1 && items.All(i => i.Type == "IFCMAPPEDITEM" && File.Get(i[0]) is not null))
        {
            var at = items.Select(i => Operator(File.Get(i[1])).Then(Axis2(File.Get(i[0])![0]))).ToList();
            if (Inverse(at[0]) is { } back && at.All(x => Vec3.Dot(Vec3.Cross(x.X, x.Y), x.Z) > 0))
                return ("maps:" + string.Join("|", items.Select((i, k) => $"{File.Get(i[0])!.Id};{_colours.GetValueOrDefault(i.Id)};{Numbers(back.Then(at[k]))}")), place.Then(at[0]));
        }
        return ($"shape:{shape.Id}", place);
    }

    /// <summary>The inverse of a turn with a uniform scale (square within a millionth), else null.</summary>
    static Xf? Inverse(Xf a)
    {
        var s2 = Vec3.Dot(a.X, a.X);
        if (s2 < 1e-24 || Math.Abs(Vec3.Dot(a.Y, a.Y) - s2) > 1e-6 * s2 || Math.Abs(Vec3.Dot(a.Z, a.Z) - s2) > 1e-6 * s2
            || Math.Abs(Vec3.Dot(a.X, a.Y)) > 1e-6 * s2 || Math.Abs(Vec3.Dot(a.Y, a.Z)) > 1e-6 * s2 || Math.Abs(Vec3.Dot(a.X, a.Z)) > 1e-6 * s2) return null;
        var k = 1 / s2;
        Xf t = new(new Vec3(a.X.X, a.Y.X, a.Z.X) * k, new Vec3(a.X.Y, a.Y.Y, a.Z.Y) * k, new Vec3(a.X.Z, a.Y.Z, a.Z.Z) * k, Vec3.Zero);
        return t with { O = t.Dir(a.O) * -1 };
    }

    /// <summary>A transform's 12 numbers in a key: to 8 significant digits, so that the same placing written twice is one key.</summary>
    static string Numbers(Xf x) => string.Join(",", new[] { x.X, x.Y, x.Z, x.O }.SelectMany(v => new[] { v.X, v.Y, v.Z })
        .Select(v => Math.Abs(v) < 1e-9 ? "0" : v.ToString("G8", System.Globalization.CultureInfo.InvariantCulture)));

    static Xf ParseXf(string text)
    {
        var n = text.Split(',').Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        return new Xf(new(n[0], n[1], n[2]), new(n[3], n[4], n[5]), new(n[6], n[7], n[8]), new(n[9], n[10], n[11]));
    }

    Dictionary<string, int> CountUses()
    {
        var uses = new Dictionary<string, int>();
        foreach (var e in File.Rooted)
            if (e.Args.Count >= 7 && e[0] is string { Length: 22 } && File.Get(e[6]) is { Type: "IFCPRODUCTDEFINITIONSHAPE" } && KeyOf(e) is { } k)
                uses[k.Key] = uses.GetValueOrDefault(k.Key) + 1;
        return uses;
    }

    IEnumerable<StepEntity> BodyItems(StepEntity shape)
    {
        foreach (var rep in Items(shape[2]))
        {
            if (rep[1] is string id && NotBody.Contains(id.ToUpperInvariant())) continue;
            foreach (var item in Items(rep[3])) yield return item;
        }
    }

    /// <summary>
    /// Every table the model fills as it is asked filled now (the placements, the shapes' uses, the 2D
    /// styles, the types), so that from here on it is only read: an import prepares its references on
    /// every core.
    /// </summary>
    public void Warm()
    {
        foreach (var p in File.OfType("IFCLOCALPLACEMENT")) Placement(new StepRef(p.Id));
        foreach (var p in Products) Placement(File.Entities[p.Id][5]);
        _uses ??= CountUses();
        Styles2d();
        TypeOf(0);
    }

    /// <summary>The body a <see cref="SharedKey"/> names, in its own coordinates, each mesh with its colour.</summary>
    public List<(MeshBuilder Mesh, string? Colour)> SharedBody(string key, ISolidBooleans? booleans = null)
    {
        var o = new List<(MeshBuilder Mesh, string? Colour)>();
        if (key.StartsWith("maps:", StringComparison.Ordinal))
        {
            foreach (var one in key[5..].Split('|'))
            {
                var f = one.Split(';');
                if (f.Length != 3 || !int.TryParse(f[0], out var mapId) || File.Entities.GetValueOrDefault(mapId) is not { } map) continue;
                // Where it stands from the first item, its map's origin included.
                var at = ParseXf(f[2]);
                foreach (var sub in Items(File.Get(map[1])?[3])) Item(sub, at, o, f[1].Length > 0 ? f[1] : null, booleans);
            }
            return o;
        }
        var parts = key.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var id) || File.Entities.GetValueOrDefault(id) is not { } source) return o;
        if (parts[0] == "map")
        {
            var colour = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : null;
            foreach (var sub in Items(File.Get(source[1])?[3])) Item(sub, Xf.Identity, o, colour, booleans);
        }
        else foreach (var item in BodyItems(source)) Item(item, Xf.Identity, o, null, booleans);
        return o;
    }
}
