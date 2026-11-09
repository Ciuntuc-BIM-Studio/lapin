using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Steel, stage 1 (decision 268): the widened catalogue, members with configurable ends, Convert to member from a sketch, IFC with boolean cuts.</summary>
public class SteelTests
{
    /// <summary>With Studio, which owns IfcMember for its solids: the steel module's members are its own class, written as IfcMember.</summary>
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params double[] v) => new([.. v.Select(x => (JsonNode)x)]);

    static string Type(Fixture f, string profile) => (string)f.Run("steel.createType", new JsonObject { ["section"] = profile })!["id"]!;

    /// <summary>A member between two points relative to the storey's level, centred on its axis.</summary>
    static string Member(Fixture f, string type, double x1, double y1, double z1, double x2, double y2, double z2) =>
        (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = x1, ["y1"] = y1, ["z1"] = z1, ["x2"] = x2, ["y2"] = y2, ["z2"] = z2 })!["id"]!;

    static Solid Body(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Single().Solid;
    static double Volume(Fixture f, string id) => Measure.Volume(Body(f, id));
    static IEnumerable<Vec3> Points(Fixture f, string id) => Body(f, id).Vertices;

    /// <summary>A closed mesh's volume by its triangles (the divergence theorem), whether or not its edges are welded.</summary>
    static double MeshVolume(MeshBuilder mesh) => mesh.T.Sum(t => Vec3.Dot(mesh.V[t.A], Vec3.Cross(mesh.V[t.B], mesh.V[t.C]))) / 6;

    static void Ends(Fixture f, string id, JsonObject? start = null, JsonObject? end = null)
    {
        var a = new JsonObject { ["ids"] = J() };
        a["ids"]!.AsArray().Add((JsonNode)id);
        if (start != null) a["start"] = start;
        if (end != null) a["end"] = end;
        f.Run("steel.setEnds", a);
    }

    // ------------------------------------------------------------ the catalogue

    [Fact]
    public void Hollow_and_cold_formed_sections_have_their_areas_and_outlines()
    {
        var rhs = Section.Catalogue["RHS 150×100×6"];
        Assert.Equal(0.1 * 0.15 - 0.088 * 0.138, rhs.SharpArea, 9);
        Assert.Single(rhs.Holes());
        Assert.True(Polygon2.SignedArea(rhs.Holes()[0]) < 0);
        Assert.Equal("RHS 150×100×6", rhs.Label);

        var chs = Section.Catalogue["CHS 114.3×5"];
        var exact = Math.PI / 4 * (0.1143 * 0.1143 - 0.1043 * 0.1043);
        Assert.Equal(exact, chs.Area, 12);

        // A cold-formed section's sheet: its area its middle line's length times its thickness.
        foreach (var name in new[] { "C 200×65×2", "Z 200×65×2", "Σ 200×62×2", "C 150×50×2" })
        {
            var s = Section.Catalogue[name];
            var line = s.Centreline();
            var length = line.Zip(line.Skip(1)).Sum(p => (p.Second - p.First).Length);
            Assert.Equal(length * s.Web, s.Area, 9);
            Assert.True(Polygon2.SignedArea(s.Outline()) > 0, name);
            // Within its box.
            Assert.All(s.Outline(), p => Assert.True(Math.Abs(p.X) <= s.Width / 2 + 1e-9 && Math.Abs(p.Y) <= s.Depth / 2 + 1e-9, name));
        }
        var c = Section.Catalogue["C 200×65×2"];
        Assert.Equal(0.002 * (2 * (0.02 - 0.001) + 2 * (0.065 - 0.002) + (0.2 - 0.002)), c.Area, 9);

        // Made by its dimensions, checked.
        var z = Section.Parse(new JsonObject { ["kind"] = "Z", ["depth"] = 0.25, ["width"] = 0.15, ["thickness"] = 0.0025, ["lip"] = 0.025 });
        Assert.Equal("Z 250×150×2.5", z.Label);
        Assert.Equal(z.Web, z.Flange);
        Assert.Throws<CommandException>(() => Section.Parse(new JsonObject { ["kind"] = "RHS", ["width"] = 0.1, ["depth"] = 0.1, ["thickness"] = 0.06 }));
        Assert.Throws<CommandException>(() => Section.Parse(new JsonObject { ["kind"] = "C", ["width"] = 0.06, ["depth"] = 0.2, ["thickness"] = 0.002, ["lip"] = 0.12 }));
    }

    [Fact]
    public void A_hollow_beam_of_the_structure_is_open_inside()
    {
        var f = F();
        var type = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = "RHS 200×100×8" })!["id"]!;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 0, ["x2"] = 4, ["y2"] = 0 })!["id"]!;
        Assert.Equal(Section.Catalogue["RHS 200×100×8"].SharpArea * 4, Volume(f, beam), 9);
    }

    // ------------------------------------------------------------ ends

    [Fact]
    public void A_member_is_its_section_along_its_axis_and_its_ends_extend_or_set_back()
    {
        var f = F();
        var ipe = Type(f, "IPE 300");
        var m = Member(f, ipe, 0, 0, 3, 6, 0, 3);
        var area = Section.Catalogue["IPE 300"].SharpArea;
        Assert.Equal(area * 6, Volume(f, m), 9);
        // Centred: the section's box about the axis.
        Assert.Equal(3 - 0.15, Points(f, m).Min(p => p.Z), 9);
        Assert.Equal(0.075, Points(f, m).Max(p => p.Y), 9);

        Ends(f, m, new JsonObject { ["extension"] = -0.01 }, new JsonObject { ["extension"] = 0.25 });
        Assert.Equal(area * 6.24, Volume(f, m), 9);
        Assert.Equal(0.01, Points(f, m).Min(p => p.X), 9);
        Assert.Equal(6.25, Points(f, m).Max(p => p.X), 9);
        var q = f.Session.Quantities(m).Single(s => s.Name == "Qto_MemberBaseQuantities").Quantities;
        Assert.Equal(6.24, q.Single(x => x.Name == "Length").Value, 9);
        Assert.Equal(Section.Catalogue["IPE 300"].Area * 6.24 * SteelModule.SteelDensity, q.Single(x => x.Name == "NetWeight").Value, 6);
    }

    [Fact]
    public void An_end_cut_by_a_plane_slants_in_plan_and_in_elevation()
    {
        var f = F();
        var ipe = Type(f, "IPE 300");
        var m = Member(f, ipe, 0, 0, 3, 6, 0, 3);
        var area = Section.Catalogue["IPE 300"].SharpArea;
        // 30° in plan: the flanges' tips run on, the section being symmetric about its up the volume stays.
        Ends(f, m, end: new JsonObject { ["cut"] = "plane", ["planAngle"] = 30 });
        Assert.Equal(area * 6, Volume(f, m), 9);
        Assert.Equal(6 + 0.075 * Math.Tan(Math.PI / 6), Points(f, m).Max(p => p.X), 9);
        Assert.Equal(6 - 0.075 * Math.Tan(Math.PI / 6), Points(f, m).Where(p => p.X > 3).Min(p => p.X), 9);
        // 45° in elevation: the cut leans back, the top 0.15 short of the axis's point, the bottom 0.15 past it.
        Ends(f, m, end: new JsonObject { ["cut"] = "plane", ["elevAngle"] = 45 });
        Assert.Equal(6 - 0.15, Points(f, m).Where(p => p.Z > 3.1499).Max(p => p.X), 9);
        Assert.Equal(6 + 0.15, Points(f, m).Where(p => p.Z < 2.8501).Max(p => p.X), 9);
        Assert.Throws<CommandException>(() => Ends(f, m, end: new JsonObject { ["cut"] = "plane", ["planAngle"] = 85 }));
        Assert.Throws<CommandException>(() => Ends(f, m, end: new JsonObject { ["cut"] = "diagonal" }));
    }

    [Fact]
    public void Mitred_members_meet_on_the_plane_halving_their_corner_and_follow_each_other()
    {
        var f = F();
        var ipe = Type(f, "IPE 300");
        var a = Member(f, ipe, 5, 0, 3, 0, 0, 3);
        var b = Member(f, ipe, 0, 0, 3, 0, 5, 3);
        f.Run("steel.mitre", new JsonObject { ["ids"] = J().Also(x => { x.Add((JsonNode)a); x.Add((JsonNode)b); }) });
        // A keeps y ≤ x; B keeps x ≤ y: they touch along the diagonal and do not overlap.
        Assert.All(Points(f, a), p => Assert.True(p.Y <= p.X + 1e-9));
        Assert.All(Points(f, b), p => Assert.True(p.X <= p.Y + 1e-9));
        Assert.Equal(Volume(f, a), Volume(f, b), 9);
        Assert.Equal(-0.075, Points(f, a).Min(p => p.X), 9);
        Assert.Equal("mitre", SteelEnd.Of(f.Model.Get(a)!).End.Cut);
        Assert.Equal(b, SteelEnd.Of(f.Model.Get(a)!).End.Ref);

        // B moves one metre over: A's mitre follows it there.
        f.Run("steel.updateMember", new JsonObject { ["id"] = b, ["x1"] = 1, ["x2"] = 1 });
        Assert.Equal(1 - 0.075, Points(f, a).Min(p => p.X), 9);
        Assert.All(Points(f, a), p => Assert.True(p.Y <= p.X - 1 + 1e-9));

        // B gone: A's end is square again where it was, and says why.
        f.Run("element.delete", new JsonObject { ["ids"] = J().Also(x => x.Add((JsonNode)b)) });
        Assert.Equal(0, Points(f, a).Min(p => p.X), 9);
        Assert.Contains(f.Session.Geometry(a)!.Diagnostics!, d => d.Contains("gone"));
    }

    [Fact]
    public void A_fitted_end_stops_on_the_face_it_comes_to_and_follows_it()
    {
        var f = F();
        var ipe = Type(f, "IPE 300");
        var hea = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = "HEA 200" })!["id"]!;
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = hea, ["x"] = 0, ["y"] = 0 })!["id"]!;
        var m = Member(f, ipe, 5, 0, 2, 0, 0, 2);
        // On the column's flange, 10 mm off it.
        Ends(f, m, end: new JsonObject { ["cut"] = "fit", ["ref"] = column, ["extension"] = -0.01 });
        Assert.Equal(0.1 + 0.01, Points(f, m).Min(p => p.X), 9);
        Assert.Equal(Section.Catalogue["IPE 300"].SharpArea * (5 - 0.11), Volume(f, m), 9);
        f.Run("structure.updateColumn", new JsonObject { ["id"] = column, ["x"] = -1 });
        Assert.Equal(-1 + 0.11, Points(f, m).Min(p => p.X), 9);
        // Turned a quarter, the column shows the member its web's side: half its depth.
        f.Run("structure.updateColumn", new JsonObject { ["id"] = column, ["rotation"] = 90 });
        Assert.Equal(-1 + 0.095 + 0.01, Points(f, m).Min(p => p.X), 9);

        // A beam of the structure takes ends too: fitted on the same column, its body is built by the steel module.
        var beamType = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = "IPE 240" })!["id"]!;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = beamType, ["x1"] = -1, ["y1"] = 4, ["x2"] = -1, ["y2"] = 0.5 })!["id"]!;
        Ends(f, beam, end: new JsonObject { ["cut"] = "fit", ["ref"] = column });
        Assert.Equal(0.1, Points(f, beam).Min(p => p.Y), 9);
        // A concrete beam does not.
        var rcType = (string)f.Run("structure.createBeamType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.25, ["depth"] = 0.4 } })!["id"]!;
        var rc = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = rcType, ["x1"] = 3, ["y1"] = 4, ["x2"] = 3, ["y2"] = 0 })!["id"]!;
        Assert.Throws<CommandException>(() => Ends(f, rc, end: new JsonObject { ["cut"] = "fit", ["ref"] = column }));
    }

    [Fact]
    public void A_cope_notches_the_flange_at_the_end()
    {
        var f = F();
        var ipe = Type(f, "IPE 300");
        var m = Member(f, ipe, 0, 0, 3, 6, 0, 3);
        var whole = Volume(f, m);
        Ends(f, m, end: new JsonObject { ["copeLength"] = 0.12, ["copeDepth"] = 0.04, ["copeTop"] = true });
        var s = Section.Catalogue["IPE 300"];
        // The top flange and the web under it, down 40 mm, over 120 mm.
        var taken = 0.12 * (s.Width * s.Flange + s.Web * (0.04 - s.Flange));
        Assert.Equal(whole - taken, Volume(f, m), 7);
        Assert.Equal(6 - 0.12, Points(f, m).Where(p => p.Z > 3.149).Max(p => p.X), 7);
        Assert.Equal(6, Points(f, m).Max(p => p.X), 9);
    }

    // ------------------------------------------------------------ sketches

    static (Band? Band, string? Problem) Read(params double[] xy) => SketchBands.Read(Enumerable.Range(0, xy.Length / 2).Select(i => new Vec2(xy[2 * i], xy[2 * i + 1])).ToList());

    [Fact]
    public void A_band_is_read_from_two_long_parallel_edges_and_its_ends()
    {
        // A rectangle 6 × 0.15.
        var (band, _) = Read(0, 0, 6, 0, 6, 0.15, 0, 0.15);
        Assert.NotNull(band);
        Assert.Equal(0.15, band!.Width, 9);
        Assert.Equal(new Vec2(0, 0.075), band.Start.At);
        Assert.Equal(new Vec2(6, 0.075), band.End.At);
        Assert.Equal(new Vec2(1, 0), band.End.Normal);

        // The far end slanted: its edge from (6, 0) to (6.1, 0.15).
        var (slanted, _) = Read(0, 0, 6, 0, 6.1, 0.15, 0, 0.15);
        Assert.Equal(6.05, slanted!.End.At.X, 9);
        Assert.Equal(-Math.Atan2(0.1, 0.15), Math.Atan2(slanted.End.Normal.Y, slanted.End.Normal.X), 9);

        // Drawn the other way round, turned: the same band.
        var (turned, _) = Read(0, 0, 0, 0.15, -6, 0.15, -6, 0).Band is { } t ? (t, (string?)null) : (null, null);
        Assert.Equal(0.15, turned!.Width, 9);
        Assert.Equal(6, turned.Length, 9);

        // Parallel within half a degree: a quarter of one is fine, a degree is not.
        var tilt = 6 * Math.Tan(0.25 * Math.PI / 180);
        Assert.NotNull(Read(0, 0, 6, 0, 6, 0.15 + tilt, 0, 0.15).Band);
        tilt = 6 * Math.Tan(Math.PI / 180);
        Assert.Contains("parallel", Read(0, 0, 6, 0, 6, 0.15 + tilt, 0, 0.15).Problem);

        // Not bands: a triangle, a square, an L.
        Assert.NotNull(Read(0, 0, 6, 0, 3, 1).Problem);
        Assert.NotNull(Read(0, 0, 1, 0, 1, 1, 0, 1).Problem);
        Assert.NotNull(Read(0, 0, 6, 0, 6, 0.15, 0.15, 0.15, 0.15, 3, 0, 3).Problem);
    }

    [Fact]
    public void A_notch_at_an_end_is_a_cope()
    {
        // A band 0.3 wide, its far end's top notched 0.12 back and 0.04 down.
        var (band, problem) = Read(0, 0, 6, 0, 6, 0.26, 5.88, 0.26, 5.88, 0.3, 0, 0.3);
        Assert.Null(problem);
        Assert.Equal(0.3, band!.Width, 9);
        var cope = Assert.Single(band.End.Copes);
        Assert.True(cope.Plus);
        Assert.Equal(0.12, cope.Length, 9);
        Assert.Equal(0.04, cope.Depth, 9);
        Assert.Empty(band.Start.Copes);
        // A notch at the bottom of the near end, drawn the outline's way after the cut.
        var (other, _) = Read(0.1, 0, 6, 0, 6, 0.3, 0, 0.3, 0, 0.05, 0.1, 0.05);
        var low = Assert.Single(other!.Start.Copes);
        Assert.False(low.Plus);
        Assert.Equal(0.1, low.Length, 9);
        Assert.Equal(0.05, low.Depth, 9);
        // A slanted step is not a cope.
        Assert.NotNull(Read(0, 0, 6, 0, 6, 0.26, 5.85, 0.3, 0, 0.3).Problem);
    }

    static string Draft(Fixture f, string view, params double[] xy)
    {
        var pts = new JsonArray([.. Enumerable.Range(0, xy.Length / 2).Select(i => (JsonNode)J(xy[2 * i], xy[2 * i + 1]))]);
        var shape = new JsonObject { ["type"] = "polyline", ["pts"] = pts, ["closed"] = true };
        return (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(shape) })!["ids"]![0]!;
    }

    [Fact]
    public void An_outline_drawn_on_a_plan_converts_to_a_member_with_its_profile_and_its_cuts()
    {
        var f = F();
        var plan = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        // 150 wide, as an IPE 300's flanges; the far end slanted 30° in plan.
        var slant = 0.15 * Math.Tan(Math.PI / 6);
        var sketch = Draft(f, plan, 0, 0, 6, 0, 6 + slant, 0.15, 0, 0.15);
        var notBand = Draft(f, plan, 10, 0, 12, 0, 11, 1);
        var r = (JsonObject)f.Run("steel.convertSketch", new JsonObject { ["ids"] = J().Also(x => { x.Add((JsonNode)sketch); x.Add((JsonNode)notBand); }) })!;
        var id = (string)r["ids"]![0]!;
        Assert.Single(r["skipped"]!.AsArray());
        var member = f.Model.Get(id)!;
        Assert.Equal("Member IPE 300", f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Name);
        // The sketch is gone, the other kept; the member lies on the storey's top, its section's top there.
        Assert.Null(f.Model.Get(sketch));
        Assert.NotNull(f.Model.Get(notBand));
        var top = Spatial.Storeys(f.Model).First().Num("height", 3);
        Assert.Equal(top, Points(f, id).Max(p => p.Z), 9);
        var (start, end) = SteelEnd.Of(member);
        Assert.Equal("square", start.Cut);
        Assert.Equal("plane", end.Cut);
        Assert.Equal(30, Math.Abs(end.PlanAngle), 6);
        Assert.Equal(0, end.ElevAngle, 6);
        // The body's shadow is the outline.
        Assert.Equal(6 + slant, Points(f, id).Max(p => p.X), 9);
        Assert.Equal(0.15, Points(f, id).Max(p => p.Y) - Points(f, id).Min(p => p.Y), 9);
        // One step back: the sketch again, no member.
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(sketch));
        Assert.Null(f.Model.Get(id));

        // Kept on request.
        f.Run("steel.convertSketch", new JsonObject { ["ids"] = J().Also(x => x.Add((JsonNode)sketch)), ["keep"] = true });
        Assert.NotNull(f.Model.Get(sketch));
        // On the storey's own plan (its view named plan:<storey>, not an entity), as the interface draws.
        var own = Draft(f, $"plan:{f.Storey}", 0, 4, 6, 4, 6, 4.15, 0, 4.15);
        f.Run("steel.convertSketch", new JsonObject { ["ids"] = J().Also(x => x.Add((JsonNode)own)) });
        Assert.Null(f.Model.Get(own));
        // A notched outline on a plan is not a cope.
        var notched = Draft(f, plan, 0, 2, 6, 2, 6, 2.11, 5.9, 2.11, 5.9, 2.15, 0, 2.15);
        Assert.Throws<CommandException>(() => f.Run("steel.convertSketch", new JsonObject { ["ids"] = J().Also(x => x.Add((JsonNode)notched)) }));
    }

    [Fact]
    public void An_outline_drawn_on_a_section_converts_with_cuts_in_elevation_and_copes()
    {
        var f = F();
        var section = (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = J(-1, -2), ["end"] = J(9, -2) })!["id"]!;
        var plane = Views.PlaneOf(f.Model, f.Session.Catalog, f.Model.Get(section)!)!;
        // 300 deep (an IPE 300, the lightest that deep), at 2.5 m; its far end slanted 45° and its top coped 120 × 40 at the near end.
        var sketch = Draft(f, section, 0, 2.5, 6, 2.5, 6.3, 2.8, 0.12, 2.8, 0.12, 2.76, 0, 2.76);
        var id = (string)f.Run("steel.convertSketch", new JsonObject { ["ids"] = J().Also(x => x.Add((JsonNode)sketch)) })!["ids"]![0]!;
        Assert.Equal("Member IPE 300", f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!.Name);
        var (start, end) = SteelEnd.Of(f.Model.Get(id)!);
        Assert.Equal(45, Math.Abs(end.ElevAngle), 6);
        Assert.Equal(0, end.PlanAngle, 6);
        Assert.True(start.CopeTop);
        Assert.False(start.CopeBottom);
        Assert.Equal(0.12, start.CopeLength, 9);
        Assert.Equal(0.04, start.CopeDepth, 9);
        // Where it is drawn: its top at 2.8, its bottom at 2.5, in the section's plane.
        Vec3 W(double x, double y) => plane.Origin + plane.Right * x + plane.Up * y;
        Assert.Equal(W(0, 2.8).Z, Points(f, id).Max(p => p.Z), 7);
        Assert.Equal(W(0, 2.5).Z, Points(f, id).Min(p => p.Z), 7);
        var along = plane.Right;
        var far = Points(f, id).Max(p => Vec3.Dot(p - plane.Origin, along));
        Assert.Equal(6.3, far, 7);
    }

    // ------------------------------------------------------------ IFC

    [Fact]
    public void A_member_exports_as_an_extrusion_clipped_by_its_ends_with_its_copes_as_openings()
    {
        var f = F();
        var ipe = Type(f, "IPE 300");
        var m = Member(f, ipe, 0, 0, 3, 6, 0, 3);
        Ends(f, m, new JsonObject { ["cut"] = "plane", ["planAngle"] = 20, ["elevAngle"] = 10 }, new JsonObject { ["copeLength"] = 0.12, ["copeDepth"] = 0.04, ["copeTop"] = true });
        var path = Path.Combine(Path.GetTempPath(), $"steel-{Guid.NewGuid():N}.ifc");
        try
        {
            IfcExport.Write(f.Session, path);
            var step = StepFile.Read(path);
            var member = Assert.Single(step.OfType("IFCMEMBER"));
            Assert.Single(step.OfType("IFCMEMBERTYPE"));
            Assert.NotEmpty(step.OfType("IFCEXTRUDEDAREASOLID"));
            Assert.Single(step.OfType("IFCBOOLEANCLIPPINGRESULT"));
            Assert.Single(step.OfType("IFCHALFSPACESOLID"));
            Assert.Single(step.OfType("IFCOPENINGELEMENT"));
            Assert.Contains(step.OfType("IFCSHAPEREPRESENTATION"), r => r[2] is string s && s == "Clipping");
            Assert.Single(step.OfType("IFCRELVOIDSELEMENT"));

            // Read back, cut by its half-spaces and voided by its opening: the body Lapin built.
            var model = new IfcModel(step);
            var booleans = f.Session.Catalog.Get<ISolidBooleans>()!;
            var product = model.Products.Single(p => p.Class.Equals("IfcMember", StringComparison.OrdinalIgnoreCase));
            var (body, openings) = model.Shape(product.Id, booleans);
            Assert.Equal(Volume(f, m), MeshVolume(body.Single().Mesh), 7);
            var cope = Assert.Single(openings);
            Assert.True(cope.Closed());
            // The cope's box over the member's top, at its end.
            Assert.Equal(3.15 - 0.04, cope.V.Min(p => p.Z), 6);
            Assert.Equal(6 - 0.12, cope.V.Min(p => p.X), 6);
        }
        finally { File.Delete(path); }
    }
}

static class JsonArrayAlso
{
    public static JsonArray Also(this JsonArray a, Action<JsonArray> f) { f(a); return a; }
}
