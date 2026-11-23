using Microsoft.Extensions.DependencyInjection;
using Speckle.Sdk;
using Lapin.Platform;

namespace Lapin.Speckle;

/// <summary>
/// Speckle's SDK, set up once per engine: its services (the client factory, the send), and its type loader, which its objects
/// need before a dynamic property is set on one (the conversion's, too, with no network).
/// </summary>
public static class SpeckleSdk
{
    static readonly Lazy<IServiceProvider> Lazy = new(() =>
    {
        var services = new ServiceCollection();
        services.AddSpeckleSdk(new Application("Lapin", SpeckleConvert.SourceApplication), Session.AppVersion, typeof(global::Speckle.Objects.Geometry.Mesh).Assembly);
        return services.BuildServiceProvider();
    }, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IServiceProvider Services => Lazy.Value;

    /// <summary>The SDK ready (its types loaded) before any of its objects is made.</summary>
    public static void Ensure() => _ = Lazy.Value;
}
