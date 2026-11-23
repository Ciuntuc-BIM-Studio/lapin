namespace Lapin.Drawing;

/// <summary>
/// The languages the words the engine writes on drawings by itself can be in (the interface's: en, ro, es, de, fr, da, it, pl,
/// pt, lo, zh). A drawing is printed, so its language is stored with what draws it (a legend's <c>lang</c>, the drafting
/// standards' <c>drawingLang</c>), never taken from whoever looks at it. The words themselves are <c>Lapin.Platform.DrawingWords</c>.
/// </summary>
public static class DrawingLanguage
{
    public static readonly string[] Languages = ["en", "ro", "es", "de", "fr", "da", "it", "pl", "pt", "lo", "zh"];

    /// <summary>A language code the drawing words know, or null.</summary>
    public static string? Known(string? lang) => lang != null && Languages.Contains(lang) ? lang : null;
}
