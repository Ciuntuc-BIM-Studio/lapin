using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>The sun for the project's place (docs/plan-visualisation.md, stage 1; taken from the urban module's sun study): over Bucharest through the year.</summary>
public class SunTests
{
    const double Lat = 44.4268, Lon = 26.1025;

    /// <summary>The highest the sun goes on a day (local time, EET and summer time), and when.</summary>
    static (double Altitude, double Hour) Noon(DateOnly d)
    {
        var (best, at) = (-90.0, 0.0);
        for (var m = 9 * 60; m <= 16 * 60; m++)
        {
            var s = Sun.At(Sun.Utc(d, m / 60.0, 2, true), Lat, Lon);
            if (s.Altitude > best) (best, at) = (s.Altitude, m / 60.0);
        }
        return (best, at);
    }

    [Fact]
    public void The_sun_at_noon_over_Bucharest_at_the_equinox_and_the_solstices()
    {
        // 90° less the latitude, plus the declination (−23.44° to 23.44°); solar noon after 12:00 by the longitude (26.1° E of a 30° E zone) and the equation of time.
        var (eq, eqAt) = Noon(new DateOnly(2026, 3, 20));
        Assert.InRange(eq, 45.3, 46.0);
        Assert.InRange(eqAt, 12.25, 12.50);
        var (su, suAt) = Noon(new DateOnly(2026, 6, 21));
        Assert.InRange(su, 68.8, 69.2);
        // Summer time: an hour later on the clock.
        Assert.InRange(suAt, 13.15, 13.40);
        var (wi, _) = Noon(new DateOnly(2026, 12, 21));
        Assert.InRange(wi, 21.9, 22.4);
        // At solar noon the sun is due south.
        Assert.Equal(180, Sun.At(Sun.Utc(new DateOnly(2026, 6, 21), suAt, 2, true), Lat, Lon).Azimuth, 1.0);
        Assert.True(Sun.EuropeanSummer(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)));
        Assert.False(Sun.EuropeanSummer(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void The_sun_for_the_project_is_turned_by_its_true_north_and_takes_its_zone_from_the_longitude()
    {
        var f = new Fixture();
        // No coordinate system: Bucharest, UTC+2 with summer time.
        var s = (JsonObject)f.Session.Query("sun.at", new JsonObject { ["date"] = "2026-06-21", ["time"] = 13.3 })!;
        Assert.False((bool)s["placeKnown"]!);
        Assert.Equal(2, (double)s["utcOffset"]!);
        Assert.True((bool)s["summerTime"]!);
        Assert.Equal(180, (double)s["azimuth"]!, 2.0);
        // Due south in a plan whose north is up: towards −Y.
        var dir = (JsonArray)s["dir"]!;
        Assert.True((double)dir[1]! < -0.3 && Math.Abs((double)dir[0]!) < 0.05 && (double)dir[2]! > 0.9);
        // The plan's north turned by 90° (the base point's angle): south turns with it, from −Y to ±X.
        f.Run("project.location", new JsonObject { ["base"] = new JsonObject { ["northAngle"] = 90 } });
        var t = (JsonArray)((JsonObject)f.Session.Query("sun.at", new JsonObject { ["date"] = "2026-06-21", ["time"] = 13.3 })!)["dir"]!;
        Assert.True(Math.Abs((double)t[1]!) < 0.05 && Math.Abs((double)t[0]!) > 0.3);
        // At night the sun is under the horizon.
        var night = (JsonObject)f.Session.Query("sun.at", new JsonObject { ["date"] = "2026-06-21", ["time"] = 1 })!;
        Assert.True((double)night["altitude"]! < 0);
    }
}
