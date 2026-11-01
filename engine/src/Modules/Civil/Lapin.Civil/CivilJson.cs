using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lapin.Civil;

/// <summary>Civil's JSON contracts, generated at build time (plain web options, as the species list was always written; a name it has not left out).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Species))]
public partial class CivilJson : JsonSerializerContext;
