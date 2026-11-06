using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Transport;

/// <summary>
/// EN 81-20's tables (5.4.2): the rated load a car's available area may hold at most (Table 6) and the least area
/// for a number of passengers (Table 8); a passenger is 75 kg. EN 81-70's three accessible cars (Table 1).
/// </summary>
public static class En81
{
    /// <summary>Rated load (kg) → the largest available car area (m²), EN 81-20 Table 6.</summary>
    public static readonly (double Load, double Area)[] MaxArea =
    [
        (100, 0.37), (180, 0.58), (225, 0.70), (300, 0.90), (375, 1.10), (400, 1.17), (450, 1.30), (525, 1.45), (600, 1.60),
        (630, 1.66), (675, 1.75), (750, 1.90), (800, 2.00), (825, 2.05), (900, 2.20), (975, 2.35), (1000, 2.40), (1050, 2.50),
        (1125, 2.65), (1200, 2.80), (1250, 2.90), (1275, 2.95), (1350, 3.10), (1425, 3.25), (1500, 3.40), (1600, 3.56),
        (2000, 4.20), (2500, 5.00),
    ];

    /// <summary>Passengers → the least available car area (m²), EN 81-20 Table 8.</summary>
    public static readonly double[] MinAreaForPersons = [0, 0.28, 0.49, 0.60, 0.79, 0.98, 1.17, 1.31, 1.45, 1.59, 1.73, 1.87, 2.01, 2.15, 2.29, 2.43, 2.57, 2.71, 2.85, 2.99, 3.13];

    /// <summary>The largest available area a rated load allows: the table, linear between its rows, 0.16 m² per 100 kg above 2500 kg.</summary>
    public static double AreaForLoad(double load)
    {
        if (load <= MaxArea[0].Load) return MaxArea[0].Area * load / MaxArea[0].Load;
        if (load > MaxArea[^1].Load) return MaxArea[^1].Area + (load - MaxArea[^1].Load) / 100 * 0.16;
        for (var i = 1; i < MaxArea.Length; i++)
            if (load <= MaxArea[i].Load)
            {
                var (a, b) = (MaxArea[i - 1], MaxArea[i]);
                return a.Area + (b.Area - a.Area) * (load - a.Load) / (b.Load - a.Load);
            }
        return MaxArea[^1].Area;
    }

    /// <summary>The least rated load a car of this available area must have (EN 81-20: the area may not be larger than its load allows), rounded up to 5 kg.</summary>
    public static double LoadForArea(double area)
    {
        if (area <= MaxArea[0].Area) return MaxArea[0].Load;
        if (area > MaxArea[^1].Area) return Math.Ceiling((MaxArea[^1].Load + (area - MaxArea[^1].Area) / 0.16 * 100) / 5) * 5;
        for (var i = 1; i < MaxArea.Length; i++)
            if (area <= MaxArea[i].Area + 1e-9)
            {
                var (a, b) = (MaxArea[i - 1], MaxArea[i]);
                return Math.Ceiling((a.Load + (b.Load - a.Load) * (area - a.Area) / (b.Area - a.Area)) / 5) * 5;
            }
        return MaxArea[^1].Load;
    }

    /// <summary>The least area for <paramref name="persons"/> passengers: Table 8, 0.115 m² for each above 20.</summary>
    public static double AreaForPersons(int persons) =>
        persons <= 0 ? 0 : persons < MinAreaForPersons.Length ? MinAreaForPersons[persons] : MinAreaForPersons[^1] + (persons - 20) * 0.115;

    /// <summary>How many passengers a car carries: its load over 75 kg, rounded down, and no more than its area holds (Table 8).</summary>
    public static int Persons(double load, double area)
    {
        var byLoad = (int)Math.Floor(load / 75 + 1e-9);
        var byArea = 0;
        while (byArea < 400 && AreaForPersons(byArea + 1) <= area + 1e-9) byArea++;
        return Math.Max(0, Math.Min(byLoad, byArea));
    }

    /// <summary>EN 81-70's accessible cars: the least car (width × depth, m), the least door, the rated load it comes with.</summary>
    public static readonly Dictionary<string, (double W, double D, double Door, double Load, string Label)> Accessible = new()
    {
        ["type1"] = (1.0, 1.25, 0.8, 450, "Type 1: one wheelchair user"),
        ["type2"] = (1.1, 1.4, 0.8, 630, "Type 2: a wheelchair user and a companion"),
        ["type3"] = (2.0, 1.4, 0.9, 1275, "Type 3: a wheelchair user and companions, room to turn"),
    };
}

/// <summary>
/// A lift as its attributes say it (docs/plan-elevators.md): its drive, load and persons, speed, the car, the doors, the
/// side of its counterweight (or jack), the pit and headroom, the machine room, the shaft's walls. A missing value is the
/// drive's default, so an old lift reads as it always did.
/// </summary>
public sealed class ElevatorSpec
{
    public static readonly string[] Drives = ["traction", "mrl", "hydraulic", "home"];
    public static readonly string[] Openings = ["centre", "side"];
    public static readonly string[] Sides = ["left", "right", "back"];
    public static readonly string[] Entrances = ["front", "through"];
    public static readonly string[] MachineRooms = ["top", "beside", "none"];
    public static readonly string[] AccessibleTypes = ["none", "type1", "type2", "type3"];

    readonly JsonObject a;
    public ElevatorSpec(JsonObject? attrs) => a = attrs ?? [];

    double N(string k, double d) => Json.Number(a[k]) ?? d;
    string S(string k, string[] allowed, string d) => a[k] is JsonValue v && v.TryGetValue<string>(out var s) && allowed.Contains(s) ? s : d;
    bool B(string k) => a[k] is JsonValue v && v.TryGetValue<bool>(out var b) && b;

    public string Drive => S("drive", Drives, "mrl");
    public double Load => Math.Max(50, N("load", 630));
    public double Speed => Math.Max(0.05, N("speed", Drive switch { "hydraulic" => 0.63, "home" => 0.15, _ => 1.0 }));
    public double CabinWidth => Math.Clamp(N("cabinWidth", 1.1), 0.6, 4);
    public double CabinDepth => Math.Clamp(N("cabinDepth", 1.4), 0.6, 6);
    public double CabinHeight => Math.Clamp(N("cabinHeight", 2.2), 1.9, 4);
    public double CabinArea => CabinWidth * CabinDepth;
    /// <summary>Its passengers: as given, else what its load and area carry.</summary>
    public int Persons => a["persons"] is { } p && Json.Number(p) is { } n && n >= 1 ? (int)Math.Round(n) : En81.Persons(Load, CabinArea);
    public bool Glass => B("glass");
    public string Entrance => S("entrances", Entrances, "front");
    public bool Through => Entrance == "through";
    public double DoorWidth => Math.Clamp(N("doorWidth", 0.8), 0.5, 3);
    public double DoorHeight => Math.Clamp(N("doorHeight", 2.0), 1.8, 3.5);
    public string DoorOpening => S("doorOpening", Openings, "side");
    public int DoorPanels => (int)Math.Clamp(Math.Round(N("doorPanels", 2)), 1, DoorOpening == "centre" ? 4 : 3) is var p && DoorOpening == "centre" ? (p >= 3 ? 4 : 2) : p;
    /// <summary>The counterweight's side (a hydraulic lift's jack, a home lift's drive): left, right or behind the car; a through car keeps it at a side.</summary>
    public string Counterweight => S("counterweight", Sides, "left") is var s && s == "back" && Through ? "left" : s;
    public string Accessible => S("accessible", AccessibleTypes, "none");
    public string MachineRoom => S("machineRoom", MachineRooms, Drive switch { "traction" => "top", "hydraulic" => "beside", _ => "none" });
    public double Pit => Math.Clamp(N("pit", SuggestedPit(Drive, Speed)), 0.05, 4);
    public double Headroom => Math.Clamp(N("headroom", SuggestedHeadroom(Drive, Speed)), 2.2, 8);
    public string? ShaftWallType => a["shaftWallTypeId"] is JsonValue v && v.TryGetValue<string>(out var t) && t.Length > 0 ? t : null;
    public double ShaftThickness => Math.Clamp(N("shaftThickness", 0.2), 0.05, 0.6);
    public bool ShaftGlass => B("shaftGlass");
    public bool FireFighting => B("fireFighting");
    public bool FireExit => B("fireExit");

    /// <summary>The pit a drive needs at a speed: EN 81-20's refuge under the car and the buffer's stroke grow with the speed (rounded to 50 mm).</summary>
    public static double SuggestedPit(string drive, double speed) => R05(drive switch
    {
        "traction" => 1.2 + 0.25 * speed,
        "mrl" => 1.1 + 0.15 * speed,
        "hydraulic" => 1.2,
        _ => 0.15,
    });

    /// <summary>The headroom over the top stop: the refuge on the car roof and the run-by above it (rounded to 50 mm).</summary>
    public static double SuggestedHeadroom(string drive, double speed) => R05(drive switch
    {
        "traction" => 3.4 + 0.3 * speed,
        "mrl" => 3.4 + 0.2 * speed,
        "hydraulic" => 3.5,
        _ => 2.5,
    });

    static double R05(double v) => Math.Round(v * 20) / 20;

    /// <summary>The clear widths round the car in the shaft: at its plain side, in the counterweight's (or jack's) zone, in front (the doors' sills and panels) and behind.</summary>
    public (double Side, double Zone, double Front, double Back) Clearances => Drive switch
    {
        "traction" => (0.15, 0.35, 0.40, 0.30),
        "mrl" => (0.15, 0.40, 0.40, 0.30),
        "hydraulic" => (0.15, 0.30, 0.40, 0.30),
        _ => (0.07, 0.12, 0.15, 0.08),
    };

    /// <summary>How wide the landing doors' wall must be inside the shaft for the panels to slide clear: a centre door's halves each side, a side door's panels stacked.</summary>
    public double DoorWallNeed => DoorOpening == "centre"
        ? DoorPanels >= 4 ? DoorWidth * 1.5 : DoorWidth * 2
        : DoorWidth * (1 + 1.0 / DoorPanels);

    /// <summary>What EN 81-20 and EN 81-70 say of the configuration: each line a warning; none when it complies.</summary>
    public List<string> Checks(double travel)
    {
        var o = new List<string>();
        var area = CabinArea;
        var max = En81.AreaForLoad(Load);
        if (area > max + 1e-6)
            o.Add($"EN 81-20: a car of {F(area)} m² needs a rated load of at least {F0(En81.LoadForArea(area))} kg ({F0(Load)} kg allows {F(max)} m²).");
        var byLoad = (int)Math.Floor(Load / 75 + 1e-9);
        if (Persons > byLoad) o.Add($"EN 81-20: {Persons} persons need a rated load of {Persons * 75} kg.");
        if (En81.AreaForPersons(Persons) > area + 1e-6) o.Add($"EN 81-20: {Persons} persons need a car of {F(En81.AreaForPersons(Persons))} m².");
        if (En81.Accessible.TryGetValue(Accessible, out var acc))
        {
            if (CabinWidth < acc.W - 1e-6 || CabinDepth < acc.D - 1e-6) o.Add($"EN 81-70 {Accessible.Replace("type", "type ")}: the car is at least {F0(acc.W * 1000)} × {F0(acc.D * 1000)} mm.");
            if (DoorWidth < acc.Door - 1e-6) o.Add($"EN 81-70 {Accessible.Replace("type", "type ")}: the door is at least {F0(acc.Door * 1000)} mm wide.");
        }
        if (DoorWidth > CabinWidth + 1e-6) o.Add("The door is wider than the car.");
        if (Drive == "hydraulic" && travel > 18) o.Add($"A hydraulic lift travels up to about 18 m; this one travels {F(travel)} m.");
        if (Drive == "home" && Speed > 0.15 + 1e-6) o.Add("A home lift (EN 81-41) runs at 0.15 m/s at most.");
        if (Drive == "home" && Load > 500) o.Add("A home lift (EN 81-41) carries up to about 500 kg.");
        if ((string?)a["counterweight"] == "back" && Through) o.Add("A through car keeps its counterweight at a side.");
        return o;
    }

    static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    static string F0(double v) => Math.Round(v).ToString(CultureInfo.InvariantCulture);
}

/// <summary>A catalogue style: the kind of lift it is and the values it sets.</summary>
public sealed record ElevatorStyle(string Id, string Label, string Group, string Description, JsonObject Values)
{
    public JsonObject ToJson() => new()
    {
        ["id"] = Id, ["label"] = Label, ["group"] = Group, ["description"] = Description, ["values"] = Values.DeepClone(),
    };
}

/// <summary>
/// The twelve kinds testers asked for (reports #54 and #97), with sizes EN 81-20 allows for their loads (Table 6) and
/// EN 81-70's for the accessible car; where a report's size would need a larger load, the size EN 81 gives for the load.
/// </summary>
public static class ElevatorStyles
{
    /// <summary>The attributes a style sets (and a project style keeps).</summary>
    public static readonly string[] Fields =
    [
        "drive", "load", "persons", "speed", "cabinWidth", "cabinDepth", "cabinHeight", "glass", "entrances", "doorWidth", "doorHeight",
        "doorOpening", "doorPanels", "counterweight", "accessible", "pit", "headroom", "machineRoom", "shaftThickness", "shaftGlass", "fireFighting", "fireExit",
    ];

    static ElevatorStyle S(string id, string label, string group, string description, string drive, double load, int persons, double w, double d, double h,
        double door, double doorH, string opening, int panels, double speed, string cw = "left", string accessible = "none", bool glass = false, bool shaftGlass = false, string entrances = "front")
    {
        var v = new JsonObject
        {
            ["drive"] = drive, ["load"] = load, ["persons"] = persons, ["speed"] = speed, ["cabinWidth"] = w, ["cabinDepth"] = d, ["cabinHeight"] = h,
            ["glass"] = glass, ["entrances"] = entrances, ["doorWidth"] = door, ["doorHeight"] = doorH, ["doorOpening"] = opening, ["doorPanels"] = panels,
            ["counterweight"] = cw, ["accessible"] = accessible, ["pit"] = ElevatorSpec.SuggestedPit(drive, speed), ["headroom"] = ElevatorSpec.SuggestedHeadroom(drive, speed),
            ["machineRoom"] = drive switch { "traction" => "top", "hydraulic" => "beside", _ => "none" }, ["shaftThickness"] = shaftGlass ? 0.05 : drive == "home" ? 0.15 : 0.2,
            ["shaftGlass"] = shaftGlass, ["fireFighting"] = false, ["fireExit"] = false,
        };
        return new ElevatorStyle(id, label, group, description, v);
    }

    public static readonly IReadOnlyList<ElevatorStyle> All =
    [
        S("residential-small", "Small residential", "Passenger", "3–4 persons, 250–320 kg: a small block of flats.", "mrl", 320, 4, 0.9, 1.0, 2.1, 0.7, 2.0, "side", 2, 1.0),
        S("passenger-standard", "Standard passenger", "Passenger", "6 persons, 450 kg: EN 81-20 allows 1.30 m², so 1000 × 1250 (EN 81-70 type 1).", "mrl", 450, 6, 1.0, 1.25, 2.2, 0.8, 2.0, "side", 2, 1.0, accessible: "type1"),
        S("passenger-medium", "Medium passenger", "Passenger", "8 persons, 630 kg: the usual lift of flats and offices.", "traction", 630, 8, 1.1, 1.4, 2.2, 0.8, 2.0, "side", 2, 1.0),
        S("passenger-large", "Large passenger", "Passenger", "10 persons, 800 kg: EN 81-20 allows 2.00 m², so 1350 × 1400.", "traction", 800, 10, 1.35, 1.4, 2.3, 0.9, 2.1, "centre", 2, 1.0),
        S("heavy-duty", "Heavy-duty passenger", "Passenger", "13 persons, 1000 kg: offices, hotels, busy buildings.", "traction", 1000, 13, 1.6, 1.4, 2.3, 1.1, 2.1, "centre", 2, 1.6),
        S("accessible", "Wheelchair-accessible", "Accessible", "EN 81-70 type 2: a wheelchair user and a companion, door 900.", "mrl", 630, 8, 1.1, 1.4, 2.2, 0.9, 2.0, "side", 2, 1.0, accessible: "type2"),
        S("hospital", "Hospital bed lift", "Special", "A bed and its attendants; 1400 × 2400 or 1500 × 2300 need 1500–1600 kg.", "traction", 1275, 17, 1.2, 2.3, 2.3, 1.1, 2.1, "side", 3, 1.0, entrances: "through"),
        S("goods", "Goods lift", "Goods", "1000–2000 kg, slow, a wide door; persons may travel with the goods.", "traction", 2000, 26, 1.5, 2.7, 2.3, 1.5, 2.3, "side", 2, 0.5),
        S("hydraulic", "Hydraulic (low-rise)", "Special", "250–1000 kg, up to about 18 m: its power unit in a room beside the shaft.", "hydraulic", 630, 8, 1.1, 1.4, 2.2, 0.8, 2.0, "side", 2, 0.63),
        S("mrl", "Machine-room-less (MRL)", "Passenger", "320–1600 kg, the machine in the shaft's headroom; a stretcher fits.", "mrl", 1000, 13, 1.1, 2.1, 2.2, 0.9, 2.1, "side", 2, 1.0),
        S("panoramic", "Panoramic glass", "Special", "4–13 persons: a glass car in a glass shaft, a centre door.", "mrl", 630, 8, 1.1, 1.4, 2.2, 0.8, 2.0, "centre", 2, 1.0, glass: true, shaftGlass: true),
        S("home", "Home lift", "Home", "2–4 persons, 0.15 m/s (EN 81-41), a shallow pit, no machine room.", "home", 250, 3, 0.8, 0.95, 2.0, 0.7, 2.0, "side", 2, 0.15),
    ];

    public static ElevatorStyle? Find(string? id) => All.FirstOrDefault(s => s.Id == id);
}
