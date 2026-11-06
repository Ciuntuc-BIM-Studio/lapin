using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// The spatial structure's identity (docs/spec-ifc.md, Structura spațială). An IFC opened as a project (<see cref="Open"/>)
/// makes a new project whose project, site, building and storeys are the file's, under its GlobalIds and names, with the
/// GlobalIds of the aggregations and containments that hold them; exported again, they are what the file said. Imported into
/// an existing project (<see cref="Run(Session, StepFile, string, IfcImportOptions?, out Diff)"/>), the project's own
/// project, site, building and matching storeys stay, and a storey the file brings new keeps the file's GlobalId.
/// </summary>
public static partial class IfcImport
{
    /// <summary>
    /// An IFC file opened as a project: a new project made from its spatial structure (<see cref="SeedOf"/>,
    /// <see cref="Session.New(SpatialSeed)"/>), then the file imported into it as one undo step. <paramref name="diff"/> is
    /// one reset: the new project with what the import brought.
    /// </summary>
    public static IfcImportResult Open(Session session, StepFile file, string name, IfcImportOptions? options, out Diff diff)
    {
        var start = session.New(SeedOf(file));
        var result = Run(session, file, name, options, out var imported);
        diff = start.Then(imported);
        return result;
    }

    /// <summary>
    /// A file's spatial structure as a new project takes it: its first project, site and building, its storeys bottom up
    /// (every one, as an import makes them: a storey the user left out in the survey comes empty), each with its GlobalId (when it is one, and once),
    /// its name and its elevation, its height up to the next storey (3 m for the top one); and the GlobalIds of the
    /// IfcRelAggregates that hold each.
    /// </summary>
    public static SpatialSeed SeedOf(StepFile f)
    {
        static string? Str(object? v) => IfcModel.Str(v);
        static string? Gid(StepEntity? e) => e != null && Str(e[0]) is { } g && GlobalId.IsValid(g) ? g : null;
        var scale = IfcModel.ProjectUnits(f).Length;
        var aggregates = f.OfType("IFCRELAGGREGATES").ToList();
        string? AggregationOf(StepEntity? whole, StepEntity? part) =>
            whole == null || part == null ? null
            : aggregates.FirstOrDefault(r => IfcModel.Ref(r[4]) == whole.Id && IfcModel.Refs(r[5]).Contains(part.Id)) is { } r && Str(r[0]) is { } g && GlobalId.IsValid(g) ? g : null;
        var project = f.OfType("IFCPROJECT").FirstOrDefault();
        var site = f.OfType("IFCSITE").FirstOrDefault();
        var building = f.OfType("IFCBUILDING").FirstOrDefault();
        var used = new HashSet<string>();
        string? Once(string? g) => g != null && used.Add(g) ? g : null;
        var levels = f.OfType("IFCBUILDINGSTOREY")
            .Select(st => (St: st, Z: Math.Round(IfcModel.Num(st[9]) * scale, 9)))
            .OrderBy(x => x.Z).ToList();
        var storeys = new List<SpatialSeed.Level>();
        for (var i = 0; i < levels.Count; i++)
        {
            var (st, z) = levels[i];
            var next = i + 1 < levels.Count ? levels[i + 1].Z - z : 0;
            var holder = aggregates.FirstOrDefault(r => IfcModel.Refs(r[5]).Contains(st.Id) && f.Get(r[4]) is { Type: "IFCBUILDING" });
            storeys.Add(new SpatialSeed.Level(Once(Gid(st)), Str(st[2]) ?? Str(st[7]) ?? "Level", z, next > 1e-6 ? Math.Round(next, 9) : 3.0,
                holder != null && Str(holder[0]) is { } hg && GlobalId.IsValid(hg) ? hg : null));
        }
        return new SpatialSeed(
            new SpatialSeed.Part(Once(Gid(project)), Str(project?[2]) is { Length: > 0 } pn ? pn : null),
            new SpatialSeed.Part(Once(Gid(site)), Str(site?[2]) is { Length: > 0 } sn ? sn : null, AggregationOf(project, site)),
            new SpatialSeed.Part(Once(Gid(building)), Str(building?[2]) is { Length: > 0 } bn ? bn : null, AggregationOf(site, building)),
            storeys);
    }

    sealed partial class Reader
    {
        /// <summary>A model storey that has the file storey's GlobalId → the file's IfcRelContainedInSpatialStructure of it, for <see cref="WriteContainment"/>.</summary>
        readonly Dictionary<string, string> containmentIds = [];

        /// <summary>The file's containment of <paramref name="fileStorey"/>, remembered when the model's storey is the file's own (its GlobalId).</summary>
        void KeepContainmentId(StepEntity fileStorey, string modelStorey)
        {
            if (IfcModel.Str(fileStorey[0]) != modelStorey) return;
            foreach (var r in f.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE"))
                if (IfcModel.Ref(r[5]) == fileStorey.Id && IfcModel.Str(r[0]) is { } g && GlobalId.IsValid(g)) { containmentIds[modelStorey] = g; return; }
        }

        /// <summary>
        /// The containment of each storey that is the file's own given the file's GlobalId (<see cref="Spatial.FileGlobalId"/>), unless it has
        /// one: when the elements are in, and again once recognition from triangles has replaced some (a storey's containment emptied is made again).
        /// </summary>
        public void KeepContainmentIds(IEdit edit)
        {
            foreach (var (storey, g) in containmentIds)
                if (edit.Model.RelationsOf(storey, IfcClass.RelContainedInSpatialStructure).FirstOrDefault(r => r.Relating == storey) is { } own && Spatial.FileGlobalIdOf(own) == null)
                {
                    var attrs = own.Attrs?.DeepClone() as JsonObject ?? [];
                    attrs[Spatial.FileGlobalId] = g;
                    edit.ReplaceRelation(own with { Attrs = attrs });
                }
        }
    }
}
