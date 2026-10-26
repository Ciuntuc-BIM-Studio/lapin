using Lapin.Drawing.Views;

namespace Lapin.Drawing.Export;

/// <summary>A page to print: its size and its pens, in paper millimetres, y down from the top-left corner.</summary>
public sealed record PaperPage(double W, double H, IReadOnlyList<Pen> Pens, string Title = "", string LineCap = "round");
