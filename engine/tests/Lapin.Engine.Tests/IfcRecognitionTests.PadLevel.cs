using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Ifc;
using Lapin.Platform;
using Lapin.Structure;
using static Lapin.Ifc.Step;

namespace Lapin.Engine.Tests;

/// <summary>
/// A recognised pad footing stays where the file puts it (decision 507, docs/spec-ifc.md, Fidel fișierului): a column over it
/// does not raise it to its base, as a pad drawn in Lapin is raised; the gate measures it as the file's.
/// </summary>
public partial class IfcRecognitionTests
{
    [Fact]
    public void A_recognised_pad_footing_under_a_column_stays_where_the_file_puts_it()
    {
        var x = new Foreign();
        // A column 400 × 400 at (2, 1), 3 m high from the storey; a pad 1.5 × 1.5 × 0.5 under it with its top 1 m down (a gap the file has).
        x.Element("IFCCOLUMN", "pad-column", "C1", x.Place(2000, 1000, 0), x.Shape(x.Box(0, 0, 0, 400, 400, 3000)), E("COLUMN"));
        x.Element("IFCFOOTING", "pad-under", "F1", x.Place(2000, 1000, 0), x.Shape(x.Box(0, 0, -1500, 1500, 1500, 500)), E("PAD_FOOTING"));
        var path = x.Write(Path.Combine(_dir, "pad-under-column.ifc"));
        var f = Full();
        IfcImport.Run(f.Session, path, null, out _);
        var pad = f.Model.Get(Guid("foreign-test", "pad-under"))!;
        // Recognised under the gate (enforced): native, not a reference with the file's body.
        Assert.False(StoredBodies.Has(pad));
        Assert.False(PadFootings.Follows(pad));
        var (min, max) = f.Bounds(pad.Id);
        Assert.Equal((-1.5, -1.0), (Math.Round(min.Z, 6), Math.Round(max.Z, 6)));
        var level = PadFootings.Level(pad, f.Model)!;
        Assert.Equal("storey", level.Rule);
        Assert.Null(level.Column);

        // Asked to, it follows the column: its top on the column's base.
        f.Run("structure.setPadLevel", new JsonObject { ["id"] = pad.Id, ["follow"] = true });
        var (_, up) = f.Bounds(pad.Id);
        Assert.Equal(0, up.Z, 6);
    }
}
