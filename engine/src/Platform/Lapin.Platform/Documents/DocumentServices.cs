using Lapin.Model;

namespace Lapin.Platform.Documents;

/// <summary>
/// How a document writes its values: its language (the decimal separator, dates), the project's units, and the step the
/// project shows its areas to (decision 471; null: as measured).
/// </summary>
public sealed record DocFormat(string Lang, string Units, double? AreaStep = null)
{
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
