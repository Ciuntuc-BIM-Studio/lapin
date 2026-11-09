using System.Text.Json.Nodes;
using Lapin.Drafting;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Steel;
using Lapin.Structure;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>Quantities follow the profile's real area (decision 418): an IPE 300 is 53.81 cm², not the sharp body's 51.88.</summary>
public class RealAreaQuantityTests
{
    static Fixture F() => new(new StructureModule(), new SteelModule(), new DraftingModule(), new StudioModule());

    static double Q(Fixture f, string id, string set, string name) =>
        f.Session.Quantities(id).First(s => s.Name == set).Quantities.First(q => q.Name == name).Value;

    // Published cross-section areas (cm²): EN 10365 (IPE, HEA, UPN), EN 10056-1 (angles), EN 10210-2 (RHS).
    [Theory]
    [InlineData("IPE 200", 28.48)]
    [InlineData("IPE 300", 53.81)]
    [InlineData("IPE 400", 84.46)]
    [InlineData("HEA 200", 53.83)]
    [InlineData("HEA 300", 112.5)]
    [InlineData("HEB 300", 149.1)]
    [InlineData("UPN 200", 32.2)]
    [InlineData("UPN 300", 58.8)]
    [InlineData("L 100×100×10", 19.2)]
    [InlineData("L 50×50×5", 4.80)]
    [InlineData("RHS 150×100×6", 28.2)]
    public void A_beam_and_a_column_measure_the_published_area_and_weight(string name, double cm2)
    {
        var f = F();
        var ct = (string)f.Run("structure.createColumnType", new JsonObject { ["name"] = name, ["section"] = name, ["material"] = "Steel" })!["id"]!;
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = ct, ["x"] = 1, ["y"] = 1 })!["id"]!;
        var bt = (string)f.Run("structure.createBeamType", new JsonObject { ["name"] = name, ["section"] = name, ["material"] = "Steel" })!["id"]!;
        var beam = (string)f.Run("structure.addBeam", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = bt, ["x1"] = 0, ["y1"] = 5, ["x2"] = 6, ["y2"] = 5 })!["id"]!;
        var a = cm2 * 1e-4;
        var height = Q(f, column, "Qto_ColumnBaseQuantities", "Length");
        Assert.InRange(Q(f, column, "Qto_ColumnBaseQuantities", "CrossSectionArea"), a * 0.995, a * 1.005);
        Assert.Equal(Q(f, column, "Qto_ColumnBaseQuantities", "CrossSectionArea") * height, Q(f, column, "Qto_ColumnBaseQuantities", "NetVolume"), 6);
        Assert.Equal(Q(f, column, "Qto_ColumnBaseQuantities", "GrossVolume"), Q(f, column, "Qto_ColumnBaseQuantities", "NetVolume"), 6);
        Assert.InRange(Q(f, beam, "Qto_BeamBaseQuantities", "NetVolume"), a * 6 * 0.995, a * 6 * 1.005);
        Assert.Equal(Q(f, beam, "Qto_BeamBaseQuantities", "GrossVolume"), Q(f, beam, "Qto_BeamBaseQuantities", "NetVolume"), 6);
    }

    [Fact]
    public void A_steel_member_its_weight_the_members_list_and_the_hall_use_it_too()
    {
        var f = F();
        var type = (string)f.Run("steel.createType", new JsonObject { ["name"] = "IPE 300", ["section"] = "IPE 300" })!["id"]!;
        var id = (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 0, ["z1"] = 3, ["x2"] = 6, ["y2"] = 0, ["z2"] = 3 })!["id"]!;
        Assert.Equal(53.81e-4 * 6, Q(f, id, "Qto_MemberBaseQuantities", "NetVolume"), 4);
        Assert.InRange(Q(f, id, "Qto_MemberBaseQuantities", "NetWeight") / 6, 42.0, 42.6); // IPE 300: 42.2 kg/m
        // The members list: kg/m from the same area, each member's weight from the volume.
        var row = SteelMembersSchedule.Rows(f.Model, f.Session.Geometry).Single();
        Assert.InRange(row.KgPerM, 42.0, 42.6);
        Assert.InRange(row.Each / 6, 42.0, 42.6);
    }

    [Fact]
    public void A_cut_member_keeps_its_body_scaled_by_the_real_to_the_sharp_area()
    {
        var f = F();
        var type = (string)f.Run("steel.createType", new JsonObject { ["name"] = "IPE 300", ["section"] = "IPE 300" })!["id"]!;
        var id = (string)f.Run("steel.addMember", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = type, ["x1"] = 0, ["y1"] = 0, ["z1"] = 3, ["x2"] = 6, ["y2"] = 0, ["z2"] = 3 })!["id"]!;
        f.Run("steel.setEnds", new JsonObject { ["id"] = id, ["start"] = new JsonObject { ["cut"] = "plane", ["planAngle"] = 30 } });
        var sharp = Measure.Volume(f.Session.Geometry(id)!.Parts.First(p => p.Key == "body").Solid);
        var section = Section.Named("IPE 300")!;
        var ratio = 53.81 / 51.88;
        Assert.InRange(section.RealRatio, ratio * 0.998, ratio * 1.002);
        Assert.Equal(sharp * section.RealRatio, Q(f, id, "Qto_MemberBaseQuantities", "NetVolume"), 6);
    }

    [Fact]
    public void A_section_without_radii_changes_nothing()
    {
        var f = F();
        var rect = Section.Rectangle(0.3, 0.4);
        Assert.Equal(1, rect.RealRatio);
        Assert.Equal(0.12, rect.Area, 12);
        var plain = new Section("I", 0.15, 0.3, 0.0071, 0.0107);
        Assert.Equal(plain.SharpArea, plain.Area, 12);
        var ct = (string)f.Run("structure.createColumnType", new JsonObject { ["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = 0.3, ["depth"] = 0.4 } })!["id"]!;
        var column = (string)f.Run("structure.addColumn", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = ct, ["x"] = 1, ["y"] = 1 })!["id"]!;
        Assert.Equal(0.12 * Q(f, column, "Qto_ColumnBaseQuantities", "Length"), Q(f, column, "Qto_ColumnBaseQuantities", "NetVolume"), 9);
    }
}
