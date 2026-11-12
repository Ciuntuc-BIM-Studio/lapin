using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drafting.Cad;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// A DXF's blocks kept as symbols (docs/plan-user-extensibility.md §3.2): asked for, each block a model symbol read once, each
/// insert an instance where the block stood, turned and turned over as it was; not asked for, blocks exploded as always.
/// </summary>
public sealed class DxfBlockSymbolTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());
    static string Plan(Fixture f) => (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey, ["scale"] = 100 })!["id"]!;

    static string Dxf(string entities, string blocks) => string.Join("\n", new[]
    {
        "0", "SECTION", "2", "HEADER", "9", "$ACADVER", "1", "AC1015", "9", "$INSUNITS", "70", "4", "0", "ENDSEC",
        "0", "SECTION", "2", "BLOCKS", blocks, "0", "ENDSEC",
        "0", "SECTION", "2", "ENTITIES", entities, "0", "ENDSEC", "0", "EOF",
    });

    // A block DOOR: a leaf 1000 mm along +x and its swing, an arc of 1000 mm from 0° to 90°.
    static readonly string Blocks = string.Join("\n",
        "0", "BLOCK", "2", "DOOR", "10", "0", "20", "0",
        "0", "LINE", "8", "0", "10", "0", "20", "0", "11", "1000", "21", "0",
        "0", "ARC", "8", "0", "10", "0", "20", "0", "40", "1000", "50", "0", "51", "90",
        "0", "ENDBLK");

    // Inserted twice: at (5000, 5000) turned 90°, and at (0, 0) mirrored (x scale −1).
    static readonly string Entities = string.Join("\n",
        "0", "INSERT", "8", "DOORS", "2", "DOOR", "10", "5000", "20", "5000", "50", "90",
        "0", "INSERT", "8", "DOORS", "2", "DOOR", "10", "0", "20", "0", "41", "-1", "42", "1");

    static JsonObject Args(string view, Action<JsonObject>? more = null)
    {
        var a = new JsonObject { ["viewId"] = view, ["name"] = "doors.dxf", ["data"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(Dxf(Entities, Blocks))) };
        more?.Invoke(a);
        return a;
    }

    static List<(Vec2 A, Vec2 B)> Drawn(Fixture f, string view, string id) =>
        Views.Render(f.Session.Reader, f.Model.Get(view)!).Marks.Single(m => m.Owner == id).Lines.Where(l => l.Layer != "PICK").Select(l => (l.A, l.B)).ToList();

    [Fact]
    public void Blocks_kept_as_symbols_are_one_symbol_and_an_instance_for_each_insert()
    {
        var f = F();
        var plan = Plan(f);
        var r = f.Run("drafting.importDxf", Args(plan, a => a["blocks"] = "symbols"))!;
        Assert.Equal(1, (int)r["symbols"]!);
        Assert.Equal(2, (int)r["instances"]!);
        var symbol = f.Model.OfClass(Symbols.Class).Single();
        Assert.Equal("DOOR", symbol.Name);
        var def = SymbolDef.From(symbol.Attrs["def"])!;
        Assert.Equal("model", def.Scale);
        Assert.Equal(2, def.Items.Count);
        var instances = Symbols.Instances(f.Model).ToList();
        Assert.Equal(2, instances.Count);
        Assert.All(instances, i => Assert.Equal("DOORS", i.Str("layer")));
        // The turned one: its leaf from (5, 5) up to (5, 6).
        var turned = instances.Single(i => Math.Abs(Symbols.Pose(i).At.X - 5) < 1e-9);
        Assert.Equal(90, Symbols.Pose(turned).Angle, 6);
        Assert.Contains(Drawn(f, plan, turned.Id), l => (l.A - new Vec2(5, 5)).Length < 1e-6 && (l.B - new Vec2(5, 6)).Length < 1e-6);
        // The mirrored one: turned over, its leaf from (0, 0) to (−1, 0).
        var mirrored = instances.Single(i => Math.Abs(Symbols.Pose(i).At.X) < 1e-9);
        Assert.True(Symbols.Pose(mirrored).FlipX);
        Assert.Contains(Drawn(f, plan, mirrored.Id), l => (l.A - new Vec2(0, 0)).Length < 1e-6 && (l.B - new Vec2(-1, 0)).Length < 1e-6);
        // No drafting left from the blocks.
        Assert.Empty(Drafts.On(f.Model, plan));
    }

    [Fact]
    public void Not_asked_for_blocks_are_exploded_as_before()
    {
        var f = F();
        var plan = Plan(f);
        f.Run("drafting.importDxf", Args(plan));
        Assert.Empty(f.Model.OfClass(Symbols.Class));
        Assert.Empty(Symbols.Instances(f.Model));
        Assert.Equal(4, Drafts.On(f.Model, plan).Count());
    }
}
