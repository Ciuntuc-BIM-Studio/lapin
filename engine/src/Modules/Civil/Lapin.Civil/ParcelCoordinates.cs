using System.Globalization;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// The parcel's coordinate inventory (Romanian "inventar de coordonate"): each
/// corner as a site plan numbers it, its northing and easting in the project's
/// map system (its plan coordinates when the project is not tied to the map),
/// the side to the next corner; then the area. The parcel is the first
/// terrain's, else the first parcel imported as a site feature.
/// </summary>
public sealed class ParcelCoordinates : IScheduleSource
{
    public string Kind => "parcel";
    public string Label => "Parcel coordinates";

    public static IReadOnlyList<Vec2>? Parcel(IModelView m)
    {
        foreach (var t in m.OfClass(IfcClass.GeographicElement))
            if (Terrains.Contour(t, "parcel") is { Count: >= 3 } p) return p;
        return m.OfClass(SiteFeatures.Class).FirstOrDefault(f => SiteFeatures.Kind(f) == "parcel") is { } feature
            && SiteFeatures.Points(feature).Select(p => p.Xy).ToList() is { Count: >= 3 } pts ? pts : null;
    }

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var map = GeoRef.Georeferenced(m);
        string N(double v, string f = "0.000") => v.ToString(f, CultureInfo.InvariantCulture);
        var rows = new List<IReadOnlyList<string>>();
        if (Parcel(m) is { } parcel)
        {
            for (var k = 0; k < parcel.Count; k++)
            {
                var (e, n) = map ? GeoRef.ToMap(m, parcel[k]) : (parcel[k].X, parcel[k].Y);
                rows.Add([(k + 1).ToString(CultureInfo.InvariantCulture), N(n), N(e), N((parcel[(k + 1) % parcel.Count] - parcel[k]).Length, "0.00")]);
            }
            rows.Add(["S", "", "", $"{N(Math.Abs(Polygon2.SignedArea(parcel)), "0.00")} m²"]);
        }
        var (nh, eh) = map ? ($"N (m, {Location.Crs(m) ?? "map"})", "E (m)") : ("Y (m, plan)", "X (m, plan)");
        return new ScheduleGrid(Label, ["Point", nh, eh, "Side to next (m)"], rows);
    }
}
