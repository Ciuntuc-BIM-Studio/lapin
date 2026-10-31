using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Studio;

/// <summary>
/// A reference an import brought made a Studio solid of its triangles (EditableReferences, the import's third way):
/// the same element, its body now its own, which takes push/pull, fillets, booleans and the rest. A body over the
/// limit, one with no triangles (only the file's 2D), or one on no storey stays a reference, and says why.
/// </summary>
sealed class ReferenceSolids : IReferenceEditor
{
    public string? MakeEditable(IEdit edit, IServices services, Entity e, int maxTriangles)
    {
        if (Spatial.StoreyOf(edit.Model, e.Id) == null) return "on no storey";
        var triangles = TrianglesOf(edit.Model, e);
        if (triangles == 0) return "no body (only its 2D)";
        if (triangles > maxTriangles) return $"more than {maxTriangles:N0} triangles";
        if (StoredBodies.Geometry(e, edit.Model, services) is not { Parts.Count: > 0 } g) return "no body (only its 2D)";
        StudioModule.MakeSolid(edit, e, g.Parts, keep: false);
        return null;
    }

    /// <summary>How many triangles the stored body holds (its own, or the shared one it is an instance of), counted without building it.</summary>
    static int TrianglesOf(IModelView m, Entity e)
    {
        var body = StoredBodies.InstanceOf(e) is var (shared, _) ? m.Get(shared)?.Attrs[StoredBodies.Key] as JsonObject : e.Attrs[StoredBodies.Key] as JsonObject;
        return (body?["parts"] as JsonArray)?.OfType<JsonObject>().Sum(p => StoredBodies.ReadPart(p).T.Length / 3) ?? 0;
    }
}
