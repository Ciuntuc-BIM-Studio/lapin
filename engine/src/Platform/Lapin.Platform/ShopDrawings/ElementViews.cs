using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A kind of view anchored to an element (docs/plan-shop-drawings.md §4): the core's (front, top, sections…) and the
/// modules' own (a section across an element, a timber wall's frame elevation, a steel hall's frame elevation and anchor
/// plan), so a drawing rule asks for "the frame elevation" of a timber wall as it asks for "the front" of a plate.
/// <see cref="ElementOf"/> recognises a view of the role (the element it is of, null when it is not); <see cref="Make"/>
/// makes one (or gives the one there is) and returns its id.
/// </summary>
public sealed record ElementViewRole(string Id, string Title, Func<IModelView, Entity, bool> AppliesTo, Func<Entity, string?> ElementOf,
    Func<ICommandContext, Entity, JsonObject, string> Make);

/// <summary>A module's element view roles (<see cref="ElementViewRole"/>).</summary>
public interface IElementViewRoles
{
    IEnumerable<ElementViewRole> Roles();
}

/// <summary>
/// Element views (docs/plan-shop-drawings.md §4, decision 578): a view of an existing kind with
/// <c>of: {element, role, station?, flip?, margin?, depth?}</c> whose plane and window are worked out from the element's frame
/// (<see cref="ElementFrames"/>) on every ask, so it follows the element. The orthographic roles (<see cref="Ortho"/>) are
/// sections whose plane lies just outside the element's box, seeing through all of it; <c>section:x|y|z</c> cuts it square to a
/// frame axis at <c>station</c> (metres from the frame's origin); <c>iso</c> is a 3D view from the frame's south-west, orthographic.
/// The view shows only its <b>subject</b> (<c>subject: [ids]</c>; the element itself by default), the rest hidden or, with
/// <c>context: faded</c>, drawn halftone: a rule of <see cref="VisibilityGraphics"/>, so sheets, PDF, DXF, 3D and mesh exports
/// read it alike. A drawing point of an element view is in the frame's axes about the box's middle, whatever way the element lies.
/// </summary>
public static class ElementViews
{
    public const string Key = "of", SubjectKey = "subject", ContextKey = "context";
    public static readonly string[] Ortho = ["front", "back", "top", "bottom", "left", "right"];
    public static readonly string[] SectionRoles = ["section:x", "section:y", "section:z"];
    public const string Iso = "iso";
    public static readonly string[] Contexts = ["none", "faded"];
    /// <summary>How far round the element's box a view sees (m) unless it says.</summary>
    public const double DefaultMargin = 0.1;
    /// <summary>How far outside the box an orthographic view's plane lies (m), so a face on the box is seen, not cut.</summary>
    const double Clear = 0.002;
    /// <summary>The level of development an element view draws at: connections and every part (decision 404).</summary>
    public const int Detail = PlanDetail.Lod350;

    public static bool IsRole(string? role) => role != null && (Ortho.Contains(role) || SectionRoles.Contains(role) || role == Iso);

    /// <summary>Whether a view is an element view of the core's roles.</summary>
    public static bool Is(Entity view) => view.Attrs[Key] is JsonObject o && (string?)o["element"] is { Length: > 0 } && IsRole((string?)o["role"]);

    public static string? ElementOf(Entity view) => Is(view) ? (string?)view.Attrs[Key]!["element"] : null;
    public static string? RoleOf(Entity view) => Is(view) ? (string?)view.Attrs[Key]!["role"] : null;

    /// <summary>What the view shows (its subject: the ids kept, else the element it is of); null: everything, as any view.</summary>
    public static HashSet<string>? SubjectOf(Entity view)
    {
        if (view.Attrs[SubjectKey] is JsonArray a && a.Select(x => (string?)x).OfType<string>().ToHashSet() is { Count: > 0 } kept) return kept;
        return ElementOf(view) is { } e ? [e] : null;
    }

    /// <summary>How the rest of the model shows in a view with a subject: <c>none</c> (hidden) or <c>faded</c> (halftone).</summary>
    public static string ContextOf(Entity view) => view.Str(ContextKey) == "faded" ? "faded" : "none";

    static JsonObject Of(Entity view) => (JsonObject)view.Attrs[Key]!;

    /// <summary>The element's frame as the view sees it: its box round the element's parts the view gathered (else its geometry's).</summary>
    public static ElementFrame? FrameOf(IModelView m, IServices? services, Entity view, IReadOnlyList<DrawPart> parts)
    {
        if (m.Get(ElementOf(view) ?? "") is not { } e) return null;
        var body = parts.Where(p => p.Element == e.Id).SelectMany(p => p.Solid.Vertices).ToList();
        return ElementFrames.Of(m, services, e, body);
    }

    /// <summary>
    /// How a role sees a frame: the drawing's right and up and the direction it looks, in the frame's axes. Front looks along +Y
    /// (X to the right, Z up), the others round it as their names say; a section square to an axis looks along it (flipped: back).
    /// </summary>
    public static (Vec3 Right, Vec3 Up, Vec3 Dir) Axes(string role, bool flip = false)
    {
        var (x, y, z) = (new Vec3(1, 0, 0), new Vec3(0, 1, 0), Vec3.UnitZ);
        var o = role switch
        {
            "front" => (x, z, y),
            "back" => (-x, z, -y),
            "top" => (x, y, -z),
            "bottom" => (x, -y, z),
            "left" => (-y, z, x),
            "right" => (y, z, -x),
            "section:x" => (-y, z, x),
            "section:y" => (x, z, y),
            "section:z" => (x, y, -z),
            _ => (x, z, y),
        };
        // A section flipped looks the other way: its right turned round with it, so the drawing is not mirrored.
        if (flip && role.StartsWith("section:", StringComparison.Ordinal))
            o = role == "section:z" ? (o.Item1, -o.Item2, -o.Item3) : (-o.Item1, o.Item2, -o.Item3);
        return o;
    }

    /// <summary>The axis a section role cuts square to (0: x, 1: y, 2: z).</summary>
    static int AxisOf(string role) => role switch { "section:y" => 1, "section:z" => 2, _ => 0 };

    static double Comp(Vec3 v, int i) => i switch { 0 => v.X, 1 => v.Y, _ => v.Z };

    /// <summary>The station a section stands at: the view's, held within the box; the box's middle when it says none.</summary>
    public static double Station(ElementFrame f, string role, JsonObject of)
    {
        var i = AxisOf(role);
        var (lo, hi) = (Comp(f.Min, i), Comp(f.Max, i));
        return Math.Clamp(Json.Number(of["station"]) ?? (lo + hi) / 2, lo, hi);
    }

    /// <summary>
    /// The drawing's frame, depth and window for a role on an element frame: the frame's origin on the view's plane in line with
    /// the box's middle (so the middle draws at 0, 0), the window the box's outline plus the margin.
    /// </summary>
    public static (ViewFrame Frame, double Depth, (double, double) Span, (double, double) Rise) Plane(ElementFrame f, string role, JsonObject of)
    {
        var flip = of["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var fl) && fl;
        var (r, u, d) = Axes(role, flip);
        var margin = Json.Number(of["margin"]) is { } mg && mg >= 0 ? mg : DefaultMargin;
        var c = f.Centre;
        var half = (f.Max - f.Min) * 0.5;
        double Half(Vec3 axis) => Math.Abs(axis.X) * half.X + Math.Abs(axis.Y) * half.Y + Math.Abs(axis.Z) * half.Z;
        // Along the look: an orthographic view from just outside the box's near face, through all of it; a section at its station, to the far face.
        var near = Vec3.Dot(c, d) - Half(d);
        var far = Vec3.Dot(c, d) + Half(d);
        double at, depth;
        if (role.StartsWith("section:", StringComparison.Ordinal))
        {
            var i = AxisOf(role);
            var s = Station(f, role, of);
            at = Comp(d, i) * s;
            depth = Json.Number(of["depth"]) is { } dp && dp > 0 ? dp : Math.Max(far - at, 0) + margin;
        }
        else
        {
            at = near - Clear;
            depth = Json.Number(of["depth"]) is { } dp && dp > 0 ? dp : far - near + 2 * Clear + margin;
        }
        // The plane's point in line with the box's middle, in the frame, then in the world.
        var local = c + d * (at - Vec3.Dot(c, d));
        var frame = new ViewFrame(f.World(local), f.Dir(r), f.Dir(u), f.Dir(d));
        var (hr, hu) = (Half(r), Half(u));
        return (frame, depth, (-hr - margin, hr + margin), (-hu - margin, hu + margin));
    }

    /// <summary>The view's spec: its plane from the element's frame as it is now; null with the reason when the element is gone or has no body.</summary>
    public static (ViewSpec? Spec, string? Problem) Spec(IModelView m, Entity view, IReadOnlyList<DrawPart> parts, IServices? services, bool hidden)
    {
        if (m.Get(ElementOf(view) ?? "") is null) return (null, "The element the view is of is gone.");
        if (FrameOf(m, services, view, parts) is not { } f) return (null, "The element the view is of has nothing to draw.");
        var role = RoleOf(view)!;
        var (frame, depth, span, rise) = Plane(f, role, Of(view));
        return (new ViewSpec("section", frame, true, depth, Hidden: hidden) { Span = span, Rise = rise }, null);
    }

    /// <summary>
    /// An iso element view's frame: from the element frame's south-west, above (looking along +X +Y −Z of the frame),
    /// orthographic, the box's middle at the drawing's 0, 0; null for any other view.
    /// </summary>
    public static ViewFrame? IsoFrame(IModelView m, IServices? services, Entity view, IReadOnlyList<DrawPart> parts)
    {
        if (RoleOf(view) != Iso || FrameOf(m, services, view, parts) is not { } f) return null;
        var look = f.Dir(new Vec3(1, 1, -1)).Normalized();
        var right = f.Dir(new Vec3(1, -1, 0)).Normalized();
        var up = f.Dir(new Vec3(1, 1, 2)).Normalized();
        return new ViewFrame(f.World(f.Centre), right, up, look);
    }

    /// <summary>A scale that fits an element of <paramref name="size"/> metres into <paramref name="paperMm"/> of paper: the first of the shop series that does.</summary>
    public static int AutoScale(double size, double paperMm = 240)
    {
        foreach (var s in ShopScales) if (size * 1000 / s <= paperMm) return s;
        return ShopScales[^1];
    }

    /// <summary>The scales a shop drawing takes (1:1 to 1:100), the first that fits.</summary>
    public static readonly int[] ShopScales = [1, 2, 5, 10, 15, 20, 25, 50, 100];

    /// <summary>A role's name for a view's title and the interface.</summary>
    public static string Title(string role) => role switch
    {
        "front" => "Front", "back" => "Back", "top" => "Top", "bottom" => "Bottom", "left" => "Left", "right" => "Right",
        "section:x" => "Section across", "section:y" => "Section along", "section:z" => "Horizontal section", Iso => "Iso",
        _ => role,
    };

    /// <summary>
    /// The attributes of a new element view of the core's roles: <c>of</c> (element, role, station, flip, margin, depth), its kind
    /// (a section, or a 3D view for iso), its scale (asked, else one that fits the element), its subject and context, drawn at
    /// <see cref="Detail"/>; a section square to an axis without hidden lines.
    /// </summary>
    public static JsonObject Attrs(IQueryContext q, Entity element, string role, JsonObject a)
    {
        if (!IsRole(role)) throw new CommandException($"„{role}” is not an element view: front, back, top, bottom, left, right, section:x, section:y, section:z or iso.");
        var of = new JsonObject { ["element"] = element.Id, ["role"] = role };
        if (role.StartsWith("section:", StringComparison.Ordinal) && Json.Number(a["station"]) is { } st) of["station"] = Math.Round(st, 6);
        if (a["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var flip) && flip) of["flip"] = true;
        if (Json.Number(a["margin"]) is { } mg) of["margin"] = mg >= 0 && mg <= 20 ? mg : throw new CommandException("The margin is between 0 and 20 m.");
        if (Json.Number(a["depth"]) is { } dp && dp > 0) of["depth"] = dp;
        var attrs = new JsonObject { ["kind"] = role == Iso ? "3d" : "section", [Key] = of };
        var frame = ElementFrames.Of(q, element);
        if (role != Iso) attrs["scale"] = Json.Number(a["scale"]) is { } sc && sc >= 1 ? sc : AutoScale(frame is { } f ? Math.Max(f.Length, Math.Max(f.Width, f.Height)) : 1);
        else if (Json.Number(a["scale"]) is { } isc && isc >= 1) attrs["scale"] = isc;
        else attrs["scale"] = AutoScale(frame is { } f2 ? Math.Max(f2.Length, Math.Max(f2.Width, f2.Height)) * 1.2 : 1);
        if (role.StartsWith("section:", StringComparison.Ordinal)) attrs["hiddenLines"] = "hide";
        attrs["styleOverrides"] = new JsonObject { ["detail"] = Detail };
        SetSubject(q.Model, attrs, a);
        if ((string?)a["discipline"] is { Length: > 0 } disc) attrs["discipline"] = disc;
        return attrs;
    }

    /// <summary>A view's subject and context from a command's arguments (<c>subject: [ids]</c>, <c>context</c>); an empty subject clears it.</summary>
    static void SetSubject(IModelView m, JsonObject attrs, JsonObject a)
    {
        if (a.ContainsKey(SubjectKey))
        {
            var ids = (a[SubjectKey] as JsonArray)?.Select(x => (string?)x).OfType<string>().Where(s => s.Length > 0).Distinct().ToList() ?? [];
            foreach (var id in ids) if (m.Get(id) is null) throw new CommandException("There is no such element to show.");
            if (ids.Count == 0) attrs.Remove(SubjectKey);
            else attrs[SubjectKey] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray());
        }
        if (a.ContainsKey(ContextKey))
        {
            var c = (string?)a[ContextKey] ?? "none";
            if (!Contexts.Contains(c)) throw new CommandException("The rest of the model is hidden (none) or faded.");
            if (c == "none") attrs.Remove(ContextKey); else attrs[ContextKey] = c;
        }
    }

    /// <summary>Every element view role this product knows: the core's, then the modules'.</summary>
    public static List<ElementViewRole> Roles(IServices services)
    {
        var o = new List<ElementViewRole>();
        foreach (var role in Ortho.Concat(SectionRoles).Append(Iso))
            o.Add(new ElementViewRole(role, Title(role), (_, _) => true, v => RoleOf(v) == role ? ElementOf(v) : null, (ctx, e, a) => Create(ctx, e, role, a).Id));
        o.Add(new ElementViewRole(AcrossSections.Key, "Section across (at a station)",
            (m, e) => e.Attrs["start"] is JsonArray && e.Attrs["end"] is JsonArray || Spatial.StoreyOf(m, e.Id) != null,
            v => AcrossSections.Is(v) ? v.Str(AcrossSections.Key) : null,
            (ctx, e, a) => AcrossSections.Create(ctx, e.Id, Json.Number(a["station"]), Json.Number(a["margin"]), a["flip"] is JsonValue fv && fv.TryGetValue<bool>(out var f) && f).Id));
        foreach (var s in services.All<IElementViewRoles>()) o.AddRange(s.Roles());
        return o;
    }

    /// <summary>The role a view is of an element (the core's, or a module's), and the element; null for a view of no element.</summary>
    public static (ElementViewRole Role, string Element)? RoleOf(IServices services, Entity view)
    {
        foreach (var r in Roles(services))
            if (r.ElementOf(view) is { } e) return (r, e);
        return null;
    }

    /// <summary>A new element view of a core role, added in the command's step.</summary>
    public static Entity Create(ICommandContext ctx, Entity element, string role, JsonObject a)
    {
        var attrs = Attrs(ctx.Query, element, role, a);
        var name = (string?)a["name"] is { Length: > 0 } n ? n : $"{element.Name} · {Title(role)}";
        var v = new Entity(ctx.NewId(), Views.Class, name, attrs);
        ctx.Add(v);
        return v;
    }

    public static void Register(IModuleRegistry r)
    {
        // An element view: {element, role, station?, flip?, margin?, depth?, scale?, name?, subject?, context?} → {id}. A module's role
        // (a frame elevation) is made by its own maker, which may give the view the element has already.
        r.Command("elementView.create", "Element view", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["element"] ?? "") ?? throw new CommandException("Which element is the view of?");
            var roleId = (string?)a["role"] ?? "front";
            var role = Roles(ctx.Services).FirstOrDefault(x => x.Id == roleId) ?? throw new CommandException($"„{roleId}” is not a view of an element here.");
            if (!role.AppliesTo(ctx.Model, e)) throw new CommandException($"„{e.Name}” has no {role.Title.ToLowerInvariant()}.");
            return new JsonObject { ["id"] = role.Make(ctx, e, a) };
        });

        // A view's subject (a kept isolate): {viewId, subject: [ids] (empty: the whole model again), context?: none | faded}.
        r.Command("view.setSubject", "Show only", (ctx, a) =>
        {
            var view = ctx.Model.Get((string?)a["viewId"] ?? "") is { Class: Views.Class } v ? v : throw new CommandException("Show only is kept on a view of its own.");
            var attrs = view.CloneAttrs();
            SetSubject(ctx.Model, attrs, a);
            ctx.Replace(view.With(attrs: attrs));
            return null;
        });

        // The element views an element can have: {id} → [{role, title}], and the views it has: [{id, role}].
        r.Query("elementView.roles", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("There is no such element.");
            var roles = Roles(q.Services);
            var views = new JsonArray();
            foreach (var v in q.Model.OfClass(Views.Class))
                foreach (var role in roles)
                    if (role.ElementOf(v) == e.Id) { views.Add((JsonNode)new JsonObject { ["id"] = v.Id, ["role"] = role.Id, ["name"] = v.Name }); break; }
            return new JsonObject
            {
                ["roles"] = new JsonArray(roles.Where(x => x.AppliesTo(q.Model, e)).Select(x => (JsonNode)new JsonObject { ["role"] = x.Id, ["title"] = x.Title }).ToArray()),
                ["views"] = views,
            };
        });

        // An element's frame (its origin, axes and box): {id} → the frame, or null when it has no body.
        r.Query("element.frame", (q, a) =>
        {
            var e = q.Model.Get((string?)a["id"] ?? "") ?? throw new CommandException("There is no such element.");
            return ElementFrames.Of(q, e)?.ToJson();
        });
    }
}
