using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>
/// Everything that crosses the protocol, as JSON contracts generated at build
/// time with the engine's options: the replies and diffs, every result a call
/// can give, so a trimmed engine (the browser's) writes the same frames as the desktop.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, NumberHandling = JsonNumberHandling.AllowReadingFromString, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Reply))]
[JsonSerializable(typeof(DiffEvent))]
[JsonSerializable(typeof(Diff))]
[JsonSerializable(typeof(SessionInfo))]
[JsonSerializable(typeof(IReadOnlyList<QuantitySet>))]
[JsonSerializable(typeof(List<QuantitySet>))]
[JsonSerializable(typeof(QuantitySet[]))]
[JsonSerializable(typeof(IReadOnlyList<RecoverableWork>))]
[JsonSerializable(typeof(List<RecoverableWork>))]
[JsonSerializable(typeof(RecoverableWork[]))]
[JsonSerializable(typeof(JsonNode))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(JsonArray))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
public partial class ProtocolJson : JsonSerializerContext;
