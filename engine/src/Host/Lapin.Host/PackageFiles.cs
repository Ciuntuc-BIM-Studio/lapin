using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>Projects on disk: the .lapin package, format 3.</summary>
public static class PackageFiles
{
    public static ProjectFiles Create() => new(Open, Save);

    public static Diff Open(Session session, string path)
    {
        var content = LapinPackage.Read(path);
        return session.Load(content.Entities, content.Relations, path, content.Classes);
    }

    public static void Save(Session session, string path)
    {
        if (!path.EndsWith(LapinPackage.Suffix, StringComparison.OrdinalIgnoreCase)) path += LapinPackage.Suffix;
        LapinPackage.Write(path, session.Model, session.ModuleOf, id => Spatial.StoreyOf(session.Model, id)?.Id,
            $"Lapin {typeof(PackageFiles).Assembly.GetName().Version}", session.Product.Id,
            session.Catalog.Modules.Select(m => new PackageModule(m.Id, m.SchemaVersion)).ToList());
    }
}
