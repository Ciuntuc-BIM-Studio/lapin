using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's colour palette (forum topic 97): the swatches the colour picker offers to everyone who opens the project.
/// Kept on the project (<c>colourPalette</c>, a list of #rrggbb) so it travels in the .lapin; it changes only what a picker
/// offers, never an element. One undo step per change.
/// </summary>
public static partial class ColourPalette
{
    public const string Key = "colourPalette";
    public const int Max = 256;

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Hex();

    /// <summary>The palette: lower-case #rrggbb, in order, each once.</summary>
    public static List<string> Of(IModelView m)
    {
        var list = new List<string>();
        if (Spatial.Project(m)?.Attrs[Key] is JsonArray a)
            foreach (var n in a)
                if (n is JsonValue v && v.TryGetValue<string>(out var s) && Hex().IsMatch(s) && !list.Contains(s.ToLowerInvariant())) list.Add(s.ToLowerInvariant());
        return list;
    }

    public static void Register(IModuleRegistry r)
    {
        // The whole palette as the picker's editor left it; an empty list clears it.
        r.Command("project.setColourPalette", "Colour palette", (ctx, a) =>
        {
            var raw = a["colours"] as JsonArray ?? throw new CommandException("Which colours?");
            var list = new List<string>();
            foreach (var n in raw)
            {
                var s = (n as JsonValue)?.TryGetValue<string>(out var t) == true ? t : null;
                if (s == null || !Hex().IsMatch(s)) throw new CommandException($"„{s}” is not a colour (#rrggbb).");
                s = s.ToLowerInvariant();
                if (!list.Contains(s)) list.Add(s);
            }
            if (list.Count > Max) throw new CommandException($"A palette holds at most {Max} colours.");
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            if (list.Count > 0) attrs[Key] = new JsonArray(list.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray()); else attrs.Remove(Key);
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });
    }
}
