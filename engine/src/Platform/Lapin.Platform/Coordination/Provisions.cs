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
    string? Make(ICommandContext ctx, Entity host, IReadOnlyList<Solid> through, OpeningRequest r);
    /// <summary>The opening grown to take in what passes through (it keeps what it had); false when it is not one this module grows.</summary>
    bool Enlarge(ICommandContext ctx, Entity host, Entity opening, IReadOnlyList<Solid> through, OpeningRequest r);
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
            if (entity.Class is not (IfcClass.Wall or IfcClass.Slab) || geometry.Parts.Count == 0) return null;
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
