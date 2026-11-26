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
            session.Catalog.Modules.Select(m => new PackageModule(m.Id, m.SchemaVersion)).ToList(),
            texts: session.Catalog.All<IPackageTexts>().SelectMany(s => s.Texts(session.Model).Select(t => new ModuleText(t.Path, t.Text, s.Module))));

    public static PackageWriteReport Write(Session session, Stream output, PackageVersions? versions = null, IssueFiles? issues = null, PackageForeign? foreign = null) =>
        LapinPackage.Write(output, Build(session), versions, issues, foreign);

    public static PackageWriteReport Write(Session session, string path, PackageVersions? versions = null, IssueFiles? issues = null, PackageForeign? foreign = null)
    {
        if (!path.EndsWith(LapinPackage.Suffix, StringComparison.OrdinalIgnoreCase)) path += LapinPackage.Suffix;
        return LapinPackage.Write(path, Build(session), versions, issues, foreign);
    }
}
