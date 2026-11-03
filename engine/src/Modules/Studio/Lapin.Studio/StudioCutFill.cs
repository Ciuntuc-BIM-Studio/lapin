using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A Studio solid cut by a plan, a section or an elevation, without a fill
/// from its material: filled with the paper's white, so the shape a sketch
/// became reads at once as a solid (the sketch alone is only its lines).
/// </summary>
sealed class StudioCutFill : ICutFill
{
    public const string Paper = "#ffffff";

    public string? FillOf(IModelView model, Entity element) => element.Str("operation") != null && !StudioModule.IsVoid(element) ? Paper : null;
}
