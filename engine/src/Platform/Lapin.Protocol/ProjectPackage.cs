using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>A session as a .lapin package (format 3): what every host writes, to a file or to bytes, with the project's versions.</summary>
public static class ProjectPackage
{
    /// <summary>The session as the parts of a package: what a save writes, what a version keeps.</summary>
    public static PackageBuild Build(Session session) =>
        LapinPackage.Build(session.Model, session.ModuleOf, id => Spatial.StoreyOf(session.Model, id)?.Id,
            $"Lapin {typeof(ProjectPackage).Assembly.GetName().Version}", session.Product.Id,
            session.Catalog.Modules.Select(m => new PackageModule(m.Id, m.SchemaVersion)).ToList());

    public static void Write(Session session, Stream output, PackageVersions? versions = null) => LapinPackage.Write(output, Build(session), versions);

    public static void Write(Session session, string path, PackageVersions? versions = null)
    {
        if (!path.EndsWith(LapinPackage.Suffix, StringComparison.OrdinalIgnoreCase)) path += LapinPackage.Suffix;
        LapinPackage.Write(path, Build(session), versions);
    }
}
