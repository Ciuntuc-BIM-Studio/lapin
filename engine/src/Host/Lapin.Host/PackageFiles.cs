using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Host;

/// <summary>Projects on disk: the .lapin package, format 3.</summary>
public static class PackageFiles
{
    public static ProjectFiles Create()
    {
        ProjectFiles? files = null;
        files = new((session, path) => { var d = Open(session, path); files!.LastNotes = LastNotes; return d; }, (session, path, versions) => ProjectPackage.Write(session, path, versions));
        return files;
    }

    /// <summary>What the last opening of an old project left out (format 2), for the interface to tell.</summary>
    public static List<string> LastNotes { get; private set; } = [];

    public static Diff Open(Session session, string path)
    {
        LastNotes = [];
        if (LegacyPackage.Is(path))
        {
            var imported = LegacyImport.Run(session, LegacyPackage.Read(path));
            LastNotes = imported.Notes;
            return imported.Diff;
        }
        var content = LapinPackage.Read(path);
        return session.Load(content.Entities, content.Relations, path, content.Classes);
    }

    /// <summary>The project written to <paramref name="path"/>, with its versions (those of the file it came from, unless given).</summary>
    public static void Save(Session session, string path, PackageVersions? versions = null) =>
        ProjectPackage.Write(session, path, versions ?? (session.Path is { } from ? PackageVersions.Of(from) : PackageVersions.None));
}
