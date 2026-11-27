using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What every type-making command takes besides its own arguments (issues #146 and #147, as createWallType has since issue #32):
/// <c>mark</c>, the type's mark (as element.setMark writes it: trimmed, empty for none, 80 characters at most), and <c>params</c>,
/// its parameters ([{name, kind, value, pset?, options?, instance?}], checked as <see cref="Params.ParseList"/> checks them; an object there
/// stays a catalogue profile's values, as before). Both
/// are set on the new type in the command's own step, so its elements have them from the start and one undo takes it all away.
/// </summary>
public static class NewTypeArgs
{
    /// <summary>The checked mark and parameters of <paramref name="a"/>, read before the type is made so a wrong one makes nothing.</summary>
    public static (string? Mark, JsonArray Params) Read(JsonObject a, string typeClass)
    {
        if (a["mark"] is { } given && (given is not JsonValue gv || !gv.TryGetValue<string>(out _))) throw new CommandException("A mark is a text.");
        var mark = ((string?)a["mark"])?.Trim() is { Length: > 0 } m ? m : null;
        if (mark is { Length: > 80 }) throw new CommandException("A mark is 80 characters at most.");
        // An object under `params` is a catalogue profile's values (Profiles.FromArgs: a strip footing of a ready-made profile), not the type's parameters.
        return (mark, a["params"] is JsonObject ? [] : Params.ParseList(a["params"], typeClass));
    }

    /// <summary>The type with the mark and parameters <see cref="Read"/> gave (nothing changed when there are none).</summary>
    public static Entity Apply(IEdit edit, Entity type, (string? Mark, JsonArray Params) given)
    {
        if (given.Mark == null && given.Params.Count == 0) return type;
        var now = edit.Model.Get(type.Id) ?? type;
        var attrs = now.CloneAttrs();
        if (given.Mark != null) attrs["mark"] = given.Mark;
        if (given.Params.Count > 0) attrs["params"] = given.Params;
        var made = now.With(attrs: attrs);
        edit.Replace(made);
        return made;
    }

    /// <summary>The id result of a type-making command, the type given its mark and parameters.</summary>
    public static JsonObject Made(IEdit edit, Entity type, (string? Mark, JsonArray Params) given) =>
        new() { ["id"] = Apply(edit, type, given).Id };
}
