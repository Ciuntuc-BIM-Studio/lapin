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
/// is a parent over elements of their own; "simple": one body per element)
/// and a phase (what exists at its end; null: the whole project).
/// </summary>
public sealed record IfcExportOptions(string Schema = "IFC4", string Detail = "group", string? Phase = null)
{
    public bool Ifc43 => Schema.StartsWith("IFC4X3", StringComparison.OrdinalIgnoreCase);
    public bool Grouped => !string.Equals(Detail, "simple", StringComparison.OrdinalIgnoreCase);
    public string SchemaName => Ifc43 ? "IFC4X3_ADD2" : "IFC4";
}

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

    /// <summary>The classes with a Pset_…Common the export can answer.</summary>
    static readonly HashSet<string> CommonSets =
    [
        IfcClass.Wall, IfcClass.Slab, IfcClass.Roof, IfcClass.Column, IfcClass.Beam, IfcClass.Footing, IfcClass.Door, IfcClass.Window,
        "IfcMember", "IfcPlate", "IfcCovering", "IfcRailing", "IfcStair", "IfcRamp", "IfcChimney", IfcClass.BuildingElementProxy,
    ];

    sealed class Writer(Session s, IfcExportOptions o)
    {
        public readonly Step File = new();
        public IfcExportOptions O => o;
        public int Elements, PartCount, StoreyCount;
        public readonly List<string> Notes = [];

        readonly IModelView m = s.Model;
        Entity project = null!;
        string model = "", body = "", axis = "", metre = "", siteRef = "", sitePlace = "";
        readonly Dictionary<string, string> storeyRef = [], storeyPlace = [], refIdOf = [];
        readonly Dictionary<string, (string Place, double Z0)> placeOf = [];
        string buildingRef = "";
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

        public void Run()
        {
            project = Spatial.Project(m) ?? throw new CommandException("The model has no project.");
            WriteSpatial();
            foreach (var mat in m.OfClass(IfcClass.Material)) Material(mat.Id);
            Grids();
            WriteElements();
            Network();
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
        string Brep(Solid solid, string? hex, double z0) => Brep(solid, hex, v => new Vec3(v.X, v.Y, v.Z - z0));

        /// <summary>A solid as a faceted B-rep, its vertices taken into the placement's frame by <paramref name="local"/>, in its colour.</summary>
        string Brep(Solid solid, string? hex, Func<Vec3, Vec3> local)
        {
            var pts = solid.Vertices.Select(local).Select(v => f.Add("IFCCARTESIANPOINT", L(new[] { R(v.X), R(v.Y), R(v.Z) }))).ToArray();
            string Loop(int[] ring) => f.Add("IFCPOLYLOOP", L(ring.Select(i => pts[i])));
            var faces = solid.Faces.Where(x => x.Outer.Length >= 3).Select(face => f.Add("IFCFACE", L(
                new[] { f.Add("IFCFACEOUTERBOUND", Loop(face.Outer), True) }
                    .Concat((face.Holes ?? []).Where(h => h.Length >= 3).Select(h => f.Add("IFCFACEBOUND", Loop(h), True)))))).ToList();
            var brep = f.Add("IFCFACETEDBREP", f.Add("IFCCLOSEDSHELL", L(faces)));
            if (hex != null) f.Add("IFCSTYLEDITEM", brep, L(new[] { Style(hex) }), Null);
            return brep;
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

        string BodyOf(IEnumerable<string> breps) => f.Add("IFCSHAPEREPRESENTATION", body, S("Body"), S("Brep"), L(breps));

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
            var projectRef = f.Add("IFCPROJECT", Gid(project.Id), Null, S(project.Name), Null, Null, S(project.Str("longName")), S(project.Str("phase")), L(new[] { model }), units);
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
            foreach (var e in m.Entities.ToList())
            {
                if (!IsElement(e) || !InPhase(e)) continue;
                try { Element(e, voids); }
                catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException)
                {
                    Notes.Add($"„{e.Name}” was not exported: {ex.Message}");
                }
            }
        }

        void Element(Entity e, IVoidShapes? voids)
        {
            if (e.Class == IfcClass.Space) { Space(e); return; }
            var geo = s.Geometry(e.Id, new GeometryOptions(CutOpenings: false));
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
            var kind = KindOf(e);
            var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
            // A wall of layers only is one body with its layer set; one with zones, lintels or a ring beam (a roof with its planes and timber, a footing with its sole) is a parent over its parts.
            // A door's frame and leaf, a column's body, a terrain: one element. Only a wall, a roof and a footing hold parts that are elements of their own.
            var grouped = o.Grouped && kind.Class is IfcClass.Wall or IfcClass.Roof or IfcClass.Footing or "IfcCurtainWall" or IfcClass.Stair or "IfcElementAssembly" && parts.Count > 1 && !parts.All(p => (p.Key ?? "").StartsWith("layer:", StringComparison.Ordinal));
            // A door or window: placed with its local +Y the way its panels open, which IFC's left and right are seen along.
            var joinery = e.Class is IfcClass.Door or IfcClass.Window && !grouped ? s.Catalog.Get<IJoineryIfc>() : null;
            var at = joinery?.Place(m, e);
            var place = at is { } jp ? JoineryPlace(relTo, jp, z0) : Place(relTo, 0);
            var reps = new List<string>();
            if (!grouped) reps.Add(BodyOf(parts.Select(p => at is { } jp2 ? Brep(p.Solid, p.Color, v => Local(jp2, v)) : Brep(p.Solid, p.Color, z0))));
            if (AxisOf(e) is { } ax) reps.Add(ax);
            reps.AddRange(Plan2dOf(e));
            var shape = reps.Count > 0 ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(reps)) : Null;
            var (height, width) = Overall(e, type);
            // Its type in IFC's terms (the mirrored type for an element that is its type's mirror image), which the element repeats.
            var mirrored = at?.Mirrored == true;
            var terms = type != null && joinery != null ? joinery.Type(m, type, mirrored) : null;
            var entity = Product(kind, Gid(e.Id), e.Name, kind.ObjectType ?? type?.Name, place, shape, e.Id, height, width, terms);
            written[e.Id] = entity;
            if (at == null) placeOf[e.Id] = (place, z0);
            Contain(spatial, entity);
            Elements++;
            if (type != null) TypeOf(type, kind, entity, mirrored, terms);
            MaterialsOf(e, entity, parts, grouped);
            if (grouped) Parts(e, kind, parts, place, z0, entity);
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
            var common = new List<string> { Single("IsExternal", Typed("IFCBOOLEAN", False)) };
            if (number != null) common.Insert(0, Single("Reference", Typed("IFCIDENTIFIER", S(number))));
            Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "common"), Null, S("Pset_SpaceCommon"), Null, L(common)), Gid(e.Id, "common-rel"));
            Define(entity, IntentOf(e, Gid(e.Id, IntentKey), new Kind(IfcClass.Space, "SPACE", null)), Gid(e.Id, "intent-rel"));
            if (ParamsOf(e, Gid(e.Id, ParamsKey)) is { } set) Define(entity, set, Gid(e.Id, "params-rel"));
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
            if (!Classes.TryGetValue(cls, out var allowed)) return new(IfcClass.BuildingElementProxy, "USERDEFINED", cls);
            if (allowed == null) return new(cls, "", null);
            var pre = e.PredefinedType?.ToUpperInvariant();
            if (pre == null && cls == IfcClass.Roof)
                pre = e.Str("shape") switch { "flat" => "FLAT_ROOF", "gable" => "GABLE_ROOF", "hip" => "HIP_ROOF", "mansard" => "MANSARD_ROOF", "shed" => "SHED_ROOF", "barrel" => "BARREL_ROOF", _ => null };
            if (pre == null && cls == IfcClass.GeographicElement) pre = "TERRAIN";
            if (pre == null) return new(cls, "NOTDEFINED", null);
            if (allowed.Contains(pre) || pre is "USERDEFINED" or "NOTDEFINED") return new(cls, pre, null);
            return new(cls, "USERDEFINED", pre);
        }

        /// <summary>The product entity with the attributes its class has: the eight every element shares, then what the class adds.</summary>
        string Product(Kind k, string gid, string? name, string? objectType, string place, string shape, string tag, double? height = null, double? width = null, IfcJoineryType? terms = null)
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
                IfcClass.StairFlight => f.Add(up, [.. common, Null, Null, Null, Null, E(k.Predefined)]),
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

        void Parts(Entity e, Kind kind, List<GeometryPart> parts, string place, double z0, string parent)
        {
            var refs = new List<string>();
            var zones = (m.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Attrs["zones"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            for (var i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                var key = p.Key ?? $"part:{i}";
                var pk = PartKind(kind.Class, key, p, zones);
                var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf([Brep(p.Solid, p.Color, z0)]) }));
                var r = Product(pk, Gid(e.Id, key), p.Name, pk.ObjectType, Place(place, 0), shape, $"{e.Id}/{key}");
                if (p.Material != null) UseMaterial(p.Material, r);
                partRefs[(e.Id, key)] = r;
                refs.Add(r);
                PartCount++;
            }
            f.Add("IFCRELAGGREGATES", Gid(e.Id, "parts"), Null, Null, Null, parent, L(refs));
        }

        /// <summary>What a part is in IFC: a lintel or ring beam a beam, a roof plane a roof slab, the timber members, the sole of a strip footing a footing; else a building element part named as Lapin names it.</summary>
        static Kind PartKind(string host, string key, GeometryPart p, List<JsonObject> zones)
        {
            var colon = key.IndexOf(':');
            var (prefix, rest) = colon < 0 ? (key, "") : (key[..colon], key[(colon + 1)..]);
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
                    var shape = solid != null ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(new[] { BodyOf([Brep(solid, null, z0)]) })) : Null;
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
            var common = new List<string>();
            if (type != null) common.Add(Single("Reference", Typed("IFCIDENTIFIER", S(type.Name))));
            if (kind.Class is IfcClass.Column or IfcClass.Beam or IfcClass.Footing) common.Add(Single("LoadBearing", Typed("IFCBOOLEAN", True)));
            if (e.Class is IfcClass.Door or IfcClass.Window && type?.Attrs["spec"] is JsonObject spec && spec["isExternal"] is JsonValue ext && ext.TryGetValue<bool>(out var isExternal))
                common.Add(Single("IsExternal", Typed("IFCBOOLEAN", isExternal ? True : False)));
            if (common.Count > 0 && CommonSets.Contains(kind.Class))
                Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, "common"), Null, S($"Pset_{kind.Class[3..]}Common"), Null, L(common)), Gid(e.Id, "common-rel"));
            Define(entity, IntentOf(e, Gid(e.Id, IntentKey), kind, extra), Gid(e.Id, "intent-rel"));
            var phase = new List<string>();
            if (e.Str("phaseCreated") is { } created) phase.Add(Single("Created", Typed("IFCLABEL", S(PhaseName(created)))));
            if (e.Str("phaseDemolished") is { } demolished) phase.Add(Single("Demolished", Typed("IFCLABEL", S(PhaseName(demolished)))));
            // Created and struck in one phase (formwork, a scaffold): temporary, as IFC's Status says it.
            if (e.Str("phaseCreated") is { } c0 && c0 == e.Str("phaseDemolished")) phase.Add(Single("Status", Typed("IFCLABEL", S("TEMPORARY"))));
            if (phase.Count > 0) Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, PhaseKey), Null, S(PhaseSetName), Null, L(phase)), Gid(e.Id, "phase-rel"));
            // The user's own parameters (those of its type are on the type, which IFC readers pass on to it).
            if (ParamsOf(e, Gid(e.Id, ParamsKey)) is { } set) Define(entity, set, Gid(e.Id, "params-rel"));
        }

        /// <summary>An entity's own parameters as a typed property set; null when it has none.</summary>
        string? ParamsOf(Entity e, string gid)
        {
            var own = new List<string>();
            foreach (var p in (e.Attrs["params"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if ((string?)p["name"] is not { } pname || p["value"] is not { } pv) continue;
                var value = (string?)p["kind"] switch
                {
                    "length" => Json.Number(pv) is { } l ? Typed("IFCLENGTHMEASURE", R(l)) : null,
                    "area" => Json.Number(pv) is { } ar ? Typed("IFCAREAMEASURE", R(ar)) : null,
                    "number" or "angle" => Json.Number(pv) is { } nm ? Typed("IFCREAL", R(nm)) : null,
                    "bool" => pv is JsonValue bv && bv.TryGetValue<bool>(out var yes) ? Typed("IFCBOOLEAN", yes ? True : False) : null,
                    "choice" => Typed("IFCLABEL", S(pv.ToString())),
                    _ => Typed("IFCTEXT", S(pv.ToString())),
                };
                if (value != null) own.Add(Single(pname, value));
            }
            return own.Count > 0 ? f.Add("IFCPROPERTYSET", gid, Null, S(ParamsSetName), Null, L(own)) : null;
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
                if (v == null || key == StoredBodies.Key || key == StoredBodies.BodyOf || key == StoredBodies.XfKey || key == Plan2d.Key || (only != null && !only.Contains(key))) continue;
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
            foreach (var set in s.Quantities(e.Id))
            {
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
