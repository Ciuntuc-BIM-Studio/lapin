using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// Convert to member (decision 268): a drafted closed outline read as a band (<see cref="SketchBands"/>) on its view's
/// plane becomes a member.
/// <list type="bullet">
/// <item>On a plan the band is the member seen from above: its width the flanges' (a profile of that width from the
/// project's member types or the catalogue, else the current type), its ends' slants cuts in plan; the member lies on the
/// storey's top, the section's top on its axis, as a beam. A notch seen from above is not a cope: a notched outline on a
/// plan is refused.</item>
/// <item>On a section or an elevation (any view lying on a standing plane) the band is the member seen from its side: its
/// width the section's depth, its ends' slants cuts in elevation, a notch a cope at the top or the bottom; the member lies
/// where it is drawn, its axis through the section's centre.</item>
/// </list>
/// </summary>
public static class SketchMembers
{
    /// <summary>How close a profile's width must be to the band's (m).</summary>
    const double WidthTolerance = 0.001;

    /// <summary>An outline's band and the plane it lies on, or why it is not one.</summary>
    public static (Band? Band, WorkPlane? Plane, string? Problem) Read(IModelView m, IServices services, string id)
    {
        if (m.Get(id) is not { Class: IfcClass.Annotation } e || e.PredefinedType != "DRAFT") return (null, null, "Not a drafted shape.");
        if (e.Attrs["shape"] is not JsonObject shape || (string?)shape["type"] != "polyline" || shape["closed"] is not JsonValue cv || !cv.TryGetValue<bool>(out var closed) || !closed)
            return (null, null, "Not a closed outline: draw the member's outline as a closed polyline or a rectangle.");
        var pts = Args.StoredPoints(shape["pts"]);
        if (Views.Find(m, e.Str("viewId") ?? "") is not { } view) return (null, null, "The outline's view is gone.");
        var midX = pts.Count > 0 ? (pts.Min(p => p.X) + pts.Max(p => p.X)) / 2 : 0;
        if (Views.PlaneOf(m, services, view, midX) is not { } plane) return (null, null, "The outline's view lies on no plane (an elevation without one yet, a 3D view).");
        var (band, problem) = SketchBands.Read(pts);
        return (band, plane, problem);
    }

    /// <summary>The member made from an outline, or why there is none.</summary>
    public static (string? Member, string? Problem) Convert(ICommandContext ctx, string id, string? typeId)
    {
        var (band, plane, problem) = Read(ctx.Model, ctx.Services, id);
        if (band == null || plane == null) return (null, problem);
        var flat = Math.Abs(plane.Normal.Z) > 0.99;
        if (flat && (band.Start.Copes.Count > 0 || band.End.Copes.Count > 0))
            return (null, "A notch drawn on a plan is not a cope (a cope is seen from the side): draw the outline on a section or an elevation, or without the notch.");
        Vec3 W3(Vec2 p) => plane.Origin + plane.Right * p.X + plane.Up * p.Y;
        Vec3 D3(Vec2 d) => plane.Right * d.X + plane.Up * d.Y;

        var view = Views.Find(ctx.Model, ctx.Model.Get(id)!.Str("viewId")!)!;
        var (s, t) = (W3(band.Start.At), W3(band.End.At));
        Entity storey;
        string insertion;
        if (flat)
        {
            // On a plan: on the storey's top, as a beam.
            storey = ctx.Model.Get(view.Str("storeyId") ?? "") is { Class: IfcClass.BuildingStorey } st ? st : Spatial.Storeys(ctx.Model).First();
            var top = storey.Num("elevation") + storey.Num("height", 3);
            (s, t) = (s with { Z = top }, t with { Z = top });
            insertion = "top";
        }
        else
        {
            var low = Math.Min(s.Z, t.Z);
            storey = Spatial.Storeys(ctx.Model).LastOrDefault(x => x.Num("elevation") <= low + 1e-6) ?? Spatial.Storeys(ctx.Model).First();
            insertion = "center";
        }

        // The profile: of the band's width (the flanges' on a plan, the depth on a side view).
        var current = typeId != null ? ctx.Model.Get(typeId) is { Class: SteelModule.MemberType } ct ? ct : throw new CommandException("There is no such member type.") : null;
        double Size(Section x) => flat ? x.Width : x.Depth;
        bool Fits(Section? x) => x != null && Math.Abs(Size(x) - band.Width) <= WidthTolerance;
        var type = current != null && Fits(Section.From(current.Attrs["section"])) ? current
            : ctx.Model.OfClass(SteelModule.MemberType).FirstOrDefault(x => Fits(Section.From(x.Attrs["section"])))
            ?? (Section.Catalogue.Values.Where(Fits).OrderBy(x => x.Area).FirstOrDefault() is { } found ? SteelModule.CreateType(ctx, null, found) : null)
            ?? current ?? SteelModule.TypeFor(ctx, null);

        // The rotation that stands the section's up across the band on a side view (up rather than down).
        var probe = new MemberAxis(s, t, Section.From(type.Attrs["section"]) ?? Section.Catalogue["IPE 200"]);
        var (v0, w0) = probe.Frame;
        var across = D3(band.Across).Normalized();
        var rotation = 0.0;
        var plusIsUp = true;
        if (!flat)
        {
            if (Vec3.Dot(across, w0) < 0) { across = -across; plusIsUp = false; }
            rotation = Math.Atan2(-Vec3.Dot(across, v0), Vec3.Dot(across, w0)) * 180 / Math.PI;
            if (Math.Abs(rotation) < 1e-9) rotation = 0;
        }
        var ax = probe with { Rotation = rotation, Insertion = insertion };
        var (v, w) = ax.Frame;
        var u = ax.U;

        // Each end's cut: its normal out of the band, read as the angles from square (about the section's up, then its side).
        SteelEnd End(BandEnd be, bool atEnd)
        {
            var n = D3(be.Normal).Normalized();
            var outward = atEnd ? u : -u;
            var elev = Math.Asin(Math.Clamp(Vec3.Dot(n, w), -1, 1)) * 180 / Math.PI;
            var plan = Math.Atan2(Vec3.Dot(n, v), Vec3.Dot(n, outward)) * 180 / Math.PI;
            (plan, elev) = (Clean(plan), Clean(elev));
            var top = be.Copes.FirstOrDefault(c => c.Plus == plusIsUp);
            var bottom = be.Copes.FirstOrDefault(c => c.Plus != plusIsUp);
            var cope = top ?? bottom;
            var square = plan == 0 && elev == 0;
            return new SteelEnd(Cut: square ? "square" : "plane", PlanAngle: plan, ElevAngle: elev,
                CopeLength: cope?.Length ?? 0, CopeDepth: cope?.Depth ?? 0, CopeTop: top != null, CopeBottom: bottom != null);
        }
        var level = storey.Num("elevation");
        var member = SteelModule.Add(ctx, storey, type, s with { Z = s.Z - level }, t with { Z = t.Z - level }, rotation, insertion, null, End(band.Start, false), End(band.End, true));
        return (member, null);
    }

    /// <summary>An angle rounded off to a millionth of a degree, so a square end drawn square stays square.</summary>
    static double Clean(double deg) => Math.Abs(deg) < 1e-6 ? 0 : Math.Round(deg, 6);
}
