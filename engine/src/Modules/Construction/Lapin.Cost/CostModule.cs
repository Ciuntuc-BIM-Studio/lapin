using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// Cost (5D, docs/plan-4d5d.md, stage 6): the user's price lists (imported from CSV or XLSX, the columns matched), the
/// articles on types and the article rules by material, the estimate (IfcCostSchedule) worked out from the model on every
/// ask — chapters, lines with the takeoff's quantities times the unit prices split into material, labour, equipment and
/// transport, the contributions on labour, overheads, profit and VAT, RON or EUR at the project's rate — its Romanian
/// forms (F3, F2, C6–C9) as schedules, workbooks and PDF, and IFC cost schedules both ways. Every command is one undo step;
/// the lines are derived, only the user's own are stored.
/// </summary>
public sealed class CostModule : ILapinModule
{
    public string Id => "cost";
    public string Name => "Cost";

    static Entity Require(IModelView m, string? id, string cls, string what) =>
        m.Get(id ?? "") is { } e && e.Class == cls ? e : throw new CommandException($"There is no such {what}.");

    static Entity EstimateArg(IModelView m, JsonObject a) =>
        (string?)a["id"] is { } id ? Require(m, id, Costing.Class, "estimate") : Costing.EstimateOf(m) ?? throw new CommandException("The project has no estimate yet: Cost → Estimate makes one.");

    static double Rate(JsonNode? n, string what, double max = 100)
    {
        if (Json.Number(n) is not { } v || !double.IsFinite(v) || v < 0 || v > max) throw new CommandException($"The {what} is a percentage between 0 and {max}.");
        return v;
    }

    static byte[] Bytes(JsonObject a)
    {
        try { return Convert.FromBase64String((string?)a["data"] ?? throw new CommandException("Which file?")); }
        catch (FormatException) { throw new CommandException("The file could not be read."); }
    }

    static Dictionary<string, int?> MapArg(JsonNode? n) =>
        PriceLists.Fields.ToDictionary(f => f, f => n is JsonObject o && Json.Number(o[f]) is { } v && v >= 0 ? (int?)(int)v : null);

    static string Currency(JsonNode? n) => ((string?)n)?.Trim().ToUpperInvariant() switch
    {
        null or "" or "RON" or "LEI" => "RON",
        "EUR" => "EUR",
        _ => throw new CommandException("Prices are in RON or EUR."),
    };

    public static JsonObject PriceListJson(PriceLists.List l) => new()
    {
        ["id"] = l.Id, ["name"] = l.Name, ["currency"] = l.Currency, ["count"] = l.Rows.Count(r => !r.IsResource), ["resources"] = l.Rows.Count(r => r.IsResource),
        ["indicative"] = l.Indicative, ["source"] = l.Entity.Str("source"),
    };

    public static JsonObject RowJson(PriceRow r) => new()
    {
        ["code"] = r.Code, ["name"] = r.Name, ["unit"] = r.Unit, ["m"] = r.Material, ["l"] = r.Labour, ["e"] = r.Equipment, ["t"] = r.Transport,
        ["hours"] = r.Hours, ["trade"] = r.Trade, ["article"] = r.Article, ["kind"] = r.Kind, ["consumption"] = r.Consumption,
    };

    static PriceRow? RowArg(JsonNode? n)
    {
        if (n is not JsonObject o || ((string?)o["code"])?.Trim() is not { Length: > 0 } code) return null;
        double N(string k) => Json.Number(o[k]) ?? 0;
        return new PriceRow(code, (string?)o["name"] ?? "", (string?)o["unit"] ?? "", N("m"), N("l"), N("e"), N("t"), Json.Number(o["hours"]),
            (string?)o["trade"], ((string?)o["article"]) is { Length: > 0 } ar ? ar : null, (string?)o["kind"], Json.Number(o["consumption"]));
    }

    static Entity AddList(ICommandContext ctx, string name, IReadOnlyList<PriceRow> rows, string currency, string? source, bool indicative)
    {
        var taken = ctx.Model.OfClass(PriceLists.Class).Select(e => e.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var unique = name;
        for (var i = 2; taken.Contains(unique); i++) unique = $"{name} ({i})";
        var e = new Entity(ctx.NewId(), PriceLists.Class, unique, PriceLists.Attrs(rows, currency, source, indicative));
        ctx.Add(e);
        return e;
    }

    static void SetArticles(ICommandContext ctx, string? typeId, JsonNode? articles)
    {
        var type = ctx.Model.Get(typeId ?? "") ?? throw new CommandException("There is no such type.");
        var list = ArticleRef.Parse(articles);
        foreach (var a in list) Parts.Parse(a.Part);
        var attrs = type.CloneAttrs();
        if (list.Count == 0) attrs.Remove("articles");
        else attrs["articles"] = new JsonArray(list.Select(x => (JsonNode)x.ToJson()).ToArray());
        ctx.Replace(type.With(attrs: attrs));
    }

    /// <summary>A rule set for a material (or a category), replacing the one there was; without a code, the rule goes.</summary>
    static string? SetRule(ICommandContext ctx, JsonObject a)
    {
        var material = ((string?)a["material"])?.Trim() is { Length: > 0 } mm ? mm : null;
        var category = material == null && ((string?)a["category"])?.Trim() is { Length: > 0 } cc ? cc : null;
        if (material == null && category == null) throw new CommandException("A rule is for a material or a category of material.");
        if (material != null && ctx.Model.Get(material) is not { Class: IfcClass.Material }) throw new CommandException("There is no such material.");
        var old = ctx.Model.OfClass(ArticleRule.Class).FirstOrDefault(e => material != null ? e.Str("material") == material : e.Str("material") == null && string.Equals(e.Str("category"), category, StringComparison.OrdinalIgnoreCase));
        if (((string?)a["code"])?.Trim() is not { Length: > 0 }) { if (old != null) ctx.Remove(old.Id); return null; }
        var art = ArticleRef.Parse(new JsonArray(a.DeepClone())).Single();
        var attrs = art.ToJson();
        if (material != null) attrs["material"] = material; else attrs["category"] = category;
        var name = material != null ? $"{ctx.Model.Get(material)!.Name} → {art.Code}" : $"{category} → {art.Code}";
        if (old != null) { ctx.Replace(old.With(name: name, attrs: attrs)); return old.Id; }
        var e = new Entity(ctx.NewId(), ArticleRule.Class, name, attrs);
        ctx.Add(e);
        return e.Id;
    }

    static JsonObject LineJson(EstimateLine l, EstimateSettings s) => new()
    {
        ["key"] = l.Key, ["code"] = l.Code, ["name"] = l.Name, ["unit"] = l.Unit, ["qty"] = l.Qty, ["basis"] = l.Basis,
        ["m"] = l.M, ["l"] = l.L, ["e"] = l.E, ["t"] = l.T, ["unitPrice"] = l.UnitPrice, ["value"] = l.Value,
        ["other"] = Money.Round(s.InOther(l.Value)), ["hours"] = l.Hours, ["manual"] = l.Manual, ["priced"] = l.Priced,
        ["elements"] = new JsonArray(l.Elements.Select(x => (JsonNode)x).ToArray()),
        ["tasks"] = l.Tasks.Count > 0 ? new JsonArray(l.Tasks.Select(x => (JsonNode)x).ToArray()) : null,
    };

    public static JsonObject EstimateJson(IQueryContext q, EstimateResult r)
    {
        var s = r.Settings;
        var t = r.Totals;
        return new JsonObject
        {
            ["id"] = r.Entity.Id, ["name"] = r.Entity.Name, ["predefined"] = r.Entity.PredefinedType ?? "COSTPLAN",
            ["settings"] = new JsonObject
            {
                ["currency"] = s.Currency, ["other"] = s.Other, ["eurRate"] = s.EurRate, ["labourTax"] = s.LabourTax, ["overheads"] = s.Overheads, ["profit"] = s.Profit,
                ["vat"] = s.Vat, ["grouping"] = s.Grouping, ["equivalent"] = s.Equivalent, ["derive"] = s.Derive,
                ["priceLists"] = new JsonArray(s.PriceLists.Select(x => (JsonNode)x).ToArray()),
            },
            ["chapters"] = new JsonArray(r.Chapters.Select(c => (JsonNode)new JsonObject
            {
                ["name"] = c.Name, ["m"] = c.Sums.M, ["l"] = c.Sums.L, ["e"] = c.Sums.E, ["t"] = c.Sums.T, ["direct"] = c.Sums.Direct,
                ["lines"] = new JsonArray(c.Lines.Select(l => (JsonNode)LineJson(l, s)).ToArray()),
            }).ToArray()),
            ["totals"] = new JsonObject
            {
                ["m"] = t.Direct.M, ["l"] = t.Direct.L, ["e"] = t.Direct.E, ["t"] = t.Direct.T, ["direct"] = t.Direct.Direct, ["labourTax"] = t.LabourTax,
                ["directTotal"] = t.DirectTotal, ["overheads"] = t.Overheads, ["profit"] = t.Profit, ["net"] = t.Net, ["vat"] = t.Vat, ["total"] = t.Total, ["other"] = t.Other,
            },
            ["unpriced"] = new JsonArray(r.Unpriced.Select(u => (JsonNode)new JsonObject
            {
                ["type"] = u.TypeId, ["class"] = u.Class, ["name"] = u.Name, ["count"] = u.Count, ["elements"] = new JsonArray(u.Elements.Select(x => (JsonNode)x).ToArray()),
            }).ToArray()),
            ["missing"] = new JsonArray(r.Missing.Select(x => (JsonNode)x).ToArray()),
            ["lists"] = new JsonArray(PriceLists.All(q.Model).Select(l => (JsonNode)new JsonObject { ["id"] = l.Id, ["name"] = l.Name }).ToArray()),
        };
    }

    /// <summary>The tasks a line of the user's is done in, checked: the project's IfcTasks.</summary>
    static List<string> TasksArg(IModelView m, JsonNode? n)
    {
        var ids = n is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().Distinct().ToList() : [];
        foreach (var id in ids) if (m.Get(id)?.Class != "IfcTask") throw new CommandException("A line is done in tasks of the work schedule.");
        return ids;
    }

    static List<string> FormsArg(IQueryContext q, JsonObject a)
    {
        var all = CostForms.All.Select(f => f.Id).ToList();
        // All of them: the month table only when there is a schedule to lay the estimate on.
        if (a["forms"] is null && a["form"] is null && q.Services.Get<IWorkSchedules>()?.Times(q) == null) all.Remove("monthly");
        if (a["forms"] is JsonArray fs) return fs.Select(x => (string?)x).OfType<string>().Where(all.Contains).ToList() is { Count: > 0 } l ? l : all;
        return (string?)a["form"] is { } one && all.Contains(one) ? [one] : all;
    }

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Costing.Class, "Estimate", [], "#7a6a3a", Kind: "resource"));
        r.Class(new(PriceLists.Class, "Price list", [], "#7a6a3a", Kind: "resource"));
        r.Class(new(ArticleRule.Class, "Article rule", [], "#7a6a3a", Kind: "resource"));
        r.Service<IIfcControls>(new CostIfc());
        foreach (var (id, label) in CostForms.All) r.Service<IScheduleSource>(new CostSchedule(id, label));
        // Cost over time (stage 7): the tasks' costs for the Gantt, the S-curve on sheets, the estimates of two versions compared.
        r.Service<IScheduleCosts>(new ScheduleCostsOf());
        r.Service<ISheetContent>(new CostCurveSheet());
        r.Service<IVersionCompare>(new CostCompare());
        r.Catalog("forms", new JsonArray(CostForms.All.Select(f => (JsonNode)new JsonObject { ["id"] = f.Id, ["label"] = f.Label }).ToArray()));

        // ---------------------------------------------------------------- price lists

        // A file's table read, its headings found and its columns guessed, with its first rows, for the matching dialog.
        r.Query("readPrices", (q, a) =>
        {
            var table = PriceLists.Table(Bytes(a));
            var (header, map) = PriceLists.Guess(table);
            var at = header >= 0 ? header : 0;
            return new JsonObject
            {
                ["header"] = header, ["rows"] = Math.Max(0, table.Count - at - 1),
                ["headings"] = new JsonArray((table.Count > at ? table[at] : []).Select(c => (JsonNode)c).ToArray()),
                ["sample"] = new JsonArray(table.Skip(at + 1).Where(r => !r.All(string.IsNullOrWhiteSpace)).Take(6).Select(r => (JsonNode)new JsonArray(r.Select(c => (JsonNode)c).ToArray())).ToArray()),
                ["map"] = new JsonObject(map.Select(kv => KeyValuePair.Create(kv.Key, kv.Value is { } v ? (JsonNode?)v : null))),
                ["fields"] = new JsonArray(PriceLists.Fields.Select(f => (JsonNode)f).ToArray()),
            };
        });

        // A price list made from a file, its columns as matched (else as guessed).
        r.Command("importPrices", "Import price list", (ctx, a) =>
        {
            var table = PriceLists.Table(Bytes(a));
            var (guessHeader, guessed) = PriceLists.Guess(table);
            var header = Json.Number(a["header"]) is { } h ? (int)h : guessHeader;
            if (header < 0) throw new CommandException("The table has no headings Lapin knows: match its columns by hand.");
            var map = a["map"] is JsonObject ? MapArg(a["map"]) : guessed;
            var res = PriceLists.Import(table, header, map);
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? Path.GetFileNameWithoutExtension(n) : "Price list";
            var e = AddList(ctx, name, res.Rows, Currency(a["currency"]), (string?)a["name"], false);
            return new JsonObject
            {
                ["id"] = e.Id, ["name"] = e.Name, ["count"] = res.Rows.Count(x => !x.IsResource), ["resources"] = res.Rows.Count(x => x.IsResource), ["skipped"] = res.Skipped,
                ["notes"] = new JsonArray(res.Notes.Select(x => (JsonNode)x).ToArray()),
                ["rows"] = new JsonArray(res.Rows.Select(x => (JsonNode)x.ToJson()).ToArray()),
            };
        });

        // A list from rows given (the app's library, another project), or Lapin's indicative template.
        r.Command("addPriceList", "Add price list", (ctx, a) =>
        {
            var template = a["template"] is JsonValue tv && tv.TryGetValue<bool>(out var tb) && tb;
            var rows = template ? PriceLists.IndicativeRows() : (a["rows"] as JsonArray)?.Select(x => PriceRow.Read(x) ?? RowArg(x)).OfType<PriceRow>().ToList() ?? [];
            if (rows.Count == 0) throw new CommandException("A price list needs rows.");
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : template ? "Lapin indicative prices" : "Price list";
            var e = AddList(ctx, name, rows, template ? "RON" : Currency(a["currency"]), template ? "Lapin template" : (string?)a["source"], template || a["indicative"] is JsonValue iv && iv.TryGetValue<bool>(out var ib) && ib);
            return new JsonObject { ["id"] = e.Id, ["name"] = e.Name };
        });

        r.Command("updatePriceList", "Change price list", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"], PriceLists.Class, "price list");
            var attrs = e.CloneAttrs();
            if (a.ContainsKey("currency")) attrs["currency"] = Currency(a["currency"]);
            ctx.Replace(e.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs));
            return null;
        });

        r.Command("removePriceList", "Remove price list", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"], PriceLists.Class, "price list");
            ctx.Remove(e.Id);
            return null;
        });

        r.Query("priceLists", (q, _) => new JsonArray(PriceLists.All(q.Model).Select(l => (JsonNode)PriceListJson(l)).ToArray()));

        // A list's rows (all lists' when none is named), maybe only those whose code or text has the words searched.
        r.Query("priceRows", (q, a) =>
        {
            var lists = PriceLists.All(q.Model).Where(l => (string?)a["id"] is not { } id || l.Id == id).ToList();
            var words = ArticleMapping.Words((string?)a["search"]);
            var limit = Json.Number(a["limit"]) is { } lim && lim > 0 ? (int)lim : 200;
            var rows = lists.SelectMany(l => l.Rows.Where(x => !x.IsResource).Select(x => (List: l, Row: x)))
                .Where(x => words.Count == 0 || words.All(w => PriceLists.Fold(x.Row.Code + " " + x.Row.Name).Contains(w, StringComparison.Ordinal)))
                .Take(limit).Select(x => { var o = RowJson(x.Row); o["list"] = x.List.Id; o["currency"] = x.List.Currency; return (JsonNode)o; }).ToArray();
            return new JsonArray(rows);
        });

        r.Query("template", (_, _) => PriceLists.TemplateCsv());

        // ---------------------------------------------------------------- articles and rules

        r.Command("setArticles", "Type articles", (ctx, a) => { SetArticles(ctx, (string?)a["type"], a["articles"]); return null; });

        r.Command("setRule", "Article rule", (ctx, a) => SetRule(ctx, a) is { } id ? new JsonObject { ["id"] = id } : null);

        r.Command("removeRule", "Remove article rule", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"], ArticleRule.Class, "article rule");
            ctx.Remove(e.Id);
            return null;
        });

        r.Query("rules", (q, _) => new JsonArray(ArticleRule.All(q.Model).Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Id, ["material"] = x.Material, ["category"] = x.Category, ["article"] = x.Article.ToJson(),
        }).ToArray()));

        r.Query("mapping", (q, _) => ArticleMapping.Of(q));

        // The Map articles dialog's choices, as one step: types' articles and materials' rules.
        r.Command("applyMapping", "Map articles", (ctx, a) =>
        {
            var types = 0;
            var rules = 0;
            foreach (var t in (a["types"] as JsonArray ?? []).OfType<JsonObject>()) { SetArticles(ctx, (string?)t["id"], t["articles"]); types++; }
            foreach (var m in (a["rules"] as JsonArray ?? []).OfType<JsonObject>()) { SetRule(ctx, m); rules++; }
            return new JsonObject { ["types"] = types, ["rules"] = rules };
        });

        // ---------------------------------------------------------------- the estimate

        r.Command("createEstimate", "New estimate", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : ctx.Query.Title is { Length: > 0 } t ? $"Estimate: {t}" : "Estimate";
            var attrs = new JsonObject { ["currency"] = Currency(a["currency"]) };
            var e = new Entity(ctx.NewId(), Costing.Class, name, attrs, "COSTPLAN");
            ctx.Add(e);
            return new JsonObject { ["id"] = e.Id };
        });

        r.Command("updateEstimate", "Estimate", (ctx, a) =>
        {
            var e = EstimateArg(ctx.Model, a);
            var attrs = e.CloneAttrs();
            if (a.ContainsKey("currency")) attrs["currency"] = Currency(a["currency"]);
            if (a.ContainsKey("eurRate"))
                attrs["eurRate"] = Json.Number(a["eurRate"]) is { } rate && rate > 0 && double.IsFinite(rate) ? rate : throw new CommandException("The rate is how many RON one EUR is: a positive number.");
            if (a.ContainsKey("labourTax")) attrs["labourTax"] = Rate(a["labourTax"], "contribution on labour");
            if (a.ContainsKey("overheads")) attrs["overheads"] = Rate(a["overheads"], "overheads rate", 500);
            if (a.ContainsKey("profit")) attrs["profit"] = Rate(a["profit"], "profit rate", 500);
            if (a.ContainsKey("vat")) attrs["vat"] = Rate(a["vat"], "VAT rate");
            if (a.ContainsKey("grouping"))
                attrs["grouping"] = (string?)a["grouping"] is { } g && EstimateSettings.Groupings.Contains(g) ? g : throw new CommandException("An estimate's chapters go by kind of work (chapter) or by storey.");
            if (a.ContainsKey("priceLists"))
            {
                if (a["priceLists"] is JsonArray pl && pl.Count > 0) attrs["priceLists"] = pl.DeepClone(); else attrs.Remove("priceLists");
            }
            foreach (var flag in new[] { "equivalent", "derive" })
                if (a[flag] is JsonValue fv && fv.TryGetValue<bool>(out var fb)) attrs[flag] = fb;
            var kind = (string?)a["predefined"] is "COSTPLAN" or "PRICEDBILLOFQUANTITIES" ? (string?)a["predefined"] : null;
            ctx.Replace(e.With(name: ((string?)a["name"])?.Trim() is { Length: > 0 } n ? n : null, attrs: attrs, predefinedType: kind));
            return null;
        });

        // A line of the user's: work the model does not have (a provision, an item of site set-up).
        r.Command("addLine", "Estimate line", (ctx, a) =>
        {
            var e = EstimateArg(ctx.Model, a);
            var attrs = e.CloneAttrs();
            var lines = attrs["lines"] as JsonArray ?? [];
            var id = ctx.NewId();
            var o = new JsonObject { ["id"] = id, ["chapter"] = ((string?)a["chapter"])?.Trim() is { Length: > 0 } c ? c : "Other", ["code"] = (string?)a["code"] ?? "", ["name"] = (string?)a["name"] ?? "New line", ["unit"] = (string?)a["unit"] ?? "pcs", ["qty"] = Json.Number(a["qty"]) ?? 1 };
            foreach (var k in new[] { "m", "l", "e", "t", "hours" }) if (Json.Number(a[k]) is { } v) o[k] = v;
            if (TasksArg(ctx.Model, a["tasks"]) is { Count: > 0 } tasks) o["tasks"] = new JsonArray(tasks.Select(x => (JsonNode)x).ToArray());
            lines.Add((JsonNode)o);
            attrs["lines"] = lines;
            ctx.Replace(e.With(attrs: attrs));
            return new JsonObject { ["line"] = id };
        });

        r.Command("updateLine", "Estimate line", (ctx, a) =>
        {
            var e = EstimateArg(ctx.Model, a);
            var attrs = e.CloneAttrs();
            var line = (attrs["lines"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(o => (string?)o["id"] == (string?)a["line"]) ?? throw new CommandException("The estimate has no such line of its own.");
            foreach (var k in new[] { "chapter", "code", "name", "unit" }) if (a.ContainsKey(k)) line[k] = ((string?)a[k])?.Trim() ?? "";
            foreach (var k in new[] { "qty", "m", "l", "e", "t", "hours" })
                if (a.ContainsKey(k))
                {
                    if (a[k] is null) line.Remove(k);
                    else line[k] = Json.Number(a[k]) is { } v && double.IsFinite(v) ? v : throw new CommandException($"„{k}” is a number.");
                }
            if (a.ContainsKey("tasks"))
            {
                var tasks = TasksArg(ctx.Model, a["tasks"]);
                if (tasks.Count > 0) line["tasks"] = new JsonArray(tasks.Select(x => (JsonNode)x).ToArray()); else line.Remove("tasks");
            }
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        r.Command("removeLine", "Remove estimate line", (ctx, a) =>
        {
            var e = EstimateArg(ctx.Model, a);
            var attrs = e.CloneAttrs();
            if (attrs["lines"] is not JsonArray lines || lines.OfType<JsonObject>().FirstOrDefault(o => (string?)o["id"] == (string?)a["line"]) is not { } line)
                throw new CommandException("The estimate has no such line of its own.");
            lines.Remove(line);
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        r.Query("estimates", (q, _) => new JsonArray(q.Model.OfClass(Costing.Class).Select(e => (JsonNode)new JsonObject { ["id"] = e.Id, ["name"] = e.Name }).ToArray()));

        r.Query("estimate", (q, a) => Costing.EstimateOf(q.Model, (string?)a["id"]) is { } e ? EstimateJson(q, Costing.Compute(q, e)) : null);

        // A form of the estimate as a table (the view shows it), or as a workbook or PDF (base64; all forms when none is named).
        r.Query("form", (q, a) =>
        {
            var e = EstimateArg(q.Model, a);
            var form = CostForms.Make(q, Costing.Compute(q, e), (string?)a["form"] ?? "estimate");
            return new JsonObject
            {
                ["id"] = form.Id, ["title"] = form.Title, ["subtitle"] = form.Subtitle,
                ["columns"] = new JsonArray(form.Columns.Select(c => (JsonNode)c).ToArray()),
                ["numeric"] = new JsonArray(form.Numeric.Select(c => (JsonNode)c).ToArray()),
                ["rows"] = new JsonArray(form.Rows.Select(x => (JsonNode)new JsonObject
                {
                    ["cells"] = new JsonArray(x.Cells.Select(c => (JsonNode)c).ToArray()), ["key"] = x.Key, ["style"] = x.Style,
                }).ToArray()),
            };
        });

        r.Query("xlsx", (q, a) =>
        {
            var e = EstimateArg(q.Model, a);
            var res = Costing.Compute(q, e);
            return Convert.ToBase64String(CostForms.Xlsx(FormsArg(q, a).Select(f => CostForms.Make(q, res, f, raw: true)).ToList()));
        });

        // ---------------------------------------------------------------- cost over time (stage 7)

        // The S-curve and the months: planned, the baseline's, the work done; the tasks' costs. Null with why when there is no estimate or schedule.
        r.Query("overTime", (q, a) =>
        {
            var (o, why) = CostOverTime.For(q, (string?)a["id"]);
            return o?.ToJson() ?? new JsonObject { ["why"] = why };
        });

        r.Query("overTimeXlsx", (q, a) =>
        {
            var (o, why) = CostOverTime.For(q, (string?)a["id"]);
            return Convert.ToBase64String(CostTimeOutputs.Xlsx(q, o ?? throw new CommandException(why!)));
        });

        r.Query("overTimePdf", (q, a) =>
        {
            var (o, why) = CostOverTime.For(q, (string?)a["id"]);
            return Convert.ToBase64String(CostTimeOutputs.Pdf(q, o ?? throw new CommandException(why!)));
        });

        // The S-curve on a new sheet (A3 landscape unless asked): the estimate is the sheet's content.
        r.Command("curveSheet", "S-curve on sheet", (ctx, a) =>
        {
            var e = EstimateArg(ctx.Model, a);
            var (o, why) = CostOverTime.For(ctx.Query, e.Id);
            if (o == null) throw new CommandException(why!);
            var paper = (string?)a["paper"] ?? "A3";
            var portrait = (string?)a["orientation"] == "portrait";
            if (!Sheets.Papers.TryGetValue(paper, out var size)) throw new CommandException("A sheet is A0, A1, A2, A3 or A4.");
            var (pw, ph) = portrait ? (size.H, size.W) : (size.W, size.H);
            var (fw, fh) = (pw - 2 * Sheets.Margin - 4, ph - 2 * Sheets.Margin - Sheets.TitleBlock.H - 4);
            var (w, h, _) = new CostCurveSheet().Pages(ctx.Query, e, fw, fh)[0];
            var sheet = Sheets.AddSheet(ctx, $"S-curve · {e.Name}", paper, portrait, [(e.Id, Sheets.Margin + 4 + w / 2, Sheets.Margin + 4 + h / 2, 1)]);
            return new JsonObject { ["sheetId"] = sheet.Id };
        });

        r.Query("pdf", (q, a) =>
        {
            var e = EstimateArg(q.Model, a);
            var res = Costing.Compute(q, e);
            return Convert.ToBase64String(CostForms.Pdf(FormsArg(q, a).Select(f => CostForms.Make(q, res, f)).ToList()));
        });
    }
}
