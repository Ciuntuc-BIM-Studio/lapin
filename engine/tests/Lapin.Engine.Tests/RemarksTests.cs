using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Platform.Cde.Remarks;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// Remarks on the model as BCF (docs/plan-cde.md, stage 2): a file written
/// and read back with every field, in 2.1 and in 3.0; hand-written minimal
/// files of both versions read; the topic GUID kept through the project and
/// a round trip; components by GlobalId; the commands and queries; the
/// import and export through the protocol.
/// </summary>
public sealed class RemarksTests
{
    static Fixture F() => new(new ArchitectureModule());

    /// <summary>A minimal PNG (1×1, as a tool would write it), enough to travel as bytes.</summary>
    static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    static BcfTopic FullTopic(string wallId) => new(
        Guid.Parse("3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91"), "Door too narrow", "The entrance door is 800 wide; 900 is required.", "Issue", "In progress", "High", 2,
        "2026-10-30T10:00:00Z", "ionut", "2026-10-30T11:00:00Z", "andrei", "andrei", "2026-11-15T00:00:00Z", "DTAC", ["accessibility", "doors"],
        [
            new BcfComment(Guid.Parse("7c7d2a10-1111-4222-8333-444455556666"), "2026-10-30T10:30:00Z", "andrei", "Will widen it.", Guid.Parse("9a9b8c7d-aaaa-4bbb-8ccc-dddd00001111")),
            new BcfComment(Guid.Parse("7c7d2a10-1111-4222-8333-444455557777"), "2026-10-30T10:40:00Z", "ionut", "Thanks.", null, "2026-10-30T10:45:00Z", "ionut"),
        ],
        [
            new BcfViewpoint(Guid.Parse("9a9b8c7d-aaaa-4bbb-8ccc-dddd00001111"),
                new BcfCamera(true, new(10.5, -8.25, 4), new(-0.6, 0.64, -0.48), new(-0.33, 0.35, 0.88), 45, 0, 1.5),
                [wallId], true, ["2O2Fr$t4X7Zf8NOew3FLKI"],
                [new BcfClippingPlane(new(0, 0, 2.5), new(0, 0, 1))],
                [new BcfLine(new(0, 0, 0), new(1, 1, 1))], Png, 0),
            new BcfViewpoint(Guid.Parse("9a9b8c7d-aaaa-4bbb-8ccc-dddd00002222"),
                new BcfCamera(false, new(6, 4, 50), new(0, 0, -1), new(0, 1, 0), 0, 12.5),
                [], false, [wallId], [], [], null, 1),
        ]);

    static void AssertSame(BcfTopic a, BcfTopic b, bool v3)
    {
        Assert.Equal(a.Guid, b.Guid);
        Assert.Equal(a.Title, b.Title);
        Assert.Equal(a.Description, b.Description);
        Assert.Equal(a.Type, b.Type);
        Assert.Equal(a.Status, b.Status);
        Assert.Equal(a.Priority, b.Priority);
        Assert.Equal(a.Index, b.Index);
        Assert.Equal(a.CreationDate, b.CreationDate);
        Assert.Equal(a.CreationAuthor, b.CreationAuthor);
        Assert.Equal(a.ModifiedDate, b.ModifiedDate);
        Assert.Equal(a.ModifiedAuthor, b.ModifiedAuthor);
        Assert.Equal(a.AssignedTo, b.AssignedTo);
        Assert.Equal(a.DueDate, b.DueDate);
        Assert.Equal(a.Stage, b.Stage);
        Assert.Equal(a.Labels, b.Labels);
        Assert.Equal(a.Comments, b.Comments);
        Assert.Equal(a.Viewpoints.Count, b.Viewpoints.Count);
        for (var i = 0; i < a.Viewpoints.Count; i++)
        {
            var (x, y) = (a.Viewpoints[i], b.Viewpoints[i]);
            Assert.Equal(x.Guid, y.Guid);
            Assert.Equal(x.Index, y.Index);
            Assert.Equal(x.Selection, y.Selection);
            Assert.Equal(x.DefaultVisibility, y.DefaultVisibility);
            Assert.Equal(x.Exceptions, y.Exceptions);
            Assert.Equal(x.ClippingPlanes, y.ClippingPlanes);
            Assert.Equal(x.Lines, y.Lines);
            Assert.Equal(x.Snapshot, y.Snapshot);
            Assert.Equal(x.Camera!.Perspective, y.Camera!.Perspective);
            Assert.Equal(x.Camera.ViewPoint, y.Camera.ViewPoint);
            Assert.Equal(x.Camera.Direction, y.Camera.Direction);
            Assert.Equal(x.Camera.Up, y.Camera.Up);
            if (x.Camera.Perspective) Assert.Equal(x.Camera.FieldOfView, y.Camera.FieldOfView);
            else Assert.Equal(x.Camera.ViewToWorldScale, y.Camera.ViewToWorldScale);
            // 3.0 carries the aspect ratio; 2.1 has no place for it.
            if (v3 && x.Camera.AspectRatio is { } r) Assert.Equal(r, y.Camera.AspectRatio);
        }
    }

    static Dictionary<string, byte[]> Entries(byte[] zip)
    {
        using var z = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return z.Entries.ToDictionary(e => e.FullName, e => { using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray(); });
    }

    static byte[] Zip(params (string Name, string Text)[] files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, text) in files)
            {
                using var s = zip.CreateEntry(name).Open();
                s.Write(Encoding.UTF8.GetBytes(text));
            }
        return ms.ToArray();
    }

    // ------------------------------------------------------------ the file

    [Fact]
    public void A_BCF_21_file_written_reads_back_with_every_field()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        var topic = FullTopic(wall);
        var bytes = BcfWriter.Write(new BcfFile(Bcf.Version21, [topic]));
        var entries = Entries(bytes);
        // The layout 2.1 fixes: bcf.version, a folder per topic GUID with markup.bcf, viewpoint.bcfv and snapshot.png (the others named after their GUID).
        Assert.Contains("bcf.version", entries.Keys);
        Assert.Contains("<DetailedVersion>2.1</DetailedVersion>", Encoding.UTF8.GetString(entries["bcf.version"]));
        Assert.Contains("3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91/markup.bcf", entries.Keys);
        Assert.Contains("3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91/viewpoint.bcfv", entries.Keys);
        Assert.Contains("3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91/snapshot.png", entries.Keys);
        Assert.Contains("3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91/9a9b8c7d-aaaa-4bbb-8ccc-dddd00002222.bcfv", entries.Keys);
        var markup = Encoding.UTF8.GetString(entries["3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91/markup.bcf"]);
        // 2.1: the comments and viewpoints beside the topic, the labels as bare elements, in the schema's order.
        Assert.Contains("</Topic>", markup);
        Assert.True(markup.IndexOf("</Topic>", StringComparison.Ordinal) < markup.IndexOf("<Comment Guid", StringComparison.Ordinal));
        Assert.Contains("<Labels>accessibility</Labels>", markup);
        Assert.True(markup.IndexOf("<Priority>", StringComparison.Ordinal) < markup.IndexOf("<Labels>", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("<CreationDate>", StringComparison.Ordinal) < markup.IndexOf("<AssignedTo>", StringComparison.Ordinal));
        var viewpoint = Encoding.UTF8.GetString(entries["3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91/viewpoint.bcfv"]);
        Assert.Contains($"<Component IfcGuid=\"{wall}\">", viewpoint);
        Assert.Contains("<Visibility DefaultVisibility=\"true\">", viewpoint);
        Assert.Contains("<FieldOfView>45</FieldOfView>", viewpoint);
        Assert.DoesNotContain("AspectRatio", viewpoint);
        var read = BcfReader.Read(bytes);
        Assert.Equal(Bcf.Version21, read.Version);
        AssertSame(topic, Assert.Single(read.Topics), v3: false);
    }

    [Fact]
    public void A_BCF_30_file_written_reads_back_with_every_field()
    {
        var f = F();
        var topic = FullTopic(f.Wall(0, 0, 5, 0));
        var bytes = BcfWriter.Write(new BcfFile(Bcf.Version30, [topic]));
        var entries = Entries(bytes);
        Assert.Contains("VersionId=\"3.0\"", Encoding.UTF8.GetString(entries["bcf.version"]));
        Assert.DoesNotContain("DetailedVersion", Encoding.UTF8.GetString(entries["bcf.version"]));
        var markup = Encoding.UTF8.GetString(entries["3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91/markup.bcf"]);
        // 3.0: comments and viewpoints inside the topic, labels wrapped, the ViewPoint element.
        Assert.True(markup.IndexOf("<Comments>", StringComparison.Ordinal) < markup.IndexOf("</Topic>", StringComparison.Ordinal));
        Assert.Contains("<Labels>", markup);
        Assert.Contains("<Label>accessibility</Label>", markup);
        Assert.Contains("<ViewPoint Guid=", markup);
        var viewpoint = Encoding.UTF8.GetString(entries["3f0c1a3e-8e4a-4b57-9e1a-5b1f0c2d7e91/viewpoint.bcfv"]);
        Assert.Contains("<AspectRatio>1.5</AspectRatio>", viewpoint);
        Assert.True(viewpoint.IndexOf("<Visibility", StringComparison.Ordinal) < viewpoint.IndexOf("<ViewSetupHints", StringComparison.Ordinal));
        var read = BcfReader.Read(bytes);
        Assert.Equal(Bcf.Version30, read.Version);
        AssertSame(topic, Assert.Single(read.Topics), v3: true);
    }

    [Fact]
    public void A_minimal_hand_written_21_file_reads()
    {
        var bytes = Zip(
            ("bcf.version", """<?xml version="1.0" encoding="utf-8"?><Version VersionId="2.1"><DetailedVersion>2.1</DetailedVersion></Version>"""),
            ("a1b2c3d4-0000-4000-8000-000000000001/markup.bcf", """
                <?xml version="1.0" encoding="utf-8"?>
                <Markup>
                  <Header><File IfcProject="0YvctVUKr0kugbFTf53O9L" isExternal="true"><Filename>house.ifc</Filename></File></Header>
                  <Topic Guid="a1b2c3d4-0000-4000-8000-000000000001" TopicType="Clash" TopicStatus="Open">
                    <Title>Beam through duct</Title>
                    <Priority>High</Priority>
                    <Labels>MEP</Labels>
                    <Labels>Structure</Labels>
                    <CreationDate>2026-10-29T08:00:00+02:00</CreationDate>
                    <CreationAuthor>solibri@office.ro</CreationAuthor>
                    <AssignedTo>ionut</AssignedTo>
                    <Description>Move the duct down.</Description>
                  </Topic>
                  <Comment Guid="a1b2c3d4-0000-4000-8000-000000000002">
                    <Date>2026-10-29T09:00:00+02:00</Date>
                    <Author>ionut</Author>
                    <Comment>Seen.</Comment>
                    <Viewpoint Guid="a1b2c3d4-0000-4000-8000-000000000003" />
                  </Comment>
                  <Viewpoints Guid="a1b2c3d4-0000-4000-8000-000000000003">
                    <Viewpoint>viewpoint.bcfv</Viewpoint>
                    <Snapshot>snapshot.png</Snapshot>
                  </Viewpoints>
                </Markup>
                """),
            ("a1b2c3d4-0000-4000-8000-000000000001/viewpoint.bcfv", """
                <?xml version="1.0" encoding="utf-8"?>
                <VisualizationInfo Guid="a1b2c3d4-0000-4000-8000-000000000003">
                  <Components>
                    <Selection><Component IfcGuid="2O2Fr$t4X7Zf8NOew3FLKI" /></Selection>
                    <Visibility DefaultVisibility="false"><Exceptions><Component IfcGuid="2O2Fr$t4X7Zf8NOew3FLKI" /><Component IfcGuid="1kTvXnbbzCWw8lcMd1dR4o" /></Exceptions></Visibility>
                  </Components>
                  <PerspectiveCamera>
                    <CameraViewPoint><X>12.3</X><Y>-7.5</Y><Z>3.2</Z></CameraViewPoint>
                    <CameraDirection><X>-0.7</X><Y>0.6</Y><Z>-0.4</Z></CameraDirection>
                    <CameraUpVector><X>0</X><Y>0</Y><Z>1</Z></CameraUpVector>
                    <FieldOfView>60</FieldOfView>
                  </PerspectiveCamera>
                  <ClippingPlanes><ClippingPlane><Location><X>0</X><Y>0</Y><Z>3</Z></Location><Direction><X>0</X><Y>0</Y><Z>1</Z></Direction></ClippingPlane></ClippingPlanes>
                </VisualizationInfo>
                """));
        var file = BcfReader.Read(bytes);
        Assert.Equal(Bcf.Version21, file.Version);
        var t = Assert.Single(file.Topics);
        Assert.Equal(Guid.Parse("a1b2c3d4-0000-4000-8000-000000000001"), t.Guid);
        Assert.Equal(("Beam through duct", "Clash", "Open", "High", "ionut", "Move the duct down."), (t.Title, t.Type, t.Status, t.Priority, t.AssignedTo, t.Description));
        Assert.Equal(["MEP", "Structure"], t.Labels);
        Assert.Equal("2026-10-29T08:00:00+02:00", t.CreationDate);
        var c = Assert.Single(t.Comments);
        Assert.Equal(("ionut", "Seen."), (c.Author, c.Text));
        Assert.Equal(Guid.Parse("a1b2c3d4-0000-4000-8000-000000000003"), c.Viewpoint);
        var vp = Assert.Single(t.Viewpoints);
        Assert.Equal(c.Viewpoint, vp.Guid);
        Assert.Equal(["2O2Fr$t4X7Zf8NOew3FLKI"], vp.Selection);
        Assert.False(vp.DefaultVisibility);
        Assert.Equal(2, vp.Exceptions.Count);
        Assert.True(vp.Camera!.Perspective);
        Assert.Equal(new BcfPoint(12.3, -7.5, 3.2), vp.Camera.ViewPoint);
        Assert.Equal(60, vp.Camera.FieldOfView);
        Assert.Equal(new BcfClippingPlane(new(0, 0, 3), new(0, 0, 1)), Assert.Single(vp.ClippingPlanes));
        Assert.Null(vp.Snapshot);   // referenced but not in the file: no error
    }

    [Fact]
    public void A_minimal_hand_written_30_file_reads()
    {
        var bytes = Zip(
            ("bcf.version", """<?xml version="1.0" encoding="utf-8"?><Version VersionId="3.0" />"""),
            ("documents.xml", """<?xml version="1.0" encoding="utf-8"?><DocumentInfo />"""),
            ("b1b2c3d4-0000-4000-8000-000000000001/markup.bcf", """
                <?xml version="1.0" encoding="utf-8"?>
                <Markup>
                  <Topic Guid="b1b2c3d4-0000-4000-8000-000000000001" ServerAssignedId="42" TopicType="Request" TopicStatus="Closed">
                    <Title>Add a handrail</Title>
                    <Labels><Label>Stairs</Label></Labels>
                    <CreationDate>2026-10-29T08:00:00Z</CreationDate>
                    <CreationAuthor>bimcollab</CreationAuthor>
                    <DueDate>2026-11-01T00:00:00Z</DueDate>
                    <Comments>
                      <Comment Guid="b1b2c3d4-0000-4000-8000-000000000002"><Date>2026-10-29T09:00:00Z</Date><Author>ionut</Author><Comment>Done.</Comment></Comment>
                    </Comments>
                    <Viewpoints>
                      <ViewPoint Guid="b1b2c3d4-0000-4000-8000-000000000003"><Viewpoint>b1b2c3d4-0000-4000-8000-000000000003.bcfv</Viewpoint><Index>0</Index></ViewPoint>
                    </Viewpoints>
                  </Topic>
                </Markup>
                """),
            ("b1b2c3d4-0000-4000-8000-000000000001/b1b2c3d4-0000-4000-8000-000000000003.bcfv", """
                <?xml version="1.0" encoding="utf-8"?>
                <VisualizationInfo Guid="b1b2c3d4-0000-4000-8000-000000000003">
                  <Components><Visibility DefaultVisibility="true"><ViewSetupHints SpacesVisible="false" /></Visibility></Components>
                  <OrthogonalCamera>
                    <CameraViewPoint><X>5</X><Y>4</Y><Z>40</Z></CameraViewPoint>
                    <CameraDirection><X>0</X><Y>0</Y><Z>-1</Z></CameraDirection>
                    <CameraUpVector><X>0</X><Y>1</Y><Z>0</Z></CameraUpVector>
                    <ViewToWorldScale>18</ViewToWorldScale>
                    <AspectRatio>1.78</AspectRatio>
                  </OrthogonalCamera>
                  <Lines><Line><StartPoint><X>0</X><Y>0</Y><Z>0</Z></StartPoint><EndPoint><X>2</X><Y>0</Y><Z>0</Z></EndPoint></Line></Lines>
                </VisualizationInfo>
                """));
        var file = BcfReader.Read(bytes);
        Assert.Equal(Bcf.Version30, file.Version);
        var t = Assert.Single(file.Topics);
        Assert.Equal(("Add a handrail", "Request", "Closed", "2026-11-01T00:00:00Z"), (t.Title, t.Type, t.Status, t.DueDate));
        Assert.Equal(["Stairs"], t.Labels);
        Assert.Equal("Done.", Assert.Single(t.Comments).Text);
        var vp = Assert.Single(t.Viewpoints);
        Assert.Equal(0, vp.Index);
        Assert.True(vp.DefaultVisibility);
        Assert.False(vp.Camera!.Perspective);
        Assert.Equal(18, vp.Camera.ViewToWorldScale);
        Assert.Equal(1.78, vp.Camera.AspectRatio);
        Assert.Equal(new BcfLine(new(0, 0, 0), new(2, 0, 0)), Assert.Single(vp.Lines));
        Assert.False(Remarks.IsOpen(t.Status));
    }

    [Fact]
    public void A_file_that_is_not_BCF_is_refused_and_an_empty_zip_is_an_empty_file()
    {
        Assert.Throws<CommandException>(() => BcfReader.Read(Encoding.UTF8.GetBytes("ISO-10303-21;")));
        Assert.Empty(BcfReader.Read(Zip(("readme.txt", "nothing here"))).Topics);
    }

    // ------------------------------------------------------------ the project

    [Fact]
    public void A_remark_keeps_its_GUID_through_the_project_and_a_round_trip()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        var created = f.Run("remarks.create", new JsonObject
        {
            ["title"] = "Check the wall", ["author"] = "ionut", ["status"] = "Open", ["priority"] = "High", ["labels"] = new JsonArray("walls"),
            ["viewpoint"] = new JsonObject { ["camera"] = new BcfCamera(true, new(8, -8, 4), new(-0.6, 0.6, -0.5), new(0, 0, 1), 45, 0).ToJson(), ["selection"] = new JsonArray(wall), ["snapshot"] = Convert.ToBase64String(Png) },
        })!;
        var id = (string)created["id"]!;
        var guid = Guid.Parse((string)created["guid"]!);
        // The entity id is the GUID in IFC's 22-character form: the same topic is the same remark, in this project and in any that reads the file.
        Assert.Equal(GlobalId.FromGuid(guid), id);
        Assert.Equal(guid, GlobalId.ToGuid(id));
        var bytes = BcfWriter.Write(Remarks.Export(f.Model, []));
        var read = BcfReader.Read(bytes);
        var topic = Assert.Single(read.Topics);
        Assert.Equal(guid, topic.Guid);
        Assert.Equal("Check the wall", topic.Title);
        Assert.Equal([wall], Assert.Single(topic.Viewpoints).Selection);
        Assert.Equal(Png, topic.Viewpoints[0].Snapshot);

        // Read into another project: the same id, so the element it points at (by GlobalId) is found when that project has it.
        var g = F();
        var r = Import(g, read);
        Assert.Equal((1, 1, 0), (r.Topics, r.Added, r.Updated));
        Assert.NotNull(g.Model.Get(id));
        Assert.Equal(guid, Remarks.GuidOf(g.Model.Get(id)!));

        // Back into the first project after a comment made elsewhere: the remark is updated, not doubled, and the comment made here stays.
        f.Run("remarks.comment", new JsonObject { ["id"] = id, ["text"] = "Mine, not yet sent.", ["author"] = "ionut" });
        var theirs = topic with { Status = "Resolved", Comments = [new BcfComment(Guid.NewGuid(), "2026-10-30T12:00:00Z", "andrei", "Fixed in Revit.", null)] };
        var r2 = Import(f, new BcfFile(Bcf.Version21, [theirs]));
        Assert.Equal((0, 1), (r2.Added, r2.Updated));
        var mine = Remarks.Read(f.Model.Get(id)!);
        Assert.Single(f.Model.OfClass(Remarks.Class));
        Assert.Equal("Resolved", mine.Status);
        Assert.Equal(["Fixed in Revit.", "Mine, not yet sent."], mine.Comments.Select(c => c.Text));
        Assert.Equal(Png, mine.Viewpoints[0].Snapshot);   // the file came without it; ours stays
    }

    [Fact]
    public void Components_are_the_elements_GlobalIds_and_an_imported_elements_id_is_the_files()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        Assert.True(GlobalId.IsValid(wall));
        f.Run("remarks.create", new JsonObject { ["title"] = "On the wall", ["author"] = "ionut", ["viewpoint"] = new JsonObject { ["selection"] = new JsonArray(wall), ["defaultVisibility"] = false, ["exceptions"] = new JsonArray(wall) } });
        var vp = Assert.Single(Remarks.All(f.Model)).Viewpoints[0];
        Assert.Equal([wall], vp.Selection);
        Assert.Equal([wall], vp.Exceptions);
        Assert.False(vp.DefaultVisibility);
        // A topic whose GUID is an element's (never a remark's) is left out, not written over the element.
        var r = Import(f, new BcfFile(Bcf.Version21, [FullTopic(wall) with { Guid = GlobalId.ToGuid(wall) }]));
        Assert.Equal((0, 0), (r.Added, r.Updated));
        Assert.Equal(IfcClass.Wall, f.Model.Get(wall)!.Class);
    }

    [Fact]
    public void The_commands_edit_comment_add_viewpoints_and_delete_each_as_one_undo_step()
    {
        var f = F();
        var id = (string)f.Run("remarks.create", new JsonObject { ["title"] = "Fix", ["author"] = "ionut" })!["id"]!;
        Assert.Throws<CommandException>(() => f.Run("remarks.create", new JsonObject { ["author"] = "ionut" }));
        var updated = f.Run("remarks.update", new JsonObject { ["id"] = id, ["status"] = "Closed", ["assignedTo"] = "andrei", ["dueDate"] = "2026-11-01T00:00:00Z", ["labels"] = new JsonArray("a", "b"), ["author"] = "ionut", ["description"] = "" })!;
        Assert.Equal("Closed", (string?)updated["status"]);
        Assert.Equal("ionut", (string?)updated["modifiedAuthor"]);
        Assert.Null(updated["description"]);
        var t = Remarks.Read(f.Model.Get(id)!);
        Assert.Equal(("andrei", "2026-11-01T00:00:00Z"), (t.AssignedTo, t.DueDate));
        Assert.Equal(["a", "b"], t.Labels);
        Assert.False(Remarks.IsOpen(t.Status));
        // A comment with a viewpoint of its own: the viewpoint joins the remark and the comment points at it.
        var c = f.Run("remarks.comment", new JsonObject { ["id"] = id, ["text"] = "Look here", ["author"] = "andrei", ["viewpoint"] = new JsonObject { ["camera"] = new BcfCamera(false, new(0, 0, 10), new(0, 0, -1), new(0, 1, 0), 0, 8).ToJson() } })!;
        t = Remarks.Read(f.Model.Get(id)!);
        Assert.Equal(Assert.Single(t.Viewpoints).Guid, Guid.Parse((string)c["viewpoint"]!));
        Assert.Throws<CommandException>(() => f.Run("remarks.comment", new JsonObject { ["id"] = id, ["text"] = " ", ["author"] = "andrei" }));
        // A viewpoint put in the first one's place: the comment follows it.
        var v = f.Run("remarks.addViewpoint", new JsonObject { ["id"] = id, ["replace"] = true, ["viewpoint"] = new JsonObject { ["camera"] = new BcfCamera(true, new(1, 1, 1), new(0, 1, 0), new(0, 0, 1), 45, 0).ToJson(), ["snapshot"] = Convert.ToBase64String(Png) } })!;
        t = Remarks.Read(f.Model.Get(id)!);
        Assert.Equal(Guid.Parse((string)v["guid"]!), Assert.Single(t.Viewpoints).Guid);
        Assert.Equal(t.Viewpoints[0].Guid, t.Comments[0].Viewpoint);
        f.Run("remarks.addViewpoint", new JsonObject { ["id"] = id, ["viewpoint"] = new JsonObject { ["camera"] = new BcfCamera(true, new(2, 2, 2), new(0, 1, 0), new(0, 0, 1), 45, 0).ToJson() } });
        Assert.Equal(2, Remarks.Read(f.Model.Get(id)!).Viewpoints.Count);
        // The list query carries no snapshot; the snapshot query does; the node sent to the interface has none either.
        var list = (JsonObject)f.Session.Query("remarks.list", [])!;
        var one = Assert.Single((JsonArray)list["remarks"]!)!.AsObject();
        Assert.Equal(id, (string?)one["id"]);
        Assert.False((bool)one["open"]!);
        Assert.Null(one["viewpoints"]![0]!["snapshot"]);
        Assert.True((bool)one["viewpoints"]![0]!["hasSnapshot"]!);
        Assert.Equal(Convert.ToBase64String(Png), (string?)f.Session.Query("remarks.snapshot", new JsonObject { ["id"] = id })!["data"]);
        var diff = f.RunDiff("remarks.update", new JsonObject { ["id"] = id, ["title"] = "Fixed", ["author"] = "ionut" });
        var node = Assert.Single(diff.Nodes, n => n.Id == id);
        Assert.Equal("Fixed", node.Name);
        Assert.Null(node.Props["viewpoints"]![0]!["snapshot"]);
        Assert.NotNull(f.Model.Get(id)!.Attrs["viewpoints"]![0]!["snapshot"]);
        f.Session.Undo();
        Assert.Equal("Fix", f.Model.Get(id)!.Name);
        f.Run("remarks.delete", new JsonObject { ["id"] = id });
        Assert.Null(f.Model.Get(id));
        f.Session.Undo();
        Assert.NotNull(f.Model.Get(id));
    }

    [Fact]
    public void The_export_takes_all_the_open_ones_or_those_asked()
    {
        var f = F();
        var a = (string)f.Run("remarks.create", new JsonObject { ["title"] = "A", ["author"] = "x" })!["id"]!;
        var b = (string)f.Run("remarks.create", new JsonObject { ["title"] = "B", ["author"] = "x", ["status"] = "Closed" })!["id"]!;
        var c = (string)f.Run("remarks.create", new JsonObject { ["title"] = "C", ["author"] = "x", ["status"] = "Resolved" })!["id"]!;
        Assert.Equal(["A", "B", "C"], Remarks.Export(f.Model, []).Topics.Select(t => t.Title));
        Assert.Equal(["A", "C"], Remarks.Export(f.Model, new JsonObject { ["open"] = true }).Topics.Select(t => t.Title));
        Assert.Equal(["B"], Remarks.Export(f.Model, new JsonObject { ["ids"] = new JsonArray(b) }).Topics.Select(t => t.Title));
        Assert.Equal(Bcf.Version30, Remarks.Export(f.Model, new JsonObject { ["version"] = "3.0" }).Version);
        // Each topic gets an index in the order written, so a tool lists them as Lapin does.
        Assert.Equal([0, 1, 2], Remarks.Export(f.Model, []).Topics.Select(t => t.Index));
        _ = (a, c);
    }

    [Fact]
    public void BCF_goes_through_the_protocol_as_bytes_both_ways()
    {
        var f = F();
        var wall = f.Wall(0, 0, 5, 0);
        var d = new EngineDispatcher(f.Session, PackageFiles.Create());
        d.Handle(new JsonObject { ["id"] = 1, ["method"] = "doc.execute", ["params"] = new JsonObject { ["command"] = "remarks.create", ["args"] = new JsonObject { ["title"] = "Via the protocol", ["author"] = "ionut", ["viewpoint"] = new JsonObject { ["selection"] = new JsonArray(wall) } } } }.ToJsonString());
        var exported = Result(d.Handle("""{"id":2,"method":"project.exportBcf","params":{"open":true}}""").Reply);
        Assert.Equal("remarks.bcfzip", (string?)exported["name"]);
        Assert.Equal(1, (int)exported["topics"]!);
        var data = (string)exported["data"]!;
        var g = F();
        var e = new EngineDispatcher(g.Session, PackageFiles.Create());
        var (reply, diff) = e.Handle(new JsonObject { ["id"] = 3, ["method"] = "project.importBcf", ["params"] = new JsonObject { ["name"] = "review.bcfzip", ["data"] = data } }.ToJsonString());
        var r = Result(reply);
        Assert.Equal((1, 1), ((int)r["topics"]!, (int)r["added"]!));
        Assert.NotNull(diff);
        var remark = Assert.Single(g.Model.OfClass(Remarks.Class));
        Assert.Equal("Via the protocol", remark.Name);
        Assert.Equal([wall], Remarks.Read(remark).Viewpoints[0].Selection);
        Assert.Equal("Import review.bcfzip", diff!.State.UndoLabel);
        // A guest may import by bytes and export, never by path.
        Assert.Null(EngineDispatcher.AllowedRemote("project.importBcf", new JsonObject { ["data"] = data }, "editor"));
        Assert.NotNull(EngineDispatcher.AllowedRemote("project.importBcf", new JsonObject { ["path"] = "/x.bcfzip" }, "editor"));
        Assert.Null(EngineDispatcher.AllowedRemote("project.exportBcf", [], "viewer"));
    }

    static JsonObject Result(byte[] frame) => (JsonObject)JsonNode.Parse(BinaryFrame.HeaderText(frame))!["result"]!;

    /// <summary>The file into the fixture's project, as the dispatcher does it: one undo step.</summary>
    static Remarks.ImportResult Import(Fixture f, BcfFile file)
    {
        Remarks.ImportResult r = null!;
        f.Session.Edit("Import", e => r = Remarks.Import(e, file));
        return r;
    }
}
