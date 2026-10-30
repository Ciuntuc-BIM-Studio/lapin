using System.IO.Compression;
using System.Xml.Linq;
using Lapin.Model;

namespace Lapin.Platform.Cde.Remarks;

/// <summary>
/// Reads a BCF file, 2.1 or 3.0, leniently: elements are found by their
/// local name wherever a version puts them (comments and viewpoints beside
/// the topic in 2.1, inside it in 3.0; labels bare or wrapped), the viewpoint
/// and snapshot files by the markup's references, relative to the topic's
/// folder. What is not understood is skipped, never an error; a zip without
/// a topic is still a (empty) file.
/// </summary>
public static class BcfReader
{
    public static BcfFile Read(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        ZipArchive zip;
        try { zip = new ZipArchive(ms, ZipArchiveMode.Read); }
        catch (InvalidDataException) { throw new CommandException("This is not a BCF file (a zip with bcf.version and a folder per topic)."); }
        using (zip)
        {
            var files = zip.Entries.Where(e => !e.FullName.EndsWith('/')).ToDictionary(e => e.FullName.Replace('\\', '/'), e => e, StringComparer.OrdinalIgnoreCase);
            var version = Bcf.Version21;
            if (files.TryGetValue("bcf.version", out var ve))
            {
                var v = Parse(ve)?.Root?.Attribute("VersionId")?.Value?.Trim();
                if (v != null) version = v.StartsWith('3') ? Bcf.Version30 : Bcf.Version21;
            }
            var topics = new List<BcfTopic>();
            foreach (var (name, entry) in files.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                if (!name.EndsWith("/markup.bcf", StringComparison.OrdinalIgnoreCase)) continue;
                var folder = name[..(name.Length - "markup.bcf".Length)];
                var doc = Parse(entry);
                var topicEl = doc?.Root?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Topic");
                if (doc?.Root == null || topicEl == null) continue;
                topics.Add(Topic(doc.Root, topicEl, folder, files));
            }
            return new BcfFile(version, topics);
        }
    }

    static XDocument? Parse(ZipArchiveEntry entry)
    {
        try
        {
            using var s = entry.Open();
            return XDocument.Load(s, LoadOptions.None);
        }
        catch (System.Xml.XmlException) { return null; }
    }

    static XElement? Child(XElement e, string name) => e.Elements().FirstOrDefault(x => x.Name.LocalName == name);
    static IEnumerable<XElement> Children(XElement e, string name) => e.Elements().Where(x => x.Name.LocalName == name);
    static string? Text(XElement e, string name) => Child(e, name)?.Value is { } v && v.Trim() is { Length: > 0 } t ? t : null;
    static string? Attr(XElement e, string name) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value?.Trim() is { Length: > 0 } v ? v : null;

    static BcfTopic Topic(XElement markup, XElement t, string folder, Dictionary<string, ZipArchiveEntry> files)
    {
        var guid = Bcf.ParseGuid(Attr(t, "Guid")) ?? Guid.NewGuid();
        // Labels: bare <Labels> elements (2.1) or <Labels><Label/></Labels> (3.0).
        var labels = new List<string>();
        foreach (var l in Children(t, "Labels"))
        {
            var inner = Children(l, "Label").ToList();
            if (inner.Count > 0) labels.AddRange(inner.Select(x => x.Value.Trim()).Where(x => x.Length > 0));
            else if (l.Value.Trim() is { Length: > 0 } v) labels.Add(v);
        }
        // Comments and viewpoint references: beside the topic (2.1) or inside it (3.0).
        var commentEls = Children(markup, "Comment").Concat(Child(t, "Comments") is { } cs ? Children(cs, "Comment") : []);
        var vpEls = Children(markup, "Viewpoints").Concat(Child(t, "Viewpoints") is { } vs ? Children(vs, "ViewPoint").Concat(Children(vs, "Viewpoint")) : []);
        var comments = commentEls.Select(c => new BcfComment(Bcf.ParseGuid(Attr(c, "Guid")) ?? Guid.NewGuid(), Text(c, "Date") ?? "", Text(c, "Author") ?? "", Text(c, "Comment") ?? "",
            Child(c, "Viewpoint") is { } cv ? Bcf.ParseGuid(Attr(cv, "Guid")) : null, Text(c, "ModifiedDate"), Text(c, "ModifiedAuthor"))).ToList();
        var viewpoints = new List<BcfViewpoint>();
        foreach (var v in vpEls)
        {
            var vg = Bcf.ParseGuid(Attr(v, "Guid"));
            var file = Text(v, "Viewpoint");
            var snapshotName = Text(v, "Snapshot");
            var index = Bcf.ParseNum(Text(v, "Index")) is { } ix ? (int?)(int)ix : null;
            var vp = file != null && files.TryGetValue(Join(folder, file), out var ve) ? Visualization(ve, vg, index) : new BcfViewpoint(vg ?? Guid.NewGuid(), null, [], true, [], [], [], null, index);
            byte[]? png = null;
            if (snapshotName != null && files.TryGetValue(Join(folder, snapshotName), out var se)) png = Bytes(se);
            viewpoints.Add(vp with { Snapshot = png });
        }
        return new BcfTopic(guid, Text(t, "Title") ?? "Untitled", Text(t, "Description"), Attr(t, "TopicType"), Attr(t, "TopicStatus"), Text(t, "Priority"),
            Bcf.ParseNum(Text(t, "Index")) is { } tix ? (int)tix : null, Text(t, "CreationDate") ?? Bcf.Now(), Text(t, "CreationAuthor") ?? "",
            Text(t, "ModifiedDate"), Text(t, "ModifiedAuthor"), Text(t, "AssignedTo"), Text(t, "DueDate"), Text(t, "Stage"), labels, comments, viewpoints);
    }

    static string Join(string folder, string file)
    {
        var f = file.Replace('\\', '/').TrimStart('/');
        return f.Contains('/') ? f : folder + f;
    }

    static byte[] Bytes(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    static BcfPoint? Point(XElement? e) => e != null && Bcf.ParseNum(Text(e, "X")) is { } x && Bcf.ParseNum(Text(e, "Y")) is { } y && Bcf.ParseNum(Text(e, "Z")) is { } z ? new(x, y, z) : null;

    static BcfViewpoint Visualization(ZipArchiveEntry entry, Guid? guidFromMarkup, int? index)
    {
        var doc = Parse(entry);
        var root = doc?.Root;
        var guid = guidFromMarkup ?? (root != null ? Bcf.ParseGuid(Attr(root, "Guid")) : null) ?? Guid.NewGuid();
        if (root == null) return new BcfViewpoint(guid, null, [], true, [], [], [], null, index);
        var selection = new List<string>();
        var exceptions = new List<string>();
        var defaultVisibility = true;
        if (Child(root, "Components") is { } comps)
        {
            if (Child(comps, "Selection") is { } sel) selection.AddRange(Children(sel, "Component").Select(c => Attr(c, "IfcGuid")).OfType<string>());
            if (Child(comps, "Visibility") is { } vis)
            {
                defaultVisibility = !string.Equals(Attr(vis, "DefaultVisibility"), "false", StringComparison.OrdinalIgnoreCase);
                if (Child(vis, "Exceptions") is { } ex) exceptions.AddRange(Children(ex, "Component").Select(c => Attr(c, "IfcGuid")).OfType<string>());
            }
        }
        BcfCamera? camera = null;
        if (Child(root, "PerspectiveCamera") is { } pc && Point(Child(pc, "CameraViewPoint")) is { } pp && Point(Child(pc, "CameraDirection")) is { } pd)
            camera = new BcfCamera(true, pp, pd, Point(Child(pc, "CameraUpVector")) ?? new(0, 0, 1), Bcf.ParseNum(Text(pc, "FieldOfView")) ?? 60, 0, Bcf.ParseNum(Text(pc, "AspectRatio")));
        else if (Child(root, "OrthogonalCamera") is { } oc && Point(Child(oc, "CameraViewPoint")) is { } op && Point(Child(oc, "CameraDirection")) is { } od)
            camera = new BcfCamera(false, op, od, Point(Child(oc, "CameraUpVector")) ?? new(0, 0, 1), 0, Bcf.ParseNum(Text(oc, "ViewToWorldScale")) ?? 10, Bcf.ParseNum(Text(oc, "AspectRatio")));
        var planes = new List<BcfClippingPlane>();
        if (Child(root, "ClippingPlanes") is { } cps)
            foreach (var p in Children(cps, "ClippingPlane"))
                if (Point(Child(p, "Location")) is { } loc && Point(Child(p, "Direction")) is { } dir) planes.Add(new(loc, dir));
        var lines = new List<BcfLine>();
        if (Child(root, "Lines") is { } ls)
            foreach (var l in Children(ls, "Line"))
                if (Point(Child(l, "StartPoint")) is { } a && Point(Child(l, "EndPoint")) is { } b) lines.Add(new(a, b));
        return new BcfViewpoint(guid, camera, selection, defaultVisibility, exceptions, planes, lines, null, index);
    }
}
