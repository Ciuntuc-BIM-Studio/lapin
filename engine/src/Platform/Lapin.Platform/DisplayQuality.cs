using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The project's display quality (docs/plan-display-precision.md): how far the 3D view lets a heavy part's
/// coarser copy stray on screen, fast (3 pixels), balanced (1) or precise (none: every part as it is). Kept on
/// the project (<c>displayQuality</c>); it changes only what the screen draws, never the model, its drawings,
/// exports or quantities.
/// </summary>
public static class DisplayQuality
{
    public const string Key = "displayQuality";
    public static readonly string[] Values = ["fast", "balanced", "precise"];

    public static string Of(IModelView m) => Spatial.Project(m)?.Str(Key) is { } q && Values.Contains(q) ? q : "balanced";

    public static void Register(IModuleRegistry r)
    {
        r.Command("project.setDisplayQuality", "Display quality", (ctx, a) =>
        {
            var q = (string?)a["quality"] is { } v && Values.Contains(v) ? v : throw new CommandException($"The display quality is one of {string.Join(", ", Values)}.");
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var pa = project.CloneAttrs();
            pa[Key] = q;
            ctx.Replace(project.With(attrs: pa));
            return null;
        });
    }
}
