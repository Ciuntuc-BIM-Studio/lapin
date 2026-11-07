using System.Globalization;
using System.Text.RegularExpressions;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// A wall's thickness typed on the wall itself (report #107): in Lapin a wall is as thick as its type's layers, so a new
/// thickness gives the wall a type with the same build-up whose core makes up the difference (its finishes kept): the
/// project's type that has exactly that build-up, or one made from the wall's type (its attributes, sweeps and lintel
/// rules copied) and named after it with the new thickness ("Partition wall 130mm" → "Partition wall 150mm"). A wall
/// whose type has no layers keeps a thickness of its own.
/// </summary>
public static partial class WallThickness
{
    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)(\s*)mm\b", RegexOptions.IgnoreCase)]
    private static partial Regex Mm();

    public static void Set(IEdit edit, IEnumerable<string> ids, double thickness)
    {
        if (!(thickness > 0) || double.IsInfinity(thickness)) throw new CommandException("A wall's thickness must be positive.");
        var made = new Dictionary<string, Entity>();
        foreach (var id in ids.Distinct())
        {
            var wall = Spatial.Require(edit.Model, id, IfcClass.Wall);
            var type = Walls.TypeOf(edit.Model, wall.Id);
            var layers = type == null ? [] : LayerSets.LayersOf(edit.Model, type.Id);
            if (type == null || layers.Count == 0)
            {
                var attrs = wall.CloneAttrs();
                attrs["thickness"] = thickness;
                edit.Replace(wall.With(attrs: attrs));
                continue;
            }
            var target = Resized(layers, thickness);
            if (Same(layers, target)) continue;
            var key = Key(target);
            if (!made.TryGetValue(type.Id + "|" + key, out var next))
            {
                next = edit.Model.OfClass(IfcClass.WallType).FirstOrDefault(t => Key(LayerSets.LayersOf(edit.Model, t.Id)) == key)
                    ?? Make(edit, type, target, thickness);
                made[type.Id + "|" + key] = next;
            }
            Walls.SetType(edit, edit.Model.Get(wall.Id)!, next);
        }
    }

    /// <summary>The layers with the core (else the thickest layer) making the build-up <paramref name="thickness"/> thick.</summary>
    public static List<MaterialLayer> Resized(IReadOnlyList<MaterialLayer> layers, double thickness)
    {
        var core = layers.FirstOrDefault(l => l.Core) ?? layers.MaxBy(l => l.Thickness)!;
        var others = layers.Where(l => !ReferenceEquals(l, core)).Sum(l => l.Thickness);
        var size = Math.Round((thickness - others) * 1e6) / 1e6;
        if (!(size > 1e-4))
            throw new CommandException($"The finishes alone are {LayerSets.Millimetres(others)} mm thick: the wall needs more than that for its core.");
        return [.. layers.Select(l => ReferenceEquals(l, core) ? l with { Thickness = size } : l)];
    }

    /// <summary>A build-up as a text two equal ones share: each layer's material, thickness to a hundredth of a mm, core, height band, pieces and priority.</summary>
    public static string Key(IReadOnlyList<MaterialLayer> layers) => string.Join(";", layers.Select(l => string.Join("|",
        l.Material, Math.Round(l.Thickness * 1e5).ToString(CultureInfo.InvariantCulture), l.Core, l.From?.ToString("R", CultureInfo.InvariantCulture), l.To?.ToString("R", CultureInfo.InvariantCulture),
        l.Parts == null ? "" : string.Join(",", l.Parts.Select(p => $"{p.Material}:{p.Height.ToString("R", CultureInfo.InvariantCulture)}")), l.Priority)));

    static bool Same(IReadOnlyList<MaterialLayer> a, IReadOnlyList<MaterialLayer> b) => Key(a) == Key(b);

    /// <summary>The new type: the wall's type copied (its attributes but its mark) with the resized layers, under its name with the new thickness.</summary>
    static Entity Make(IEdit edit, Entity from, IReadOnlyList<MaterialLayer> layers, double thickness)
    {
        var mm = LayerSets.Millimetres(thickness);
        var name = Mm().IsMatch(from.Name) ? Mm().Replace(from.Name, m => mm + m.Groups[2].Value + "mm", 1) : $"{from.Name} {mm}mm";
        var taken = edit.Model.OfClass(IfcClass.WallType).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unique = name;
        for (var i = 2; taken.Contains(unique); i++) unique = $"{name} ({i})";
        var specs = layers.Select(l => LayerSets.SpecOf(edit.Model, l)).ToList();
        var type = WallTypes.Create(edit, unique, specs);
        var attrs = from.CloneAttrs();
        attrs.Remove("mark");
        edit.Replace(edit.Model.Get(type.Id)!.With(attrs: attrs));
        return edit.Model.Get(type.Id)!;
    }
}
