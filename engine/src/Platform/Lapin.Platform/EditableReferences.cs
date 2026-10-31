using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// What makes a reference (an element drawn from the triangles a file brought) editable: Studio makes it a solid
/// of its own with the same id, class, name, properties and relationships, which then takes push/pull, fillets,
/// booleans and the rest (docs/spec-ifc.md, Importul, the third way). Null when it did; else why it stays a reference.
/// </summary>
public interface IReferenceEditor
{
    string? MakeEditable(IEdit edit, IServices services, Entity reference, int maxTriangles);
}

/// <summary>What an import made editable, and why the rest stayed references (reason → how many).</summary>
public sealed record EditableReport(int Made, IReadOnlyDictionary<string, int> Kept)
{
    public static readonly EditableReport None = new(0, new Dictionary<string, int>());

    /// <summary>As an import's result says it: {made, kept: {why: how many}}.</summary>
    public System.Text.Json.Nodes.JsonObject ToJson()
    {
        var kept = new System.Text.Json.Nodes.JsonObject();
        foreach (var (k, v) in Kept.OrderByDescending(kv => kv.Value)) kept[k] = v;
        return new() { ["made"] = Made, ["kept"] = kept };
    }
}

/// <summary>
/// The references an import brought, made editable where they can be (docs/spec-ifc.md, Importul: as references,
/// recognised, or everything editable). A body of more than <see cref="MaxTriangles"/> triangles (a pump, a fitting
/// another program meshed finely) stays a reference: a solid of it would weigh the project down for little.
/// </summary>
public static class EditableReferences
{
    public const int MaxTriangles = 5000;

    /// <summary><paramref name="apply"/> done, and the references it placed anew made editable.</summary>
    public static EditableReport Around(IEdit edit, IServices services, Action<IEdit> apply, int maxTriangles = MaxTriangles)
    {
        var before = edit.Model.Entities.Select(e => e.Id).ToHashSet();
        apply(edit);
        return Make(edit, services, edit.Model.Entities.Where(e => !before.Contains(e.Id)).Select(e => e.Id).ToList(), maxTriangles);
    }

    /// <summary>The references among <paramref name="ids"/> made editable.</summary>
    public static EditableReport Make(IEdit edit, IServices services, IEnumerable<string> ids, int maxTriangles = MaxTriangles)
    {
        if (services.Get<IReferenceEditor>() is not { } editor) return EditableReport.None;
        var made = 0;
        var kept = new Dictionary<string, int>();
        foreach (var id in ids.ToList())
        {
            if (edit.Model.Get(id) is not { } e || !StoredBodies.Has(e) || e.Str(Links.Key) != null) continue;
            if (editor.MakeEditable(edit, services, e, maxTriangles) is { } why) kept[why] = kept.GetValueOrDefault(why) + 1;
            else made++;
        }
        return new(made, kept);
    }
}
