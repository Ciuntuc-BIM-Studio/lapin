using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform.Coordination;

/// <summary>
/// A provision opening (decision 293): the hole an MEP run cuts in a wall or a floor it crosses, by the cut order's MEP
/// rule (<see cref="CutOrder.MepCut"/>), never stored: worked out from the run and its host each time either is built,
/// so it follows the run when it moves or changes size and goes when the run goes or no longer crosses.
/// <see cref="Void"/> runs exactly through the host's thickness; <see cref="Cutter"/> a little past both faces, what is
/// taken out of the host. <see cref="Held"/>: why it is not cut ("structural": a load-bearing host the rule does not cut,
/// "order": the host ranks over the run in the cut order); a held one is reported by the clash check, not cut.
/// </summary>
public sealed record ProvisionOpening(string Key, string SourceId, string HostId, string Kind, string Shape, double Width, double Height, double Depth,
    Solid Void, Solid Cutter, string? StoreyId, string? Held = null)
{
    public const string Structural = "structural", Order = "order";
    public bool Cuts => Held == null;
    /// <summary>Its GlobalId as an IfcOpeningElement: derived from its key, the same at every export.</summary>
    public string GlobalId => OpeningVoids.DerivedId(Key, "provision");
    public double? Diameter => Shape == OpeningVoids.Round ? Width : null;
    /// <summary>A wall's direction where it stands (plan, unit), for its void's label; null in a floor.</summary>
    public Vec2? Along { get; init; }

    public OpeningVoid AsVoid(IModelView m) => new(Key, HostId, null, null, Kind, Shape, Width, Height, Depth, Void, StoreyId) { Source = SourceId, Along = Along };
}

/// <summary>The provision openings a module's elements cut in their hosts (the MEP module's runs); a service.</summary>
public interface IProvisionOpenings
{
    /// <summary>The provisions in <paramref name="host"/> (null: every host), their sides moved out by <paramref name="offset"/>; held ones too.</summary>
    IEnumerable<ProvisionOpening> Of(IModelView m, IServices services, Entity? host, double offset = 0);
}

/// <summary>
/// Which hosts a run's edit builds again (decision 296): a host's provisions read the runs near it under keys of a grid
/// in plan (<see cref="Provisions.ReadNear"/>), never their classes whole; a run built again touches the cells of where it
/// was and where it is (the session, <see cref="Provisions.CellKeys"/>), so only the walls, floors and roofs it crossed
/// before or crosses now are built again.
/// </summary>
public static class ProvisionCells
{
    /// <summary>The side of a cell in plan (m).</summary>
    public const double Size = 4;
    /// <summary>How far round a box its cells reach: more than any clearance, so a crossing always shares a cell.</summary>
    public const double Margin = 0.5;
    /// <summary>The key every host reads: touched when a run's place is not known (no body), so every host looks again.</summary>
    public const string Any = "pcell:*";
    /// <summary>A box over more cells than this reads and touches <see cref="Any"/> instead.</summary>
    const int MaxCells = 4096;

    public static string Key(long x, long y) => $"pcell:{x},{y}";

    /// <summary>The cells a box (with the margin) falls in; <see cref="Any"/> alone for one too large or not finite.</summary>
    public static IEnumerable<string> Of((Vec3 Min, Vec3 Max) b)
    {
        double x0 = Math.Floor((b.Min.X - Margin) / Size), y0 = Math.Floor((b.Min.Y - Margin) / Size), x1 = Math.Floor((b.Max.X + Margin) / Size), y1 = Math.Floor((b.Max.Y + Margin) / Size);
        if (!double.IsFinite(x0 + y0 + x1 + y1) || (x1 - x0 + 1) * (y1 - y0 + 1) > MaxCells) { yield return Any; yield break; }
        for (var x = (long)x0; x <= (long)x1; x++)
            for (var y = (long)y0; y <= (long)y1; y++)
                yield return Key(x, y);
    }
}

/// <summary>A new opening made in a host round what goes through it (Make opening, Enlarge opening, decision 293).</summary>
public sealed record OpeningRequest(double Clearance, bool Round, CutOrder.MepCut Rule);

/// <summary>
/// The module that owns walls and floors makes their openings: round what passes through the host (the MEP element's
/// solids within the host's envelope), its sides <see cref="OpeningRequest.Clearance"/> away, sized up to the rule's step,
/// a proper opening of the host (IfcOpeningElement) so voids and IFC see it. A service.
/// </summary>
public interface IOpeningMaker
{
    /// <summary>The new opening's id; null when the host is not one this module makes openings in. Refuses (CommandException) what does not fit.</summary>
    string? Make(IEdit ctx, Entity host, IReadOnlyList<Solid> through, OpeningRequest r);
    /// <summary>
    /// A real opening where an imported provision opening was (decision 296: Lapin's own IFC read back without the run
    /// that cut it): <paramref name="centre"/> its middle, its width and height (a wall's along it and up it; a floor's or
    /// a roof's along <paramref name="along"/> and across it), round or not. Null when the host is not one this module makes openings in.
    /// </summary>
    string? MakeAt(IEdit edit, Entity host, Vec3 centre, double width, double height, bool round, Vec2? along = null) => null;
    /// <summary>The opening grown to take in what passes through (it keeps what it had); false when it is not one this module grows.</summary>
    bool Enlarge(IEdit ctx, Entity host, Entity opening, IReadOnlyList<Solid> through, OpeningRequest r);
}

/// <summary>The provision openings of every module, cut out of their hosts and offered as voids.</summary>
public static class Provisions
{
    /// <summary>Every provision (held ones too) in a host, or in every host.</summary>
    public static List<ProvisionOpening> Of(IModelView m, IServices services, Entity? host = null, double offset = 0)
    {
        var o = new List<ProvisionOpening>();
        foreach (var p in services.All<IProvisionOpenings>())
            try { o.AddRange(p.Of(m, services, host, offset)); }
            catch (Exception ex) when (ex is ModelException or InvalidOperationException or ArgumentException) { }
        return o;
    }

    /// <summary>The model under a geometry build's tracking view, read without noting what is read (what a host's provisions filter runs by).</summary>
    public static IModelView Untracked(IModelView m) => m is TrackingView tv ? tv.Inner : m;

    /// <summary>Whether the model is read by a geometry build (its reads noted): the model does not change meanwhile.</summary>
    public static bool Building(IModelView m) => m is TrackingView;

    /// <summary>A host's build notes the cells round its box (and <see cref="ProvisionCells.Any"/>): a run built again there builds it again.</summary>
    public static void ReadNear(IModelView m, (Vec3 Min, Vec3 Max) box)
    {
        if (m is not TrackingView tv) return;
        tv.Read.Add(ProvisionCells.Any);
        foreach (var k in ProvisionCells.Of(box)) tv.Read.Add(k);
    }

    /// <summary>Whether an element bears load: Pset_…Common LoadBearing true on it or its type, or its own <c>loadBearing</c>.</summary>
    public static bool LoadBearing(IModelView m, Entity e)
    {
        static bool? In(Entity x)
        {
            if (x.Attrs["loadBearing"] is JsonValue lv && lv.TryGetValue<bool>(out var own)) return own;
            if (x.Attrs["params"] is not JsonArray ps) return null;
            foreach (var p in ps.OfType<JsonObject>())
                if (string.Equals((string?)p["name"], "LoadBearing", StringComparison.OrdinalIgnoreCase) && ((string?)p["pset"])?.StartsWith("Pset_", StringComparison.Ordinal) != false)
                    return p["value"] is JsonValue v && (v.TryGetValue<bool>(out var b) ? b : v.TryGetValue<string>(out var s) && s.Trim().ToLowerInvariant() is "true" or "yes" or "1");
            return null;
        }
        return In(e) ?? (m.RelatingOf(e.Id, IfcClass.RelDefinesByType) is { } t ? In(t) : null) ?? false;
    }

    /// <summary>Takes the provisions out of a host's parts (a geometry filter): only where they cut and the build cuts openings.</summary>
    sealed class Cuts : IGeometryFilter
    {
        public ElementGeometry? Apply(Entity entity, ElementGeometry geometry, IModelView model, IServices services)
        {
            if (entity.Class is not (IfcClass.Wall or IfcClass.Slab or IfcClass.Roof) || geometry.Parts.Count == 0) return null;
            if (services.Get<GeometryOptions>()?.CutOpenings == false) return null;
            var cutters = Of(model, services, entity).Where(p => p.Cuts).Select(p => p.Cutter).ToList();
            if (cutters.Count == 0) return null;
            if (services.Get<ISolidBooleans>() is not { } booleans)
                return geometry with { Diagnostics = [.. geometry.Diagnostics ?? [], "Provision openings need the boolean library (manifold), missing here."] };
            var parts = new List<GeometryPart>();
            foreach (var part in geometry.Parts)
            {
                var b = Measure.Bounds(part.Solid);
                var near = cutters.Where(c => b is { } pb && Measure.Bounds(c) is { } cb && pb.Min.X <= cb.Max.X && cb.Min.X <= pb.Max.X && pb.Min.Y <= cb.Max.Y && cb.Min.Y <= pb.Max.Y && pb.Min.Z <= cb.Max.Z && cb.Min.Z <= pb.Max.Z).ToList();
                if (near.Count == 0) { parts.Add(part); continue; }
                try { if (booleans.Difference(part.Solid, near, part.Solid.Tag) is { } cut) parts.Add(part with { Solid = cut }); }
                catch (InvalidOperationException) { parts.Add(part); }
            }
            return geometry with { Parts = parts };
        }
    }

    public static void Register(IModuleRegistry r)
    {
        r.Service<IGeometryFilter>(new Cuts());
        // The provisions: {hostId?}: each with its host, its run, its shape and size, and whether it cuts (held: why not).
        r.Query("coordination.provisions", (q, a) =>
        {
            var host = (string?)a["hostId"] is { } h ? q.Model.Get(h) : null;
            return new JsonArray(Of(q.Model, q.Services, host).Select(p => (JsonNode)new JsonObject
            {
                ["key"] = p.Key, ["sourceId"] = p.SourceId, ["hostId"] = p.HostId, ["kind"] = p.Kind, ["shape"] = p.Shape,
                ["width"] = Math.Round(p.Width, 4), ["height"] = Math.Round(p.Height, 4), ["depth"] = Math.Round(p.Depth, 4), ["held"] = p.Held, ["globalId"] = p.GlobalId,
            }).ToArray());
        });
    }
}
