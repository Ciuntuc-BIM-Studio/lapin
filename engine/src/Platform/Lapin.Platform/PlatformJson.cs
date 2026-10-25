using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lapin.Platform;

/// <summary>The platform's JSON contracts, generated at build time with the engine's options (web names, numbers read from strings, nulls left out).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, NumberHandling = JsonNumberHandling.AllowReadingFromString, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProductManifest))]
[JsonSerializable(typeof(ModuleLoader.ModuleManifest))]
public partial class PlatformJson : JsonSerializerContext;
