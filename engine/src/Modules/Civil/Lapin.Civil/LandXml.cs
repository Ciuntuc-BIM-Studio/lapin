using System.Globalization;
using System.Xml.Linq;
using BubbleBim.Geometry;

namespace Lapin.Civil;

/// <summary>
/// LandXML 1.2 surfaces (TIN): each Surface's points (Pnts/P, written
/// northing, easting, elevation) and its faces. Lapin keeps the points and
/// triangulates them again; the faces only count what was there.
/// </summary>
public static class LandXml
{
    public sealed record Surface(string Name, List<Vec3> Points, int Faces);

    public static List<Surface> Surfaces(string xml)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (System.Xml.XmlException ex) { throw new Lapin.Platform.CommandException($"Not a LandXML file: {ex.Message}"); }
        var o = new List<Surface>();
        foreach (var s in doc.Descendants().Where(e => e.Name.LocalName == "Surface"))
        {
            var pts = new List<Vec3>();
            foreach (var p in s.Descendants().Where(e => e.Name.LocalName == "P"))
            {
                var f = p.Value.Split([' ', '\t', '\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN).ToArray();
                if (f.Length >= 3 && f.Take(3).All(double.IsFinite)) pts.Add(new Vec3(f[1], f[0], f[2]));
            }
            if (pts.Count < 3) continue;
            var faces = s.Descendants().Count(e => e.Name.LocalName == "F");
            o.Add(new Surface((string?)s.Attribute("name") ?? "Surface", pts, faces));
        }
        return o;
    }
}
