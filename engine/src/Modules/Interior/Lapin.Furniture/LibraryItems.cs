using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Furniture;

/// <summary>
/// A furniture type as the user's own furniture library keeps it (docs/plan-furniture.md, Configuratorul flexibil, stage 3;
/// decision 476): what the type is, taken out of its project so it can go into another. The item holds the type's name, its
/// IFC PredefinedType and its <c>spec</c> as they are (whatever kind: casework, a kitchen, a sofa, later a mesh-based piece,
/// whose spec names its model, sizes and materials), and every material the spec names, with its attributes (colour,
/// category, graphics, data) and its image when it has one, so the type looks the same where it lands.
/// <para>
/// Inserted into a project (<see cref="Insert"/>), a material the project already has by name (case aside) is used as it is;
/// one it has not is made from the item. A type already there with the same spec and IFC type is reused, under whatever name
/// it has now; otherwise a new <c>IfcFurnitureType</c> is made with a GlobalId of its own. One command, one undo step.
/// </para>
/// </summary>
public static class LibraryItems
{
    /// <summary>What an item is: the class of the type it holds.</summary>
    public const string ItemClass = FurnitureModule.TypeClass;

    /// <summary>The attributes of a material that name another entity of its project: carried by value (the image), never by id.</summary>
    static readonly string[] ByReference = ["textureImage"];

    /// <summary>A furniture type as a library item: {class, name, predefinedType, spec, materials: [{name, attrs, image?}]}.</summary>
    public static JsonObject Of(IModelView m, Entity type)
    {
        if (type.Class != FurnitureModule.TypeClass) throw new CommandException("Only a furniture type goes into the furniture library.");
        var spec = type.Attrs["spec"] as JsonObject ?? throw new CommandException("The furniture type has no spec.");
        var materials = new JsonArray();
        foreach (var name in MaterialNames(spec))
        {
            if (Materials.Find(m, name) is not { } mat) { materials.Add((JsonNode)new JsonObject { ["name"] = name }); continue; }
            var attrs = mat.CloneAttrs();
            foreach (var k in ByReference) attrs.Remove(k);
            var o = new JsonObject { ["name"] = mat.Name, ["attrs"] = attrs };
            if (Images.Of(m, mat) is { } image)
                o["image"] = new JsonObject { ["name"] = image.Name, ["mime"] = image.Str("mime"), ["data"] = image.Str("data") };
            materials.Add((JsonNode)o);
        }
        var item = new JsonObject
        {
            ["class"] = ItemClass,
            ["name"] = type.Name,
            ["spec"] = spec.DeepClone(),
            ["materials"] = materials,
        };
        if (type.PredefinedType != null) item["predefinedType"] = type.PredefinedType;
        // A model of the user's travels with it, its file inside (docs/plan-user-extensibility.md §5.5).
        if (UserModels.Carry(m, (string?)spec[FurnitureModels.Key]) is { } model) item["model"] = model;
        return item;
    }

    /// <summary>
    /// The materials a spec names, by name, in the kind's role order: each role's (the kind's default when the spec names none),
    /// then any other the spec's <c>materials</c> names (a kind this build does not know, a role added later).
    /// </summary>
    public static IReadOnlyList<string> MaterialNames(JsonObject spec)
    {
        var names = new List<string>();
        void Add(string? n)
        {
            if (string.IsNullOrWhiteSpace(n)) return;
            if (!names.Contains(n, StringComparer.OrdinalIgnoreCase)) names.Add(n);
        }
        var key = (string?)(spec["kind"] as JsonValue);
        if (FurnitureKinds.Find(key) is { } kind)
        {
            var s = new Spec(spec);
            foreach (var role in kind.Roles) Add(s.MaterialName(role));
        }
        if (spec["materials"] is JsonObject given)
            foreach (var (_, v) in given) Add(v is JsonValue jv && jv.TryGetValue<string>(out var n) ? n : null);
        return names;
    }

    /// <summary>
    /// An item in the project: its materials matched by name or made, the type reused when an identical one is there (same spec,
    /// same IFC type), else made under a fresh GlobalId and a free name. Returns the type and whether it was reused.
    /// </summary>
    public static (Entity Type, bool Reused) Insert(IEdit edit, JsonObject item)
    {
        if ((string?)item["class"] is { } cls && cls != ItemClass) throw new CommandException($"This library item is a {cls}, not a furniture type.");
        var spec = item["spec"] as JsonObject ?? throw new CommandException("The library item has no spec.");
        var key = (string?)(spec["kind"] as JsonValue);
        if (FurnitureKinds.Find(key) is null)
            throw new CommandException($"This piece of furniture is of a kind („{key}”) this edition of Lapin does not build.");
        UserModels.Bring(edit, item["model"] as JsonObject);
        var predefined = (string?)item["predefinedType"] ?? FurnitureKinds.IfcOf(new Spec(spec)).Predefined;

        // Its materials: the project's own when it has one of the name, else made as the item keeps it.
        if (item["materials"] is JsonArray mats)
            foreach (var node in mats)
            {
                if (node is not JsonObject mo || ((string?)mo["name"])?.Trim() is not { Length: > 0 } name) continue;
                if (Materials.Find(edit.Model, name) != null) continue;
                var attrs = mo["attrs"] is JsonObject a ? (JsonObject)a.DeepClone() : new JsonObject();
                foreach (var k in ByReference) attrs.Remove(k);
                if ((string?)attrs["color"] is not { Length: 7 }) attrs["color"] = FurnitureKinds.ColorOf(name) ?? Materials.DefaultColor(name);
                if (mo["image"] is JsonObject img && (string?)img["data"] is { Length: > 0 } data && (string?)img["mime"] is { } mime && Images.Mimes.Contains(mime))
                {
                    var image = new Entity(edit.NewId(), Images.Class, ((string?)img["name"])?.Trim() is { Length: > 0 } iname ? iname : name,
                        new JsonObject { ["mime"] = mime, ["data"] = data });
                    edit.Add(image);
                    attrs["textureImage"] = image.Id;
                }
                edit.Add(new Entity(edit.NewId(), IfcClass.Material, name, attrs));
            }
        // A material the spec names that the item did not carry (an item written by hand): made from its name, as a new type's are.
        foreach (var name in MaterialNames(spec)) Materials.Ensure(edit, name, FurnitureKinds.ColorOf(name));

        var canon = Canonical(spec);
        var same = edit.Model.OfClass(FurnitureModule.TypeClass)
            .FirstOrDefault(t => t.Attrs["spec"] is JsonObject ts && t.PredefinedType == predefined && Canonical(ts) == canon);
        if (same != null) return (same, true);

        var wanted = ((string?)item["name"])?.Trim() is { Length: > 0 } n2 ? n2 : "Furniture";
        var type = new Entity(edit.NewId(), FurnitureModule.TypeClass, FreeName(edit.Model, wanted), new JsonObject { ["spec"] = spec.DeepClone() }, predefined);
        edit.Add(type);
        return (type, false);
    }

    static string FreeName(IModelView m, string name)
    {
        var taken = m.OfClass(FurnitureModule.TypeClass).Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var i = 2; ; i++) if (!taken.Contains($"{name} {i}")) return $"{name} {i}";
    }

    /// <summary>A spec as one text whatever order its keys were written in and however its numbers were spelled: what "identical" compares.</summary>
    public static string Canonical(JsonNode? node)
    {
        var sb = new System.Text.StringBuilder();
        Write(node, sb);
        return sb.ToString();
    }

    static void Write(JsonNode? node, System.Text.StringBuilder sb)
    {
        switch (node)
        {
            case null: sb.Append("null"); break;
            case JsonObject o:
                sb.Append('{');
                var first = true;
                foreach (var (k, v) in o.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(JsonValue.Create(k).ToJsonString()).Append(':');
                    Write(v, sb);
                }
                sb.Append('}');
                break;
            case JsonArray a:
                sb.Append('[');
                for (var i = 0; i < a.Count; i++) { if (i > 0) sb.Append(','); Write(a[i], sb); }
                sb.Append(']');
                break;
            case JsonValue v:
                if (Json.Number(v) is { } d && v.GetValueKind() == System.Text.Json.JsonValueKind.Number) sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                else sb.Append(v.ToJsonString());
                break;
        }
    }
}
