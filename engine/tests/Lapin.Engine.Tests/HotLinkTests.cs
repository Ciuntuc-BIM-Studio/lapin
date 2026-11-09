using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Host;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>Hotlinks (docs/links.md): another Lapin project linked read-only, native, placed where asked, as many times as wanted, read again when it changes.</summary>
public class HotLinkTests
{
    /// <summary>A small project on disk: two joined walls with a door, a floor.</summary>
    static (string Path, byte[] Bytes, Fixture Source) Source()
    {
        var f = new Fixture(new ArchitectureModule());
        var a = f.Wall(0, 0, 4, 0);
        f.Wall(4, 0, 4, 3);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = a, ["along"] = 2 });
        f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = new JsonArray(new JsonArray(0, 0), new JsonArray(4, 0), new JsonArray(4, 3), new JsonArray(0, 3)) });
        var path = Path.Combine(Path.GetTempPath(), $"hotlink-{Guid.NewGuid():N}.lapin");
        PackageFiles.Save(f.Session, path);
        return (path, File.ReadAllBytes(path), f);
    }

    static double[] Start(Entity wall) => [.. wall.Attrs["start"]!.AsArray().Select(v => Json.Number(v) ?? 0)];
    static double[] End(Entity wall) => [.. wall.Attrs["end"]!.AsArray().Select(v => Json.Number(v) ?? 0)];

    [Fact]
    public void It_comes_in_native_read_only_and_placed()
    {
        var (path, bytes, _) = Source();
        var host = new Fixture(new ArchitectureModule());
        var r = HotLinks.Link(host.Session, bytes, "module.lapin", path, new HotLinks.Placing(10, 5, 0), out _);
        var link = host.Model.Get(r.LinkId!)!;
        Assert.Equal(HotLinks.Kind, link.Str("kind"));
        var walls = host.Model.OfClass(IfcClass.Wall).ToList();
        Assert.Equal(2, walls.Count);
        Assert.All(walls, w => Assert.Equal(link.Id, w.Str(Links.Key)));
        Assert.Single(host.Model.OfClass(IfcClass.Door));
        Assert.Single(host.Model.OfClass(IfcClass.Slab));
        Assert.Contains(walls, w => Math.Abs(Start(w)[0] - 10) < 1e-6 && Math.Abs(Start(w)[1] - 5) < 1e-6);
        // Native, not a reference; and it cannot be edited.
        Assert.All(walls, w => Assert.False(StoredBodies.Has(w)));
        Assert.Throws<CommandException>(() => host.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(walls[0].Id) }));
        File.Delete(path);
    }

    [Fact]
    public void One_project_is_placed_many_times_each_with_its_own_ids()
    {
        var (path, bytes, _) = Source();
        var host = new Fixture(new ArchitectureModule());
        HotLinks.Link(host.Session, bytes, "module.lapin", path, new HotLinks.Placing(0, 0, 0), out _);
        HotLinks.Link(host.Session, bytes, "module.lapin", path, new HotLinks.Placing(20, 0, 90), out _);
        var walls = host.Model.OfClass(IfcClass.Wall).ToList();
        Assert.Equal(4, walls.Count);
        Assert.Equal(2, host.Model.OfClass(Links.Class).Count());
        Assert.Equal(4, walls.Select(w => w.Id).Distinct().Count());
        File.Delete(path);
    }

    [Fact]
    public void A_copy_of_this_very_project_never_touches_its_elements()
    {
        var (path, bytes, source) = Source();
        // The host holds the source's own elements, under the same ids.
        var host = new Fixture(new ArchitectureModule());
        PackageFiles.Open(host.Session, path);
        var mine = host.Model.OfClass(IfcClass.Wall).Select(w => w.Id).ToHashSet();
        HotLinks.Link(host.Session, bytes, "copy.lapin", path, new HotLinks.Placing(30, 0, 0), out _);
        Assert.Equal(4, host.Model.OfClass(IfcClass.Wall).Count());
        Assert.All(mine, id => Assert.Null(host.Model.Get(id)!.Str(Links.Key)));
        Assert.All(mine, id => Assert.True(Start(host.Model.Get(id)!)[0] < 10));
        Assert.Equal(mine, source.Model.OfClass(IfcClass.Wall).Select(w => w.Id).ToHashSet());
        File.Delete(path);
    }

    [Fact]
    public void Read_again_it_takes_the_source_as_it_is_now_under_the_same_ids()
    {
        var (path, bytes, source) = Source();
        var host = new Fixture(new ArchitectureModule());
        var r = HotLinks.Link(host.Session, bytes, "module.lapin", path, new HotLinks.Placing(10, 0, 0), out _);
        var link = host.Model.Get(r.LinkId!)!;
        var before = host.Model.OfClass(IfcClass.Wall).Select(w => w.Id).ToHashSet();
        Assert.False(HotLinks.Changed(link));

        // The source changes: a wall more, its file newer.
        source.Wall(0, 0, 0, 3);
        PackageFiles.Save(source.Session, path);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        Assert.True(HotLinks.Changed(link));

        HotLinks.Link(host.Session, File.ReadAllBytes(path), "module.lapin", path, HotLinks.Placing.Of(link.Attrs["options"] as JsonObject), out _, link.Id);
        var after = host.Model.OfClass(IfcClass.Wall).ToList();
        Assert.Equal(3, after.Count);
        Assert.Subset(after.Select(w => w.Id).ToHashSet(), before);
        Assert.Contains(after, w => Math.Abs(Start(w)[0] - 10) < 1e-6);
        Assert.False(HotLinks.Changed(host.Model.Get(link.Id)!));
        File.Delete(path);
    }

    [Fact]
    public void Placed_anew_it_keeps_its_ids_and_moves()
    {
        var (path, bytes, _) = Source();
        var host = new Fixture(new ArchitectureModule());
        var r = HotLinks.Link(host.Session, bytes, "module.lapin", path, new HotLinks.Placing(10, 0, 0), out _);
        var before = host.Model.OfClass(IfcClass.Wall).Select(w => w.Id).ToHashSet();
        HotLinks.Link(host.Session, bytes, "module.lapin", path, new HotLinks.Placing(30, 0, 0), out _, r.LinkId);
        var walls = host.Model.OfClass(IfcClass.Wall).ToList();
        Assert.Equal(before, walls.Select(w => w.Id).ToHashSet());
        Assert.All(walls, w => Assert.True(Start(w)[0] >= 30 - 1e-6));
        Assert.Equal(30, HotLinks.Placing.Of(host.Model.Get(r.LinkId!)!.Attrs["options"] as JsonObject).X, 6);
        File.Delete(path);
    }

    [Fact]
    public void What_the_source_links_comes_along_and_moves_unless_hidden_there()
    {
        // The inner project, linked into the source; the source placed in the host.
        var (innerPath, innerBytes, _) = Source();
        var middle = new Fixture(new ArchitectureModule());
        middle.Wall(0, 10, 4, 10);
        var inner = HotLinks.Link(middle.Session, innerBytes, "inner.lapin", innerPath, new HotLinks.Placing(0, 20, 0), out _);
        var path = Path.Combine(Path.GetTempPath(), $"hotlink-{Guid.NewGuid():N}.lapin");
        PackageFiles.Save(middle.Session, path);
        var host = new Fixture(new ArchitectureModule());
        HotLinks.Link(host.Session, File.ReadAllBytes(path), "middle.lapin", path, new HotLinks.Placing(100, 0, 0), out _);
        var walls = host.Model.OfClass(IfcClass.Wall).ToList();
        Assert.Equal(3, walls.Count);
        Assert.All(walls, w => Assert.True(Start(w)[0] >= 100 - 1e-6));
        Assert.Contains(walls, w => Math.Abs(Start(w)[1] - 20) < 1e-6);

        // Hidden in the source, it stays out.
        middle.Run("link.update", new JsonObject { ["id"] = inner.LinkId, ["visible"] = false });
        PackageFiles.Save(middle.Session, path);
        var other = new Fixture(new ArchitectureModule());
        HotLinks.Link(other.Session, File.ReadAllBytes(path), "middle.lapin", path, new HotLinks.Placing(100, 0, 0), out _);
        Assert.Single(other.Model.OfClass(IfcClass.Wall));
        File.Delete(path); File.Delete(innerPath);
    }

    [Fact]
    public void A_reference_in_the_source_moves_with_the_rest()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Wall(0, 0, 4, 0);
        var storey = f.Storey;
        f.Session.Edit("A reference", edit =>
        {
            var e = new Entity(edit.NewId(), IfcClass.BuildingElementProxy, "Imported chair", new JsonObject
            {
                [StoredBodies.Key] = StoredBodies.Write([("Body", "#884422", new List<Lapin.Geometry.Vec3> { new(1, 1, 0), new(2, 1, 0), new(1, 2, 0), new(1, 1, 1) }, new List<(int, int, int)> { (0, 2, 1), (0, 1, 3), (1, 2, 3), (2, 0, 3) })]),
            });
            edit.Add(e);
            Spatial.Contain(edit, storey, e.Id);
        });
        var path = Path.Combine(Path.GetTempPath(), $"hotlink-{Guid.NewGuid():N}.lapin");
        PackageFiles.Save(f.Session, path);
        var host = new Fixture(new ArchitectureModule());
        HotLinks.Link(host.Session, File.ReadAllBytes(path), "m.lapin", path, new HotLinks.Placing(50, 0, 0), out _);
        var chair = host.Model.Entities.Single(e => e.Name == "Imported chair");
        // As it came in, on its own or an instance of a shared body: every corner where it was asked to go.
        var shared = StoredBodies.InstanceOf(chair);
        var holder = shared is { } s ? host.Model.Get(s.Body)! : chair;
        var (v, _) = StoredBodies.ReadPart(holder.Attrs[StoredBodies.Key]!["parts"]!.AsArray()[0]!.AsObject());
        var xs = Enumerable.Range(0, v.Length / 3).Select(i => new Lapin.Geometry.Vec3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]))
            .Select(c => shared is { } t ? StoredBodies.Apply(t.Xf, c).X : c.X).ToList();
        Assert.Equal(51, xs.Min(), 3);
        Assert.Equal(52, xs.Max(), 3);
        File.Delete(path);
    }

    [Fact]
    public void Mirrored_the_walls_swap_sides_and_stay_when_read_again()
    {
        var (path, bytes, _) = Source();
        var host = new Fixture(new ArchitectureModule());
        var place = new HotLinks.Placing(100, 0, 0, Mirrored: true);
        var r = HotLinks.Link(host.Session, bytes, "module.lapin", path, place, out _);
        var walls = host.Model.OfClass(IfcClass.Wall).ToList();
        var xs = walls.SelectMany(w => new[] { Start(w)[0], End(w)[0] }).ToList();
        Assert.True(xs.Max() <= 100 + 1e-6, $"to x = {xs.Max()}");
        Assert.True(xs.Min() < 100 - 1, $"from x = {xs.Min()}");
        var ids = walls.Select(w => w.Id).ToHashSet();
        Assert.True(HotLinks.Placing.Of(host.Model.Get(r.LinkId!)!.Attrs["options"] as JsonObject).Mirrored);
        HotLinks.Link(host.Session, bytes, "module.lapin", path, HotLinks.Placing.Of(host.Model.Get(r.LinkId!)!.Attrs["options"] as JsonObject), out _, r.LinkId);
        Assert.Equal(ids, host.Model.OfClass(IfcClass.Wall).Select(w => w.Id).ToHashSet());
        Assert.True(host.Model.OfClass(IfcClass.Wall).SelectMany(w => new[] { Start(w)[0], End(w)[0] }).Max() <= 100 + 1e-6);
        File.Delete(path);
    }

    [Fact]
    public void A_mirrored_door_opens_the_other_way()
    {
        var f = new Fixture(new ArchitectureModule());
        var wall = f.Wall(0, 0, 4, 0);
        f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = 1 });
        var path = Path.Combine(Path.GetTempPath(), $"hotlink-{Guid.NewGuid():N}.lapin");
        PackageFiles.Save(f.Session, path);
        var bytes = File.ReadAllBytes(path);
        var plain = new Fixture(new ArchitectureModule());
        HotLinks.Link(plain.Session, bytes, "m.lapin", path, new HotLinks.Placing(0, 0, 0), out _);
        var mirrored = new Fixture(new ArchitectureModule());
        HotLinks.Link(mirrored.Session, bytes, "m.lapin", path, new HotLinks.Placing(0, 0, 0, Mirrored: true), out _);
        var was = plain.Model.OfClass(IfcClass.Door).Single();
        var now = mirrored.Model.OfClass(IfcClass.Door).Single();
        Assert.NotEqual(was.Bool("flipHand") ^ was.Bool("flipSide"), now.Bool("flipHand") ^ now.Bool("flipSide"));
        File.Delete(path);
    }

    /// <summary>Six times the signed volume of a closed body: positive when its faces look outwards.</summary>
    static double SignedVolume(double[] v, int[] t)
    {
        var sum = 0.0;
        for (var i = 0; i + 2 < t.Length; i += 3)
        {
            var (a, b, c) = (t[i] * 3, t[i + 1] * 3, t[i + 2] * 3);
            sum += v[a] * (v[b + 1] * v[c + 2] - v[b + 2] * v[c + 1]) - v[a + 1] * (v[b] * v[c + 2] - v[b + 2] * v[c]) + v[a + 2] * (v[b] * v[c + 1] - v[b + 1] * v[c]);
        }
        return sum;
    }

    [Fact]
    public void A_mirrored_reference_keeps_its_faces_outwards_shared_or_not()
    {
        var f = new Fixture(new ArchitectureModule());
        var storey = f.Storey;
        var corners = new List<Lapin.Geometry.Vec3> { new(1, 1, 0), new(2, 1, 0), new(1, 2, 0), new(1, 1, 1) };
        var faces = new List<(int, int, int)> { (0, 2, 1), (0, 1, 3), (1, 2, 3), (2, 0, 3) };
        f.Session.Edit("References", edit =>
        {
            var own = new Entity(edit.NewId(), IfcClass.BuildingElementProxy, "Own", new JsonObject { [StoredBodies.Key] = StoredBodies.Write([("Body", "#884422", corners, faces)]) });
            edit.Add(own);
            Spatial.Contain(edit, storey, own.Id);
            var body = new Entity(edit.NewId(), StoredBodies.SharedClass, "Body", new JsonObject { [StoredBodies.Key] = StoredBodies.Write([("Body", "#884422", corners, faces)]) });
            edit.Add(body);
            var one = new Entity(edit.NewId(), IfcClass.BuildingElementProxy, "Instance", new JsonObject
            {
                [StoredBodies.BodyOf] = body.Id,
                [StoredBodies.XfKey] = StoredBodies.WriteXf([1, 0, 0, 0, 1, 0, 0, 0, 1, 10, 0, 0]),
            });
            edit.Add(one);
            Spatial.Contain(edit, storey, one.Id);
        });
        var path = Path.Combine(Path.GetTempPath(), $"hotlink-{Guid.NewGuid():N}.lapin");
        PackageFiles.Save(f.Session, path);
        var host = new Fixture(new ArchitectureModule());
        HotLinks.Link(host.Session, File.ReadAllBytes(path), "m.lapin", path, new HotLinks.Placing(0, 0, 0, Mirrored: true), out _);
        foreach (var (name, lo, hi) in new[] { ("Own", -2.0, -1.0), ("Instance", -12.0, -11.0) })
        {
            var e = host.Model.Entities.Single(x => x.Name == name);
            var shared = StoredBodies.InstanceOf(e);
            var holder = shared is { } s ? host.Model.Get(s.Body)! : e;
            var (v, t) = StoredBodies.ReadPart(holder.Attrs[StoredBodies.Key]!["parts"]!.AsArray()[0]!.AsObject());
            var points = Enumerable.Range(0, v.Length / 3).Select(i => new Lapin.Geometry.Vec3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2]))
                .Select(c => shared is { } q ? StoredBodies.Apply(q.Xf, c) : c).ToList();
            Assert.Equal(lo, points.Min(p => p.X), 3);
            Assert.Equal(hi, points.Max(p => p.X), 3);
            var flat = points.SelectMany(p => new[] { p.X, p.Y, p.Z }).ToArray();
            Assert.True(SignedVolume(flat, t) > 0, $"{name} is inside out");
        }
        File.Delete(path);
    }

    [Fact]
    public void What_is_not_a_project_says_so()
    {
        var host = new Fixture(new ArchitectureModule());
        Assert.ThrowsAny<Exception>(() => HotLinks.Link(host.Session, [1, 2, 3], "x.lapin", null, new HotLinks.Placing(0, 0, 0), out _));
        Assert.Empty(host.Model.OfClass(Links.Class));
    }
}
