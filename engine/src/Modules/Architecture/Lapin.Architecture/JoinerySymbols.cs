using Lapin.Geometry;

namespace Lapin.Architecture;

/// <summary>A stroke of a joinery drawing: its points, closed or not, its weight (cut, outline, thin), dashed or not.</summary>
public sealed record JStroke(IReadOnlyList<Vec2> Pts, string Weight, bool Closed = false, bool Dashed = false);

/// <summary>
/// The drawings of a joinery type, from its spec (the old Lapin's symbols,
/// one engine for plans, elevations, sections and the schedule's cards), in
/// the type's frame: a plan is (x along the wall, y across, +y the exterior),
/// an elevation (x, z) seen from the interior unless asked otherwise, a
/// section (y, z) cut at <c>cutX</c>.
/// <list type="bullet">
/// <item>Plan: the body sliced at the cut height (frame, mullions and sashes
/// cut, glass thin), the window boards seen below it, each door leaf open at
/// 90° to the interior with its swing arc (a double-acting leaf's other swing
/// dashed; a sliding leaf beside the opening with an arrow; a folding one in two).</item>
/// <item>Elevation: frame and panel outlines and the opening symbols, dashed
/// where the panel opens away from the viewer; sliding an arrow. Two
/// conventions: "handle" (default, the window makers' sketches: the apex at
/// the handle, a fixed pane crossed) and "hinge" (DIN 1356: the lines meet at
/// the hinges, fixed panes plain). With hardware: hinges and handles.</item>
/// <item>Section: the body sliced at <c>cutX</c>, cut parts heavy, glass thin.</item>
/// </list>
/// </summary>
public static class JoinerySymbols
{
    static JStroke Rect(double x0, double y0, double x1, double y1, string w, bool dashed = false) =>
        new([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)], w, true, dashed);
    static JStroke Line(Vec2 a, Vec2 b, string w, bool dashed = false) => new([a, b], w, false, dashed);

    static JStroke Arc(Vec2 c, double r, double from, double to, string w, bool dashed = false)
    {
        var steps = Math.Max(8, (int)Math.Ceiling(Math.Abs(to - from) / (Math.PI / 32)));
        return new(Enumerable.Range(0, steps + 1).Select(i => { var a = from + (to - from) * i / steps; return new Vec2(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)); }).ToList(), w, false, dashed);
    }

    static List<JStroke> Arrow(Vec2 a, Vec2 b, string w)
    {
        var len = Math.Max((b - a).Length, 1e-9);
        var (ux, uy) = ((b.X - a.X) / len, (b.Y - a.Y) / len);
        var head = Math.Min(0.08, len / 3);
        return [Line(a, b, w), new([new(b.X - ux * head - uy * head / 2, b.Y - uy * head + ux * head / 2), b, new(b.X - ux * head + uy * head / 2, b.Y - uy * head - ux * head / 2)], w)];
    }

    static bool HingeLeft(string op) => op.EndsWith("-left");

    // ─── Plan ──────────────────────────────────────────────────────────────

    static List<JStroke> DoorLeafPlan(JinerySpecView s, JoineryPanel p, CellRect r)
    {
        var f = s.Spec.Frame;
        var t = s.Spec.Sash.LeafThickness;
        var yIn = f.Offset - f.Depth / 2;
        var w = r.X1 - r.X0;
        var left = HingeLeft(p.Operation);
        var op = p.Operation;
        if (op.StartsWith("door-sliding"))
        {
            // It slides along the interior face, towards its side.
            var dir = left ? -1 : 1;
            return [Rect(r.X0, yIn - t - 0.01, r.X1, yIn - 0.01, "cut"),
                .. Arrow(new((r.X0 + r.X1) / 2 - dir * w / 4, yIn - t - 0.08), new((r.X0 + r.X1) / 2 + dir * w / 4, yIn - t - 0.08), "thin")];
        }
        if (op.StartsWith("folding"))
        {
            // Two halves folded towards the hinge side.
            var (hx, sg, half) = (left ? r.X0 : r.X1, left ? 1 : -1, w / 2);
            var fold = new Vec2(hx + sg * half * Math.Cos(Math.PI / 3), yIn - half * Math.Sin(Math.PI / 3));
            return [Line(new(hx, yIn), fold, "outline"), Line(fold, new(hx + sg * 2 * half * Math.Cos(Math.PI / 3), yIn), "outline")];
        }
        var hinge = new Vec2(left ? r.X0 : r.X1, yIn);
        var sgn = left ? 1 : -1;
        var o = new List<JStroke>
        {
            // The leaf open at 90° to the interior: a rectangle from the hinge, cut by the plan (heavy, as the old Lapin drew it).
            Rect(hinge.X, hinge.Y - w, hinge.X + sgn * t, hinge.Y, "cut"),
            left ? Arc(hinge, w, -Math.PI / 2, 0, "thin") : Arc(hinge, w, Math.PI, 1.5 * Math.PI, "thin"),
        };
        if (op.StartsWith("double-acting"))
        {
            var h2 = new Vec2(hinge.X, f.Offset + f.Depth / 2);
            o.Add(left ? Arc(h2, w, 0, Math.PI / 2, "thin", true) : Arc(h2, w, Math.PI / 2, Math.PI, "thin", true));
        }
        return o;
    }

    sealed record JinerySpecView(JoinerySpec Spec);

    static List<JStroke> WindowSwingPlan(JoinerySpec s, JoineryPanel p, CellRect r)
    {
        var yIn = s.Frame.Offset - s.Sash.Depth / 2;
        var w = r.X1 - r.X0;
        var left = HingeLeft(p.Operation);
        var hinge = new Vec2(left ? r.X0 : r.X1, yIn);
        return [Line(hinge, new(hinge.X, yIn - w), "thin"), left ? Arc(hinge, w, -Math.PI / 2, 0, "thin", true) : Arc(hinge, w, Math.PI, 1.5 * Math.PI, "thin", true)];
    }

    /// <summary>The plan at <paramref name="cutZ"/> over the bottom (default: half the height).</summary>
    public static List<JStroke> Plan(JoinerySpec s, double? cutZ = null, bool windowSwings = false)
    {
        var z = cutZ ?? s.Height / 2;
        var boxes = Joinery.Boxes(s);
        var o = new List<JStroke>();
        foreach (var b in boxes.Where(b => b.Min.Z <= z && b.Max.Z >= z))
        {
            if (b.Role is "leaf" or "handle" or "hardware" or "muntin") continue;
            // An outline (an arch, a round frame) is cut where the plane crosses it.
            foreach (var (x0, x1) in JoineryShapes.Spans(b, z))
                o.Add(Rect(x0, b.Min.Y, x1, b.Max.Y, b.Role == "glass" ? "thin" : "cut"));
        }
        foreach (var b in boxes.Where(b => b.Role == "board" && b.Max.Z < z)) o.Add(Rect(b.Min.X, b.Min.Y, b.Max.X, b.Max.Y, "outline"));
        var view = new JinerySpecView(s);
        foreach (var p in Joinery.Normalised(s))
        {
            var r = Joinery.PanelRect(s, p);
            if (r.Z0 > z || r.Z1 < z) continue;
            if (Joinery.IsDoorLeaf(p.Operation)) o.AddRange(DoorLeafPlan(view, p, r));
            else if (windowSwings && Joinery.SwingsInPlan(p.Operation)) o.AddRange(WindowSwingPlan(s, p, r));
        }
        return o;
    }

    // ─── Elevation ─────────────────────────────────────────────────────────

    /// <summary>One panel's opening symbol in its rectangle (seen from the interior).</summary>
    public static List<JStroke> OpeningMarks(string op, CellRect r, bool dashed, string convention = "handle")
    {
        var (xm, zm) = ((r.X0 + r.X1) / 2, (r.Z0 + r.Z1) / 2);
        static Vec2 P(double x, double y) => new(x, y);
        var toHandle = convention == "handle";
        JStroke ApexSide(bool atLeft) => atLeft
            ? new([P(r.X1, r.Z1), P(r.X0, zm), P(r.X1, r.Z0)], "thin", false, dashed)
            : new([P(r.X0, r.Z1), P(r.X1, zm), P(r.X0, r.Z0)], "thin", false, dashed);
        var apexDown = new JStroke([P(r.X0, r.Z1), P(xm, r.Z0), P(r.X1, r.Z1)], "thin", false, dashed);
        var apexUp = new JStroke([P(r.X0, r.Z0), P(xm, r.Z1), P(r.X1, r.Z0)], "thin", false, dashed);
        // Hinged on the left: the apex at the hinges (DIN), or across at the handle.
        JStroke TriangleSide(bool hingeAtLeft) => ApexSide(toHandle ? !hingeAtLeft : hingeAtLeft);
        var bottom = toHandle ? apexUp : apexDown;
        var top = toHandle ? apexDown : apexUp;
        var (w, h) = (r.X1 - r.X0, r.Z1 - r.Z0);
        return op switch
        {
            "fixed" => toHandle ? [new([P(r.X0, r.Z0), P(r.X1, r.Z1)], "thin"), new([P(r.X0, r.Z1), P(r.X1, r.Z0)], "thin")] : [],
            "side-left" or "swing-left" or "double-acting-left" => [TriangleSide(true)],
            "side-right" or "swing-right" or "double-acting-right" => [TriangleSide(false)],
            "tilt-turn-left" => [TriangleSide(true), bottom],
            "tilt-turn-right" => [TriangleSide(false), bottom],
            "bottom-hung" => [bottom],
            "top-hung" => [top],
            "pivot-vertical" => [new([P(xm, r.Z0 - 0.02), P(xm, r.Z1 + 0.02)], "thin", false, true), new([P(r.X0, r.Z1), P(xm, zm), P(r.X0, r.Z0)], "thin", false, dashed), new([P(r.X1, r.Z1), P(xm, zm), P(r.X1, r.Z0)], "thin", false, dashed)],
            "pivot-horizontal" => [new([P(r.X0 - 0.02, zm), P(r.X1 + 0.02, zm)], "thin", false, true), new([P(r.X0, r.Z0), P(xm, zm), P(r.X1, r.Z0)], "thin", false, dashed), new([P(r.X0, r.Z1), P(xm, zm), P(r.X1, r.Z1)], "thin", false, dashed)],
            "sliding-left" or "door-sliding-left" => Arrow(P(xm + w / 4, zm), P(xm - w / 4, zm), "thin"),
            "sliding-right" or "door-sliding-right" => Arrow(P(xm - w / 4, zm), P(xm + w / 4, zm), "thin"),
            "sliding-vertical" => Arrow(P(xm, zm - h / 4), P(xm, zm + h / 4), "thin"),
            "folding-left" => toHandle ? OpeningMarks("folding-right", r, dashed, "hinge") : [new([P(r.X1, r.Z1), P(xm, zm), P(r.X1, r.Z0)], "thin", false, dashed), new([P(xm, r.Z1), P(r.X0, zm), P(xm, r.Z0)], "thin", false, dashed)],
            "folding-right" => toHandle ? OpeningMarks("folding-left", r, dashed, "hinge") : [new([P(r.X0, r.Z1), P(xm, zm), P(r.X0, r.Z0)], "thin", false, dashed), new([P(xm, r.Z1), P(r.X1, zm), P(xm, r.Z0)], "thin", false, dashed)],
            _ => [],
        };
    }

    static List<JStroke> Hardware(JoinerySpec s, JoineryPanel p, CellRect r)
    {
        var op = p.Operation;
        var o = new List<JStroke>();
        const double hw = 0.016, hl = 0.09;
        var leaf = Joinery.IsDoorLeaf(op);
        if (Joinery.SwingsInPlan(op))
        {
            var x = HingeLeft(op) ? r.X0 : r.X1 - hw;
            double[] zs = leaf ? [r.Z0 + 0.25, (r.Z0 + r.Z1) / 2 + 0.2, r.Z1 - 0.25] : [r.Z0 + 0.12, r.Z1 - 0.12 - hl];
            foreach (var z in zs) o.Add(Rect(x, z, x + hw, z + hl, "outline"));
        }
        if (op == "bottom-hung" || op.StartsWith("tilt-turn")) foreach (var x in new[] { r.X0 + 0.1, r.X1 - 0.1 - hl }) o.Add(Rect(x, r.Z0, x + hl, r.Z0 + hw, "outline"));
        if (op == "top-hung") foreach (var x in new[] { r.X0 + 0.1, r.X1 - 0.1 - hl }) o.Add(Rect(x, r.Z1 - hw, x + hl, r.Z1, "outline"));
        // The handle: a lever on the side opposite the hinges.
        var sw = s.Sash.Width / 2;
        var style = Joinery.HandleStyle(s);
        void HandleAt(double x, double z, double dir)
        {
            switch (style)
            {
                case "none": return;
                case "knob": o.Add(Arc(new(x, z), 0.03, 0, 2 * Math.PI, "outline")); o.Add(Arc(new(x, z), 0.012, 0, 2 * Math.PI, "thin")); return;
                case "pull": { var len = leaf ? 0.6 : 0.25; o.Add(Rect(x - 0.0125, z - len / 2, x + 0.0125, z + len / 2, "outline")); return; }
                case "window": o.Add(Rect(x - 0.016, z - 0.04, x + 0.016, z + 0.04, "outline")); o.Add(Rect(x - 0.011, z - 0.12, x + 0.011, z + 0.01, "outline")); return;
                default: o.Add(Arc(new(x, z), 0.026, 0, 2 * Math.PI, "outline")); o.Add(Rect(Math.Min(x, x + dir * 0.13), z - 0.01, Math.Max(x, x + dir * 0.13), z + 0.01, "outline")); return;
            }
        }
        var zh = leaf ? Math.Min(r.Z0 + 1.05, r.Z1 - 0.2) : (r.Z0 + r.Z1) / 2;
        var slides = op.StartsWith("sliding") || op.StartsWith("door-sliding");
        if (op.EndsWith("-left") && !slides) HandleAt(r.X1 - sw, zh, -1);
        else if (op.EndsWith("-right") && !slides) HandleAt(r.X0 + sw, zh, 1);
        else if (op == "bottom-hung") HandleAt((r.X0 + r.X1) / 2, r.Z1 - sw, 1);
        else if (op == "top-hung") HandleAt((r.X0 + r.X1) / 2, r.Z0 + sw, 1);
        return o;
    }

    /// <summary>The outline's strokes and what only a shaped type has: the head's inner curve and transom, a round window's ring, the bars of either, the glazing bars.</summary>
    static IEnumerable<JStroke> ShapeLines(JoinerySpec s)
    {
        yield return new(JoineryShapes.Outline(s, 0, 0), "outline", true);
        var fw = s.Frame.Width;
        if (JoineryShapes.Round(s)) yield return new(JoineryShapes.Ellipse(s, fw), "outline", true);
        else if (JoineryShapes.Headed(s))
        {
            var zs = JoineryShapes.Spring(s);
            var t = Math.Max(s.Frame.Transom, 0.02);
            List<Vec2> fan = [new(s.Width / 2 - fw, zs), .. JoineryShapes.Head(s, fw).Where(p => p.Y > zs + 1e-6), new(-s.Width / 2 + fw, zs)];
            yield return new(fan, "outline", true);
            yield return Line(new(-s.Width / 2 + fw, zs - t), new(s.Width / 2 - fw, zs - t), "outline");
        }
        foreach (var b in Joinery.Boxes(s).Where(b => b.Profile != null && b.Role is "frame" or "muntin" && (b.Max.X - b.Min.X < s.Width * 0.9 || b.Role == "muntin")))
            yield return new(b.Profile!, "thin", true);
    }

    public static List<JStroke> Elevation(JoinerySpec s, bool exterior = false, string convention = "handle", bool hardware = false)
    {
        var shaped = JoineryShapes.Shaped(s) || s.Muntins is { Pattern: not "none" };
        var o = shaped ? ShapeLines(s).ToList() : [Rect(-s.Width / 2, 0, s.Width / 2, s.Height, "outline")];
        if (JoineryShapes.Round(s)) return exterior ? o.Select(x => x with { Pts = x.Pts.Select(q => new Vec2(-q.X, q.Y)).ToList() }).ToList() : o;
        foreach (var p in Joinery.Normalised(s))
        {
            var r = Joinery.PanelRect(s, p);
            o.Add(Rect(r.X0, r.Z0, r.X1, r.Z1, "outline"));
            var sash = p.Operation != "fixed" && (!Joinery.IsDoorLeaf(p.Operation) || p.Glazed);
            var inner = sash ? new CellRect(r.X0 + s.Sash.Width, r.X1 - s.Sash.Width, r.Z0 + s.Sash.Width, r.Z1 - s.Sash.Width) : r;
            if (sash) o.Add(Rect(inner.X0, inner.Z0, inner.X1, inner.Z1, "thin"));
            o.AddRange(OpeningMarks(p.Operation, r, exterior, convention));
            if (hardware) o.AddRange(Hardware(s, p, r));
        }
        if (s.Kind == "window" && s.Board.Exterior > 0 && exterior) o.Add(Rect(-s.Width / 2 - 0.03, -0.04, s.Width / 2 + 0.03, 0, "outline"));
        return exterior ? o.Select(x => x with { Pts = x.Pts.Select(q => new Vec2(-q.X, q.Y)).ToList() }).ToList() : o;
    }

    // ─── Dressings ─────────────────────────────────────────────────────────

    /// <summary>
    /// The dressings (shutters, grille, window box, canopy) in one of the configurator's drawings, for a wall
    /// <paramref name="thickness"/> thick: "elevation" (from inside: only what is on the interior face), "exterior"
    /// (from outside, mirrored like its elevation), "plan" (their outlines, cut where they cross the cut), "section" (cut at <paramref name="cutX"/>).
    /// </summary>
    public static List<JStroke> Dressings(JoinerySpec s, double thickness, string view, double cutX = 0)
    {
        var o = new List<JStroke>();
        var faces = WindowDressings.Of(s, thickness);
        if (view == "plan")
        {
            foreach (var (kind, x0, x1, y0, y1) in Openings.DressingPlan(s, thickness, Openings.PlanCut - s.SillHeight))
                o.Add(Rect(x0, y0, x1, y1, kind == "heavy" ? "cut" : "thin", kind == "overhead"));
            return o;
        }
        foreach (var piece in WindowDressings.Pieces(s, faces))
        {
            foreach (var b in piece.Boxes)
            {
                if (view == "section") { foreach (var (z0, z1) in JoineryShapes.Spans(b, cutX, vertical: true)) o.Add(Rect(b.Min.Y, z0, b.Max.Y, z1, "cut")); continue; }
                // From inside, only what stands on the interior face shows (the rest is behind the glass or the wall).
                if (view == "elevation" && b.Max.Y > faces.In + 1e-6) continue;
                if (b.Profile is { } outline)
                {
                    foreach (var loop in (b.Holes ?? []).Prepend(outline))
                        o.Add(new(view == "exterior" ? loop.Select(q => new Vec2(-q.X, q.Y)).ToList() : loop.ToList(), "thin", true));
                    continue;
                }
                o.Add(view == "exterior" ? Rect(-b.Max.X, b.Min.Z, -b.Min.X, b.Max.Z, "thin") : Rect(b.Min.X, b.Min.Z, b.Max.X, b.Max.Z, "thin"));
            }
            if (piece.Swept is { } sw)
            {
                if (view == "section" && sw.X0 <= cutX && sw.X1 >= cutX) o.Add(new(sw.Profile.Select(q => new Vec2(q.Y, q.Z)).ToList(), "cut", true));
                else if (view == "exterior") o.Add(Rect(-sw.X1, sw.Profile.Min(q => q.Z), -sw.X0, sw.Profile.Max(q => q.Z), "outline"));
            }
        }
        return o;
    }

    // ─── Section ───────────────────────────────────────────────────────────

    /// <summary>Where the section is cut by default: through the middle of the first column's panel.</summary>
    public static double SectionCut(JoinerySpec s)
    {
        var first = Joinery.Normalised(s).FirstOrDefault(p => p.Col == 0);
        return first != null ? (Joinery.PanelRect(s, first).X0 + Joinery.PanelRect(s, first).X1) / 2 : 0;
    }

    public static List<JStroke> Section(JoinerySpec s, double? cutX = null)
    {
        var x = cutX ?? SectionCut(s);
        // Cut where the plane crosses; the handles beside it seen (report #48), in front of the panel they turn.
        var boxes = Joinery.Boxes(s);
        var o = boxes.Where(b => b.Role is not ("handle" or "hardware" or "muntin") && b.Min.X <= x && b.Max.X >= x)
            .SelectMany(b => JoineryShapes.Spans(b, x, vertical: true).Select(z => Rect(b.Min.Y, z.A, b.Max.Y, z.B, b.Role == "glass" ? "thin" : "cut"))).ToList();
        foreach (var b in boxes.Where(b => b.Role is "handle" or "hardware"))
            o.Add(Rect(b.Min.Y, b.Min.Z, b.Max.Y, b.Max.Z, "outline"));
        return o;
    }

    public static (Vec2 Min, Vec2 Max) Bounds(IEnumerable<JStroke> strokes)
    {
        var pts = strokes.SelectMany(s => s.Pts).ToList();
        return (new Vec2(pts.Min(p => p.X), pts.Min(p => p.Y)), new Vec2(pts.Max(p => p.X), pts.Max(p => p.Y)));
    }
}
