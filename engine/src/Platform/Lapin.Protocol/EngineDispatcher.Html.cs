using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// Lapin for the browser (decision 482, docs/html-export.md): <c>project.exportHtml</c> writes the project as one .html,
/// delivered as every export is (where the desktop's dialog says, else as bytes to download); <c>project.htmlPlan</c>
/// tells its dialog what it would take and weigh. Neither changes the model. The 3D model comes through Lapin.Mesh, in a
/// method of its own, so the browser's engine fetches that part only when a page takes the 3D model.
/// </summary>
public sealed partial class EngineDispatcher
{
    (object? Result, Diff? Diff) ExportHtml(JsonObject p, Func<byte[], string, JsonObject> deliver)
    {
        var o = HtmlExportOptions.From(p);
        ReportProgress("Writing the page", 0, 0);
        var (bytes, sizes) = HtmlExport.Write(session, files, o, Glb);
        var delivered = deliver(bytes, HtmlExport.FileName(session));
        delivered["sizes"] = sizes;
        return (delivered, null);
    }

    (object? Result, Diff? Diff) HtmlPlan(JsonObject p) =>
        (HtmlExport.Plan(session, files, (p["model3d"] is JsonValue v && v.TryGetValue<bool>(out var b) && !b) ? null : GlbSize, (string?)p["viewId"]), null);

    /// <summary>The model as GLB (base64), as the 3D view <paramref name="viewId"/> shows it (none: the whole model), and the elements it holds; null when nothing is shown.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static (string Base64, IReadOnlyCollection<string> Ids)? Glb(Session session, string? viewId)
    {
        var scene = Lapin.Mesh.MeshScene.Build(session, new Lapin.Mesh.MeshExportOptions("glb", viewId));
        if (scene.ElementCount == 0) return null;
        var ids = scene.Groups.SelectMany(g => g.Elements).SelectMany(e => e.Flatten()).Select(e => e.GlobalId).ToHashSet();
        return (Convert.ToBase64String(Lapin.Mesh.GltfWriter.Glb(scene)), ids);
    }

    /// <summary>What the GLB weighs in the page (base64), worked out as it would be written.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static long GlbSize(Session session, string? viewId) => Glb(session, viewId) is { } g ? g.Base64.Length : 0;
}
