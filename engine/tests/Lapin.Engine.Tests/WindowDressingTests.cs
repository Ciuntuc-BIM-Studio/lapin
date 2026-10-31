using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// What dresses a window on the facade (reports #38 and #39, decision 174): shutters, a safety grille,
/// a window box, a canopy, each set by the host wall's real faces (its outermost layers', or the
/// surround's), moved by hand along the wall from the window's axis and off the face, drawn in 3D,
/// in plan and in the configurator, measured.
/// </summary>
public class WindowDressingTests
{
    /// <summary>Layers run from the wall's right face (the exterior, the lowest Y of a wall drawn along +X) to its left.</summary>
    static string Rendered(Fixture f) => f.Type("Rendered", ("Render", 0.02, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
    static GeometryPart? Part(Fixture f, string id, string key) => f.Session.Geometry(id)!.Parts.FirstOrDefault(p => p.Key == key);
    static (Vec3 Min, Vec3 Max) Bounds(GeometryPart p) => Measure.Bounds(p.Solid)!.Value;

    static (string Window, double Exterior, double Interior, JoinerySpec Spec) Window(JoineryDetails details)
    {
        var f = Current = new Fixture();
        var wall = f.Wall(0, 0, 6, 0, Rendered(f));
        var type = (string)f.Run("architecture.createJoineryType", new JsonObject { ["spec"] = (JoinerySpec.DefaultWindow("Dressed") with { Details = details }).ToJson() })!["id"]!;
        var window = (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = 3, ["typeId"] = type })!["id"]!;
        var bands = Walls.Section(f.Model, f.Model.Get(wall)!);
        return (window, bands.Min(b => b.From), bands.Max(b => b.To), Openings.SpecOf(f.Model, window)!);
    }

    static Fixture Current = null!;

    [Fact]
    public void Shutters_stand_off_the_walls_exterior_face_closed_over_the_opening_or_open_beside_it()
    {
        var (w, ext, _, spec) = Window(new JoineryDetails(Shutters: new JoineryShutters(true, "louvred", 2, Open: false, Thickness: 0.03, Gap: 0.01, Overlap: 0.04)));
        var b = Bounds(Part(Current, w, "shutters")!);
        // Closed: the opening and 40 mm past each jamb, 10 mm off the render's face, 30 mm thick, the opening's height.
        Assert.Equal(3 - spec.Width / 2 - 0.04, b.Min.X, 6);
        Assert.Equal(3 + spec.Width / 2 + 0.04, b.Max.X, 6);
        Assert.Equal(ext - 0.01, b.Max.Y, 6);
        Assert.Equal(ext - 0.04, b.Min.Y, 6);
        Assert.Equal(spec.SillHeight, b.Min.Z, 6);
        Assert.Equal(spec.SillHeight + spec.Height, b.Max.Z, 6);

        // Open: each leaf folded flat against the wall beside the opening, nothing over it.
        var (w2, _, _, _) = Window(new JoineryDetails(Shutters: new JoineryShutters(true, "planks", 2, Open: true, Overlap: 0.04)));
        var open = Part(Current, w2, "shutters")!;
        var leaf = (spec.Width + 0.08) / 2;
        Assert.Equal(3 - spec.Width / 2 - 0.04 - leaf, Bounds(open).Min.X, 6);
        Assert.Equal(3 + spec.Width / 2 + 0.04 + leaf, Bounds(open).Max.X, 6);
        Assert.DoesNotContain(open.Solid.Vertices, v => v.X > 3 - spec.Width / 2 + 0.05 && v.X < 3 + spec.Width / 2 - 0.05);

        // With a surround, they stand off the surround's face; moved by hand 200 mm along the wall and 20 mm further out.
        var (w3, ext3, _, _) = Window(new JoineryDetails(Surround: new JoinerySurround(true, 0.1, 0.025),
            Shutters: new JoineryShutters(true, "panel", 1, Open: false, Thickness: 0.03, Gap: 0.03, Overlap: 0, Along: 0.2)));
        var moved = Bounds(Part(Current, w3, "shutters")!);
        Assert.Equal(ext3 - 0.025 - 0.03, moved.Max.Y, 6);
        Assert.Equal(3.2, (moved.Min.X + moved.Max.X) / 2, 6);
    }

    [Fact]
    public void A_grille_sits_in_the_reveal_set_back_from_the_face_or_on_the_facade_with_its_bars_spaced()
    {
        var (w, ext, inn, spec) = Window(new JoineryDetails(Grille: new JoineryGrille(true, "reveal", "horizontal", Bar: 0.012, Spacing: 0.1, Gap: 0.04)));
        var g = Part(Current, w, "grille")!;
        var b = Bounds(g);
        // In the clear opening, 40 mm in from the exterior face, short of the frame.
        Assert.Equal(3 - spec.Width / 2, b.Min.X, 6);
        Assert.Equal(3 + spec.Width / 2, b.Max.X, 6);
        Assert.Equal(ext + 0.04, b.Min.Y, 6);
        // Never into the frame (its exterior face, from the wall's middle).
        Assert.True(b.Max.Y <= (ext + inn) / 2 - (spec.Frame.Offset + spec.Frame.Depth / 2) + 1e-9);
        // Horizontal bars no more than 100 mm apart between the frame's rails.
        var bars = WindowDressings.Stations(0.03, spec.Height - 0.03, 0.1);
        Assert.True(bars.Count >= (int)((spec.Height - 0.06) / 0.1) - 1);
        Assert.All(bars.Zip(bars.Skip(1)), p => Assert.True(p.Second - p.First <= 0.1 + 1e-9));

        var (w2, ext2, _, spec2) = Window(new JoineryDetails(Grille: new JoineryGrille(true, "outside", "grid", Gap: 0.02, Overlap: 0.05)));
        var o = Bounds(Part(Current, w2, "grille")!);
        Assert.Equal(spec2.Width + 0.1, o.Max.X - o.Min.X, 6);
        Assert.Equal(ext2 - 0.02, o.Max.Y, 6);
        var q = Current.Session.Quantities(w2).Single(s => s.Name == "Lapin_Details").Quantities.ToDictionary(x => x.Name, x => x.Value);
        Assert.Equal((spec2.Width + 0.1) * (spec2.Height + 0.1), q["GrilleArea"], 6);
    }

    [Fact]
    public void A_grille_goes_in_the_reveal_on_the_rooms_side_by_default_never_into_the_frame()
    {
        // Report #47: safety grilles are fixed inside, in the opening, seen from the room in front of the glass.
        Assert.Equal("revealInside", new JoineryGrille().Side);
        var (w, ext, inn, spec) = Window(new JoineryDetails(Grille: new JoineryGrille(true, Gap: 0.04)));
        var b = Bounds(Part(Current, w, "grille")!);
        Assert.Equal(3 - spec.Width / 2, b.Min.X, 6);
        Assert.Equal(3 + spec.Width / 2, b.Max.X, 6);
        Assert.Equal(inn - 0.04, b.Max.Y, 6);
        Assert.True(b.Min.Y >= (ext + inn) / 2 - (spec.Frame.Offset - spec.Frame.Depth / 2) - 1e-9);
    }

    [Fact]
    public void A_window_box_hangs_under_the_window_and_a_canopy_over_it_both_off_the_face()
    {
        var (w, ext, _, spec) = Window(new JoineryDetails(
            WindowBox: new JoineryWindowBox(true, Width: 1.0, Depth: 0.2, Height: 0.18, Gap: 0.01, Drop: 0.06, Along: -0.1),
            Canopy: new JoineryCanopy(true, Projection: 0.6, Thickness: 0.06, Overhang: 0.15, Rise: 0.15, Slope: 0, Gap: 0)));
        var box = Bounds(Part(Current, w, "windowbox")!);
        Assert.Equal(1.0, box.Max.X - box.Min.X, 6);
        Assert.Equal(2.9, (box.Min.X + box.Max.X) / 2, 6);
        Assert.Equal(spec.SillHeight - 0.06, box.Max.Z, 6);
        Assert.Equal(ext - 0.01, box.Max.Y, 6);
        Assert.Equal(ext - 0.21, box.Min.Y, 6);
        var canopy = Bounds(Part(Current, w, "canopy")!);
        Assert.Equal(spec.SillHeight + spec.Height + 0.15, canopy.Min.Z, 6);
        Assert.Equal(spec.Width + 0.3, canopy.Max.X - canopy.Min.X, 6);
        Assert.Equal(ext - 0.6, canopy.Min.Y, 6);
    }

    [Fact]
    public void The_plan_and_the_configurators_drawings_show_the_dressings()
    {
        var (w, ext, _, _) = Window(new JoineryDetails(Shutters: new JoineryShutters(true, Open: true)));
        var plan = Current.Session.Geometry(w)!.Plan!;
        // The open leaves cut in plan, outside the exterior face.
        Assert.Contains(plan, i => i.Kind == "heavy" && i.Closed && i.Points.All(p => p.Y < ext + 1e-6));

        var spec = (JoinerySpec.DefaultWindow("W") with { Details = new JoineryDetails(Grille: new JoineryGrille(true, "outside")) }).ToJson();
        int Count(JsonNode spec, string view) => Current.Session.Query("architecture.joineryPreview", new JsonObject { ["spec"] = spec.DeepClone() })!["drawings"]![view]!.ToJsonString().Length;
        var plain = JoinerySpec.DefaultWindow("W").ToJson();
        Assert.True(Count(spec, "exterior") > Count(plain, "exterior"));
        Assert.True(Count(spec, "plan") > Count(plain, "plan"));
        Assert.Equal(Count(spec, "elevation"), Count(plain, "elevation")); // from inside, an outside grille is behind the glass
    }
}
