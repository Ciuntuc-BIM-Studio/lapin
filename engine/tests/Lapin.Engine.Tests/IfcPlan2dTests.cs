using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// The 2D an IFC file gives its elements (docs/spec-ifc.md, Reprezentările
/// 2D): footprints, plan symbols in a plan context (directly or through a
/// type's maps), fill areas and texts, with their styles; drawn in plans
/// instead of the body's cut, unless the view's style turns it off.
/// </summary>
public class IfcPlan2dTests
{
    static Fixture New() => new(new ArchitectureModule(), new StructureModule());

    /// <summary>A small foreign file in metres: one storey at 0, a model context, a plan context with its Body/PLAN_VIEW subcontext.</summary>
    sealed class File2d
    {
        public readonly Step F = new();
        public readonly string Model, Plan, PlanBody, StoreyPlacement, Storey;
        readonly List<string> _contained = [];

        public File2d()
        {
            var world = F.Add("IFCAXIS2PLACEMENT3D", F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null);
            Model = F.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Model"), "3", "1.E-05", world, Null);
            Plan = F.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Null, S("Plan"), "2", "1.E-05", world, Null);
            PlanBody = F.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Body"), S("Plan"), Derived, Derived, Derived, Derived, Plan, Null, E("PLAN_VIEW"), Null);
            var units = F.Add("IFCUNITASSIGNMENT", L([F.Add("IFCSIUNIT", Derived, E("LENGTHUNIT"), Null, E("METRE"))]));
            var project = F.Add("IFCPROJECT", G("project"), Null, S("Foreign"), Null, Null, Null, Null, L([Model, Plan]), units);
            var sitePl = F.Add("IFCLOCALPLACEMENT", Null, world);
            var site = F.Add("IFCSITE", G("site"), Null, S("Site"), Null, Null, sitePl, Null, Null, E("ELEMENT"), Null, Null, Null, Null, Null);
            var bPl = F.Add("IFCLOCALPLACEMENT", sitePl, world);
            var building = F.Add("IFCBUILDING", G("building"), Null, S("Building"), Null, Null, bPl, Null, Null, E("ELEMENT"), Null, Null, Null);
            StoreyPlacement = F.Add("IFCLOCALPLACEMENT", bPl, world);
            Storey = F.Add("IFCBUILDINGSTOREY", G("storey"), Null, S("Ground"), Null, Null, StoreyPlacement, Null, Null, E("ELEMENT"), "0.");
            F.Add("IFCRELAGGREGATES", G("a1"), Null, Null, Null, project, L([site]));
            F.Add("IFCRELAGGREGATES", G("a2"), Null, Null, Null, site, L([building]));
            F.Add("IFCRELAGGREGATES", G("a3"), Null, Null, Null, building, L([Storey]));
        }

        public static string G(string key) => S(Guid("plan2d-test", key));
        public static string Gid(string key) => Guid("plan2d-test", key);

        public string Place(double x, double y, double angleDeg = 0)
        {
            var xDir = F.Add("IFCDIRECTION", $"({R(Math.Cos(angleDeg * Math.PI / 180))},{R(Math.Sin(angleDeg * Math.PI / 180))},0.)");
            return F.Add("IFCLOCALPLACEMENT", StoreyPlacement, F.Add("IFCAXIS2PLACEMENT3D", F.Add("IFCCARTESIANPOINT", $"({R(x)},{R(y)},0.)"), F.Add("IFCDIRECTION", "(0.,0.,1.)"), xDir));
        }

        public string Box(double x0, double y0, double dx, double dy, double h)
        {
            var prof = F.Add("IFCRECTANGLEPROFILEDEF", E("AREA"), Null, F.Add("IFCAXIS2PLACEMENT2D", F.Add("IFCCARTESIANPOINT", $"({R(x0 + dx / 2)},{R(y0 + dy / 2)})"), Null), R(dx), R(dy));
            return F.Add("IFCEXTRUDEDAREASOLID", prof, F.Add("IFCAXIS2PLACEMENT3D", F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null), F.Add("IFCDIRECTION", "(0.,0.,1.)"), R(h));
        }

        public string Polyline(params (double X, double Y)[] pts) => F.Add("IFCPOLYLINE", L(pts.Select(p => F.Add("IFCCARTESIANPOINT", $"({R(p.X)},{R(p.Y)})"))));

        public string Rep(string context, string id, string type, params string[] items) => F.Add("IFCSHAPEREPRESENTATION", context, S(id), S(type), L(items));

        public string Element(string type, string key, string placement, params string[] reps)
        {
            var shape = F.Add("IFCPRODUCTDEFINITIONSHAPE", Null, Null, L(reps));
            var tail = type is "IFCDOOR" or "IFCWINDOW" ? new[] { Null, Null, Null, Null, Null } : type == "IFCBUILDINGELEMENTPROXY" ? new[] { Null } : [];
            var id = F.Add(type, [G(key), Null, S(key), Null, Null, placement, shape, Null, .. tail]);
            _contained.Add(id);
            return id;
        }

        public byte[] Bytes()
        {
            F.Add("IFCRELCONTAINEDINSPATIALSTRUCTURE", G("contained"), Null, Null, Null, L(_contained), Storey);
            using var ms = new MemoryStream();
            F.Write(ms, "plan2d.ifc", "ViewDefinition [ReferenceView_V1.2]");
            return ms.ToArray();
        }
    }

    static IfcImportResult Import(Fixture f, File2d x, string mode = "auto") => IfcImport.Run(f.Session, x.Bytes(), "plan2d.ifc", new IfcImportOptions(mode), out _);

    static Entity Plan(Fixture f, string element) => Views.Find(f.Model, "plan:" + Spatial.StoreyOf(f.Model, element)!.Id)!;

    static ViewDrawing Drawing(Fixture f, Entity view) => Views.Generate(f.Session.Reader, view).Drawing!;

    static List<DLine> LinesOf(ViewDrawing d, string element) => d.Lines.Where(l => l.Element == element).ToList();

    static bool Near(Vec2 a, double x, double y) => Math.Abs(a.X - x) < 1e-3 && Math.Abs(a.Y - y) < 1e-3;

    /// <summary>A proxy with a box body and a footprint of another shape (a triangle): the plan draws the footprint, as cut lines, and not the box.</summary>
    [Fact]
    public void A_footprint_is_drawn_instead_of_the_cut()
    {
        var x = new File2d();
        var body = x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 2, 1, 3));
        var foot = x.Rep(x.Model, "FootPrint", "Curve2D", x.Polyline((0, 0), (2, 0), (1, 1), (0, 0)));
        x.Element("IFCBUILDINGELEMENTPROXY", "proxy", x.Place(10, 5), body, foot);
        var f = New();
        var r = Import(f, x);
        var id = File2d.Gid("proxy");
        var e = f.Model.Get(id)!;
        Assert.True(StoredBodies.Has(e));
        Assert.True(Plan2d.Has(e));
        var item = Assert.Single(Plan2d.Read(e));
        Assert.True(item.Closed);
        Assert.True(item.Footprint);
        Assert.Equal(3, item.Points.Count);
        Assert.Contains(r.Notes, n => n.Contains("own 2D"));

        var d = Drawing(f, Plan(f, id));
        Assert.DoesNotContain(d.Cuts, c => c.Element == id);
        var lines = LinesOf(d, id);
        Assert.Equal(3, lines.Count);
        Assert.All(lines, l => Assert.Equal(LineRole.Cut, l.Role)); // the body crosses the cut at 1.2
        Assert.Contains(lines, l => Near(l.A, 12, 5) && Near(l.B, 11, 6) || Near(l.B, 12, 5) && Near(l.A, 11, 6));
        // Pickable and snappable: its plan linework is the footprint.
        var g = f.Session.Reader.Geometry(id)!;
        Assert.Contains(g.Plan!, p => p.Kind == "symbol" && p.Closed && p.Points.Count == 3);
    }

    /// <summary>The view's style turned off (ifc2d false): the element is cut as before.</summary>
    [Fact]
    public void The_view_switch_falls_back_to_the_cut()
    {
        var x = new File2d();
        x.Element("IFCBUILDINGELEMENTPROXY", "proxy", x.Place(0, 0),
            x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 2, 1, 3)), x.Rep(x.Model, "FootPrint", "Curve2D", x.Polyline((0, 0), (2, 0), (1, 1), (0, 0))));
        var f = New();
        Import(f, x);
        var id = File2d.Gid("proxy");
        var storey = Spatial.StoreyOf(f.Model, id)!.Id;
        var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey, ["name"] = "Plan" })!["id"]!;
        Assert.Equal(3, LinesOf(Drawing(f, f.Model.Get(view)!), id).Count);
        f.Run("view.setStyle", new JsonObject { ["id"] = view, ["ifc2d"] = false });
        Assert.False(ViewStyles.Of(f.Model, f.Model.Get(view)!).Ifc2d);
        var d = Drawing(f, f.Model.Get(view)!);
        var cut = Assert.Single(d.Cuts, c => c.Element == id);
        Assert.Equal(4, cut.Loops[0].Length); // the box's rectangle
        // A style can turn it off for all its views; on by default.
        Assert.True(ViewStyles.Defaults.All(s => s.Ifc2d));
    }

    /// <summary>The hello-wall pattern: a window type maps a Body representation in the Plan/Body/PLAN_VIEW subcontext, the window maps it.</summary>
    [Fact]
    public void A_type_plan_symbol_in_a_plan_context_is_used()
    {
        var x = new File2d();
        var origin = x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null);
        var bodyMap = x.F.Add("IFCREPRESENTATIONMAP", origin, x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 0.9, 0.05, 1.2)));
        var pts = x.F.Add("IFCCARTESIANPOINTLIST2D", "((0.1,0.02),(0.8,0.02),(0.8,0.03),(0.1,0.03))");
        var symbol = x.F.Add("IFCINDEXEDPOLYCURVE", pts, "(IFCLINEINDEX((1,2,3,4,1)))", Null);
        var open = x.F.Add("IFCINDEXEDPOLYCURVE", x.F.Add("IFCCARTESIANPOINTLIST2D", "((0.1,0.05),(0.8,0.05))"), Null, Null);
        var planMap = x.F.Add("IFCREPRESENTATIONMAP", origin, x.Rep(x.PlanBody, "Body", "Annotation2D", x.F.Add("IFCGEOMETRICCURVESET", L([symbol, open]))));
        var type = x.F.Add("IFCWINDOWTYPE", File2d.G("wt"), Null, S("WT01"), Null, Null, Null, L([bodyMap, planMap]), Null, Null, E("NOTDEFINED"), E("NOTDEFINED"), Null, Null);
        var op = x.F.Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", Null, Null, x.F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), "1.", Null);
        var win = x.Element("IFCWINDOW", "window", x.Place(5, 0, 90),
            x.Rep(x.Model, "Body", "MappedRepresentation", x.F.Add("IFCMAPPEDITEM", bodyMap, op)),
            x.Rep(x.PlanBody, "Body", "MappedRepresentation", x.F.Add("IFCMAPPEDITEM", planMap, op)));
        x.F.Add("IFCRELDEFINESBYTYPE", File2d.G("byType"), Null, Null, Null, L([win]), type);
        var f = New();
        Import(f, x, "reference");
        var id = File2d.Gid("window");
        var items = Plan2d.Read(f.Model.Get(id)!);
        Assert.Equal(2, items.Count);
        var closed = Assert.Single(items, i => i.Closed);
        Assert.Equal(4, closed.Points.Count);
        Assert.False(closed.Footprint);
        // Turned 90° about (5, 0): local (0.1, 0.02) is at (5 − 0.02, 0.1).
        Assert.Contains(closed.Points, p => Near(p, 4.98, 0.1));
        var lines = LinesOf(Drawing(f, Plan(f, id)), id);
        Assert.Equal(5, lines.Count); // the closed symbol's four sides and the open line
        Assert.All(lines, l => Assert.Equal(LineRole.Projection, l.Role));
    }

    /// <summary>The same symbol in the type's maps only (the window maps its body): placed where the window puts its type.</summary>
    [Fact]
    public void A_type_plan_map_the_occurrence_does_not_map_is_placed_with_its_body()
    {
        var x = new File2d();
        var origin = x.F.Add("IFCAXIS2PLACEMENT3D", x.F.Add("IFCCARTESIANPOINT", "(0.,0.,0.)"), Null, Null);
        var bodyMap = x.F.Add("IFCREPRESENTATIONMAP", origin, x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 0.9, 0.05, 1.2)));
        var planMap = x.F.Add("IFCREPRESENTATIONMAP", origin, x.Rep(x.PlanBody, "Body", "Curve2D", x.Polyline((0, 0), (0.9, 0))));
        var type = x.F.Add("IFCWINDOWTYPE", File2d.G("wt"), Null, S("WT01"), Null, Null, Null, L([bodyMap, planMap]), Null, Null, E("NOTDEFINED"), E("NOTDEFINED"), Null, Null);
        var op = x.F.Add("IFCCARTESIANTRANSFORMATIONOPERATOR3D", Null, Null, x.F.Add("IFCCARTESIANPOINT", "(1.,0.,0.)"), "1.", Null);
        var win = x.Element("IFCWINDOW", "window", x.Place(5, 0), x.Rep(x.Model, "Body", "MappedRepresentation", x.F.Add("IFCMAPPEDITEM", bodyMap, op)));
        x.F.Add("IFCRELDEFINESBYTYPE", File2d.G("byType"), Null, Null, Null, L([win]), type);
        var f = New();
        Import(f, x, "reference");
        var item = Assert.Single(Plan2d.Read(f.Model.Get(File2d.Gid("window"))!));
        Assert.True(Near(item.Points[0], 6, 0) && Near(item.Points[1], 6.9, 0));
    }

    /// <summary>An element with only a 2D (a symbol, no body) is kept and drawn in its storey's plan.</summary>
    [Fact]
    public void An_element_with_only_2d_is_imported_and_drawn()
    {
        var x = new File2d();
        x.Element("IFCFURNITURE", "chair", x.Place(3, 3), x.Rep(x.PlanBody, "Annotation", "Annotation2D", x.Polyline((0, 0), (0.5, 0), (0.5, 0.5), (0, 0.5), (0, 0))));
        var f = New();
        var r = Import(f, x);
        var id = File2d.Gid("chair");
        var e = f.Model.Get(id)!;
        Assert.True(StoredBodies.Has(e));
        Assert.Empty(f.Session.Reader.Geometry(id)!.Parts);
        Assert.Equal(1, r.ReferencesByClass?.GetValueOrDefault(e.Class));
        var lines = LinesOf(Drawing(f, Plan(f, id)), id);
        Assert.Equal(4, lines.Count);
        Assert.All(lines, l => Assert.Equal(LineRole.Projection, l.Role)); // no body: never cut
        Assert.Contains(Views.Render(f.Session.Reader, Plan(f, id)).Marks, m => m.Owner == id && m.Lines.Count == 4);
    }

    /// <summary>A proxy with a body and its footprint, and a chair drawn only in 2D.</summary>
    static File2d WithAndOnly2d()
    {
        var x = new File2d();
        x.Element("IFCBUILDINGELEMENTPROXY", "proxy", x.Place(10, 5),
            x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 2, 1, 3)), x.Rep(x.Model, "FootPrint", "Curve2D", x.Polyline((0, 0), (2, 0), (1, 1), (0, 0))));
        x.Element("IFCFURNITURE", "chair", x.Place(3, 3), x.Rep(x.PlanBody, "Annotation", "Annotation2D", x.Polyline((0, 0), (0.5, 0), (0.5, 0.5), (0, 0.5), (0, 0))));
        x.Element("IFCBUILDINGELEMENTPROXY", "plain", x.Place(0, 0), x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 1, 1, 1)));
        return x;
    }

    /// <summary>The survey tells how many elements carry their own 2D and how many are nothing but 2D, before anything comes in.</summary>
    [Fact]
    public void The_survey_counts_the_2d_a_file_carries()
    {
        var s = IfcSurvey.Of(StepFile.Read(WithAndOnly2d().Bytes()), "plan2d.ifc", 0);
        Assert.Equal(1, (int)s["twoD"]!["elements"]!);
        Assert.Equal(1, (int)s["twoD"]!["only"]!);
    }

    /// <summary>
    /// The 2D left out as the user chose: without the elements' own 2D the proxy comes with its body alone (a plan cuts
    /// it); without the elements drawn only in 2D the chair stays out; the element with neither is untouched.
    /// </summary>
    [Fact]
    public void The_2d_comes_in_only_as_chosen()
    {
        var bytes = WithAndOnly2d().Bytes();
        string proxy = File2d.Gid("proxy"), chair = File2d.Gid("chair"), plain = File2d.Gid("plain");

        var all = New();
        IfcImport.Run(all.Session, bytes, "plan2d.ifc", new IfcImportOptions(), out _);
        Assert.True(Plan2d.Has(all.Model.Get(proxy)!));
        Assert.NotNull(all.Model.Get(chair));

        var no2d = New();
        var r = IfcImport.Run(no2d.Session, bytes, "plan2d.ifc", new IfcImportOptions(Plan2d: false), out _);
        Assert.False(Plan2d.Has(no2d.Model.Get(proxy)!));
        Assert.True(StoredBodies.Has(no2d.Model.Get(proxy)!));
        Assert.NotNull(no2d.Model.Get(chair));
        Assert.Contains(r.Notes, n => n.Contains("1 element(s) brought without their own 2D"));
        Assert.Contains(Drawing(no2d, Plan(no2d, proxy)).Cuts, c => c.Element == proxy);

        var noOnly = New();
        r = IfcImport.Run(noOnly.Session, bytes, "plan2d.ifc", new IfcImportOptions(Only2d: false), out _);
        Assert.Null(noOnly.Model.Get(chair));
        Assert.True(Plan2d.Has(noOnly.Model.Get(proxy)!));
        Assert.Contains(r.Notes, n => n.Contains("1 element(s) drawn only in 2D left out"));

        var none = New();
        IfcImport.Run(none.Session, bytes, "plan2d.ifc", new IfcImportOptions(Plan2d: false, Only2d: false), out _);
        Assert.Null(none.Model.Get(chair));
        Assert.False(Plan2d.Has(none.Model.Get(proxy)!));
        Assert.NotNull(none.Model.Get(plain));
    }

    /// <summary>A curve's colour and font from its styled item; a fill area's colour from its fill area style; a text literal with its height.</summary>
    [Fact]
    public void Styles_fills_and_texts_are_read_and_drawn()
    {
        var x = new File2d();
        var red = x.F.Add("IFCCOLOURRGB", Null, "1.", "0.", "0.");
        var line = x.Polyline((0, 0), (1, 0));
        var font = x.F.Add("IFCDRAUGHTINGPREDEFINEDCURVEFONT", S("dashed"));
        x.F.Add("IFCSTYLEDITEM", line, L([x.F.Add("IFCCURVESTYLE", Null, font, Null, red, Null)]), Null);
        var area = x.F.Add("IFCANNOTATIONFILLAREA", x.Polyline((0, 1), (1, 1), (1, 2), (0, 2), (0, 1)), Null);
        var blue = x.F.Add("IFCCOLOURRGB", Null, "0.", "0.", "1.");
        x.F.Add("IFCSTYLEDITEM", area, L([x.F.Add("IFCFILLAREASTYLE", Null, L([blue]), Null)]), Null);
        var text = x.F.Add("IFCTEXTLITERAL", S("WC"), x.F.Add("IFCAXIS2PLACEMENT2D", x.F.Add("IFCCARTESIANPOINT", "(0.2,0.3)"), Null), E("RIGHT"));
        var fontModel = x.F.Add("IFCTEXTSTYLEFONTMODEL", S("Sans"), L([S("Arial")]), Null, Null, Null, "IFCLENGTHMEASURE(0.2)");
        x.F.Add("IFCSTYLEDITEM", text, L([x.F.Add("IFCTEXTSTYLE", Null, Null, Null, fontModel, Null)]), Null);
        x.Element("IFCBUILDINGELEMENTPROXY", "fixture", x.Place(0, 0), x.Rep(x.PlanBody, "Annotation", "Annotation2D", line, area, text));
        var f = New();
        Import(f, x);
        var id = File2d.Gid("fixture");
        var items = Plan2d.Read(f.Model.Get(id)!);
        var l = Assert.Single(items, i => i.Kind == Plan2dItem.Line);
        Assert.Equal("#ff0000", l.Color);
        Assert.Equal("dashed", l.Pattern);
        var fill = Assert.Single(items, i => i.Kind == Plan2dItem.Fill);
        Assert.Equal("#0000ff", fill.Color);
        var t = Assert.Single(items, i => i.Kind == Plan2dItem.Label);
        Assert.Equal("WC", t.Text);
        Assert.Equal(0.2, t.Height!.Value, 6);
        Assert.True(Near(t.Points[0], 0.2, 0.3));

        var rendered = Views.Render(f.Session.Reader, Plan(f, id));
        Assert.Contains(rendered.Pens, p => p.Stroke == "#ff0000" && p.Dash is { Length: > 0 } && p.Segments.Count == 1);
        Assert.Contains(rendered.Pens, p => p.Layer == "FILL" && p.Fill == "#0000ff" && p.Loops.Count == 1);
        // 0.2 m at 1:100 is 2 mm on paper.
        Assert.Contains(rendered.Pens, p => p.Texts.Any(tx => tx.Text == "WC" && Math.Abs(tx.Height - 2) < 1e-6));
    }

    /// <summary>A view's graphics win over the file's colour: a category's line colour.</summary>
    [Fact]
    public void A_category_colour_wins_over_the_files()
    {
        var x = new File2d();
        var line = x.Polyline((0, 0), (1, 0));
        x.F.Add("IFCSTYLEDITEM", line, L([x.F.Add("IFCCURVESTYLE", Null, Null, Null, x.F.Add("IFCDRAUGHTINGPREDEFINEDCOLOUR", S("red")), Null)]), Null);
        x.Element("IFCBUILDINGELEMENTPROXY", "fixture", x.Place(0, 0), x.Rep(x.PlanBody, "Annotation", "Annotation2D", line));
        var f = New();
        Import(f, x);
        var id = File2d.Gid("fixture");
        var drawing = Drawing(f, Plan(f, id));
        var dl = Assert.Single(LinesOf(drawing, id));
        var red = Pens.Of(drawing, null, new Pens.Options());
        Assert.Contains(red, p => p.Stroke == "#ff0000");
        var green = Pens.Of(drawing, null, new Pens.Options(Categories: new Dictionary<string, CategoryGraphics> { ["other"] = new(LineColor: "#00aa00") }));
        Assert.DoesNotContain(green, p => p.Stroke == "#ff0000");
        Assert.Contains(green, p => p.Stroke == "#00aa00");
    }

    /// <summary>A door from a file without a 2D of its own (a reference): drawn by its cut in plan, no longer left out.</summary>
    [Fact]
    public void A_reference_door_without_2d_is_cut_in_plan()
    {
        var x = new File2d();
        x.Element("IFCDOOR", "door", x.Place(2, 2), x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 0.9, 0.1, 2.1)));
        var f = New();
        Import(f, x, "reference");
        var id = File2d.Gid("door");
        Assert.False(Plan2d.Has(f.Model.Get(id)!));
        var d = Drawing(f, Plan(f, id));
        Assert.Contains(d.Cuts, c => c.Element == id);
        Assert.Contains(d.Lines, l => l.Element == id && l.Role == LineRole.Cut);
    }

    /// <summary>A linked file's elements bring their 2D too, and plans draw it.</summary>
    [Fact]
    public void A_link_draws_its_elements_2d()
    {
        var x = new File2d();
        x.Element("IFCBUILDINGELEMENTPROXY", "proxy", x.Place(0, 0),
            x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 2, 1, 3)), x.Rep(x.Model, "FootPrint", "Curve2D", x.Polyline((0, 0), (2, 0), (1, 1), (0, 0))));
        var f = New();
        var r = IfcImport.Link(f.Session, x.Bytes(), "plan2d.ifc", null, null, out _);
        Assert.NotNull(r.LinkId);
        var id = File2d.Gid("proxy");
        Assert.True(Plan2d.Has(f.Model.Get(id)!));
        Assert.Equal(3, LinesOf(Drawing(f, Plan(f, id)), id).Count);
    }

    /// <summary>Section and elevation representations are counted, not drawn; a plan's are not taken for a body.</summary>
    [Fact]
    public void Section_and_elevation_2d_are_counted()
    {
        var x = new File2d();
        var sectionCtx = x.F.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Annotation"), S("Model"), Derived, Derived, Derived, Derived, x.Model, Null, E("SECTION_VIEW"), Null);
        var elevationCtx = x.F.Add("IFCGEOMETRICREPRESENTATIONSUBCONTEXT", S("Annotation"), S("Model"), Derived, Derived, Derived, Derived, x.Model, Null, E("ELEVATION_VIEW"), Null);
        x.Element("IFCBUILDINGELEMENTPROXY", "proxy", x.Place(0, 0),
            x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 2, 1, 3)),
            x.Rep(sectionCtx, "Annotation", "Annotation2D", x.Polyline((0, 0), (1, 0))),
            x.Rep(elevationCtx, "Annotation", "Annotation2D", x.Polyline((0, 0), (1, 0))));
        var f = New();
        Import(f, x);
        var e = f.Model.Get(File2d.Gid("proxy"))!;
        Assert.False(Plan2d.Has(e)); // nothing for plans: the body is cut
        Assert.Null(e.Attrs[Plan2d.Key]);
    }

    /// <summary>A reference's 2D written back to IFC: a FootPrint representation in a plan subcontext, read again the same.</summary>
    [Fact]
    public void Exported_references_keep_their_2d()
    {
        var x = new File2d();
        x.Element("IFCBUILDINGELEMENTPROXY", "proxy", x.Place(0, 0),
            x.Rep(x.Model, "Body", "SweptSolid", x.Box(0, 0, 2, 1, 3)), x.Rep(x.Model, "FootPrint", "Curve2D", x.Polyline((0, 0), (2, 0), (1, 1), (0, 0))));
        var f = New();
        Import(f, x);
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "out.ifc");
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("'FootPrint','GeometricCurveSet'", text);
        Assert.Contains(".PLAN_VIEW.", text);
        Assert.DoesNotContain("plan2d", text); // not in the intent set
        var g = new Fixture(new ArchitectureModule());
        IfcImport.Run(g.Session, ms.ToArray(), "out.ifc", new IfcImportOptions("reference"), out _);
        var back = Plan2d.Read(g.Model.Get(File2d.Gid("proxy"))!);
        var item = Assert.Single(back);
        Assert.True(item.Footprint && item.Closed && item.Points.Count == 3);
    }

    /// <summary>
    /// A real file's plan with its elements' 2D and without (the view's switch off), as two PDFs, for the eye
    /// (LAPIN_IFC_SAMPLE, LAPIN_IFC_2D_PDF=/path/out.pdf; the second is out-3d.pdf; LAPIN_IFC_MODE as for the import).
    /// The storey drawn is the one with the most elements with 2D.
    /// </summary>
    [Fact]
    public void A_sample_files_2d_drawn_when_asked()
    {
        var path = Environment.GetEnvironmentVariable("LAPIN_IFC_SAMPLE");
        var outPath = Environment.GetEnvironmentVariable("LAPIN_IFC_2D_PDF");
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(outPath) || !File.Exists(path)) return;
        var f = new Fixture(new ArchitectureModule(), new StructureModule(), new Lapin.Drafting.DraftingModule(), new Lapin.Civil.CivilModule(), new Lapin.Phases.PhasesModule(), new Lapin.Studio.StudioModule());
        IfcImport.Run(f.Session, path, Environment.GetEnvironmentVariable("LAPIN_IFC_MODE") is { } mode ? new IfcImportOptions(mode) : null, out _);
        var storey = Spatial.Storeys(f.Model).OrderByDescending(st => f.Model.Entities.Count(e => Plan2d.Has(e) && Spatial.StoreyOf(f.Model, e.Id)?.Id == st.Id)).First();
        var probe = Views.Render(f.Session.Reader, Views.Find(f.Model, "plan:" + storey.Id)!, print: true);
        if (probe.Bounds is not { } b) return;
        var scale = new[] { 20, 50, 100, 200, 500, 1000 }.FirstOrDefault(s => (b.Max.X - b.Min.X) * 1000 / s < 780 && (b.Max.Y - b.Min.Y) * 1000 / s < 520, 2000);
        foreach (var (file, on) in new[] { (outPath, true), (Path.ChangeExtension(outPath, null) + "-3d.pdf", false) })
        {
            var view = (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = storey.Id, ["name"] = on ? "Plan 2D" : "Plan 3D", ["scale"] = scale })!["id"]!;
            f.Run("view.setStyle", new JsonObject { ["id"] = view, ["ifc2d"] = on });
            var sheet = (string)f.Run("sheet.create", new JsonObject { ["name"] = "Plan", ["paper"] = "A1" })!["id"]!;
            var vp = (string)f.Run("sheet.addViewport", new JsonObject { ["id"] = sheet, ["viewId"] = view, ["x"] = 400, ["y"] = 297 })!["viewportId"]!;
            f.Run("sheet.updateViewport", new JsonObject { ["id"] = sheet, ["viewportId"] = vp, ["width"] = 780, ["height"] = 520, ["center"] = new JsonArray((b.Min.X + b.Max.X) / 2, (b.Min.Y + b.Max.Y) / 2) });
            File.WriteAllBytes(file, Sheets.Pdf(f.Session.Reader, [f.Model.Get(sheet)!]));
        }
    }
}
