using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// A straight steel member as its body is built (decision 268): its axis from <see cref="Start"/> to <see cref="End"/>
/// (world metres), its section, turned by <see cref="Rotation"/> (degrees) about the axis, and where the axis runs through
/// the section (<see cref="Insertion"/>: <c>center</c>, or the section's <c>top</c> or <c>bottom</c> on it); flipped, the
/// section is mirrored across its up.
/// <para>
/// Its frame: <see cref="U"/> along the axis; the section's x along <see cref="V"/> and its y along <see cref="W"/>
/// (V × W = U). Unturned, a member that is not vertical has V level, to the axis's left, and W up from it, as a beam's
/// web stands; a vertical member has V along x and W along y, as a column's section lies at rotation 0.
/// </para>
/// </summary>
public sealed record MemberAxis(Vec3 Start, Vec3 End, Section Section, double Rotation = 0, string Insertion = "center", bool Flipped = false)
{
    public static readonly string[] Insertions = ["center", "top", "bottom"];

    public double Length => (End - Start).Length;
    public Vec3 U => (End - Start).Normalized();

    public (Vec3 V, Vec3 W) Frame
    {
        get
        {
            var u = U;
            var v0 = Math.Abs(u.Z) > 1 - 1e-9 ? new Vec3(1, 0, 0) : new Vec3(-u.Y, u.X, 0).Normalized();
            var w0 = Vec3.Cross(u, v0);
            var r = Rotation * Math.PI / 180;
            var (c, s) = (Math.Cos(r), Math.Sin(r));
            return (v0 * c + w0 * s, w0 * c - v0 * s);
        }
    }

    /// <summary>Where the section's centre is from the axis: down half its depth with the axis on its top, up with it on its bottom.</summary>
    public Vec3 Offset => Insertion switch
    {
        "top" => Frame.W * (-Section.Depth / 2),
        "bottom" => Frame.W * (Section.Depth / 2),
        _ => Vec3.Zero,
    };

    /// <summary>The section's outline and holes as the member carries them (mirrored when flipped).</summary>
    public (IReadOnlyList<Vec2> Outline, IReadOnlyList<IReadOnlyList<Vec2>> Holes) Profile
    {
        get
        {
            Vec2 F(Vec2 p) => Flipped ? p with { X = -p.X } : p;
            var outline = Polygon2.EnsureCcw(Section.Outline().Select(F).ToList());
            var holes = Section.Holes().Select(h => (IReadOnlyList<Vec2>)Polygon2.EnsureCcw(h.Select(F).ToList()).Reverse().ToList()).ToList();
            return (outline, holes);
        }
    }

    /// <summary>A point of the section (its x, y) at a station along the axis, in the world.</summary>
    public Vec3 At(Vec2 p, double station)
    {
        var (v, w) = Frame;
        return Start + Offset + v * p.X + w * p.Y + U * station;
    }
}

/// <summary>The steel members a module builds ends for: a steel module's own (LapinSteelMember), and the structure module's straight beams and columns.</summary>
public static class MemberAxes
{
    /// <summary>A member's end points relative to its storey's level: [x, y, z].</summary>
    public static Vec3 Point(Entity e, string key) => Args.TryPoint3(e.Attrs[key]) ?? default;

    public static double Level(IModelView m, Entity e) => Spatial.StoreyOf(m, e.Id)?.Num("elevation") ?? 0;

    /// <summary>A member's type's section: its member type's, else an IPE 200.</summary>
    public static Section SectionOf(IModelView m, Entity member) =>
        Section.From(m.RelatingOf(member.Id, IfcClass.RelDefinesByType)?.Attrs["section"]) ?? Section.Catalogue["IPE 200"];

    /// <summary>
    /// The axis of a member, a beam or a column, null for anything else (or a curved beam, or one with no length): a member's
    /// as it is kept; a beam's along its clear span between the column faces, its section's top on the axis at the beam's
    /// top; a column's from its base up its height, its section centred.
    /// </summary>
    public static MemberAxis? Of(IModelView m, Entity e)
    {
        switch (e.Class)
        {
            case SteelModule.Member:
            {
                var z = Level(m, e);
                var (s, t) = (Point(e, "start"), Point(e, "end"));
                var ax = new MemberAxis(s with { Z = s.Z + z }, t with { Z = t.Z + z }, SectionOf(m, e), e.Num("rotation"), Insertion(e), e.Bool("flipped"));
                return ax.Length > 1e-3 ? ax : null;
            }
            case IfcClass.Beam:
            {
                var span = Beams.SpanOf(m, e);
                if (span.Curve.IsArc || span.Empty) return null;
                var top = Beams.Top(m, e);
                return new MemberAxis(span.Start.At(top), span.End.At(top), Beams.Spec(m, e).Section, 0, "top", e.Bool("flipped"));
            }
            case IfcClass.Column:
            {
                var (section, _, height) = Columns.Spec(m, e);
                if (!(height > 0) || Args.TryPoint(e.Attrs["at"]) is not { } at) return null;
                var z = Level(m, e) + e.Num("baseOffset");
                return new MemberAxis(at.At(z), at.At(z + height), section, e.Num("rotation"));
            }
        }
        return null;
    }

    static string Insertion(Entity e) => e.Str("insertion") is { } i && MemberAxis.Insertions.Contains(i) ? i : "center";

    /// <summary>Whether the element has a steel profile (a beam or a column of a concrete rectangle takes no steel ends).</summary>
    public static bool IsSteel(IModelView m, Entity e) => e.Class == SteelModule.Member || Of(m, e)?.Section.IsProfile == true;
}
