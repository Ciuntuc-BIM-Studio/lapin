using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Engine.Tests;

/// <summary>The model: transactions, undo and redo, relationships.</summary>
public class ModelTests
{
    static Entity E(string id, string cls = "IfcWall") => new(id, cls, id, new JsonObject { ["height"] = 3.0 });

    [Fact]
    public void A_transaction_is_one_undo_step_and_redo_puts_it_back()
    {
        var h = new History(new ModelStore());
        h.Execute("make", e => { e.Add(E("a", "IfcBuildingStorey")); e.Add(E("w")); e.Relate("IfcRelContainedInSpatialStructure", "a", "w"); });
        Assert.Equal(2, h.Store.EntityCount);
        Assert.True(h.Dirty);
        h.Undo();
        Assert.Equal(0, h.Store.EntityCount);
        Assert.Equal(0, h.Store.RelationCount);
        h.Redo();
        Assert.Equal("a", h.Store.RelatingOf("w", "IfcRelContainedInSpatialStructure")!.Id);
        Assert.Equal("make", h.UndoLabel);
    }

    [Fact]
    public void A_command_that_throws_leaves_the_model_as_it_was()
    {
        var h = new History(new ModelStore());
        h.Execute("make", e => e.Add(E("a")));
        Assert.Throws<ModelException>(() => h.Execute("bad", e =>
        {
            e.Replace(E("a").With(name: "changed"));
            e.Add(E("b"));
            throw new ModelException("no");
        }));
        Assert.Equal("a", h.Store.Get("a")!.Name);
        Assert.Null(h.Store.Get("b"));
        Assert.Equal("make", h.UndoLabel);
    }

    [Fact]
    public void An_element_has_one_relating_entity_per_relationship_class()
    {
        var h = new History(new ModelStore());
        h.Execute("make", e =>
        {
            e.Add(E("s1", "IfcBuildingStorey")); e.Add(E("s2", "IfcBuildingStorey")); e.Add(E("w1")); e.Add(E("w2"));
            e.Relate("IfcRelContainedInSpatialStructure", "s1", "w1");
            e.Relate("IfcRelContainedInSpatialStructure", "s1", "w2");
        });
        var rel = h.Store.Relations.Single();
        Assert.Equal(["w1", "w2"], rel.Related);   // one relationship per storey, its elements in order
        h.Execute("move", e => e.Relate("IfcRelContainedInSpatialStructure", "s2", "w1"));
        Assert.Equal("s2", h.Store.RelatingOf("w1", "IfcRelContainedInSpatialStructure")!.Id);
        Assert.Equal(["w2"], h.Store.Get("s1") is { } ? h.Store.RelatedOf("s1", "IfcRelContainedInSpatialStructure").Select(x => x.Id) : []);
        h.Undo();
        Assert.Equal("s1", h.Store.RelatingOf("w1", "IfcRelContainedInSpatialStructure")!.Id);
        Assert.Equal(["w1", "w2"], h.Store.RelatedOf("s1", "IfcRelContainedInSpatialStructure").Select(x => x.Id));
    }

    [Fact]
    public void Removing_an_entity_takes_it_out_of_its_relationships()
    {
        var h = new History(new ModelStore());
        h.Execute("make", e =>
        {
            e.Add(E("s", "IfcBuildingStorey")); e.Add(E("w1")); e.Add(E("w2"));
            e.Relate("IfcRelContainedInSpatialStructure", "s", "w1");
            e.Relate("IfcRelContainedInSpatialStructure", "s", "w2");
        });
        h.Execute("delete w1", e => e.Remove("w1"));
        Assert.Equal(["w2"], h.Store.Relations.Single().Related);
        h.Execute("delete s", e => e.Remove("s"));
        Assert.Empty(h.Store.Relations);   // the storey related them: its relationship goes
        Assert.NotNull(h.Store.Get("w2"));
        h.Undo(); h.Undo();
        Assert.Equal(["w1", "w2"], h.Store.Relations.Single().Related);
    }

    [Fact]
    public void Dirty_follows_the_save_point_through_undo_and_redo()
    {
        var h = new History(new ModelStore());
        h.Execute("a", e => e.Add(E("a")));
        h.MarkSaved();
        Assert.False(h.Dirty);
        h.Undo();
        Assert.True(h.Dirty);
        h.Redo();
        Assert.False(h.Dirty);
    }

    [Fact]
    public void Global_ids_are_22_ifc_characters_and_round_trip()
    {
        for (var i = 0; i < 200; i++)
        {
            var g = Guid.NewGuid();
            var id = GlobalId.FromGuid(g);
            Assert.True(GlobalId.IsValid(id), id);
            Assert.Equal(g, GlobalId.ToGuid(id));
        }
        Assert.Equal("0000000000000000000000", GlobalId.FromGuid(Guid.Empty));
        Assert.Equal("3zzzzzzzzzzzzzzzzzzzzz".Replace('z', '$'), GlobalId.FromGuid(new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff")));
    }
}
