using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// Reinforcement's notation (decision 864), the first domain of the project's notations (<see cref="Notations"/>): the diameter's
/// symbol and how a label writes the count, the diameter, the step, the length and the steel, and the bar bending schedule's
/// headings. The defaults are exactly what Lapin wrote before („3Ø16 L=6000”, „41Ø8/150”, „Ø (mm)”); the presets are
/// indicative (the UK's H12-200 after BS 8666, the US's #13 @ 200 after ACI 318 with ASTM A615M's metric sizes, the spaced
/// „Ø 10 c/c 200”), each a starting point the user checks and changes. A label's parts still say what it holds and in which
/// order (<see cref="LabelStyle.Parts"/>): the diameter written straight after the count, the step straight after the
/// diameter, the rest a space apart, so a template that wants a space starts with one.
/// </summary>
public static class RebarNotation
{
    public const string Id = "rebar";

    public static readonly NotationDomain Domain = new(Id, "Reinforcement",
    [
        new("symbol", "Diameter symbol", "Ø", [], NotationKey.Symbol),
        new("diameter", "Diameter", "{symbol}{d}", ["symbol", "d"]),
        new("count", "Count", "{count}", ["count"]),
        new("step", "Spacing", "/{step}", ["step"]),
        new("length", "Length", "L={length}", ["length"]),
        new("steel", "Steel grade", "{steel}", ["steel"]),
        new("schedule.title", "Schedule title", "Bar bending schedule", []),
        new("heading.partition", "Heading: partition", "Partition", []),
        new("heading.mark", "Heading: mark", "Mark", []),
        new("heading.diameter", "Heading: diameter", "{symbol} (mm)", ["symbol"]),
        new("heading.shape", "Heading: shape", "Shape", []),
        new("heading.count", "Heading: number", "No.", []),
        new("heading.length", "Heading: length", "Length (m)", []),
        new("heading.total", "Heading: total length", "Total (m)", []),
        new("heading.mass", "Heading: mass", "Mass (kg)", []),
        new("heading.note", "Heading: note", "Note", []),
        new("schedule.total", "Total row", "Total", []),
        new("schedule.differs", "Note: a mark that differs", "{symbol} or length differs", ["symbol"]),
    ],
    [
        new("ro", "Romania, Eurocode (Lapin's)", "SR EN 1992-1-1, Romanian practice", new Dictionary<string, string>()),
        new("uk", "United Kingdom", "BS 8666 (H for high-yield bars, the spacing after a dash)", new Dictionary<string, string>
        {
            ["symbol"] = "H", ["step"] = "-{step}", ["heading.mark"] = "Bar mark", ["heading.diameter"] = "Size (mm)", ["heading.count"] = "No. of bars",
            ["schedule.differs"] = "Size or length differs",
        }),
        new("us", "United States", "ACI 318, ASTM A615M metric bar sizes (#13 for 13 mm)", new Dictionary<string, string>
        {
            ["symbol"] = "#", ["diameter"] = " {symbol}{d}", ["step"] = " @ {step}", ["heading.diameter"] = "Bar size", ["heading.count"] = "Qty",
            ["heading.mass"] = "Weight (kg)", ["schedule.differs"] = "Size or length differs",
        }),
        new("cc", "Spacing centre to centre", "12 Ø 10 c/c 200", new Dictionary<string, string>
        {
            ["diameter"] = " {symbol} {d}", ["step"] = " c/c {step}",
        }),
    ],
    new Dictionary<string, string> { ["d"] = "12", ["count"] = "4", ["step"] = "150", ["length"] = "2500", ["steel"] = "B500C" });

    /// <summary>Lapin's own: what a project with no notation writes.</summary>
    public static readonly Notation Default = Notation.Default(Domain);

    public static void Register(IModuleRegistry r) => r.Service(Domain);
}
