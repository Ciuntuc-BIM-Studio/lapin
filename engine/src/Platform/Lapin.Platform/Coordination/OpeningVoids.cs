using System.Text;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Coordination;

/// <summary>
/// One opening as a solid void (a tester's request, decision 289): the body an
/// opening takes out of its host, exactly through the host's whole thickness
/// (finishes included), grown or shrunk by an offset on its sides. What a clash
/// tool tests MEP against ("x completely inside y"): written to IFC as an
/// IfcBuildingElementProxy PROVISIONFORVOID on request, drawn in 3D, and what
/// Lapin's own check (<see cref="OpeningClashes"/>) measures pipes and ducts by.
/// <para>
/// <see cref="OpeningId"/> is the IfcOpeningElement (null for a roof window or
/// dormer, which are the roof's own); <see cref="FillId"/> the door or window
/// in it; <see cref="Kind"/> one of <see cref="OpeningVoids.Kinds"/>.
/// <see cref="Shape"/> as Pset_ProvisionForVoid names it: Rectangle, Round or Undefined.
/// </para>
/// </summary>
public sealed record OpeningVoid(string Key, string HostId, string? OpeningId, string? FillId, string Kind, string Shape, double Width, double Height, double Depth, Solid Solid, string? StoreyId)
{
    /// <summary>The void's mark (V1, V2…), numbered storey by storey, as <see cref="OpeningVoids.Of"/> numbers them.</summary>
    public string Mark { get; init; } = "";
    /// <summary>A round void's diameter (its width), else null.</summary>
    public double? Diameter => Shape == OpeningVoids.Round ? Width : null;
    /// <summary>A wall opening's wall direction where it stands (plan, unit), else null: its label's directions in a plan (decision 292).</summary>
    public Vec2? Along { get; init; }
    /// <summary>What a click on it selects: its opening, else its host.</summary>
    public string SelectId => OpeningId ?? HostId;
    /// <summary>The void's GlobalId as an element of its own (the IFC proxy, a mesh export's element): derived from its key, the same at every export.</summary>
    public string GlobalId => OpeningVoids.DerivedId(Key, "void");
}

/// <summary>The voids a module's hosts have: walls (bare openings, doors, windows), floors, roofs. A service of the module that owns the hosts.</summary>
public interface IOpeningVoids
{
    /// <summary>Every opening's void, its sides moved out by <paramref name="offset"/> (metres; negative: in).</summary>
    IEnumerable<OpeningVoid> Voids(IModelView m, double offset);
}

/// <summary>Which voids are wanted and how much bigger (or smaller) than their openings.</summary>
public sealed record VoidOptions(IReadOnlySet<string> Kinds, double Offset = 0)
{
    public static readonly VoidOptions All = new(OpeningVoids.Kinds.ToHashSet());

    /// <summary>From a command's arguments ({kinds: [...], offset: m}), over the project's settings.</summary>
    public static VoidOptions From(IModelView m, JsonObject? a)
    {
        var s = OpeningVoids.Settings(m);
        var kinds = OpeningVoids.KindsOf(a?["kinds"]) ?? OpeningVoids.KindsOf(s["kinds"]) ?? OpeningVoids.Kinds.ToHashSet();
        var offset = Json.Number(a?["offset"]) ?? Json.Number(s["offset"]) ?? 0;
        return new(kinds, Math.Clamp(offset, -0.5, 0.5));
    }
}

/// <summary>
/// The voids of the project's openings (decision 289), their settings kept on
/// the IfcProject (<c>coordination</c>: kinds, offset, clearance, empty) and the
/// queries the interface draws and lists them with.
/// </summary>
public static class OpeningVoids
{
    public const string Key = "coordination";
    public const string Wall = "wall", Door = "door", Window = "window", Slab = "slab", Roof = "roof";
    /// <summary>The kinds of opening a void is made for, in the order the interface lists them.</summary>
    public static readonly string[] Kinds = [Wall, Door, Window, Slab, Roof];
    public const string Rectangle = "Rectangle", Round = "Round", Undefined = "Undefined";
    /// <summary>The colour voids are drawn in (3D, mesh exports): a clear violet no material has.</summary>
    public const string Color = "#b05cff";

    public static JsonObject Settings(IModelView m) => Spatial.Project(m)?.Attrs[Key] as JsonObject ?? [];

    public static HashSet<string>? KindsOf(JsonNode? n) =>
        n is JsonArray a ? a.Select(x => (string?)x).OfType<string>().Where(Kinds.Contains).ToHashSet() : null;

    /// <summary>A stable GlobalId derived from a seed and a key (MD5, as the IFC export derives its ids).</summary>
    public static string DerivedId(string seed, string key)
    {
        var hash = Md5.HashData(Encoding.UTF8.GetBytes($"{seed}/{key}"));
        return Model.GlobalId.FromGuid(new Guid(hash));
    }

    /// <summary>Every void the product's modules give, of the kinds asked, marked V1, V2… storey by storey (lowest first), then by host and place.</summary>
    public static List<OpeningVoid> Of(IQueryContext q, VoidOptions o) => Of(q.Model, q.Services, o);

    public static List<OpeningVoid> Of(IModelView m, IServices services, VoidOptions o)
    {
        var all = services.All<IOpeningVoids>().SelectMany(s => s.Voids(m, o.Offset)).Where(v => o.Kinds.Contains(v.Kind)).ToList();
        var storeys = Spatial.Storeys(m).Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var ordered = all.Select(v => (v, b: Measure.Bounds(v.Solid)))
            .OrderBy(x => x.v.StoreyId is { } s && storeys.TryGetValue(s, out var i) ? i : int.MaxValue)
            .ThenBy(x => x.b?.Min.Z ?? 0).ThenBy(x => x.b?.Min.Y ?? 0).ThenBy(x => x.b?.Min.X ?? 0).Select(x => x.v).ToList();
        return ordered.Select((v, i) => v with { Mark = $"V{i + 1}" }).ToList();
    }

    public static JsonObject ToJson(OpeningVoid v, IModelView m, bool mesh)
    {
        var o = new JsonObject
        {
            ["key"] = v.Key, ["mark"] = v.Mark, ["kind"] = v.Kind, ["shape"] = v.Shape,
            ["hostId"] = v.HostId, ["host"] = m.Get(v.HostId)?.Name, ["openingId"] = v.OpeningId, ["fillId"] = v.FillId, ["fill"] = v.FillId is { } f ? m.Get(f)?.Name : null,
            ["selectId"] = v.SelectId, ["storeyId"] = v.StoreyId, ["globalId"] = v.GlobalId,
            ["width"] = Math.Round(v.Width, 4), ["height"] = Math.Round(v.Height, 4), ["depth"] = Math.Round(v.Depth, 4),
        };
        if (v.Diameter is { } d) o["diameter"] = Math.Round(d, 4);
        if (mesh)
        {
            var t = Tessellator.Tessellate(v.Solid);
            o["positions"] = new JsonArray(t.Positions.Select(x => (JsonNode)Math.Round(x, 4)).ToArray());
            o["indices"] = new JsonArray(t.Indices.Select(x => (JsonNode)x).ToArray());
        }
        return o;
    }

    static void Write(ICommandContext ctx, Action<JsonObject> change)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var attrs = project.CloneAttrs();
        var c = (attrs[Key] as JsonObject)?.DeepClone() as JsonObject ?? new JsonObject();
        change(c);
        attrs[Key] = c;
        ctx.Replace(project.With(attrs: attrs));
    }

    public static void Register(IModuleRegistry r)
    {
        // The settings: the kinds of opening voids are made for, their offset, the clearance a pipe must keep inside one, whether empty voids are reported.
        r.Command("coordination.setSettings", "Coordination settings", (ctx, a) =>
        {
            Write(ctx, c =>
            {
                if (KindsOf(a["kinds"]) is { } kinds) c["kinds"] = new JsonArray(Kinds.Where(kinds.Contains).Select(k => (JsonNode)k).ToArray());
                if (Json.Number(a["offset"]) is { } off) c["offset"] = Math.Clamp(off, -0.5, 0.5);
                if (Json.Number(a["clearance"]) is { } cl) c["clearance"] = Math.Clamp(cl, 0, 0.5);
                if (a["empty"] is JsonValue ev && ev.TryGetValue<bool>(out var empty)) c["empty"] = empty;
            });
            return null;
        });
        r.Query("coordination.settings", (q, _) =>
        {
            var s = Settings(q.Model);
            return new JsonObject
            {
                ["kinds"] = new JsonArray(Kinds.Where((KindsOf(s["kinds"]) ?? Kinds.ToHashSet()).Contains).Select(k => (JsonNode)k).ToArray()),
                ["offset"] = Json.Number(s["offset"]) ?? 0,
                ["clearance"] = Json.Number(s["clearance"]) ?? 0,
                ["empty"] = s["empty"] is JsonValue ev && ev.TryGetValue<bool>(out var e) && e,
                ["allKinds"] = new JsonArray(Kinds.Select(k => (JsonNode)k).ToArray()),
            };
        });
        // The voids: {kinds?, offset?, mesh? (true: each with its triangles, for the 3D view)}.
        r.Query("coordination.voids", (q, a) =>
        {
            var voids = Of(q, VoidOptions.From(q.Model, a));
            var mesh = a["mesh"] is JsonValue mv && mv.TryGetValue<bool>(out var withMesh) && withMesh;
            return new JsonObject { ["voids"] = new JsonArray(voids.Select(v => (JsonNode)ToJson(v, q.Model, mesh)).ToArray()), ["count"] = voids.Count };
        });
        OpeningClashes.Register(r);
    }
}
