using System.Text.Json.Nodes;
using Lapin.Architecture.Roofs;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Wood;

/// <summary>
/// A joisted floor type's rules (docs/plan-wood.md stage 3, decision 244), kept on the floor type as <c>wood</c> with
/// <c>construction: joists</c>: the joists' section, spacing and grade, their direction (the short way, or along x or y),
/// the rim joist round the edge, the blocking rows, and round each opening in the floor (a stairwell) the trimmers doubled
/// along its sides and the headers across its ends; the cavities filled with the core's material or left empty.
/// </summary>
public sealed record JoistSpec(string Section, double Spacing, string Grade, string Direction, bool Rim, int BlockingRows, int Trimmers, int HeaderPlies, bool Fill)
{
    public static JoistSpec Default => new("45x220 mm", 0.6, "C24", "auto", true, 1, 2, 2, false);

    public JsonObject ToJson() => new()
    {
        ["construction"] = "joists", ["section"] = Section, ["spacing"] = Spacing, ["grade"] = Grade, ["direction"] = Direction, ["rim"] = Rim,
        ["blockingRows"] = BlockingRows, ["trimmers"] = Trimmers, ["headerPlies"] = HeaderPlies, ["fill"] = Fill,
    };

    public static JoistSpec From(JsonNode? n)
    {
        var d = Default;
        if (n is not JsonObject o) return d;
        bool B(string k, bool def) => o[k] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : def;
        return new JoistSpec((string?)o["section"] ?? d.Section, Json.Number(o["spacing"]) ?? d.Spacing, (string?)o["grade"] ?? d.Grade,
            CltSpec.Directions.Contains((string?)o["direction"] ?? "") ? (string)o["direction"]! : d.Direction, B("rim", d.Rim),
            (int)(Json.Number(o["blockingRows"]) ?? d.BlockingRows), (int)(Json.Number(o["trimmers"]) ?? d.Trimmers), (int)(Json.Number(o["headerPlies"]) ?? d.HeaderPlies), B("fill", d.Fill));
    }

    public static JoistSpec? Of(Entity? type) => type?.Attrs[FrameSpec.Key] is JsonObject o && (string?)o["construction"] == "joists" ? From(o) : null;

    public string? Problem()
    {
        if (Spacing < 0.2 || Spacing > 1.2) return "Joists are 200 to 1200 mm apart.";
        if (FrameSpec.Size(Section, (0, 0)).W <= 0) return $"„{Section}” is not a section (write it as 45x220 mm).";
        if (BlockingRows is < 0 or > 5 || Trimmers is < 1 or > 4 || HeaderPlies is < 1 or > 4) return "A floor has 0 to 5 blocking rows, 1 to 4 trimmers and 1 to 4 header plies.";
        return null;
    }
}

public static class Joists
{
    /// <summary>A floor member: its outline in plan, standing from z0 to z1.</summary>
    public sealed record Member(string Role, IReadOnlyList<Vec2> Outline, double Z0, double Z1, string Section)
    {
        public double Length
        {
            get
            {
                var e = Enumerable.Range(0, Outline.Count).Select(i => (Outline[(i + 1) % Outline.Count] - Outline[i]).Length).ToList();
                return e.Count == 4 ? Math.Max(e[0], e[1]) : e.Max();
            }
        }
        public double Volume => Math.Abs(Polygon2.SignedArea(Outline.ToList())) * (Z1 - Z0);
    }

    public sealed record Result(List<Member> Members, List<string> Warnings, Vec2 Along);

    /// <summary>
    /// The joists of a floor: its outline and its openings in plan, its core between <paramref name="z0"/> and <paramref name="z1"/>.
    /// The joists run the way the spec says (the short way of the outline's box by default), at the spacing across it, from rim to
    /// rim; round each opening the trimmers along its sides, the headers across its ends, the joists stopped there (tails).
    /// </summary>
    public static Result Layout(JoistSpec spec, IReadOnlyList<Vec2> outline, IReadOnlyList<IReadOnlyList<Vec2>> holes, double z0, double z1)
    {
        var members = new List<Member>();
        var warnings = new List<string>();
        var (t, depth) = FrameSpec.Size(spec.Section, (0.045, 0.22));
        if (Math.Abs((z1 - z0) - depth) > 0.003) warnings.Add($"The core is {Math.Round((z1 - z0) * 1000)} mm thick and the joists {Math.Round(depth * 1000)} mm deep.");
        var (x0, x1, y0, y1) = (outline.Min(p => p.X), outline.Max(p => p.X), outline.Min(p => p.Y), outline.Max(p => p.Y));
        var alongX = spec.Direction == "x" || spec.Direction == "auto" && x1 - x0 <= y1 - y0;
        var d = alongX ? new Vec2(1, 0) : new Vec2(0, 1);
        var n = new Vec2(-d.Y, d.X);
        Vec2 W(double u, double v) => d * u + n * v;
        List<Vec2> Rect(double u0, double u1, double v0, double v1) => [W(u0, v0), W(u1, v0), W(u1, v1), W(u0, v1)];
        void Add(string role, List<Vec2> o) => members.Add(new Member(role, o, z0, z1, spec.Section));
        var vs = outline.Select(p => Vec2.Dot(p, n)).ToList();
        var (vmin, vmax) = (vs.Min(), vs.Max());
        var rim = spec.Rim ? t : 0;

        // The rim joists along every edge, on its inside.
        if (spec.Rim)
        {
            var ccw = Polygon2.SignedArea(outline.ToList()) > 0;
            for (var i = 0; i < outline.Count; i++)
            {
                var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
                if ((b - a).Length < 0.05) continue;
                var e = (b - a).Normalized();
                var inward = ccw ? e.Perp : -e.Perp;
                members.Add(new Member("rim", [a, b, b + inward * t, a + inward * t], z0, z1, spec.Section));
            }
        }
        // The runs of a line across the floor at v, inside the rims.
        List<(double U0, double U1)> Runs(double v) =>
            RoofParts.Crossings(outline, n * v, d).Select(c => (c.T0 + rim, c.T1 - rim)).Where(c => c.Item2 - c.Item1 > 0.05).ToList();

        // Each opening's box along and across the joists; its trimmers and headers.
        var boxes = holes.Where(h => h.Count >= 3).Select(h => (U0: h.Min(p => Vec2.Dot(p, d)), U1: h.Max(p => Vec2.Dot(p, d)), V0: h.Min(p => Vec2.Dot(p, n)), V1: h.Max(p => Vec2.Dot(p, n)))).ToList();
        var blocked = new List<(double V0, double V1)>();
        var stops = new List<(double V0, double V1, double U0, double U1)>();
        foreach (var b in boxes)
        {
            var tv0 = b.V0 - spec.Trimmers * t;
            var tv1 = b.V1 + spec.Trimmers * t;
            for (var i = 0; i < spec.Trimmers; i++)
            {
                foreach (var v in new[] { b.V0 - (i + 0.5) * t, b.V1 + (i + 0.5) * t })
                    foreach (var (u0, u1) in Runs(v)) Add("trimmer", Rect(u0, u1, v - t / 2, v + t / 2));
            }
            var hu0 = b.U0 - spec.HeaderPlies * t;
            var hu1 = b.U1 + spec.HeaderPlies * t;
            for (var p = 0; p < spec.HeaderPlies; p++)
            {
                Add("header", Rect(b.U0 - (p + 1) * t, b.U0 - p * t, b.V0, b.V1));
                Add("header", Rect(b.U1 + p * t, b.U1 + (p + 1) * t, b.V0, b.V1));
            }
            blocked.Add((tv0 - 0.05, tv1 + 0.05));
            stops.Add((b.V0, b.V1, hu0, hu1));
        }

        // The joists at the spacing across, the last one against the far rim; stopped at the openings' headers.
        var lines = new List<double>();
        for (var v = vmin + rim + t / 2; v <= vmax - rim - t / 2 + 1e-9; v += spec.Spacing) lines.Add(v);
        var last = vmax - rim - t / 2;
        if (lines.Count == 0 || last - lines[^1] > spec.Spacing / 3) lines.Add(last);
        var joists = new List<(double V, List<(double U0, double U1)> Runs)>();
        // A joist dropped beside a trimmer must not leave more than the spacing: where two neighbours (joists or trimmers)
        // would stand further apart, joists go evenly between them.
        var kept = lines.Where(v => !blocked.Any(bv => v + t / 2 > bv.V0 && v - t / 2 < bv.V1 && !stops.Any(s => v > s.V0 && v < s.V1))).ToList();
        var trimmerLines = boxes.SelectMany(b => Enumerable.Range(0, spec.Trimmers).SelectMany(i => new[] { b.V0 - (i + 0.5) * t, b.V1 + (i + 0.5) * t })).ToList();
        var all = kept.Concat(trimmerLines).OrderBy(v => v).ToList();
        var extra = new List<double>();
        for (var i = 0; i + 1 < all.Count; i++)
        {
            var gap = all[i + 1] - all[i];
            if (gap <= spec.Spacing + 1e-6) continue;
            var count = (int)Math.Ceiling(gap / spec.Spacing - 1e-9) - 1;
            for (var k = 1; k <= count; k++) extra.Add(all[i] + gap * k / (count + 1));
        }
        lines = [.. kept, .. extra];
        lines.Sort();
        foreach (var v in lines)
        {
            if (blocked.Any(bv => v + t / 2 > bv.V0 && v - t / 2 < bv.V1 && !stops.Any(s => v > s.V0 && v < s.V1))) continue;
            var runs = Runs(v);
            foreach (var s in stops.Where(s => v > s.V0 && v < s.V1))
                runs = runs.SelectMany(r => r.U1 <= s.U0 || r.U0 >= s.U1 ? [r] : new[] { (r.U0, Math.Min(r.U1, s.U0)), (Math.Max(r.U0, s.U1), r.U1) }.Where(x => x.Item2 - x.Item1 > 0.05)).ToList();
            var tail = stops.Any(s => v > s.V0 && v < s.V1);
            foreach (var (u0, u1) in runs) Add(tail ? "tail" : "joist", Rect(u0, u1, v - t / 2, v + t / 2));
            joists.Add((v, runs));
        }

        // Blocking rows: between neighbouring joists, at even stations along their span.
        if (spec.BlockingRows > 0 && joists.Count > 1)
        {
            var us = outline.Select(p => Vec2.Dot(p, d)).ToList();
            var (umin, umax) = (us.Min(), us.Max());
            for (var k = 1; k <= spec.BlockingRows; k++)
            {
                var u = umin + (umax - umin) * k / (spec.BlockingRows + 1);
                for (var i = 0; i + 1 < joists.Count; i++)
                {
                    var (va, ra) = joists[i];
                    var (vb, rb) = joists[i + 1];
                    if (!ra.Any(r => u > r.U0 && u < r.U1) || !rb.Any(r => u > r.U0 && u < r.U1)) continue;
                    if (boxes.Any(b => u > b.U0 - t && u < b.U1 + t && vb > b.V0 && va < b.V1)) continue;
                    if (vb - va - t < 0.02) continue;
                    Add("blocking", Rect(u - t / 2, u + t / 2, va + t / 2, vb - t / 2));
                }
            }
        }
        return new Result(members, warnings, d);
    }

    public static string Label(string role) => role switch
    {
        "joist" => "Joist", "tail" => "Tail joist", "trimmer" => "Trimmer", "header" => "Header", "rim" => "Rim joist", "blocking" => "Blocking", _ => role,
    };

    /// <summary>A joisted floor where it is: its spec and its members, from its outline, its openings and its core.</summary>
    public sealed record FloorFrame(Entity Slab, JoistSpec Spec, Result Result, int CoreIndex, string? CoreMaterial);

    public static FloorFrame? Of(IModelView m, Entity slab)
    {
        if (slab.Class != IfcClass.Slab || JoistSpec.Of(m.RelatingOf(slab.Id, IfcClass.RelDefinesByType)) is not { } spec) return null;
        if (LayerSets.BandsOf(m, slab.Id) is not { } bands || !bands.Any(b => b.Core)) return null;
        var core = bands.First(b => b.Core);
        var outline = Lapin.Architecture.Slabs.Contour(slab);
        if (outline.Count < 3) return null;
        var top = Lapin.Architecture.Slabs.Top(m, slab);
        var holes = m.RelatedOf(slab.Id, IfcClass.RelVoidsElement).Where(o => o.Class == IfcClass.OpeningElement && o.Attrs["contour"] != null)
            .Select(o => (IReadOnlyList<Vec2>)Lapin.Architecture.Slabs.Contour(o)).ToList();
        return new FloorFrame(slab, spec, Layout(spec, outline, holes, top + core.From, top + core.To), core.Index, core.Material);
    }

    /// <summary>A joisted floor's members in place of its core, each a part; the cavities filled with the core's material when asked.</summary>
    public static ElementGeometry Apply(Entity slab, ElementGeometry g, IModelView m, IServices services, FloorFrame f)
    {
        var corePart = g.Parts.FirstOrDefault(p => p.Key == $"layer:{f.CoreIndex}");
        var parts = g.Parts.Where(p => p != corePart).ToList();
        var solids = new List<Solid>();
        var counts = new Dictionary<string, int>();
        foreach (var mb in f.Result.Members)
        {
            if (Prism.ExtrudeFootprint(mb.Outline.ToList(), mb.Z0, mb.Z1 - mb.Z0, mb.Role) is not { } body) continue;
            solids.Add(body);
            var i = counts[mb.Role] = counts.GetValueOrDefault(mb.Role) + 1;
            parts.Add(new GeometryPart($"{Label(mb.Role)} {WoodModule.Section(mb.Section)}", mb.Role is "rim" or "header" ? "#a87a46" : "#c99a64", body, null, $"wood:{mb.Role}:{i}")
            {
                PropertySet = "Lapin_Timber",
                Properties = [("Role", Label(mb.Role)), ("Section", WoodModule.Section(mb.Section)), ("Grade", f.Spec.Grade), ("Length", Math.Round(mb.Length, 3).ToString(System.Globalization.CultureInfo.InvariantCulture))],
            });
        }
        if (f.Spec.Fill && corePart != null && services.Get<ISolidBooleans>() is { } booleans)
        {
            try
            {
                if (booleans.Difference(corePart.Solid, solids, "infill") is { } infill)
                    parts.Add(new GeometryPart("Infill", corePart.Color, infill, f.CoreMaterial, "wood-insulation"));
            }
            catch (InvalidOperationException) { }
        }
        return g with { Parts = parts, Diagnostics = f.Result.Warnings.Count > 0 ? [.. g.Diagnostics ?? [], .. f.Result.Warnings] : g.Diagnostics };
    }
}
