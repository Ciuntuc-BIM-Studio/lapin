using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>A title block's frame: shown or not, and its margins from the paper's edges (mm).</summary>
public sealed record TitleBlockFrame(bool Show = true, double Left = Sheets.Margin, double Top = Sheets.Margin, double Right = Sheets.Margin, double Bottom = Sheets.Margin)
{
    public JsonObject ToJson() => new() { ["show"] = Show, ["left"] = Left, ["top"] = Top, ["right"] = Right, ["bottom"] = Bottom };

    public static TitleBlockFrame From(JsonNode? n)
    {
        if (n is not JsonObject o) return new();
        static double M(JsonNode? x) => Math.Clamp(Json.Number(x) ?? Sheets.Margin, 0, 60);
        return new(o["show"] is not JsonValue v || !v.TryGetValue<bool>(out var show) || show, M(o["left"]), M(o["top"]), M(o["right"]), M(o["bottom"]));
    }
}

/// <summary>
/// A title block template (docs/plan-title-blocks.md, decision 340): a box <see cref="W"/> × <see cref="H"/> paper mm laid in the
/// frame's corner <see cref="Anchor"/> names, the frame's margins, where the revision table goes when no item draws it
/// (<c>above</c> the block, or <c>none</c>), and its items in mm from the box's bottom-left corner, y up: lines, rectangles,
/// polylines, fills and hatches, texts (live text: <c>{project.name}</c>), images and the revision table.
/// </summary>
public sealed record TitleBlockTemplate(double W, double H, string Anchor, TitleBlockFrame Frame, string Revisions, JsonArray Items)
{
    public static readonly string[] Anchors = ["bottomRight", "bottomLeft", "topRight", "topLeft"];
    public static readonly string[] ItemTypes = ["line", "rect", "polyline", "text", "image", "revisions"];
    public const int MaxItems = 2000;

    public JsonObject ToJson() => new()
    {
        ["w"] = W, ["h"] = H, ["anchor"] = Anchor, ["frame"] = Frame.ToJson(), ["revisions"] = Revisions, ["items"] = Items.DeepClone(),
    };

    /// <summary>A template as kept; null when it is not one.</summary>
    public static TitleBlockTemplate? From(JsonNode? n)
    {
        if (n is not JsonObject o) return null;
        var w = Json.Number(o["w"]) ?? 180;
        var h = Json.Number(o["h"]) ?? 50;
        var anchor = (string?)o["anchor"] is { } a && Anchors.Contains(a) ? a : "bottomRight";
        var revisions = (string?)o["revisions"] is "none" ? "none" : "above";
        return new(w, h, anchor, TitleBlockFrame.From(o["frame"]), revisions, o["items"] as JsonArray is { } items ? (JsonArray)items.DeepClone() : []);
    }

    /// <summary>
    /// A template as a command gives it, checked: a box 10–1000 mm, a known corner, each item of a known type with its numbers;
    /// what is not a template's is refused with why.
    /// </summary>
    public static TitleBlockTemplate Parse(JsonNode? n)
    {
        if (n is not JsonObject o) throw new CommandException("A title block template is an object: its size, corner and items.");
        double Size(string k, double d)
        {
            var v = o.ContainsKey(k) ? Json.Number(o[k]) ?? throw new CommandException($"The template's {k} is a number (mm).") : d;
            return v is >= 10 and <= 1000 ? v : throw new CommandException("A title block is 10 to 1000 mm across.");
        }
        var w = Size("w", 180);
        var h = Size("h", 50);
        if (o["anchor"] is { } an && !Anchors.Contains((string?)an)) throw new CommandException($"A title block sits in a corner: {string.Join(", ", Anchors)}.");
        var items = o["items"] as JsonArray ?? (o.ContainsKey("items") ? throw new CommandException("The template's items are a list.") : []);
        if (items.Count > MaxItems) throw new CommandException($"A title block has at most {MaxItems} items.");
        var checkedItems = new JsonArray();
        for (var i = 0; i < items.Count; i++) checkedItems.Add((JsonNode)CheckItem(items[i], i + 1));
        var t = From(o)!;
        return t with { W = w, H = h, Items = checkedItems };
    }

    static JsonObject CheckItem(JsonNode? n, int index)
    {
        if (n is not JsonObject item) throw new CommandException($"Item {index} is not an object.");
        var type = (string?)item["type"];
        if (type == null || !ItemTypes.Contains(type)) throw new CommandException($"Item {index} is not a kind of item ({string.Join(", ", ItemTypes)}).");
        void Num(params string[] keys)
        {
            foreach (var k in keys)
                if (Json.Number(item[k]) is not { } v || !double.IsFinite(v)) throw new CommandException($"Item {index} ({type}) needs its {k} (mm).");
        }
        void Pt(string k)
        {
            if (item[k] is not JsonArray { Count: 2 } p || Json.Number(p[0]) is null || Json.Number(p[1]) is null) throw new CommandException($"Item {index} ({type}) needs its {k} as [x, y] (mm).");
        }
        switch (type)
        {
            case "line": Pt("a"); Pt("b"); break;
            case "rect": case "image": case "revisions": Num("x", "y", "w", "h"); break;
            case "polyline":
                if (item["pts"] is not JsonArray { Count: >= 2 } pts || pts.Any(p => p is not JsonArray { Count: 2 } q || Json.Number(q[0]) is null || Json.Number(q[1]) is null))
                    throw new CommandException($"Item {index} (polyline) needs two points at least, each [x, y].");
                break;
            case "text":
                Pt("at");
                if (item["text"] is not JsonValue) throw new CommandException($"Item {index} (text) needs its text.");
                if (item["h"] is { } th && Json.Number(th) is not (> 0 and <= 100)) throw new CommandException($"Item {index} (text): a height is above 0 and at most 100 mm.");
                break;
        }
        return (JsonObject)item.DeepClone();
    }

    /// <summary>Paper mm of a number, for templates written in code.</summary>
    internal static JsonNode N(double v) => JsonValue.Create(Math.Round(v, 3))!;

    internal static JsonNode Line(double x0, double y0, double x1, double y1, double w = 0.25) =>
        new JsonObject { ["type"] = "line", ["a"] = new JsonArray(N(x0), N(y0)), ["b"] = new JsonArray(N(x1), N(y1)), ["w"] = w };

    internal static JsonNode Rect(double x, double y, double w, double h, double stroke = 0.25, string? fill = null)
    {
        var o = new JsonObject { ["type"] = "rect", ["x"] = N(x), ["y"] = N(y), ["w"] = N(w), ["h"] = N(h), ["stroke"] = stroke };
        if (fill != null) o["fill"] = fill;
        return o;
    }

    internal static JsonNode Text(double x, double y, string text, double h, bool bold = false, string align = "start") =>
        new JsonObject { ["type"] = "text", ["at"] = new JsonArray(N(x), N(y)), ["text"] = text, ["h"] = h, ["bold"] = bold, ["align"] = align };

    internal static string F(double v) => v.ToString(CultureInfo.InvariantCulture);
}
