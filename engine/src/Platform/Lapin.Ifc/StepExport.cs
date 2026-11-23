using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using static Lapin.Ifc.Step;

namespace Lapin.Ifc;

/// <summary>What a STEP export takes: the elements asked for (null: every element with a body), less those left out.</summary>
public sealed record StepExportOptions(IReadOnlyCollection<string>? Ids = null, IReadOnlySet<string>? Exclude = null);

/// <summary>What a STEP export wrote: its solids, how many of them exactly (on their surfaces) and how many as their facets.</summary>
public sealed record StepExportResult(string Path, int Solids, int Exact, int Faceted, IReadOnlyList<string> Notes)
{
    public JsonObject ToJson() => new()
    {
        ["path"] = Path, ["solids"] = Solids, ["exact"] = Exact, ["faceted"] = Faceted,
        ["notes"] = new JsonArray(Notes.Select(n => (JsonNode)n).ToArray()),
    };
}

/// <summary>
/// The model's bodies as a STEP file (ISO 10303-21, AP214 AUTOMOTIVE_DESIGN; docs/plan-studio-parametric.md X1, docs/plan-studio-freeform.md
/// §9): each element a product of its own with an ADVANCED_BREP_SHAPE_REPRESENTATION of its bodies, each body a MANIFOLD_SOLID_BREP of
/// advanced faces — on its exact surfaces where a module knows them (<see cref="IIfcExactBodies"/>: Studio's cylinders, revolutions, smooth
/// lofts, SubD patches), else on the planes of its facets — in its colour (STYLED_ITEM), in millimetres. Written after ISO 10303-42 and -214
/// as published, with no dependency. CAD programs (FreeCAD, Inventor, SolidWorks, Fusion) read it as solids.
/// </summary>
public static class StepExport
{
    public const string Schema = "AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }";

    public static (byte[] Bytes, StepExportResult Result) Bytes(Session session, string name, StepExportOptions? options = null)
    {
        var o = options ?? new StepExportOptions();
        var m = session.Model;
        var f = new Step { Application = session.Product.Name };
        var w = new ExactWriter(f, ifc: false, scale: 1000);
        var notes = new List<string>();
        // The contexts: millimetres, radians, the uncertainty of a hundredth of a millimetre.
        var appContext = f.Add("APPLICATION_CONTEXT", S("core data for automotive mechanical design processes"));
        f.Add("APPLICATION_PROTOCOL_DEFINITION", S("international standard"), S("automotive_design"), I(2000), appContext);
        var productContext = f.Add("PRODUCT_CONTEXT", S(""), appContext, S("mechanical"));
        var definitionContext = f.Add("PRODUCT_DEFINITION_CONTEXT", S("part definition"), appContext, S("design"));
        var mm = f.AddComplex(("LENGTH_UNIT", []), ("NAMED_UNIT", [Derived]), ("SI_UNIT", [E("MILLI"), E("METRE")]));
        var rad = f.AddComplex(("NAMED_UNIT", [Derived]), ("PLANE_ANGLE_UNIT", []), ("SI_UNIT", [Null, E("RADIAN")]));
        var sr = f.AddComplex(("NAMED_UNIT", [Derived]), ("SI_UNIT", [Null, E("STERADIAN")]), ("SOLID_ANGLE_UNIT", []));
        var uncertainty = f.Add("UNCERTAINTY_MEASURE_WITH_UNIT", Typed("LENGTH_MEASURE", R(0.01)), mm, S("distance_accuracy_value"), S("confusion accuracy"));
        var context = f.AddComplex(("GEOMETRIC_REPRESENTATION_CONTEXT", [I(3)]), ("GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT", [L(new[] { uncertainty })]),
            ("GLOBAL_UNIT_ASSIGNED_CONTEXT", [L(new[] { mm, rad, sr })]), ("REPRESENTATION_CONTEXT", [S("3D"), S("3D")]));
        var origin = w.Placement(new Frame3(Vec3.Zero, Vec3.UnitZ, new Vec3(1, 0, 0)));
        var products = new List<string>();
        var styled = new List<string>();
        var colours = new Dictionary<string, string>();
        string Style(string hex)
        {
            if (colours.TryGetValue(hex, out var known)) return known;
            var (r, g, b) = HexColor.Unit(hex, (0.75, 0.75, 0.75));
            var rgb = f.Add("COLOUR_RGB", S(""), R(r), R(g), R(b));
            var fill = f.Add("FILL_AREA_STYLE", S(""), L(new[] { f.Add("FILL_AREA_STYLE_COLOUR", S(""), rgb) }));
            var side = f.Add("SURFACE_SIDE_STYLE", S(""), L(new[] { f.Add("SURFACE_STYLE_FILL_AREA", fill) }));
            return colours[hex] = f.Add("PRESENTATION_STYLE_ASSIGNMENT", L(new[] { f.Add("SURFACE_STYLE_USAGE", E("BOTH"), side) }));
        }
        int solids = 0, exact = 0, faceted = 0;
        var exactSources = session.Catalog.All<IIfcExactBodies>();
        IEnumerable<Entity> elements = o.Ids != null ? o.Ids.Select(m.Get).OfType<Entity>() : m.Entities;
        foreach (var e in elements.ToList())
        {
            if (o.Exclude?.Contains(e.Id) == true || !IsElement(session, e)) continue;
            ElementGeometry? geo;
            try { geo = session.Geometry(e.Id); }
            catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { notes.Add($"„{e.Name}” was left out: {ex.Message}"); continue; }
            var parts = geo?.Parts.Where(p => p.Solid.Faces.Length > 0).ToList() ?? [];
            if (parts.Count == 0) continue;
            var items = new List<string>();
            foreach (var p in parts)
            {
                ExactBody? body = null;
                // One part: the element's exact body; several: each part's, where its module knows them (an abutment's wings).
                foreach (var src in exactSources)
                {
                    try { body = parts.Count == 1 ? src.ExactOf(m, session.Catalog, e, p.Solid) : p.Key is { } key ? src.ExactOfPart(m, session.Catalog, e, key, p.Solid) : null; }
                    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { notes.Add($"„{e.Name}” was written as its facets: {ex.Message}"); }
                    if (body != null) break;
                }
                if (body != null) exact++;
                else { body = ExactBuild.Planar(p.Solid); faceted++; }
                var brep = w.Brep(body, parts.Count == 1 ? e.Name : $"{e.Name} {p.Name}");
                items.Add(brep);
                solids++;
                if (p.Color is { } hex) styled.Add(f.Add("STYLED_ITEM", S("color"), L(new[] { Style(hex) }), brep));
            }
            var product = f.Add("PRODUCT", S(e.Id), S(e.Name), S(e.Class), L(new[] { productContext }));
            products.Add(product);
            var formation = f.Add("PRODUCT_DEFINITION_FORMATION", S(""), S(""), product);
            var definition = f.Add("PRODUCT_DEFINITION", S("design"), S(""), formation, definitionContext);
            var shape = f.Add("PRODUCT_DEFINITION_SHAPE", S(""), S(""), definition);
            var rep = f.Add("ADVANCED_BREP_SHAPE_REPRESENTATION", S(e.Name), L(items.Prepend(origin)), context);
            f.Add("SHAPE_DEFINITION_REPRESENTATION", shape, rep);
        }
        if (products.Count > 0) f.Add("PRODUCT_RELATED_PRODUCT_CATEGORY", S("part"), Null, L(products));
        if (styled.Count > 0) f.Add("MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION", S(""), L(styled), context);
        if (solids == 0) notes.Add("Nothing with a body was chosen: the file holds no solid.");
        if (exact > 0) notes.Add($"{exact} solid(s) on their exact surfaces, {faceted} as their facets.");
        var bytes = f.ToArray(name, "Lapin STEP export", Schema);
        return (bytes, new StepExportResult(name, solids, exact, faceted, notes));
    }

    /// <summary>What the IFC export takes as an element: a body of its own, an element's class (no opening, grid or annotation).</summary>
    static bool IsElement(Session s, Entity e) =>
        (s.Catalog.Geometry(e.Class) != null || StoredBodies.Has(e))
        && e.Class is not (IfcClass.OpeningElement or IfcClass.Grid or IfcClass.Annotation or IfcClass.Space)
        && (s.Catalog.Class(e.Class)?.Kind ?? "element") == "element";
}
