using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>A session as a .lapin package (format 3): what every host writes, to a file or to bytes, with the project's versions.</summary>
public static class ProjectPackage
{
    /// <summary>The session as the parts of a package: what a save writes, what a version keeps (the modules' texts too: a project's scripts).</summary>
    public static PackageBuild Build(Session session) => Build(session, session.Model);

    /// <summary>The package of <paramref name="model"/> (the session's, or a view of it: the "uncommitted" question's, EngineDispatcher.Compared).</summary>
    public static PackageBuild Build(Session session, Lapin.Model.IModelView model) =>
        LapinPackage.Build(model, session.ModuleOf, id => Spatial.StoreyOf(model, id)?.Id,
            $"Lapin {typeof(ProjectPackage).Assembly.GetName().Version}", session.Product.Id,
            Modules(session),
            texts: session.Catalog.All<IPackageTexts>().SelectMany(s => s.Texts(session.Model).Select(t => new ModuleText(t.Path, t.Text, s.Module))));

    /// <summary>
    /// The modules a package records: the edition's, each with the extension it came in; then the extension modules of the
    /// file opened that this edition lacks, carried over (<see cref="PackageModule.Absent"/>), so the project still says it needs them.
    /// </summary>
    public static List<PackageModule> Modules(Session session)
    {
        var ext = session.Catalog.Extensions;
        var o = session.Catalog.Modules.Select(m => new PackageModule(m.Id, m.SchemaVersion, ext.TryGetValue(m.Id, out var x) ? new PackageExtension(x.Id, x.Version) : null)).ToList();
        foreach (var a in session.AbsentExtensions()) o.Add(new PackageModule(a.Module, a.SchemaVersion, new PackageExtension(a.Id, a.Version), true));
        return o;
    }

    /// <summary>The extensions the project needs that this edition has not loaded, as <c>project.open</c> returns them.</summary>
    public static System.Text.Json.Nodes.JsonArray AbsentExtensions(Session session) =>
        new([.. session.AbsentExtensions().Select(x => (System.Text.Json.Nodes.JsonNode)new System.Text.Json.Nodes.JsonObject { ["module"] = x.Module, ["id"] = x.Id, ["version"] = x.Version })]);

    public static PackageWriteReport Write(Session session, Stream output, PackageVersions? versions = null, IssueFiles? issues = null, PackageForeign? foreign = null) =>
        LapinPackage.Write(output, Build(session), versions, issues, foreign);

    public static PackageWriteReport Write(Session session, string path, PackageVersions? versions = null, IssueFiles? issues = null, PackageForeign? foreign = null)
    {
        if (!path.EndsWith(LapinPackage.Suffix, StringComparison.OrdinalIgnoreCase)) path += LapinPackage.Suffix;
        return LapinPackage.Write(path, Build(session), versions, issues, foreign);
    }
}
