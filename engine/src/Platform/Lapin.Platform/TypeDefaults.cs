using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A type's instance defaults (a tester's report: fire rating is the type's, new / existing / demolish each element's): a
/// parameter of a type marked <c>instance: true</c> (<see cref="Params.IsInstanceDefault"/>) is not a property of the type
/// but what each element of it starts with. An element given the type in a command (placed with it, or its type changed)
/// that has no parameter of that set and name gets a copy of it, in the command's undo step, and may change it; nothing
/// is filled in afterwards on its own (an element made before the default was) until
/// <c>types.applyInstanceDefaults</c> is asked. An IFC import adds nothing (it reproduces the file): it is not a command.
/// </summary>
public static class TypeDefaults
{
    /// <summary>The type's instance defaults, as an element takes them (without the flag).</summary>
    public static List<JsonObject> Of(Entity type) =>
        (type.Attrs["params"] as JsonArray)?.OfType<JsonObject>().Where(Params.IsInstanceDefault).Select(p =>
        {
            var c = (JsonObject)p.DeepClone();
            c.Remove(Params.InstanceKey);
            return c;
        }).ToList() ?? [];

    /// <summary>The type's instance defaults the element lacks, given it; whether anything was.</summary>
    public static bool Fill(IEdit edit, Entity element, Entity type)
    {
        var defaults = Of(type);
        if (defaults.Count == 0) return false;
        var list = element.Attrs["params"] is JsonArray old ? (JsonArray)old.DeepClone() : new JsonArray();
        var added = false;
        foreach (var d in defaults)
        {
            if (list.OfType<JsonObject>().Any(p => Params.Same(p, Params.SetOf(d), (string)d["name"]!))) continue;
            list.Add((JsonNode)d);
            added = true;
        }
        if (!added) return false;
        var attrs = element.CloneAttrs();
        attrs["params"] = list;
        edit.Replace(element.With(attrs: attrs));
        return true;
    }

    /// <summary>After a command: each element that took a type in it (a new one, or one whose type changed) given that type's instance defaults it lacks.</summary>
    public static void Settle(IEdit edit)
    {
        var m = edit.Model;
        var given = new Dictionary<string, string>();
        foreach (var c in edit.RelationChanges)
        {
            if (c.After is not { Class: IfcClass.RelDefinesByType } rel) continue;
            var before = c.Before?.Relating == rel.Relating ? c.Before.Related.ToHashSet() : [];
            foreach (var id in rel.Related) if (!before.Contains(id)) given[id] = rel.Relating;
        }
        if (given.Count == 0) return;
        var typed = new Dictionary<string, Entity?>();
        foreach (var (id, typeId) in given)
        {
            if (!typed.TryGetValue(typeId, out var type)) typed[typeId] = type = m.Get(typeId) is { } t && Of(t).Count > 0 ? t : null;
            // Only where the element stands in that type still at the end of the command.
            if (type == null || m.Get(id) is not { } e || m.RelatingOf(id, IfcClass.RelDefinesByType)?.Id != typeId) continue;
            Fill(edit, e, type);
        }
    }

    public static void Register(IModuleRegistry r)
    {
        // {id} → {filled}: every element of the type given the instance defaults it lacks (those it has are its own); one undo step.
        r.Command("types.applyInstanceDefaults", "Apply instance defaults", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("Which type?");
            if (Of(type).Count == 0) throw new CommandException($"„{type.Name}” has no instance defaults: mark a parameter of the type as one.");
            var filled = 0;
            foreach (var e in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).ToList())
                if (Fill(ctx, e, type)) filled++;
            return new JsonObject { ["filled"] = filled };
        });
    }
}
