using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Lapin.Platform;

namespace Lapin.Protocol;

/// <summary>The IDS calls' way in (docs/ids.md): <c>Lapin.Ids.IdsCalls</c>, made the first time one is called, kept as an object so this class mentions nothing of Lapin.Ids.</summary>
public sealed partial class EngineDispatcher
{
    object? _ids;

    [MethodImpl(MethodImplOptions.NoInlining)]
    (object? Result, Diff? Diff) IdsCall(string method, JsonObject p, Func<ArraySegment<byte>, string, JsonObject> deliver)
    {
        var ids = _ids as Lapin.Ids.IdsCalls ?? new Lapin.Ids.IdsCalls(session, ReportProgress);
        _ids = ids;
        switch (method)
        {
            case "ids.add":
            {
                if ((string?)p["text"] is { } text) return ids.Add(p, Encoding.UTF8.GetBytes(text), (string?)p["name"] ?? "requirements.ids");
                var (bytes, name, _, _) = IfcBytes(p);
                return ids.Add(p, bytes, (string?)p["name"] ?? name);
            }
            case "ids.read": return ids.Read(p);
            case "ids.run":
            {
                // An IFC file given (its bytes, or a path on the desktop): that file checked instead of the model.
                (byte[], string)? given = null;
                if (p["data"] is JsonValue || (string?)p["path"] is { Length: > 0 }) { var (b, n, _, _) = IfcBytes(p); given = (b, n); }
                return ids.Run(p, given);
            }
            case "ids.results": return ids.Results(p);
            case "ids.makeRemarks": return ids.MakeRemarks(p);
            case "ids.report": return ids.Report(p, deliver);
            default: throw new CommandException($"Unknown method „{method}”.");
        }
    }
}
