using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// A georeferenced file's tie to the map read into the project's location (issue 27; docs/spec-ifc.md): IFC 4's and 4.3's
/// IfcMapConversion from the Model context to an IfcProjectedCRS, or IFC2X3's ePSet_MapConversion and ePSet_ProjectedCRS
/// (buildingSMART's User Guide for Geo-referencing in IFC), and IfcSite's RefLatitude and RefLongitude. The file's
/// eastings, northings and height become a survey point at the plan's origin, its X axis's direction the angle to true
/// north, its EPSG system the project's when Lapin knows it. Only into a project not yet tied to the map; the file is only read.
/// </summary>
public static partial class IfcImport
{
    /// <summary>A file's tie to the map: its system's name, the plan origin's eastings, northings and height (metres), the plan's X axis on the map, the scale.</summary>
    public sealed record FileTie(string? Crs, double Eastings, double Northings, double Height, double XAxisAbscissa, double XAxisOrdinate, double Scale)
    {
        /// <summary>The angle to true north (degrees) the plan's X axis on the map makes: (cos θ, −sin θ).</summary>
        public double NorthAngle => Math.Abs(XAxisAbscissa) < 1e-12 && Math.Abs(XAxisOrdinate) < 1e-12 ? 0 : Math.Atan2(-XAxisOrdinate, XAxisAbscissa) * 180 / Math.PI;
    }

    /// <summary>
    /// The file's tie to the map: its IfcMapConversion (from a Model context, else the first one), its target system's
    /// name and map unit (the project's length unit when it names none); else IFC2X3's property sets on the site or the
    /// project. Null when the file is not georeferenced.
    /// </summary>
    public static FileTie? TieOf(StepFile f)
    {
        var projectUnit = IfcModel.LengthUnit(f);
        var conversions = f.OfType("IFCMAPCONVERSION").ToList();
        var mc = conversions.FirstOrDefault(c => f.Get(c[0]) is { Type: "IFCGEOMETRICREPRESENTATIONCONTEXT" } ctx && IfcModel.Str(ctx[1]) == "Model") ?? conversions.FirstOrDefault();
        if (mc != null)
        {
            var target = f.Get(mc[1]);
            var unit = target is { Type: "IFCPROJECTEDCRS" } && f.Get(target[6]) is { } u ? IfcModel.UnitFactor(f, u, 0) : projectUnit;
            double N(int i, double none) => mc[i] is double or long or StepTyped ? IfcModel.Num(mc[i]) : none;
            return new FileTie(IfcModel.Str(target?[0]), N(2, 0) * unit, N(3, 0) * unit, N(4, 0) * unit, N(5, 1), N(6, 0), N(7, 1));
        }
        // IFC2X3: the same as property sets (on the site, as the guide asks; some writers put them on the project).
        var holders = f.OfType("IFCSITE").Concat(f.OfType("IFCPROJECT")).ToList();
        foreach (var holder in holders)
        {
            var conversion = SetOf(f, holder.Id, "ePSet_MapConversion");
            if (conversion.Count == 0) continue;
            var crs = SetOf(f, holder.Id, "ePSet_ProjectedCRS");
            if (crs.Count == 0)
                foreach (var other in holders)
                    if ((crs = SetOf(f, other.Id, "ePSet_ProjectedCRS")).Count > 0) break;
            var unit = crs.GetValueOrDefault("MapUnit") switch
            {
                StepEntity e when e.Type is "IFCSIUNIT" or "IFCCONVERSIONBASEDUNIT" => IfcModel.UnitFactor(f, e, 0),
                var v when IfcModel.Str(v) is { } label => label.ToUpperInvariant() switch
                {
                    var l when l.Contains("MILLI") => 1e-3,
                    var l when l.Contains("CENTI") => 1e-2,
                    var l when l.Contains("FOOT") || l.Contains("FEET") => 0.3048,
                    var l when l.Contains("METRE") || l.Contains("METER") || l == "M" => 1,
                    _ => projectUnit,
                },
                _ => projectUnit,
            };
            double Get(string name, double none) => conversion.GetValueOrDefault(name) is double or long or StepTyped ? IfcModel.Num(conversion[name]) : none;
            return new FileTie(IfcModel.Str(crs.GetValueOrDefault("Name")), Get("Eastings", 0) * unit, Get("Northings", 0) * unit, Get("OrthogonalHeight", 0) * unit,
                Get("XAxisAbscissa", 1), Get("XAxisOrdinate", 0), Get("Scale", 1));
        }
        return null;
    }

    /// <summary>The site's RefLatitude and RefLongitude (degrees, from IfcCompoundPlaneAngleMeasure), or null.</summary>
    public static (double Lat, double Lon)? PlaceOf(StepFile f)
    {
        var site = f.OfType("IFCSITE").FirstOrDefault();
        if (site == null || Compound(site[9]) is not { } lat || Compound(site[10]) is not { } lon) return null;
        return Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180 ? (lat, lon) : null;
    }

    /// <summary>An IfcCompoundPlaneAngleMeasure (degrees, minutes, seconds, millionths of a second, all of one sign) in degrees.</summary>
    static double? Compound(object? v)
    {
        if (v is StepTyped t) v = t.Value;
        if (v is not List<object?> l || l.Count < 3) return null;
        double P(int i) => i < l.Count ? IfcModel.Num(l[i]) : 0;
        return P(0) + P(1) / 60 + P(2) / 3600 + P(3) / 3600e6;
    }

    /// <summary>A property set's single values by name on an entity (the value as the file gives it; its unit entity when it has no value).</summary>
    static Dictionary<string, object?> SetOf(StepFile f, int id, string setName)
    {
        var o = new Dictionary<string, object?>();
        foreach (var r in f.OfType("IFCRELDEFINESBYPROPERTIES"))
        {
            if (!IfcModel.Refs(r[4]).Contains(id) || f.Get(r[5]) is not { Type: "IFCPROPERTYSET" } set || IfcModel.Str(set[2]) != setName) continue;
            foreach (var pid in IfcModel.Refs(set[4]))
                if (f.Get(new StepRef(pid)) is { Type: "IFCPROPERTYSINGLEVALUE" } p && IfcModel.Str(p[0]) is { } name)
                    o[name] = p[2] ?? f.Get(p[3]);
        }
        return o;
    }

    sealed partial class Reader
    {
        /// <summary>Whether Lapin's own intent on the file's project brought its location (a file Lapin wrote): then it is that one.</summary>
        bool locationFromIntent;

        /// <summary>The file's tie to the map, or its site's latitude and longitude, into the project's location when it has none.</summary>
        void Georeference(IEdit edit)
        {
            if (o.ReferenceOnly || locationFromIntent) return;
            var tie = TieOf(f);
            // Without a map conversion, the height of ±0.00 above the sea the building or the site gives (decision 867).
            if (tie == null) RefHeight(edit);
            var place = tie == null ? PlaceOf(f) : null;
            if (tie == null && place == null) return;
            static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            if (GeoRef.Tie(edit.Model) is { } own)
            {
                // The project is tied already: it stays as it is.
                if (tie != null && (Math.Abs(own.Eastings - tie.Eastings) > 1e-3 || Math.Abs(own.Northings - tie.Northings) > 1e-3))
                    notes.Add($"The file is tied to the map at E {F(tie.Eastings)} N {F(tie.Northings)}, the project at E {F(own.Eastings)} N {F(own.Northings)}: the project keeps its own (Manage → Location).");
                return;
            }
            var project = Spatial.Project(edit.Model);
            if (project == null) { Spatial.EnsureBuilding(edit); project = Spatial.Project(edit.Model)!; }
            var attrs = project.CloneAttrs();
            var loc = (attrs["location"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            var basePoint = loc["base"] as JsonObject ?? new JsonObject();
            if (tie != null)
            {
                loc["survey"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["easting"] = tie.Eastings, ["northing"] = tie.Northings, ["elevation"] = tie.Height };
                if (Math.Abs(Lapin.Model.Json.Number(basePoint["northAngle"]) ?? 0) < 1e-12) basePoint["northAngle"] = Math.Round(tie.NorthAngle, 9);
                if (Math.Abs(Lapin.Model.Json.Number(basePoint["elevation"]) ?? 0) < 1e-12) basePoint["elevation"] = tie.Height;
                var known = GeoRef.Get(tie.Crs);
                if (known != null && (string?)loc["crs"] is not { Length: > 0 }) loc["crs"] = known.Id;
                if (known == null && tie.Crs is { Length: > 0 } name) notes.Add($"The file's coordinate system {name} is not one Lapin knows: the survey point keeps its eastings and northings, with no system (Manage → Location).");
                if (Math.Abs(tie.Scale - 1) > 1e-9) notes.Add($"The file's map conversion has a scale of {tie.Scale.ToString("0.######", CultureInfo.InvariantCulture)}: Lapin keeps the model at its own size (scale 1).");
                notes.Add($"The project is tied to the map as the file is: E {F(tie.Eastings)} N {F(tie.Northings)}{(known != null ? $" in {known.Id}" : "")}.");
            }
            else if (place is var (lat, lon) && basePoint["latitude"] == null && basePoint["longitude"] == null)
            {
                basePoint["latitude"] = lat;
                basePoint["longitude"] = lon;
            }
            else return;
            loc["base"] = basePoint;
            attrs["location"] = loc;
            edit.Replace(project.With(attrs: attrs));
        }

        /// <summary>
        /// The file's altitude of its ±0.00, when it has no map conversion (decision 867): IfcBuilding's ElevationOfRefHeight, else
        /// IfcSite's RefElevation, into the base point's elevation of a project that has none yet; the model does not move.
        /// </summary>
        void RefHeight(IEdit edit)
        {
            var height = f.OfType("IFCBUILDING").Select(b => b.Args.Count > 9 ? b[9] : null).FirstOrDefault(v => v is double or long or StepTyped)
                ?? f.OfType("IFCSITE").Select(s => s.Args.Count > 11 ? s[11] : null).FirstOrDefault(v => v is double or long or StepTyped);
            if (height == null) return;
            var h = Length(height);
            if (Math.Abs(h) < 1e-9 || !double.IsFinite(h)) return;
            var project = Spatial.Project(edit.Model);
            if (project == null) { Spatial.EnsureBuilding(edit); project = Spatial.Project(edit.Model)!; }
            if (Math.Abs(Location.Base(edit.Model).Elevation) > 1e-12) return;
            var attrs = project.CloneAttrs();
            var loc = (attrs["location"] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
            var basePoint = loc["base"] as JsonObject ?? new JsonObject();
            basePoint["elevation"] = h;
            loc["base"] = basePoint.DeepClone();
            attrs["location"] = loc;
            edit.Replace(project.With(attrs: attrs));
            notes.Add($"The project's ±0.00 is at the file's altitude, {h.ToString("0.###", CultureInfo.InvariantCulture)} m (Manage → Location).");
        }

        /// <summary>The project's ±0.00 storey (its level datum, decision 867) as the file names it, by its GlobalId: the storey it became.</summary>
        void ZeroStorey(IEdit edit)
        {
            if (o.ReferenceOnly || Spatial.Project(edit.Model) is not { } project || project.Attrs[LevelDatum.Key] is not JsonObject d || (string?)d["zeroStorey"] is not { } gid) return;
            if (edit.Model.Get(gid) is { Class: IfcClass.BuildingStorey }) return;
            var st = f.OfType("IFCBUILDINGSTOREY").FirstOrDefault(x => IfcModel.Str(x[0]) == gid);
            var attrs = project.CloneAttrs();
            var o2 = (JsonObject)d.DeepClone();
            if (st != null && storeyOf.TryGetValue(st.Id, out var id)) o2["zeroStorey"] = id; else o2.Remove("zeroStorey");
            if (o2.Count > 0) attrs[LevelDatum.Key] = o2; else attrs.Remove(LevelDatum.Key);
            edit.Replace(project.With(attrs: attrs));
        }
    }
}
