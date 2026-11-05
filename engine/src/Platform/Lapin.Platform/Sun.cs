using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>Where the sun stands: its azimuth from true north, clockwise, and its height above the horizon (degrees).</summary>
public sealed record SunPosition(double Azimuth, double Altitude)
{
    public bool Up => Altitude > 0;
}

/// <summary>
/// The sun's position by NOAA's solar calculator (Meeus's low-precision
/// formulas: within a minute of arc for the years around now), the local
/// time of a place (its offset from UTC, with the European summer time when
/// asked), and the direction towards the sun in a project's plan, turned by
/// the project's true north.
/// </summary>
public static class Sun
{
    static double Rad(double d) => d * Math.PI / 180;
    static double Deg(double r) => r * 180 / Math.PI;

    /// <summary>The sun at a moment (UTC) seen from a place (degrees; east and north positive).</summary>
    public static SunPosition At(DateTime utc, double lat, double lon)
    {
        var jd = utc.ToOADate() + 2415018.5;
        var t = (jd - 2451545.0) / 36525.0;
        var l0 = (280.46646 + t * (36000.76983 + t * 0.0003032)) % 360;
        var m = 357.52911 + t * (35999.05029 - 0.0001537 * t);
        var e = 0.016708634 - t * (0.000042037 + 0.0000001267 * t);
        var c = Math.Sin(Rad(m)) * (1.914602 - t * (0.004817 + 0.000014 * t)) + Math.Sin(Rad(2 * m)) * (0.019993 - 0.000101 * t) + Math.Sin(Rad(3 * m)) * 0.000289;
        var trueLong = l0 + c;
        var omega = 125.04 - 1934.136 * t;
        var lambda = trueLong - 0.00569 - 0.00478 * Math.Sin(Rad(omega));
        var eps0 = 23 + (26 + (21.448 - t * (46.815 + t * (0.00059 - t * 0.001813))) / 60) / 60;
        var eps = eps0 + 0.00256 * Math.Cos(Rad(omega));
        var decl = Math.Asin(Math.Sin(Rad(eps)) * Math.Sin(Rad(lambda)));
        var y = Math.Pow(Math.Tan(Rad(eps / 2)), 2);
        var eot = 4 * Deg(y * Math.Sin(2 * Rad(l0)) - 2 * e * Math.Sin(Rad(m)) + 4 * e * y * Math.Sin(Rad(m)) * Math.Cos(2 * Rad(l0))
            - 0.5 * y * y * Math.Sin(4 * Rad(l0)) - 1.25 * e * e * Math.Sin(2 * Rad(m)));
        var minutes = utc.TimeOfDay.TotalMinutes;
        var tst = ((minutes + eot + 4 * lon) % 1440 + 1440) % 1440;
        var ha = tst / 4 < 0 ? tst / 4 + 180 : tst / 4 - 180;
        var phi = Rad(lat);
        var cosZ = Math.Clamp(Math.Sin(phi) * Math.Sin(decl) + Math.Cos(phi) * Math.Cos(decl) * Math.Cos(Rad(ha)), -1, 1);
        var zenith = Math.Acos(cosZ);
        var den = Math.Cos(phi) * Math.Sin(zenith);
        double az;
        if (Math.Abs(den) < 1e-9) az = lat > 0 ? 180 : 0;
        else
        {
            var a = Math.Clamp((Math.Sin(phi) * Math.Cos(zenith) - Math.Sin(decl)) / den, -1, 1);
            az = ha > 0 ? (Deg(Math.Acos(a)) + 180) % 360 : (540 - Deg(Math.Acos(a))) % 360;
        }
        var alt = 90 - Deg(zenith);
        // The atmosphere lifts the sun a little near the horizon (NOAA's refraction).
        alt += Refraction(alt);
        return new SunPosition(az, alt);
    }

    static double Refraction(double alt)
    {
        if (alt > 85) return 0;
        var te = Math.Tan(Rad(alt));
        var r = alt > 5 ? 58.1 / te - 0.07 / Math.Pow(te, 3) + 0.000086 / Math.Pow(te, 5)
            : alt > -0.575 ? 1735 + alt * (-518.2 + alt * (103.4 + alt * (-12.79 + alt * 0.711)))
            : -20.774 / te;
        return r / 3600;
    }

    /// <summary>Whether European summer time holds at a moment (from the last Sunday of March to the last Sunday of October, at 01:00 UTC).</summary>
    public static bool EuropeanSummer(DateTime utc)
    {
        static DateTime LastSunday(int y, int month) { var d = new DateTime(y, month, DateTime.DaysInMonth(y, month), 1, 0, 0, DateTimeKind.Utc); while (d.DayOfWeek != DayOfWeek.Sunday) d = d.AddDays(-1); return d; }
        return utc >= LastSunday(utc.Year, 3) && utc < LastSunday(utc.Year, 10);
    }

    /// <summary>A local date and time (hours) as UTC, for a place <paramref name="offset"/> hours from UTC, with European summer time when asked.</summary>
    public static DateTime Utc(DateOnly date, double hours, double offset, bool summer)
    {
        var standard = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(hours - offset);
        return summer && EuropeanSummer(standard.AddHours(-1)) ? standard.AddHours(-1) : standard;
    }

    /// <summary>The direction towards the sun in a project's plan (x, y, z), turned by its true north (the base point's angle).</summary>
    public static Vec3 Direction(IModelView m, SunPosition s)
    {
        var th = Rad(Location.Base(m).NorthAngle ?? 0);
        var north = new Vec2(-Math.Sin(th), Math.Cos(th));
        var east = new Vec2(Math.Cos(th), Math.Sin(th));
        var (az, alt) = (Rad(s.Azimuth), Rad(s.Altitude));
        var h = north * Math.Cos(az) + east * Math.Sin(az);
        return new Vec3(h.X * Math.Cos(alt), h.Y * Math.Cos(alt), Math.Sin(alt));
    }

    /// <summary>The project's place (degrees), from its system and survey point; Bucharest when it has none.</summary>
    public static (double Lon, double Lat, bool Known) Place(IModelView m) =>
        GeoRef.LonLat(m, Location.Survey(m)?.At ?? default) is { } ll ? (ll.Lon, ll.Lat, true) : (26.1025, 44.4268, false);

    /// <summary>A place's hours from UTC when none is given: its longitude's zone (Romania's 26° E: UTC+2).</summary>
    public static double ZoneOf(double lon) => Math.Round(lon / 15);

    /// <summary>Whether a place keeps the European summer time when nothing says otherwise: Europe, roughly.</summary>
    public static bool InEurope(double lon, double lat) => lon is > -25 and < 45 && lat is > 34 and < 72;

    /// <summary>
    /// <c>sun.at {date, time, utcOffset?, summerTime?}</c>: where the sun is for the project's place on a day
    /// (yyyy-MM-dd) at a local time (hours): its azimuth and height, the direction towards it in the plan,
    /// the place. The offset defaults to the longitude's zone, the summer time to Europe's rule there.
    /// </summary>
    public static void Register(IModuleRegistry r)
    {
        r.Query("sun.at", (q, a) =>
        {
            var m = q.Model;
            var date = DateOnly.TryParseExact((string?)a["date"] ?? "", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
            var hours = Json.Number(a["time"]) ?? 12;
            var (lon, lat, known) = Place(m);
            var offset = Json.Number(a["utcOffset"]) ?? ZoneOf(lon);
            var summer = a["summerTime"] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : InEurope(lon, lat);
            var sun = At(Utc(date, hours, offset, summer), lat, lon);
            var dir = Direction(m, sun);
            return new JsonObject
            {
                ["azimuth"] = sun.Azimuth, ["altitude"] = sun.Altitude, ["dir"] = new JsonArray(dir.X, dir.Y, dir.Z),
                ["lon"] = lon, ["lat"] = lat, ["placeKnown"] = known, ["utcOffset"] = offset, ["summerTime"] = summer,
            };
        });
    }
}
