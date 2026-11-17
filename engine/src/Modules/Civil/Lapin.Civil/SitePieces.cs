using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Civil;

/// <summary>
/// A platform split or trimmed by a line (<see cref="OutlineSplits"/>, audit 2026-11-15 §15): each part levelled at the same
/// elevation, its arcs and its own holes on the part they lie in; the new part cuts the terrain the platform cut.
/// </summary>
sealed class PlatformPieces : IOutlinePieces
{
    public bool Handles(Entity e) => e.Class == IfcClass.EarthworksCut && !Ditches.Is(e) && e.Attrs["contour"] is JsonArray;

    public string What(Entity e) => "A platform";

    public void Rehost(ICommandContext ctx, Entity e, Entity? second, Func<Vec2, bool> onFirst)
    {
        if (second != null && Terrains.TerrainOf(ctx.Model, e.Id) is { } terrain)
            ctx.AddRelation(new Relation(ctx.NewId(), IfcClass.RelVoidsElement, terrain.Id, [second.Id]));
    }
}

/// <summary>
/// A terrain's parcel trimmed by a line (audit §15): its corners and arcs (<c>parcel</c>, <see cref="Terrains.ParcelArcs"/>).
/// It is not split: a terrain keeps one parcel.
/// </summary>
sealed class ParcelPieces : IOutlinePieces
{
    public bool Handles(Entity e) => e.Class == IfcClass.GeographicElement && e.Attrs["parcel"] is JsonArray;

    public string Key => "parcel";
    public string ArcsKey => Terrains.ParcelArcs;
    public string What(Entity e) => "A parcel";

    public string? Refuse(IModelView m, Entity e, bool split) =>
        split ? $"„{e.Name}” keeps one parcel: trim it by the line instead, or draw the other part's parcel on another terrain." : null;
}
