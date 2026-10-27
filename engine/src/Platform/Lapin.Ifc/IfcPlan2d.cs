using Lapin.Geometry;
using Lapin.Platform;

namespace Lapin.Ifc;

/// <summary>
/// A product's own 2D representations (docs/spec-ifc.md, Reprezentările 2D):
/// what another program drew for its plans — a footprint, a door's swing, a
/// fixture's symbol — read from its representations and from its type's maps
/// (through IfcMappedItem), placed in world metres and laid flat on the plan.
/// A representation is a plan's when its identifier is FootPrint, or its
/// context is a plan's (a subcontext whose TargetView is PLAN_VIEW, or a
/// context whose ContextType is Plan, as the window type of hello-wall.ifc:
/// identifier Body, context Body/Plan/PLAN_VIEW). Curves, curve sets,
/// annotation fill areas and text literals are read, with the colours and
/// patterns of their curve, fill area and text styles. Section and elevation
/// representations (SECTION_VIEW, ELEVATION_VIEW) are counted, not placed.
/// </summary>
public sealed partial class IfcModel
{
    /// <summary>What a styled item says of a 2D item: its line colour and pattern, its fill, its text height (m).</summary>
    sealed record Style2d(string? Line, string? Pattern, string? Fill, double? TextHeight)
    {
        public Style2d Over(Style2d? under) => under == null ? this
            : new(Line ?? under.Line, Pattern ?? under.Pattern, Fill ?? under.Fill, TextHeight ?? under.TextHeight);
    }

    Dictionary<int, Style2d>? _styles2d;
    Dictionary<int, int>? _typeOf;

    /// <summary>Which view a representation is drawn for: "plan", "section", "elevation", or null (a body, an axis, a 3D annotation…).</summary>
    string? View2d(StepEntity? rep)
    {
        if (rep == null) return null;
        var id = (Str(rep[1]) ?? "").ToUpperInvariant();
        if (id is "AXIS" or "BOX" or "LIGHTSOURCE" or "PROFILE" or "CLEARANCE" or "SURFACE" or "REFERENCE") return null;
        var ctx = File.Get(rep[0]);
        string? target = null, type = null;
        if (ctx?.Type == "IFCGEOMETRICREPRESENTATIONSUBCONTEXT")
        {
            target = (ctx[8] as StepEnum)?.Value;
            type = Str(ctx[1]) ?? Str(File.Get(ctx[6])?[1]);
        }
        else if (ctx?.Type == "IFCGEOMETRICREPRESENTATIONCONTEXT") type = Str(ctx[1]);
        if (target == "SECTION_VIEW") return "section";
        if (target == "ELEVATION_VIEW") return "elevation";
        if (target is "REFLECTED_PLAN_VIEW" or "MODEL_VIEW" or "GRAPH_VIEW" && id != "FOOTPRINT") return null;
        if (id == "FOOTPRINT" || target == "PLAN_VIEW" || string.Equals(type, "Plan", StringComparison.OrdinalIgnoreCase)) return "plan";
        return null;
    }

    /// <summary>
    /// The product's 2D for plans, in world metres (its placement, the mapped
    /// items' transforms and the 2D placements applied, then laid flat), and
    /// how many section and elevation representations it has besides.
    /// </summary>
    public (List<Plan2dItem> Plan, int Section, int Elevation) Drawing2d(int product)
    {
        var plan = new List<Plan2dItem>();
        int section = 0, elevation = 0;
        var e = File.Entities.GetValueOrDefault(product);
        if (e == null || e.Args.Count < 7) return (plan, 0, 0);
        var place = Placement(e[5]);
        var usedMaps = new HashSet<int>();
        Xf? typeAt = null; // where the occurrence puts its type's maps (its first mapped item)
        foreach (var rep in Items(File.Get(e[6])?[2]))
        {
            var view = View2d(rep);
            foreach (var item in Items(rep[3]))
            {
                if (item.Type == "IFCMAPPEDITEM" && File.Get(item[0]) is { } map)
                {
                    usedMaps.Add(map.Id);
                    var at = place.Then(Operator(File.Get(item[1])));
                    typeAt ??= at;
                    var mapped = File.Get(map[1]);
                    var v = view ?? View2d(mapped);
                    if (v == "plan") Read2d(item, place, plan, null, IsFootprint(rep) || IsFootprint(mapped));
                    else if (v == "section") section++;
                    else if (v == "elevation") elevation++;
                    continue;
                }
                if (view == "plan") Read2d(item, place, plan, null, IsFootprint(rep));
            }
            if (view == "section") section++;
            else if (view == "elevation") elevation++;
        }
        // The type's own plan maps the occurrence does not map itself: placed where it puts the type's body (else at its own placement).
        if (plan.Count == 0 && TypeOf(product) is { } type && type.Args.Count > 6)
            foreach (var map in Items(type[6]))
            {
                if (usedMaps.Contains(map.Id) || File.Get(map[1]) is not { } mapped || View2d(mapped) != "plan") continue;
                var xf = (typeAt ?? place).Then(Axis2(map[0]));
                foreach (var sub in Items(mapped[3])) Read2d(sub, xf, plan, null, IsFootprint(mapped));
            }
        return (plan, section, elevation);
    }

    static bool IsFootprint(StepEntity? rep) => string.Equals(Str(rep?[1]), "FootPrint", StringComparison.OrdinalIgnoreCase);

    StepEntity? TypeOf(int product)
    {
        if (_typeOf == null)
        {
            _typeOf = [];
            foreach (var r in File.OfType("IFCRELDEFINESBYTYPE"))
                foreach (var o in Refs(r[4])) _typeOf[o] = Ref(r[5]);
        }
        return _typeOf.TryGetValue(product, out var t) ? File.Entities.GetValueOrDefault(t) : null;
    }

    static Vec2 OnPlan(Vec3 p) => new(p.X, p.Y);

    /// <summary>One representation item into plan items: curves, curve sets, fill areas, texts; mapped items and styled occurrences opened.</summary>
    void Read2d(StepEntity item, Xf at, List<Plan2dItem> o, Style2d? inherited, bool footprint, int depth = 0)
    {
        if (depth > 8) return;
        var style = Styles2d().GetValueOrDefault(item.Id)?.Over(inherited) ?? inherited;
        switch (item.Type)
        {
            case "IFCMAPPEDITEM":
            {
                if (File.Get(item[0]) is not { } map) return;
                var xf = at.Then(Operator(File.Get(item[1]))).Then(Axis2(map[0]));
                foreach (var sub in Items(File.Get(map[1])?[3])) Read2d(sub, xf, o, style, footprint, depth + 1);
                return;
            }
            case "IFCGEOMETRICCURVESET" or "IFCGEOMETRICSET":
                foreach (var sub in Items(item[0])) Read2d(sub, at, o, style, footprint, depth + 1);
                return;
            case "IFCSTYLEDITEM" or "IFCANNOTATIONCURVEOCCURRENCE" or "IFCANNOTATIONFILLAREAOCCURRENCE" or "IFCANNOTATIONTEXTOCCURRENCE" or "IFCANNOTATIONSYMBOLOCCURRENCE":
                if (File.Get(item[0]) is { } inner) Read2d(inner, at, o, ParseStyle(item[1]).Over(style), footprint, depth + 1);
                return;
            case "IFCANNOTATIONFILLAREA":
            {
                var outer = Curve(File.Get(item[0]));
                if (outer.Count < 3) return;
                var loops = new List<IReadOnlyList<Vec2>> { outer.Select(p => OnPlan(at.Apply(p))).ToList() };
                foreach (var h in Items(item[1])) { var pts = Curve(h); if (pts.Count >= 3) loops.Add(pts.Select(p => OnPlan(at.Apply(p))).ToList()); }
                if (style?.Fill is { } fill) o.Add(new Plan2dItem(Plan2dItem.Fill, true, loops, fill));
                else foreach (var l in loops) o.Add(new Plan2dItem(Plan2dItem.Line, true, [l], style?.Line, style?.Pattern, Footprint: footprint));
                return;
            }
            case "IFCTEXTLITERAL" or "IFCTEXTLITERALWITHEXTENT":
            {
                if (Str(item[0]) is not { Length: > 0 } text) return;
                var xf = at.Then(Axis2(item[1]));
                var scale = xf.Y.Length;
                double? height = style?.TextHeight is { } th ? th * scale
                    : item.Type == "IFCTEXTLITERALWITHEXTENT" && File.Get(item[3]) is { } ext && Num(ext[1]) > 0 ? Num(ext[1]) * _len * scale : null;
                var angle = Math.Atan2(xf.X.Y, xf.X.X) * 180 / Math.PI;
                o.Add(new Plan2dItem(Plan2dItem.Label, false, [[OnPlan(xf.O)]], style?.Line, null, text, height, angle));
                return;
            }
        }
        var curve = Curve(item, out var closed);
        if (curve.Count < 2) return;
        var flat = curve.Select(p => OnPlan(at.Apply(p))).ToList();
        var clean = new List<Vec2>();
        foreach (var p in flat) if (clean.Count == 0 || (clean[^1] - p).Length > 1e-6) clean.Add(p);
        if (clean.Count < 2) return; // a curve seen end on (vertical): nothing in plan
        o.Add(new Plan2dItem(Plan2dItem.Line, closed && clean.Count > 2, [clean], style?.Line, style?.Pattern, Footprint: footprint));
    }

    /// <summary>Every styled item's 2D style, by the item it styles (IFC2X3's annotation occurrences too).</summary>
    Dictionary<int, Style2d> Styles2d()
    {
        if (_styles2d != null) return _styles2d;
        _styles2d = [];
        foreach (var s in File.Entities.Values)
        {
            if (s.Type is not ("IFCSTYLEDITEM" or "IFCANNOTATIONCURVEOCCURRENCE" or "IFCANNOTATIONFILLAREAOCCURRENCE" or "IFCANNOTATIONTEXTOCCURRENCE")) continue;
            if (s[0] is not StepRef item) continue;
            var st = ParseStyle(s[1]);
            _styles2d[item.Id] = _styles2d.TryGetValue(item.Id, out var had) ? st.Over(had) : st;
        }
        return _styles2d;
    }

    Style2d ParseStyle(object? styles)
    {
        string? line = null, pattern = null, fill = null;
        double? height = null;
        foreach (var s in Items(styles))
            switch (s.Type)
            {
                case "IFCPRESENTATIONSTYLEASSIGNMENT":
                {
                    var inner = ParseStyle(s[0]);
                    line ??= inner.Line; pattern ??= inner.Pattern; fill ??= inner.Fill; height ??= inner.TextHeight;
                    break;
                }
                case "IFCCURVESTYLE":
                    line ??= DrawingColour(File.Get(s[3]), paper: true);
                    pattern ??= Pattern(File.Get(s[1]));
                    break;
                case "IFCFILLAREASTYLE":
                    foreach (var f in Items(s[1])) fill ??= DrawingColour(f, paper: false);
                    break;
                case "IFCSURFACESTYLE":
                    foreach (var r in Items(s[2])) fill ??= DrawingColour(File.Get(r[0]), paper: false);
                    break;
                case "IFCTEXTSTYLE":
                    if (File.Get(s[1]) is { } look) line ??= DrawingColour(File.Get(look[0]), paper: true);
                    if (File.Get(s[3]) is { Type: "IFCTEXTSTYLEFONTMODEL" } font && font[5] is StepTyped { Type: "IFCLENGTHMEASURE" or "IFCPOSITIVELENGTHMEASURE" } size && Num(size) > 0)
                        height ??= Num(size) * _len;
                    break;
            }
        return new Style2d(line, pattern, fill, height);
    }

    static readonly Dictionary<string, string> Predefined = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000", ["red"] = "#ff0000", ["green"] = "#00ff00", ["blue"] = "#0000ff", ["yellow"] = "#ffff00",
        ["magenta"] = "#ff00ff", ["cyan"] = "#00ffff", ["white"] = "#ffffff",
    };

    /// <summary>A colour as #rrggbb; a line's white (a screen's convention) is the paper's ink, so none.</summary>
    string? DrawingColour(StepEntity? c, bool paper)
    {
        string? hex = c?.Type switch
        {
            "IFCCOLOURRGB" => $"#{(int)Math.Round(Math.Clamp(Num(c[1]), 0, 1) * 255):x2}{(int)Math.Round(Math.Clamp(Num(c[2]), 0, 1) * 255):x2}{(int)Math.Round(Math.Clamp(Num(c[3]), 0, 1) * 255):x2}",
            "IFCDRAUGHTINGPREDEFINEDCOLOUR" => Str(c[0]) is { } n && Predefined.TryGetValue(n, out var h) ? h : null,
            _ => null,
        };
        return paper && hex == "#ffffff" ? null : hex;
    }

    /// <summary>A curve font as one of the drawing's patterns (dashed, dotted, center, hidden); continuous: none.</summary>
    string? Pattern(StepEntity? font)
    {
        if (font == null) return null;
        if (font.Type == "IFCCURVESTYLEFONTANDSCALING") return Pattern(File.Get(font[1]));
        var name = (Str(font[0]) ?? "").ToLowerInvariant();
        if (name.Contains("continuous") || name.Contains("solid")) return null;
        if (name.Contains("chain") || (name.Contains("dash") && name.Contains("dot")) || name.Contains("center") || name.Contains("centre")) return "center";
        if (name.Contains("hidden")) return "hidden";
        if (name.Contains("dot")) return "dotted";
        if (name.Contains("dash")) return "dashed";
        return font.Type == "IFCCURVESTYLEFONT" && font[1] is List<object?> { Count: >= 2 } ? "dashed" : null;
    }
}
