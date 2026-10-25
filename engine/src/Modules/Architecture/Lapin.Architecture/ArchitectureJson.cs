using System.Text.Json;
using System.Text.Json.Serialization;
using Lapin.Architecture.Roofs;
using Lapin.Architecture.Stairs;

namespace Lapin.Architecture;

/// <summary>
/// Architecture's JSON contracts, generated at build time with the engine's
/// options (web names, numbers read from strings, nulls left out): the specs
/// the types keep and the products the project lists, written and read
/// without reflection, so a trimmed engine (the browser's) does as the desktop.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, NumberHandling = JsonNumberHandling.AllowReadingFromString, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JoinerySpec))]
[JsonSerializable(typeof(LintelRule))]
[JsonSerializable(typeof(LintelProduct))]
[JsonSerializable(typeof(StairSpec))]
public partial class ArchitectureJson : JsonSerializerContext;

/// <summary>The catalogues the interface reads (roof styles and coverings, stair rule sets), with plain web options (nulls written).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(IReadOnlyList<RoofCatalog.Style>))]
[JsonSerializable(typeof(IReadOnlyList<RoofCatalog.Covering>))]
[JsonSerializable(typeof(StairRules))]
public partial class ArchitectureCatalogJson : JsonSerializerContext;
