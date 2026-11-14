using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A class's elements read only near a box, for a build that looks at its neighbours (a timber-frame wall at the walls
/// stopping against it, report #125): instead of the whole class, which built every such element again at any edit of
/// the class, the build notes the cells of a grid in plan round its box and reads through the model only the elements it
/// finds there. An element of the class built again touches the cells of where it was and where it is
/// (<see cref="Session"/>), so only the readers round those are built again.
/// </summary>
public static class NearReads
{
    /// <summary>The side of a cell in plan (m).</summary>
    public const double Size = 4;
    /// <summary>How far round a box its cells reach (m).</summary>
    public const double Margin = 0.5;
    /// <summary>A box over more cells than this reads and touches <see cref="Any"/> alone.</summary>
    const int MaxCells = 4096;

    /// <summary>The key every reader of the class notes: touched when an element's place is not known (no body), and how the session knows the class has readers.</summary>
    public static string Any(string ifcClass) => "near:" + ifcClass;

    /// <summary>The cells a box (with the margin) falls in; <see cref="Any"/> alone for one too large or not finite.</summary>
    public static IEnumerable<string> Cells(string ifcClass, (Vec3 Min, Vec3 Max) b)
    {
        double x0 = Math.Floor((b.Min.X - Margin) / Size), y0 = Math.Floor((b.Min.Y - Margin) / Size), x1 = Math.Floor((b.Max.X + Margin) / Size), y1 = Math.Floor((b.Max.Y + Margin) / Size);
        if (!double.IsFinite(x0 + y0 + x1 + y1) || (x1 - x0 + 1) * (y1 - y0 + 1) > MaxCells) { yield return Any(ifcClass); yield break; }
        for (var x = (long)x0; x <= (long)x1; x++)
            for (var y = (long)y0; y <= (long)y1; y++)
                yield return $"near:{ifcClass}:{x},{y}";
    }

    /// <summary>
    /// The class's elements for a build looking round <paramref name="box"/>: every one of them, unnoted, for the caller to
    /// sort by the place in their own attributes; it reads those it keeps through <paramref name="m"/> (<c>m.Get</c>).
    /// Outside a build it is simply the class.
    /// </summary>
    public static IEnumerable<Entity> Candidates(IModelView m, string ifcClass, (Vec3 Min, Vec3 Max) box)
    {
        if (m is not TrackingView tv) return m.OfClass(ifcClass);
        tv.Read.Add(Any(ifcClass));
        foreach (var k in Cells(ifcClass, box)) tv.Read.Add(k);
        return tv.Inner.OfClass(ifcClass);
    }
}

/// <summary>
/// The elements along an axis (a wall) read only round a segment by where their attributes put them (<see cref="PlaceCells"/>): unlike
/// <see cref="NearReads"/>, which goes by the built bodies, the cells are the store's, stamped, so a build that reads them is still
/// taken back on an undo; an edit touches the cells of where an element was and is, which builds the readers round it again.
/// </summary>
public static class PlaceReads
{
    /// <summary>
    /// The class's elements for a build looking within <paramref name="reach"/> of a segment in plan: every one of them, unnoted, for the caller to sort by the
    /// place in their own attributes; it reads those it keeps through <paramref name="m"/> (<c>m.Get</c>). Outside a build it is simply the class.
    /// </summary>
    public static IEnumerable<Entity> Candidates(IModelView m, string ifcClass, Vec2 a, Vec2 b, double reach)
    {
        if (m is not TrackingView tv) return m.OfClass(ifcClass);
        foreach (var k in PlaceCells.Round(ifcClass, a.X, a.Y, b.X, b.Y, reach)) tv.Read.Add(k);
        return tv.Inner.OfClass(ifcClass);
    }
}
