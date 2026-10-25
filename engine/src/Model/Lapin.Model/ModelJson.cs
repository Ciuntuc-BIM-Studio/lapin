using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lapin.Model;

/// <summary>
/// The model's JSON contracts, generated at build time with the options of
/// <see cref="Json.Options"/> (web names, numbers read from strings, nulls left
/// out): what the engine writes of its own types without reflection, so a
/// trimmed engine (the browser's) writes the same as the desktop's.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, NumberHandling = JsonNumberHandling.AllowReadingFromString, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<MaterialLayer>))]
[JsonSerializable(typeof(Entity))]
[JsonSerializable(typeof(Relation))]
[JsonSerializable(typeof(List<Relation>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class ModelJson : JsonSerializerContext;
