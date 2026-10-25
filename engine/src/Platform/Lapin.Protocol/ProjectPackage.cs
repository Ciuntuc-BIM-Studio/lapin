using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>A session as a .lapin package (format 3): what every host writes, to a file or to bytes.</summary>
public static class ProjectPackage
{
    public static void Write(Session session, Stream output) =>
        LapinPackage.Write(output, session.Model, session.ModuleOf, id => Spatial.StoreyOf(session.Model, id)?.Id,
            $"Lapin {typeof(ProjectPackage).Assembly.GetName().Version}", session.Product.Id,
            session.Catalog.Modules.Select(m => new PackageModule(m.Id, m.SchemaVersion)).ToList());

    public static void Write(Session session, string path)
    {
        if (!path.EndsWith(LapinPackage.Suffix, StringComparison.OrdinalIgnoreCase)) path += LapinPackage.Suffix;
        var temp = path + ".saving";
        using (var file = File.Create(temp)) Write(session, file);
        File.Move(temp, path, overwrite: true);
    }
}
