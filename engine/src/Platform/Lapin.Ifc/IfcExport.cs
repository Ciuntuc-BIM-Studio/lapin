using System.Text.Json.Nodes;
using BubbleBim.Geometry;
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
    public const string IntentSetName = "Pset_Lapin_Intent";
    public const string PhaseSetName = "Pset_Lapin_Phase";
    public const string ParamsSetName = "Pset_Lapin_Parameters";
    public const string Version = "1";

    public static IfcExportResult Write(Session session, string path, IfcExportOptions? options = null)
    {
        var w = new Writer(session, options ?? new IfcExportOptions());
        w.Run();
        w.File.Write(path, System.IO.Path.GetFileName(path), w.O.Ifc43 ? "ViewDefinition [ReferenceView]" : "ViewDefinition [ReferenceView_V1.2]", w.O.SchemaName);
        return new IfcExportResult(path, w.O.SchemaName, w.Elements, w.PartCount, w.StoreyCount, w.Notes);
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
        ["IfcCovering"] = ["CEILING", "FLOORING", "CLADDING", "ROOFING", "MOLDING", "SKIRTINGBOARD", "INSULATION", "MEMBRANE", "SLEEVING", "WRAPPING"],
        ["IfcRailing"] = ["HANDRAIL", "GUARDRAIL", "BALUSTRADE"],
        ["IfcStair"] = ["STRAIGHT_RUN_STAIR", "TWO_STRAIGHT_RUN_STAIR", "QUARTER_WINDING_STAIR", "QUARTER_TURN_STAIR", "HALF_WINDING_STAIR", "HALF_TURN_STAIR", "TWO_QUARTER_WINDING_STAIR", "TWO_QUARTER_TURN_STAIR", "THREE_QUARTER_WINDING_STAIR", "THREE_QUARTER_TURN_STAIR", "SPIRAL_STAIR", "DOUBLE_RETURN_STAIR", "CURVED_RUN_STAIR", "TWO_CURVED_RUN_STAIR"],
        ["IfcRamp"] = ["STRAIGHT_RUN_RAMP", "TWO_STRAIGHT_RUN_RAMP", "QUARTER_TURN_RAMP", "TWO_QUARTER_TURN_RAMP", "HALF_TURN_RAMP", "SPIRAL_RAMP"],
        ["IfcChimney"] = [],
        ["IfcFurnishingElement"] = null,
        [IfcClass.BuildingElementProxy] = ["COMPLEX", "ELEMENT", "PARTIAL", "PROVISIONFORVOID", "PROVISIONFORSPACE"],
        [IfcClass.GeographicElement] = ["TERRAIN"],
        [IfcClass.EarthworksCut] = ["BASE_EXCAVATION", "CUT", "DREDGING", "EXCAVATION", "OVEREXCAVATION", "PAVEMENTMILLING", "STEPEXCAVATION", "TOPSOILREMOVAL", "TRENCH"],
        ["IfcBuildingElementPart"] = ["INSULATION", "PRECASTPANEL"],
        ["IfcPipeSegment"] = ["CULVERT", "FLEXIBLESEGMENT", "RIGIDSEGMENT", "GUTTER", "SPOOL"],
        ["IfcDiscreteAccessory"] = ["ANCHORPLATE", "BRACKET", "SHOE"],
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
        readonly Dictionary<string, double> storeyZ = [];
        readonly Dictionary<string, List<string>> contained = [];
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
            Finish();
        }

        // ─── Ids and small helpers ──────────────────────────────────────────

        /// <summary>The model's id as the GlobalId; one that is not a GlobalId (an old project) is mapped to one, the same every time.</summary>
        static string Gid(string id) => S(GlobalId.IsValid(id) ? id : Guid("lapin", id));
        static string Gid(string parent, string key) => S(Guid(parent, key));

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
        string Brep(Solid solid, string? hex, double z0)
        {
            var pts = solid.Vertices.Select(v => f.Add("IFCCARTESIANPOINT", L(new[] { R(v.X), R(v.Y), R(v.Z - z0) }))).ToArray();
            string Loop(int[] ring) => f.Add("IFCPOLYLOOP", L(ring.Select(i => pts[i])));
            var faces = solid.Faces.Where(x => x.Outer.Length >= 3).Select(face => f.Add("IFCFACE", L(
                new[] { f.Add("IFCFACEOUTERBOUND", Loop(face.Outer), True) }
                    .Concat((face.Holes ?? []).Where(h => h.Length >= 3).Select(h => f.Add("IFCFACEBOUND", Loop(h), True)))))).ToList();
            var brep = f.Add("IFCFACETEDBREP", f.Add("IFCCLOSEDSHELL", L(faces)));
            if (hex != null) f.Add("IFCSTYLEDITEM", brep, L(new[] { Style(hex) }), Null);
            return brep;
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
            Define(projectRef, IntentOf(project, Gid(project.Id, IntentSetName), null, only: ProjectIntent), Gid(project.Id, "intent-rel"));

            var site = m.OfClass(IfcClass.Site).FirstOrDefault();
            var building = Spatial.Building(m);
            var basePoint = Location.Base(m);
            sitePlace = Place(null, 0);
            var siteId = site?.Id ?? project.Id;
            siteRef = f.Add("IFCSITE", site != null ? Gid(site.Id) : Gid(project.Id, "site"), Null, S(site?.Name ?? "Site"), Null, Null, sitePlace, Null, Null, E("ELEMENT"), Null, Null, R(basePoint.Elevation), Null, Null);
            refIdOf[siteRef] = siteId;
            var buildingPlace = Place(sitePlace, 0);
            var buildingId = building?.Id ?? project.Id;
            var buildingRef = f.Add("IFCBUILDING", building != null ? Gid(building.Id) : Gid(project.Id, "building"), Null, S(building?.Name ?? "Building"), Null, Null, buildingPlace, Null, Null, E("ELEMENT"), Null, Null, Null);
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
                Define(r, IntentOf(st, Gid(st.Id, IntentSetName), null), Gid(st.Id, "intent-rel"));
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
            var crs = f.Add("IFCPROJECTEDCRS", S(project.Str("crs") ?? "Site grid"), Null, Null, Null, Null, Null, metre);
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
                Define(grid, IntentOf(g, Gid(g.Id, IntentSetName), null), Gid(g.Id, "intent-rel"));
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
            var grouped = o.Grouped && kind.Class is IfcClass.Wall or IfcClass.Roof or IfcClass.Footing && parts.Count > 1 && !parts.All(p => (p.Key ?? "").StartsWith("layer:", StringComparison.Ordinal));
            var place = Place(relTo, 0);
            var reps = new List<string>();
            if (!grouped) reps.Add(BodyOf(parts.Select(p => Brep(p.Solid, p.Color, z0))));
            if (AxisOf(e) is { } ax) reps.Add(ax);
            var shape = reps.Count > 0 ? f.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(reps)) : Null;
            var (height, width) = Overall(e, type);
            var entity = Product(kind, Gid(e.Id), e.Name, kind.ObjectType ?? type?.Name, place, shape, e.Id, height, width);
            written[e.Id] = entity;
            Contain(spatial, entity);
            Elements++;
            if (type != null) TypeOf(type, kind, entity);
            MaterialsOf(e, entity, parts, grouped);
            if (grouped) Parts(e, kind, parts, place, z0, entity);
            Openings(e, entity, place, z0, voids);
            Properties(e, kind, entity, type);
            Quantities(e, entity, grouped);
        }

        /// <summary>A door or window stands on its wall's storey.</summary>
        Entity? HostStorey(Entity e) =>
            m.RelatingOf(e.Id, IfcClass.RelFillsElement) is { } opening && m.RelatingOf(opening.Id, IfcClass.RelVoidsElement) is { } host ? Spatial.StoreyOf(m, host.Id) : null;

        Kind KindOf(Entity e)
        {
            var cls = e.Str("exportAs") is { Length: > 3 } x && x.StartsWith("Ifc", StringComparison.Ordinal) ? x : e.Class;
            if (cls == IfcClass.EarthworksCut && !o.Ifc43) return new(IfcClass.BuildingElementProxy, "USERDEFINED", "Earthworks cut");
            if (!Classes.TryGetValue(cls, out var allowed)) return new(IfcClass.BuildingElementProxy, "USERDEFINED", cls);
            if (allowed == null) return new(cls, "", null);
            var pre = e.PredefinedType?.ToUpperInvariant();
            if (pre == null && cls == IfcClass.Roof)
                pre = e.Str("shape") switch { "flat" => "FLAT_ROOF", "gable" => "GABLE_ROOF", "hip" => "HIP_ROOF", "mansard" => "MANSARD_ROOF", "shed" => "SHED_ROOF", _ => null };
            if (pre == null && cls == IfcClass.GeographicElement) pre = "TERRAIN";
            if (pre == null) return new(cls, "NOTDEFINED", null);
            if (allowed.Contains(pre) || pre is "USERDEFINED" or "NOTDEFINED") return new(cls, pre, null);
            return new(cls, "USERDEFINED", pre);
        }

        /// <summary>The product entity with the attributes its class has: the eight every element shares, then what the class adds.</summary>
        string Product(Kind k, string gid, string? name, string? objectType, string place, string shape, string tag, double? height = null, double? width = null)
        {
            string[] common = [gid, Null, S(name), Null, S(objectType), place, shape, S(tag)];
            var up = k.Class.ToUpperInvariant();
            return k.Class switch
            {
                IfcClass.Door => f.Add(up, [.. common, Opt(height), Opt(width), E(k.Predefined), E("NOTDEFINED"), Null]),
                IfcClass.Window => f.Add(up, [.. common, Opt(height), Opt(width), E(k.Predefined), E("NOTDEFINED"), Null]),
                "IfcElementAssembly" => f.Add(up, [.. common, Null, E(k.Predefined)]),
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

        // ─── Types and materials ────────────────────────────────────────────

        void TypeOf(Entity type, Kind kind, string entity)
        {
            if (!types.TryGetValue(type.Id, out var t))
            {
                var typeClass = type.Class.EndsWith("Type", StringComparison.Ordinal) && Classes.ContainsKey(type.Class[..^4]) ? type.Class : kind.Class + "Type";
                var intent = IntentOf(type, Gid(type.Id, IntentSetName), null);
                // The type's parameters, which every element of the type has.
                string[] sets = ParamsOf(type, Gid(type.Id, ParamsSetName)) is { } ps ? [intent, ps] : [intent];
                var pre = E(kind.Predefined.Length == 0 ? "NOTDEFINED" : kind.Predefined);
                string[] common = [Gid(type.Id), Null, S(type.Name), Null, Null, L(sets), Null, S(type.Id), S(kind.Predefined == "USERDEFINED" ? kind.ObjectType : null)];
                var up = typeClass.ToUpperInvariant();
                var r = kind.Class switch
                {
                    IfcClass.Door => f.Add(up, [.. common, pre, E("NOTDEFINED"), False, Null]),
                    IfcClass.Window => f.Add(up, [.. common, pre, E("NOTDEFINED"), False, Null]),
                    _ => f.Add(up, [.. common, pre]),
                };
                types[type.Id] = t = (r, []);
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
                    Define(opening, IntentOf(op, Gid(op.Id, IntentSetName), null), Gid(op.Id, "intent-rel"));
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
            Define(entity, IntentOf(e, Gid(e.Id, IntentSetName), kind, extra), Gid(e.Id, "intent-rel"));
            var phase = new List<string>();
            if (e.Str("phaseCreated") is { } created) phase.Add(Single("Created", Typed("IFCLABEL", S(PhaseName(created)))));
            if (e.Str("phaseDemolished") is { } demolished) phase.Add(Single("Demolished", Typed("IFCLABEL", S(PhaseName(demolished)))));
            if (phase.Count > 0) Define(entity, f.Add("IFCPROPERTYSET", Gid(e.Id, PhaseSetName), Null, S(PhaseSetName), Null, L(phase)), Gid(e.Id, "phase-rel"));
            // The user's own parameters (those of its type are on the type, which IFC readers pass on to it).
            if (ParamsOf(e, Gid(e.Id, ParamsSetName)) is { } set) Define(entity, set, Gid(e.Id, "params-rel"));
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
                if (v == null || key == StoredBodies.Key || (only != null && !only.Contains(key))) continue;
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
            // A platform voiding the terrain: IFC 4.3 has it as a feature; in IFC4 it is a proxy and only stands there.
            if (o.Ifc43)
                foreach (var (rel, host, v) in pendingVoids)
                    if (written.TryGetValue(host, out var h) && written.TryGetValue(v, out var cut))
                        f.Add("IFCRELVOIDSELEMENT", Gid(rel.Id), Null, Null, Null, h, cut);
            foreach (var (spatial, list) in contained)
                f.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", Gid(refIdOf[spatial], "contains"), Null, Null, Null, L(list), spatial);
        }

        static string Connection(string? value)
        {
            var v = (value ?? "").ToUpperInvariant();
            return v.Contains("START") ? "ATSTART" : v.Contains("END") ? "ATEND" : v.Contains("PATH") ? "ATPATH" : "NOTDEFINED";
        }
    }
}
