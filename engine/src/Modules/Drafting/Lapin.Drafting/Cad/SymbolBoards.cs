using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Drafting.Cad;

/// <summary>
/// A symbol edited on the shared sketch canvas (audit S11 stage 2, docs/plan-sketch-canvas.md §4): a board in the symbol's own
/// millimetres round its insertion point (the origin, its +x the symbol's direction; a cross and an arrow there), drawn on with
/// every Draft command, snap and grip (<see cref="ItemBoards"/>).
///
/// <para><c>drafting.editSymbol {symbolId}</c> opens a project symbol's board (opened again, the same board is answered);
/// <c>{name, def}</c> a board for a symbol not yet in the project (<c>target</c> <c>new</c>, made at Finish) or one of this
/// computer's library (<c>library</c>: Finish only gives the definition back, the interface keeps it). Lapin's own symbols stay as
/// they are. <c>drafting.setSymbolBoard</c> changes the fields kept beside the drawing (name, scale, category, description,
/// attributes, layers, the layer new items take), <c>drafting.symbolBoard</c> answers the definition as Finish would write it (the
/// preview's), <c>drafting.finishSymbol</c> writes it (one undo step: every instance draws it; a board finished unchanged leaves
/// the symbol as it was, byte for byte) and <c>drafting.cancelSymbol</c> drops the board.</para>
/// </summary>
public static class SymbolBoards
{
    /// <summary>The attribute marking a symbol's board: {symbolId?, target (project | new | library), name, def (as it was opened), layer?}.</summary>
    public const string Key = "symbolOf";

    const string What = "a symbol";

    static JsonArray Guides(SymbolDef def)
    {
        // The insertion point's cross and the symbol's direction, sized to the symbol (5 mm at least).
        var reach = 5.0;
        foreach (var item in def.Items.OfType<JsonObject>())
            foreach (var k in new[] { "a", "b", "c", "at" })
                if (item[k] is JsonArray p && p.Count >= 2) reach = Math.Max(reach, Math.Max(Math.Abs(Json.Number(p[0]) ?? 0), Math.Abs(Json.Number(p[1]) ?? 0)) * 0.15);
        var s = Math.Min(reach, 500) * 0.001;
        const string red = "#d63031";
        return
        [
            ItemBoards.Guide([new Vec2(-s, 0), new Vec2(s, 0)], false, red),
            ItemBoards.Guide([new Vec2(0, -s), new Vec2(0, s)], false, red),
            ItemBoards.Guide([new Vec2(s, 0), new Vec2(s * 1.6, 0)], false, red),
            ItemBoards.Guide([new Vec2(s * 1.35, s * 0.18), new Vec2(s * 1.6, 0), new Vec2(s * 1.35, -s * 0.18)], false, red),
        ];
    }

    /// <summary>{symbolId} | {name, def, target: new | library} → {viewId, ids}: the board, one undo step.</summary>
    public static JsonNode? Edit(ICommandContext ctx, JsonObject a)
    {
        var m = ctx.Model;
        string target, name;
        SymbolDef def;
        string? symbolId = null;
        if ((string?)a["symbolId"] is { Length: > 0 } sid)
        {
            if (BuiltinSymbols.Is(sid)) throw new CommandException("Lapin's own symbols stay as they are: duplicate one to change it.");
            if (m.Get(sid) is not { Class: Symbols.Class } e || SymbolDef.From(e.Attrs["def"]) is not { } d) throw new CommandException("There is no such symbol.");
            if (ItemBoards.Find(m, Key, o => (string?)o["symbolId"] == sid) is { } open) return new JsonObject { ["viewId"] = open.Id, ["ids"] = new JsonArray() };
            (target, name, def, symbolId) = ("project", e.Name, d, sid);
        }
        else
        {
            target = (string?)a["target"] is "library" ? "library" : "new";
            name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : "Symbol";
            def = a["def"] is JsonObject dj ? SymbolDef.From(dj) ?? throw new CommandException("That is not a symbol's definition.") : new SymbolDef("annotation", "other", [], []);
        }
        var of = new JsonObject { ["target"] = target, ["name"] = name, ["def"] = def.ToJson() };
        if (symbolId != null) of["symbolId"] = symbolId;
        var view = ItemBoards.NewView(ctx, $"Symbol: {name}", Key, of, Guides(def));
        ItemBoards.Lay(ctx, view, def.Items);
        return new JsonObject { ["viewId"] = view.Id };
    }

    /// <summary>The definition the board holds now: its fields, its items (as <see cref="ItemBoards.Collect"/> reads them), the attributes new texts name.</summary>
    public static (SymbolDef Def, string Name, JsonObject Of) Read(IModelView m, Entity view, JsonObject of)
    {
        var was = SymbolDef.From(of["def"]) ?? new SymbolDef("annotation", "other", [], []);
        var c = ItemBoards.Collect(m, view, was.Items, curves: true, "A symbol");
        if (c.Images.Count > 0) throw new CommandException($"A symbol has no pictures: „{c.Images[0].Name}” is one. Delete it, then Finish.");
        var items = c.Items;
        var attributes = was.Attributes.ToList();
        if (c.Fresh.Count > 0)
        {
            var (fresh, found) = ItemBoards.Fresh(m, view, c.Fresh, curves: true);
            var layer = (string?)of["layer"];
            foreach (var it in fresh.OfType<JsonObject>())
            {
                if (layer is { Length: > 0 }) it["layer"] = layer;
                items.Add((JsonNode)it.DeepClone());
            }
            attributes.AddRange(found.Where(f => !attributes.Any(x => x.Key.Equals(f.Key, StringComparison.OrdinalIgnoreCase))));
        }
        return (was with { Items = items, Attributes = attributes }, (string?)of["name"] ?? "Symbol", of);
    }

    /// <summary>{viewId} → {symbolId?, name, def}: the symbol written (a new one made; a library's only given back), the board gone.</summary>
    public static JsonNode? Finish(ICommandContext ctx, JsonObject a)
    {
        var (view, of) = ItemBoards.Board(ctx.Model, a, Key, What);
        var (def, name, _) = Read(ctx.Model, view, of);
        if (def.Items.Count == 0) throw new CommandException("A symbol draws something: draw one line at least, then Finish (or Cancel).");
        var json = def.ToJson();
        // Checked as every symbol is (its items, its attributes): refused, the board stays open.
        var parsed = SymbolDef.Parse(json).ToJson();
        string? id = null;
        switch ((string?)of["target"])
        {
            case "project":
            {
                var e = ctx.Model.Get((string?)of["symbolId"] ?? "") is { Class: Symbols.Class } s ? s : throw new CommandException("The symbol this board is of is gone.");
                // Unchanged, the symbol stays as it was, byte for byte.
                var unchanged = JsonNode.DeepEquals(e.Attrs["def"], json) && e.Name == name;
                if (!unchanged)
                {
                    var attrs = e.CloneAttrs();
                    if (!JsonNode.DeepEquals(e.Attrs["def"], json)) attrs["def"] = parsed.DeepClone();
                    ctx.Replace(e.With(name: e.Name == name ? null : Symbols.FreeName(ctx.Model, name, e.Id), attrs: attrs));
                }
                id = e.Id;
                break;
            }
            case "new":
                id = Symbols.Create(ctx, name, SymbolDef.From(parsed.DeepClone())!).Id;
                break;
        }
        ItemBoards.Drop(ctx, view);
        var o = new JsonObject { ["name"] = id != null ? ctx.Model.Get(id)?.Name ?? name : name, ["def"] = parsed };
        if (id != null) o["symbolId"] = id;
        return o;
    }

    /// <summary>{viewId, name?, scale?, category?, description?, attributes?, layers?, layer?}: the fields beside the board, one undo step.</summary>
    public static JsonNode? Set(ICommandContext ctx, JsonObject a)
    {
        var (view, of) = ItemBoards.Board(ctx.Model, a, Key, What);
        var next = (JsonObject)of.DeepClone();
        var def = (JsonObject)(next["def"] as JsonObject ?? new JsonObject()).DeepClone();
        if (a.ContainsKey("name")) next["name"] = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : throw new CommandException("A symbol needs a name.");
        if (a.ContainsKey("scale")) def["scale"] = (string?)a["scale"] is "model" or "annotation" ? (string?)a["scale"] : throw new CommandException("A symbol's scale is annotation (paper mm) or model (the building's mm).");
        if (a.ContainsKey("category")) def["category"] = ((string?)a["category"])?.Trim() is { Length: > 0 } c ? c[..Math.Min(c.Length, 40)] : "other";
        if (a.ContainsKey("description")) { if (((string?)a["description"])?.Trim() is { Length: > 0 } d) def["description"] = d; else def.Remove("description"); }
        if (a["attributes"] is JsonArray attrs)
        {
            var list = new List<SymbolAttribute>();
            foreach (var x in attrs)
            {
                var at = SymbolAttribute.From(x) ?? throw new CommandException("An attribute is {key, label?, default?}: its key letters, digits and _ only.");
                if (list.Any(y => y.Key.Equals(at.Key, StringComparison.OrdinalIgnoreCase))) throw new CommandException($"The attribute „{at.Key}” is there twice.");
                list.Add(at);
            }
            def["attributes"] = new JsonArray([.. list.Select(x => (JsonNode)x.ToJson())]);
        }
        if (a.ContainsKey("layers")) { if (a["layers"] is JsonArray { Count: > 0 } ls) def["layers"] = ls.DeepClone(); else def.Remove("layers"); }
        if (a.ContainsKey("layer")) { if ((string?)a["layer"] is { Length: > 0 } l) next["layer"] = l; else next.Remove("layer"); }
        // The fields as a symbol keeps them (the items stay the board's).
        var kept = SymbolDef.From(def) ?? throw new CommandException("That is not a symbol's definition.");
        next["def"] = kept.ToJson();
        ItemBoards.SetMark(ctx, view, Key, next);
        if (a.ContainsKey("name"))
        {
            var now = ctx.Model.Get(view.Id)!;
            ctx.Replace(now.With(name: $"Symbol: {(string?)next["name"]}"));
        }
        return null;
    }

    public static void Register(IModuleRegistry r)
    {
        r.Command("drafting.editSymbol", "Edit symbol", Edit);
        r.Command("drafting.finishSymbol", "Finish symbol", Finish);
        r.Command("drafting.cancelSymbol", "Cancel symbol", (ctx, a) => { ItemBoards.Drop(ctx, ItemBoards.Board(ctx.Model, a, Key, What).View); return null; });
        r.Command("drafting.setSymbolBoard", "Symbol fields", Set);
        // {viewId} → {name, def, target, symbolId?, layer?}: the board's symbol as Finish would write it (an error when it would refuse).
        r.Query("drafting.symbolBoard", (q, a) =>
        {
            var (view, of) = ItemBoards.Board(q.Model, a, Key, What);
            var (def, name, _) = Read(q.Model, view, of);
            var o = new JsonObject { ["name"] = name, ["def"] = def.ToJson(), ["target"] = (string?)of["target"], ["layer"] = (string?)of["layer"] };
            if ((string?)of["symbolId"] is { } sid) o["symbolId"] = sid;
            return o;
        });
    }
}
