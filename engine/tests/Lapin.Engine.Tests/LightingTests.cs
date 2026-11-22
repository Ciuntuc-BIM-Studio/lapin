using System.Text.Json.Nodes;
using Lapin.Ifc;
using Lapin.Mep;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Lighting;

namespace Lapin.Engine.Tests;

/// <summary>
/// Artificial light (docs/plan-render.md, S2): the IES LM-63 reader and the generated profiles (BimGo's cases, MIT-0, with
/// sample files written here by hand), colour temperatures on Planck's locus, light data on fixtures, types and materials
/// (one undo step, checked), where the view's lights stand, and IFC (Lapin_Light, IfcLightSource…, read back by Lapin).
/// </summary>
public class LightingTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "lapin-light-" + Guid.NewGuid().ToString("N"));
    public LightingTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    // ─── Sample files (written here, CC0) ────────────────────────────────

    /// <summary>A 30° spot, bilateral (C 0–180), 0–90° gamma, a 1000 lm lamp.</summary>
    const string Spot = "IESNA:LM-63-2002\n[TEST] Lapin test file (hand-written, CC0)\n[MANUFAC] Lapin\n[LUMINAIRE] Test spot, 30 deg, bilateral 0-180\nTILT=NONE\n"
        + "1 1000 1 7 3 1 2 0.1 0.1 0\n1 1 10\n0 10 20 30 45 60 90\n0 90 180\n2000 1800 900 300 80 20 0\n2000 1600 700 200 60 15 0\n2000 1400 500 150 40 10 0\n";

    /// <summary>An even 100 cd source over the whole sphere (one C plane): 400π ≈ 1257 lm.</summary>
    const string Even = "IESNA91\n[TEST] even\nTILT=NONE\n1,-1,1,3,1,1,2,0,0,0\n1 1 5\n0 90 180\n0\n100 100 100\n";

    /// <summary>Quadrant symmetric (C 0–90): brighter along C 0 than C 90, multiplier 2.</summary>
    const string Quadrant = "TILT=NONE\n1 500 2 2 2 1 2 0 0 0\n1 1 5\n0 90\n0 90\n100 50\n40 20\n";

    /// <summary>Bilateral about the 90–270 plane.</summary>
    const string Plane90 = "TILT=NONE\n1 500 1 2 3 1 2 0 0 0\n1 1 5\n0 90\n90 180 270\n10 10\n50 50\n200 200\n";

    /// <summary>C 0–270 without 360: C 315 is half way between C 270 and C 0.</summary>
    const string Wrap = "TILT=NONE\n1 500 1 2 4 1 2 0 0 0\n1 1 5\n0 90\n0 90 180 270\n100 100\n0 0\n0 0\n300 300\n";

    /// <summary>TILT=INCLUDE: the tilt block read past (kept).</summary>
    const string Tilted = "IESNA:LM-63-1995\nTILT=INCLUDE\n1\n3\n0 45 90\n1 0.9 0.8\n1 1000 1 2 1 1 2 0 0 0\n1 1 10\n0 90\n0\n500 100\n";

    // ─── The reader ──────────────────────────────────────────────────────

    [Fact]
    public void Ies_reads_its_header_angles_and_keywords()
    {
        var d = IesData.Parse(Spot, out var error);
        Assert.NotNull(d);
        Assert.Null(error);
        Assert.Equal(1, d!.Lamps);
        Assert.Equal(1000, d.LumensPerLamp);
        Assert.Equal(1, d.PhotometricType);
        Assert.Equal(10, d.InputWatts);
        Assert.Equal(new double[] { 0, 10, 20, 30, 45, 60, 90 }, d.Vertical);
        Assert.Equal(new double[] { 0, 90, 180 }, d.Horizontal);
        Assert.Equal("Lapin", d.Keywords["MANUFAC"]);
        Assert.Equal(2000, d.Candela(0, 0), 3);
        Assert.Equal(1900, d.Candela(5, 0), 3);
    }

    [Fact]
    public void Ies_expands_its_symmetries()
    {
        var spot = IesData.Parse(Spot, out _)!;
        Assert.Equal(spot.Candela(20, 90), spot.Candela(20, 270), 3);
        Assert.Equal(spot.Candela(20, 45), spot.Candela(20, 315), 3);
        Assert.Equal(0, spot.Candela(120, 0));
        Assert.Equal(100, IesData.Parse(Even, out _)!.Candela(130, 77), 3);
        var q = IesData.Parse(Quadrant, out _)!;
        Assert.Equal(200, q.Candela(0, 0), 3);
        Assert.Equal(q.Candela(45, 30), q.Candela(45, 150), 3);
        Assert.Equal(q.Candela(45, 30), q.Candela(45, 330), 3);
        var p = IesData.Parse(Plane90, out _)!;
        Assert.Equal(50, p.Candela(30, 0), 3);
        Assert.Equal(p.Candela(30, 60), p.Candela(30, 120), 3);
        Assert.Equal(p.Candela(30, 300), p.Candela(30, 240), 3);
        Assert.Equal(200, IesData.Parse(Wrap, out _)!.Candela(30, 315), 3);
    }

    [Fact]
    public void Ies_reads_past_an_included_tilt_and_keeps_it()
    {
        var d = IesData.Parse(Tilted, out var error);
        Assert.True(d != null, error);
        Assert.Equal(500, d!.Candela(0, 0), 3);
        Assert.Equal(300, d.Candela(45, 200), 3);
        Assert.Equal(new double[] { 0, 45, 90 }, d.TiltAngles);
        Assert.Equal(new[] { 1, 0.9, 0.8 }, d.TiltFactors);
        Assert.True(IesData.Parse(Even.Replace("TILT=NONE", "TILT=lamp.tlt"), out _)!.TiltFileIgnored);
        Assert.Null(IesData.Parse("TILT=INCLUDE\n1\n3\n0 45\n", out _));
        Assert.Null(IesData.Parse(Tilted.Replace("0 45 90", "0 90 45"), out _)!.TiltAngles);
    }

    [Fact]
    public void Ies_refuses_what_is_damaged_or_not_type_C()
    {
        Assert.Null(IesData.Parse("", out var error));
        Assert.NotNull(error);
        Assert.Null(IesData.Parse("IESNA:LM-63-2002\n[TEST] x\n1 2 3\n", out _));
        Assert.Null(IesData.Parse(Even.Replace("1,-1,1,3,1,1,", "1,-1,1,3,1,2,"), out error));
        Assert.Contains("type C", error);
        Assert.Null(IesData.Parse(Spot[..^20], out _));
        Assert.Null(IesData.Parse(Spot.Replace("0 10 20 30 45 60 90", "0 20 10 30 45 60 90"), out _));
        Assert.Null(IesData.Parse(Spot.Replace("1 1 10\n", "1 1 x10\n"), out _));
    }

    [Fact]
    public void Ies_total_lumens_integrate_the_candela()
    {
        Assert.Equal(400 * Math.PI, IesData.Parse(Even, out _)!.TotalLumens(), 400 * Math.PI * 0.01);
        // The luminaire's output, not its lamp's 1000 lm.
        Assert.InRange(IesData.Parse(Spot, out _)!.TotalLumens(), 864, 904);
    }

    // ─── Profiles ────────────────────────────────────────────────────────

    [Fact]
    public void A_profile_keeps_its_output_and_a_narrow_beam_concentrates_it()
    {
        var even = LightProfile.FromFunction("t:even", "Even", (g, c) => 3);
        Assert.All(even.Table, v => Assert.Equal(1, v));
        Assert.Equal(1, even.Scale, 2);
        var fromIes = LightProfile.FromIes(LightProfiles.KeyOf(Even), "even.ies", Even, out _)!;
        Assert.Equal(1, fromIes.Scale, 2);
        Assert.Equal(400 * Math.PI, fromIes.Lumens, 15.0);
        Assert.True(fromIes.IsFile);

        var spot = LightProfile.FromIes(LightProfiles.KeyOf(Spot), "spot.ies", Spot, out _)!;
        Assert.Equal(1, spot.Sample(0, 0), 6);
        Assert.Equal(0, spot.Sample(150, 0), 6);
        Assert.InRange(spot.Scale, 15, 40);
        Assert.InRange(spot.BeamAngle, 25, 45);
        Assert.InRange(spot.FieldAngle, 90, 130);
        Assert.Equal(4 * Math.PI, LightProfile.Integrate(spot.Table) * spot.Scale, 2);
    }

    [Fact]
    public void The_five_built_ins_are_distinct_and_shaped()
    {
        var all = LightProfiles.BuiltIns;
        Assert.Equal(5, all.Count);
        Assert.Equal(5, all.Select(p => p.Key).Distinct().Count());
        Assert.All(all, p => Assert.False(p.IsFile));
        var narrow = LightProfiles.BuiltIn(LightProfiles.DownlightNarrow)!;
        var medium = LightProfiles.BuiltIn(LightProfiles.DownlightMedium)!;
        var flood = LightProfiles.BuiltIn(LightProfiles.Flood)!;
        Assert.True(narrow.Scale > medium.Scale && medium.Scale > flood.Scale && flood.Scale > 1);
        Assert.Equal(25, narrow.BeamAngle, 4.0);
        Assert.Equal(40, medium.BeamAngle, 5.0);
        var washer = LightProfiles.BuiltIn(LightProfiles.WallWasher)!;
        Assert.True(washer.Sample(35, 0) > 3 * washer.Sample(35, 180));
        var pendant = LightProfiles.BuiltIn(LightProfiles.Pendant)!;
        Assert.True(pendant.Sample(180, 0) > 0.3 && pendant.Sample(0, 0) > pendant.Sample(180, 0));
        Assert.Null(LightProfiles.BuiltIn("builtin:nope"));
    }

    [Fact]
    public void Generated_spots_and_the_half_sphere()
    {
        Assert.Equal("gen:spot:30:60", LightProfiles.SpotKey(30.2, 59.6));
        Assert.Equal("gen:spot:60:60", LightProfiles.SpotKey(80, 60));
        var spot = LightProfiles.Generated("gen:spot:30:60")!;
        Assert.Equal(1, spot.Sample(10, 0), 6);
        Assert.Equal(0, spot.Sample(40, 90), 6);
        var hemi = LightProfiles.Generated(LightProfiles.Hemispherical)!;
        Assert.Equal(Math.Cos(60 * Math.PI / 180), hemi.Sample(60, 0), 2);
        Assert.Equal(0, hemi.Sample(120, 0), 6);
        Assert.Null(LightProfiles.Generated("gen:spot:90:30"));
    }

    // ─── Colour temperature ──────────────────────────────────────────────

    [Fact]
    public void Colour_temperatures_lie_on_Plancks_locus()
    {
        // CIE illuminant A (a tungsten lamp, 2856 K) and the locus at 6500 K.
        var (xa, ya) = Kelvin.Chromaticity(2856);
        Assert.Equal(0.4476, xa, 0.001);
        Assert.Equal(0.4074, ya, 0.001);
        var (x65, y65) = Kelvin.Chromaticity(6500);
        Assert.Equal(0.3135, x65, 0.001);
        Assert.Equal(0.3236, y65, 0.001);
        var warm = Kelvin.Rgb(2700);
        var cold = Kelvin.Rgb(10000);
        Assert.Equal(1, warm.R, 6);
        Assert.True(warm.B < 0.5);
        Assert.Equal(1, cold.B, 6);
        Assert.True(cold.R < 0.85);
        Assert.Equal(Kelvin.Rgb(12000), Kelvin.Rgb(30000));
    }

    // ─── Data ────────────────────────────────────────────────────────────

    static (MepFixture F, string Light) Fixture()
    {
        var f = new MepFixture();
        var system = (string)((JsonObject)f.Run("mep.placeSystem", new JsonObject { ["service"] = Services.Electrical, ["storeyId"] = f.Storey, ["x"] = 0, ["y"] = 0 })!)["id"]!;
        var light = (string)((JsonObject)f.Run("mep.placeNode", new JsonObject { ["systemId"] = system, ["role"] = "light", ["storeyId"] = f.Storey, ["x"] = 2, ["y"] = 3 })!)["id"]!;
        return (f, light);
    }

    static JsonArray Sources(MepFixture f) => (JsonArray)f.Session.Query("light.sources", new JsonObject())!;

    [Fact]
    public void A_light_placed_lights_with_the_defaults_under_its_body()
    {
        var (f, light) = Fixture();
        var spec = Lights.Of(f.Model, f.Model.Get(light)!)!;
        Assert.Equal(1000, spec.Lumens);
        Assert.Equal(3500, spec.Kelvin);
        Assert.Equal("point", spec.Kind);
        var s = (JsonObject)Sources(f).Single(x => (string?)x!["id"] == light)!;
        var at = (JsonArray)s["position"]!;
        Assert.Equal(2, (double)at[0]!, 3);
        Assert.Equal(3, (double)at[1]!, 3);
        // The role hangs at 2.40 m: the light a hair under the fixture's bottom.
        Assert.Equal(2.39, (double)at[2]!, 2);
        Assert.Equal(-1, (double)((JsonArray)s["direction"]!)[2]!);
    }

    [Fact]
    public void Setting_a_light_is_one_step_checked_and_an_element_without_data_gives_none()
    {
        var (f, light) = Fixture();
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["kind"] = "spot", ["lumens"] = 650, ["kelvin"] = 2700, ["beam"] = 36, ["field"] = 50 });
        var spec = Lights.Of(f.Model, f.Model.Get(light)!)!;
        Assert.Equal(("spot", 650.0, 2700.0, 36.0, 50.0), (spec.Kind, spec.Lumens, spec.Kelvin, spec.Beam, spec.Field));
        f.Session.Undo();
        Assert.Equal("point", Lights.Of(f.Model, f.Model.Get(light)!)!.Kind);
        f.Session.Redo();

        Assert.Throws<CommandException>(() => f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["kelvin"] = 500 }));
        Assert.Throws<CommandException>(() => f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["kind"] = "laser" }));
        Assert.Throws<CommandException>(() => f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["beam"] = 90, ["field"] = 40 }));
        Assert.Throws<CommandException>(() => f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["profile"] = "ies:0000000000000000" }));
        Assert.Throws<CommandException>(() => f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(f.Storey), ["lumens"] = 10 }));

        // Off: still a light, drawn dark.
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["on"] = false });
        Assert.False((bool)Sources(f).Single()!["on"]!);
        // Cleared: no light at all, as an element placed before lights had data.
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["clear"] = true });
        Assert.Null(f.Model.Get(light)!.Attrs[Lights.Key]);
        Assert.Empty(Sources(f));
    }

    [Fact]
    public void An_element_takes_its_types_light_under_its_own_and_a_glowing_material_lights_what_is_made_of_it()
    {
        var f = new Fixture();
        var typeId = f.Type("Lit wall", ("Glow panel", 0.1, true));
        var wall = f.Wall(0, 0, 2, 0, typeId);
        Assert.Empty((JsonArray)f.Session.Query("light.sources", new JsonObject())!);
        // On the material: the wall glows.
        var mat = Materials.Find(f.Model, "Glow panel")!.Id;
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(mat), ["lumens"] = 3000, ["kelvin"] = 5000, ["kind"] = "area", ["at"] = "centre" });
        var lit = (JsonObject)((JsonArray)f.Session.Query("light.sources", new JsonObject())!).Single()!;
        Assert.Equal(wall, (string?)lit["id"]);
        Assert.Equal(mat, (string?)lit["material"]);
        Assert.Equal(3000, (double)lit["lumens"]!);
        // On the type: its data, the element's own over it.
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(mat), ["clear"] = true });
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(typeId), ["lumens"] = 800, ["kelvin"] = 3000 });
        Assert.Equal(800, Lights.Of(f.Model, f.Model.Get(wall)!)!.Lumens);
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(wall), ["kelvin"] = 4000 });
        var both = Lights.Of(f.Model, f.Model.Get(wall)!)!;
        Assert.Equal((800.0, 4000.0), (both.Lumens, both.Kelvin));
    }

    [Fact]
    public void An_ies_file_comes_into_the_project_once_and_a_light_takes_it()
    {
        var (f, light) = Fixture();
        var r = (JsonObject)f.Run("light.addProfile", new JsonObject { ["name"] = "spot.ies", ["text"] = Spot })!;
        var key = (string)r["key"]!;
        Assert.Equal(LightProfiles.KeyOf(Spot), key);
        Assert.InRange((double)r["lumens"]!, 864, 904);
        var again = (JsonObject)f.Run("light.addProfile", new JsonObject { ["name"] = "copy.ies", ["text"] = Spot.Replace("\n", "\r\n") })!;
        Assert.Equal((string)r["id"]!, (string)again["id"]!);
        Assert.Single(f.Model.OfClass(Lights.ProfileClass));
        Assert.Throws<CommandException>(() => f.Run("light.addProfile", new JsonObject { ["text"] = "not a photometric file" }));
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["kind"] = "spot", ["profile"] = key });
        var table = (JsonObject)f.Session.Query("light.profile", new JsonObject { ["key"] = key })!;
        Assert.Equal(LightProfile.TableSize, ((JsonArray)table["table"]!).Count);
        var names = ((JsonArray)f.Session.Query("light.profiles", new JsonObject())!).Select(p => (string)p!["key"]!).ToList();
        Assert.Equal(6, names.Count);
        Assert.Contains(key, names);
    }

    // ─── IFC ─────────────────────────────────────────────────────────────

    [Fact]
    public void Lights_go_to_IFC_as_Lapin_Light_and_light_sources_and_come_back()
    {
        var (f, light) = Fixture();
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(light), ["kind"] = "spot", ["lumens"] = 650, ["kelvin"] = 2700, ["beam"] = 36, ["field"] = 50 });
        var (_, second) = (f, (string)((JsonObject)f.Run("mep.placeNode", new JsonObject { ["systemId"] = Systems.All(f.Model).First().Id, ["role"] = "light", ["storeyId"] = f.Storey, ["x"] = 5, ["y"] = 3 })!)["id"]!);
        f.Run("light.set", new JsonObject { ["ids"] = new JsonArray(second), ["profile"] = LightProfiles.DownlightMedium, ["kelvin"] = 4000 });
        var ifc = Path.Combine(_dir, "lights.ifc");
        IfcExport.Write(f.Session, ifc, null);
        var text = File.ReadAllText(ifc);
        Assert.Contains("'Lapin_Light'", text);
        Assert.Contains("IFCLUMINOUSFLUXMEASURE(650.)", text);
        Assert.Contains("IFCTHERMODYNAMICTEMPERATUREMEASURE(2700.)", text);
        Assert.Contains("IFCLIGHTSOURCESPOT(", text);
        Assert.Contains("IFCLIGHTSOURCEGONIOMETRIC(", text);
        Assert.Contains("IFCLIGHTINTENSITYDISTRIBUTION(.TYPE_C.", text);
        Assert.Contains("'Lighting','LightSource'", text);
        Assert.DoesNotContain("'Pset_Light", text.Replace("'Pset_LightFixture", ""));

        var b = new MepFixture();
        IfcImport.Run(b.Session, ifc, null, out _);
        var back = b.Model.OfClass("IfcLightFixture").ToDictionary(e => e.Name);
        var one = Lights.Of(b.Model, back[f.Model.Get(light)!.Name])!;
        Assert.Equal(("spot", 650.0, 2700.0, 36.0, 50.0), (one.Kind, one.Lumens, one.Kelvin, one.Beam, one.Field));
        var two = Lights.Of(b.Model, back[f.Model.Get(second)!.Name])!;
        Assert.Equal((LightProfiles.DownlightMedium, 4000.0), (two.Profile, two.Kelvin));
        // Lapin's own set is not taken for the user's parameters.
        Assert.All(back.Values, e => Assert.DoesNotContain("Lapin_Light", (e.Attrs["params"] as JsonArray ?? []).Select(p => (string?)p?["pset"])));
        Assert.Equal(2, ((JsonArray)b.Session.Query("light.sources", new JsonObject())!).Count);
    }
}
