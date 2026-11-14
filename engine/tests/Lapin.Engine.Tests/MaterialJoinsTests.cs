using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// A material that stays out of joins (forum topic 59, <c>joins: false</c>, the Materials dialog's "Joins with other
/// elements"): its elements keep their shape where they meet others — not joined automatically (and their joins taken off
/// when the material stops joining, made again when it joins again, one undo step), not cut by the cut order nor cutting
/// others, and drawn with their own outline in plans and sections. Materials that join (the default) do as before.
/// </summary>
public sealed class MaterialJoinsTests
{
    static Fixture F() => new(new ArchitectureModule(), new StructureModule());
    static string Id(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name).Id;
    static void Joins(Fixture f, string material, bool joins) => f.Run("material.update", new JsonObject { ["id"] = Id(f, material), ["joins"] = joins });

    static IEnumerable<(double X0, double Y0, double X1, double Y1)> Segments(JsonNode d)
    {
        foreach (var pen in d["pens"]!.AsArray())
            if (pen!["segments"]?.AsArray() is { } s)
                for (var k = 0; k + 3 < s.Count; k += 4) yield return ((double)s[k]!, (double)s[k + 1]!, (double)s[k + 2]!, (double)s[k + 3]!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Walls_of_a_material_that_stays_out_of_joins_are_not_joined_and_show_their_joint(bool joins)
    {
        var f = F();
        var type = (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Steel panel", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Steel", ["thickness"] = 0.2 }) })!["id"]!;
        if (!joins) Joins(f, "Steel", false);
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(5, 0, 10, 0, type);
        var c = f.Wall(10, 0, 10, 4, type);
        Assert.Equal(joins, Lapin.Platform.Joins.Between(f.Model, a, b) != null);
        Assert.Equal(joins, Lapin.Platform.Joins.Between(f.Model, b, c) != null);
        var plan = f.Session.Query("view.drawing", new JsonObject { ["id"] = "plan:" + f.Storey })!;
        Assert.Equal(!joins, Segments(plan).Any(s => Math.Abs(s.X0 - 5) < 1e-6 && Math.Abs(s.X1 - 5) < 1e-6));
    }

    [Fact]
    public void A_material_that_stops_joining_takes_its_elements_joins_off_and_joining_again_makes_them()
    {
        var f = F();
        var type = (string)f.Run("architecture.createWallType", new JsonObject { ["name"] = "Timber", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Timber", ["thickness"] = 0.2 }) })!["id"]!;
        var a = f.Wall(0, 0, 5, 0, type);
        var b = f.Wall(5, 0, 5, 3, type);
        var brick = f.Type("Brick", ("Brick", 0.25, true));
        var other = f.Wall(0, 6, 5, 6, brick);
        var o2 = f.Wall(5, 6, 5, 9, brick);
        Assert.NotNull(Lapin.Platform.Joins.Between(f.Model, a, b));
        Joins(f, "Timber", false);
        Assert.Null(Lapin.Platform.Joins.Between(f.Model, a, b));
        Assert.False(MaterialGraphics.JoinsOf(f.Model.Get(Id(f, "Timber"))));
        // Others keep theirs.
        Assert.NotNull(Lapin.Platform.Joins.Between(f.Model, other, o2));
        // One undo step: the flag and the joins back together.
        f.Session.Undo();
        Assert.NotNull(Lapin.Platform.Joins.Between(f.Model, a, b));
        Assert.True(MaterialGraphics.JoinsOf(f.Model.Get(Id(f, "Timber"))));
        f.Session.Redo();
        Assert.Null(Lapin.Platform.Joins.Between(f.Model, a, b));
        Joins(f, "Timber", true);
        Assert.Equal("L", (string?)Lapin.Platform.Joins.Between(f.Model, a, b)?.Attrs?["kind"]);
        // Joining is the default: not kept on the material.
        Assert.Null(f.Model.Get(Id(f, "Timber"))!.Attrs["joins"]);
    }

    /// <summary>A beam 0.2 × 0.4 from (0, 0) to (10, 0) of <paramref name="beamMaterial"/>, through a 0.3 × 0.3 concrete column at (5, 0).</summary>
    static (Fixture F, string Beam) BeamThroughColumn(string beamMaterial)
    {
        var f = F();
        var col = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.3 }, ["material"] = "Reinforced concrete" })!["id"]!;
        f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 5, ["y"] = 0, ["typeId"] = col });
        var bt = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.2, ["depth"] = 0.4 }, ["material"] = beamMaterial })!["id"]!;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 10, ["y2"] = 0, ["typeId"] = bt })!["id"]!;
        return (f, beam);
    }

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void A_steel_beam_that_stays_out_of_joins_runs_whole_through_a_concrete_column()
    {
        // Joining (the default): the column cuts its width out of the beam, as before.
        var (f, beam) = BeamThroughColumn("Steel S235");
        Assert.Equal(0.2 * 0.4 * (10 - 0.3), Volume(f, beam), 6);
        Assert.Equal(10 - 0.3, Beams.SpanOf(f.Model, f.Model.Get(beam)!).ClearLength, 6);
        // Out of joins: whole, and measured whole.
        Joins(f, "Steel S235", false);
        Assert.Equal(0.2 * 0.4 * 10, Volume(f, beam), 6);
        Assert.Equal(10, Beams.SpanOf(f.Model, f.Model.Get(beam)!).ClearLength, 6);
        // A concrete column of a material that stays out of joins cuts no concrete beam either.
        var (g, concreteBeam) = BeamThroughColumn("Reinforced concrete");
        Assert.Equal(0.2 * 0.4 * (10 - 0.3), Volume(g, concreteBeam), 6);
        Joins(g, "Reinforced concrete", false);
        Assert.Equal(0.2 * 0.4 * 10, Volume(g, concreteBeam), 6);
    }

    // ------------------------------------------------------------ drawings

    static DPolygon Box(string element, string cls, string material, double x0, double x1, double y0, double y1) =>
        new([[new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]], element, cls, null, material);
    static DLine Line(string element, string cls, double x0, double y0, double x1, double y1) => new(new(x0, y0), new(x1, y1), "cut", element, cls);

    /// <summary>A section across a beam (0..4, its top at 3) meeting a column (4..4.3, up to 3): the side they share at x = 4.</summary>
    static ViewDrawing BeamMeetingColumn(string beamMaterial, string columnMaterial) => new(
        [Box("beam", IfcClass.Beam, beamMaterial, 0, 4, 2.6, 3), Box("column", IfcClass.Column, columnMaterial, 4, 4.3, 0, 3)],
        [Line("beam", IfcClass.Beam, 0, 2.6, 4, 2.6), Line("beam", IfcClass.Beam, 0, 3, 4, 3), Line("beam", IfcClass.Beam, 0, 2.6, 0, 3), Line("beam", IfcClass.Beam, 4, 2.6, 4, 3),
         Line("column", IfcClass.Column, 4, 0, 4, 3), Line("column", IfcClass.Column, 4.3, 0, 4.3, 3)]);

    static ResolvedMaterial Graphics(string id, string name, bool joins) =>
        MaterialGraphics.Resolve(new Entity(id, IfcClass.Material, name, joins ? [] : new JsonObject { ["joins"] = false }), "section");

    [Theory]
    [InlineData("Reinforced concrete", true, true)]
    [InlineData("Steel", true, true)]
    [InlineData("Steel", false, false)]
    public void A_beam_meeting_a_column_of_its_material_merges_with_it_only_when_the_material_joins(string material, bool joins, bool merged)
    {
        var d = BeamMeetingColumn("m", "m");
        var g = Graphics("m", material, joins);
        var r = MaterialDrawing.Apply(d, _ => g);
        // The beam's end at x = 4 lies on the column's side: gone when they merge, its own outline kept when not.
        Assert.Equal(merged, r.DropLines.Contains(3));
        Assert.Equal(merged, r.Seams.Count > 0);
        Assert.Equal(joins, g.Joins);
        Assert.Equal(joins, MaterialDrawing.SeamKey(g, IfcClass.Beam) != null);
    }

    [Fact]
    public void A_steel_beam_meeting_a_concrete_column_keeps_its_own_outline()
    {
        var d = BeamMeetingColumn("steel", "concrete");
        var steel = Graphics("steel", "Steel", false);
        var concrete = Graphics("concrete", "Reinforced concrete", true);
        var r = MaterialDrawing.Apply(d, p => p.Material == "steel" ? steel : concrete);
        Assert.DoesNotContain(3, r.DropLines);
        Assert.Empty(r.Seams);
    }
}
