using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// How a document writes its values: its language (the decimal separator, dates), the project's units, and the step the
/// project shows its areas to (decision 471; null: as measured).
/// </summary>
public sealed record DocFormat(string Lang, string Units, double? AreaStep = null)
{
    /// <summary>The project's date format (<see cref="Lapin.Drawing.StyleBook.DateFormat"/>), which title blocks write too; null: the language's.</summary>
    public string? DateFormat { get; init; }
    /// <summary>The project's standards, for its levels (<see cref="Lapin.Drawing.Standards.FormatLevel(double, Lapin.Drawing.StyleBook, bool)"/>); null: two decimals in metres, or the units' imperial.</summary>
    public Lapin.Drawing.StyleBook? Book { get; init; }
    /// <summary>The project's level datum (relative, absolute or both: decision 867); null: the model's z as it is.</summary>
    public LevelDatum? Datum { get; init; }

    public static DocFormat Ro => new("ro", "metric");
}

/// <summary>A field a document reads, {{key}}: its label for the interface and its value now, already written (null: the project has none yet).</summary>
public sealed record DocField(string Key, string Label, string? Value);

/// <summary>A collection a document repeats over ({"kind": "repeat", "over": Key}), each item read through {{ItemPrefix.field}}.</summary>
public sealed record DocCollection(string Key, string Label, string ItemPrefix);

/// <summary>
/// The fields and collections a module gives documents (docs/plan-documents.md): a service through the platform, as
/// IScheduleSource is, so no module depends on another for them.
/// </summary>
public interface IDocumentFields
{
    IEnumerable<DocField> Fields(IQueryContext q, DocFormat f);
    IEnumerable<DocCollection> Collections => [];
    /// <summary>Each item of <paramref name="collection"/> as its fields, keys without the prefix ("name", "area").</summary>
    IEnumerable<IReadOnlyDictionary<string, string>> Items(IQueryContext q, string collection, DocFormat f) => [];
}

/// <summary>A table a document can show: "schedule:&lt;id&gt;" (a schedule of the project, as it is laid out) or "kind:&lt;kind&gt;".</summary>
public sealed record DocTableChoice(string Source, string Label);

/// <summary>The project's tables for documents: given by the module that owns the schedules (architecture); without it, the IScheduleSource tables.</summary>
public interface IDocumentTables
{
    IEnumerable<DocTableChoice> Choices(IQueryContext q);
    /// <summary>The table for a source, or null when there is none.</summary>
    ScheduleGrid? Table(IQueryContext q, string source);
}
