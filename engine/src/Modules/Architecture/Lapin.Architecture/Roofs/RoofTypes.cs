using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Roofs;

/// <summary>
/// Roof types (issue #151, a CYPECAD MEP tester): an IfcRoofType a roof is typed by (IfcRelDefinesByType), optionally. It holds
/// what every roof of it shares: its type parameters (<c>params</c>: Pset_RoofCommon and the user's own sets, as a wall type's,
/// decision 667) and, when made from a roof, its build-up (<c>buildUp</c>: the covering and its material, the deck, the custom
/// layers, the framing, the gutters and snow guards; never the shape, the pitches, the overhangs nor the outline, which stay
/// each roof's). A roof given a type with a build-up takes it, as a project roof style is put on (decision 233); a type saved
/// again from a roof puts it on again on every roof of the type. The project's roof styles stay as they are, beside the types.
/// IFC writes the type as IfcRoofType with its property sets and reads it back.
/// </summary>
public static class RoofTypes
{
    public const string Class = "IfcRoofType";
    public const string BuildUpKey = "buildUp";

    /// <summary>What shapes a roof: its own, never its type's.</summary>
    static readonly string[] Shape = ["shape", "pitch", "overhang", "gableOverhang", "ridgeDirection", "shedFlip", "upperPitch", "mansardInset"];

    /// <summary>The fields a roof type's build-up carries: a project style's (<see cref="ProjectRoofStyles.Fields"/>) less the shape.</summary>
    public static readonly string[] Fields = [.. ProjectRoofStyles.Fields.Except(Shape)];

    public static Entity? Of(IModelView m, string roofId) => m.RelatingOf(roofId, IfcClass.RelDefinesByType) is { Class: Class } t ? t : null;

    /// <summary>A roof's build-up as a type keeps it.</summary>
    public static JsonObject BuildUp(Entity roof)
    {
        var o = new JsonObject();
        foreach (var k in Fields) if (roof.Attrs[k] is { } v) o[k] = v.DeepClone();
        return o;
    }

    /// <summary>A roof's attributes with its type's build-up put on (its own build-up fields replaced); the roof as it is when the type has none.</summary>
    public static Entity Apply(Entity roof, Entity type)
    {
        if (type.Attrs[BuildUpKey] is not JsonObject b) return roof;
        var attrs = roof.CloneAttrs();
        foreach (var k in Fields) attrs.Remove(k);
        foreach (var (k, v) in b) if (Fields.Contains(k)) attrs[k] = v?.DeepClone();
        return roof.With(attrs: attrs);
    }

    /// <summary>
    /// A new roof type: its name (numbered when none), the build-up of <paramref name="from"/> when given (which is then typed by
    /// it), its parameters (checked as element.setParam checks one).
    /// </summary>
    public static Entity Create(ICommandContext ctx, string? name, Entity? from, JsonArray? ps, Func<Entity, Entity> check)
    {
        var attrs = new JsonObject();
        if (from != null) attrs[BuildUpKey] = BuildUp(from);
        if (ps is { Count: > 0 }) attrs["params"] = ps;
        var label = name?.Trim() is { Length: > 0 } n ? n : $"Roof type {Numbering.Next(ctx.Model.OfClass(Class))}";
        var type = new Entity(ctx.NewId(), Class, label, attrs, "NOTDEFINED");
        ctx.Add(type);
        if (from != null) Assign(ctx, from, type, check);
        return type;
    }

    /// <summary>A roof given a type (its build-up put on, refused when the roof would not build), or none (it keeps how it is).</summary>
    public static void Assign(ICommandContext ctx, Entity roof, Entity? type, Func<Entity, Entity> check)
    {
        if (type == null) { if (Of(ctx.Model, roof.Id) != null) ctx.Unrelate(IfcClass.RelDefinesByType, roof.Id); return; }
        var next = Apply(roof, type);
        if (!ReferenceEquals(next, roof)) ctx.Replace(check(next));
        ctx.Relate(IfcClass.RelDefinesByType, type.Id, roof.Id);
    }

    /// <summary>A type's build-up taken again from a roof (or left), its name changed; every roof of it takes the build-up again.</summary>
    public static void Update(ICommandContext ctx, Entity type, string? name, Entity? from, Func<Entity, Entity> check)
    {
        var attrs = type.CloneAttrs();
        if (from != null) attrs[BuildUpKey] = BuildUp(from);
        var label = name?.Trim() is { Length: > 0 } n ? n : type.Name;
        var next = type.With(name: label, attrs: attrs);
        ctx.Replace(next);
        if (from == null) return;
        if (Of(ctx.Model, from.Id)?.Id != type.Id) ctx.Relate(IfcClass.RelDefinesByType, type.Id, from.Id);
        foreach (var r in ctx.Model.RelatedOf(type.Id, IfcClass.RelDefinesByType).Where(e => e.Class == IfcClass.Roof).ToList())
            if (Apply(r, next) is var applied && !ReferenceEquals(applied, r)) ctx.Replace(check(applied));
    }
}
