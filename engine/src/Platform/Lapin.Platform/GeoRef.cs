using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>An ellipsoid by its semi-major axis and inverse flattening.</summary>
public sealed record Ellipsoid(string Name, double A, double InvF)
{
    public double F => 1 / InvF;
    public double E2 => F * (2 - F);
    public double E => Math.Sqrt(E2);

    public static readonly Ellipsoid Wgs84 = new("WGS 84", 6378137, 298.257223563);
    public static readonly Ellipsoid Krassowsky = new("Krassowsky 1940", 6378245, 298.3);

    /// <summary>Latitude and longitude (radians) and height to earth-centred cartesian coordinates.</summary>
    public (double X, double Y, double Z) ToCartesian(double lat, double lon, double h = 0)
    {
        var (s, c) = (Math.Sin(lat), Math.Cos(lat));
        var nu = A / Math.Sqrt(1 - E2 * s * s);
        return ((nu + h) * c * Math.Cos(lon), (nu + h) * c * Math.Sin(lon), (nu * (1 - E2) + h) * s);
    }

    /// <summary>Earth-centred cartesian coordinates to latitude, longitude (radians) and height.</summary>
    public (double Lat, double Lon, double H) FromCartesian(double x, double y, double z)
    {
        var p = Math.Sqrt(x * x + y * y);
        var lon = Math.Atan2(y, x);
        var lat = Math.Atan2(z, p * (1 - E2));
        double nu = A;
        for (var i = 0; i < 10; i++)
        {
            var s = Math.Sin(lat);
            nu = A / Math.Sqrt(1 - E2 * s * s);
            var next = Math.Atan2(z + E2 * nu * s, p);
            if (Math.Abs(next - lat) < 1e-14) { lat = next; break; }
            lat = next;
        }
        var h = p / Math.Cos(lat) - nu;
        return (lat, lon, h);
    }
}

/// <summary>
/// A seven-parameter Helmert transformation to WGS 84 in the coordinate frame
/// convention (EPSG method 1032): translations (m), rotations (arc seconds),
/// scale (ppm).
/// </summary>
public sealed record Helmert(double Tx, double Ty, double Tz, double Rx, double Ry, double Rz, double Ds)
{
    const double Sec = Math.PI / (180 * 3600);

    public (double X, double Y, double Z) Forward(double x, double y, double z)
    {
        var (rx, ry, rz, m) = (Rx * Sec, Ry * Sec, Rz * Sec, 1 + Ds * 1e-6);
        return (Tx + m * (x + rz * y - ry * z), Ty + m * (-rz * x + y + rx * z), Tz + m * (ry * x - rx * y + z));
    }

    /// <summary>From WGS 84 back: the forward one solved by a few fixed-point steps (the rotations are tiny).</summary>
    public (double X, double Y, double Z) Inverse(double x, double y, double z)
    {
        var (px, py, pz) = (x - Tx, y - Ty, z - Tz);
        var g = (X: px, Y: py, Z: pz);
        for (var i = 0; i < 4; i++)
        {
            var f = Forward(g.X, g.Y, g.Z);
            g = (g.X + (x - f.X), g.Y + (y - f.Y), g.Z + (z - f.Z));
        }
        return g;
    }
}

/// <summary>
/// A coordinate reference system Lapin reads and writes: geographic WGS 84,
/// Stereo 70 (Romania's, oblique stereographic on Krassowsky with the
/// datum shift EPSG uses for the country), UTM on WGS 84, Web Mercator.
/// </summary>
public sealed record Crs(string Id, string Name, string Datum, string Projection, bool Geographic = false)
{
    public override string ToString() => $"{Id} {Name}";
}

/// <summary>
/// Map projections and datum shifts, and the project's plan against its map:
/// the survey point ties a plan point to eastings and northings, the base
/// point's angle turns the plan to true north (as the IFC export's map conversion).
/// </summary>
public static class GeoRef
{
    public const string Wgs84 = "EPSG:4326", Stereo70 = "EPSG:3844", WebMercator = "EPSG:3857";

    /// <summary>Pulkovo 1942(58) to WGS 84 for Romania (EPSG:15993, about 3 m).</summary>
    static readonly Helmert Stereo70Shift = new(2.329, -147.042, -92.08, 0.309, -0.325, -0.497, 5.69);

    /// <summary>The systems Lapin knows by id ("EPSG:3844", "3844" or "epsg:32635"), or null.</summary>
    public static Crs? Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var code = Code(id);
        return code switch
        {
            4326 => new(Wgs84, "WGS 84", "WGS 84", "Geographic", true),
            3844 => new(Stereo70, "Pulkovo 1942(58) / Stereo 70", "Pulkovo 1942(58)", "Oblique Stereographic"),
            3857 => new(WebMercator, "WGS 84 / Pseudo-Mercator", "WGS 84", "Popular Visualisation Pseudo Mercator"),
            >= 32601 and <= 32660 => new($"EPSG:{code}", $"WGS 84 / UTM zone {code - 32600}N", "WGS 84", "Transverse Mercator"),
            >= 32701 and <= 32760 => new($"EPSG:{code}", $"WGS 84 / UTM zone {code - 32700}S", "WGS 84", "Transverse Mercator"),
            _ => null,
        };
    }

    /// <summary>The systems offered for a project.</summary>
    public static readonly string[] Offered = [Stereo70, "EPSG:32634", "EPSG:32635", Wgs84, WebMercator];

    static int Code(string id)
    {
        var s = id.Trim();
        var i = s.LastIndexOfAny([':', '/']);
        if (i >= 0) s = s[(i + 1)..];
        return int.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    /// <summary>A system's coordinates (easting, northing; or longitude, latitude in degrees) to WGS 84 longitude and latitude (degrees).</summary>
    public static (double Lon, double Lat) ToWgs84(string crs, double x, double y)
    {
        var code = Code(crs);
        switch (code)
        {
            case 4326: return (x, y);
            case 3857: return (x / Ellipsoid.Wgs84.A * 180 / Math.PI, (2 * Math.Atan(Math.Exp(y / Ellipsoid.Wgs84.A)) - Math.PI / 2) * 180 / Math.PI);
            case 3844:
            {
                var (lat, lon) = StereoInverse(x, y);
                var k = Ellipsoid.Krassowsky.ToCartesian(lat, lon);
                var w = Stereo70Shift.Forward(k.X, k.Y, k.Z);
                var g = Ellipsoid.Wgs84.FromCartesian(w.X, w.Y, w.Z);
                return (Deg(g.Lon), Deg(g.Lat));
            }
            case >= 32601 and <= 32760:
            {
                var (zone, south) = code > 32700 ? (code - 32700, true) : (code - 32600, false);
                var (lat, lon) = TmInverse(Ellipsoid.Wgs84, Rad(zone * 6 - 183), 0.9996, 500000, south ? 10000000 : 0, x, y);
                return (Deg(lon), Deg(lat));
            }
            default: throw new ArgumentException($"Lapin does not know the coordinate system {crs}.");
        }
    }

    /// <summary>WGS 84 longitude and latitude (degrees) to a system's coordinates.</summary>
    public static (double X, double Y) FromWgs84(string crs, double lon, double lat)
    {
        var code = Code(crs);
        switch (code)
        {
            case 4326: return (lon, lat);
            case 3857: return (Rad(lon) * Ellipsoid.Wgs84.A, Math.Log(Math.Tan(Math.PI / 4 + Rad(lat) / 2)) * Ellipsoid.Wgs84.A);
            case 3844:
            {
                var w = Ellipsoid.Wgs84.ToCartesian(Rad(lat), Rad(lon));
                var k = Stereo70Shift.Inverse(w.X, w.Y, w.Z);
                var g = Ellipsoid.Krassowsky.FromCartesian(k.X, k.Y, k.Z);
                return StereoForward(g.Lat, g.Lon);
            }
            case >= 32601 and <= 32760:
            {
                var (zone, south) = code > 32700 ? (code - 32700, true) : (code - 32600, false);
                return TmForward(Ellipsoid.Wgs84, Rad(zone * 6 - 183), 0.9996, 500000, south ? 10000000 : 0, Rad(lat), Rad(lon));
            }
            default: throw new ArgumentException($"Lapin does not know the coordinate system {crs}.");
        }
    }

    /// <summary>Coordinates from one system to another (through WGS 84; the same system: unchanged).</summary>
    public static (double X, double Y) Convert(string from, string to, double x, double y)
    {
        if (Code(from) == Code(to)) return (x, y);
        var (lon, lat) = ToWgs84(from, x, y);
        return FromWgs84(to, lon, lat);
    }

    static double Rad(double d) => d * Math.PI / 180;
    static double Deg(double r) => r * 180 / Math.PI;

    // ─── Oblique stereographic (EPSG 9809, PROJ's sterea): a conformal sphere (Gauss), then a stereographic projection of it ───

    const double Lat0 = 46 * Math.PI / 180, Lon0 = 25 * Math.PI / 180, K0 = 0.99975, Fe = 500000, Fn = 500000;

    sealed record Gauss(double C, double K, double RatExp, double E, double Chi0, double R);

    static readonly Gauss G = GaussOf(Ellipsoid.Krassowsky.E, Lat0);

    static Gauss GaussOf(double e, double phi0)
    {
        var es = e * e;
        var (sphi, cphi2) = (Math.Sin(phi0), Math.Pow(Math.Cos(phi0), 2));
        var r = Math.Sqrt(1 - es) / (1 - es * sphi * sphi);
        var c = Math.Sqrt(1 + es * cphi2 * cphi2 / (1 - es));
        var chi = Math.Asin(sphi / c);
        var ratexp = 0.5 * c * e;
        var k = Math.Tan(0.5 * chi + Math.PI / 4) / (Math.Pow(Math.Tan(0.5 * phi0 + Math.PI / 4), c) * Srat(e * sphi, ratexp));
        return new Gauss(c, k, ratexp, e, chi, r);
    }

    static double Srat(double esinp, double exp) => Math.Pow((1 - esinp) / (1 + esinp), exp);

    static (double X, double Y) StereoForward(double lat, double lon)
    {
        var a = Ellipsoid.Krassowsky.A;
        var phi = 2 * Math.Atan(G.K * Math.Pow(Math.Tan(0.5 * lat + Math.PI / 4), G.C) * Srat(G.E * Math.Sin(lat), G.RatExp)) - Math.PI / 2;
        var lam = G.C * (lon - Lon0);
        var (sinc, cosc, cosl) = (Math.Sin(phi), Math.Cos(phi), Math.Cos(lam));
        var (sinc0, cosc0) = (Math.Sin(G.Chi0), Math.Cos(G.Chi0));
        var k = K0 * 2 * G.R / (1 + sinc0 * sinc + cosc0 * cosc * cosl);
        return (Fe + a * k * cosc * Math.Sin(lam), Fn + a * k * (cosc0 * sinc - sinc0 * cosc * cosl));
    }

    static (double Lat, double Lon) StereoInverse(double e, double n)
    {
        var a = Ellipsoid.Krassowsky.A;
        var x = (e - Fe) / a / K0;
        var y = (n - Fn) / a / K0;
        var (sinc0, cosc0) = (Math.Sin(G.Chi0), Math.Cos(G.Chi0));
        var rho = Math.Sqrt(x * x + y * y);
        double phi, lam;
        if (rho > 1e-15)
        {
            var c = 2 * Math.Atan2(rho, 2 * G.R);
            var (sinc, cosc) = (Math.Sin(c), Math.Cos(c));
            phi = Math.Asin(cosc * sinc0 + y * sinc * cosc0 / rho);
            lam = Math.Atan2(x * sinc, rho * cosc0 * cosc - y * sinc0 * sinc);
        }
        else (phi, lam) = (G.Chi0, 0);
        // Back from the conformal sphere.
        var num = Math.Pow(Math.Tan(0.5 * phi + Math.PI / 4) / G.K, 1 / G.C);
        var lat = phi;
        for (var i = 0; i < 20; i++)
        {
            var next = 2 * Math.Atan(num * Srat(G.E * Math.Sin(lat), -0.5 * G.E)) - Math.PI / 2;
            if (Math.Abs(next - lat) < 1e-14) { lat = next; break; }
            lat = next;
        }
        return (lat, lam / G.C + Lon0);
    }

    // ─── Transverse Mercator (Krüger's series to n⁶, as Karney gives them) ───

    static (double[] Alpha, double[] Beta, double A) TmSeries(Ellipsoid el)
    {
        var n = el.F / (2 - el.F);
        var (n2, n3, n4, n5, n6) = (n * n, n * n * n, Math.Pow(n, 4), Math.Pow(n, 5), Math.Pow(n, 6));
        var alpha = new[]
        {
            n / 2 - 2 * n2 / 3 + 5 * n3 / 16 + 41 * n4 / 180 - 127 * n5 / 288 + 7891 * n6 / 37800,
            13 * n2 / 48 - 3 * n3 / 5 + 557 * n4 / 1440 + 281 * n5 / 630 - 1983433 * n6 / 1935360,
            61 * n3 / 240 - 103 * n4 / 140 + 15061 * n5 / 26880 + 167603 * n6 / 181440,
            49561 * n4 / 161280 - 179 * n5 / 168 + 6601661 * n6 / 7257600,
            34729 * n5 / 80640 - 3418889 * n6 / 1995840,
            212378941 * n6 / 319334400,
        };
        var beta = new[]
        {
            n / 2 - 2 * n2 / 3 + 37 * n3 / 96 - n4 / 360 - 81 * n5 / 512 + 96199 * n6 / 604800,
            n2 / 48 + n3 / 15 - 437 * n4 / 1440 + 46 * n5 / 105 - 1118711 * n6 / 3870720,
            17 * n3 / 480 - 37 * n4 / 840 - 209 * n5 / 4480 + 5569 * n6 / 90720,
            4397 * n4 / 161280 - 11 * n5 / 504 - 830251 * n6 / 7257600,
            4583 * n5 / 161280 - 108847 * n6 / 3991680,
            20648693 * n6 / 638668800,
        };
        var a = el.A / (1 + n) * (1 + n2 / 4 + n4 / 64 + n6 / 256);
        return (alpha, beta, a);
    }

    static (double X, double Y) TmForward(Ellipsoid el, double lon0, double k0, double fe, double fn, double lat, double lon)
    {
        var (alpha, _, a) = TmSeries(el);
        var e = el.E;
        var l = lon - lon0;
        var t = Math.Sinh(Math.Atanh(Math.Sin(lat)) - e * Math.Atanh(e * Math.Sin(lat)));
        var xi0 = Math.Atan2(t, Math.Cos(l));
        var eta0 = Math.Atanh(Math.Sin(l) / Math.Sqrt(1 + t * t));
        var (xi, eta) = (xi0, eta0);
        for (var j = 1; j <= 6; j++)
        {
            xi += alpha[j - 1] * Math.Sin(2 * j * xi0) * Math.Cosh(2 * j * eta0);
            eta += alpha[j - 1] * Math.Cos(2 * j * xi0) * Math.Sinh(2 * j * eta0);
        }
        return (fe + k0 * a * eta, fn + k0 * a * xi);
    }

    static (double Lat, double Lon) TmInverse(Ellipsoid el, double lon0, double k0, double fe, double fn, double x, double y)
    {
        var (_, beta, a) = TmSeries(el);
        var e = el.E;
        var xi = (y - fn) / (k0 * a);
        var eta = (x - fe) / (k0 * a);
        var (xi1, eta1) = (xi, eta);
        for (var j = 1; j <= 6; j++)
        {
            xi1 -= beta[j - 1] * Math.Sin(2 * j * xi) * Math.Cosh(2 * j * eta);
            eta1 -= beta[j - 1] * Math.Cos(2 * j * xi) * Math.Sinh(2 * j * eta);
        }
        var taup = Math.Sin(xi1) / Math.Sqrt(Math.Pow(Math.Sinh(eta1), 2) + Math.Pow(Math.Cos(xi1), 2));
        var lam = Math.Atan2(Math.Sinh(eta1), Math.Cos(xi1));
        var tau = taup;
        for (var i = 0; i < 10; i++)
        {
            var sigma = Math.Sinh(e * Math.Atanh(e * tau / Math.Sqrt(1 + tau * tau)));
            var taui = tau * Math.Sqrt(1 + sigma * sigma) - sigma * Math.Sqrt(1 + tau * tau);
            var d = (taup - taui) / Math.Sqrt(1 + taui * taui) * (1 + (1 - el.E2) * tau * tau) / ((1 - el.E2) * Math.Sqrt(1 + tau * tau));
            tau += d;
            if (Math.Abs(d) < 1e-15) break;
        }
        return (Math.Atan(tau), lam + lon0);
    }

    // ─── The project's plan and its map ───

    /// <summary>The project's coordinate system (its location's <c>crs</c>), or null when it has none.</summary>
    public static string? ProjectCrs(IModelView m) => Location.Crs(m);

    /// <summary>Whether the project is tied to map coordinates (it has a survey point).</summary>
    public static bool Georeferenced(IModelView m) => Location.Survey(m) != null;

    /// <summary>A plan point in the project's map coordinates (eastings, northings) through the survey point and the angle to true north.</summary>
    public static (double E, double N) ToMap(IModelView m, Vec2 p)
    {
        var sv = Location.Survey(m) ?? new Location.Point(default, 0, 0, 0);
        var (c, sn) = Turn(m);
        var d = p - sv.At;
        return ((sv.Easting ?? 0) + c * d.X - sn * d.Y, (sv.Northing ?? 0) + sn * d.X + c * d.Y);
    }

    /// <summary>Map coordinates (eastings, northings) in the project's plan.</summary>
    public static Vec2 ToPlan(IModelView m, double e, double n)
    {
        var sv = Location.Survey(m) ?? new Location.Point(default, 0, 0, 0);
        var (c, sn) = Turn(m);
        var (de, dn) = (e - (sv.Easting ?? 0), n - (sv.Northing ?? 0));
        return sv.At + new Vec2(c * de + sn * dn, -sn * de + c * dn);
    }

    /// <summary>The project's X axis in map coordinates (cos, sin), from the base point's angle to true north.</summary>
    static (double C, double S) Turn(IModelView m)
    {
        var th = (Location.Base(m).NorthAngle ?? 0) * Math.PI / 180;
        return (Math.Cos(th), -Math.Sin(th));
    }

    /// <summary>A plan point's longitude and latitude (degrees), when the project has a system and a survey point.</summary>
    public static (double Lon, double Lat)? LonLat(IModelView m, Vec2 p)
    {
        if (ProjectCrs(m) is not { } crs || !Georeferenced(m) || Get(crs) == null) return null;
        var (e, n) = ToMap(m, p);
        return ToWgs84(crs, e, n);
    }
}
