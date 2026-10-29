using System.Text.Json.Nodes;

namespace Lapin.Ifc;

/// <summary>
/// What an IFC file holds, before it is imported, for the user to choose from
/// (File → Import IFC…): its storeys with how many elements each carries, and
/// its elements by kind, grouped by discipline. A kind is the IFC class; a
/// generic class (a building element proxy, a furnishing element) is split by
/// the category its name starts with, as Revit writes it ("Mechanical
/// Equipment : Dryer : …"), so a machine is not "other". Read from the file's
/// index alone: a 1.2 GB export answers in a second or two.
/// </summary>
public static class IfcSurvey
{
    /// <summary>The classes whose name carries a category worth splitting by.</summary>
    static readonly HashSet<string> Generic = ["IfcBuildingElementProxy", "IfcFurnishingElement", "IfcDistributionElement", "IfcDistributionFlowElement", "IfcDistributionControlElement", "IfcElementAssembly", "IfcGeographicElement", "IfcCivilElement"];

    static readonly Dictionary<string, string> ByClass = new()
    {
        ["IfcWall"] = "architecture", ["IfcWallStandardCase"] = "architecture", ["IfcWallElementedCase"] = "architecture", ["IfcCurtainWall"] = "architecture",
        ["IfcDoor"] = "architecture", ["IfcDoorStandardCase"] = "architecture", ["IfcWindow"] = "architecture", ["IfcWindowStandardCase"] = "architecture",
        ["IfcSlab"] = "architecture", ["IfcSlabStandardCase"] = "architecture", ["IfcSlabElementedCase"] = "architecture", ["IfcRoof"] = "architecture", ["IfcCovering"] = "architecture",
        ["IfcStair"] = "architecture", ["IfcStairFlight"] = "architecture", ["IfcRamp"] = "architecture", ["IfcRampFlight"] = "architecture", ["IfcRailing"] = "architecture",
        ["IfcSpace"] = "architecture", ["IfcShadingDevice"] = "architecture", ["IfcChimney"] = "architecture", ["IfcOpeningElement"] = "architecture", ["IfcBuildingElementPart"] = "architecture",
        ["IfcBeam"] = "structure", ["IfcBeamStandardCase"] = "structure", ["IfcColumn"] = "structure", ["IfcColumnStandardCase"] = "structure", ["IfcMember"] = "structure", ["IfcMemberStandardCase"] = "structure",
        ["IfcPlate"] = "structure", ["IfcPlateStandardCase"] = "structure", ["IfcFooting"] = "structure", ["IfcPile"] = "structure", ["IfcReinforcingBar"] = "structure", ["IfcReinforcingMesh"] = "structure",
        ["IfcTendon"] = "structure", ["IfcTendonAnchor"] = "structure", ["IfcDiscreteAccessory"] = "structure", ["IfcMechanicalFastener"] = "structure", ["IfcFastener"] = "structure",
        ["IfcFurnishingElement"] = "furniture", ["IfcFurniture"] = "furniture", ["IfcSystemFurnitureElement"] = "furniture",
        ["IfcGeographicElement"] = "site", ["IfcCivilElement"] = "site",
    };

    /// <summary>A Revit category a generic element's name starts with, to its discipline.</summary>
    static readonly Dictionary<string, string> ByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mechanical Equipment"] = "mep", ["Plumbing Fixtures"] = "mep", ["Plumbing Equipment"] = "mep", ["Electrical Equipment"] = "mep", ["Electrical Fixtures"] = "mep",
        ["Lighting Fixtures"] = "mep", ["Lighting Devices"] = "mep", ["Fire Alarm Devices"] = "mep", ["Fire Protection"] = "mep", ["Sprinklers"] = "mep", ["Ducts"] = "mep", ["Duct Fittings"] = "mep",
        ["Duct Accessories"] = "mep", ["Pipes"] = "mep", ["Pipe Fittings"] = "mep", ["Pipe Accessories"] = "mep", ["Cable Trays"] = "mep", ["Cable Tray Fittings"] = "mep", ["Conduits"] = "mep",
        ["Conduit Fittings"] = "mep", ["Air Terminals"] = "mep", ["Data Devices"] = "mep", ["Communication Devices"] = "mep", ["Security Devices"] = "mep", ["Nurse Call Devices"] = "mep",
        ["Telephone Devices"] = "mep", ["Flex Ducts"] = "mep", ["Flex Pipes"] = "mep", ["Wires"] = "mep",
        ["Furniture"] = "furniture", ["Furniture Systems"] = "furniture", ["Casework"] = "furniture", ["Entourage"] = "furniture", ["Planting"] = "furniture", ["Specialty Equipment"] = "furniture",
        ["Site"] = "site", ["Topography"] = "site", ["Parking"] = "site", ["Roads"] = "site", ["Pads"] = "site",
        ["Structural Framing"] = "structure", ["Structural Columns"] = "structure", ["Structural Foundations"] = "structure", ["Structural Connections"] = "structure", ["Structural Rebar"] = "structure",
        ["Walls"] = "architecture", ["Floors"] = "architecture", ["Ceilings"] = "architecture", ["Roofs"] = "architecture", ["Doors"] = "architecture", ["Windows"] = "architecture", ["Stairs"] = "architecture", ["Railings"] = "architecture",
    };

    public static readonly (string Id, string Label)[] Disciplines =
    [
        ("architecture", "Architecture"), ("structure", "Structure"), ("mep", "MEP"), ("furniture", "Furniture and equipment"), ("site", "Site"), ("other", "Other"),
    ];

    /// <summary>The kind an element is counted and chosen by: its class, with its category for a generic one ("IfcBuildingElementProxy|Mechanical Equipment").</summary>
    public static string KindOf(StepEntity e)
    {
        var cls = IfcModel.ClassName(e.Type);
        return Generic.Contains(cls) && CategoryOf(e) is { } c ? $"{cls}|{c}" : cls;
    }

    /// <summary>What a generic element's name starts with, up to " : " (Revit's category), when it has such a name.</summary>
    public static string? CategoryOf(StepEntity e)
    {
        if (e[2] is not string name) return null;
        var cut = name.IndexOf(" : ", StringComparison.Ordinal);
        if (cut <= 0 || cut > 40) return null;
        var c = name[..cut].Trim();
        return c.Length > 0 ? c : null;
    }

    public static string DisciplineOf(string cls, string? category)
    {
        if (cls.StartsWith("IfcFlow", StringComparison.Ordinal) || cls.StartsWith("IfcDistribution", StringComparison.Ordinal) || cls.StartsWith("IfcEnergy", StringComparison.Ordinal)
            || cls.StartsWith("IfcDuct", StringComparison.Ordinal) || cls.StartsWith("IfcPipe", StringComparison.Ordinal) || cls.StartsWith("IfcCable", StringComparison.Ordinal)
            || cls.StartsWith("IfcElectric", StringComparison.Ordinal) || cls.StartsWith("IfcLight", StringComparison.Ordinal) || cls.StartsWith("IfcSanitary", StringComparison.Ordinal)
            || cls.StartsWith("IfcAir", StringComparison.Ordinal) || cls.StartsWith("IfcFire", StringComparison.Ordinal) || cls.StartsWith("IfcFan", StringComparison.Ordinal)
            || cls.StartsWith("IfcPump", StringComparison.Ordinal) || cls.StartsWith("IfcValve", StringComparison.Ordinal) || cls.StartsWith("IfcTank", StringComparison.Ordinal)
            || cls.StartsWith("IfcBoiler", StringComparison.Ordinal) || cls.StartsWith("IfcChiller", StringComparison.Ordinal) || cls.StartsWith("IfcCoil", StringComparison.Ordinal)
            || cls.StartsWith("IfcUnitary", StringComparison.Ordinal) || cls.StartsWith("IfcHeat", StringComparison.Ordinal) || cls.StartsWith("IfcCommunications", StringComparison.Ordinal)
            || cls.StartsWith("IfcAudio", StringComparison.Ordinal) || cls.StartsWith("IfcSensor", StringComparison.Ordinal) || cls.StartsWith("IfcActuator", StringComparison.Ordinal)
            || cls.StartsWith("IfcController", StringComparison.Ordinal) || cls.StartsWith("IfcAlarm", StringComparison.Ordinal) || cls.StartsWith("IfcSwitch", StringComparison.Ordinal)
            || cls.StartsWith("IfcOutlet", StringComparison.Ordinal) || cls.StartsWith("IfcJunction", StringComparison.Ordinal) || cls.StartsWith("IfcProtective", StringComparison.Ordinal)
            || cls.StartsWith("IfcTransformer", StringComparison.Ordinal) || cls.StartsWith("IfcMotor", StringComparison.Ordinal) || cls.StartsWith("IfcSolar", StringComparison.Ordinal)
            || cls.StartsWith("IfcSpaceHeater", StringComparison.Ordinal) || cls.StartsWith("IfcHumidifier", StringComparison.Ordinal) || cls.StartsWith("IfcEvaporat", StringComparison.Ordinal)
            || cls.StartsWith("IfcCompressor", StringComparison.Ordinal) || cls.StartsWith("IfcCondenser", StringComparison.Ordinal) || cls.StartsWith("IfcCooling", StringComparison.Ordinal)
            || cls.StartsWith("IfcCooled", StringComparison.Ordinal) || cls.StartsWith("IfcFilter", StringComparison.Ordinal) || cls.StartsWith("IfcInterceptor", StringComparison.Ordinal)
            || cls.StartsWith("IfcMedical", StringComparison.Ordinal) || cls.StartsWith("IfcStackTerminal", StringComparison.Ordinal) || cls.StartsWith("IfcWasteTerminal", StringComparison.Ordinal)
            || cls.StartsWith("IfcDamper", StringComparison.Ordinal) || cls.StartsWith("IfcSilencer", StringComparison.Ordinal) || cls.StartsWith("IfcVibration", StringComparison.Ordinal)
            || cls.StartsWith("IfcBurner", StringComparison.Ordinal) || cls.StartsWith("IfcEngine", StringComparison.Ordinal) || cls.StartsWith("IfcElectricGenerator", StringComparison.Ordinal))
            return "mep";
        if (category != null && ByCategory.TryGetValue(category, out var byCategory)) return byCategory;
        return ByClass.GetValueOrDefault(cls, "other");
    }

    /// <summary>
    /// The storey (its file id; 0 for none) each element stands on: up what contains it (a storey, or a space that
    /// stands on one, as ArchiCAD contains furniture) and what it is a part of (a wall's parts, a stair's flights),
    /// as many levels as it takes. The survey and the import's choice read it alike.
    /// </summary>
    public static Func<int, int> StoreyResolver(StepFile f)
    {
        var up = new Dictionary<int, int>();
        var storeys = f.OfType("IFCBUILDINGSTOREY").Select(s => s.Id).ToHashSet();
        foreach (var r in f.OfType("IFCRELCONTAINEDINSPATIALSTRUCTURE")) if (r[5] is StepRef s) foreach (var p in Ids(r[4])) up[p] = s.Id;
        foreach (var r in f.OfType("IFCRELAGGREGATES"))
        {
            if (r[4] is not StepRef whole || !f.Entities.TryGetValue(whole.Id, out var w) || w.Type is "IFCPROJECT" or "IFCSITE" or "IFCBUILDING") continue;
            foreach (var p in Ids(r[5])) up.TryAdd(p, whole.Id);
        }
        var known = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();
        return id => known.GetOrAdd(id, start =>
        {
            var at = start;
            for (var depth = 0; depth < 16; depth++)
            {
                if (storeys.Contains(at)) return at;
                if (!up.TryGetValue(at, out var next)) return 0;
                at = next;
            }
            return 0;
        });
    }

    /// <summary>The survey of a read file, as the interface shows it.</summary>
    public static JsonObject Of(StepFile f, string name, long size)
    {
        var len = IfcModel.LengthUnit(f);
        var storeyOfProduct = StoreyResolver(f);
        int StoreyFileId(int product) => storeyOfProduct(product);

        var storeys = f.OfType("IFCBUILDINGSTOREY").Select(s => (Id: s.Id, Gid: (string?)s[0] ?? "", Name: (string?)s[2] ?? (string?)s[7] ?? "Level", Elevation: IfcModel.Num(s[9]) * len)).OrderBy(s => s.Elevation).ToList();
        var perStorey = storeys.ToDictionary(s => s.Id, _ => 0);
        var noStorey = 0;
        var kinds = new Dictionary<string, (string Class, string? Category, int Count)>();
        var products = 0;
        // The elements, and the rooms (spaces: chosen as one more kind); not the virtual elements (space boundaries, no body).
        foreach (var e in f.Rooted)
        {
            if (!(IfcImport.IsProductEntity(e) || e.Type == "IFCSPACE") || e.Type == "IFCVIRTUALELEMENT") continue;
            products++;
            var kind = KindOf(e);
            var (cls, category) = Split(kind);
            kinds[kind] = (cls, category, kinds.TryGetValue(kind, out var k) ? k.Count + 1 : 1);
            var at = StoreyFileId(e.Id);
            if (at != 0 && perStorey.ContainsKey(at)) perStorey[at]++; else noStorey++;
        }
        var groups = new JsonArray();
        foreach (var (id, label) in Disciplines)
        {
            var rows = kinds.Where(k => DisciplineOf(k.Value.Class, k.Value.Category) == id).OrderByDescending(k => k.Value.Count).ThenBy(k => k.Key).ToList();
            if (rows.Count == 0) continue;
            groups.Add((JsonNode)new JsonObject
            {
                ["discipline"] = id, ["label"] = label, ["count"] = rows.Sum(r => r.Value.Count),
                ["kinds"] = new JsonArray(rows.Select(r => (JsonNode)new JsonObject { ["key"] = r.Key, ["class"] = r.Value.Class, ["category"] = r.Value.Category, ["label"] = Label(r.Value.Class, r.Value.Category), ["count"] = r.Value.Count }).ToArray()),
            });
        }
        var application = f.Header("FILE_NAME") is { } fileName && fileName.Count > 5 && fileName[5] is string app ? app : null;
        return new JsonObject
        {
            ["name"] = name, ["size"] = size, ["schema"] = f.Schema, ["application"] = application, ["products"] = products,
            ["storeys"] = new JsonArray(storeys.Select(s => (JsonNode)new JsonObject { ["id"] = s.Gid, ["name"] = s.Name, ["elevation"] = Math.Round(s.Elevation, 3), ["count"] = perStorey[s.Id] }).ToArray()),
            ["noStorey"] = noStorey,
            ["groups"] = groups,
        };
    }

    static IEnumerable<int> Ids(object? v) => v is List<object?> l ? l.OfType<StepRef>().Select(r => r.Id) : [];

    public static (string Class, string? Category) Split(string kind)
    {
        var bar = kind.IndexOf('|');
        return bar < 0 ? (kind, null) : (kind[..bar], kind[(bar + 1)..]);
    }

    /// <summary>A kind as the interface names it: "Wall", "Proxy · Mechanical Equipment".</summary>
    static string Label(string cls, string? category)
    {
        var plain = cls.StartsWith("Ifc", StringComparison.Ordinal) ? cls[3..] : cls;
        plain = plain.Replace("StandardCase", "").Replace("BuildingElementProxy", "Proxy").Replace("FurnishingElement", "Furnishing").Replace("OpeningElement", "Opening");
        // CamelCase to words: "StairFlight" → "Stair flight".
        var words = System.Text.RegularExpressions.Regex.Replace(plain, "(?<=[a-z])(?=[A-Z])", " ");
        words = words.Length > 1 ? words[0] + words[1..].ToLowerInvariant() : words;
        return category == null ? words : $"{words} · {category}";
    }
}
