using System.Globalization;
using System.Xml.Linq;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Civil;

/// <summary>
/// LandXML 1.2: surfaces (TIN) — each Surface's points (Pnts/P, written
/// northing, easting, elevation) and its faces; Lapin keeps the points and
/// triangulates them again, the faces only count what was there — and
/// alignments (docs/plan-rail.md, R4): each horizontal segment as a Line, a
/// Curve or a Spiral, the vertical as a ProfAlign of PVIs, the cant as a Cant
/// with a CantStation at every segment's start (its applied cant, the speed in
/// force, the transition that follows), the speed table as a Feature. A track
/// proposed from its survey also carries what was measured: the points as a
/// CgPoints group, their heights as the profile's ProfSurf of the existing
/// line, and the shifts onto the design (slew + left, lift + up, mm) as a
/// TrackShifts Feature — what GEDO, Civil 3D or OpenRail read beside the design.
/// </summary>
public static class LandXml
{
    /// <summary>An alignment to write: its segments, the speed table (km/h by station) when it has one.</summary>
    public sealed record AlignmentOut(string Id, string Name, Alignment Alignment, IReadOnlyList<SpeedOut>? Speeds = null, string? Description = null, IReadOnlyList<StationEquation>? Equations = null, IReadOnlyList<ExistingPoint>? Existing = null);

    /// <summary>A row of a speed table: from this station on, the design speed (km/h) and, optionally, the slowest regular trains'.</summary>
    public sealed record SpeedOut(double From, double Speed, double? MinSpeed = null);

    /// <summary>The row in force at a station (the first before the first row); null without rows.</summary>
    static SpeedOut? SpeedAt(IReadOnlyList<SpeedOut>? rows, double station)
    {
        if (rows == null || rows.Count == 0) return null;
        var row = rows[0];
        foreach (var r in rows) { if (r.From <= station + 1e-9) row = r; else break; }
        return row;
    }

    /// <summary>A measured point of the existing track: its chainage, its station on the design, where it is (Z NaN without a height), the slew and lift onto the design (m; lift NaN without a profile).</summary>
    public sealed record ExistingPoint(double Chainage, double Station, Vec3 P, double Slew, double Lift);

    /// <summary>Where the alignments of the export come from (the railway's tracks and branches), registered by the module.</summary>
    /// <remarks>Assigned whole, never mutated: queries enumerate it while another session registers the module.</remarks>
    public static IReadOnlyList<Func<IModelView, IEnumerable<AlignmentOut>>> AlignmentSources { get; set; } = [];

    /// <summary>A station as kilometre + metres, "1+200.00".</summary>
    static string Km(double station)
    {
        var km = Math.Floor(station / 1000);
        return $"{km.ToString("0", CultureInfo.InvariantCulture)}+{(station - km * 1000).ToString("000.00", CultureInfo.InvariantCulture)}";
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string F6(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
    static string Esc(string s) => System.Security.SecurityElement.Escape(s) ?? "";
    /// <summary>A plan point as LandXML writes it: northing, easting.</summary>
    static string NE(double x, double y) => $"{F(y)} {F(x)}";

    /// <summary>Surfaces (and alignments) written as LandXML 1.2: points as "northing easting elevation" (what the reader takes), faces by point id, elevations surveyed (model heights plus the datum).</summary>
    public static string Write(IReadOnlyList<(string Name, Tin Tin, double Datum)> surfaces, IReadOnlyList<AlignmentOut>? alignments = null)
    {
        var sb = new System.Text.StringBuilder();
        var now = DateTime.UtcNow;
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append($"<LandXML xmlns=\"http://www.landxml.org/schema/LandXML-1.2\" version=\"1.2\" date=\"{now:yyyy-MM-dd}\" time=\"{now:HH:mm:ss}\">\n");
        sb.Append("  <Units><Metric linearUnit=\"meter\" areaUnit=\"squareMeter\" volumeUnit=\"cubicMeter\" temperatureUnit=\"celsius\" pressureUnit=\"milliBars\" angularUnit=\"decimal degrees\" directionUnit=\"decimal degrees\"/></Units>\n");
        sb.Append("  <Application name=\"Lapin\" manufacturer=\"Ciuntuc BIM Studio\"/>\n");
        if (surfaces.Count > 0)
        {
            sb.Append("  <Surfaces>\n");
            foreach (var (name, tin, datum) in surfaces)
            {
                sb.Append($"    <Surface name=\"{Esc(name)}\">\n      <Definition surfType=\"TIN\">\n        <Pnts>\n");
                for (var i = 0; i < tin.Points.Count; i++)
                    sb.Append($"          <P id=\"{i + 1}\">{F(tin.Points[i].Y)} {F(tin.Points[i].X)} {F(tin.Points[i].Z + datum)}</P>\n");
                sb.Append("        </Pnts>\n        <Faces>\n");
                foreach (var (fa, fb, fc) in tin.Triangles) sb.Append($"          <F>{fa + 1} {fb + 1} {fc + 1}</F>\n");
                sb.Append("        </Faces>\n      </Definition>\n    </Surface>\n");
            }
            sb.Append("  </Surfaces>\n");
        }
        foreach (var a in alignments ?? [])
        {
            if (a.Existing is not { Count: > 0 } ex) continue;
            sb.Append($"  <CgPoints name=\"{Esc(a.Name)} existing\" desc=\"Measured centre line\">\n");
            for (var j = 0; j < ex.Count; j++)
            {
                var q = ex[j];
                sb.Append($"    <CgPoint name=\"{Esc(a.Name)} {j + 1}\" code=\"EXISTING\" desc=\"{Km(q.Chainage)}\">{NE(q.P.X, q.P.Y)}{(double.IsNaN(q.P.Z) ? "" : $" {F(q.P.Z)}")}</CgPoint>\n");
            }
            sb.Append("  </CgPoints>\n");
        }
        if (alignments is { Count: > 0 })
        {
            sb.Append("  <Alignments>\n");
            foreach (var a in alignments) WriteAlignment(sb, a);
            sb.Append("  </Alignments>\n");
        }
        sb.Append("</LandXML>\n");
        return sb.ToString();
    }

    static readonly Dictionary<HorizontalType, string> SpiralTypes = new()
    {
        [HorizontalType.Clothoid] = "clothoid", [HorizontalType.Cubic] = "cubic", [HorizontalType.BlossCurve] = "bloss", [HorizontalType.CosineCurve] = "cosine",
        [HorizontalType.SineCurve] = "sinusoid", [HorizontalType.HelmertCurve] = "biquadratic", [HorizontalType.VienneseBend] = "weinerBogen",
    };

    static readonly Dictionary<CantType, string> CantTransitions = new()
    {
        [CantType.LinearTransition] = "clothoid", [CantType.BlossCurve] = "bloss", [CantType.CosineCurve] = "cosine", [CantType.SineCurve] = "sinusoid",
        [CantType.HelmertCurve] = "biquadraticParabola", [CantType.VienneseBend] = "weinerBogen",
    };

    /// <summary>
    /// One alignment: a Line, a Curve (its centre from the start point and
    /// direction) or a Spiral (its spiType from the segment's type, radii INF
    /// for a straight end, the PI where its end tangents meet) per horizontal
    /// segment; the vertical as PVIs — every curve's PVI where its tangents
    /// meet, a ParaCurve, or a CircCurve with its radius, of its horizontal
    /// length; the cant as CantStations (applied cant = |right − left| in
    /// metres, the curvature's hand, the speed in force, adverse when the
    /// outer rail is the low one); the speed table as a Feature of
    /// station/speed properties.
    /// </summary>
    static void WriteAlignment(System.Text.StringBuilder sb, AlignmentOut a)
    {
        var p = a.Alignment.Prepared();
        var h = p.Horizontal;
        sb.Append($"    <Alignment name=\"{Esc(a.Name)}\" length=\"{F(p.Length)}\" staStart=\"{F(p.StartStation)}\"{(a.Description != null ? $" desc=\"{Esc(a.Description)}\"" : "")}>\n      <CoordGeom>\n");
        for (var i = 0; i < h.Segments.Count; i++)
        {
            var g = h.Segments[i].Segment;
            var d0 = h.Starts[i]; var len = g.Length;
            var s = h.At(d0); var e = h.At(d0 + len);
            var k0 = Alignment.CurvatureOf(g.StartRadius); var k1 = Alignment.CurvatureOf(g.EndRadiusOf);
            if (g.Type == HorizontalType.Line || (k0 == 0 && k1 == 0))
                sb.Append($"        <Line length=\"{F(len)}\"><Start>{NE(s.X, s.Y)}</Start><End>{NE(e.X, e.Y)}</End></Line>\n");
            else if (g.Type == HorizontalType.CircularArc)
            {
                var k = k0 != 0 ? k0 : k1;
                var c = s.P + s.Left * (1 / k);
                sb.Append($"        <Curve rot=\"{(k > 0 ? "ccw" : "cw")}\" radius=\"{F(Math.Abs(1 / k))}\" length=\"{F(len)}\"><Start>{NE(s.X, s.Y)}</Start><Center>{NE(c.X, c.Y)}</Center><End>{NE(e.X, e.Y)}</End></Curve>\n");
            }
            else
            {
                var det = Math.Cos(s.Direction) * Math.Sin(e.Direction) - Math.Sin(s.Direction) * Math.Cos(e.Direction);
                var t = Math.Abs(det) > 1e-12 ? ((e.X - s.X) * Math.Sin(e.Direction) - (e.Y - s.Y) * Math.Cos(e.Direction)) / det : len / 2;
                var pi = s.P + s.Tangent * t;
                string R(double k) => k != 0 ? F(Math.Abs(1 / k)) : "INF";
                sb.Append($"        <Spiral length=\"{F(len)}\" radiusStart=\"{R(k0)}\" radiusEnd=\"{R(k1)}\" rot=\"{((k0 != 0 ? k0 : k1) > 0 ? "ccw" : "cw")}\" spiType=\"{SpiralTypes.GetValueOrDefault(g.Type, "clothoid")}\"><Start>{NE(s.X, s.Y)}</Start><PI>{NE(pi.X, pi.Y)}</PI><End>{NE(e.X, e.Y)}</End></Spiral>\n");
            }
        }
        sb.Append("      </CoordGeom>\n");
        // The station equations: the station ahead and back, and the internal (continuous) station at the break.
        foreach (var e in new Stationing(p.StartStation, p.Length, a.Equations).Equations)
            sb.Append($"      <StaEquation staAhead=\"{F(e.Ahead)}\" staBack=\"{F(e.Back)}\" staInternal=\"{F(p.StartStation + e.At)}\"/>\n");
        var v = a.Alignment.Vertical ?? [];
        var measured = a.Existing?.Where(q => !double.IsNaN(q.P.Z)).OrderBy(q => q.Station).ToList() ?? [];
        var designed = v.Count > 0 && p.Vertical != null;
        if (designed || measured.Count >= 2) sb.Append($"      <Profile name=\"{Esc(a.Name)}\">\n");
        // The existing line's heights along the design: what the lift is measured from.
        if (measured.Count >= 2)
            sb.Append($"        <ProfSurf name=\"{Esc(a.Name)} existing\"><PntList2D>{string.Join(" ", measured.Select(q => $"{F(q.Station)} {F(q.P.Z)}"))}</PntList2D></ProfSurf>\n");
        if (designed)
        {
            string St(double d) => F(p.StartStation + d);
            sb.Append($"        <ProfAlign name=\"{Esc(a.Name)}\">\n");
            sb.Append($"          <PVI>{St(v[0].StartDistAlong)} {F(v[0].StartHeight)}</PVI>\n");
            foreach (var seg in v)
            {
                if (seg.Type == VerticalType.ConstantGradient) continue;
                var len = seg.HorizontalLength; var g0 = seg.StartGradient; var g1 = seg.EndGradient;
                var h1 = p.Vertical!.At(seg.EndDistAlong)?.Z ?? seg.StartHeight + (g0 + g1) / 2 * len;
                // The PVI: where the start tangent (h0 + g0 x) meets the end one (h1 − g1 (L − x)).
                var x = Math.Abs(g0 - g1) > 1e-12 ? (h1 - seg.StartHeight - g1 * len) / (g0 - g1) : len / 2;
                var at = $"{St(seg.StartDistAlong + x)} {F(seg.StartHeight + g0 * x)}";
                if (seg.Type == VerticalType.CircularArc && seg.RadiusOfCurvature is { } r && r != 0) sb.Append($"          <CircCurve length=\"{F(len)}\" radius=\"{F(Math.Abs(r))}\">{at}</CircCurve>\n");
                else sb.Append($"          <ParaCurve length=\"{F(len)}\">{at}</ParaCurve>\n");
            }
            var last = v[^1];
            sb.Append($"          <PVI>{St(last.EndDistAlong)} {F(p.Vertical!.At(last.EndDistAlong)?.Z ?? last.StartHeight)}</PVI>\n");
            sb.Append("        </ProfAlign>\n");
        }
        if (designed || measured.Count >= 2) sb.Append("      </Profile>\n");
        if (a.Alignment.Cant is { Segments.Count: > 0 } cant)
        {
            // The low rail stays when every segment keeps one rail at zero; otherwise the cant turns about the axis.
            var insideRail = cant.Segments.All(s => Math.Min(Math.Abs(s.StartCantLeft), Math.Abs(s.StartCantRight)) < 1e-9 && Math.Min(Math.Abs(s.EndLeft), Math.Abs(s.EndRight)) < 1e-9);
            sb.Append($"      <Cant name=\"{Esc(a.Name)}\" gauge=\"{F6(cant.RailHeadDistance)}\" rotationPoint=\"{(insideRail ? "insideRail" : "center")}\">\n");
            void Station(double d, double left, double right, CantType? next)
            {
                var superelevation = right - left;
                var curvature = h.At(Math.Clamp(d, 0, h.Length)).Curvature;
                var hand = curvature != 0 ? curvature : superelevation;
                var speed = SpeedAt(a.Speeds, p.StartStation + d);
                sb.Append($"        <CantStation station=\"{F(p.StartStation + d)}\" appliedCant=\"{F6(Math.Abs(superelevation))}\" curvature=\"{(hand < 0 ? "cw" : "ccw")}\"");
                if (speed != null) sb.Append($" speed=\"{F(speed.Speed)}\"");
                if (next is { } n && CantTransitions.TryGetValue(n, out var tt)) sb.Append($" transitionType=\"{tt}\"");
                if (curvature != 0 && superelevation != 0 && Math.Sign(curvature) != Math.Sign(superelevation)) sb.Append(" adverse=\"true\"");
                sb.Append("/>\n");
            }
            foreach (var s in cant.Segments) Station(s.StartDistAlong, s.StartCantLeft, s.StartCantRight, s.Type);
            var end = cant.Segments[^1];
            Station(end.EndDistAlong, end.EndLeft, end.EndRight, null);
            sb.Append("      </Cant>\n");
        }
        if (a.Speeds is { Count: > 0 })
        {
            sb.Append("      <Feature code=\"Speeds\" source=\"Lapin\">\n");
            foreach (var row in a.Speeds)
                sb.Append($"        <Property label=\"{F(row.From)}\" value=\"{F(row.Speed)}{(row.MinSpeed is { } ms ? $" {F(ms)}" : "")}\"/>\n");
            sb.Append("      </Feature>\n");
        }
        if (a.Existing is { Count: > 0 } shifts)
        {
            // Each measured point: its station on the design → its chainage, the slew and the lift onto the design (mm).
            sb.Append("      <Feature name=\"Shifts: chainage, slew (+ left), lift (+ up), mm\" code=\"TrackShifts\" source=\"Lapin\">\n");
            static string Mm(double v) => double.IsNaN(v) ? "" : (v * 1000).ToString("0.0", CultureInfo.InvariantCulture);
            foreach (var q in shifts.OrderBy(q => q.Station))
                sb.Append($"        <Property label=\"{F(q.Station)}\" value=\"{F(q.Chainage)} {Mm(q.Slew)} {Mm(q.Lift)}\"/>\n");
            sb.Append("      </Feature>\n");
        }
        sb.Append("    </Alignment>\n");
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
