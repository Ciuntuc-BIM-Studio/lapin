using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Mep;

/// <summary>
/// The schematic volume of a node by its role, enough to read the network in
/// 3D and check it: a boiler's casing with its display and flue, a radiator's
/// two panels, a basin's bowl, a WC's pan and cistern, a shower's tray and
/// riser, a stack's pipe… Built in the node's own frame (X across, Y back to
/// the wall, Z up from its elevation), so it turns and rises with the node;
/// its size is the role's (or the node's width, depth, height).
/// </summary>
public static class Bodies
{
    public sealed record Piece(string Key, string Label, string Color, Solid Solid);

    const string Steel = "#9aa0a6", White = "#eceae6", Dark = "#4a4f55", Chrome = "#b8bec6", Accent = "#c0392b";

    public static List<Piece> Of(IModelView m, Entity e, Role role, Vec3 size)
    {
        var body = new List<Solid>();
        var detail = new List<Solid>();
        double w = size.X / 2, d = size.Y / 2, h = size.Z;
        Vec3 L(double x, double y, double z) => Nodes.ToWorld(m, e, role, new Vec3(x, y, z));
        // A block between two corners of the node's frame.
        void Block(List<Solid> into, double x0, double x1, double y0, double y1, double z0, double z1)
        {
            if (x1 - x0 < 1e-4 || y1 - y0 < 1e-4 || z1 - z0 < 1e-4) return;
            Vec2[] fp = [L(x0, y0, 0).Xy, L(x1, y0, 0).Xy, L(x1, y1, 0).Xy, L(x0, y1, 0).Xy];
            if (Polygon2.SignedArea(fp) < 0) Array.Reverse(fp);
            if (Prism.ExtrudeFootprint(fp, L(0, 0, z0).Z, z1 - z0, "body") is { } s) into.Add(s);
        }
        // A tube between two points of the node's frame.
        void Tube(List<Solid> into, Vec3 a, Vec3 b, double r, int sides = 14)
        {
            if (Pipes.Tube(L(a.X, a.Y, a.Z), L(b.X, b.Y, b.Z), r, sides, "body") is { } s) into.Add(s);
        }
        // An open box: its floor and four walls (a basin, a bath, a tray).
        void Tray(double x0, double x1, double y0, double y1, double z0, double z1, double t)
        {
            Block(body, x0, x1, y0, y1, z0, z0 + t);
            Block(body, x0, x1, y0, y0 + t, z0, z1); Block(body, x0, x1, y1 - t, y1, z0, z1);
            Block(body, x0, x0 + t, y0 + t, y1 - t, z0, z1); Block(body, x1 - t, x1, y0 + t, y1 - t, z0, z1);
        }
        string color = Nodes.Color(role), detailColor = Dark;
        switch (role.Symbol)
        {
            case "boiler":
                // The casing, its display at the front, the flue out of the top.
                Block(body, -w, w, -d, d, 0, h);
                Block(detail, -w * 0.5, w * 0.5, -d - 0.01, -d, h * 0.55, h * 0.8);
                Tube(detail, new(0, d * 0.2, h), new(0, d * 0.2, h + 0.25), 0.05);
                color = White;
                break;
            case "radiator":
                // Two panels with a gap, standing on its ports' line (the bottom), its back to the wall.
                Block(body, -w, w, -d, -d * 0.2, 0.05, h);
                Block(body, -w, w, d * 0.2, d, 0.05, h);
                Block(detail, -w, w, -d * 0.2, d * 0.2, h - 0.03, h);
                color = White;
                break;
            case "manifold":
                Tube(body, new(-w, 0, h / 2), new(w, 0, h / 2), Math.Min(d, h) / 2);
                for (var k = 0; k < 4; k++) Tube(detail, new(-w * 0.75 + k * w * 0.5, 0, h / 2), new(-w * 0.75 + k * w * 0.5, -d - 0.04, h / 2), 0.012, 8);
                color = Chrome;
                break;
            case "basin":
            case "sink":
                // The bowl, open at the top, on its trap.
                Tray(-w, w, -d, d, 0, h, 0.03);
                Tube(detail, new(0, 0, 0), new(0, 0, -0.25), 0.02, 10);
                color = White;
                break;
            case "bath":
                Tray(-w, w, -d, d, 0, h, 0.05);
                color = White;
                break;
            case "shower":
                // The tray, the mixer at its ports' height, the riser and the head.
                Tray(-w, w, -d, d, 0, Math.Max(h, 0.05), 0.02);
                Block(detail, -0.08, 0.08, d - 0.06, d, 1.05, 1.15);
                Tube(detail, new(0, d - 0.03, 1.15), new(0, d - 0.03, 2.0), 0.012, 10);
                Tube(detail, new(0, d - 0.03, 2.0), new(0, d - 0.25, 2.0), 0.08, 16);
                color = White;
                break;
            case "wc":
                // The pan in front, the cistern on the wall behind.
                Block(body, -w * 0.9, w * 0.9, -d, d - 0.15, 0, h);
                Block(body, -w, w, d - 0.15, d, 0, h);
                Block(detail, -w, w, d - 0.15, d, h, h + 0.4);
                color = White; detailColor = White;
                break;
            case "stack":
                Tube(body, new(0, 0, 0), new(0, 0, h), Math.Max(w, d), 16);
                color = Steel;
                break;
            case "cleanout":
                Tube(body, new(-w, 0, h / 2), new(w, 0, h / 2), Math.Min(d, h) / 2);
                Tube(detail, new(0, 0, h / 2), new(0, 0, h + 0.03), Math.Min(d, h) / 2.5);
                color = Steel;
                break;
            case "valve":
                // Two cones (as tubes narrowing to the seat) and the handle.
                Tube(body, new(-w, 0, h / 2), new(0, 0, h / 2), Math.Min(d, h) / 2);
                Tube(body, new(0, 0, h / 2), new(w, 0, h / 2), Math.Min(d, h) / 2);
                Block(detail, -0.01, 0.01, -0.01, 0.01, h / 2, h + 0.03);
                Block(detail, -0.04, 0.04, -0.01, 0.01, h + 0.03, h + 0.045);
                color = "#b0803a"; detailColor = Accent;
                break;
            case "meter":
                Tube(body, new(-w, 0, h / 2), new(w, 0, h / 2), Math.Min(d, h) / 2);
                if (Prism.Cylinder(L(0, 0, 0).Xy, L(0, 0, h).Z, Math.Min(d, h) * 0.6, 0.03, 18, "body") is { } dial) detail.Add(dial);
                color = "#b0803a";
                break;
            case "panel":
                Block(body, -w, w, -d, d, 0, h);
                Block(detail, -w * 0.9, w * 0.9, -d - 0.005, -d, h * 0.1, h * 0.9);
                color = White;
                break;
            case "outlet":
            case "switch":
                Block(body, -w, w, -0.01, d, 0, h);
                Block(detail, -w * 0.6, w * 0.6, -0.015, -0.01, h * 0.2, h * 0.8);
                color = White;
                break;
            case "light":
                // A disc on its cable from the ceiling.
                if (Prism.Cylinder(L(0, 0, 0).Xy, L(0, 0, 0).Z, Math.Max(w, d), h, 20, "body") is { } disc) body.Add(disc);
                Tube(detail, new(0, 0, h), new(0, 0, h + 0.3), 0.005, 6);
                color = "#f5e7a1";
                break;
            case "fan":
                Tube(body, new(-w, 0, h / 2), new(w, 0, h / 2), Math.Min(d, h) / 2, 18);
                Tube(detail, new(-w * 0.2, 0, h / 2), new(w * 0.2, 0, h / 2), Math.Min(d, h) / 5, 12);
                color = Steel;
                break;
            case "diffuser":
            case "grille":
                Block(body, -w, w, -d, d, 0, 0.02);
                if (role.Symbol == "diffuser") Block(detail, -w * 0.6, w * 0.6, -d * 0.6, d * 0.6, -0.02, 0);
                else for (var k = 0; k < 5; k++) Block(detail, -w * 0.9, w * 0.9, -d * 0.8 + k * d * 0.4 - 0.005, -d * 0.8 + k * d * 0.4 + 0.005, -0.02, 0);
                Tube(detail, new(0, 0, 0.02), new(0, 0, h), 0.06, 14);
                color = White;
                break;
            case "cap":
                Tube(body, new(-w, 0, h / 2), new(w, 0, h / 2), Math.Min(d, h) / 2, 12);
                color = Steel;
                break;
            default:
                Block(body, -w, w, -d, d, 0, h);
                break;
        }
        var o = new List<Piece>();
        if (body.Count > 0) o.Add(new("body", role.Label, color, Openings.Combine(body, "body")));
        if (detail.Count > 0) o.Add(new("detail", role.Label + " detail", detailColor, Openings.Combine(detail, "detail")));
        return o;
    }
}
