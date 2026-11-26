using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Lapin.Archicad;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Recorded answers of a running Archicad (fixtures/demo: Archicad 29, Tapir 2.0.1, the demo model of 26.11.2026: three composite
/// walls, one with a window, three columns, a beam on the story above, a composite slab, a multi-plane roof, a stair and four
/// elevations), changed in memory where a test needs another model (an element moved, deleted, a wall or a zone added).
/// </summary>
sealed class ArchicadAnswers
{
    public Dictionary<string, List<JsonObject>> Answers { get; } = [];

    public static string Folder([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "fixtures", "demo");

    public static ArchicadAnswers Demo()
    {
        var o = new ArchicadAnswers();
        foreach (var f in Directory.GetFiles(Folder(), "*.json"))
        {
            var j = (JsonObject)JsonNode.Parse(File.ReadAllText(f))!;
            o.Answers[Path.GetFileNameWithoutExtension(f)] = j["responses"] is JsonArray many ? [.. many.OfType<JsonObject>()] : [(JsonObject)j["response"]!];
        }
        return o;
    }

    public ReplayTransport Transport() => new(Answers.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<JsonObject>)kv.Value.Select(x => (JsonObject)x.DeepClone()).ToList()));

    internal JsonArray List(string command, string key) => (JsonArray)Answers[command][0]["result"]!["addOnCommandResponse"]![key]!;

    internal JsonArray All => List("GetAllElements", "elements");
    internal JsonArray Details => List("GetDetailsOfElements", "detailsOfElements");

    internal int AllIndex(string guid) => All.Select((e, i) => ((string?)e!["elementId"]!["guid"], i)).First(x => x.Item1 == guid).i;

    /// <summary>The index among the elements a receive asks the geometry and IFC data of (no subelements).</summary>
    internal int KeepIndex(string guid)
    {
        var k = 0;
        for (var i = 0; i < All.Count; i++)
        {
            if ((string?)All[i]!["elementId"]!["guid"] == guid) return k;
            if (!ArchicadRead.Subelement((string)Details[i]!["type"]!)) k++;
        }
        throw new InvalidOperationException(guid);
    }

    internal static readonly (string Command, string Key)[] KeepLists =
    [
        ("Get3DGeometryOfElements", "geometries3D"), ("GetIFCIdsOfElements", "elementIFCIds"), ("GetIFCTypeOfElements", "elementIFCTypes"),
        ("GetIFCPropertiesOfElements", "elementIFCProperties"), ("GetClassificationsOfElements", "elementClassifications"), ("GetPropertyValuesOfElements", "propertyValuesForElements"),
    ];

    public JsonObject DetailsOf(string guid) => (JsonObject)Details[AllIndex(guid)]!["details"]!;

    public JsonObject GeometryOf(string guid) => (JsonObject)List("Get3DGeometryOfElements", "geometries3D")[KeepIndex(guid)]!;

    /// <summary>An element and its 3D moved in plan.</summary>
    public void Move(string guid, double dx, double dy)
    {
        void Shift(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o when o["x"] is JsonValue && o["y"] is JsonValue && o.Count <= 3:
                    o["x"] = (double)o["x"]! + dx; o["y"] = (double)o["y"]! + dy; break;
                case JsonObject o: foreach (var (_, v) in o.ToList()) Shift(v); break;
                case JsonArray a: foreach (var v in a) Shift(v); break;
            }
        }
        Shift(DetailsOf(guid));
        Shift(GeometryOf(guid));
    }

    public void Remove(string guid)
    {
        var k = KeepIndex(guid);
        var i = AllIndex(guid);
        foreach (var (c, key) in KeepLists) if (Answers.ContainsKey(c)) { var l = List(c, key); if (k < l.Count) l.RemoveAt(k); }
        Details.RemoveAt(i);
        All.RemoveAt(i);
    }

    /// <summary>An element added at the end: its details and its 3D, with an IFC id.</summary>
    public void Add(string guid, string type, int floor, JsonObject details, JsonObject geometry, string ifcId, string ifcType)
    {
        All.Add((JsonNode)new JsonObject { ["elementId"] = new JsonObject { ["guid"] = guid } });
        Details.Add((JsonNode)new JsonObject { ["type"] = type, ["floorIndex"] = floor, ["layerIndex"] = 1, ["id"] = type.ToUpperInvariant()[..2] + " - 9", ["details"] = details });
        List("Get3DGeometryOfElements", "geometries3D").Add((JsonNode)geometry);
        List("GetIFCIdsOfElements", "elementIFCIds").Add((JsonNode)new JsonObject { ["elementId"] = new JsonObject { ["guid"] = guid }, ["ifcId"] = ifcId, ["externalIFCId"] = "" });
        List("GetIFCTypeOfElements", "elementIFCTypes").Add((JsonNode)new JsonObject { ["elementId"] = new JsonObject { ["guid"] = guid }, ["ifcType"] = ifcType });
        foreach (var (c, key) in KeepLists.Skip(3)) if (Answers.ContainsKey(c)) List(c, key).Add((JsonNode)new JsonObject());
    }

    /// <summary>A box as Archicad's 3D gives a body: eight vertices, six faces anticlockwise seen from outside.</summary>
    public static JsonObject Box(double x0, double y0, double z0, double x1, double y1, double z1)
    {
        JsonObject V(double x, double y, double z) => new() { ["x"] = x, ["y"] = y, ["z"] = z };
        JsonObject F(params int[] ids) => new() { ["vertexIds"] = new JsonArray([.. ids.Select(i => (JsonNode)i)]) };
        return new JsonObject
        {
            ["bodies"] = new JsonArray(new JsonObject
            {
                ["vertices"] = new JsonArray(V(x0, y0, z0), V(x1, y0, z0), V(x1, y1, z0), V(x0, y1, z0), V(x0, y0, z1), V(x1, y0, z1), V(x1, y1, z1), V(x0, y1, z1)),
                ["polygons"] = new JsonArray(F(0, 3, 2, 1), F(4, 5, 6, 7), F(0, 1, 5, 4), F(1, 2, 6, 5), F(2, 3, 7, 6), F(3, 0, 4, 7)),
            }),
        };
    }
}

public sealed class ArchicadTests
{
    // The demo model's GlobalIds (Archicad's IFC ids) and guids.
    const string Wall1 = "0lUYYA0Qg_Jv6L5E_XGz4q", Wall2 = "2XGKthhAKIG8jRkpv60a_O", Wall3 = "1dcL4MxemWJx$zEQdTz95t";
    const string Column1 = "0JCtKV3JjvIAaX7XCZH0OK", Beam = "3ya42F4gwrGOt5Ap5I9XcS", Window = "20oNm40eM6HhRbOKvv6LKg", Slab = "1QhMteRc3bJgiVxKrfRnn4";
    const string Roof = "0lrUJ38HFGJhSzwlw9HemN", Stair = "32RrCq83n3JBdmpyOnubaZ";
    const string Wall3Guid = "67995116-EE8C-204F-BFFD-39A9DDF49177", Column1Guid = "1333751F-0D3B-7948-A921-1E1323440614";
    const string Story1 = "2F0V3QPDDFMRTpJqxqPeZR";

    static Fixture Project(ArchicadAnswers? answers = null) => Project(answers, out _);

    static Fixture Project(ArchicadAnswers? answers, out ArchicadModule module)
    {
        module = new ArchicadModule((answers ?? ArchicadAnswers.Demo()).Transport());
        return new(new ArchitectureModule(), new StructureModule(), module);
    }

    static Vec2 At(Entity e) => e.Attrs["at"] is JsonArray a ? new Vec2((double)a[0]!, (double)a[1]!) : default;

    static JsonObject Receive(Fixture f, JsonObject? args = null)
    {
        var a = args ?? new JsonObject();
        a["port"] = ArchicadClient.FirstPort;
        return (JsonObject)f.Run("archicad.receive", a)!;
    }

    static JsonObject Meta(Entity e) => (JsonObject)e.Attrs[ArchicadReceive.Key]!;

    [Fact]
    public void The_instances_are_found_and_described()
    {
        var f = Project();
        var all = (JsonArray)f.Session.Query("archicad.instances", new JsonObject())!;
        var one = Assert.Single(all.OfType<JsonObject>());
        Assert.Equal(19723, (int)one["port"]!);
        Assert.Equal("29", (string?)one["archicad"]);
        Assert.Equal("2.0.1", (string?)one["tapir"]);
        Assert.True((bool)one["ok"]!);
        Assert.True((bool)one["untitled"]!);
    }

    [Fact]
    public void A_Tapir_older_than_2_is_refused_with_where_to_update()
    {
        var answers = ArchicadAnswers.Demo();
        foreach (var r in answers.Answers["GetAddOnVersion"]) r["result"]!["addOnCommandResponse"]!["version"] = "1.5.8";
        var f = Project(answers);
        var one = (JsonObject)((JsonArray)f.Session.Query("archicad.instances", new JsonObject())!)[0]!;
        Assert.False((bool)one["ok"]!);
        Assert.Contains("2.0", (string)one["problem"]!);
        Assert.Contains("github.com/ENZYME-APD/tapir-archicad-automation/releases", (string)one["problem"]!);
        var ex = Assert.ThrowsAny<Exception>(() => Receive(f));
        Assert.Contains("Tapir 1.5.8", ex.Message);
    }

    [Fact]
    public void The_survey_counts_what_a_receive_would_bring()
    {
        var f = Project();
        var s = (JsonObject)f.Session.Query("archicad.survey", new JsonObject { ["port"] = 19723 })!;
        Assert.Equal(11, (int)s["elements"]!);
        Assert.Equal(3, (int)s["byType"]!["Wall"]!);
        Assert.Equal(3, (int)s["native"]!["columns"]!);
        Assert.Equal(1, (int)s["native"]!["openings"]!);
        Assert.Equal(2, (int)s["references"]!);
        Assert.Equal(49, (int)s["skipped"]!["a part of another element (its owner brings it)"]!);
        Assert.Equal(4, (int)s["skipped"]!["2D only, or no 3D body (Elevation)"]!);
        Assert.Equal(3, ((JsonArray)s["storeys"]!).Count);
        Assert.Equal(11, (int)s["adds"]!);
        Assert.False((bool)s["fromThisProject"]!);
    }

    [Fact]
    public void Walls_slabs_columns_and_beams_come_native_with_Archicads_ids_measured_against_its_bodies()
    {
        var f = Project();
        var r = Receive(f);
        Assert.Equal(11, (int)r["added"]!);
        var m = f.Model;
        foreach (var id in new[] { Wall1, Wall2, Wall3 }) Assert.Equal(IfcClass.Wall, m.Get(id)!.Class);
        Assert.Equal(IfcClass.Slab, m.Get(Slab)!.Class);
        Assert.Equal(IfcClass.Column, m.Get(Column1)!.Class);
        Assert.Equal(IfcClass.Beam, m.Get(Beam)!.Class);
        foreach (var id in new[] { Wall1, Wall2, Wall3, Slab, Column1, Beam })
        {
            Assert.False(StoredBodies.Has(m.Get(id)!), id);
            Assert.Equal("native", (string?)Meta(m.Get(id)!)["recognition"]);
        }
        // The gate measured every native element, all within 1 mm and 0.5 %.
        var fidelity = (JsonObject)r["recognition"]!["fidelity"]!;
        Assert.Equal(8, (int)fidelity["measured"]!);
        Assert.Equal(0, (int)fidelity["failed"]!);
        // The roof and the stair stay references, said why.
        Assert.True(StoredBodies.Has(m.Get(Roof)!));
        Assert.Equal("IfcStair", m.Get(Stair)!.Class);
        Assert.Equal(1, (int)r["recognition"]!["kept"]![$"{ArchicadRecognition.NotOffered}: roof"]!);
        Assert.Equal(1, (int)r["recognition"]!["kept"]![$"{ArchicadRecognition.NotOffered}: stair"]!);
        Assert.Equal(8, (int)r["native"]!);            // 3 walls, 3 columns, a slab, a beam
        Assert.Equal(3, (int)r["references"]!);        // the window, the roof, the stair
        // The beam on the story above: its storey made with Archicad's IFC id for the story; the ground floor matched to the project's.
        Assert.Equal(Story1, Spatial.StoreyOf(m, Beam)!.Id);
        Assert.Equal(f.Storey, Spatial.StoreyOf(m, Wall1)!.Id);
        Assert.Equal(1, (int)r["levels"]!["matched"]!);
        Assert.Equal(2, (int)r["levels"]!["made"]!);
    }

    [Fact]
    public void Properties_classifications_and_layers_come_with_each_element()
    {
        var f = Project();
        Receive(f);
        var wall = f.Model.Get(Wall1)!;
        var props = (JsonObject)wall.Attrs["properties"]!;
        Assert.Equal("2F7A288A-01AA-BE4F-9195-14EFA143D134", (string?)props["Archicad"]!["Guid"]);
        Assert.Equal("SW - 001", (string?)props["Archicad"]!["ID"]);
        Assert.Equal("IfcWall", (string?)props["Archicad"]!["IFC type"]);
        Assert.False(string.IsNullOrEmpty((string?)props["Archicad"]!["Layer"]));
        // The IFC property sets Archicad maps, by their IFC names (standard sets too: they are the file's).
        Assert.Equal("New", (string?)props["AC_Pset_RenovationAndPhasing"]!["Renovation Status"]);
        Assert.NotNull(props["Pset_DesignOptions"]);
        Assert.DoesNotContain(props, kv => kv.Key.StartsWith("Pset_", StringComparison.Ordinal) && kv.Key == "Pset_Archicad");
        Assert.Equal("2F7A288A-01AA-BE4F-9195-14EFA143D134", (string?)Meta(wall)["guid"]);
        Assert.Equal(33, (int)Meta(wall)["layer"]!);
        // Its classification: Archicad Classification v 2.0 as a system of the project, the element's code in it.
        var refs = Classifications.RefsOf(wall);
        var (system, code) = Assert.Single(refs);
        var sys = f.Model.Get(system)!;
        Assert.Equal("Archicad Classification", sys.Name);
        Assert.Equal("v 2.0", sys.Str("edition"));
        Assert.Contains(Classifications.Items(sys), i => i.Code == code);
    }

    [Fact]
    public void A_composite_becomes_a_wall_type_its_skins_as_layers_and_the_axis_on_its_core()
    {
        var f = Project();
        Receive(f);
        var m = f.Model;
        var wall = m.Get(Wall1)!;
        var type = m.RelatingOf(Wall1, IfcClass.RelDefinesByType)!;
        Assert.Equal("Generic Wall/Shell", type.Name);
        var layers = LayerSets.LayersOf(m, type.Id);
        Assert.Equal([0.05, 0.25], layers.Select(l => l.Thickness));
        Assert.True(layers[1].Core);
        // SW - 001 runs −Y at x 0.7319, flipped, its reference line on the core's outside face: the finish on its right (−X), the core
        // to its left; Lapin's axis in the core's middle, 0.125 to the left of the reference line.
        Assert.Equal(0.731926 + 0.125, Walls.Start(wall).X, 5);
        Assert.Equal(0.731926 + 0.125, Walls.End(wall).X, 5);
        var bands = LayerSets.BandsOf(m, Wall1)!;
        Assert.Equal(-0.175, bands[0].From, 6);
        Assert.Equal(0.125, bands[1].To, 6);
        // The three walls share the type; the slab has its own, its skins top down.
        Assert.Equal(type.Id, m.RelatingOf(Wall3, IfcClass.RelDefinesByType)!.Id);
        var slabType = m.RelatingOf(Slab, IfcClass.RelDefinesByType)!;
        Assert.Equal("Generic Slab/Roof", slabType.Name);
        Assert.Equal([0.1, 0.2], LayerSets.LayersOf(m, slabType.Id).Select(l => l.Thickness));
        Assert.Equal(0.1, m.Get(Slab)!.Num("offset"), 6);
    }

    [Fact]
    public void The_window_is_a_native_opening_in_its_host_wall_and_its_reference_stands_in_it()
    {
        var f = Project();
        var r = Receive(f);
        var m = f.Model;
        var opening = Assert.Single(Openings.Of(m, Wall2));
        Assert.Equal(7.0688, opening.Num("along"), 3);
        Assert.Equal(1.0, opening.Num("sill"), 6);
        Assert.Equal(0.9, opening.Num("width"), 6);
        Assert.Equal(1.5, opening.Num("height"), 6);
        var window = m.Get(Window)!;
        Assert.True(StoredBodies.Has(window));
        Assert.Equal(1, (int)r["recognition"]!["kept"]![ArchicadRecognition.FillDiffers]!);
        // No lintel, reveal or join of Lapin's own: the wall's body is Archicad's (the gate measured it with the hole, 0 %).
        Assert.Equal("opening", (string?)Meta(opening)["role"]);
    }

    [Fact]
    public void A_native_element_that_differs_from_its_body_comes_back_a_reference()
    {
        var answers = ArchicadAnswers.Demo();
        // SW - 003 said 2.5 m high while its body is 3 m: the native wall would be another; the gate takes it back.
        answers.DetailsOf(Wall3Guid)["height"] = 2.5;
        var f = Project(answers);
        var r = Receive(f);
        var wall = f.Model.Get(Wall3)!;
        Assert.True(StoredBodies.Has(wall));
        Assert.Equal(IfcClass.Wall, wall.Class);
        Assert.Contains("box", (string?)Meta(wall)["why"]);
        Assert.Equal(1, (int)r["recognition"]!["fidelity"]!["reverted"]!);
        Assert.Contains(((JsonObject)r["recognition"]!["kept"]!).Select(kv => kv.Key), k => k.StartsWith(ArchicadRecognition.Differs, StringComparison.Ordinal));
        // Asked not to recognise, everything comes as references.
        var g = Project();
        var plain = Receive(g, new JsonObject { ["recognize"] = false });
        Assert.Equal(0, (int)plain["native"]!);
        Assert.True(StoredBodies.Has(g.Model.Get(Wall1)!));
    }

    [Theory]
    // Archicad's location, flipped, the distance its reference line stands from the outside face, and where Lapin's axis goes (y)
    // for a wall from (0, 30) to (5, 30) of Generic Wall/Shell (0.05 finish outside, 0.25 core) with its body from y0 to y1.
    [InlineData("Outside", false, 29.825, 29.70, 30.00)]
    [InlineData("Outside", true, 30.175, 30.00, 30.30)]
    [InlineData("Center", false, 29.975, 29.85, 30.15)]
    [InlineData("Inside", false, 30.125, 30.00, 30.30)]
    [InlineData("CoreOutside", false, 29.875, 29.75, 30.05)]
    [InlineData("CoreOutside", true, 30.125, 29.95, 30.25)]
    [InlineData("CoreCenter", false, 30.000, 29.875, 30.175)]
    [InlineData("CoreInside", true, 29.875, 29.70, 30.00)]
    public void Each_reference_line_location_puts_Lapins_axis_on_the_core(string location, bool flipped, double axisY, double y0, double y1)
    {
        var answers = ArchicadAnswers.Demo();
        var details = answers.DetailsOf(Wall3Guid).DeepClone().AsObject();
        details["begCoordinate"] = new JsonObject { ["x"] = 0.0, ["y"] = 30.0 };
        details["endCoordinate"] = new JsonObject { ["x"] = 5.0, ["y"] = 30.0 };
        details["referenceLineLocation"] = location;
        details["flipped"] = flipped;
        details["offset"] = 0.0;
        details.Remove("offsetFromOutside");
        details.Remove("floorPlanPolygons");
        answers.Add("11111111-2222-3333-4444-555555555555", "Wall", 0, details, ArchicadAnswers.Box(0, y0, 0, 5, y1, 3), "1111111111111111111111", "IfcWall");
        var f = Project(answers);
        Receive(f);
        var wall = f.Model.Get("1111111111111111111111")!;
        Assert.False(StoredBodies.Has(wall), (string?)Meta(wall)["why"]);
        var (a, b) = (Walls.Start(wall), Walls.End(wall));
        Assert.Equal(axisY, a.Y, 6);
        Assert.Equal(axisY, b.Y, 6);
        // The layers in Archicad's order (one type for the composite, flipped or not), the core marked; their bands where the body is.
        var bands = LayerSets.BandsOf(f.Model, wall.Id)!;
        Assert.Equal(y0 - axisY, bands.Min(x => x.From), 6);
        Assert.Equal(y1 - axisY, bands.Max(x => x.To), 6);
        Assert.Equal(0.25, bands.Single(x => x.Core).To - bands.Single(x => x.Core).From, 6);
        Assert.Equal(0.0, (bands.Single(x => x.Core).To + bands.Single(x => x.Core).From) / 2, 6);
    }

    [Fact]
    public void The_reference_lines_distance_from_the_outside_face_is_read_from_its_location()
    {
        List<ArchicadSkin> skins = [new(0.02, "Plaster", false), new(0.2, "Brick", true), new(0.1, "Insulation", false)];
        Assert.Equal(0, ArchicadForeign.FromOutside("Outside", 0, skins), 9);
        Assert.Equal(0.16, ArchicadForeign.FromOutside("Center", 0, skins), 9);
        Assert.Equal(0.32, ArchicadForeign.FromOutside("Inside", 0, skins), 9);
        Assert.Equal(0.02, ArchicadForeign.FromOutside("CoreOutside", 0, skins), 9);
        Assert.Equal(0.12, ArchicadForeign.FromOutside("CoreCenter", 0, skins), 9);
        Assert.Equal(0.22, ArchicadForeign.FromOutside("CoreInside", 0, skins), 9);
        Assert.Equal(0.07, ArchicadForeign.FromOutside("CoreOutside", 0.05, skins), 9);
        // Unflipped, the outside face is on the left of the line (0.02 from it): the layers from it run right (NEGATIVE), in
        // Archicad's order; Lapin's axis in the brick's middle, 0.1 right of the reference line.
        var s = ArchicadForeign.Section(skins, 0.02, flipped: false);
        Assert.Equal(["Plaster", "Brick", "Insulation"], s.Layers.Select(l => l.Material));
        Assert.Equal(-0.1, s.Axis, 9);
        Assert.Equal(0.12, s.Offset, 9);
        Assert.True(s.Negative);
        Assert.Equal(-0.3, s.Bands[2].From, 9);
        Assert.Equal(-0.2, s.Bands[2].To, 9);
        // Flipped, the same skins from the outside face on the right.
        var flippedSection = ArchicadForeign.Section(skins, 0.02, flipped: true);
        Assert.False(flippedSection.Negative);
        Assert.Equal(0.1, flippedSection.Axis, 9);
        Assert.Equal(-0.12, flippedSection.Offset, 9);
    }

    [Fact]
    public void A_zone_becomes_a_room_with_its_number_name_outline_and_height()
    {
        var answers = ArchicadAnswers.Demo();
        var details = new JsonObject
        {
            ["name"] = "Living room", ["numberStr"] = "0.01", ["isManual"] = false, ["zCoordinate"] = 0.0, ["height"] = 2.7, ["bottomOffset"] = 0.0, ["topOffset"] = 0.0,
            ["relativeTopStory"] = 0, ["floorThickness"] = 0.0, ["roomLSize"] = 0.0,
            ["polygonOutline"] = new JsonArray(P(1, 7), P(12, 7), P(12, 13), P(1, 13), P(1, 7)),
        };
        answers.Add("99999999-2222-3333-4444-555555555555", "Zone", 0, details, ArchicadAnswers.Box(1, 7, 0, 12, 13, 2.7), "2222222222222222222222", "IfcSpace");
        var f = Project(answers);
        var r = Receive(f);
        var room = f.Model.Get("2222222222222222222222")!;
        Assert.False(StoredBodies.Has(room), (string?)Meta(room)["why"]);
        Assert.Equal("Living room", room.Name);
        Assert.Equal("0.01", room.Str("number"));
        Assert.Equal(2.7, room.Num("height"), 6);
        Assert.Equal(4, ((JsonArray)room.Attrs["contour"]!).Count);
        Assert.Equal(1, (int)r["recognition"]!["byClass"]![room.Class]!);
        static JsonObject P(double x, double y) => new() { ["x"] = x, ["y"] = y };
    }

    [Fact]
    public void Received_again_what_did_not_change_stays_what_moved_is_updated_in_place_and_what_is_gone_is_removed()
    {
        var answers = ArchicadAnswers.Demo();
        var f = Project(answers, out var module);
        Receive(f);
        var before = f.Model.Entities.Where(e => e.Attrs[ArchicadReceive.Key] is JsonObject).Select(e => e.Id).ToHashSet();
        var wall1 = f.Model.Get(Wall1)!;

        // Unchanged: nothing touched, the native elements the same entities.
        var same = Receive(f);
        Assert.Equal(11, (int)same["unchanged"]!);
        Assert.Equal(0, (int)same["added"]! + (int)same["updated"]! + (int)same["removed"]!);
        Assert.Same(wall1, f.Model.Get(Wall1));

        // CRE - 001 moved a metre east, SW - 003 deleted: a new receive (another session reads Archicad anew).
        answers.Move(Column1Guid, 1, 0);
        answers.Remove(Wall3Guid);
        module.Transport = answers.Transport();
        var status = (JsonObject)f.Session.Query("archicad.status", new JsonObject { ["port"] = 19723, ["full"] = true })!;
        Assert.True((bool)status["changed"]!);
        Assert.Equal(1, (int)status["updated"]!);
        Assert.Equal(1, (int)status["removed"]!);
        var next = Receive(f);
        Assert.Equal(1, (int)next["updated"]!);
        Assert.Equal(1, (int)next["removed"]!);
        Assert.Equal(9, (int)next["unchanged"]!);
        var column = f.Model.Get(Column1)!;
        Assert.Equal(IfcClass.Column, column.Class);
        Assert.False(StoredBodies.Has(column));
        Assert.Equal(10.909096 + 1, At(column).X, 4);
        Assert.Null(f.Model.Get(Wall3));
        Assert.Same(wall1, f.Model.Get(Wall1));
    }

    [Fact]
    public void A_receive_is_one_undo_step()
    {
        var f = Project();
        var storeys = Spatial.Storeys(f.Model).Count();
        Receive(f);
        Assert.Contains(f.Model.Entities, e => e.Attrs[ArchicadReceive.Key] is JsonObject);
        f.Session.Undo();
        Assert.DoesNotContain(f.Model.Entities, e => e.Attrs[ArchicadReceive.Key] is JsonObject);
        Assert.Equal(storeys, Spatial.Storeys(f.Model).Count());
        Assert.Null(f.Model.Get(Wall1));
    }

    [Fact]
    public void A_model_made_from_this_project_is_refused_unless_received_as_a_copy()
    {
        var f = Project();
        // The project has an element of its own under one of the model's GlobalIds (Archicad received it from Lapin's IFC).
        f.Session.Edit("own", e => e.Add(new Entity(Wall1, IfcClass.Wall, "Own wall", new JsonObject())));
        var ex = Assert.ThrowsAny<Exception>(() => Receive(f));
        Assert.Contains("made from this project", ex.Message);
        var r = Receive(f, new JsonObject { ["asCopy"] = true });
        Assert.Equal(11, (int)r["added"]!);
        Assert.Equal("Own wall", f.Model.Get(Wall1)!.Name);
        // The copy's ids are its own, derived (the same every time), never the model's.
        var copies = f.Model.Entities.Where(e => (string?)(e.Attrs[ArchicadReceive.Key] as JsonObject)?["key"] == "archicad|untitled|copy" && (string?)Meta(e)["guid"] == "2F7A288A-01AA-BE4F-9195-14EFA143D134").ToList();
        var copy = Assert.Single(copies);
        Assert.NotEqual(Wall1, copy.Id);
        Assert.Equal(ArchicadForeign.Derived("archicad|untitled|copy", "2F7A288A-01AA-BE4F-9195-14EFA143D134"), copy.Id);
    }

    [Fact]
    public void Faces_with_holes_and_concave_ones_are_triangulated_whole()
    {
        // An L-shaped face (concave) with a square hole, flat at z = 0, seen from above: area 3 − 0.25.
        var v = new List<Vec3> { new(0, 0, 0), new(2, 0, 0), new(2, 1, 0), new(1, 1, 0), new(1, 2, 0), new(0, 2, 0), new(0.25, 0.25, 0), new(0.25, 0.75, 0), new(0.75, 0.75, 0), new(0.75, 0.25, 0) };
        var body = new ArchicadBody(v, [new ArchicadFace([0, 1, 2, 3, 4, 5], [[6, 7, 8, 9]], "S")]);
        var part = Assert.Single(ArchicadRead.Triangulate([body]));
        var area = part.Triangles.Sum(t => Vec3.Cross(part.Vertices[t.B] - part.Vertices[t.A], part.Vertices[t.C] - part.Vertices[t.A]).Z / 2);
        Assert.Equal(2.75, area, 9);
        Assert.Equal("S", part.Surface);
    }

    [Fact]
    public void References_take_Archicads_surfaces_as_materials()
    {
        var f = Project();
        Receive(f);
        var roof = f.Model.Get(Roof)!;
        var parts = (JsonArray)roof.Attrs[StoredBodies.Key]!["parts"]!;
        Assert.NotEmpty(parts);
        foreach (var p in parts.OfType<JsonObject>())
        {
            var material = f.Model.Get((string)p["material"]!)!;
            Assert.Equal(IfcClass.Material, material.Class);
            Assert.Equal((string?)p["name"], material.Name);
        }
    }
}

