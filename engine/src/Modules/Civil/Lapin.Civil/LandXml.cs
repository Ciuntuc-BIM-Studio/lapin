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

    /// <summary>Points along breaklines (a terrain's edges that must hold: a kerb, a ditch), every <paramref name="step"/> m, for the TIN to keep their line.</summary>
    public static IEnumerable<Vec3> Densify(IEnumerable<IReadOnlyList<Vec3>> lines, double step = 1)
    {
        foreach (var line in lines)
            for (var i = 0; i + 1 < line.Count; i++)
            {
                var (a, b) = (line[i], line[i + 1]);
                var n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y)) / step));
                for (var k = 0; k < n; k++) yield return a + (b - a) * ((double)k / n);
                if (i + 2 == line.Count) yield return b;
            }
    }
}
