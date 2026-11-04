using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Strip footings of any profile (decision 297, a user's request: "I can't find any way to model strip footing profile
/// like Revit does"): the type's profile swept along the axis, its insertion point on the axis at the bottom; exact volumes
/// for stepped and sloped profiles, mitred L corners, a T cut by the footing it ends against, curved axes, IFC as the
/// profile extruded.
/// </summary>
public class FootingProfileTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-footings-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static Fixture Make() => new(new ArchitectureModule(), new StructureModule());

    static string Type(Fixture f, string catalog, JsonObject? values = null) =>
        (string)f.Run("structure.createStripType", new JsonObject { ["catalog"] = catalog, ["params"] = values ?? new JsonObject(), ["bottom"] = -1.0 })!["id"]!;

    static string Strip(Fixture f, string type, double x1, double y1, double x2, double y2, JsonObject? more = null)
    {
        var a = new JsonObject { ["storeyId"] = f.Storey, ["x1"] = x1, ["y1"] = y1, ["x2"] = x2, ["y2"] = y2, ["typeId"] = type };
        foreach (var (k, v) in more ?? []) a[k] = v?.DeepClone();
        return (string)f.Run("structure.addStripFooting", a)!["id"]!;
    }

    static double Volume(Fixture f, string id) => f.Session.Geometry(id)!.Parts.Sum(p => Measure.Volume(p.Solid));

    [Fact]
    public void A_stepped_footing_is_its_steps_along_its_length()
    {
        var f = Make();
        var t = Type(f, "stepped2");
        var id = Strip(f, t, 0, 0, 6, 0);
        // Bottom step 1.00 × 0.35, top step 0.60 × 0.35: 0.56 m² along 6 m.
        Assert.Equal(0.56 * 6, Volume(f, id), 9);
        var box = f.Bounds(id);
        Assert.Equal(-1.0, box.Min.Z, 9);
        Assert.Equal(-0.3, box.Max.Z, 9);
        Assert.Equal(-0.5, box.Min.Y, 9);
        Assert.Equal(0.5, box.Max.Y, 9);
        var q = f.Session.Quantities(id).First(s => s.Name == "Qto_FootingBaseQuantities").Quantities.ToDictionary(x => x.Name, x => x.Value);
        Assert.Equal(0.56, q["CrossSectionArea"], 9);
        Assert.Equal(1.0, q["Width"], 9);
        Assert.Equal(0.7, q["Height"], 9);
        Assert.Equal(0.56 * 6, q["NetVolume"], 9);
        // Three steps, and the sloped top: 1.00 wide, 0.25 upright at the edges, 0.50 high, 0.40 on top.
        Assert.Equal((1.2 * 0.3 + 0.85 * 0.3 + 0.5 * 0.3) * 4, Volume(f, Strip(f, Type(f, "stepped3"), 0, 5, 4, 5)), 9);
        Assert.Equal((1.0 * 0.25 + (1.0 + 0.4) / 2 * 0.25) * 5, Volume(f, Strip(f, Type(f, "trapezoid"), 0, 10, 5, 10)), 9);
        // The eccentric one: its stem on the axis, the footing out to its left.
        var ecc = Strip(f, Type(f, "eccentric"), 0, 15, 5, 15);
        Assert.Equal((0.8 * 0.4 + 0.3 * 1.0) * 5, Volume(f, ecc), 9);
        Assert.Equal(15.15, f.Bounds(ecc).Max.Y, 9);
        Assert.Equal(15 - 0.65, f.Bounds(ecc).Min.Y, 9);
    }

    [Fact]
    public void Its_faces_are_the_profiles_sides_along_the_axis()
    {
        var f = Make();
        var id = Strip(f, Type(f, "stepped2"), 0, 0, 6, 0);
        var faces = ElementFaces.Of(f.Session.Catalog, f.Model.Get(id)!, f.Model);
        // Four upright sides (two a step), the bottom, the bottom step's two ledges and the top.
        Assert.Equal(4, faces.Count(x => x.Role.StartsWith("Side[")));
        Assert.Single(faces, x => x.Role == "Bottom");
        Assert.Equal(3, faces.Count(x => x.Role.StartsWith("Top")));
        Assert.Equal(6 * 0.35 * 4, faces.Where(x => x.Role.StartsWith("Side[")).Sum(x => x.Size), 9);
    }

    [Fact]
    public void Two_footings_of_one_profile_are_mitred_round_an_L_corner()
    {
        var f = Make();
        var t = Type(f, "stepped2");
        var a = Strip(f, t, 0, 0, 6, 0, new JsonObject { ["joinStyle"] = "butt" });
        var b = Strip(f, t, 6, 0, 6, 5, new JsonObject { ["joinStyle"] = "butt" });
        // Mitred whatever the style: each holds its profile along its axis, the corner shared on the diagonal.
        Assert.Equal(0.56 * 6, Volume(f, a), 9);
        Assert.Equal(0.56 * 5, Volume(f, b), 9);
        Assert.Equal(6.5, f.Bounds(a).Max.X, 9);
        Assert.Equal(-0.5, f.Bounds(b).Min.Y, 9);
    }

    [Fact]
    public void A_footing_ending_against_another_meets_each_of_its_steps()
    {
        var f = Make();
        var t = Type(f, "stepped2");
        var through = Strip(f, t, 0, 0, 10, 0);
        var ending = Strip(f, t, 5, 0, 5, 5);
        // The through one is whole; the bottom step of the other starts at its bottom step's face (y 0.5), the top step at its top step's (0.3).
        Assert.Equal(0.56 * 10, Volume(f, through), 9);
        Assert.Equal(1.0 * 0.35 * 4.5 + 0.6 * 0.35 * 4.7, Volume(f, ending), 6);
        Assert.Equal(0.3, f.Bounds(ending).Min.Y, 6);
    }

    [Fact]
    public void A_curved_footing_sweeps_its_profile_along_the_arc()
    {
        var f = Make();
        var id = Strip(f, Type(f, "stepped2"), 0, 0, 8, 0, new JsonObject { ["arc"] = 2.0 });
        var c = PathCurve.Of(f.Model.Get(id)!);
        // Symmetric about the axis: the profile's area along the axis's length, within the chords' tolerance.
        Assert.InRange(Volume(f, id) / (0.56 * c.Length), 0.998, 1.001);
    }

    [Fact]
    public void A_type_turns_from_its_stem_to_a_profile_and_back()
    {
        var f = Make();
        var id = (string)f.Run("structure.addStripFooting", new JsonObject { ["storeyId"] = f.Storey, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0 })!["id"]!;
        var type = f.Model.RelatingOf(id, IfcClass.RelDefinesByType)!;
        var standard = Volume(f, id);
        f.Run("structure.updateType", new JsonObject { ["id"] = type.Id, ["catalog"] = "footingRect", ["params"] = new JsonObject { ["width"] = 0.8, ["height"] = 0.5 } });
        Assert.Equal(0.8 * 0.5 * 5, Volume(f, id), 9);
        Assert.NotNull(StripFootings.ProfileOf(f.Model, f.Model.Get(type.Id)!));
        f.Run("structure.updateType", new JsonObject { ["id"] = type.Id, ["profile"] = null });
        Assert.Equal(standard, Volume(f, id), 9);
        // A profile changed in the project: the footing follows.
        f.Run("structure.updateType", new JsonObject { ["id"] = type.Id, ["catalog"] = "footingRect", ["params"] = new JsonObject { ["width"] = 0.8, ["height"] = 0.5 } });
        var profile = Assert.Single(f.Model.OfClass(Profiles.Class));
        f.Run("profile.update", new JsonObject { ["id"] = profile.Id, ["params"] = new JsonObject { ["width"] = 1.0, ["height"] = 0.5 } });
        Assert.Equal(1.0 * 0.5 * 5, Volume(f, id), 9);
    }

    [Fact]
    public void A_profile_footing_goes_to_IFC_as_its_profile_extruded_and_comes_back()
    {
        var f = Make();
        var t = Type(f, "trapezoid");
        var a = Strip(f, t, 0, 0, 6, 0);
        var b = Strip(f, t, 6, 0, 6, 5);
        var path = Path.Combine(_dir, "footings.ifc");
        IfcExport.Write(f.Session, path);
        var step = StepFile.Read(path);
        Assert.Equal(2, step.OfType("IFCFOOTING").Count(x => x[8] is StepEnum { Value: "STRIP_FOOTING" }));
        Assert.Equal(2, step.OfType("IFCEXTRUDEDAREASOLID").Count());
        Assert.NotEmpty(step.OfType("IFCARBITRARYCLOSEDPROFILEDEF"));
        // The mitres as clipping planes.
        Assert.Equal(2, step.OfType("IFCBOOLEANCLIPPINGRESULT").Count());

        var g = Make();
        var r = IfcImport.Run(g.Session, path, null, out _);
        Assert.Equal(0, r.References);
        foreach (var id in new[] { a, b }) Assert.Equal(Volume(f, id), Volume(g, id), 9);
        Assert.NotNull(StripFootings.ProfileOf(g.Model, g.Model.RelatingOf(a, IfcClass.RelDefinesByType)!));
    }

    [Fact]
    public void A_profile_is_read_from_a_drafted_closed_outline()
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule());
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!;
        var pts = new JsonArray(new JsonArray(2.0, 1.0), new JsonArray(2.8, 1.0), new JsonArray(2.8, 1.4), new JsonArray(2.0, 1.4));
        var shape = (string)f.Run("drafting.draw", new JsonObject { ["viewId"] = view, ["shapes"] = new JsonArray(new JsonObject { ["type"] = "polyline", ["pts"] = pts, ["closed"] = true }) })!["ids"]![0]!;
        var id = (string)f.Run("profile.create", new JsonObject { ["annotationId"] = shape, ["use"] = "footing", ["name"] = "Drawn" })!["id"]!;
        var p = Profiles.Read(f.Model.Get(id)!)!;
        Assert.Equal(0.32, p.Area, 9);
        Assert.Equal((-0.4, 0.4, 0.0, 0.4), p.Bounds);
    }
}
