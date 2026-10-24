using Lapin.Host;
using Xunit;

namespace Lapin.Engine.Tests;

public class ExitGuardTests
{
    [Fact]
    public void SeesTheImageGoneOnlyWhenItsFileIsGone()
    {
        var image = Path.Combine(Path.GetTempPath(), $"lapin-guard-{Guid.NewGuid():N}");
        File.WriteAllText(image, "");
        try
        {
            ExitGuard.Arm(image);
            Assert.False(ExitGuard.ImageGone());
            File.Delete(image);
            Assert.True(ExitGuard.ImageGone());
        }
        finally
        {
            File.Delete(image);
            ExitGuard.Arm();
        }
    }
}
