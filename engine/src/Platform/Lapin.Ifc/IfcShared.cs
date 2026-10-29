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
        return ($"shape:{shape.Id}", place);
    }

    Dictionary<string, int> CountUses()
    {
        var uses = new Dictionary<string, int>();
        foreach (var e in File.Entities.Values)
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

    /// <summary>The body a <see cref="SharedKey"/> names, in its own coordinates, each mesh with its colour.</summary>
    public List<(MeshBuilder Mesh, string? Colour)> SharedBody(string key, ISolidBooleans? booleans = null)
    {
        var o = new List<(MeshBuilder Mesh, string? Colour)>();
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
