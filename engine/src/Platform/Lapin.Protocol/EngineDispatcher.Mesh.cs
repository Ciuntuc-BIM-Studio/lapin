using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// The calls that use Lapin.Mesh (3D exports, Fragments, BIM Open Schema, meshes from other programs), apart from
/// <see cref="Dispatch"/>: each in a method of its own, never inlined, so that a call that does not use them touches
/// nothing of that assembly. The browser's engine downloads it the first time one of them runs
/// (docs/plan-browser-engine.md, Lazy parts); the desktop runs them as before.
/// </summary>
public sealed partial class EngineDispatcher
{
    sealed record FragSurveyed(string Key, Lapin.Mesh.Fragments.FragModel Model);
    sealed record BosSurveyed(string Key, Lapin.Mesh.Bos.BosView View);

    static Lapin.Mesh.Fragments.FragImportOptions ImportOptions(JsonObject p) =>
        new(Chosen(p["kinds"]), Chosen(p["storeys"]), p["group"] is not JsonValue gv || !gv.TryGetValue<bool>(out var gb) || gb,
            Editable: (string?)p["mode"] == "editable" || p["editable"] is JsonValue ev && ev.TryGetValue<bool>(out var eb) && eb,
            // "auto" and "editable": recognised from their triangles where a module takes them; "reference" (the default): as the file draws them.
            Recognize: (string?)p["mode"] is "auto" or "editable");

    /// <summary>The model as a 3D mesh (docs/plan-mesh-export.md): glb, gltf, usdz, usda or dae; a 3D view's visible elements, or all; a phase; edges; the origin.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    (object? Result, Diff? Diff) ExportMesh(JsonObject p, Func<byte[], string, JsonObject> deliver)
    {
        var options = Lapin.Mesh.MeshExportOptions.From(p);
        // The kinds and storeys chosen (the export survey's), left out as the hidden are.
        if (p["kinds"] is JsonArray || p["storeys"] is JsonArray)
        {
            // A mesh is for showing the model: its entourage goes with it unless the kinds chosen leave it out.
            var q = (JsonObject)p.DeepClone();
            q["entourage"] = true;
            options = options with { Hidden = Lapin.Ifc.ModelSurvey.Excluded(session, q) };
        }
        var (bytes, result) = Lapin.Mesh.MeshExport.Write(session, options);
        var name = $"{(session.Path is { } sp ? Path.GetFileNameWithoutExtension(sp) : "project")}.{options.Extension}";
        var delivered = deliver(bytes, name);
        foreach (var (k, v) in result.ToJson()) delivered[k] = v?.DeepClone();
        return (delivered, null);
    }

    /// <summary>What a Fragments file holds, in the IFC survey's shape, kept read for the import that follows.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    (object? Result, Diff? Diff) FragSurvey(JsonObject p)
    {
        var (bytes, name, _, key) = IfcBytes(p);
        ReportProgress("Reading the file", 0, 0);
        var model = Lapin.Mesh.Fragments.FragImport.Read(bytes, name);
        _fragSurveyed = new FragSurveyed(key, model);
        return (Lapin.Mesh.Fragments.FragSurvey.Of(model, name, bytes.Length), null);
    }

    /// <summary>
    /// A That Open Fragments file (.frag, docs/fragments.md) into the open project: its items as references with their bodies, by path or by its bytes;
    /// the kinds and storeys chosen (the survey's), as one group unless asked otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    (object? Result, Diff? Diff) ImportFragments(JsonObject p)
    {
        ReportProgress("Reading the file", 0, 0);
        var (bytes, name, _, key) = IfcBytes(p);
        var options = ImportOptions(p);
        var model = _fragSurveyed is FragSurveyed fs && fs.Key == key ? fs.Model : Lapin.Mesh.Fragments.FragImport.Read(bytes, name);
        _fragSurveyed = null;
        var r = Lapin.Mesh.Fragments.FragImport.Run(session, model, name, out var imported, options);
        return (r.ToJson(), imported);
    }

    /// <summary>What a BIM Open Schema file holds (docs/bos.md), in the IFC survey's shape, kept read for the import that follows.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    (object? Result, Diff? Diff) BosSurvey(JsonObject p)
    {
        var (bytes, name, _, key) = IfcBytes(p);
        ReportProgress("Reading the file", 0, 0);
        var view = Lapin.Mesh.Bos.BosImport.Read(bytes, name);
        _bosSurveyed = new BosSurveyed(key, view);
        return (Lapin.Mesh.Bos.BosSurvey.Of(view, name, bytes.Length), null);
    }

    /// <summary>
    /// A BIM Open Schema file (.bos, docs/bos.md) into the open project, as a Fragments file comes in: references with their bodies,
    /// the kinds and storeys chosen, as one group unless asked otherwise; recognised or made editable on request.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    (object? Result, Diff? Diff) ImportBos(JsonObject p)
    {
        ReportProgress("Reading the file", 0, 0);
        var (bytes, name, _, key) = IfcBytes(p);
        var options = ImportOptions(p);
        var view = _bosSurveyed is BosSurveyed bs && bs.Key == key ? bs.View : Lapin.Mesh.Bos.BosImport.Read(bytes, name);
        _bosSurveyed = null;
        var r = Lapin.Mesh.Bos.BosImport.Run(session, view, name, out var imported, options);
        return (r.ToJson(), imported);
    }

    /// <summary>A mesh from another program (OBJ, STL, glTF, GLB) into the open project as references (MeshImport); a .gltf's buffers beside it read from its folder.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    (object? Result, Diff? Diff) ImportMesh(JsonObject p)
    {
        ReportProgress("Reading the file", 0, 0);
        var (bytes, name, path, _) = IfcBytes(p);
        var r = Lapin.Mesh.MeshImport.Run(session, bytes, name, p, path != null ? Path.GetDirectoryName(path) : null, out var imported);
        return (r.ToJson(), imported);
    }
}
