using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Rebar;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Beams stop at the column face (decision 225): drawn from column axis to column axis, a beam's body, plan outline,
/// type stirrups and quantities take the clear span between the faces of the columns at its ends, and the stirrups are
/// laid per clear piece between the columns it passes through; the axis stored stays from axis to axis.
/// </summary>
public class BeamSpansTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule(), new DraftingModule(), new RebarModule());

    /// <summary>A 0,40 × 0,40 column type (optionally of its own height), one column of it at a point.</summary>
    static string ColumnType(Fixture f, double? height = null)
    {
        var a = new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.4, ["depth"] = 0.4 } };
        if (height != null) a["height"] = height;
        return (string)f.Run("structure.createColumnType", a)!["id"]!;
    }

    static string Column(Fixture f, string type, double x, double y) =>
        (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = x, ["y"] = y, ["typeId"] = type })!["id"]!;

    static string Beam(Fixture f, double x1, double y1, double x2, double y2, double rise = 0)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2 };
        if (rise != 0) a[PathCurve.Key] = rise;
        return (string)f.Run("structure.addBeam", a)!["id"]!;
    }

    static Dictionary<string, double> Quantities(Fixture f, string beam) =>
        f.Session.Quantities(beam).Single(q => q.Name == "Qto_BeamBaseQuantities").Quantities.ToDictionary(q => q.Name, q => q.Value);

    static (double Min, double Max) OutlineX(Fixture f, string beam)
    {
        var pts = f.Session.Geometry(beam)!.Plan!.Single(p => p.Kind == "outline").Points;
        return (pts.Min(p => p.X), pts.Max(p => p.X));
    }

    static TypeStirrups.Laid Stirrups(Fixture f, string beam)
    {
        f.Run("rebar.setTypeStirrups", new JsonObject { ["id"] = f.Model.RelatingOf(beam, IfcClass.RelDefinesByType)!.Id, ["enabled"] = true, ["step"] = 0.15 });
        return TypeStirrups.Of(f.Model, f.Model.Get(beam)!)!;
    }

    [Fact]
    public void A_beam_between_two_columns_runs_from_face_to_face_in_its_body_outline_quantities_and_stirrups()
    {
        var f = F();
        var type = ColumnType(f);
        Column(f, type, 0, 0);
        Column(f, type, 6, 0);
        var beam = Beam(f, 0, 0, 6, 0);
        // The axis stays from column axis to column axis; the span is 0,20 … 5,80.
        Assert.Equal((new Lapin.Geometry.Vec2(0, 0), new Lapin.Geometry.Vec2(6, 0)), (Joins.Start(f.Model.Get(beam)!), Joins.End(f.Model.Get(beam)!)));
        var span = Beams.SpanOf(f.Model, f.Model.Get(beam)!);
        Assert.Equal((0.2, 5.8), (Math.Round(span.S0, 9), Math.Round(span.S1, 9)));
        Assert.Equal(5.6, span.ClearLength, 9);
        var b = f.Bounds(beam);
        Assert.Equal((0.2, 5.8), (Math.Round(b.Min.X, 9), Math.Round(b.Max.X, 9)));
        var (x0, x1) = OutlineX(f, beam);
        Assert.Equal((0.2, 5.8), (Math.Round(x0, 9), Math.Round(x1, 9)));
        var q = Quantities(f, beam);
        Assert.Equal(5.6, q["Length"], 9);
        Assert.Equal(0.25 * 0.4 * 5.6, q["GrossVolume"], 9);
        Assert.Equal(0.25 * 0.4 * 5.6, q["NetVolume"], 6);
        // Stirrups at 150 over the clear span: the first on the face at 0,20, the last at 5,80.
        var laid = Stirrups(f, beam);
        Assert.Equal(Runs.Positions(5.6, 0.15).Count, laid.Stations.Count);
        Assert.Equal(0.2, laid.Stations[0], 9);
        Assert.Equal(5.8, laid.Stations[^1], 9);
        Assert.Equal(0.2, laid.S0, 9);
        Assert.Equal(5.6, laid.Length, 9);
        Assert.Equal(0.2, laid.Start.X, 9);
        Assert.Equal("150", laid.StepText);
        Assert.All(laid.LoopsAt(laid.Stations[0])[0].Corners, p => Assert.Equal(0.2, p.X, 9));
    }

    [Fact]
    public void Two_beams_in_line_through_a_column_each_stop_at_its_face()
    {
        var f = F();
        var type = ColumnType(f);
        foreach (var x in new[] { 0.0, 6, 12 }) Column(f, type, x, 0);
        var a = Beam(f, 0, 0, 6, 0);
        var b = Beam(f, 6, 0, 12, 0);
        // Whether or not a join relation stands between them, neither crosses the column at x = 6.
        Assert.Equal(5.8, OutlineX(f, a).Max, 9);
        Assert.Equal(6.2, OutlineX(f, b).Min, 9);
        Assert.Equal(5.8, f.Bounds(a).Max.X, 9);
        Assert.Equal(6.2, f.Bounds(b).Min.X, 9);
        Assert.Equal(5.6, Quantities(f, a)["Length"], 9);
        Assert.Equal(5.6, Quantities(f, b)["Length"], 9);
    }

    [Fact]
    public void A_column_in_mid_span_splits_the_length_and_the_stirrups_into_two_pieces()
    {
        var f = F();
        var type = ColumnType(f);
        foreach (var x in new[] { 0.0, 3, 6 }) Column(f, type, x, 0);
        var beam = Beam(f, 0, 0, 6, 0);
        var span = Beams.SpanOf(f.Model, f.Model.Get(beam)!);
        Assert.Equal([(0.2, 2.8), (3.2, 5.8)], span.Pieces.Select(p => (Math.Round(p.A, 9), Math.Round(p.B, 9))).ToList());
        Assert.Equal(5.2, Quantities(f, beam)["Length"], 9);
        Assert.Equal(0.25 * 0.4 * 5.2, Quantities(f, beam)["GrossVolume"], 9);
        // The footprint stays whole across the middle column (it draws over the beam in plan); the body is cut by it.
        Assert.Equal((0.2, 5.8), (Math.Round(OutlineX(f, beam).Min, 9), Math.Round(OutlineX(f, beam).Max, 9)));
        Assert.Equal(0.25 * 0.4 * 5.2, Quantities(f, beam)["NetVolume"], 6);
        var laid = Stirrups(f, beam);
        Assert.Equal(2 * Runs.Positions(2.6, 0.15).Count, laid.Stations.Count);
        Assert.DoesNotContain(laid.Stations, s => s > 2.8 + 1e-9 && s < 3.2 - 1e-9);
        Assert.Contains(laid.Stations, s => Math.Abs(s - 2.8) < 1e-9);
        Assert.Contains(laid.Stations, s => Math.Abs(s - 3.2) < 1e-9);
        Assert.Equal(5.6, laid.Length, 9);
        Assert.Equal(2.6, laid.Piece!.Value, 9);
    }

    [Fact]
    public void A_beam_touching_no_column_and_one_under_a_column_too_short_to_reach_it_are_not_trimmed()
    {
        var f = F();
        var free = Beam(f, 0, 0, 6, 0);
        Assert.Equal(6, Quantities(f, free)["Length"], 9);
        Assert.Equal(41, Stirrups(f, free).Stations.Count);
        Assert.False(Beams.SpanOf(f.Model, f.Model.Get(free)!).TrimmedStart);
        // A column 1 m high ends under the beam (its bottom at +2,60): the beam over it is whole.
        var low = ColumnType(f, 1.0);
        Column(f, low, 0, 5);
        Column(f, low, 6, 5);
        var over = Beam(f, 0, 5, 6, 5);
        var span = Beams.SpanOf(f.Model, f.Model.Get(over)!);
        Assert.Empty(span.Columns);
        Assert.Equal(6, Quantities(f, over)["Length"], 9);
        Assert.Equal((0, 6), (Math.Round(OutlineX(f, over).Min, 9), Math.Round(OutlineX(f, over).Max, 9)));
        // A column the axis passes beside without crossing leaves the beam alone.
        Column(f, ColumnType(f), 3, 10.5);
        var beside = Beam(f, 0, 10, 6, 10);
        Assert.Equal(6, Quantities(f, beside)["Length"], 9);
    }

    [Fact]
    public void A_beam_lying_entirely_in_columns_has_no_body_and_a_curved_beam_stops_at_the_faces_too()
    {
        var f = F();
        var type = ColumnType(f);
        Column(f, type, 0, 0);
        var inside = Beam(f, -0.1, 0, 0.1, 0);
        var g = f.Session.Geometry(inside)!;
        Assert.Empty(g.Parts);
        Assert.Contains("The beam lies entirely in columns.", g.Diagnostics!);
        // An arc between two columns: the span is found along the arc, within a micron of the faces.
        Column(f, type, 6, 0);
        var arc = Beam(f, 0, 0, 6, 0, rise: 1.5);
        var span = Beams.SpanOf(f.Model, f.Model.Get(arc)!);
        Assert.True(span.TrimmedStart && span.TrimmedEnd);
        // Each face found is on the column's boundary (the steep arc leaves the square through its side, not its front).
        var c = span.Curve;
        var (p0, p1) = (c.PointAt(span.S0), c.PointAt(span.S1));
        Assert.Equal(0.2, Math.Max(Math.Abs(p0.X), Math.Abs(p0.Y)), 5);
        Assert.Equal(0.2, Math.Max(Math.Abs(p1.X - 6), Math.Abs(p1.Y)), 5);
        Assert.True(span.S0 > 0.2 && span.S1 < c.Length - 0.2);
        Assert.Equal(c.Length - span.S0 - (c.Length - span.S1), Quantities(f, arc)["Length"], 9);
        Assert.NotEmpty(f.Session.Geometry(arc)!.Parts);
    }
}
