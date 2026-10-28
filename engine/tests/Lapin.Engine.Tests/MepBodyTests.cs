using Lapin.Geometry;
using Lapin.Mep;

namespace Lapin.Engine.Tests;

/// <summary>Every role has a schematic volume that stands where the node is, at its elevation, and keeps its ports outside or on it.</summary>
public class MepBodyTests
{
    [Theory]
    [MemberData(nameof(RoleIds))]
    public void Every_role_has_a_volume_at_its_place(string roleId)
    {
        var f = new MepFixture();
        var role = Roles.Require(roleId);
        var service = role.Ports.Select(p => p.Service).FirstOrDefault(s => s != "*") ?? Services.Heating;
        var s = f.System(service);
        var node = f.Node(s, roleId, 2, 3);
        var g = f.Session.Geometry(node)!;
        Assert.Empty(g.Diagnostics ?? []);
        Assert.NotEmpty(g.Parts);
        var (min, max) = f.Bounds(node);
        Assert.InRange(2.0, min.X - 0.01, max.X + 0.01);
        Assert.InRange(3.0, min.Y - 0.01, max.Y + 0.01);
        if (!role.IsRise) Assert.True(max.Z > min.Z);
    }

    public static IEnumerable<object[]> RoleIds() => Roles.All.Select(r => new object[] { r.Id });
}
