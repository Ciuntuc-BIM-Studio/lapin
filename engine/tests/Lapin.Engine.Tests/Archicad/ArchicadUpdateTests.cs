using System.IO.Compression;
using System.Text.Json.Nodes;
using Lapin.Archicad;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Package.Remote;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// An Archicad played from the demo fixtures as a running one answers (docs/plan-archicad.md A2): every command over a list of elements
/// answered for the elements asked (in the order asked, an error entry for one it does not have), so a read of some elements gets
/// those; and the element states Tapir keeps: <c>CaptureElementState</c> notes each element's answers, <c>GetElementChangesSince</c>
/// compares them with the answers now (new, modified, deleted; and the elements a test says Archicad touched, as moving a wall touches
/// the wall joined to it), <c>ReleaseElementState</c> frees one, <see cref="Restart"/> forgets them all. A dialog open in Archicad makes
/// every call answer 4001, <c>API.IsAlive</c> too, as Archicad 29 does.
/// </summary>
sealed class FakeArchicad(ArchicadAnswers answers) : IArchicadTransport
{
    public ArchicadAnswers Answers { get; } = answers;
    readonly Dictionary<string, (Dictionary<string, string> Then, HashSet<string> Touched)> _states = [];

    /// <summary>An element Archicad marks modified though its answers are the same (the wall joined to one moved): for every state captured before.</summary>
    public void Touch(string guid) { foreach (var s in _states.Values) s.Touched.Add(guid); }
    public string? Dialog { get; set; }
    /// <summary>Each element-list command asked, with the guids asked.</summary>
    public List<(string Command, List<string> Guids)> Asked { get; } = [];
    public int States => _states.Count;

    public void Restart() => _states.Clear();

    static JsonObject Ok(JsonObject r) => new() { ["succeeded"] = true, ["result"] = new JsonObject { ["addOnCommandResponse"] = r } };

    static JsonObject Failed(string message) => Ok(new JsonObject { ["success"] = false, ["error"] = new JsonObject { ["code"] = 1, ["message"] = message } });

    List<string> Guids() => [.. Answers.All.Select(e => (string)e!["elementId"]!["guid"]!)];

    /// <summary>What Archicad holds of each element now: its answers to every element-list command.</summary>
    Dictionary<string, string> Snapshot()
    {
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in Guids())
            o[g] = string.Join("\n", new[] { Answers.Details[Answers.AllIndex(g)]!.ToJsonString() }.Concat(ArchicadAnswers.KeepLists.Where(k => Answers.Answers.ContainsKey(k.Command)).Select(k => Entry(k.Command, k.Key, g)?.ToJsonString() ?? "")));
        return o;
    }

    JsonNode? Entry(string command, string key, string guid)
    {
        if (command == "GetDetailsOfElements") return Answers.Details[Answers.AllIndex(guid)];
        var details = (string?)Answers.Details[Answers.AllIndex(guid)]!["type"] ?? "";
        if (ArchicadRead.Subelement(details)) return null;
        var list = Answers.List(command, key);
        var k = Answers.KeepIndex(guid);
        return k < list.Count ? list[k] : null;
    }

    static readonly Dictionary<string, string> ListKeys = new()
    {
        ["GetDetailsOfElements"] = "detailsOfElements", ["Get3DGeometryOfElements"] = "geometries3D", ["GetIFCIdsOfElements"] = "elementIFCIds",
        ["GetIFCTypeOfElements"] = "elementIFCTypes", ["GetIFCPropertiesOfElements"] = "elementIFCProperties", ["GetClassificationsOfElements"] = "elementClassifications",
        ["GetPropertyValuesOfElements"] = "propertyValuesForElements",
    };

    public JsonObject? Post(int port, JsonObject body, TimeSpan timeout, CancellationToken ct)
    {
        if (port != ArchicadClient.FirstPort) return null;
        if (Dialog != null) return new JsonObject { ["succeeded"] = false, ["error"] = new JsonObject { ["code"] = 4001, ["message"] = $"Invalid program status (there is an open modal dialog: {Dialog})" } };
        var name = ArchicadClient.NameOf(body);
        var p = body["parameters"]?["addOnCommandParameters"] as JsonObject ?? new JsonObject();
        switch (name)
        {
            case "CaptureElementState":
            {
                var id = Guid.NewGuid().ToString().ToUpperInvariant();
                _states[id] = (Snapshot(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                return Ok(new JsonObject { ["stateId"] = new JsonObject { ["guid"] = id }, ["type"] = "ModificationStamp" });
            }
            case "ReleaseElementState":
                _states.Remove((string?)p["stateId"]?["guid"] ?? "");
                return Ok(new JsonObject { ["success"] = true });
            case "GetElementChangesSince":
            {
                if (!_states.TryGetValue((string?)p["stateId"]?["guid"] ?? "", out var state)) return Failed("The state is unknown: it was never captured, was released, or was lost.");
                var (then, touched) = state;
                var now = Snapshot();
                JsonArray L(IEnumerable<string> g) => new([.. g.Select(x => (JsonNode)new JsonObject { ["elementId"] = new JsonObject { ["guid"] = x } })]);
                return Ok(new JsonObject
                {
                    ["newElements"] = L(now.Keys.Where(g => !then.ContainsKey(g))),
                    ["modifiedElements"] = L(now.Keys.Where(g => then.TryGetValue(g, out var t) && (t != now[g] || touched.Contains(g)))),
                    ["deletedElements"] = L(then.Keys.Where(g => !now.ContainsKey(g))),
                });
            }
            case "GetAllElements":
                return Ok(new JsonObject { ["elements"] = Answers.All.DeepClone() });
        }
        if (ListKeys.TryGetValue(name, out var key) && p["elements"] is JsonArray asked)
        {
            var guids = asked.Select(e => (string?)e?["elementId"]?["guid"] ?? "").ToList();
            Asked.Add((name, guids));
            var have = Guids().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var list = new JsonArray();
            foreach (var g in guids)
                list.Add(have.Contains(g) && Entry(name, key, g) is { } e ? e.DeepClone() : new JsonObject { ["error"] = new JsonObject { ["code"] = 2, ["message"] = "No element." } });
            return Ok(new JsonObject { [key] = list });
        }
        if (!Answers.Answers.TryGetValue(name, out var recorded) || recorded.Count == 0)
            return new JsonObject { ["succeeded"] = false, ["error"] = new JsonObject { ["code"] = 404, ["message"] = $"No answer recorded for {name}." } };
        return (JsonObject)recorded[0].DeepClone();
    }

    /// <summary>The guids an element-list command was asked about since <paramref name="from"/> (an index in <see cref="Asked"/>).</summary>
    public HashSet<string> AskedOf(string command, int from = 0) =>
        Asked.Skip(from).Where(a => a.Command == command).SelectMany(a => a.Guids).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

public sealed class ArchicadUpdateTests
{
    const string Wall1 = "0lUYYA0Qg_Jv6L5E_XGz4q", Wall2 = "2XGKthhAKIG8jRkpv60a_O", Wall3 = "1dcL4MxemWJx$zEQdTz95t";
    const string Column1 = "0JCtKV3JjvIAaX7XCZH0OK", Window = "20oNm40eM6HhRbOKvv6LKg", Slab = "1QhMteRc3bJgiVxKrfRnn4", Roof = "0lrUJ38HFGJhSzwlw9HemN", Beam = "3ya42F4gwrGOt5Ap5I9XcS";
    const string Wall2Guid = "A1414DEB-ACA5-1240-8B5B-BB3E46024F98", Wall3Guid = "67995116-EE8C-204F-BFFD-39A9DDF49177", WindowGuid = "80C97C04-0285-8646-B6E5-614E7919552A";
    const string Column1Guid = "1333751F-0D3B-7948-A921-1E1323440614";
    const string NewWallGuid = "11111111-2222-3333-4444-555555555555", NewWall = "1111111111111111111111";
    const string WallComposite = "29EE690D-089C-4573-94C8-DEC75CFA0950", FinishMaterial = "A24B0919-6AB3-4FCE-9E5E-AE20A8177F5E", Concrete = "0222FAA1-342E-4681-A7EA-383B8461D499";

    static (Fixture F, FakeArchicad A) Project(ArchicadAnswers? answers = null)
    {
        var fake = new FakeArchicad(answers ?? ArchicadAnswers.Demo());
        return (new Fixture(new ArchitectureModule(), new StructureModule(), new ArchicadModule(fake)), fake);
    }

    static JsonObject Run(Fixture f, string command, JsonObject? args = null)
    {
        var a = args ?? new JsonObject();
        a["port"] ??= ArchicadClient.FirstPort;
        return (JsonObject)f.Run(command, a)!;
    }

    static JsonObject Status(Fixture f) => (JsonObject)f.Session.Query("archicad.status", new JsonObject())!;

    static Vec2 Start(Entity wall) => Walls.Start(wall);

    /// <summary>A wall added at the end of the model: Generic Wall/Shell from (0, 30) to (5, 30), its body a box (as the reference-line test makes one).</summary>
    static void AddWall(ArchicadAnswers answers)
    {
        var details = answers.DetailsOf(Wall3Guid).DeepClone().AsObject();
        details["begCoordinate"] = new JsonObject { ["x"] = 0.0, ["y"] = 30.0 };
        details["endCoordinate"] = new JsonObject { ["x"] = 5.0, ["y"] = 30.0 };
        details["referenceLineLocation"] = "Center";
        details["flipped"] = false;
        details["offset"] = 0.0;
        details.Remove("offsetFromOutside");
        details.Remove("floorPlanPolygons");
        answers.Add(NewWallGuid, "Wall", 0, details, ArchicadAnswers.Box(0, 29.85, 0, 5, 30.15, 3), NewWall, "IfcWall");
    }

    [Fact]
    public void A_receive_keeps_Archicads_state_as_the_projects_link_never_in_the_model()
    {
        var (f, fake) = Project();
        var r = Run(f, "archicad.receive");
        Assert.True((bool)r["linked"]!);
        var link = Assert.Single(ArchicadLink.All(f.Session.CopyState).Values);
        Assert.Equal("archicad|untitled", link.Key);
        Assert.Equal(ArchicadClient.FirstPort, link.Port);
        Assert.Equal(3, link.Stories.Count);
        Assert.Equal(1, fake.States);
        Assert.DoesNotContain(f.Model.Entities, e => e.Attrs.ToJsonString().Contains(link.StateId, StringComparison.Ordinal));
        // Received again: a new state kept, the old one released in Archicad.
        Run(f, "archicad.receive");
        Assert.Equal(1, fake.States);
        Assert.NotEqual(link.StateId, ArchicadLink.All(f.Session.CopyState).Values.Single().StateId);
    }

    [Fact]
    public void An_update_reads_only_what_changed_and_updates_it_in_place_keeping_GlobalIds_in_one_undo_step()
    {
        var (f, fake) = Project();
        Run(f, "archicad.receive");
        var m = f.Model;
        var slab = m.Get(Slab)!; var roof = m.Get(Roof)!; var beam = m.Get(Beam)!; var wall1 = m.Get(Wall1)!; var wall3 = m.Get(Wall3)!;
        var start = Start(m.Get(Wall2)!);

        // In Archicad: SW - 002 moved half a metre south with its window; SW - 003, joined to it, touched; CRE - 001 deleted; a wall drawn.
        fake.Answers.Move(Wall2Guid, 0, -0.5);
        fake.Answers.Move(WindowGuid, 0, -0.5);
        fake.Touch(Wall3Guid);
        fake.Answers.Remove(Column1Guid);
        AddWall(fake.Answers);
        var status = Status(f);
        Assert.True((bool)status["changed"]!);
        Assert.Equal(1, (int)status["added"]!);
        Assert.Equal(3, (int)status["updated"]!);      // the wall, its window, the wall joined to it
        Assert.Equal(1, (int)status["removed"]!);
        Assert.False((bool)status["local"]!);

        var asked = fake.Asked.Count;
        var r = Run(f, "archicad.update");
        Assert.Equal("incremental", (string?)r["mode"]);
        // Only the changed elements' 3D was read: the moved wall, its window, the touched wall, the new wall.
        Assert.Equal(new HashSet<string>([Wall2Guid, WindowGuid, Wall3Guid, NewWallGuid], StringComparer.OrdinalIgnoreCase), fake.AskedOf("Get3DGeometryOfElements", asked));
        Assert.Equal(1, (int)r["added"]!);
        Assert.Equal(2, (int)r["updated"]!);
        Assert.Equal(1, (int)r["removed"]!);
        // The moved wall native again with its GlobalId, half a metre south, its window's opening in it; the window its reference, its GlobalId kept.
        var wall2 = m.Get(Wall2)!;
        Assert.Equal(IfcClass.Wall, wall2.Class);
        Assert.False(StoredBodies.Has(wall2));
        Assert.Equal(start.Y - 0.5, Start(wall2).Y, 6);
        Assert.Single(Openings.Of(m, Wall2));
        Assert.NotNull(m.Get(Window));
        Assert.Null(m.Get(Column1));
        Assert.False(StoredBodies.Has(m.Get(NewWall)!));
        // What did not change is the same entity, untouched (the touched wall too: its answers are as they were).
        Assert.Same(slab, m.Get(Slab)); Assert.Same(roof, m.Get(Roof)); Assert.Same(beam, m.Get(Beam)); Assert.Same(wall1, m.Get(Wall1)); Assert.Same(wall3, m.Get(Wall3));
        // A new state kept, the old released; nothing changed since.
        Assert.Equal(1, fake.States);
        Assert.False((bool)Status(f)["changed"]!);

        // One undo step: everything as it was; the project is then behind Archicad, and says so.
        f.Session.Undo();
        Assert.Equal(start.Y, Start(m.Get(Wall2)!).Y, 6);
        Assert.NotNull(m.Get(Column1));
        Assert.Null(m.Get(NewWall));
        var behind = Status(f);
        Assert.True((bool)behind["local"]!);
        Assert.True((bool)behind["changed"]!);
        // Get latest then reads the whole model (the project no longer holds what the state was captured with).
        var again = Run(f, "archicad.update");
        Assert.Equal("full", (string?)again["mode"]);
        Assert.Contains("undo", (string?)again["why"]);
        Assert.Null(m.Get(Column1));
        Assert.NotNull(m.Get(NewWall));
        Assert.Equal(start.Y - 0.5, Start(m.Get(Wall2)!).Y, 6);
    }

    [Fact]
    public void Nothing_changed_nothing_is_read_and_the_state_stays()
    {
        var (f, fake) = Project();
        Run(f, "archicad.receive");
        var state = ArchicadLink.All(f.Session.CopyState).Values.Single().StateId;
        var wall = f.Model.Get(Wall1);
        var asked = fake.Asked.Count;
        var r = Run(f, "archicad.update");
        Assert.True((bool)r["nothing"]!);
        Assert.Equal(11, (int)r["unchanged"]!);
        Assert.Equal(asked, fake.Asked.Count);
        Assert.Same(wall, f.Model.Get(Wall1));
        Assert.Equal(state, ArchicadLink.All(f.Session.CopyState).Values.Single().StateId);
        Assert.Equal(1, fake.States);
    }

    [Fact]
    public void A_state_Archicad_lost_falls_back_to_a_whole_read_compared_by_hash()
    {
        var (f, fake) = Project();
        Run(f, "archicad.receive");
        var wall = f.Model.Get(Wall1);
        fake.Restart();
        fake.Answers.Move(Column1Guid, 1, 0);
        var status = Status(f);
        Assert.True((bool)status["lost"]!);
        var r = Run(f, "archicad.update");
        Assert.Equal("full", (string?)r["mode"]);
        Assert.Contains("restarted", (string?)r["why"]);
        Assert.Equal(1, (int)r["updated"]!);
        Assert.Equal(10, (int)r["unchanged"]!);
        Assert.Same(wall, f.Model.Get(Wall1));
        Assert.False(StoredBodies.Has(f.Model.Get(Column1)!));
        // A new state captured: the next update is incremental again.
        fake.Answers.Move(Column1Guid, 1, 0);
        Assert.Equal("incremental", (string?)Run(f, "archicad.update")["mode"]);
    }

    [Fact]
    public void Another_Archicad_project_is_refused()
    {
        var (f, fake) = Project();
        Run(f, "archicad.receive");
        // Archicad restarted with another untitled project: the same names, other elements.
        fake.Restart();
        var text = string.Join("\n", fake.Answers.Answers.Select(kv => kv.Key + "\t" + new JsonArray([.. kv.Value.Select(x => (JsonNode)x.DeepClone())]).ToJsonString()));
        foreach (var g in fake.Answers.All.Select(e => (string)e!["elementId"]!["guid"]!).ToList()) text = text.Replace(g, Guid.NewGuid().ToString().ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);
        fake.Answers.Answers.Clear();
        foreach (var line in text.Split('\n'))
        {
            var tab = line.IndexOf('\t');
            fake.Answers.Answers[line[..tab]] = [.. ((JsonArray)JsonNode.Parse(line[(tab + 1)..])!).OfType<JsonObject>()];
        }
        var before = f.Model.Entities.Count();
        var ex = Assert.ThrowsAny<Exception>(() => Run(f, "archicad.update"));
        Assert.Contains("not the one this project received", ex.Message);
        Assert.Equal(before, f.Model.Entities.Count());
        Assert.NotNull(f.Model.Get(Wall1));
    }

    [Fact]
    public void An_update_with_nothing_received_says_to_receive_first_and_a_busy_Archicad_says_so()
    {
        var (f, fake) = Project();
        Assert.False((bool)Status(f)["linked"]!);
        Assert.Contains("Receive", Assert.ThrowsAny<Exception>(() => Run(f, "archicad.update")).Message);
        Run(f, "archicad.receive");
        fake.Dialog = "Project Info";
        var status = Status(f);
        Assert.True((bool)status["busy"]!);
        Assert.False((bool)status["changed"]!);
        var ex = Assert.ThrowsAny<Exception>(() => Run(f, "archicad.update"));
        Assert.Contains("dialog is open in it (Project Info)", ex.Message);
        // The instances say it too, rather than that Tapir is missing.
        var one = (JsonObject)((JsonArray)f.Session.Query("archicad.instances", new JsonObject())!)[0]!;
        Assert.Contains("Close it in Archicad", (string?)one["problem"]);
    }

    [Fact]
    public void A_changed_composite_updates_its_type_in_place_and_its_walls_are_rebuilt_on_it()
    {
        var (f, fake) = Project();
        Run(f, "archicad.receive");
        var m = f.Model;
        var type = m.RelatingOf(Wall1, IfcClass.RelDefinesByType)!;
        Assert.Equal(WallComposite, (string?)type.Attrs[ArchicadTypes.TagKey]!["guid"]);
        var wallTypes = m.OfClass(IfcClass.WallType).Count();
        // In Archicad: the composite renamed, its finish made of structural concrete (no element changed).
        var comp = (JsonObject)fake.Answers.Answers["GetComposites"][0]["result"]!["addOnCommandResponse"]!["composites"]!.AsArray().First(c => (string?)c!["attributeId"]!["guid"] == WallComposite)!;
        comp["name"] = "Brick wall 300";
        comp["skins"]![0]!["buildingMaterialId"]!["attributeId"]!["guid"] = Concrete;
        var r = Run(f, "archicad.update");
        Assert.Equal("incremental", (string?)r["mode"]);
        Assert.Equal(4, (int)r["updated"]!);           // the three walls and the window in one of them
        Assert.Contains("Brick wall 300", r["types"]!["updated"]!.AsArray().Select(x => (string?)x));
        // The same type, renamed, its layers rewritten; no other type made; the walls native on it.
        var now = m.Get(type.Id)!;
        Assert.Equal("Brick wall 300", now.Name);
        Assert.Equal(wallTypes, m.OfClass(IfcClass.WallType).Count());
        var layers = LayerSets.LayersOf(m, type.Id);
        Assert.Equal("Concrete - Structural", m.Get(layers[0].Material)!.Name);
        Assert.Equal([0.05, 0.25], layers.Select(l => l.Thickness));
        foreach (var id in new[] { Wall1, Wall2, Wall3 })
        {
            Assert.False(StoredBodies.Has(m.Get(id)!), id);
            Assert.Equal(type.Id, m.RelatingOf(id, IfcClass.RelDefinesByType)!.Id);
        }
    }

    [Fact]
    public void A_building_material_renamed_in_Archicad_renames_its_material()
    {
        var (f, fake) = Project();
        Run(f, "archicad.receive");
        var m = f.Model;
        var material = m.OfClass(IfcClass.Material).Single(x => (string?)(x.Attrs[ArchicadTypes.TagKey] as JsonObject)?["guid"] == FinishMaterial);
        var materials = m.OfClass(IfcClass.Material).Count();
        var bm = fake.Answers.Answers["GetBuildingMaterials"][0]["result"]!["addOnCommandResponse"]!["buildingMaterials"]!.AsArray().First(x => (string?)x!["attributeId"]!["guid"] == FinishMaterial)!;
        bm["name"] = "Lime plaster";
        var r = Run(f, "archicad.update");
        Assert.Equal("Lime plaster", m.Get(material.Id)!.Name);
        Assert.Equal(materials, m.OfClass(IfcClass.Material).Count());
        Assert.Equal("Lime plaster", (string?)r["types"]!["materialsRenamed"]![0]!["to"]);
        // A whole receive matches it the same way.
        bm["name"] = "Lime plaster 2";
        Run(f, "archicad.receive");
        Assert.Equal("Lime plaster 2", m.Get(material.Id)!.Name);
    }

    static JsonObject Geo(double lon, double lat, double e = 0, double n = 0, double h = 0, string crs = "", double abscissa = 1, double ordinate = 0, double inX = 0, double inY = 0) => new()
    {
        ["succeeded"] = true,
        ["result"] = new JsonObject
        {
            ["addOnCommandResponse"] = new JsonObject
            {
                ["projectLocation"] = new JsonObject { ["longitude"] = lon, ["latitude"] = lat, ["altitude"] = 0.0, ["north"] = Math.PI / 2 },
                ["surveyPoint"] = new JsonObject
                {
                    ["position"] = new JsonObject { ["eastings"] = e, ["northings"] = n, ["elevation"] = h },
                    ["positionInProject"] = new JsonObject { ["x"] = inX, ["y"] = inY, ["z"] = 0.0 },
                    ["geoReferencingParameters"] = new JsonObject
                    {
                        ["crsName"] = crs, ["description"] = "", ["geodeticDatum"] = "", ["verticalDatum"] = "", ["mapProjection"] = "", ["mapZone"] = "",
                        ["xAxisAbscissa"] = abscissa, ["xAxisOrdinate"] = ordinate, ["scale"] = 1.0,
                    },
                    ["isVisible"] = false,
                },
            },
        },
    };

    [Fact]
    public void Archicads_survey_point_georeferences_the_project_its_turn_as_the_angle_to_true_north()
    {
        var answers = ArchicadAnswers.Demo();
        // Stereo 70, the survey point at (10, 5) of the project, the project's X axis 30° off the map's east.
        var th = 30 * Math.PI / 180;
        answers.Answers["GetGeoLocation"] = [Geo(27.6, 47.16, 600000, 650000, 45, "EPSG:3844", Math.Cos(th), -Math.Sin(th), 10, 5)];
        var (f, _) = Project(answers);
        var s = (JsonObject)f.Session.Query("archicad.survey", new JsonObject { ["port"] = ArchicadClient.FirstPort })!;
        Assert.Equal("survey", (string?)s["georeference"]!["state"]);
        Assert.True((bool)s["georeference"]!["taken"]!);
        var r = Run(f, "archicad.receive");
        Assert.True((bool)r["georeference"]!["applied"]!);
        Assert.Equal(GeoRef.Stereo70, Location.Crs(f.Model));
        Assert.Equal(30, Location.Base(f.Model).NorthAngle!.Value, 6);
        Assert.Equal(45, Location.Base(f.Model).Elevation, 6);
        // The survey point's plan position lands on its map coordinates.
        var (e, n) = GeoRef.ToMap(f.Model, new Vec2(10, 5));
        Assert.Equal(600000, e, 4);
        Assert.Equal(650000, n, 4);
        // Received again: the project is georeferenced already and keeps it.
        var again = Run(f, "archicad.receive");
        Assert.False((bool)again["georeference"]!["taken"]!);
    }

    [Fact]
    public void Archicads_untouched_default_location_is_not_taken_a_place_set_is()
    {
        var answers = ArchicadAnswers.Demo();
        answers.Answers["GetGeoLocation"] = [Geo(ArchicadGeo.DefaultLongitude, ArchicadGeo.DefaultLatitude)];
        var (f, _) = Project(answers);
        var r = Run(f, "archicad.receive");
        Assert.False((bool)r["georeference"]!["taken"]!);
        Assert.Equal("default", (string?)r["georeference"]!["state"]);
        Assert.False(GeoRef.Georeferenced(f.Model));
        Assert.Null(Location.Base(f.Model).Latitude);

        var placed = ArchicadAnswers.Demo();
        placed.Answers["GetGeoLocation"] = [Geo(26.1025, 44.4268)];
        var (g, _) = Project(placed);
        Run(g, "archicad.receive", new JsonObject { ["georeference"] = false });
        Assert.Null(Location.Base(g.Model).Latitude);
        var taken = Run(g, "archicad.receive");
        Assert.Equal("place", (string?)taken["georeference"]!["state"]);
        Assert.Equal(44.4268, Location.Base(g.Model).Latitude!.Value, 9);
        Assert.Equal(26.1025, Location.Base(g.Model).Longitude!.Value, 9);
    }

    [Fact]
    public void The_link_is_kept_with_the_history_in_remotes_json_and_read_back()
    {
        var h = PackageHistory.Empty();
        Assert.False(h.Any);
        h.Connectors["archicad"] = new JsonObject { ["links"] = new JsonObject { ["archicad|untitled"] = new JsonObject { ["stateId"] = "S", ["port"] = 19723 } } };
        Assert.True(h.Any);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            Assert.Null(h.Clone().WriteTo((name, bytes) => { using var s = zip.CreateEntry(name).Open(); s.Write(bytes); }, new HashSet<string>(), new HashSet<string>()));
        var bytes = ms.ToArray();
        var back = PackageHistory.Of(bytes);
        Assert.Equal("S", (string?)back.Connectors["archicad"]["links"]!["archicad|untitled"]!["stateId"]);
        Assert.Empty(back.Refs);
    }
}
