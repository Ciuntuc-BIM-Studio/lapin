using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// 2D symbols of the user's own (docs/plan-user-extensibility.md §3): definitions are <c>LapinSymbol</c> entities of the project
/// (<see cref="SymbolDef"/> in <c>def</c>) beside Lapin's built-ins (<see cref="BuiltinSymbols"/>); an instance is an IfcAnnotation
/// <c>SYMBOL</c> on a view (<c>viewId</c>, <c>at</c> in the view's metres) or on a sheet (<c>sheetId</c>, <c>at</c> in paper mm from
/// the page's top-left), with its <c>symbolId</c>, <c>angle</c> (°), <c>scale</c>, <c>flipX</c>, <c>flipY</c>, the values of its
/// attributes (<c>values</c>), its drafting layer and colour. A symbol's library file is <c>.lapinsymbols</c>
/// (<c>{format: "lapin-symbols", version: 1, symbols: [{name, def}]}</c>).
/// </summary>
public static class Symbols
{
    public const string Class = "LapinSymbol";
    public const string Predefined = "SYMBOL";
    public const string FileFormat = "lapin-symbols";
    public const int FileVersion = 1;

    // ------------------------------------------------------------ definitions

    /// <summary>A symbol's definition by id: a built-in's or the project's; null when there is none.</summary>
    public static SymbolDef? DefOf(IModelView m, string? id) =>
        BuiltinSymbols.Find(id)?.Def ?? (m.Get(id ?? "") is { Class: Class } e ? SymbolDef.From(e.Attrs["def"]) : null);

    public static string? NameOf(IModelView m, string? id) => BuiltinSymbols.Find(id)?.Name ?? (m.Get(id ?? "") is { Class: Class } e ? e.Name : null);

    public static IEnumerable<Entity> Instances(IModelView m) => m.OfClass(IfcClass.Annotation).Where(e => e.PredefinedType == Predefined);

    public static int Uses(IModelView m, string id) => Instances(m).Count(e => e.Str("symbolId") == id);

    /// <summary>A name no other symbol of the project (nor a built-in) has: the one asked, else with " 2", " 3"….</summary>
    public static string FreeName(IModelView m, string name, string? except = null)
    {
        var taken = m.OfClass(Class).Where(e => e.Id != except).Select(e => e.Name).Concat(BuiltinSymbols.All.Select(b => b.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(name)) return name;
        for (var n = 2; ; n++) if (!taken.Contains($"{name} {n}")) return $"{name} {n}";
    }

    static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } e ? e
        : BuiltinSymbols.Is(id) ? throw new CommandException("Lapin's own symbols stay as they are: duplicate one to change it.")
        : throw new CommandException("There is no such symbol.");

    public static Entity Create(ICommandContext ctx, string name, SymbolDef def)
    {
        var e = new Entity(ctx.NewId(), Class, FreeName(ctx.Model, name.Trim() is { Length: > 0 } n ? n : "Symbol"), new JsonObject { ["def"] = def.ToJson() });
        ctx.Add(e);
        return e;
    }

    // ------------------------------------------------------------ live text

    /// <summary>
    /// What an instance's tokens say: its attributes (<c>attr.key</c>: its value, else the definition's default), the symbol's
    /// (<c>symbol.name</c>, <c>symbol.angle</c>, <c>symbol.scale</c>), where it stands (<c>point.x</c>, <c>point.y</c> in the view's
    /// metres), the view's (<c>view.name</c>, <c>view.scale</c> as 1:100, <c>view.scaleNumber</c>), the plan's level
    /// (<c>level.name</c>, <c>level.elevation</c> as +3.00, <c>level.z</c>), the project's and the system's; then <paramref name="more"/>
    /// (a slot's own tokens, a sheet's).
    /// </summary>
    public static Func<string, (bool Known, string? Value)> Resolver(IModelView m, SymbolDef def, Entity? instance, Entity? view, Vec2? at = null, Func<string, (bool Known, string? Value)>? more = null)
    {
        var values = instance?.Attrs["values"] as JsonObject;
        return key =>
        {
            if (more?.Invoke(key) is { Known: true } fromMore) return fromMore;
            var dot = key.IndexOf('.');
            if (dot < 0) return (false, null);
            var (source, k) = (key[..dot], key[(dot + 1)..]);
            switch (source)
            {
                case "attr":
                {
                    if (values != null && values.FirstOrDefault(p => p.Key.Equals(k, StringComparison.OrdinalIgnoreCase)) is { Value: { } v }) return (true, v.ToString());
                    return def.Attributes.FirstOrDefault(a => a.Key.Equals(k, StringComparison.OrdinalIgnoreCase)) is { } a ? (true, a.Default) : (false, null);
                }
                case "symbol":
                    return k switch
                    {
                        "name" => (true, instance?.Str("symbolId") is { } sid ? NameOf(m, sid) : null),
                        "angle" => (true, Num(instance?.NumOrNull("angle") ?? 0)),
                        "scale" => (true, Num(instance?.NumOrNull("scale") ?? 1)),
                        _ => (false, null),
                    };
                case "point" when at is { } p:
                    return k switch { "x" => (true, Num(p.X)), "y" => (true, Num(p.Y)), _ => (false, null) };
                case "view" when view != null:
                    return k switch
                    {
                        "name" => (true, view.Name),
                        "scale" => (true, $"1:{view.NumOrNull("scale") ?? 100:0}"),
                        "scaleNumber" => (true, Num(view.NumOrNull("scale") ?? 100)),
                        "kind" => (true, view.Str("kind")),
                        _ => view.Attrs[k] is JsonValue jv ? (true, jv.ToString()) : (false, null),
                    };
                case "level":
                {
                    var storey = view != null ? m.Get(view.Str("storeyId") ?? "") : null;
                    if (storey is not { Class: IfcClass.BuildingStorey }) return k is "name" or "elevation" or "z" ? (true, "") : (false, null);
                    var z = storey.Num("elevation");
                    return k switch
                    {
                        "name" => (true, storey.Name),
                        "elevation" => (true, Level(z)),
                        "z" => (true, Num(z)),
                        _ => (false, null),
                    };
                }
                case "project":
                    if (k == "name") return (true, Spatial.Project(m)?.Name ?? "");
                    if (Documents.ProjectInfo.Fields.Any(f => f.Key == k)) return (true, Documents.ProjectInfo.Get(m, k));
                    return Documents.ProjectInfo.Customs(m).FirstOrDefault(c => c.Key.Equals(k, StringComparison.OrdinalIgnoreCase)) is { } c ? (true, c.Printed) : (false, null);
                case "system":
                    return k switch
                    {
                        "printDate" => (true, DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)),
                        "app" => (true, "Lapin"),
                        _ => (false, null),
                    };
                default: return (false, null);
            }
        };
    }

    static string Num(double v) => Math.Round(v, 6).ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>A level as drawings write it: +3.00, −0.45, ±0.00.</summary>
    public static string Level(double z) => Math.Abs(z) < 0.005 ? "±0.00" : $"{(z >= 0 ? "+" : "−")}{Math.Abs(z):0.00}".Replace(',', '.');

    /// <summary>The tokens offered to insert in a symbol's text, by group (its attributes first).</summary>
    public static List<(string Key, string Label, string Group)> FieldList(IModelView m, SymbolDef? def)
    {
        var o = new List<(string Key, string Label, string Group)>();
        foreach (var a in def?.Attributes ?? []) o.Add(("attr." + a.Key, a.Label, "attribute"));
        o.AddRange([
            ("view.name", "View name", "view"), ("view.scale", "View scale (1:100)", "view"), ("view.scaleNumber", "View scale (100)", "view"),
            ("level.name", "Level name", "view"), ("level.elevation", "Level elevation (+3.00)", "view"), ("level.z", "Level elevation (m)", "view"),
            ("point.x", "Insertion point x (m)", "symbol"), ("point.y", "Insertion point y (m)", "symbol"),
            ("point.elevation", "Level at the insertion point (+3.00; sections and elevations)", "symbol"), ("point.z", "Level at the insertion point (m)", "symbol"),
            ("symbol.name", "Symbol name", "symbol"), ("symbol.angle", "Symbol angle (°)", "symbol"), ("symbol.scale", "Symbol scale", "symbol"),
            ("project.name", "Project name", "project"),
        ]);
        foreach (var (key, label, _) in Documents.ProjectInfo.Grouped) o.Add(("project." + key, label, "project"));
        foreach (var c in Documents.ProjectInfo.Customs(m)) o.Add(("project." + c.Key, c.Label, "project"));
        o.Add(("system.printDate", "Date of print", "system"));
        // Drawn as a tag (decision 552): the tagged element's fields, its storey's parameters, any parameter by its name.
        o.AddRange([
            ("name", "Tagged element's name", "tag"), ("mark", "Tagged element's mark", "tag"), ("type", "Tagged element's type", "tag"),
            ("number", "Room's number", "tag"), ("area", "Room's area", "tag"), ("storey.name", "Tagged element's storey", "tag"),
        ]);
        var storeyParams = Spatial.Storeys(m).SelectMany(s => s.Attrs["params"] is JsonArray l ? l.OfType<JsonObject>() : [])
            .Where(p => Params.SetOf(p) == null).Select(p => ((string?)p["name"])?.Trim()).OfType<string>().Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var p in storeyParams) o.Add(("storey." + p, p, "tag"));
        return o;
    }

    // ------------------------------------------------------------ instances

    /// <summary>How an instance lies: its point, angle (°), scale and flips.</summary>
    public static (Vec2 At, double Angle, double Scale, bool FlipX, bool FlipY) Pose(Entity e)
    {
        var at = e.Attrs["at"] is JsonArray a && a.Count >= 2 ? new Vec2(Json.Number(a[0]) ?? 0, Json.Number(a[1]) ?? 0) : default;
        var scale = e.NumOrNull("scale") is { } s && s > 0 ? s : 1;
        return (at, e.NumOrNull("angle") ?? 0, scale, e.Bool("flipX"), e.Bool("flipY"));
    }

    /// <summary>
    /// An instance drawn on its view: an annotation symbol's mm on paper (the view's scale), a model one's the building's;
    /// its own colour, else its layer's (<paramref name="layerColor"/>). Null when its symbol is gone.
    /// </summary>
    public static ViewMark? Mark(ViewContext c, Entity e, string layer, string layerColor, Func<string, (bool Known, string? Value)>? more = null)
    {
        if (DefOf(c.Model, e.Str("symbolId")) is not { } def) return null;
        var (at, angle, scale, fx, fy) = Pose(e);
        var model = def.Scale == "model";
        var unit = (model ? 0.001 : c.Mm) * scale;
        var x = new SymbolXf(at, angle * Math.PI / 180, unit, model ? scale * 0.001 / c.Mm : scale, fx, fy);
        // Where it stands, as a level: a section's or an elevation's height there, a plan's level (a spot level reads it, {point.elevation}).
        double? z = c.Kind is "section" or "elevation" && Math.Abs(c.Frame.Up.Z) > 0.999 ? c.Frame.Origin.Z + at.Y * c.Frame.Up.Z
            : c.Kind == "plan" && c.Model.Get(c.View.Str("storeyId") ?? "") is { Class: IfcClass.BuildingStorey } st ? st.Num("elevation") : null;
        Func<string, (bool Known, string? Value)> tokens = key =>
        {
            if (more?.Invoke(key) is { Known: true } r) return r;
            return key switch
            {
                "point.z" when z is { } h => (true, Num(h)),
                "point.elevation" when z is { } h => (true, Standards.IsImperial(c.Book.Units) ? Standards.FormatLevel(h, c.Book.Units) : Level(h)),
                _ => (false, null),
            };
        };
        var sd = SymbolRender.Render(c.Model, def, Resolver(c.Model, def, e, c.View, at, tokens), unit);
        var mk = new ViewMark(e.Id);
        SymbolRender.Into(mk, sd, x, layer, e.Str("color") ?? layerColor, c.Mm, DraftingResources.Patterns(c.Model));
        mk.Data = new JsonObject { ["symbol"] = e.Str("symbolId"), ["at"] = new JsonArray(Math.Round(at.X, 6), Math.Round(at.Y, 6)), ["angle"] = angle };
        return mk;
    }

    /// <summary>A sheet's own symbols (a north arrow, a legend's key, a stamp) as its pens, in paper mm, its tokens the sheet's as a title block reads them.</summary>
    public static List<Pen> SheetPens(IQueryContext q, Entity sheet, IReadOnlyList<Sheets.Placement>? placements = null)
    {
        var m = q.Model;
        var pens = new List<Pen>();
        var sheetTokens = TitleBlocks.Resolver(q, sheet, placements);
        foreach (var e in Instances(m).Where(e => e.Str("sheetId") == sheet.Id))
        {
            if (DefOf(m, e.Str("symbolId")) is not { } def) continue;
            var (at, angle, scale, fx, fy) = Pose(e);
            var sd = SymbolRender.Render(m, def, Resolver(m, def, e, null, at, sheetTokens), 0.001 * scale);
            var own = SymbolRender.ToPens(sd, at, angle * Math.PI / 180, scale, fx, fy, e.Str("color") ?? "#000000", DraftingResources.Patterns(m));
            pens.AddRange(own);
        }
        return pens;
    }

    /// <summary>A symbol alone, as the library and the editor show it: its box (mm) and its pens, y down, its insertion point at (0, 0).</summary>
    public static (double X0, double Y0, double X1, double Y1, List<Pen> Pens) Preview(IModelView m, SymbolDef def, JsonObject? values = null, bool flipX = false, bool flipY = false, double angle = 0)
    {
        var instance = values == null ? null : new Entity("preview", IfcClass.Annotation, "preview", new JsonObject { ["values"] = values.DeepClone() }, Predefined);
        var sd = SymbolRender.Render(m, def, Resolver(m, def, instance, null, default), 0.001);
        var pens = SymbolRender.ToPens(sd, default, angle * Math.PI / 180, 1, flipX, flipY, "#000000", DraftingResources.Patterns(m));
        var pts = pens.SelectMany(p => p.Segments.SelectMany(s => new[] { s.A, s.B }).Concat(p.Loops.SelectMany(l => l))).ToList();
        foreach (var t in pens.SelectMany(p => p.Texts))
        {
            var w = t.Text.Length * t.Height * 0.6;
            pts.AddRange([t.At - new Vec2(w, t.Height), t.At + new Vec2(w, t.Height)]);
        }
        pts.Add(default);
        return (pts.Min(p => p.X), pts.Min(p => p.Y), pts.Max(p => p.X), pts.Max(p => p.Y), pens);
    }

    // ------------------------------------------------------------ files

    /// <summary>Symbols as a library file: each with its name and definition.</summary>
    public static JsonObject Export(IModelView m, IEnumerable<string> ids, string? name = null)
    {
        var list = new JsonArray();
        foreach (var id in ids.Distinct())
        {
            var def = DefOf(m, id) ?? throw new CommandException("There is no such symbol.");
            list.Add((JsonNode)new JsonObject { ["name"] = NameOf(m, id), ["def"] = def.ToJson() });
        }
        if (list.Count == 0) throw new CommandException("Which symbols?");
        var o = new JsonObject { ["format"] = FileFormat, ["version"] = FileVersion };
        if (name is { Length: > 0 }) o["name"] = name;
        o["symbols"] = list;
        return o;
    }

    /// <summary>A library file read and checked: its symbols' names and definitions; a wrong file refused whole.</summary>
    public static List<(string Name, SymbolDef Def)> Read(string text)
    {
        JsonNode? n;
        try { n = JsonNode.Parse(text); }
        catch (JsonException) { throw new CommandException("That is not a symbol library (.lapinsymbols): it is not JSON."); }
        if (n is not JsonObject o || (string?)o["format"] != FileFormat) throw new CommandException("That is not a symbol library (.lapinsymbols).");
        if ((Json.Number(o["version"]) ?? 0) > FileVersion) throw new CommandException("That symbol library was written by a newer Lapin: update Lapin to read it.");
        var o2 = new List<(string, SymbolDef)>();
        var i = 0;
        foreach (var s in o["symbols"] as JsonArray ?? [])
        {
            i++;
            if (s is not JsonObject so || ((string?)so["name"])?.Trim() is not { Length: > 0 } name) throw new CommandException($"Symbol {i} of the library has no name.");
            try { o2.Add((name, SymbolDef.Parse(so["def"]))); }
            catch (CommandException e) { throw new CommandException($"„{name}”: {e.Message}"); }
        }
        if (o2.Count == 0) throw new CommandException("The symbol library has no symbols.");
        return o2;
    }

    static bool Same(SymbolDef a, SymbolDef b) => JsonNode.DeepEquals(a.ToJson(), b.ToJson());

    // ------------------------------------------------------------ commands

    static JsonObject Summary(IModelView m, string id, string name, SymbolDef def, bool builtin) => new()
    {
        ["id"] = id, ["name"] = name, ["builtin"] = builtin, ["category"] = def.Category, ["scale"] = def.Scale,
        ["description"] = def.Description, ["uses"] = Uses(m, id),
        ["attributes"] = new JsonArray(def.Attributes.Select(a => (JsonNode)a.ToJson()).ToArray()),
    };

    static JsonObject PreviewJson(IModelView m, SymbolDef def, JsonObject? values = null, bool fx = false, bool fy = false, double angle = 0)
    {
        var (x0, y0, x1, y1, pens) = Preview(m, def, values, fx, fy, angle);
        return new JsonObject
        {
            ["box"] = new JsonArray(Math.Round(x0, 3), Math.Round(y0, 3), Math.Round(x1, 3), Math.Round(y1, 3)),
            ["pens"] = new JsonArray(pens.Select(p => (JsonNode)p.ToJson()).ToArray()),
        };
    }

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Symbol", [], "#555555", Kind: "resource"));

        // The symbols: Lapin's, then the project's, each with its category, scale, attributes and how many instances use it.
        r.Query("symbols.list", (q, a) =>
        {
            var m = q.Model;
            var list = new JsonArray();
            var previews = a["previews"] is JsonValue pv && pv.TryGetValue<bool>(out var p) && p;
            foreach (var b in BuiltinSymbols.All)
            {
                var s = Summary(m, b.Id, b.Name, b.Def, true);
                if (previews) s["preview"] = PreviewJson(m, b.Def);
                list.Add((JsonNode)s);
            }
            foreach (var e in m.OfClass(Class).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                if (SymbolDef.From(e.Attrs["def"]) is { } def)
                {
                    var s = Summary(m, e.Id, e.Name, def, false);
                    if (previews) s["preview"] = PreviewJson(m, def);
                    list.Add((JsonNode)s);
                }
            return new JsonObject { ["items"] = list, ["categories"] = new JsonArray(SymbolDef.Categories.Select(c => (JsonNode)c).ToArray()) };
        });

        r.Query("symbols.get", (q, a) =>
        {
            var id = (string?)a["id"] ?? "";
            var def = DefOf(q.Model, id) ?? throw new CommandException("There is no such symbol.");
            return new JsonObject { ["id"] = id, ["name"] = NameOf(q.Model, id), ["builtin"] = BuiltinSymbols.Is(id), ["def"] = def.ToJson(), ["uses"] = Uses(q.Model, id) };
        });

        // A symbol drawn alone (by id, or a definition given: the editor's), turned and flipped as asked, its attributes' values given.
        r.Query("symbols.preview", (q, a) =>
        {
            var def = a["def"] is JsonObject dj ? SymbolDef.Parse(dj) : DefOf(q.Model, (string?)a["id"]) ?? throw new CommandException("There is no such symbol.");
            static bool B(JsonNode? n) => n is JsonValue v && v.TryGetValue<bool>(out var b) && b;
            return PreviewJson(q.Model, def, a["values"] as JsonObject, B(a["flipX"]), B(a["flipY"]), Json.Number(a["angle"]) ?? 0);
        });

        // The tokens a symbol's text may hold, and the formula functions.
        r.Query("symbols.fields", (q, a) =>
        {
            var def = a["def"] is JsonObject dj ? SymbolDef.From(dj) : DefOf(q.Model, (string?)a["id"]);
            return new JsonObject
            {
                ["fields"] = new JsonArray(FieldList(q.Model, def).Select(f => (JsonNode)new JsonObject { ["key"] = f.Key, ["label"] = f.Label, ["group"] = f.Group }).ToArray()),
                ["functions"] = new JsonArray(Expr.FunctionNames.Select(f => (JsonNode)f).ToArray()),
            };
        });

        // A formula tried out: its value, or why it does not read (the editor's help), names read from the values given.
        r.Query("symbols.evaluate", (q, a) =>
        {
            var text = (string?)a["text"] ?? "";
            var values = a["values"] as JsonObject;
            var unknown = new List<string>();
            var filled = LiveText.Fill(text, key => values?[key] is JsonValue v ? (true, v.ToString()) : (false, null), unknown);
            return new JsonObject { ["text"] = filled, ["unknown"] = new JsonArray(unknown.Select(u => (JsonNode)u).ToArray()) };
        });

        // A new symbol: a definition given, or a copy of another (a built-in or the project's).
        r.Command("symbols.create", "New symbol", (ctx, a) =>
        {
            SymbolDef def;
            string name;
            if ((string?)a["from"] is { Length: > 0 } from)
            {
                def = DefOf(ctx.Model, from) ?? throw new CommandException("There is no such symbol to copy.");
                name = (string?)a["name"] is { Length: > 0 } n0 ? n0 : NameOf(ctx.Model, from) + " copy";
            }
            else
            {
                def = SymbolDef.Parse(a["def"]);
                name = (string?)a["name"] ?? "Symbol";
            }
            var e = Create(ctx, name, def);
            return new JsonObject { ["id"] = e.Id, ["name"] = e.Name };
        });

        // A symbol's name or definition changed, one undo step: every instance draws the new one.
        r.Command("symbols.update", "Edit symbol", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            if (a["def"] is JsonObject dj) attrs["def"] = SymbolDef.Parse(dj).ToJson();
            var name = (string?)a["name"] is { } n ? n.Trim() is { Length: > 0 } nn ? FreeName(ctx.Model, nn, e.Id) : throw new CommandException("A symbol needs a name.") : null;
            ctx.Replace(e.With(name: name, attrs: attrs));
            return null;
        });

        // A symbol deleted; its instances too only when asked (instances: true), else refused while any stands.
        r.Command("symbols.delete", "Delete symbol", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var placed = Instances(ctx.Model).Where(i => i.Str("symbolId") == e.Id).Select(i => i.Id).ToList();
            if (placed.Count > 0 && !(a["instances"] is JsonValue iv && iv.TryGetValue<bool>(out var withInstances) && withInstances))
                throw new CommandException($"„{e.Name}” is placed {placed.Count} time{(placed.Count == 1 ? "" : "s")}: delete those too, or keep the symbol.");
            foreach (var id in placed) ctx.Remove(id);
            ctx.Remove(e.Id);
            return new JsonObject { ["removed"] = placed.Count };
        });

        // Symbols as a library file (.lapinsymbols).
        r.Query("symbols.export", (q, a) =>
        {
            var ids = a["ids"] is JsonArray arr ? arr.Select(x => (string?)x).OfType<string>().ToList() : [];
            var o = Export(q.Model, ids, (string?)a["name"]);
            return new JsonObject { ["text"] = o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ["file"] = ((string?)a["name"] is { Length: > 0 } fn ? fn : "symbols") + ".lapinsymbols" };
        });

        // A library file read: its symbols, and for each whether the project has one of that name and whether it is the same.
        r.Query("symbols.readPack", (q, a) =>
        {
            var read = Read((string?)a["text"] ?? "");
            var m = q.Model;
            return new JsonObject
            {
                ["symbols"] = new JsonArray(read.Select(s =>
                {
                    var mine = m.OfClass(Class).FirstOrDefault(e => e.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
                    var mineDef = mine != null ? SymbolDef.From(mine.Attrs["def"]) : null;
                    return (JsonNode)new JsonObject
                    {
                        ["name"] = s.Name, ["category"] = s.Def.Category, ["scale"] = s.Def.Scale, ["exists"] = mine != null,
                        ["same"] = mineDef != null && Same(mineDef, s.Def), ["preview"] = PreviewJson(m, s.Def),
                    };
                }).ToArray()),
            };
        });

        // A library file's symbols brought into the project, one undo step. One the project has of that name and the same is
        // kept; one that differs takes the choice given for its name, else `all`, else keep both ("both": added as „Name 2");
        // "update" replaces the project's (its instances draw the new one, ids kept), "skip" leaves it out. `names`: only those.
        r.Command("symbols.importFrom", "Import symbols", (ctx, a) =>
        {
            var read = Read((string?)a["text"] ?? "");
            var only = a["names"] is JsonArray na ? na.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
            var choices = a["choices"] as JsonObject;
            var all = (string?)a["all"];
            int added = 0, updated = 0, kept = 0, skipped = 0;
            var ids = new JsonArray();
            foreach (var (name, def) in read)
            {
                if (only != null && !only.Contains(name)) continue;
                var mine = ctx.Model.OfClass(Class).FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (mine != null && SymbolDef.From(mine.Attrs["def"]) is { } md && Same(md, def)) { kept++; ids.Add((JsonNode)mine.Id); continue; }
                var choice = mine == null ? "both" : (string?)choices?[name] ?? all ?? "both";
                switch (choice)
                {
                    case "skip": skipped++; continue;
                    case "update" when mine != null:
                    {
                        var attrs = mine.CloneAttrs();
                        attrs["def"] = def.ToJson();
                        ctx.Replace(mine.With(attrs: attrs));
                        updated++;
                        ids.Add((JsonNode)mine.Id);
                        break;
                    }
                    default:
                        ids.Add((JsonNode)Create(ctx, name, def).Id);
                        added++;
                        break;
                }
            }
            return new JsonObject { ["added"] = added, ["updated"] = updated, ["kept"] = kept, ["skipped"] = skipped, ["ids"] = ids };
        });
    }
}
