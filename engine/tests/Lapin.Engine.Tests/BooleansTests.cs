using Lapin.Booleans;
using Lapin.Geometry;

namespace Lapin.Engine.Tests;

/// <summary>
/// The engine cuts with manifold wherever it is built: macOS's and Windows' libraries are committed, Linux's is made by the
/// engine's build when it is missing (engine/native/README.md). Without the library the engine still runs but cuts nothing,
/// so openings stay in their walls and every test that measures a cut body fails far from the cause (a wall of 4.2 m³ where
/// its opening leaves 3.576 m³); this test names the cause.
/// </summary>
public class BooleansTests
{
    [Fact]
    public void A_session_cuts_with_manifold()
    {
        Assert.True(ManifoldBooleans.Available, "manifold's native library is not next to the engine: see engine/native/README.md.");
        Assert.IsType<ManifoldBooleans>(new Fixture().Session.Catalog.Get<ISolidBooleans>());
    }
}
