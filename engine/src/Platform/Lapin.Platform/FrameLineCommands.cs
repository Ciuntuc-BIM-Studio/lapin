using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The pen weight, colour and line type of detail callouts' boundaries and inset views' frames (issue #73): the project's
/// default in the drafting standards (<c>standards.frameLine</c>), and each element's own (<c>lineWeight</c>, <c>lineColor</c>,
/// <c>lineType</c>, set by <c>view.updateCallout</c> and <c>view.updateInset</c>). Nothing set draws as Lapin always did.
/// </summary>
public static class FrameLineCommands
{
    public static void Register(IModuleRegistry r)
    {
        // The project's default for a kind ({kind: callout | inset, weight?, color?, lineType?}): each one given is set, null clears it (Lapin's own again).
        r.Command("standards.frameLine", "Change drafting standards", (ctx, a) =>
        {
            var key = (string?)a["kind"] switch
            {
                "callout" => FrameLines.CalloutKey, "inset" => FrameLines.InsetKey,
                _ => throw new CommandException("A frame line is a callout's (callout) or an inset view's (inset)."),
            };
            var book = DraftingResources.Book(ctx.Model);
            var raw = Standards.ToJson(book);
            var line = raw[key] as JsonObject ?? [];
            if (a.ContainsKey("weight")) Weight(line, "weight", a["weight"]);
            if (a.ContainsKey("color")) Color(line, "color", a["color"]);
            if (a.ContainsKey("lineType")) Type(line, "lineType", a["lineType"], book);
            if (line.Count == 0) raw.Remove(key); else raw[key] = line;
            DraftingResources.Write(ctx, raw);
            return null;
        });
    }

    /// <summary>An element's own frame line set from a command's arguments (<c>lineWeight</c> paper mm, <c>lineColor</c>, <c>lineType</c>) into its attributes (<paramref name="attrs"/>): each given is checked and set, null clears it.</summary>
    public static void SetOwn(ICommandContext ctx, JsonObject a, JsonNode attrs)
    {
        var into = (JsonObject)attrs;
        if (a.ContainsKey("lineWeight")) Weight(into, FrameLines.WeightAttr, a["lineWeight"]);
        if (a.ContainsKey("lineColor")) Color(into, FrameLines.ColorAttr, a["lineColor"]);
        if (a.ContainsKey("lineType")) Type(into, FrameLines.TypeAttr, a["lineType"], DraftingResources.Book(ctx.Model));
    }

    static bool Blank(JsonNode? v) => v is null || v is JsonValue j && j.TryGetValue<string>(out var s) && s.Trim().Length == 0;

    static void Weight(JsonObject into, string key, JsonNode? v)
    {
        if (v is null) into.Remove(key);
        else into[key] = FrameLines.WeightOf(v) ?? throw new CommandException($"A pen weight is {FrameLines.MinWeight} to {FrameLines.MaxWeight} mm.");
    }

    static void Color(JsonObject into, string key, JsonNode? v)
    {
        if (Blank(v)) into.Remove(key);
        else into[key] = Lapin.Geometry.HexColor.Read(v, null) ?? throw new CommandException("A colour is #rrggbb.");
    }

    static void Type(JsonObject into, string key, JsonNode? v, StyleBook book)
    {
        if (Blank(v)) into.Remove(key);
        else into[key] = FrameLines.TypeOf(v, book) ?? throw new CommandException($"There is no line type {v!.ToJsonString()}: one of {string.Join(", ", Standards.LineTypeNames(book))}.");
    }
}
