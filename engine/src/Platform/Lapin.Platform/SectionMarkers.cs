using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Where a section's marker shows (docs/sections.md, decision 223): in every plan, and in every other section and
/// elevation whose window its plane crosses, as a trace (a fine dashed line where the plane cuts the view, the name
/// and an arrow looking the section's way at the top), never in itself. Each section sits on a marker layer
/// (<c>markerLayer</c>, "Sections" unless set); each view says what it hides, by section or by layer
/// (<c>sectionMarkers: {hidden: [ids], hiddenLayers: [names]}</c>; everything shown when absent).
/// </summary>
public static class SectionMarkers
{
    public const string DefaultLayer = "Sections";
    public const string Key = "sectionMarkers";

    public static string LayerOf(Entity section) => section.Str("markerLayer") is { Length: > 0 } l ? l : DefaultLayer;

    /// <summary>What the view hides: the sections by id, and the layers by name.</summary>
    public static (HashSet<string> Hidden, HashSet<string> HiddenLayers) Of(Entity view)
    {
        static HashSet<string> Set(JsonNode? n) => n is JsonArray a ? a.Select(x => (string?)x).OfType<string>().ToHashSet() : [];
        var o = view.Attrs[Key] as JsonObject;
        return (Set(o?["hidden"]), Set(o?["hiddenLayers"]));
    }

    public static bool Shows(Entity view, Entity section)
    {
        var (hidden, layers) = Of(view);
        return !hidden.Contains(section.Id) && !layers.Contains(LayerOf(section));
    }

    static JsonObject? ToJson(HashSet<string> hidden, HashSet<string> layers)
    {
        if (hidden.Count == 0 && layers.Count == 0) return null;
        var o = new JsonObject();
        if (hidden.Count > 0) o["hidden"] = new JsonArray(hidden.Order().Select(x => (JsonNode)x).ToArray());
        if (layers.Count > 0) o["hiddenLayers"] = new JsonArray(layers.Order().Select(x => (JsonNode)x).ToArray());
        return o;
    }

    /// <summary>The views that keep what they show: plans, sections and elevations of their own (never an implicit plan, nor the 3D).</summary>
    public static bool Keeps(Entity view) => view.Class == Views.Class && view.Str("kind") is "plan" or "section" or "elevation";

    static Entity Target(IModelView m, string? id) =>
        m.Get(id ?? "") is { } v && Keeps(v) ? v : throw new CommandException("Section markers are shown or hidden on a plan, a section or an elevation of its own.");

    /// <summary>
    /// Where the sections' planes cut this section or elevation: one trace per piece of each other section, within the
    /// view's extent (a developed view piece by piece), a spanned piece (one of a jogged section) only between its ends.
    /// </summary>
    public static IEnumerable<ViewMark> Traces(ViewContext c)
    {
        var m = c.Model;
        if (c.Kind is not ("section" or "elevation") || c.View.Str("kind") == RefPlanes.Kind || c.Drawing.Bounds is not { } bounds) yield break;
        var sections = m.OfClass(Views.Class).Where(v => v.Str("kind") == "section" && v.Id != c.View.Id && Shows(c.View, v)).ToList();
        if (sections.Count == 0) yield break;
        var k = c.Scale / 100;
        var text = Standards.ResolveText(c.Book, []);
        var margin = 1.0 * k;
        var (x0, x1, y0, y1) = (bounds.Min.X - margin, bounds.Max.X + margin, bounds.Min.Y - margin, bounds.Max.Y + margin);
        var frames = Views.ViewPlanes(c, x0, x1);
        var (arrow, head) = (0.7 * k, 0.25 * k);
        foreach (var v in sections)
        {
            var (path, _, _) = SectionPaths.Resolve(m, c.Services, v);
            if (path == null) continue;
            var pieces = SectionPaths.Pieces(path);
            var spanned = pieces.Count > 1;
            var mk = new ViewMark(v.Id);
            foreach (var piece in pieces)
            {
                var along = (piece.B - piece.A) * (1 / piece.Length);
                var look = along.Perp;
                var look3 = new Vec3(look.X, look.Y, 0);
                var plane = new WorkPlane(new Vec3(piece.A.X, piece.A.Y, 0), new Vec3(along.X, along.Y, 0), Vec3.UnitZ, look3);
                foreach (var (view, fx0, fx1) in frames)
                {
                    if (fx1 - fx0 < 1e-6 || Views.PlaneCut(view, plane, fx0, fx1, y0, y1) is not var (a, b)) continue;
                    if (spanned && Clip(view, piece, a, b) is var (ca, cb)) (a, b) = (ca, cb);
                    else if (spanned) continue;
                    mk.Line(a, b, "SECTION", Pens.Default.Hatch, "#333333", LineStyles.DashOf("dashed"));
                    // The arrow at the end that lies up (or to the right), across the trace the way the section looks; the name past its tip.
                    var top = b.Y > a.Y + 1e-9 || (Math.Abs(b.Y - a.Y) <= 1e-9 && b.X >= a.X) ? b : a;
                    var dir = new Vec2(Vec3.Dot(look3, view.Right), Vec3.Dot(look3, view.Up)).Normalized();
                    var tip = top + dir * arrow;
                    mk.Line(top, tip, "SECTION", Pens.Default.Seen, "#333333")
                      .Line(tip, tip - dir * head + dir.Perp * (head * 0.5), "SECTION", Pens.Default.Seen, "#333333")
                      .Line(tip, tip - dir * head - dir.Perp * (head * 0.5), "SECTION", Pens.Default.Seen, "#333333")
                      .Text(new PenText(tip + dir * (0.35 * k), v.Name, text.Height, Anchor: dir.X < -0.5 ? "end" : "start", Baseline: "middle", Font: text.Font), "#333333");
                    mk.Snaps.AddRange([a, b]);
                }
            }
            if (mk.Lines.Count > 0) yield return mk;
        }
    }

    /// <summary>The trace held between the piece's ends (its stations 0..length along it); null when it lies wholly past one.</summary>
    static (Vec2 A, Vec2 B)? Clip(WorkPlane view, SectionPaths.Piece piece, Vec2 a, Vec2 b)
    {
        var along = (piece.B - piece.A) * (1 / piece.Length);
        double T(Vec2 p) { var w = view.World(p); return Vec2.Dot(new Vec2(w.X, w.Y) - piece.A, along); }
        var (ta, tb) = (T(a), T(b));
        if (Math.Abs(tb - ta) < 1e-9) return ta >= -1e-9 && ta <= piece.Length + 1e-9 ? (a, b) : null;
        var (s0, s1) = ((0 - ta) / (tb - ta), (piece.Length - ta) / (tb - ta));
        if (s0 > s1) (s0, s1) = (s1, s0);
        (s0, s1) = (Math.Max(0, s0), Math.Min(1, s1));
        if (s0 >= s1) return null;
        return (a + (b - a) * s0, a + (b - a) * s1);
    }

    public static void Register(IModuleRegistry r)
    {
        // One section's marker shown or hidden in a view: {viewId, sectionId, shown}.
        r.Command("view.showSection", "Section marker", (ctx, a) =>
        {
            var v = Target(ctx.Model, (string?)a["viewId"]);
            var s = ctx.Model.Get((string?)a["sectionId"] ?? "") is { Class: Views.Class } x && x.Str("kind") == "section" ? x : throw new CommandException("Not a section.");
            var shown = a["shown"] is JsonValue sv && sv.TryGetValue<bool>(out var b) ? b : throw new CommandException("Shown, or not?");
            var (hidden, layers) = Of(v);
            if (shown) hidden.Remove(s.Id); else hidden.Add(s.Id);
            Write(ctx, v, hidden, layers);
            return null;
        });

        // A marker layer shown or hidden in a view: {viewId, layer, shown}.
        r.Command("view.showSectionLayer", "Section marker layer", (ctx, a) =>
        {
            var v = Target(ctx.Model, (string?)a["viewId"]);
            var layer = (string?)a["layer"] is { Length: > 0 } l ? l : throw new CommandException("Which layer?");
            var shown = a["shown"] is JsonValue sv && sv.TryGetValue<bool>(out var b) ? b : throw new CommandException("Shown, or not?");
            var (hidden, layers) = Of(v);
            if (shown) layers.Remove(layer); else layers.Add(layer);
            Write(ctx, v, hidden, layers);
            return null;
        });

        // What a view shows of the sections' markers: each section with its layer, whether this view shows it and how many
        // views hide it; the layers with whether this view shows them.
        r.Query("view.sectionMarkers", (q, a) =>
        {
            var m = q.Model;
            var v = Views.Find(m, (string?)a["viewId"] ?? "") ?? throw new CommandException("There is no such view.");
            var (hidden, hiddenLayers) = Of(v);
            var views = m.OfClass(Views.Class).Where(Keeps).Select(Of).ToList();
            var sections = m.OfClass(Views.Class).Where(x => x.Str("kind") == "section" && x.Id != v.Id).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
            var layers = sections.Select(LayerOf).Append(DefaultLayer).Distinct().OrderBy(l => l == DefaultLayer ? 0 : 1).ThenBy(l => l, StringComparer.OrdinalIgnoreCase).ToList();
            return new JsonObject
            {
                ["sections"] = new JsonArray(sections.Select(s => (JsonNode)new JsonObject
                {
                    ["id"] = s.Id, ["name"] = s.Name, ["layer"] = LayerOf(s),
                    ["shown"] = !hidden.Contains(s.Id) && !hiddenLayers.Contains(LayerOf(s)),
                    ["hiddenIn"] = views.Count(w => w.Hidden.Contains(s.Id) || w.HiddenLayers.Contains(LayerOf(s))),
                }).ToArray()),
                ["layers"] = new JsonArray(layers.Select(l => (JsonNode)new JsonObject { ["name"] = l, ["shown"] = !hiddenLayers.Contains(l) }).ToArray()),
            };
        });
    }

    static void Write(ICommandContext ctx, Entity v, HashSet<string> hidden, HashSet<string> layers)
    {
        var attrs = v.CloneAttrs();
        if (ToJson(hidden, layers) is { } o) attrs[Key] = o; else attrs.Remove(Key);
        ctx.Replace(v.With(attrs: attrs));
    }
}
