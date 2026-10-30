using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Lapin.Platform.Cde.Remarks;

/// <summary>
/// Writes a BCF file (a zip): <c>bcf.version</c>, then one folder per topic
/// GUID with <c>markup.bcf</c>, a <c>.bcfv</c> per viewpoint and its
/// snapshot PNG. BCF 2.1 by default (what most tools take); BCF 3.0 on
/// request, with the comments and viewpoints nested in the topic as its
/// schema asks. Written by hand with <see cref="XmlWriter"/>, in the
/// element order the schemas fix.
/// </summary>
public static class BcfWriter
{
    public static byte[] Write(BcfFile file)
    {
        var v3 = file.Version == Bcf.Version30;
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Put(zip, "bcf.version", Xml(w =>
            {
                w.WriteStartElement("Version");
                w.WriteAttributeString("VersionId", file.Version);
                if (!v3) w.WriteElementString("DetailedVersion", file.Version);
                w.WriteEndElement();
            }));
            foreach (var topic in file.Topics)
            {
                var folder = topic.Guid.ToString("D");
                var names = ViewpointFiles(topic);
                Put(zip, $"{folder}/markup.bcf", Xml(w => Markup(w, topic, names, v3)));
                foreach (var vp in topic.Viewpoints)
                {
                    var (bcfv, png) = names[vp.Guid];
                    Put(zip, $"{folder}/{bcfv}", Xml(w => Visualization(w, vp, v3)));
                    if (vp.Snapshot is { Length: > 0 } bytes) Put(zip, $"{folder}/{png}", bytes);
                }
            }
        }
        return ms.ToArray();
    }

    /// <summary>The first viewpoint's files are named as BCF 2.1's convention (viewpoint.bcfv, snapshot.png); the others after their GUID.</summary>
    static Dictionary<Guid, (string Bcfv, string Png)> ViewpointFiles(BcfTopic topic)
    {
        var names = new Dictionary<Guid, (string, string)>();
        for (var i = 0; i < topic.Viewpoints.Count; i++)
        {
            var g = topic.Viewpoints[i].Guid;
            names[g] = i == 0 ? ("viewpoint.bcfv", "snapshot.png") : ($"{g:D}.bcfv", $"{g:D}.png");
        }
        return names;
    }

    static void Markup(XmlWriter w, BcfTopic t, Dictionary<Guid, (string Bcfv, string Png)> names, bool v3)
    {
        w.WriteStartElement("Markup");
        w.WriteStartElement("Topic");
        w.WriteAttributeString("Guid", t.Guid.ToString("D"));
        // 3.0 requires both; 2.1 leaves them optional, and an empty one is best left out.
        if (t.Type is { Length: > 0 } || v3) w.WriteAttributeString("TopicType", t.Type is { Length: > 0 } ? t.Type : "Issue");
        if (t.Status is { Length: > 0 } || v3) w.WriteAttributeString("TopicStatus", t.Status is { Length: > 0 } ? t.Status : "Open");
        w.WriteElementString("Title", t.Title);
        if (t.Priority is { Length: > 0 }) w.WriteElementString("Priority", t.Priority);
        if (t.Index is { } ix) w.WriteElementString("Index", ix.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (t.Labels.Count > 0)
        {
            if (v3) w.WriteStartElement("Labels");
            foreach (var l in t.Labels) w.WriteElementString(v3 ? "Label" : "Labels", l);
            if (v3) w.WriteEndElement();
        }
        w.WriteElementString("CreationDate", t.CreationDate);
        w.WriteElementString("CreationAuthor", t.CreationAuthor);
        if (t.ModifiedDate is { Length: > 0 }) w.WriteElementString("ModifiedDate", t.ModifiedDate);
        if (t.ModifiedAuthor is { Length: > 0 }) w.WriteElementString("ModifiedAuthor", t.ModifiedAuthor);
        if (t.DueDate is { Length: > 0 }) w.WriteElementString("DueDate", t.DueDate);
        if (t.AssignedTo is { Length: > 0 }) w.WriteElementString("AssignedTo", t.AssignedTo);
        if (t.Stage is { Length: > 0 }) w.WriteElementString("Stage", t.Stage);
        if (t.Description is { Length: > 0 }) w.WriteElementString("Description", t.Description);
        if (v3)
        {
            if (t.Comments.Count > 0)
            {
                w.WriteStartElement("Comments");
                foreach (var c in t.Comments) Comment(w, c);
                w.WriteEndElement();
            }
            if (t.Viewpoints.Count > 0)
            {
                w.WriteStartElement("Viewpoints");
                foreach (var vp in t.Viewpoints) ViewpointRef(w, vp, names[vp.Guid], "ViewPoint");
                w.WriteEndElement();
            }
            w.WriteEndElement(); // Topic
        }
        else
        {
            w.WriteEndElement(); // Topic
            foreach (var c in t.Comments) Comment(w, c);
            foreach (var vp in t.Viewpoints) ViewpointRef(w, vp, names[vp.Guid], "Viewpoints");
        }
        w.WriteEndElement(); // Markup
    }

    static void Comment(XmlWriter w, BcfComment c)
    {
        w.WriteStartElement("Comment");
        w.WriteAttributeString("Guid", c.Guid.ToString("D"));
        w.WriteElementString("Date", c.Date);
        w.WriteElementString("Author", c.Author);
        w.WriteElementString("Comment", c.Text);
        if (c.Viewpoint is { } v)
        {
            w.WriteStartElement("Viewpoint");
            w.WriteAttributeString("Guid", v.ToString("D"));
            w.WriteEndElement();
        }
        if (c.ModifiedDate is { Length: > 0 }) w.WriteElementString("ModifiedDate", c.ModifiedDate);
        if (c.ModifiedAuthor is { Length: > 0 }) w.WriteElementString("ModifiedAuthor", c.ModifiedAuthor);
        w.WriteEndElement();
    }

    static void ViewpointRef(XmlWriter w, BcfViewpoint vp, (string Bcfv, string Png) files, string element)
    {
        w.WriteStartElement(element);
        w.WriteAttributeString("Guid", vp.Guid.ToString("D"));
        w.WriteElementString("Viewpoint", files.Bcfv);
        if (vp.Snapshot is { Length: > 0 }) w.WriteElementString("Snapshot", files.Png);
        if (vp.Index is { } ix) w.WriteElementString("Index", ix.ToString(System.Globalization.CultureInfo.InvariantCulture));
        w.WriteEndElement();
    }

    static void Visualization(XmlWriter w, BcfViewpoint vp, bool v3)
    {
        w.WriteStartElement("VisualizationInfo");
        w.WriteAttributeString("Guid", vp.Guid.ToString("D"));
        // Components: the selection, then the visibility (2.1 requires it; 3.0 keeps the hints inside it).
        w.WriteStartElement("Components");
        if (!v3)
        {
            w.WriteStartElement("ViewSetupHints");
            w.WriteAttributeString("SpacesVisible", "false");
            w.WriteAttributeString("SpaceBoundariesVisible", "false");
            w.WriteAttributeString("OpeningsVisible", "false");
            w.WriteEndElement();
        }
        if (vp.Selection.Count > 0)
        {
            w.WriteStartElement("Selection");
            foreach (var id in vp.Selection) Component(w, id);
            w.WriteEndElement();
        }
        w.WriteStartElement("Visibility");
        w.WriteAttributeString("DefaultVisibility", vp.DefaultVisibility ? "true" : "false");
        if (v3)
        {
            w.WriteStartElement("ViewSetupHints");
            w.WriteAttributeString("SpacesVisible", "false");
            w.WriteAttributeString("SpaceBoundariesVisible", "false");
            w.WriteAttributeString("OpeningsVisible", "false");
            w.WriteEndElement();
        }
        if (vp.Exceptions.Count > 0)
        {
            w.WriteStartElement("Exceptions");
            foreach (var id in vp.Exceptions) Component(w, id);
            w.WriteEndElement();
        }
        w.WriteEndElement(); // Visibility
        w.WriteEndElement(); // Components
        if (vp.Camera is { } cam)
        {
            w.WriteStartElement(cam.Perspective ? "PerspectiveCamera" : "OrthogonalCamera");
            Point(w, "CameraViewPoint", cam.ViewPoint);
            Point(w, "CameraDirection", cam.Direction);
            Point(w, "CameraUpVector", cam.Up);
            if (cam.Perspective)
            {
                // 2.1's schema allows 45–60 degrees only; 3.0 any angle under 180.
                var fov = v3 ? Math.Clamp(cam.FieldOfView, 1, 179) : Math.Clamp(cam.FieldOfView, 45, 60);
                w.WriteElementString("FieldOfView", Bcf.Num(fov));
            }
            else w.WriteElementString("ViewToWorldScale", Bcf.Num(cam.ViewToWorldScale));
            if (v3) w.WriteElementString("AspectRatio", Bcf.Num(cam.AspectRatio is > 0 and { } r ? r : 16.0 / 9));
            w.WriteEndElement();
        }
        if (vp.Lines.Count > 0)
        {
            w.WriteStartElement("Lines");
            foreach (var l in vp.Lines)
            {
                w.WriteStartElement("Line");
                Point(w, "StartPoint", l.Start);
                Point(w, "EndPoint", l.End);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        if (vp.ClippingPlanes.Count > 0)
        {
            w.WriteStartElement("ClippingPlanes");
            foreach (var p in vp.ClippingPlanes)
            {
                w.WriteStartElement("ClippingPlane");
                Point(w, "Location", p.Location);
                Point(w, "Direction", p.Direction);
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
        w.WriteEndElement(); // VisualizationInfo
    }

    static void Component(XmlWriter w, string ifcGuid)
    {
        w.WriteStartElement("Component");
        w.WriteAttributeString("IfcGuid", ifcGuid);
        w.WriteElementString("OriginatingSystem", "Lapin");
        w.WriteEndElement();
    }

    static void Point(XmlWriter w, string name, BcfPoint p)
    {
        w.WriteStartElement(name);
        w.WriteElementString("X", Bcf.Num(p.X));
        w.WriteElementString("Y", Bcf.Num(p.Y));
        w.WriteElementString("Z", Bcf.Num(p.Z));
        w.WriteEndElement();
    }

    static byte[] Xml(Action<XmlWriter> body)
    {
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, IndentChars = "  " }))
        {
            w.WriteStartDocument();
            body(w);
            w.WriteEndDocument();
        }
        return ms.ToArray();
    }

    static void Put(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(bytes);
    }
}
