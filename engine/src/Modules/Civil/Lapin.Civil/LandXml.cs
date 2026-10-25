using System.Globalization;
using System.Xml.Linq;
using Lapin.Geometry;

namespace Lapin.Civil;

/// <summary>
/// LandXML 1.2 surfaces (TIN): each Surface's points (Pnts/P, written
/// northing, easting, elevation) and its faces. Lapin keeps the points and
/// triangulates them again; the faces only count what was there.
/// </summary>
public static class LandXml
{
    /// <summary>Surfaces written as LandXML 1.2: points as "northing easting elevation" (what the reader takes), faces by point id, elevations surveyed (model heights plus the datum).</summary>
    public static string Write(IReadOnlyList<(string Name, Tin Tin, double Datum)> surfaces)
    {
        var sb = new System.Text.StringBuilder();
        var now = DateTime.UtcNow;
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append($"<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" version=\"1.2\" date=\"{now:yyyy-MM-dd}\" time=\"{now:HH:mm:ss}\">\n");
        sb.Append("  <Units><Metric linearUnit=\"meter\" areaUnit=\"squareMeter\" volumeUnit=\"cubicMeter\" temperatureUnit=\"celsius\" pressureUnit=\"milliBars\" angularUnit=\"decimal degrees\" directionUnit=\"decimal degrees\"/></Units>\n");
        sb.Append("  <Application name=\"Lapin\" manufacturer=\"Ciuntuc BIM Studio\"/>\n  <Surfaces>\n");
        foreach (var (name, tin, datum) in surfaces)
        {
            sb.Append($"    <Surface name=\"{System.Security.SecurityElement.Escape(name)}\">\n      <Definition surfType=\"TIN\">\n        <Pnts>\n");
            for (var i = 0; i < tin.Points.Count; i++)
                sb.Append($"          <P id=\"{i + 1}\">{F(tin.Points[i].Y)} {F(tin.Points[i].X)} {F(tin.Points[i].Z + datum)}</P>\n");
            sb.Append("        </Pnts>\n        <Faces>\n");
            foreach (var (fa, fb, fc) in tin.Triangles) sb.Append($"          <F>{fa + 1} {fb + 1} {fc + 1}</F>\n");
            sb.Append("        </Faces>\n      </Definition>\n    </Surface>\n");
        }
        sb.Append("  </Surfaces>\n</LandXML>\n");
        return sb.ToString();
    }

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
