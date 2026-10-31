using Lapin.Alignments;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Ifc;

/// <summary>
/// What an export writes: the schema (IFC4, read everywhere; IFC4X3 for the
/// terrain and the platforms as 4.3 names them), the detail ("group": an
/// element with parts of its own, zones and lintels, roof planes and timber,
/// is a parent over elements of their own; "simple": one body per element),
/// a phase (what exists at its end; null: the whole project) and how bodies
/// are written: "brep" (a faceted B-rep, a face entity per face, as always) or
/// "faceset" (the same faces as one IfcTriangulatedFaceSet or
/// IfcPolygonalFaceSet over a shared point list, a much smaller file; coplanar
/// triangles merged into one face), and a tolerance (metres; 0: exact) within
/// which a body's triangles are simplified (<see cref="Simplifier"/>).
/// </summary>
/// <summary><paramref name="Exclude"/>: elements left out (the kinds and storeys not chosen, what the 3D view hides: <see cref="ModelSurvey.Excluded"/>).</summary>
public sealed record IfcExportOptions(string Schema = "IFC4", string Detail = "group", string? Phase = null, string Geometry = "brep", double Tolerance = 0, IReadOnlySet<string>? Exclude = null)
{
    public bool Ifc43 => Schema.StartsWith("IFC4X3", StringComparison.OrdinalIgnoreCase);
    public bool Grouped => !string.Equals(Detail, "simple", StringComparison.OrdinalIgnoreCase);
    public bool FaceSet => string.Equals(Geometry, "faceset", StringComparison.OrdinalIgnoreCase);
    public string SchemaName => Ifc43 ? "IFC4X3_ADD2" : "IFC4";
}

/// <summary>What IfcStairFlight says of itself: NumberOfRisers, NumberOfTreads, RiserHeight, TreadLength.</summary>
public sealed record StairFlightNumbers(int? Risers, int? Treads, double? RiserHeight, double? TreadLength);

public sealed record IfcExportResult(string Path, string Schema, int Elements, int Parts, int Storeys, IReadOnlyList<string> Notes)
{
    public JsonObject ToJson() => new()
    {
        ["path"] = Path, ["schema"] = Schema, ["elements"] = Elements, ["parts"] = Parts, ["storeys"] = Storeys,
        ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
    };
}

/// <summary>
/// A session written as IFC (docs/spec-ifc.md): project, site, building and
/// storeys; every element that has a body, placed relative to its storey, as
/// faceted B-rep in its colours, with its type, its material (layer set and
/// usage for layered elements), the IFC common property set it can answer,
/// the Lapin intent set (its attributes, so Lapin and BubbleBIM rebuild the
/// element), its quantities with their IFC names and its phases. Openings are
/// written as IfcOpeningElement with their body over the uncut host, as IFC
/// asks; doors and windows fill them. GlobalIds are the model's; what the
/// export derives (parts, relationships, property sets) gets ids derived from
/// its parent's id and a key, the same at every export.
/// </summary>
public static class IfcExport
{
    // Lapin's own sets: no "Pset_" prefix, which IFC keeps for the sets its specification declares (IfcPropertySet).
    public const string IntentSetName = "Lapin_Intent";
    public const string PhaseSetName = "Lapin_Phase";
    public const string ParamsSetName = "Lapin_Parameters";
    /// <summary>The intent set's name until 25.10.2026: files written before still carry it.</summary>
    public const string OldIntentSetName = "Pset_Lapin_Intent";
    public static bool IsIntentSet(string? name) => name is IntentSetName or OldIntentSetName;
    // The keys the sets' GlobalIds derive from stay the old names: a file exported again keeps its sets' ids.
    /// <summary>The key a type's mirror image (doors and windows flipped on one axis) is derived from.</summary>
    const string MirrorKey = "#mirrored";
    const string IntentKey = "Pset_Lapin_Intent", PhaseKey = "Pset_Lapin_Phase", ParamsKey = "Pset_Lapin_Parameters";
    public const string Version = "1";

    public static IfcExportResult Write(Session session, string path, IfcExportOptions? options = null)
    {
        // Built in memory first: a failed export leaves no file behind.
        using var ms = new MemoryStream();
        var result = Write(session, ms, System.IO.Path.GetFileName(path), options);
        File.WriteAllBytes(path, ms.ToArray());
        return result with { Path = path };
    }

    /// <summary>The project as IFC into a stream, <paramref name="name"/> in its header (a browser hands the bytes to the user).</summary>
    public static IfcExportResult Write(Session session, Stream output, string name, IfcExportOptions? options = null)
    {
        var w = new Writer(session, options ?? new IfcExportOptions());
        w.Run();
        w.File.Write(output, name, w.O.Ifc43 ? "ViewDefinition [ReferenceView]" : "ViewDefinition [ReferenceView_V1.2]", w.O.SchemaName);
        return new IfcExportResult(name, w.O.SchemaName, w.Elements, w.PartCount, w.StoreyCount, w.Notes);
    }

    /// <summary>An IFC class and what the element is as one: its predefined type (empty: the class has none) and, user-defined, the object type that names it.</summary>
    sealed record Kind(string Class, string Predefined, string? ObjectType);

    /// <summary>The predefined types IFC4 knows per class (USERDEFINED and NOTDEFINED always); null: the class has no predefined type.</summary>
    static readonly Dictionary<string, string[]?> Classes = new()
    {
        [IfcClass.Wall] = ["MOVABLE", "PARAPET", "PARTITIONING", "PLUMBINGWALL", "SHEAR", "SOLIDWALL", "STANDARD", "POLYGONAL", "ELEMENTEDWALL"],
        [IfcClass.Slab] = ["FLOOR", "ROOF", "LANDING", "BASESLAB"],
        [IfcClass.Roof] = ["FLAT_ROOF", "SHED_ROOF", "GABLE_ROOF", "HIP_ROOF", "HIPPED_GABLE_ROOF", "GAMBREL_ROOF", "MANSARD_ROOF", "BARREL_ROOF", "RAINBOW_ROOF", "BUTTERFLY_ROOF", "PAVILION_ROOF", "DOME_ROOF", "FREEFORM"],
        [IfcClass.Column] = ["COLUMN", "PILASTER"],
        [IfcClass.Beam] = ["BEAM", "JOIST", "HOLLOWCORE", "LINTEL", "SPANDREL", "T_BEAM"],
        [IfcClass.Footing] = ["CAISSON_FOUNDATION", "FOOTING_BEAM", "PAD_FOOTING", "PILE_CAP", "STRIP_FOOTING"],
        [IfcClass.Door] = ["DOOR", "GATE", "TRAPDOOR"],
        [IfcClass.Window] = ["WINDOW", "SKYLIGHT", "LIGHTDOME"],
        ["IfcMember"] = ["BRACE", "CHORD", "COLLAR", "MEMBER", "MULLION", "PLATE", "POST", "PURLIN", "RAFTER", "STRINGER", "STRUT", "STUD"],
        ["IfcPlate"] = ["CURTAIN_PANEL", "SHEET"],
        ["IfcCurtainWall"] = [],
        ["IfcCovering"] = ["CEILING", "FLOORING", "CLADDING", "ROOFING", "MOLDING", "SKIRTINGBOARD", "INSULATION", "MEMBRANE", "SLEEVING", "WRAPPING"],
        ["IfcRailing"] = ["HANDRAIL", "GUARDRAIL", "BALUSTRADE"],
        ["IfcStair"] = ["STRAIGHT_RUN_STAIR", "TWO_STRAIGHT_RUN_STAIR", "QUARTER_WINDING_STAIR", "QUARTER_TURN_STAIR", "HALF_WINDING_STAIR", "HALF_TURN_STAIR", "TWO_QUARTER_WINDING_STAIR", "TWO_QUARTER_TURN_STAIR", "THREE_QUARTER_WINDING_STAIR", "THREE_QUARTER_TURN_STAIR", "SPIRAL_STAIR", "DOUBLE_RETURN_STAIR", "CURVED_RUN_STAIR", "TWO_CURVED_RUN_STAIR"],
        [IfcClass.StairFlight] = ["STRAIGHT", "WINDER", "SPIRAL", "CURVED", "FREEFORM"],
        ["IfcRamp"] = ["STRAIGHT_RUN_RAMP", "TWO_STRAIGHT_RUN_RAMP", "QUARTER_TURN_RAMP", "TWO_QUARTER_TURN_RAMP", "HALF_TURN_RAMP", "SPIRAL_RAMP"],
        ["IfcChimney"] = [],
        ["IfcFurnishingElement"] = null,
        [IfcClass.BuildingElementProxy] = ["COMPLEX", "ELEMENT", "PARTIAL", "PROVISIONFORVOID", "PROVISIONFORSPACE"],
        [IfcClass.GeographicElement] = ["TERRAIN"],
        [IfcClass.Pavement] = ["FLEXIBLE", "RIGID"],
        [IfcClass.EarthworksCut] = ["BASE_EXCAVATION", "CUT", "DREDGING", "EXCAVATION", "OVEREXCAVATION", "PAVEMENTMILLING", "STEPEXCAVATION", "TOPSOILREMOVAL", "TRENCH"],
        ["IfcBuildingElementPart"] = ["INSULATION", "PRECASTPANEL"],
        ["IfcPipeSegment"] = ["CULVERT", "FLEXIBLESEGMENT", "RIGIDSEGMENT", "GUTTER", "SPOOL"],
        ["IfcDiscreteAccessory"] = ["ANCHORPLATE", "BRACKET", "SHOE"],
        // Formwork and scaffolding (the site works module): assemblies of members, plates and accessories.
        ["IfcElementAssembly"] = ["ACCESSORY_ASSEMBLY", "ARCH", "BEAM_GRID", "BRACED_FRAME", "GIRDER", "REINFORCEMENT_UNIT", "RIGID_FRAME", "SLAB_FIELD", "TRUSS"],
        // The distribution elements (the MEP module's nodes and runs), IFC4's enumerations.
        ["IfcDuctSegment"] = ["RIGIDSEGMENT", "FLEXIBLESEGMENT"],
        ["IfcCableSegment"] = ["BUSBARSEGMENT", "CABLESEGMENT", "CONDUCTORSEGMENT", "CORESEGMENT"],
        ["IfcPipeFitting"] = ["BEND", "CONNECTOR", "ENTRY", "EXIT", "JUNCTION", "OBSTRUCTION", "TRANSITION"],
        ["IfcDuctFitting"] = ["BEND", "CONNECTOR", "ENTRY", "EXIT", "JUNCTION", "OBSTRUCTION", "TRANSITION"],
        ["IfcBoiler"] = ["WATER", "STEAM"],
        ["IfcSpaceHeater"] = ["CONVECTOR", "RADIATOR"],
        ["IfcPump"] = ["CIRCULATOR", "ENDSUCTION", "SPLITCASE", "SUBMERSIBLEPUMP", "SUMPPUMP", "VERTICALINLINE", "VERTICALTURBINE"],
        ["IfcValve"] = ["AIRRELEASE", "ANTIVACUUM", "CHANGEOVER", "CHECK", "COMMISSIONING", "DIVERTING", "DRAWOFFCOCK", "DOUBLECHECK", "DOUBLEREGULATING", "FAUCET", "FLUSHING", "GASCOCK", "GASTAP", "ISOLATING", "MIXING", "PRESSUREREDUCING", "PRESSURERELIEF", "REGULATING", "SAFETYCUTOFF", "STEAMTRAP", "STOPCOCK"],
        ["IfcFlowMeter"] = ["ENERGYMETER", "GASMETER", "OILMETER", "WATERMETER"],
        ["IfcTank"] = ["BASIN", "BREAKPRESSURE", "EXPANSION", "FEEDANDEXPANSION", "PRESSUREVESSEL", "STORAGE", "VESSEL"],
        ["IfcSanitaryTerminal"] = ["BATH", "BIDET", "CISTERN", "SHOWER", "SINK", "SANITARYFOUNTAIN", "TOILETPAN", "URINAL", "WASHHANDBASIN", "WCSEAT"],
        ["IfcWasteTerminal"] = ["FLOORTRAP", "FLOORWASTE", "GULLYSUMP", "GULLYTRAP", "ROOFDRAIN", "WASTEDISPOSALUNIT", "WASTETRAP"],
        ["IfcFan"] = ["CENTRIFUGALFORWARDCURVED", "CENTRIFUGALRADIAL", "CENTRIFUGALBACKWARDINCLINEDCURVED", "CENTRIFUGALAIRFOIL", "TUBEAXIAL", "VANEAXIAL", "PROPELLORAXIAL"],
        ["IfcAirTerminal"] = ["DIFFUSER", "GRILLE", "LOUVRE", "REGISTER"],
        ["IfcElectricDistributionBoard"] = ["CONSUMERUNIT", "DISTRIBUTIONBOARD", "MOTORCONTROLCENTRE", "SWITCHBOARD"],
        ["IfcOutlet"] = ["AUDIOVISUALOUTLET", "COMMUNICATIONSOUTLET", "POWEROUTLET", "DATAOUTLET", "TELEPHONEOUTLET"],
        ["IfcLightFixture"] = ["POINTSOURCE", "DIRECTIONSOURCE", "SECURITYLIGHTING"],
        ["IfcSwitchingDevice"] = ["CONTACTOR", "DIMMERSWITCH", "EMERGENCYSTOP", "KEYPAD", "MOMENTARYSWITCH", "SELECTORSWITCH", "STARTER", "SWITCHDISCONNECTOR", "TOGGLESWITCH"],
    };

    /// <summary>Whether IFC4 gives the class (upper case, as a file writes it) a PredefinedType after the eight attributes every element has.</summary>
    internal static bool HasPredefined(string upper) => UpperClasses.TryGetValue(upper, out var allowed) && allowed != null;
    static readonly Dictionary<string, string[]?> UpperClasses = Classes.ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => kv.Value);
    /// <summary>What IFC 4.3 adds: the railway's classes, and predefined types IFC4 lacks (a platform slab, a track panel).</summary>
    static readonly Dictionary<string, string[]?> Classes43 = new()
    {
        [IfcClass.Slab] = ["FLOOR", "ROOF", "LANDING", "BASESLAB", "APPROACH_SLAB", "PAVING", "WEARING", "SIDEWALK", "TRACKSLAB"],
        ["IfcElementAssembly"] = ["ACCESSORY_ASSEMBLY", "ARCH", "BEAM_GRID", "BRACED_FRAME", "GIRDER", "REINFORCEMENT_UNIT", "RIGID_FRAME", "SLAB_FIELD", "TRUSS", "ABUTMENT", "PIER", "PYLON", "CROSS_BRACING", "DECK", "MAST", "SIGNALASSEMBLY", "GRID", "SHELTER", "SUPPORTINGASSEMBLY", "SUSPENSIONASSEMBLY", "TRACTION_SWITCHING_ASSEMBLY", "TRACKPANEL", "TURNOUTPANEL", "DILATATIONPANEL", "RAIL_MECHANICAL_EQUIPMENT_ASSEMBLY", "ENTRANCEWORKS", "SUMPBUSTER", "TRAFFIC_CALMING_DEVICE"],
        ["IfcRail"] = ["RACKRAIL", "BLADE", "GUARDRAIL", "STOCKRAIL", "CHECKRAIL", "RAIL"],
        ["IfcTrackElement"] = ["TRACKENDOFALIGNMENT", "BLOCKINGDEVICE", "VEHICLESTOP", "SLEEPER", "HALF_SET_OF_BLADES", "SPEEDREGULATOR", "DERAILER", "FROG"],
        ["IfcCourse"] = ["ARMOUR", "FILTER", "BALLASTBED", "CORE", "PAVEMENT", "PROTECTION"],
        ["IfcSignal"] = ["VISUAL", "AUDIO", "MIXED"],
    };

    /// <summary>The element assembly in IFC4: its own enumeration, without the railway's panels.</summary>
    static readonly string[] AssemblyTypes4 = ["ACCESSORY_ASSEMBLY", "ARCH", "BEAM_GRID", "BRACED_FRAME", "GIRDER", "REINFORCEMENT_UNIT", "RIGID_FRAME", "SLAB_FIELD", "TRUSS"];

    /// <summary>The classes with a Pset_…Common the export can answer.</summary>
    static readonly HashSet<string> CommonSets =
    [
        IfcClass.Wall, IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Footing, IfcClass.Door, IfcClass.Window,
        "IfcMember", "IfcPlate", "IfcCovering", "IfcRailing", "IfcStair", "IfcRamp", "IfcChimney", IfcClass.BuildingElementProxy,
    ];

    sealed class Writer(Session s, IfcExportOptions o)
    {
        // The application that wrote it: the product (Lapin, or an edition under its own name).
        public readonly Step File = new() { Application = s.Product.Name };
        public IfcExportOptions O => o;
        public int Elements, PartCount, StoreyCount;
        public readonly List<string> Notes = [];

        readonly IModelView m = s.Model;
        Entity project = null!;
        string model = "", body = "", axis = "", footprint = "", metre = "", siteRef = "", sitePlace = "";
        readonly Dictionary<string, string> storeyRef = [], storeyPlace = [], refIdOf = [];
        readonly Dictionary<string, (string Place, double Z0)> placeOf = [];
        string buildingRef = "", projectRef = "";
        readonly Dictionary<string, double> storeyZ = [];
        readonly Dictionary<string, List<string>> contained = [];
        /// <summary>The rooms of each storey (by the storey's IFC entity), aggregated into it.</summary>
        readonly Dictionary<string, List<string>> spaces = [];
        readonly Dictionary<string, string> styles = [], materials = [], layerSets = [], written = [];
        readonly Dictionary<string, (string Ref, List<string> Occurrences)> types = [];
        readonly Dictionary<string, List<string>> materialUses = [];
        readonly Dictionary<(string Element, string Key), string> partRefs = [];
        readonly List<Relation> pendingFills = [];
        readonly List<(Relation Rel, string Host, string Void)> pendingVoids = [];

        Step f => File;
        IfcCarry.Writer? carry;
        /// <summary>What foreign elements carry from their file (<see cref="IfcSource"/>), written back as it came.</summary>
        IfcCarry.Writer Carry => carry ??= new IfcCarry.Writer(File);

        public void Run()
        {
            project = Spatial.Project(m) ?? throw new CommandException("The model has no project.");
            WriteSpatial();
            foreach (var mat in m.OfClass(IfcClass.Material)) Material(mat.Id);
            Grids();
            Railway();
            // The alignments before the elements: an element of the railway is placed along its alignment's curve (IfcLinearPlacement).
            Alignments();
            WriteElements();
            Assemblies();
            Network();
            carry?.Finish(buildingRef);
            Finish();
        }

        // ─── Ids and small helpers ──────────────────────────────────────────

        /// <summary>The model's id as the GlobalId; one that is not a GlobalId (an old project) is mapped to one, the same every time.</summary>
        static string Gid(string id) => S(GlobalId.IsValid(id) ? id : Guid("lapin", id));
        static string Gid(string parent, string key) => S(Guid(parent, key));
        static string GlobalIdOf(string id) => GlobalId.IsValid(id) ? id : Guid("lapin", id);

        string Place(string? relTo, double z) =>
            f.Add("IFCLOCALPLACEMENT", relTo ?? Null, f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", L(new[] { R(0), R(0), R(z) })), Null, Null));

        string P2(Vec2 p) => f.Add("IFCCARTESIANPOINT", L(new[] { R(p.X), R(p.Y) }));

        static Vec2? Pt(JsonNode? n) =>
            n is JsonArray { Count: >= 2 } a && Json.Number(a[0]) is { } x && Json.Number(a[1]) is { } y ? new Vec2(x, y) : null;

        static string Opt(double? v) => v is { } x && x > 0 ? R(x) : Null;

        void Contain(string spatial, string element) => (contained.TryGetValue(spatial, out var l) ? l : contained[spatial] = []).Add(element);

        string Single(string name, string value, string? description = null) => f.Add("IFCPROPERTYSINGLEVALUE", S(name), S(description), value, Null);

        void Define(string entity, string definition, string gid) => f.Add("IFCRELDEFINESBYPROPERTIES", gid, Null, Null, Null, L(new[] { entity }), definition);

        string Style(string hex)
        {
            if (styles.TryGetValue(hex, out var st)) return st;
            var (r, g, b) = Rgb(hex);
            var colour = f.Add("IFCCOLOURRGB", Null, R(r), R(g), R(b));
            return styles[hex] = f.Add("IFCSURFACESTYLE", S(hex), E("BOTH"), L(new[] { f.Add("IFCSURFACESTYLESHADING", colour, R(0)) }));
        }

        static (double R, double G, double B) Rgb(string hex)
        {
            var h = hex.TrimStart('#');
            if (h.Length != 6 || !int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v)) return (0.75, 0.75, 0.75);
            return (((v >> 16) & 255) / 255.0, ((v >> 8) & 255) / 255.0, (v & 255) / 255.0);
        }

        /// <summary>A solid as a faceted B-rep, its vertices relative to the placement's height, in its colour.</summary>
        /// <summary>
        /// The frame a body is written in: the storey's (its level under the world,
        /// axes along it) or, for an element placed along an alignment, the
        /// alignment's point there heading along it. World → local: moved to the
        /// origin, turned back by the heading.
        /// </summary>
        readonly record struct Frame(Vec3 Origin, double Heading)
        {
            public static Frame AtZ(double z0) => new(new Vec3(0, 0, z0), 0);
            public Vec3 Local(Vec3 p) => Turn(p - Origin);
            public Vec3 Turn(Vec3 v)
            {
                if (Heading == 0) return v;
                var (c, s) = (Math.Cos(Heading), Math.Sin(Heading));
                return new Vec3(v.X * c + v.Y * s, -v.X * s + v.Y * c, v.Z);
            }
        }

        string Brep(Solid solid, string? hex, double z0) => Brep(solid, hex, v => new Vec3(v.X, v.Y, v.Z - z0));
        string Brep(Solid solid, string? hex, Frame fr) => Brep(solid, hex, fr.Local);

        /// <summary>
        /// A solid as the export writes bodies (<see cref="IfcExportOptions.Geometry"/>), simplified within the tolerance asked,
        /// its vertices taken into the placement's frame by <paramref name="local"/>, in its colour.
        /// </summary>
        string Brep(Solid solid, string? hex, Func<Vec3, Vec3> local)
        {
            // A body of few faces (what Lapin builds) is written as it is; a mesh's many triangles are merged where they are
            // coplanar (for a face set, which loses nothing by it) and simplified when a tolerance is asked.
            if (solid.Faces.Length > SimplifyFrom && (o.Tolerance > 0 || o.FaceSet))
                solid = simplified.TryGetValue(solid, out var ready) ? ready : Simplifier.Simplify(solid, Math.Max(0, o.Tolerance));
            var item = o.FaceSet ? FaceSet(solid, local) : FacetedBrep(solid, local);
            if (hex != null) f.Add("IFCSTYLEDITEM", item, L(new[] { Style(hex) }), Null);
            return item;
        }

        /// <summary>How many faces a body has before it is worth merging or simplifying.</summary>
        const int SimplifyFrom = 24;

        /// <summary>
        /// The faces over one point list: an IfcTriangulatedFaceSet when they are all triangles, else an
        /// IfcPolygonalFaceSet (a face with holes an IfcIndexedPolygonalFaceWithVoids); indices from 1.
        /// </summary>
        string FaceSet(Solid solid, Func<Vec3, Vec3> local)
        {
            var faces = solid.Faces.Where(x => x.Outer.Length >= 3).ToList();
            var used = new int[solid.Vertices.Length];
            var points = new List<string>();
            int Index(int i) { if (used[i] == 0) { var v = local(solid.Vertices[i]); points.Add(L(new[] { R(v.X), R(v.Y), R(v.Z) })); used[i] = points.Count; } return used[i]; }
            string Ring(int[] ring) => L(ring.Select(i => I(Index(i))));
            if (faces.All(x => x.Outer.Length == 3 && (x.Holes?.Length ?? 0) == 0))
            {
                var tris = faces.Select(x => Ring(x.Outer)).ToList();
                return f.Add("IFCTRIANGULATEDFACESET", f.Add("IFCCARTESIANPOINTLIST3D", L(points), Null), Null, Null, L(tris), Null);
            }
            var items = faces.Select(x => (x.Holes ?? []).Where(h => h.Length >= 3).ToList() is { Count: > 0 } holes
                ? (Outer: Ring(x.Outer), Holes: L(holes.Select(Ring)))
                : (Outer: Ring(x.Outer), Holes: (string?)null)).ToList();
            var refs = items.Select(x => x.Holes != null ? f.Add("IFCINDEXEDPOLYGONALFACEWITHVOIDS", x.Outer, x.Holes) : f.Add("IFCINDEXEDPOLYGONALFACE", x.Outer)).ToList();
            return f.Add("IFCPOLYGONALFACESET", f.Add("IFCCARTESIANPOINTLIST3D", L(points), Null), Null, L(refs), Null);
        }

        /// <summary>A solid as a faceted B-rep: a face, a bound and a loop for each of its faces.</summary>
        string FacetedBrep(Solid solid, Func<Vec3, Vec3> local)
        {
            var pts = solid.Vertices.Select(local).Select(v => f.Add("IFCCARTESIANPOINT", L(new[] { R(v.X), R(v.Y), R(v.Z) }))).ToArray();
            string Loop(int[] ring) => f.Add("IFCPOLYLOOP", L(ring.Select(i => pts[i])));
            var faces = solid.Faces.Where(x => x.Outer.Length >= 3).Select(face => f.Add("IFCFACE", L(
                new[] { f.Add("IFCFACEOUTERBOUND", Loop(face.Outer), True) }
                    .Concat((face.Holes ?? []).Where(h => h.Length >= 3).Select(h => f.Add("IFCFACEBOUND", Loop(h), True)))))).ToList();
            return f.Add("IFCFACETEDBREP", f.Add("IFCCLOSEDSHELL", L(faces)));
        }

        /// <summary>A door's or window's placement: its origin and axes (world) under the storey's, which stands at <paramref name="z0"/>.</summary>
        string JoineryPlace(string? relTo, IfcJoineryPlace at, double z0) =>
            f.Add("IFCLOCALPLACEMENT", relTo ?? Null, f.Add("IFCAXIS2PLACEMENT3D",
                f.Add("IFCCARTESIANPOINT", L(new[] { R(at.Origin.X), R(at.Origin.Y), R(at.Origin.Z - z0) })),
                f.Add("IFCDIRECTION", L(new[] { R(0), R(0), R(1) })),
                f.Add("IFCDIRECTION", L(new[] { R(at.X.X), R(at.X.Y), R(at.X.Z) }))));

        /// <summary>A world point in a door's or window's frame.</summary>
        static Vec3 Local(IfcJoineryPlace at, Vec3 v)
        {
            var d = v - at.Origin;
            return new Vec3(Vec3.Dot(d, at.X), Vec3.Dot(d, at.Y), d.Z);
        }

        /// <summary>
        /// A door or window type's predefined property sets: one IfcDoorPanelProperties or
        /// IfcWindowPanelProperties per panel (how it opens, where it is) and the lining's.
        /// </summary>
        IEnumerable<string> JoinerySets(string key, IfcJoineryType t)
        {
            static string Ratio(double v) => v > 0 && v <= 1 ? R(v) : Null;
            static string Length(double v) => v > 0 ? R(v) : Null;
            var i = 0;
            foreach (var p in t.Panels)
            {
                var gid = Gid(key, $"panel/{i}");
                var name = S($"Panel {++i}");
                yield return t.Door
                    ? f.Add("IFCDOORPANELPROPERTIES", gid, Null, name, Null, Null, E(p.Operation), p.Width is { } w ? Ratio(w) : Null, E(p.Position), Null)
                    : f.Add("IFCWINDOWPANELPROPERTIES", gid, Null, name, Null, E(p.Operation), E(p.Position), Length(t.Lining.Depth), Length(t.Lining.Thickness), Null);
            }
            var l = t.Lining;
            string Offset(IReadOnlyList<double> v, int n) => v.Count > n ? Ratio(v[n]) : Null;
            yield return t.Door
                ? f.Add("IFCDOORLININGPROPERTIES", Gid(key, "lining"), Null, S("Lining"), Null, Length(l.Depth), Length(l.Thickness), l.Threshold > 0 ? Length(l.Depth) : Null, Length(l.Threshold),
                    l.TransomOffsets.Count > 0 ? Length(l.Transom) : Null, Offset(l.TransomOffsets, 0), Null, Null, Null, Null, Null, Null, Null)
                : f.Add("IFCWINDOWLININGPROPERTIES", Gid(key, "lining"), Null, S("Lining"), Null, Length(l.Depth), Length(l.Thickness),
                    l.TransomOffsets.Count > 0 ? Length(l.Transom) : Null, l.MullionOffsets.Count > 0 ? Length(l.Mullion) : Null,
                    Offset(l.TransomOffsets, 0), Offset(l.TransomOffsets, 1), Offset(l.MullionOffsets, 0), Offset(l.MullionOffsets, 1), Null, Null, Null, Null);
        }

        readonly Dictionary<string, string?> maps = [];

        /// <summary>
        /// An instance's body as IFC shares one (docs/plan-instancing.md): its shared body written once as an
        /// IfcRepresentationMap, and here an IfcMappedItem placing it (relative to the storey, at <paramref name="z0"/>).
        /// Null when the placement is not a rotation and a uniform scale, which IFC's operator cannot say: then its own B-rep.
        /// Square within a millionth: what a file wrote its axes to (BIMCamel: 11 decimals) and an instance keeps of them.
        /// </summary>
        string? Mapped(SharedInstance inst, double z0)
        {
            var x = inst.Xf;
            Vec3 ax = new(x[0], x[1], x[2]), ay = new(x[3], x[4], x[5]), az = new(x[6], x[7], x[8]);
            var scale = ax.Length;
            const double tol = 1e-6;
            if (scale < 1e-12 || Math.Abs(ay.Length - scale) > tol * scale || Math.Abs(az.Length - scale) > tol * scale
                || Math.Abs(Vec3.Dot(ax, ay)) > tol * scale * scale || Math.Abs(Vec3.Dot(ay, az)) > tol * scale * scale || Math.Abs(Vec3.Dot(ax, az)) > tol * scale * scale
                || Vec3.Dot(Vec3.Cross(ax, ay), az) <= 0) return null;
            // Written square: x as it is, y made square to it, z their cross product.
            var ux = ax * (1 / scale);
            var uy = ay - ux * Vec3.Dot(ay, ux);
            uy = uy * (1 / uy.Length);
            var uz = Vec3.Cross(ux, uy);
            if (!maps.TryGetValue(inst.Body, out var map))
            {
                map = m.Get(inst.Body) is { } shared && StoredBodies.SharedParts(shared) is { Count: > 0 } parts
                    ? f.Add("IFCREPRESENTATIONMAP", f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null),
                        BodyOf(parts.Select(p => Brep(p.Solid, p.Color, 0))))
                    : null;
                maps[inst.Body] = map;
            }
            if (map == null) return null;
            string Dir(Vec3 n) => f.Add("IFCDIRECTION", L(new[] { R(n.X), R(n.Y), R(n.Z) }));
            var op = f.Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", Dir(ux), Dir(uy), f.Add("IFCCARTESIANPOINT", L(new[] { R(x[9]), R(x[10]), R(x[11] - z0) })),
                Math.Abs(scale - 1) < 1e-12 ? Null : R(scale), Dir(uz));
            return f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("MappedRepresentation"), L(new[] { f.Add("IFCMAPPEDITEM", map, op) }));
        }

        string BodyOf(IEnumerable<string> breps) => f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S(o.FaceSet ? "Tessellation" : "Brep"), L(breps));

        /// <summary>
        /// A part of many copies of one solid (a track's sleepers): the solid
        /// once, as a representation map at its own origin, and each copy as a
        /// mapped item — its axes and origin (relative to the placement's height).
        /// </summary>
        string Mapped(Solid unit, IReadOnlyList<PartInstance> copies, string? hex, Frame fr)
        {
            var source = f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("Brep"), L(new[] { Brep(unit, hex, Frame.AtZ(0)) }));
            var map = f.Add("IFCREPRESENTATIONMAP", f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null), source);
            string D(Vec3 v) => f.Add("IFCDIRECTION", L(new[] { R(v.X), R(v.Y), R(v.Z) }));
            var items = copies.Select(c => { var o = fr.Local(c.Origin); return f.Add("IFCMAPPEDITEM", map,
                f.Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", D(fr.Turn(c.X)), D(fr.Turn(c.Y)), f.Add("IFCCARTESIANPOINT", L(new[] { R(o.X), R(o.Y), R(o.Z) })), R(1), D(fr.Turn(c.Z)))); }).ToList();
            return f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("MappedRepresentation"), L(items));
        }

        // ─── Contexts, units, the spatial tree ──────────────────────────────

        void WriteSpatial()
        {
            var origin = f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
            var wcs = f.Add("IFCAXIS2PLACEMENT3D", origin, Null, Null);
            // True north: the project's +Y turned by the base point's angle, counter-clockwise.
            var north = (Location.Base(m).NorthAngle ?? 0) * Math.PI / 180;
            var trueNorth = f.Add("IFCDIRECTION", L(new[] { R(-Math.Sin(north)), R(Math.Cos(north)) }));
            model = f.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", R(1e-5), wcs, trueNorth);
            body = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Body"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("MODEL_VIEW"), Null);
            axis = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Axis"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("GRAPH_VIEW"), Null);
            metre = f.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), Null, E("METRE"));
            var units = f.Add("IFCUNITASSIGNMENT", L(new[]
            {
                metre,
                f.Add("IFCSIUNIT", Derived, E("AREAUNIT"), Null, E("SQUARE_METRE")),
                f.Add("IFCSIUNIT", Derived, E("VOLUMEUNIT"), Null, E("CUBIC_METRE")),
                f.Add("IFCSIUNIT", Derived, E("PLANEANGLEUNIT"), Null, E("RADIAN")),
            }));
            projectRef = f.Add("IFCPROJECT", Gid(project.Id), Null, S(project.Name), Null, Null, S(project.Str("longName")), S(project.Str("phase")), L(new[] { model }), units);
            // What the project itself holds that an import rebuilds: its location and its phases (the drafting standards and views stay in .lapin).
            Define(projectRef, IntentOf(project, Gid(project.Id, IntentKey), null, only: ProjectIntent), Gid(project.Id, "intent-rel"));

            var site = m.OfClass(IfcClass.Site).FirstOrDefault();
            var building = Spatial.Building(m);
            var basePoint = Location.Base(m);
            sitePlace = Place(null, 0);
            var siteId = site?.Id ?? project.Id;
            siteRef = f.Add("IFCSITE", site != null ? Gid(site.Id) : Gid(project.Id, "site"), Null, S(site?.Name ?? "Site"), Null, Null, sitePlace, Null, Null, E("ELEMENT"), Null, Null, R(basePoint.Elevation), Null, Null);
            refIdOf[siteRef] = siteId;
            var buildingPlace = Place(sitePlace, 0);
            var buildingId = building?.Id ?? project.Id;
            buildingRef = f.Add("IFCBUILDING", building != null ? Gid(building.Id) : Gid(project.Id, "building"), Null, S(building?.Name ?? "Building"), Null, Null, buildingPlace, Null, Null, E("ELEMENT"), Null, Null, Null);
            f.Add("IFCRELAGGREGATES", Gid(project.Id, "aggregates"), Null, Null, Null, projectRef, L(new[] { siteRef }));
            f.Add("IFCRELAGGREGATES", Gid(siteId, "aggregates/building"), Null, Null, Null, siteRef, L(new[] { buildingRef }));

            var storeys = Spatial.Storeys(m).ToList();
            foreach (var st in storeys)
            {
                var z = st.Num("elevation");
                var place = Place(buildingPlace, z);
                storeyPlace[st.Id] = place;
                storeyZ[st.Id] = z;
                var r = f.Add("IFCBUILDINGSTOREY", Gid(st.Id), Null, S(st.Name), Null, Null, place, Null, Null, E("ELEMENT"), R(z));
                Define(r, IntentOf(st, Gid(st.Id, IntentKey), null), Gid(st.Id, "intent-rel"));
                storeyRef[st.Id] = r;
                refIdOf[r] = st.Id;
            }
            if (storeys.Count > 0) f.Add("IFCRELAGGREGATES", Gid(buildingId, "aggregates/storeys"), Null, Null, Null, buildingRef, L(storeys.Select(st => storeyRef[st.Id])));
            StoreyCount = storeys.Count;
            MapConversion();
        }

        /// <summary>The survey point ties the project's origin to eastings and northings, the base point's angle to true north; the geometry stays where it is.</summary>
        void MapConversion()
        {
            if (Location.Survey(m) is not { } sv) return;
            var th = (Location.Base(m).NorthAngle ?? 0) * Math.PI / 180;
            // The project's X axis in map coordinates; map(p) = (E0, N0) + (c·x − sn·y, sn·x + c·y).
            var (c, sn) = (Math.Cos(th), -Math.Sin(th));
            var e0 = (sv.Easting ?? 0) - (c * sv.At.X - sn * sv.At.Y);
            var n0 = (sv.Northing ?? 0) - (sn * sv.At.X + c * sv.At.Y);
            // The system by its EPSG name, its datum and projection when Lapin knows it; else the site's own grid.
            var known = GeoRef.Get(Location.Crs(m));
            var crs = known != null
                ? f.Add("IFCPROJECTEDCRS", S(known.Id), S(known.Name), S(known.Datum), Null, S(known.Projection), Null, metre)
                : f.Add("IFCPROJECTEDCRS", S(Location.Crs(m) ?? "Site grid"), Null, Null, Null, Null, Null, metre);
            f.Add("IFCMAPCONVERSION", model, crs, R(e0), R(n0), R(Location.Base(m).Elevation), R(c), R(sn), Null);
        }

        /// <summary>The project's grid on the lowest storey: the axes that run up the plan as U, the others as V.</summary>
        void Grids()
        {
            var storey = Spatial.Storeys(m).FirstOrDefault();
            if (storey == null) return;
            foreach (var g in m.OfClass(IfcClass.Grid))
            {
                var axes = (g.Attrs["axes"] as JsonArray)?.OfType<JsonObject>()
                    .Select(o => (Tag: (string?)o["tag"] ?? "?", A: Pt(o["a"]), B: Pt(o["b"])))
                    .Where(x => x.A != null && x.B != null && (x.B!.Value - x.A!.Value).Length > 1e-9)
                    .Select(x => (x.Tag, A: x.A!.Value, B: x.B!.Value)).ToList() ?? [];
                var u = axes.Where(x => Math.Abs(x.B.Y - x.A.Y) >= Math.Abs(x.B.X - x.A.X)).ToList();
                var v = axes.Where(x => Math.Abs(x.B.Y - x.A.Y) < Math.Abs(x.B.X - x.A.X)).ToList();
                if (u.Count == 0 || v.Count == 0)
                {
                    if (axes.Count > 0) Notes.Add("The grid needs axes in two directions to be written as an IfcGrid.");
                    continue;
                }
                string Axis((string Tag, Vec2 A, Vec2 B) x) => f.Add("IFCGRIDAXIS", S(x.Tag), f.Add("IFCPOLYLINE", L(new[] { P2(x.A), P2(x.B) })), True);
                var rectangular = u.All(x => Math.Abs(x.B.X - x.A.X) < 1e-6) && v.All(x => Math.Abs(x.B.Y - x.A.Y) < 1e-6);
                var grid = f.Add("IFCGRID", Gid(g.Id), Null, S(g.Name), Null, Null, Place(storeyPlace[storey.Id], 0), Null, L(u.Select(Axis)), L(v.Select(Axis)), Null, E(rectangular ? "RECTANGULAR" : "IRREGULAR"));
                Contain(storeyRef[storey.Id], grid);
                Define(grid, IntentOf(g, Gid(g.Id, IntentKey), null), Gid(g.Id, "intent-rel"));
            }
        }

        // ─── Elements ───────────────────────────────────────────────────────

        bool IsElement(Entity e) => (s.Catalog.Geometry(e.Class) != null || StoredBodies.Has(e)) && IsElementClass(e);

        bool IsElementClass(Entity e) =>
            e.Class is not (IfcClass.OpeningElement or IfcClass.Grid or IfcClass.Annotation)
            && (s.Catalog.Class(e.Class)?.Kind ?? "element") == "element";

        /// <summary>Whether the element exists at the end of the phase asked for (as the phases module sees it: created before or in it, not demolished before it).</summary>
        bool InPhase(Entity e)
        {
            if (o.Phase == null) return true;
            var phases = ((project.Attrs["phases"] as JsonObject)?["phases"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            int Index(string? id) => id == null ? -1 : phases.FindIndex(p => (string?)p["id"] == id);
            var v = Index(o.Phase);
            if (v < 0) return true;
            var c = Index(e.Str("phaseCreated"));
            if (c < 0) c = Math.Max(0, phases.FindIndex(p => (string?)p["kind"] == "construction"));
            var d = Index(e.Str("phaseDemolished"));
            return c <= v && (d < 0 || d >= v);
        }

        string PhaseName(string id) =>
            ((project.Attrs["phases"] as JsonObject)?["phases"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(p => (string?)p["id"] == id) is { } p ? (string?)p["name"] ?? id : id;

        void WriteElements()
        {
            var voids = s.Catalog.Get<IVoidShapes>();
            var elements = m.Entities.Where(e => IsElement(e) && InPhase(e) && o.Exclude?.Contains(e.Id) != true).ToList();
            // Bodies to merge or simplify are prepared a batch at a time on every core, then written in order.
            var prepare = o.Tolerance > 0 || o.FaceSet;
            const int batch = 1000;
            for (var from = 0; from < elements.Count; from += batch)
            {
                var some = elements.GetRange(from, Math.Min(batch, elements.Count - from));
                var geos = new ElementGeometry?[some.Count];
                if (prepare)
                {
                    for (var i = 0; i < some.Count; i++)
                        if (some[i].Class != IfcClass.Space)
                            try { geos[i] = s.Geometry(some[i].Id, new GeometryOptions(CutOpenings: false)); }
                            catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { geos[i] = null; }
                    var solids = geos.SelectMany(g => g?.Parts ?? []).Select(p => p.Solid).Where(x => x.Faces.Length > SimplifyFrom).Distinct(ReferenceEqualityComparer.Instance).Cast<Solid>().ToList();
                    Parallel.ForEach(solids, x => simplified[x] = Simplifier.Simplify(x, Math.Max(0, o.Tolerance)));
                }
                for (var i = 0; i < some.Count; i++)
                {
                    var e = some[i];
                    try { Element(e, voids, geos[i]); }
                    catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException)
                    {
                        Notes.Add($"„{e.Name}” was not exported: {ex.Message}");
                    }
                }
                simplified.Clear();
            }
        }

        /// <summary>
        /// Assemblies of separate elements (a Studio box with its bottom and top, docs/plan-studio-thick.md): an
        /// IfcElementAssembly without a body of its own, in the storey, aggregating its parts (IfcRelAggregates); the parts,
        /// written as elements already, are then no longer contained in the storey themselves, as IFC has parts.
        /// </summary>
        void Assemblies()
        {
            foreach (var asm in m.OfClass("IfcElementAssembly").Where(e => !written.ContainsKey(e.Id) && !StoredBodies.Has(e) && InPhase(e)).ToList())
            {
                var parts = m.RelatedOf(asm.Id, IfcClass.RelAggregates).Select(p => written.GetValueOrDefault(p.Id)).OfType<string>().ToList();
                if (parts.Count == 0) continue;
                var storey = Spatial.StoreyOf(m, asm.Id) ?? m.RelatedOf(asm.Id, IfcClass.RelAggregates).Select(p => Spatial.StoreyOf(m, p.Id)).FirstOrDefault(s => s != null);
                var (spatial, relTo) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr) ? (sr, storeyPlace[storey.Id]) : (siteRef, sitePlace);
                var kind = new Kind("IfcElementAssembly", asm.PredefinedType ?? "USERDEFINED", asm.Str("objectType"));
                var entity = Product(kind, Gid(asm.Id), asm.Name, kind.ObjectType, Place(relTo, 0), Null, asm.Id);
                written[asm.Id] = entity;
                foreach (var list in contained.Values) list.RemoveAll(parts.Contains);
                Contain(spatial, entity);
                Elements++;
                f.Add("IFCRELAGGREGATES", Gid(asm.Id, "parts"), Null, Null, Null, entity, L(parts));
            }
        }

        /// <summary>The bodies of the batch being written, merged or simplified ahead (<see cref="WriteElements"/>).</summary>
        readonly System.Collections.Concurrent.ConcurrentDictionary<Solid, Solid> simplified = new(ReferenceEqualityComparer.Instance);

        void Element(Entity e, IVoidShapes? voids, ElementGeometry? prepared = null)
        {
            if (e.Class == IfcClass.Space) { Space(e); return; }
            var geo = prepared ?? s.Geometry(e.Id, new GeometryOptions(CutOpenings: false));
            var parts = geo?.Parts.Where(p => p.Solid.Faces.Length > 0).ToList() ?? [];
            if (parts.Count == 0)
            {
                if (geo?.Diagnostics is { Count: > 0 } d) Notes.Add($"„{e.Name}” has no body and was not exported: {d[0]}");
                return;
            }
            var storey = Spatial.StoreyOf(m, e.Id) ?? HostStorey(e);
            var (spatial, relTo, z0) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr)
                ? (sr, storeyPlace[storey.Id], storeyZ[storey.Id])
                : (siteRef, sitePlace, 0.0);
            // IFC 4.3: a track, a turnout, a platform stand in a railway facility's part, not on a storey (their heights absolute),
            // placed along their alignment's curve where the module says where (IfcLinearPlacement), their body in that frame.
            var railPart = o.Ifc43 ? RailwayPart(e) : null;
            if (railPart is { } part) (spatial, relTo, z0) = (part, sitePlace, 0.0);
            var fr = Frame.AtZ(z0);
            string? place = null;
            if (railPart != null && LinearPlace(e) is { } along) (place, fr) = along;
            var kind = KindOf(e);
            var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            // A wall of layers only is one body with its layer set; one with zones, lintels or a ring beam (a roof with its planes and timber, a footing with its sole) is a parent over its parts.
            // A door's frame and leaf, a column's body, a terrain: one element. Only a wall, a roof and a footing hold parts that are elements of their own.
            var grouped = o.Grouped && kind.Class is IfcClass.Wall or IfcClass.Roof or IfcClass.Footing or "IfcCurtainWall" or IfcClass.Stair or "IfcElementAssembly" && parts.Count > 1 && !parts.All(p => (p.Key ?? "").StartsWith("layer:", StringComparison.Ordinal));
            // A door or window: placed with its local +Y the way its panels open, which IFC's left and right are seen along.
            var joinery = e.Class is IfcClass.Door or IfcClass.Window && !grouped ? s.Catalog.Get<IJoineryIfc>() : null;
            var at = joinery?.Place(m, e);
            // A railway element already has its linear placement and frame; the rest stand on their storey (or in the joinery's frame).
            place ??= at is { } jp ? JoineryPlace(relTo, jp, z0) : Place(relTo, 0);
            var reps = new List<string>();
            // An instance of a shared body: the body once, as a representation map, each instance mapping it where it stands.
            if (!grouped && at == null && railPart == null && geo!.Instance is { } inst && Mapped(inst, z0) is { } mapped) reps.Add(mapped);
            else if (!grouped) reps.Add(BodyOf(parts.Select(p => at is { } jp2 ? Brep(p.Solid, p.Color, v => Local(jp2, v)) : Brep(p.Solid, p.Color, fr))));
            if (AxisOf(e) is { } ax) reps.Add(ax);
            reps.AddRange(Plan2dOf(e));
            var shape = reps.Count > 0 ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(reps)) : Null;
            var (height, width) = Overall(e, type);
            // Its type in IFC's terms (the mirrored type for an element that is its type's mirror image), which the element repeats.
            var mirrored = at?.Mirrored == true;
            var terms = type != null && joinery != null ? joinery.Type(m, type, mirrored) : null;
            // A foreign element: its class and attributes as its file wrote them.
            var src = e.Attrs[IfcSource.Key] as JsonObject;
            var entity = (src != null ? Carried(src, e, place, shape) : null) ?? Product(kind, Gid(e.Id), e.Name, kind.ObjectType ?? type?.Name, place, shape, e.Id, height, width, terms);
            written[e.Id] = entity;
            if (at == null) placeOf[e.Id] = (place, z0);
            Contain(spatial, entity);
            Elements++;
            if (type != null) TypeOf(type, kind, entity, mirrored, terms);
            if (src?["material"] == null) MaterialsOf(e, entity, parts, grouped);
            if (grouped) Parts(e, kind, parts, place, fr, entity);
            Openings(e, entity, place, z0, voids);
            Properties(e, kind, entity, type);
            Quantities(e, entity, grouped);
        }

        /// <summary>
        /// A room as IFC has one: an IfcSpace aggregated into its storey (not
        /// contained), its body its outline extruded up its height (an
        /// IfcExtrudedAreaSolid of an arbitrary profile, its islands as voids),
        /// Name its number and LongName its name (as most tools write them),
        /// ObjectType its function; Pset_SpaceCommon's Reference (its number)
        /// and IsExternal, its Qto_SpaceBaseQuantities, and Lapin's intent.
        /// </summary>
        void Space(Entity e)
        {
            var geo = s.Geometry(e.Id);
            var plan = geo?.Plan ?? [];
            var outer = plan.FirstOrDefault(p => p.Kind == "room")?.Points;
            if (outer is not { Count: >= 3 })
            {
                Notes.Add($"„{e.Name}” has no outline and was not exported.");
                return;
            }
            var islands = plan.Where(p => p.Kind == "roomIsland" && p.Points.Count >= 3).Select(p => p.Points).ToList();
            var storey = Spatial.StoreyOf(m, e.Id);
            var (spatial, relTo, z0) = storey != null && storeyRef.TryGetValue(storey.Id, out var sr) ? (sr, storeyPlace[storey.Id], storeyZ[storey.Id]) : (buildingRef, sitePlace, 0.0);
            var height = geo!.Parts.Select(p => Measure.Bounds(p.Solid)).OfType<(Vec3 Min, Vec3 Max)>().Select(b => b.Max.Z - b.Min.Z).DefaultIfEmpty(storey?.NumOrNull("height") ?? 3).Max();
            string Curve(IReadOnlyList<Vec2> loop) => f.Add("IFCPOLYLINE", L(loop.Append(loop[0]).Select(P2)));
            var profile = islands.Count == 0
                ? f.Add("IFCARBITRARYCLOSEDPROFILEDEF", E("AREA"), Null, Curve(Polygon2.EnsureCcw(outer)))
                : f.Add("IFCARBITRARYPROFILEDEFWITHVOIDS", E("AREA"), Null, Curve(Polygon2.EnsureCcw(outer)), L(islands.Select(i => Curve(Polygon2.EnsureCcw(i).Reverse().ToList()))));
            var origin = f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null);
            var solid = f.Add("IFCEXTRUDEDAREASOLID", profile, origin, f.Add("IFCDIRECTION", "(0.,0.,1.)"), R(height));
            f.Add("IFCSTYLEDITEM", solid, L(new[] { Style(geo.Parts.FirstOrDefault()?.Color ?? "#9ec5e8") }), Null);
            var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("SweptSolid"), L(new[] { solid })) }));
            var place = Place(relTo, e.Num("baseOffset"));
            var number = e.Str("number") is { Length: > 0 } n ? n : null;
            var function = e.Str("function") is { Length: > 0 } fn ? fn : null;
            var entity = f.Add("IFCSPACE", Gid(e.Id), Null, S(number ?? e.Name), Null, S(function), place, shape, S(e.Name), E("ELEMENT"), E("SPACE"), Null);
            written[e.Id] = entity;
            placeOf[e.Id] = (place, z0);
            (spaces.TryGetValue(spatial, out var l) ? l : spaces[spatial] = []).Add(entity);
            Elements++;
            var common = new List<(string Name, string Value)> { ("IsExternal", Typed("IFCBOOLEAN", False)) };
            if (number != null) common.Insert(0, ("Reference", Typed("IFCIDENTIFIER", S(number))));
            Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "common"), Null, S("Pset_SpaceCommon"), Null, L(CommonWith(e, "Pset_SpaceCommon", common))), Gid(e.Id, "common-rel"));
            Define(entity, IntentOf(e, Gid(e.Id, IntentKey), new Kind(IfcClass.Space, "SPACE", null)), Gid(e.Id, "intent-rel"));
            if (ParamsOf(e, Gid(e.Id, ParamsKey)) is { } set) Define(entity, set, Gid(e.Id, "params-rel"));
            NamedSets(e, entity, "Pset_SpaceCommon", new HashSet<string>());
            Quantities(e, entity, false);
        }

        /// <summary>A door or window stands on its wall's storey.</summary>
        Entity? HostStorey(Entity e) =>
            m.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening && m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is { } host ? Spatial.StoreyOf(m, host.Id) : null;

        Kind KindOf(Entity e)
        {
            var cls = e.Str("exportAs") is { Length: > 3 } x && x.StartsWith("Ifc", StringComparison.Ordinal) ? x : e.Class;
            if (cls == IfcClass.EarthworksCut && !o.Ifc43) return new(IfcClass.BuildingElementProxy, "USERDEFINED", e.PredefinedType == "TRENCH" ? "Trench" : "Earthworks cut");
            // IFC 4.3's pavement and vegetation; IFC4 has a slab named Pavement and a user-defined geographic element.
            if (cls == IfcClass.Pavement && !o.Ifc43) return new(IfcClass.Slab, "USERDEFINED", "Pavement");
            if (cls == IfcClass.GeographicElement && e.PredefinedType == "VEGETATION" && o.Ifc43) return new(cls, "VEGETATION", null);
            string[]? allowed;
            if (o.Ifc43 && Classes43.TryGetValue(cls, out var allowed43)) allowed = allowed43;
            else if (cls == "IfcElementAssembly") allowed = AssemblyTypes4;
            else if (!Classes.TryGetValue(cls, out allowed)) return new(IfcClass.BuildingElementProxy, "USERDEFINED", cls);
            if (allowed == null) return new(cls, "", null);
            var pre = e.PredefinedType?.ToUpperInvariant();
            if (pre == null && cls == IfcClass.Roof)
                pre = e.Str("shape") switch { "flat" => "FLAT_ROOF", "gable" => "GABLE_ROOF", "hip" => "HIP_ROOF", "mansard" => "MANSARD_ROOF", "shed" => "SHED_ROOF", "barrel" => "BARREL_ROOF", _ => null };
            if (pre == null && cls == IfcClass.GeographicElement) pre = "TERRAIN";
            if (pre == null) return new(cls, "NOTDEFINED", null);
            // A user-defined type names itself through the element's objectType (a diamond crossing, a level crossing).
            if (pre == "USERDEFINED") return new(cls, pre, e.Str("objectType"));
            if (allowed.Contains(pre) || pre == "NOTDEFINED") return new(cls, pre, null);
            return new(cls, "USERDEFINED", pre);
        }

        /// <summary>A stair flight's numbers as its module counts them (Qto_StairFlightBaseQuantities of that part): risers, treads, riser height, tread length.</summary>
        StairFlightNumbers? FlightOf(Entity e, string key)
        {
            var set = s.Quantities(e.Id).FirstOrDefault(q => q.Name == "Qto_StairFlightBaseQuantities" && q.Part == key);
            if (set == null) return null;
            double? Q(string n) => set.Quantities.FirstOrDefault(x => x.Name == n)?.Value;
            return new StairFlightNumbers((int?)Q("NumberOfRiser"), (int?)Q("NumberOfTreads"), Q("RiserHeight"), Q("TreadLength"));
        }

        /// <summary>
        /// A foreign element as its file wrote it: its class, Description, ObjectType, Tag and what its class adds
        /// (a PredefinedType), with its GlobalId and name as the model has them and its placement and body as
        /// written here; null when nothing of it was kept.
        /// </summary>
        string? Carried(JsonObject src, Entity e, string place, string shape)
        {
            if (IfcCarry.Writer.ClassOf(src) is not var (type, count) || count < 8) return null;
            var args = new string[count];
            for (var i = 0; i < count; i++)
                args[i] = i switch { 0 => Gid(e.Id), 1 => Null, 2 => S(e.Name), 5 => place, 6 => shape, _ => IfcCarry.Writer.ArgOf(src, i) ?? Null };
            return f.Add(type, args);
        }

        /// <summary>The product entity with the attributes its class has: the eight every element shares, then what the class adds.</summary>
        string Product(Kind k, string gid, string? name, string? objectType, string place, string shape, string tag, double? height = null, double? width = null, IfcJoineryType? terms = null, StairFlightNumbers? flight = null)
        {
            string[] common = [gid, Null, S(name), Null, S(objectType), place, shape, S(tag)];
            var up = k.Class.ToUpperInvariant();
            var operation = E(terms?.Operation ?? "NOTDEFINED");
            var userDefined = S(terms?.Operation == "USERDEFINED" ? terms.UserDefined : null);
            return k.Class switch
            {
                IfcClass.Door => f.Add(up, [.. common, Opt(height), Opt(width), E(k.Predefined), operation, userDefined]),
                IfcClass.Window => f.Add(up, [.. common, Opt(height), Opt(width), E(k.Predefined), operation, userDefined]),
                "IfcElementAssembly" => f.Add(up, [.. common, Null, E(k.Predefined)]),
                IfcClass.StairFlight => f.Add(up, [.. common, flight?.Risers is { } nr ? nr.ToString(System.Globalization.CultureInfo.InvariantCulture) : Null, flight?.Treads is { } nt ? nt.ToString(System.Globalization.CultureInfo.InvariantCulture) : Null, Opt(flight?.RiserHeight), Opt(flight?.TreadLength), E(k.Predefined)]),
                _ when k.Predefined.Length == 0 => f.Add(up, common),
                _ => f.Add(up, [.. common, E(k.Predefined)]),
            };
        }

        (double? Height, double? Width) Overall(Entity e, Entity? type)
        {
            if (e.Class is not (IfcClass.Door or IfcClass.Window)) return (null, null);
            var spec = type?.Attrs["spec"] as JsonObject;
            var opening = m.RelatingOf(e.Id, IfcClass.RelFillsElement);
            return (Json.Number(spec?["height"]) ?? opening?.NumOrNull("height"), Json.Number(spec?["width"]) ?? opening?.NumOrNull("width"));
        }

        /// <summary>A path element's axis, start to end, as IFC's Axis representation.</summary>
        string? AxisOf(Entity e)
        {
            if (!s.Catalog.IsPath(e.Class) || Pt(e.Attrs["start"]) is not { } a || Pt(e.Attrs["end"]) is not { } b) return null;
            // An arc as the polyline it is drawn with (steps of at most 5°).
            var c = PathCurve.Of(e);
            var pts = c.IsArc ? c.Line(0, c.Length, 0) : [a, b];
            return f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve2D"), L(new[] { f.Add("IFCPOLYLINE", L(pts.Select(P2).ToArray())) }));
        }

        string? plan2dContext;

        /// <summary>
        /// A reference's own 2D, as the file it came from gave it (<see cref="Plan2d"/>), written back: its footprint lines as a
        /// FootPrint representation, the rest as an Annotation one, in a plan subcontext (colours and patterns are not written).
        /// </summary>
        List<string> Plan2dOf(Entity e)
        {
            var o = new List<string>();
            var items = Plan2d.Read(e);
            if (items.Count == 0) return o;
            plan2dContext ??= f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Annotation"), S("Plan"), Derived, Derived, Derived, Derived, model, Null, E("PLAN_VIEW"), Null);
            string Poly(IReadOnlyList<Vec2> pts, bool closed) => f.Add("IFCPOLYLINE", L((closed ? pts.Append(pts[0]) : pts).Select(P2).ToArray()));
            string? Item(Plan2dItem i) => i.Kind switch
            {
                Plan2dItem.Line when i.Points.Count >= 2 => Poly(i.Points, i.Closed),
                Plan2dItem.Fill => f.Add("IFCANNOTATIONFILLAREA", Poly(i.Loops[0], true), i.Loops.Count > 1 ? L(i.Loops.Skip(1).Select(l => Poly(l, true)).ToArray()) : Null),
                Plan2dItem.Label when i.Text != null => f.Add("IFCTEXTLITERAL", S(i.Text),
                    f.Add("IFCAXIS2PLACEMENT2D", P2(i.Points[0]), f.Add("IFCDIRECTION", L(new[] { R(Math.Cos(i.Angle * Math.PI / 180)), R(Math.Sin(i.Angle * Math.PI / 180)) }))), E("RIGHT")),
                _ => null,
            };
            foreach (var (id, type, footprint) in new[] { ("FootPrint", "GeometricCurveSet", true), ("Annotation", "Annotation2D", false) })
            {
                var written = items.Where(i => i.Footprint == footprint).Select(Item).OfType<string>().ToList();
                if (written.Count == 0) continue;
                var set = f.Add("IFCGEOMETRICCURVESET", L(written));
                o.Add(f.Add("IFCSHAPEREPRESENTATION", plan2dContext, S(id), S(type), L(new[] { set })));
            }
            return o;
        }

        // ─── Types and materials ────────────────────────────────────────────

        void TypeOf(Entity type, Kind kind, string entity, bool mirrored = false, IfcJoineryType? terms = null)
        {
            var key = mirrored ? type.Id + MirrorKey : type.Id;
            if (!types.TryGetValue(key, out var t))
            {
                var typeClass = type.Class.EndsWith("Type", StringComparison.Ordinal) && Classes.ContainsKey(type.Class[..^4]) ? type.Class : kind.Class + "Type";
                var gid = mirrored ? Gid(type.Id, MirrorKey) : Gid(type.Id);
                // The mirrored type carries the type's intent and says whose mirror it is, so Lapin reads it back as the type itself.
                var intent = IntentOf(type, Gid(key, IntentKey), null, mirrored ? [("LapinMirrorOf", GlobalIdOf(type.Id))] : null);
                // The type's parameters, which every element of the type has.
                var sets = new List<string> { intent };
                if (ParamsOf(type, Gid(key, ParamsKey)) is { } ps) sets.Add(ps);
                foreach (var (setName, props) in NamedParams(type))
                    sets.Add(f.Add("IFCPROPERTYSET", Gid(key, "params:" + setName), Null, S(setName), Null, L(props.Select(p => Single(p.Name, p.Value)).ToArray())));
                if (terms != null) sets.AddRange(JoinerySets(key, terms));
                var pre = E(kind.Predefined.Length == 0 ? "NOTDEFINED" : kind.Predefined);
                var name = mirrored ? $"{type.Name} (mirrored)" : type.Name;
                string[] common = [gid, Null, S(name), Null, Null, L(sets), Null, S(type.Id), S(kind.Predefined == "USERDEFINED" ? kind.ObjectType : null)];
                var up = typeClass.ToUpperInvariant();
                var operation = E(terms?.Operation ?? "NOTDEFINED");
                var userDefined = S(terms?.Operation == "USERDEFINED" ? terms.UserDefined : null);
                var r = kind.Class switch
                {
                    IfcClass.Door => f.Add(up, [.. common, pre, operation, False, userDefined]),
                    IfcClass.Window => f.Add(up, [.. common, pre, operation, False, userDefined]),
                    _ => f.Add(up, [.. common, pre]),
                };
                types[key] = t = (r, []);
                // Its material: the layer set of a layered type; the one material a column's or beam's type names.
                if (m.RelatingOf(type.Id, IfcClass.RelAssociatesMaterial) is { Class: IfcClass.MaterialLayerSet } set)
                    f.Add("IFCRELASSOCIATESMATERIAL", Gid(type.Id, "material"), Null, Null, Null, L(new[] { r }), LayerSet(set));
                else if (type.Str("material") is { } mat && m.Get(mat) is { Class: IfcClass.Material })
                    UseMaterial(mat, r);
            }
            t.Occurrences.Add(entity);
        }

        string LayerSet(Entity set)
        {
            if (layerSets.TryGetValue(set.Id, out var r)) return r;
            var layers = Layers.Read(set).Select(l =>
                f.Add("IFCMATERIALLAYER", Material(l.Material), R(l.Thickness), Null, S(l.Name ?? m.Get(l.Material)?.Name), Null, S(l.Core ? "Core" : null), Null)).ToList();
            return layerSets[set.Id] = f.Add("IFCMATERIALLAYERSET", L(layers), S(set.Name), Null);
        }

        /// <summary>A material, with its colour as its representation (what a reader without the styled bodies shows).</summary>
        string Material(string id)
        {
            if (materials.TryGetValue(id, out var r)) return r;
            var mat = m.Get(id);
            r = f.Add("IFCMATERIAL", S(mat?.Name ?? id), Null, S(mat?.Str("category")));
            materials[id] = r;
            // Its Lapin id and attributes, so an import gives the material the same id and what refers to it (a column type, a zone) still finds it.
            if (mat != null) f.Add("IFCMATERIALPROPERTIES", S(IntentSetName), Null, L(IntentProperties(mat, null, [("LapinId", mat.Id)])), r);
            var style = Style(Materials.ColorOf(m, id));
            f.Add("IFCMATERIALDEFINITIONREPRESENTATION", Null, Null,
                L(new[] { f.Add("IFCSTYLEDREPRESENTATION", model, Null, Null, L(new[] { f.Add("IFCSTYLEDITEM", Null, L(new[] { style }), Null) })) }), r);
            return r;
        }

        void UseMaterial(string materialId, string entity) => (materialUses.TryGetValue(materialId, out var l) ? l : materialUses[materialId] = []).Add(entity);

        /// <summary>A layered element's usage of its type's layer set; else the one material of its body (a grouped element's parts carry theirs).</summary>
        void MaterialsOf(Entity e, string entity, List<GeometryPart> parts, bool grouped)
        {
            if (LayerSets.UsageOf(m, e.Id) is { } u && Layers.ReadUsage(u) is { } usage && m.Get(usage.ForLayerSet) is { } set)
            {
                var direction = e.Class is IfcClass.Slab or IfcClass.Roof ? "AXIS3" : "AXIS2";
                var usageRef = f.Add("IFCMATERIALLAYERSETUSAGE", LayerSet(set), E(direction), E(usage.DirectionSense), R(usage.Offset), Null);
                var rel = m.RelationsOf(e.Id, IfcClass.RelAssociatesMaterial).FirstOrDefault(r => r.Relating == u.Id);
                f.Add("IFCRELASSOCIATESMATERIAL", rel != null ? Gid(rel.Id) : Gid(e.Id, "material"), Null, Null, Null, L(new[] { entity }), usageRef);
                return;
            }
            if (grouped) return;
            var mats = parts.Select(p => p.Material).OfType<string>().Distinct().ToList();
            if (mats.Count == 1) UseMaterial(mats[0], entity);
        }

        // ─── Parts, openings ────────────────────────────────────────────────

        void Parts(Entity e, Kind kind, List<GeometryPart> parts, string place, Frame fr, string parent)
        {
            var refs = new List<string>();
            var zones = (m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Attrs["zones"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            for (var i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                var key = p.Key ?? $"part:{i}";
                var pk = PartKind(kind.Class, key, p, zones);
                var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { p.Unit != null && p.Instances is { Count: > 0 } copies ? Mapped(p.Unit, copies, p.Color, fr) : BodyOf([Brep(p.Solid, p.Color, fr)]) }));
                var r = Product(pk, Gid(e.Id, key), p.Name, pk.ObjectType, Place(place, 0), shape, $"{e.Id}/{key}", flight: pk.Class == IfcClass.StairFlight ? FlightOf(e, key) : null);
                if (p.Material != null) UseMaterial(p.Material, r);
                StandardSets(e, p, r, $"{e.Id}/{key}");
                partRefs[(e.Id, key)] = r;
                refs.Add(r);
                PartCount++;
            }
            f.Add("IFCRELAGGREGATES", Gid(e.Id, "parts"), Null, Null, Null, parent, L(refs));
        }

        /// <summary>What a part is in IFC: a lintel or ring beam a beam, a roof plane a roof slab, the timber members, the sole of a strip footing a footing; else a building element part named as Lapin names it.</summary>
        Kind PartKind(string host, string key, GeometryPart p, List<JsonObject> zones)
        {
            var colon = key.IndexOf(':');
            var (prefix, rest) = colon < 0 ? (key, "") : (key[..colon], key[(colon + 1)..]);
            // The railway (a track, a turnout: an element assembly): rails, sleepers and bearers, the ballast bed and the sub-ballast — IFC 4.3's own classes, IFC4's nearest.
            if (host == "IfcElementAssembly")
                switch (prefix)
                {
                    case "rail": return o.Ifc43 ? new("IfcRail", "RAIL", null) : new("IfcMember", "USERDEFINED", "Rail");
                    case "sleeper": return o.Ifc43 ? new("IfcTrackElement", "SLEEPER", null) : new("IfcBuildingElementPart", "USERDEFINED", "Sleeper");
                    case "bearer": return o.Ifc43 ? new("IfcTrackElement", "SLEEPER", null) : new("IfcBuildingElementPart", "USERDEFINED", "Bearer");
                    case "ballast": return o.Ifc43 ? new("IfcCourse", "BALLASTBED", null) : new("IfcBuildingElementPart", "USERDEFINED", "Ballast bed");
                    case "subballast": return o.Ifc43 ? new("IfcCourse", "USERDEFINED", "Sub-ballast") : new("IfcBuildingElementPart", "USERDEFINED", "Sub-ballast");
                    case "frog": return o.Ifc43 ? new("IfcTrackElement", "FROG", null) : new("IfcBuildingElementPart", "USERDEFINED", "Frog");
                    // A level crossing: its deck a rigid pavement, its barriers boom barriers, its signals signals — IFC 4.3; IFC4's nearest.
                    case "deck": return o.Ifc43 ? new(IfcClass.Pavement, "RIGID", null) : new(IfcClass.Slab, "USERDEFINED", "Level crossing deck");
                    case "barrier": return o.Ifc43 ? new(IfcClass.Door, "BOOM_BARRIER", null) : new("IfcBuildingElementPart", "USERDEFINED", "Barrier");
                    case "signal": return o.Ifc43 ? new("IfcSignal", "VISUAL", null) : new("IfcBuildingElementPart", "USERDEFINED", "Signal");
                }
            return (host, prefix) switch
            {
                (IfcClass.Wall, "lintel") => new(IfcClass.Beam, "LINTEL", null),
                (IfcClass.Wall, "downstand") => new(IfcClass.Beam, "USERDEFINED", "Ring beam"),
                (IfcClass.Wall, "zone") when zones.FirstOrDefault(z => (string?)z["key"] == rest) is { } zone && (string?)zone["role"] == "ringBeam" => new(IfcClass.Beam, "USERDEFINED", "Ring beam"),
                (IfcClass.Roof, "covering") or (IfcClass.Roof, "dormer-roofs") => new(IfcClass.Slab, "ROOF", null),
                (IfcClass.Roof, "roof-windows") => new(IfcClass.Window, "SKYLIGHT", null),
                (IfcClass.Roof, "glass") => new("IfcPlate", "SHEET", null),
                (IfcClass.Roof, "dormer-walls") => new(IfcClass.Wall, "NOTDEFINED", null),
                (IfcClass.Roof, "timber") => new("IfcMember", "MEMBER", null),
                (IfcClass.Roof, "drainage") => new("IfcPipeSegment", "GUTTER", null),
                (IfcClass.Roof, "snow") => new("IfcDiscreteAccessory", "USERDEFINED", "Snow guard"),
                ("IfcElementAssembly", "standards" or "ledgers" or "transoms" or "guardrails") => new("IfcMember", "MEMBER", null),
                ("IfcElementAssembly", "diagonals") => new("IfcMember", "BRACE", null),
                ("IfcElementAssembly", "props") => new("IfcMember", "STRUT", null),
                ("IfcElementAssembly", "primary" or "secondary" or "bearers" or "chamfers") => new("IfcMember", "USERDEFINED", p.Name),
                ("IfcElementAssembly", "decks" or "toe" or "plywood" or "edges" or "bottom" or "sides" or "net") => new("IfcPlate", "USERDEFINED", p.Name),
                ("IfcElementAssembly", "jacks" or "anchors") => new("IfcDiscreteAccessory", "USERDEFINED", p.Name),
                ("IfcCurtainWall", "mullion") => new("IfcMember", "MULLION", null),
                ("IfcCurtainWall", "transom") => new("IfcMember", "USERDEFINED", "TRANSOM"),
                ("IfcCurtainWall", "panel") => new("IfcPlate", "CURTAIN_PANEL", null),
                (IfcClass.Stair, "flight") => new(IfcClass.StairFlight, "STRAIGHT", null),
                (IfcClass.Stair, "winders") => new(IfcClass.StairFlight, "WINDER", null),
                (IfcClass.Stair, "landing") => new(IfcClass.Slab, "LANDING", null),
                (IfcClass.Stair, "railing") => new("IfcRailing", "HANDRAIL", null),
                (IfcClass.Stair, "finish") => new("IfcCovering", "FLOORING", null),
                (IfcClass.Footing, "footing") => new(IfcClass.Footing, "STRIP_FOOTING", null),
                (IfcClass.Footing, "step") => new(IfcClass.Footing, "PAD_FOOTING", null),
                _ => new("IfcBuildingElementPart", "NOTDEFINED", p.Name),
            };
        }

        /// <summary>The element's openings with their bodies, voiding it; a void that is an element of its own (a platform in the terrain) is related at the end, in IFC 4.3.</summary>
        void Openings(Entity e, string entity, string place, double z0, IVoidShapes? voids)
        {
            foreach (var rel in m.RelationsOf(e.Id, IfcClass.RelVoidsElement).Where(r => r.Relating == e.Id).ToList())
                foreach (var oid in rel.Related)
                {
                    if (m.Get(oid) is not { } op) continue;
                    if (op.Class != IfcClass.OpeningElement)
                    {
                        // A void that is an element of its own (a platform in the terrain): its host's body already shows it, so it has no
                        // producer of its own; it is written without a body, with its intent, where its host is.
                        if (!written.ContainsKey(oid) && s.Catalog.Geometry(op.Class) == null && IsElementClass(op))
                        {
                            var k = KindOf(op);
                            var r = Product(k, Gid(op.Id), op.Name, k.ObjectType, Place(place, 0), Null, op.Id);
                            written[oid] = r;
                            Contain(contained.First(c => c.Value.Contains(entity)).Key, r);
                            Properties(op, k, r, null, [("LapinHost", e.Id)]);
                            Elements++;
                        }
                        pendingVoids.Add((rel, e.Id, oid));
                        continue;
                    }
                    var solid = voids?.Shape(op, m);
                    var shape = solid != null ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf([Brep(solid, null, Frame.AtZ(z0))]) })) : Null;
                    var opening = f.Add("IFCOPENINGELEMENT", Gid(op.Id), Null, S(op.Name), Null, Null, Place(place, 0), shape, S(op.Id), E("OPENING"));
                    written[op.Id] = opening;
                    f.Add("IFCRELVOIDSELEMENT", Gid(rel.Id), Null, Null, Null, entity, opening);
                    Define(opening, IntentOf(op, Gid(op.Id, IntentKey), null), Gid(op.Id, "intent-rel"));
                    pendingFills.AddRange(m.RelationsOf(op.Id, IfcClass.RelFillsElement).Where(r => r.Relating == op.Id));
                }
        }

        // ─── Properties and quantities ──────────────────────────────────────

        void Properties(Entity e, Kind kind, string entity, Entity? type, IReadOnlyList<(string Name, string Value)>? extra = null)
        {
            var common = new List<(string Name, string Value)>();
            if (type != null) common.Add(("Reference", Typed("IFCIDENTIFIER", S(type.Name))));
            if (kind.Class is IfcClass.Column or IfcClass.Beam or IfcClass.Footing) common.Add(("LoadBearing", Typed("IFCBOOLEAN", True)));
            if (e.Class is IfcClass.Door or IfcClass.Window && type?.Attrs["spec"] is JsonObject spec && spec["isExternal"] is JsonValue ext && ext.TryGetValue<bool>(out var isExternal))
                common.Add(("IsExternal", Typed("IFCBOOLEAN", isExternal ? True : False)));
            // A stair's numbers, as Pset_StairCommon names them: the whole stair's risers and treads, its riser and tread.
            if (kind.Class == IfcClass.Stair && s.Quantities(e.Id).FirstOrDefault(q => q.Name == "Lapin_StairQuantities") is { } sq)
            {
                double? Q(string n) => sq.Quantities.FirstOrDefault(x => x.Name == n)?.Value;
                var treads = s.Quantities(e.Id).Where(q => q.Name == "Qto_StairFlightBaseQuantities").Sum(q => q.Quantities.FirstOrDefault(x => x.Name == "NumberOfTreads")?.Value ?? 0);
                if (Q("NumberOfRiser") is { } nr) common.Add(("NumberOfRiser", Typed("IFCCOUNTMEASURE", ((int)nr).ToString(System.Globalization.CultureInfo.InvariantCulture))));
                common.Add(("NumberOfTreads", Typed("IFCCOUNTMEASURE", ((int)treads).ToString(System.Globalization.CultureInfo.InvariantCulture))));
                if (Q("RiserHeight") is { } rh && rh > 0) common.Add(("RiserHeight", Typed("IFCPOSITIVELENGTHMEASURE", R(rh))));
                if (Q("TreadLength") is { } tl && tl > 0) common.Add(("TreadLength", Typed("IFCPOSITIVELENGTHMEASURE", R(tl))));
            }
            // A foreign element's own sets, as its file wrote them, stand for what Lapin would answer.
            var src = e.Attrs[IfcSource.Key] as JsonObject;
            var carried = src != null ? IfcCarry.Writer.SetNames(src).ToHashSet() : [];
            // The common set: what Lapin knows, and what the user wrote into it (their value over Lapin's of the same name).
            var commonName = src == null && CommonSets.Contains(kind.Class) ? $"Pset_{kind.Class[3..]}Common" : null;
            var commonProps = commonName != null ? CommonWith(e, commonName, common) : [];
            if (commonProps.Count > 0)
                Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "common"), Null, S(commonName), Null, L(commonProps)), Gid(e.Id, "common-rel"));
            NamedSets(e, entity, commonName, carried);
            Define(entity, IntentOf(e, Gid(e.Id, IntentKey), kind, extra), Gid(e.Id, "intent-rel"));
            var phase = new List<string>();
            if (e.Str("phaseCreated") is { } created) phase.Add(Single("Created", Typed("IFCLABEL", S(PhaseName(created)))));
            if (e.Str("phaseDemolished") is { } demolished) phase.Add(Single("Demolished", Typed("IFCLABEL", S(PhaseName(demolished)))));
            // Created and struck in one phase (formwork, a scaffold): temporary, as IFC's Status says it.
            if (e.Str("phaseCreated") is { } c0 && c0 == e.Str("phaseDemolished")) phase.Add(Single("Status", Typed("IFCLABEL", S("TEMPORARY"))));
            if (phase.Count > 0) Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, PhaseKey), Null, S(PhaseSetName), Null, L(phase)), Gid(e.Id, "phase-rel"));
            // The user's own parameters (those of its type are on the type, which IFC readers pass on to it).
            var own = ParamsOf(e, Gid(e.Id, ParamsKey));
            if (own != null) Define(entity, own, Gid(e.Id, "params-rel"));
            StandardSets(e, null, entity, e.Id);
            if (src == null) return;
            // The file's sets, with the user's values in the sets of the same name; a set of Lapin's that was written above, Lapin's.
            HashSet<string> lapins = [IntentSetName, OldIntentSetName];
            if (phase.Count > 0) lapins.Add(PhaseSetName);
            if (own != null) lapins.Add(ParamsSetName);
            var user = NamedParams(e).Where(x => carried.Contains(x.Set)).ToDictionary(x => x.Set, x => x.Props);
            Carry.Element(m, src, entity, GlobalIdOf(e.Id), user, lapins, (n, v) => Single(n, v));
        }

        /// <summary>The standard sets the modules give an element or one of its parts (IFC 4.3), each written as it is named.</summary>
        void StandardSets(Entity e, GeometryPart? part, string entity, string seed)
        {
            if (!o.Ifc43) return;
            foreach (var src in s.Catalog.All<IIfcAlignments>())
                foreach (var set in src.StandardSets(m, e, part))
                {
                    var props = set.Properties.Select(p => Single(p.Name, p.Value switch
                    {
                        bool b => Typed(p.Measure, b ? True : False),
                        double d => Typed(p.Measure, R(d)),
                        int i => Typed(p.Measure, R(i)),
                        _ => Typed(p.Measure, S(p.Value.ToString())),
                    })).ToList();
                    if (props.Count > 0) Define(entity, f.Add("IFCPROPERTYSET", Gid(seed, "pset/" + set.Name), Null, S(set.Name), Null, L(props)), Gid(seed, "pset-rel/" + set.Name));
                }
        }

        /// <summary>An entity's own parameters (those in no named set) as a typed property set; null when it has none.</summary>
        string? ParamsOf(Entity e, string gid)
        {
            var own = new List<string>();
            foreach (var p in (e.Attrs["params"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (Params.SetOf(p) != null || (string?)p["name"] is not { } pname || ParamValue(p, false) is not { } value) continue;
                own.Add(Single(pname, value));
            }
            return own.Count > 0 ? f.Add("IFCPROPERTYSET", gid, Null, S(ParamsSetName), Null, L(own)) : null;
        }

        /// <summary>A parameter's value as IFC types it; a text in a standard set is a label, as IFC's Pset_…Common have them.</summary>
        static string? ParamValue(JsonObject p, bool standard) => p["value"] is not { } pv ? null : (string?)p["kind"] switch
        {
            "length" => Json.Number(pv) is { } l ? Typed("IFCLENGTHMEASURE", R(l)) : null,
            "area" => Json.Number(pv) is { } ar ? Typed("IFCAREAMEASURE", R(ar)) : null,
            "angle" => Json.Number(pv) is { } an ? Typed(standard ? "IFCPLANEANGLEMEASURE" : "IFCREAL", R(an)) : null,
            "number" => Json.Number(pv) is { } nm ? Typed("IFCREAL", R(nm)) : null,
            "bool" => pv is JsonValue bv && bv.TryGetValue<bool>(out var yes) ? Typed("IFCBOOLEAN", yes ? True : False) : null,
            "choice" => Typed("IFCLABEL", S(pv.ToString())),
            _ => Typed(standard ? "IFCLABEL" : "IFCTEXT", S(pv.ToString())),
        };

        /// <summary>The user's parameters in named sets (Pset_DoorCommon, a set of their own), set by set.</summary>
        static List<(string Set, List<(string Name, string Value)> Props)> NamedParams(Entity e)
        {
            var sets = new List<(string Set, List<(string Name, string Value)> Props)>();
            foreach (var p in (e.Attrs["params"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (Params.SetOf(p) is not { } set || (string?)p["name"] is not { } name || ParamValue(p, StandardPsets.IsStandard(set)) is not { } value) continue;
                var i = sets.FindIndex(x => string.Equals(x.Set, set, StringComparison.OrdinalIgnoreCase));
                if (i < 0) sets.Add((set, [(name, value)])); else sets[i].Props.Add((name, value));
            }
            return sets;
        }

        /// <summary>A common set's properties: Lapin's, each replaced by the user's value of the same name, then the user's others.</summary>
        List<string> CommonWith(Entity e, string setName, List<(string Name, string Value)> computed)
        {
            var user = NamedParams(e).FirstOrDefault(x => x.Set == setName).Props ?? [];
            var merged = computed.Where(c => !user.Any(u => u.Name == c.Name)).Concat(user);
            return merged.Select(p => Single(p.Name, p.Value)).ToList();
        }

        /// <summary>The user's named sets other than <paramref name="except"/> (the common set, written with Lapin's) and the <paramref name="carried"/> (written with the file's), each as its own property set.</summary>
        void NamedSets(Entity e, string entity, string? except, IReadOnlySet<string> carried)
        {
            foreach (var (set, props) in NamedParams(e))
            {
                if (set == except || carried.Contains(set)) continue;
                Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "params:" + set), Null, S(set), Null, L(props.Select(p => Single(p.Name, p.Value)).ToArray())), Gid(e.Id, "params-rel:" + set));
            }
        }

        /// <summary>
        /// The intent set: the entity's attributes as Lapin holds them, typed by
        /// the class's fields (lengths as IfcLengthMeasure, angles as reals in
        /// degrees, lists and objects as JSON text), the Lapin class when the IFC
        /// one differs, and the version of this set.
        /// </summary>
        string IntentOf(Entity e, string gid, Kind? kind, IReadOnlyList<(string Name, string Value)>? extra = null, IReadOnlySet<string>? only = null) =>
            f.Add("IFCPROPERTYSET", gid, Null, S(IntentSetName), Null, L(IntentProperties(e, kind, extra, only)));

        static readonly HashSet<string> ProjectIntent = ["location", "phases", "lengthUnit", "longName", "phase", "crs"];

        List<string> IntentProperties(Entity e, Kind? kind, IReadOnlyList<(string Name, string Value)>? extra = null, IReadOnlySet<string>? only = null)
        {
            var fields = (s.Catalog.Class(e.Class)?.Fields ?? []).GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Kind);
            var props = new List<string>();
            foreach (var (key, v) in e.Attrs)
            {
                // A stored body (a reference from IFC) is written as geometry, not as text; the project's standards stay in .lapin.
                if (v == null || key == StoredBodies.Key || key == StoredBodies.SolidBodyKey || key == StoredBodies.BodyOf || key == StoredBodies.XfKey || key == Plan2d.Key || key == IfcSource.Key || (only != null && !only.Contains(key))) continue;
                // A foreign element's properties go as the sets its file had, not as text.
                if (key == "properties" && e.Attrs[IfcSource.Key] != null) continue;
                var field = fields.GetValueOrDefault(key);
                var value = v switch
                {
                    JsonValue jv when jv.TryGetValue<bool>(out var b) => Typed("IFCBOOLEAN", b ? True : False),
                    JsonValue jv when Json.Number(jv) is { } d => Typed(field == "length" ? "IFCLENGTHMEASURE" : "IFCREAL", R(d)),
                    JsonValue jv => Typed("IFCLABEL", S(jv.ToString())),
                    _ => Typed("IFCTEXT", S(v.ToJsonString(Json.Options))),
                };
                props.Add(Single(key, value, field == "angle" ? "degrees" : null));
            }
            if (kind != null && kind.Class != e.Class) props.Add(Single("LapinClass", Typed("IFCLABEL", S(e.Class))));
            if (e.PredefinedType != null) props.Add(Single("LapinPredefinedType", Typed("IFCLABEL", S(e.PredefinedType))));
            foreach (var (name, value) in extra ?? []) props.Add(Single(name, Typed("IFCLABEL", S(value))));
            props.Add(Single("LapinVersion", Typed("IFCLABEL", S(Version))));
            return props;
        }

        /// <summary>The element's quantity sets, by the kind IFC measures; a part's set goes on the part when it is an element of its own, else on the element, named after the part.</summary>
        void Quantities(Entity e, string entity, bool grouped)
        {
            var carried = e.Attrs[IfcSource.Key] is JsonObject src ? IfcCarry.Writer.SetNames(src).ToHashSet() : [];
            foreach (var set in s.Quantities(e.Id))
            {
                if (carried.Contains(set.Name)) continue;
                var target = set.Part != null && grouped && partRefs.TryGetValue((e.Id, set.Part), out var pr) ? pr : entity;
                var seen = new Dictionary<string, int>();
                var q = new List<string>();
                foreach (var x in set.Quantities.Where(x => double.IsFinite(x.Value)))
                {
                    var n = seen[x.Name] = seen.GetValueOrDefault(x.Name) + 1;
                    var name = S(n == 1 ? x.Name : $"{x.Name} ({n})");
                    var cls = x.Kind switch
                    {
                        "LENGTH" => "IFCQUANTITYLENGTH", "AREA" => "IFCQUANTITYAREA", "VOLUME" => "IFCQUANTITYVOLUME",
                        "WEIGHT" => "IFCQUANTITYWEIGHT", "COUNT" => "IFCQUANTITYCOUNT", _ => null,
                    };
                    if (cls == null) continue;
                    q.Add(f.Add(cls, name, S(x.Label), Null, cls == "IFCQUANTITYCOUNT" ? I((int)Math.Round(x.Value)) : R(x.Value), Null));
                }
                if (q.Count == 0) continue;
                var key = set.Part != null ? $"{set.Name}/{set.Part}" : set.Name;
                var description = target == entity && set.Part != null ? set.PartName ?? set.Part : null;
                Define(target, f.Add("IFCELEMENTQUANTITY", Gid(e.Id, "qto/" + key), Null, S(set.Name), S(description), Null, L(q)), Gid(e.Id, "qto-rel/" + key));
            }
        }

        // ─── The railway: its facility, its parts, its alignments (IFC 4.3) ──

        readonly Dictionary<string, string> railwayParts = [];
        string railwayRef = "";
        /// <summary>Each alignment written: the curve its distances run along (the gradient curve, else the horizontal) and its placement, by the alignment's id.</summary>
        readonly Dictionary<string, (string Basis, string Place)> alignmentCurves = [];

        /// <summary>
        /// An element placed along its alignment, as its module says (IIfcAlignments):
        /// an IfcLinearPlacement at a distance along the alignment's curve, heading
        /// along it, with the absolute frame as its CartesianPosition — and that
        /// frame, for the body to be written in. Null: placed by coordinates.
        /// </summary>
        (string Place, Frame Frame)? LinearPlace(Entity e)
        {
            foreach (var src in s.Catalog.All<IIfcAlignments>())
            {
                if (src.LinearPlacementOf(m, e) is not { } lp || !alignmentCurves.TryGetValue(lp.AlignmentId, out var curve)) continue;
                var point = f.Add("IFCPOINTBYDISTANCEEXPRESSION", Typed("IFCLENGTHMEASURE", R(lp.DistanceAlong)), Null, Null, Null, curve.Basis);
                var up = f.Add("IFCDIRECTION", "(0.,0.,1.)");
                var ahead = f.Add("IFCDIRECTION", L(new[] { R(Math.Cos(lp.Heading)), R(Math.Sin(lp.Heading)), R(0) }));
                var cartesian = f.Add("IFCAXIS2PLACEMENT3D", f.Add("IFCCARTESIANPOINT", L(new[] { R(lp.Origin.X), R(lp.Origin.Y), R(lp.Origin.Z) })), up, ahead);
                return (f.Add("IFCLINEARPLACEMENT", sitePlace, f.Add("IFCAXIS2PLACEMENTLINEAR", point, up, ahead), cartesian), new Frame(lp.Origin, lp.Heading));
            }
            return null;
        }

        /// <summary>The railway part an element belongs to, as its module says; null: none, or not IFC 4.3.</summary>
        string? RailwayPart(Entity e)
        {
            if (!o.Ifc43) return null;
            foreach (var src in s.Catalog.All<IIfcAlignments>())
                if (src.RailwayPartOf(m, e) is { } part)
                {
                    if (!railwayParts.TryGetValue(part, out var r)) railwayParts[part] = r = RailwayPartRef(part);
                    return r;
                }
            return null;
        }

        /// <summary>IfcRailway under the site, once; each part (TRACK, TURNOUTTRACK, LINESIDE…) aggregated in it, once.</summary>
        string RailwayPartRef(string part)
        {
            if (railwayRef.Length == 0)
            {
                railwayRef = f.Add("IFCRAILWAY", Gid(project.Id, "railway"), Null, S("Railway"), Null, Null, Place(sitePlace, 0), Null, Null, E("ELEMENT"), E("NOTDEFINED"));
                refIdOf[railwayRef] = project.Id;
                f.Add("IFCRELAGGREGATES", Gid(project.Id, "aggregates/railway"), Null, Null, Null, siteRef, L(new[] { railwayRef }));
            }
            // IfcRailwayPartTypeEnum: the track as its structure, the turnouts as a superstructure, what stands beside the track as a
            // lineside structure; the usage along the line. IFC4X3_ADD2 renamed the items (TRACKSTRUCTURE → TRACK, TURNOUTSUPERSTRUCTURE
            // → TURNOUTTRACK, LINESIDESTRUCTURE → LINESIDE); the first IFC4X3 keeps the long names.
            var add2 = o.Schema.Contains("ADD", StringComparison.OrdinalIgnoreCase);
            var (predefined, name) = part switch
            {
                "TRACK" => (add2 ? "TRACK" : "TRACKSTRUCTURE", "Track"),
                "TURNOUTTRACK" => (add2 ? "TURNOUTTRACK" : "TURNOUTSUPERSTRUCTURE", "Turnouts"),
                "LINESIDE" => (add2 ? "LINESIDE" : "LINESIDESTRUCTURE", "Lineside"),
                _ => ("USERDEFINED", part),
            };
            var r = f.Add("IFCRAILWAYPART", Gid(project.Id, "railway/" + part), Null, S(name), Null, predefined == "USERDEFINED" ? S(part) : Null, Place(sitePlace, 0), Null, Null, E("ELEMENT"), E("LONGITUDINAL"), E(predefined));
            refIdOf[r] = project.Id + "/" + part;
            f.Add("IFCRELAGGREGATES", Gid(project.Id, "railway/" + part + "/aggregates"), Null, Null, Null, railwayRef, L(new[] { r }));
            // Lapin's track is ballasted (its section is a ballast bed on a sub-ballast).
            if (part == "TRACK")
                Define(r, f.Add("IFCPROPERTYSET", Gid(project.Id, "railway/" + part + "/pset"), Null, S("Pset_RailwayTrackStructurePart"), Null, L(new[] { Single("HasBallastTrack", Typed("IFCBOOLEAN", True)) })), Gid(project.Id, "railway/" + part + "/pset-rel"));
            return r;
        }

        /// <summary>Nothing of the railway's spatial tree is made ahead; the parts come as the elements ask for them.</summary>
        void Railway() { }

        /// <summary>
        /// The alignments the modules offer (IIfcAlignments), IFC 4.3 only: each an
        /// IfcAlignment aggregated by the project and referenced in the site, with
        /// its horizontal, vertical and cant layers nested in it, each layer's
        /// segments nested in order and closed by the zero-length segment the
        /// concept template asks for, every segment's design parameters as the
        /// module gives them; its plan drawn through as the Axis representation.
        /// </summary>
        void Alignments()
        {
            if (!o.Ifc43) return;
            var all = s.Catalog.All<IIfcAlignments>().SelectMany(src => src.Alignments(m)).ToList();
            if (all.Count == 0) return;
            var refs = new List<string>();
            foreach (var a in all)
            {
                string Gi(string key) => Gid(a.Id, "alignment/" + key);
                var reps = new List<string>();
                // The geometry: the horizontal as IfcCompositeCurve (FootPrint), the vertical as IfcGradientCurve over it and the
                // cant as IfcSegmentedReferenceCurve over that (Axis, 3D); each layer's segments remember their curve segments
                // for their own representation. Without the curves, the sampled plan as a polycurve.
                string? composite = null, gradient = null;
                var segmentItems = new Dictionary<(string Layer, int Source), List<string>>();
                if (a.Curves is { } curves)
                {
                    string Items(IReadOnlyList<IfcCurveSegmentSpec> specs, string layer)
                    {
                        var ids = new List<string>();
                        foreach (var sp in specs)
                        {
                            var cs = CurveSegment(sp);
                            ids.Add(cs);
                            (segmentItems.TryGetValue((layer, sp.Source), out var l) ? l : segmentItems[(layer, sp.Source)] = []).Add(cs);
                        }
                        return L(ids);
                    }
                    composite = f.Add("IFCCOMPOSITECURVE", Items(curves.Horizontal, "horizontal"), False);
                    if (curves.Vertical != null) gradient = f.Add("IFCGRADIENTCURVE", Items(curves.Vertical, "vertical"), False, composite, Null);
                    var reference = curves.Cant != null && gradient != null ? f.Add("IFCSEGMENTEDREFERENCECURVE", Items(curves.Cant, "cant"), False, gradient, Null) : null;
                    if (footprint == "") footprint = f.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("FootPrint"), S("Model"), Derived, Derived, Derived, Derived, model, Null, E("PLAN_VIEW"), Null);
                    reps.Add(f.Add("IFCSHAPEREPRESENTATION", footprint, S("FootPrint"), S("Curve2D"), L(new[] { composite })));
                    reps.Add((reference ?? gradient) is { } axisCurve
                        ? f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve3D"), L(new[] { axisCurve }))
                        : f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve2D"), L(new[] { composite })));
                }
                else if (a.Plan.Count >= 2)
                {
                    var points = f.Add("IFCCARTESIANPOINTLIST2D", L(a.Plan.Select(p => L(new[] { R(p.X), R(p.Y) }))), Null);
                    reps.Add(f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Curve2D"), L(new[] { f.Add("IFCINDEXEDPOLYCURVE", points, Null, False) })));
                }
                var shape = reps.Count > 0 ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(reps)) : Null;
                var place = Place(sitePlace, 0);
                if ((gradient ?? composite) is { } along) alignmentCurves[a.Id] = (along, place);
                var entity = f.Add("IFCALIGNMENT", Gid(a.Id, "alignment"), Null, S(a.Name), Null, Null, place, shape, Null);
                refs.Add(entity);
                var layers = new List<string>();

                var hs = new List<string>();
                foreach (var (sg, i) in a.Horizontal.Select((x, i) => (x, i)))
                    hs.Add(Seg(Gi($"h/{i}"), f.Add("IFCALIGNMENTHORIZONTALSEGMENT", Null, Null, P2(sg.Start), R(sg.Direction), R(sg.StartRadius), R(sg.EndRadius), R(sg.Length), sg.GravityCenterLineHeight is { } h ? R(h) : Null, E(sg.Type)), "horizontal", i));
                if (a.Horizontal.Count > 0)
                {
                    var last = a.Horizontal[^1];
                    hs.Add(Seg(Gi("h/end"), f.Add("IFCALIGNMENTHORIZONTALSEGMENT", Null, Null, P2(EndOf(a.Plan, last)), R(last.Direction), R(last.EndRadius), R(last.EndRadius), R(0), Null, E("LINE")), "horizontal", -1));
                }
                var horizontal = f.Add("IFCALIGNMENTHORIZONTAL", Gi("h"), Null, S("Horizontal"), Null, Null, Null, Null);
                f.Add("IFCRELNESTS", Gi("h/nests"), Null, Null, Null, horizontal, L(hs));
                layers.Add(horizontal);

                if (a.Vertical is { Count: > 0 } vs)
                {
                    var list = new List<string>();
                    foreach (var (v, i) in vs.Select((x, i) => (x, i)))
                        list.Add(Seg(Gi($"v/{i}"), f.Add("IFCALIGNMENTVERTICALSEGMENT", Null, Null, R(v.StartDistAlong), R(v.HorizontalLength), R(v.StartHeight), R(v.StartGradient), R(v.EndGradient), v.Radius is { } r ? R(r) : Null, E(v.Type)), "vertical", i));
                    var lv = vs[^1];
                    list.Add(Seg(Gi("v/end"), f.Add("IFCALIGNMENTVERTICALSEGMENT", Null, Null, R(lv.StartDistAlong + lv.HorizontalLength), R(0), R(EndHeight(lv)), R(lv.EndGradient), R(lv.EndGradient), Null, E("CONSTANTGRADIENT")), "vertical", -1));
                    var vertical = f.Add("IFCALIGNMENTVERTICAL", Gi("v"), Null, S("Vertical"), Null, Null, Null, Null);
                    f.Add("IFCRELNESTS", Gi("v/nests"), Null, Null, Null, vertical, L(list));
                    layers.Add(vertical);
                }

                if (a.Cant is { Segments.Count: > 0 } c)
                {
                    var list = new List<string>();
                    foreach (var (cs, i) in c.Segments.Select((x, i) => (x, i)))
                        list.Add(Seg(Gi($"c/{i}"), f.Add("IFCALIGNMENTCANTSEGMENT", Null, Null, R(cs.StartDistAlong), R(cs.HorizontalLength), R(cs.StartCantLeft), cs.EndCantLeft is { } el ? R(el) : Null, R(cs.StartCantRight), cs.EndCantRight is { } er ? R(er) : Null, E(cs.Type)), "cant", i));
                    var lc = c.Segments[^1];
                    list.Add(Seg(Gi("c/end"), f.Add("IFCALIGNMENTCANTSEGMENT", Null, Null, R(lc.StartDistAlong + lc.HorizontalLength), R(0), R(lc.EndCantLeft ?? lc.StartCantLeft), Null, R(lc.EndCantRight ?? lc.StartCantRight), Null, E("CONSTANTCANT")), "cant", -1));
                    var cant = f.Add("IFCALIGNMENTCANT", Gi("c"), Null, S("Cant"), Null, Null, Null, Null, R(c.RailHeadDistance));
                    f.Add("IFCRELNESTS", Gi("c/nests"), Null, Null, Null, cant, L(list));
                    layers.Add(cant);
                }
                // The referents (the start station, the kilometre posts), placed linearly on the alignment's curve and
                // nested in it after the layers, each with its Pset_Stationing.
                if (a.Referents is { Count: > 0 } referents && (gradient ?? composite) is { } basis)
                {
                    foreach (var (r, i) in referents.Select((x, i) => (x, i)))
                    {
                        var point = f.Add("IFCPOINTBYDISTANCEEXPRESSION", Typed("IFCLENGTHMEASURE", X(r.DistanceAlong)), Null, Null, Null, basis);
                        var linear = f.Add("IFCLINEARPLACEMENT", place, f.Add("IFCAXIS2PLACEMENTLINEAR", point, Null, Null), Null);
                        var referent = f.Add("IFCREFERENT", Gi($"referent/{i}"), Null, S(r.Name), Null, Null, linear, Null, E(r.Type));
                        var props = new List<string> { Single("Station", Typed("IFCLENGTHMEASURE", R(r.Station))) };
                        if (r.HasIncreasingStation is { } increasing) props.Add(Single("HasIncreasingStation", Typed("IFCBOOLEAN", increasing ? True : False)));
                        if (r.IncomingStation is { } incoming) props.Add(Single("IncomingStation", Typed("IFCLENGTHMEASURE", R(incoming))));
                        Define(referent, f.Add("IFCPROPERTYSET", Gi($"referent/{i}/stationing"), Null, S("Pset_Stationing"), Null, L(props)), Gi($"referent/{i}/stationing-rel"));
                        layers.Add(referent);
                    }
                }
                f.Add("IFCRELNESTS", Gi("layers"), Null, Null, Null, entity, L(layers));
                // Where its stationing starts, as the alignment's own property set.
                Define(entity, f.Add("IFCPROPERTYSET", Gi("stationing"), Null, S("Lapin_Stationing"), Null, L(new[] { Single("StartStation", $"IFCLENGTHMEASURE({R(a.StartStation)})") })), Gi("stationing-rel"));

                // A semantic segment's representation: its curve segment(s), placed as the alignment is (a product with a shape
                // needs a placement), or nothing without the curves.
                string Seg(string gid, string design, string layer, int source) =>
                    segmentItems.TryGetValue((layer, source), out var items)
                        ? f.Add("IFCALIGNMENTSEGMENT", gid, Null, Null, Null, Null, place,
                            f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { f.Add("IFCSHAPEREPRESENTATION", axis, S("Axis"), S("Segment"), L(items)) })), design)
                        : f.Add("IFCALIGNMENTSEGMENT", gid, Null, Null, Null, Null, Null, Null, design);
            }
            f.Add("IFCRELAGGREGATES", Gid(project.Id, "aggregates/alignments"), Null, Null, Null, projectRef, L(refs));
            f.Add("IFCRELREFERENCEDINSPATIALSTRUCTURE", Gid(project.Id, "site/alignments"), Null, Null, Null, L(refs), siteRef);

            // An IfcCurveSegment: its transition, its placement (2D, or 3D with the cant's tilted axis), SegmentStart and
            // SegmentLength on the parent curve, the parent at its own origin. Full precision: a cubic's A₃ or a parabola's
            // C are far below the nine decimals the rest of the file keeps.
            string CurveSegment(IfcCurveSegmentSpec sp)
            {
                var location = f.Add("IFCCARTESIANPOINT", L(sp.Location.Select(X)));
                var refDirection = f.Add("IFCDIRECTION", L(sp.RefDirection.Select(X)));
                var placement = sp.Location.Count >= 3
                    ? f.Add("IFCAXIS2PLACEMENT3D", location, sp.Axis != null ? f.Add("IFCDIRECTION", L(sp.Axis.Select(X))) : Null, refDirection)
                    : f.Add("IFCAXIS2PLACEMENT2D", location, refDirection);
                return f.Add("IFCCURVESEGMENT", E(sp.Transition), placement, Typed("IFCLENGTHMEASURE", X(sp.Start)), Typed("IFCLENGTHMEASURE", X(sp.Length)), Parent(sp.Parent));
            }

            string Parent(IfcParentCurve c)
            {
                string Origin() => f.Add("IFCAXIS2PLACEMENT2D", f.Add("IFCCARTESIANPOINT", "(0.,0.)"), Null);
                static string T(double? v) => v is { } x ? X(x) : Null;
                switch (c.Kind)
                {
                    case "circle": return f.Add("IFCCIRCLE", f.Add("IFCAXIS2PLACEMENT2D", f.Add("IFCCARTESIANPOINT", L(new[] { X(c.Centre.X), X(c.Centre.Y) })), Null), X(c.Radius));
                    case "clothoid": return f.Add("IFCCLOTHOID", Origin(), X(c.A));
                    case "polynomial": return f.Add("IFCPOLYNOMIALCURVE", Origin(), L(c.X!.Select(X)), L(c.Y!.Select(X)), Null);
                    case "spiral":
                    {
                        // IFC lists the terms from the highest power down.
                        var terms = c.Terms!;
                        var attrs = new[] { Origin() }.Concat(terms.Reverse().Select(T)).ToArray();
                        return f.Add(terms.Count switch { 3 => "IFCSECONDORDERPOLYNOMIALSPIRAL", 4 => "IFCTHIRDORDERPOLYNOMIALSPIRAL", _ => "IFCSEVENTHORDERPOLYNOMIALSPIRAL" }, attrs);
                    }
                    case "cosine": return f.Add("IFCCOSINESPIRAL", Origin(), X(c.Trig), T(c.Constant));
                    case "sine": return f.Add("IFCSINESPIRAL", Origin(), X(c.Trig), T(c.Linear), T(c.Constant));
                    default: return f.Add("IFCLINE", f.Add("IFCCARTESIANPOINT", "(0.,0.)"), f.Add("IFCVECTOR", f.Add("IFCDIRECTION", "(1.,0.)"), R(1)));
                }
            }

            // A real at full precision, in STEP's form (a decimal point in the mantissa, an exponent when short).
            static string X(double v)
            {
                if (!double.IsFinite(v)) v = 0;
                var t = v.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
                var e = t.IndexOf('E');
                var mantissa = e < 0 ? t : t[..e];
                if (!mantissa.Contains('.')) mantissa += ".";
                return e < 0 ? mantissa : mantissa + t[e..];
            }
            static double EndHeight(IfcVerticalSegment v) => v.Type == "PARABOLICARC" ? v.StartHeight + (v.StartGradient + v.EndGradient) / 2 * v.HorizontalLength : v.StartHeight + v.StartGradient * v.HorizontalLength;
            static Vec2 EndOf(IReadOnlyList<Vec2> plan, IfcHorizontalSegment last) => plan.Count > 0 ? plan[^1] : last.Start;
        }

        // ─── The relationships gathered on the way ──────────────────────────

        /// <summary>
        /// The distribution network the modules declare (IIfcNetwork): each
        /// element's ports nested in it, placed where they are and facing
        /// their way; the connections between ports; the systems grouping
        /// their elements and serving the building.
        /// </summary>
        void Network()
        {
            var nets = s.Catalog.All<IIfcNetwork>();
            if (nets.Count == 0) return;
            var portRef = new Dictionary<(string Element, string Port), string>();
            foreach (var (id, entity) in written.ToList())
            {
                if (m.Get(id) is not { } e || !placeOf.TryGetValue(id, out var at)) continue;
                var ports = new List<string>();
                foreach (var net in nets)
                    foreach (var p in net.PortsOf(m, e))
                    {
                        if (portRef.ContainsKey((id, p.Key))) continue;
                        var dir = p.Dir.Length > 1e-9 ? p.Dir.Normalized() : new Vec3(0, 0, 1);
                        var side = Vec3.Cross(new Vec3(0, 0, 1), dir);
                        var refDir = side.Length > 1e-6 ? side.Normalized() : new Vec3(1, 0, 0);
                        var place = f.Add("IFCLOCALPLACEMENT", at.Place, f.Add("IFCAXIS2PLACEMENT3D",
                            f.Add("IFCCARTESIANPOINT", L(new[] { R(p.At.X), R(p.At.Y), R(p.At.Z - at.Z0) })),
                            f.Add("IFCDIRECTION", L(new[] { R(dir.X), R(dir.Y), R(dir.Z) })),
                            f.Add("IFCDIRECTION", L(new[] { R(refDir.X), R(refDir.Y), R(refDir.Z) }))));
                        var port = f.Add("IFCDISTRIBUTIONPORT", Gid(id, "port/" + p.Key), Null, S(p.Key), Null, Null, place, Null,
                            E(p.Flow is "SOURCE" or "SINK" or "SOURCEANDSINK" ? p.Flow : "NOTDEFINED"), E(p.Kind.Length > 0 ? p.Kind : "NOTDEFINED"), E(p.SystemType.Length > 0 ? p.SystemType : "NOTDEFINED"));
                        portRef[(id, p.Key)] = port;
                        ports.Add(port);
                    }
                if (ports.Count > 0) f.Add("IFCRELNESTS", Gid(id, "ports"), Null, Null, Null, entity, L(ports));
            }
            foreach (var net in nets)
            {
                foreach (var c in net.Connections(m))
                    if (portRef.TryGetValue((c.ElementA, c.PortA), out var a) && portRef.TryGetValue((c.ElementB, c.PortB), out var b))
                        f.Add("IFCRELCONNECTSPORTS", Gid(c.ElementA, $"connects/{c.PortA}/{c.ElementB}/{c.PortB}"), Null, Null, Null, a, b, Null);
                foreach (var sys in net.Systems(m))
                {
                    var members = sys.Members.Where(written.ContainsKey).Select(x => written[x]).ToList();
                    var system = f.Add("IFCDISTRIBUTIONSYSTEM", Gid(sys.Id), Null, S(sys.Name), Null, Null, Null, E(sys.Predefined.Length > 0 ? sys.Predefined : "NOTDEFINED"));
                    if (members.Count > 0) f.Add("IFCRELASSIGNSTOGROUP", Gid(sys.Id, "members"), Null, Null, Null, L(members), Null, system);
                    f.Add("IFCRELSERVICESBUILDINGS", Gid(sys.Id, "serves"), Null, Null, Null, system, L(new[] { buildingRef }));
                }
            }
        }

        void Finish()
        {
            // Lapin's groups (docs/groups.md) as IFC's: an IfcGroup assigned the members written.
            foreach (var g in Groups.All(m))
            {
                var members = Groups.MembersOf(m, g.Id).Where(written.ContainsKey).Select(x => written[x]).ToList();
                if (members.Count == 0) continue;
                var group = f.Add("IFCGROUP", Gid(g.Id), Null, S(g.Name), Null, Null);
                f.Add("IFCRELASSIGNSTOGROUP", Gid(g.Id, "members"), Null, Null, Null, L(members), Null, group);
            }
            foreach (var (typeId, t) in types) f.Add("IFCRELDEFINESBYTYPE", Gid(typeId, "defines"), Null, Null, Null, L(t.Occurrences), t.Ref);
            foreach (var (materialId, list) in materialUses) f.Add("IFCRELASSOCIATESMATERIAL", Gid(materialId, "associates"), Null, Null, Null, L(list), Material(materialId));
            foreach (var fill in pendingFills)
                if (written.TryGetValue(fill.Relating, out var opening) && fill.Related.Count > 0 && written.TryGetValue(fill.Related[0], out var filler))
                    f.Add("IFCRELFILLSELEMENT", Gid(fill.Id), Null, Null, Null, opening, filler);
            // Joins: the element that runs through relates the other; the ends they meet at, the style as the description.
            foreach (var rel in m.Relations.Where(r => r.Class == IfcClass.RelConnectsPathElements))
                if (rel.Related.Count > 0 && written.TryGetValue(rel.Relating, out var a) && written.TryGetValue(rel.Related[0], out var b))
                    f.Add("IFCRELCONNECTSPATHELEMENTS", Gid(rel.Id), Null, S((string?)rel.Attrs?["kind"]), S((string?)rel.Attrs?["style"]), Null, a, b, "()", "()",
                        E(Connection((string?)rel.Attrs?["relatedConnection"])), E(Connection((string?)rel.Attrs?["relatingConnection"])));
            // Temporary works: the product resource they are (formwork, a scaffold), assigned to them; formwork connected to the element it forms.
            foreach (var e in m.Entities)
            {
                if (!written.TryGetValue(e.Id, out var element)) continue;
                if (e.Str("resource") is { Length: > 0 } res)
                {
                    var known = res is "FORMWORK" or "ASSEMBLY";
                    var resource = f.Add("IFCCONSTRUCTIONPRODUCTRESOURCE", Gid(e.Id, "resource"), Null, S(e.Name), Null, S(known ? null : res), Null, Null, Null, Null, Null, E(known ? res : "USERDEFINED"));
                    f.Add("IFCRELASSIGNSTOPRODUCT", Gid(e.Id, "resource-rel"), Null, Null, Null, L(new[] { resource }), Null, element);
                }
                if (e.Str("host") is { } host && written.TryGetValue(host, out var h))
                    f.Add("IFCRELCONNECTSELEMENTS", Gid(e.Id, "host-rel"), Null, S("Formwork"), Null, Null, h, element);
            }
            // A platform voiding the terrain: IFC 4.3 has it as a feature; in IFC4 it is a proxy and only stands there.
            if (o.Ifc43)
                foreach (var (rel, host, v) in pendingVoids)
                    if (m.Get(v)?.Class == IfcClass.EarthworksCut && written.TryGetValue(host, out var h) && written.TryGetValue(v, out var cut))
                        f.Add("IFCRELVOIDSELEMENT", Gid(rel.Id), Null, Null, Null, h, cut);
            foreach (var (spatial, list) in contained)
                f.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", Gid(refIdOf[spatial], "contains"), Null, Null, Null, L(list), spatial);
            // Rooms are parts of their storey: aggregated, as IFC has spaces.
            foreach (var (spatial, list) in spaces)
                f.Add("IFCRELAGGREGATES", Gid(refIdOf.GetValueOrDefault(spatial) ?? project.Id, "aggregates/spaces"), Null, Null, Null, spatial, L(list));
        }

        static string Connection(string? value)
        {
            var v = (value ?? "").ToUpperInvariant();
            return v.Contains("START") ? "ATSTART" : v.Contains("END") ? "ATEND" : v.Contains("PATH") ? "ATPATH" : "NOTDEFINED";
        }
    }
}
