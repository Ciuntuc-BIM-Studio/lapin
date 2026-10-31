using System.Text.Json.Nodes;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// A plane a view lies on and drafting draws on, in the model: its origin, its drawing axes
/// (<see cref="Right"/>, <see cref="Up"/>) and the side it faces (<see cref="Normal"/>, towards
/// the viewer; <c>Right × Up</c>). A point drawn at (x, y) stands at <see cref="World"/>.
/// </summary>
public sealed record WorkPlane(Vec3 Origin, Vec3 Right, Vec3 Up, Vec3 Normal)
{
    public Vec3 World(Vec2 p) => Origin + Right * p.X + Up * p.Y;

    /// <summary>The point's place on the plane, and its height off it (along the normal).</summary>
    public (Vec2 At, double Off) Local(Vec3 p) => (new Vec2(Vec3.Dot(p - Origin, Right), Vec3.Dot(p - Origin, Up)), Vec3.Dot(p - Origin, Normal));
}

/// <summary>
/// Reference planes (docs/plan-studio-thick.md): a view of kind <c>plane</c> on a plane anywhere in
/// the model, drawn on with Draft like a section, and what Studio's solids are sketched on. The plane is
/// its origin (<c>x</c>, <c>y</c>, <c>z</c>) and three angles, so it moves and turns from the properties
/// panel and what is sketched on it follows:
/// <list type="bullet">
/// <item><c>bearing</c>: the plan direction the plane faces (degrees from +X, counter-clockwise);</item>
/// <item><c>tilt</c>: how far its normal rises from level (0: a standing plane, 90: lying, facing up; −90 facing down);</item>
/// <item><c>spin</c>: its drawing's x axis turned about the normal from level (0: x runs level, along the plane).</item>
/// </list>
/// Its drawing is a section on the plane, looking against the normal, as deep as its <c>depth</c> (unlimited without).
/// </summary>
public static class RefPlanes
{
    public const string Kind = "plane";

    /// <summary>The fields that place the plane.</summary>
    public static readonly string[] Keys = ["x", "y", "z", "bearing", "tilt", "spin"];

    const double Deg = Math.PI / 180;

    /// <summary>The plane a reference plane view lies on.</summary>
    public static WorkPlane Of(Entity view)
    {
        var o = new Vec3(view.Num("x"), view.Num("y"), view.Num("z"));
        var (b, t, s) = (view.Num("bearing") * Deg, view.Num("tilt") * Deg, view.Num("spin") * Deg);
        var n = new Vec3(Math.Cos(t) * Math.Cos(b), Math.Cos(t) * Math.Sin(b), Math.Sin(t)).Normalized();
        // Level along the plane (the bearing turned a quarter clockwise), whatever the tilt: no singular case.
        var x0 = new Vec3(-Math.Sin(b), Math.Cos(b), 0);
        var x = (x0 * Math.Cos(s) + Vec3.Cross(n, x0) * Math.Sin(s)).Normalized();
        return new WorkPlane(o, x, Vec3.Cross(n, x).Normalized(), n);
    }

    /// <summary>The fields of a plane through <paramref name="origin"/> facing <paramref name="normal"/>, its x along <paramref name="right"/> when given (else level).</summary>
    public static JsonObject Attrs(Vec3 origin, Vec3 normal, Vec3? right = null)
    {
        if (normal.Length < 1e-12) throw new CommandException("A plane needs a direction it faces.");
        var n = normal.Normalized();
        var t = Math.Asin(Math.Clamp(n.Z, -1, 1));
        var b = Math.Sqrt(n.X * n.X + n.Y * n.Y) > 1e-9 ? Math.Atan2(n.Y, n.X) : -Math.PI / 2;
        var x0 = new Vec3(-Math.Sin(b), Math.Cos(b), 0);
        var s = 0.0;
        if (right is { } r)
        {
            var rr = r - n * Vec3.Dot(r, n);
            if (rr.Length > 1e-9) { rr = rr.Normalized(); s = Math.Atan2(Vec3.Dot(Vec3.Cross(x0, rr), n), Vec3.Dot(x0, rr)); }
        }
        static double R(double v) => Math.Round(v, 9);
        return new JsonObject
        {
            ["x"] = R(origin.X), ["y"] = R(origin.Y), ["z"] = R(origin.Z),
            ["bearing"] = R(b / Deg), ["tilt"] = R(t / Deg), ["spin"] = R(s / Deg),
        };
    }

    /// <summary>The plane through three points: the first its origin, x towards the second, facing the side the three turn counter-clockwise seen from (up, for three points on a level).</summary>
    public static JsonObject Through(Vec3 a, Vec3 b, Vec3 c)
    {
        var n = Vec3.Cross(b - a, c - a);
        if (n.Length < 1e-9 || (b - a).Length < 1e-9) throw new CommandException("Three points on a line (or two the same) make no plane.");
        return Attrs(a, n, b - a);
    }

    /// <summary>The plane on a face: through the point picked, facing out of it, x level along it (or along +X on a level face).</summary>
    public static JsonObject OnFace(Vec3 point, Vec3 normal) => Attrs(point, normal);

    /// <summary>A plane parallel to another, <paramref name="offset"/> metres towards its viewer (away when negative), x along its x.</summary>
    public static JsonObject Offset(WorkPlane p, double offset) => Attrs(p.Origin + p.Normal * offset, p.Normal, p.Right);

    /// <summary>The plane's drawing: a section on it (a hair towards the viewer, so a face lying on the plane is seen, not cut), looking against its normal.</summary>
    public static ViewSpec Spec(Entity view, bool hidden)
    {
        var p = Of(view);
        var frame = new ViewFrame(p.Origin + p.Normal * 1e-4, p.Right, p.Up, -p.Normal);
        return new ViewSpec("section", frame, true, view.NumOrNull("depth") ?? ViewRanges.Unlimited, Hidden: hidden);
    }

    /// <summary>A plane moved by <paramref name="by"/> and turned about the vertical through <paramref name="about"/> by <paramref name="turnDeg"/>: its fields after.</summary>
    public static JsonObject Moved(Entity view, Vec3 by, double turnDeg = 0, Vec3? about = null)
    {
        var p = Of(view);
        var o = p.Origin + by;
        if (Math.Abs(turnDeg) < 1e-12) return Attrs(o, p.Normal, p.Right);
        var (c, s) = (Math.Cos(turnDeg * Deg), Math.Sin(turnDeg * Deg));
        Vec3 Rot(Vec3 v) => new(v.X * c - v.Y * s, v.X * s + v.Y * c, v.Z);
        var at = about ?? o;
        return Attrs(at + Rot(o - at), Rot(p.Normal), Rot(p.Right));
    }
}
