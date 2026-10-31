using System.Text.Json.Nodes;
using Lapin.Civil;

namespace Lapin.Engine.Tests;

/// <summary>
/// The stationing with station equations (docs/plan-rail.md, R4): regions,
/// the station of a distance and back, a gap and an overlap, the kilometre
/// posts per region, the kilometrage written and read.
/// </summary>
public class StationingTests
{
    [Fact]
    public void Without_equations_the_station_is_the_start_plus_the_distance()
    {
        var st = new Stationing(1000, 2500);
        Assert.Single(st.Regions);
        Assert.Equal((1250, 0), st.StationOf(250));
        Assert.Equal(250, st.DistanceOf(1250));
        Assert.Null(st.DistanceOf(900));
        Assert.Equal(["km 2", "km 3"], st.Generate(1000).Where(r => r.Station > 1000).Select(r => r.Name));
    }

    [Fact]
    public void A_gap_skips_stations_and_an_overlap_repeats_them()
    {
        // At 400 m the stationing jumps from 1+400 to 2+000 (a gap of 600); at 900 m it falls back from 2+500 to 2+300 (an overlap of 200).
        var st = new Stationing(1000, 1500, [new(900, 0, 2300), new(400, 0, 2000)]);
        Assert.Equal([400, 900], st.Equations.Select(e => e.At));
        Assert.Equal([1400, 2500], st.Equations.Select(e => e.Back));
        Assert.Equal(3, st.Regions.Count);
        Assert.Equal((2000, 1), st.StationOf(400));
        Assert.Equal((2450, 1), st.StationOf(850));
        Assert.Equal((2300, 2), st.StationOf(900));
        // 1+700 exists nowhere; 2+400 twice.
        Assert.Null(st.DistanceOf(1700));
        Assert.Equal(2, st.Occurrences(2400).Count);
        Assert.Equal(800, st.DistanceOf(2400, 1));
        Assert.Equal(1000, st.DistanceOf(2400, 2));
        Assert.Equal(800, st.DistanceOf(2400));
        // The km posts: km 2 at the gap's ahead side, km 2 is not repeated by the overlap (2+300 … 2+900 holds none), none between 1+000 and 1+400.
        var posts = st.Generate(1000);
        Assert.Equal(["km 1", "km 2"], posts.Select(p => p.Name));
        Assert.Equal(400, st.DistanceOf(posts[1].Station, posts[1].Region));
        // Equations outside the length or at the start are dropped.
        Assert.Empty(new Stationing(0, 100, [new(0, 0, 50), new(100, 0, 50), new(150, 0, 50)]).Equations);
    }

    [Fact]
    public void The_kilometrage_is_written_and_read_back()
    {
        Assert.Equal("12+345,600", Stationing.FormatKm(12345.6));
        Assert.Equal("1+000,000", Stationing.FormatKm(999.9996));
        Assert.Equal("-0+012,500", Stationing.FormatKm(-12.5));
        Assert.Equal("0+000", Stationing.FormatKm(0, 0));
        Assert.Equal(12345.6, Stationing.ParseKm("12+345,6"));
        Assert.Equal(12345.6, Stationing.ParseKm("km 12+345.600"));
        Assert.Equal(12345.6, Stationing.ParseKm("12345,6"));
        Assert.Equal(-12.5, Stationing.ParseKm("-0+012,5"));
        Assert.Null(Stationing.ParseKm("12+1000"));
        Assert.Null(Stationing.ParseKm("abc"));
        Assert.Equal("12+100", Stationing.ReferentName(12100));
        Assert.Equal("km 12", Stationing.ReferentName(12000));
    }

    [Fact]
    public void Equations_and_referents_round_trip_through_JSON()
    {
        var eq = Stationing.ReadEquations(Stationing.WriteEquations([new(400, 1400, 2000)]));
        Assert.Equal(new StationEquation(400, 1400, 2000), Assert.Single(eq));
        var rows = Stationing.ReadEquations(System.Text.Json.Nodes.JsonNode.Parse("[[400, 2000], {\"at\": 900, \"ahead\": 2300}]"));
        Assert.Equal([2000, 2300], rows.Select(r => r.Ahead));
        var refs = Stationing.ReadReferents(Stationing.WriteReferents([new("h1", "Halt", 2400, 2, "station")]));
        Assert.Equal(new Referent("h1", "Halt", 2400, 2, "station"), Assert.Single(refs));
        var bare = Stationing.ReadReferents(System.Text.Json.Nodes.JsonNode.Parse("[{\"station\": 12000}]"));
        Assert.Equal(("r1", "km 12", "other"), (bare[0].Id, bare[0].Name, bare[0].Kind));
    }

    [Fact]
    public void An_alignment_with_equations_equates_its_stations_and_labels_them()
    {
        var a = new Alignment([new HorizontalSegment(HorizontalType.Line, new(0, 0), 0, 0, 0, 2500)], StartStation: 800, Equations: [new StationEquation(1000, 0, 3000)]);
        Assert.Equal(2500, a.Length);
        Assert.Equal(1300, a.Equated(1300));
        // At the break the stationing jumps 1+800 → 3+000; the label follows it.
        Assert.Equal(3000, a.Equated(1800));
        Assert.Equal("3+500.00", a.Km(2300));
        // Without equations the station is itself.
        Assert.Equal(2300, (a with { Equations = null }).Equated(2300));
        Assert.Equal("2+300.00", (a with { Equations = null }).Km(2300));
    }
}
