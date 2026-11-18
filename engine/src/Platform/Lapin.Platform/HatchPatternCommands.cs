using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Drawing.Hatch;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Hatch patterns made without writing <c>.pat</c> text (stage 7 of docs/plan-material-graphics.md): a pattern of line families
/// from its numbers (<c>hatch.create</c>, <c>hatch.update</c>), or a tile drawn in a cell (<c>drafting.toPattern</c>, the drafting
/// module), kept with the project's patterns (<c>hatchPatterns</c> on the project, <see cref="Pat"/> text). Only the project's
/// own patterns change: the built-in ones are fixed, and a new pattern cannot take a built-in's name. A pattern renamed is
/// renamed wherever it is named (materials, their steps and backgrounds, standards, material graphics sets, Visibility / Graphics
/// rows and filters, drafted hatches, symbols); a pattern in use is deleted only when asked to (<c>force</c>), and then what named
/// it draws no hatch (materials, standards, sets, rows) or, a drafted hatch or a symbol's, the built-in 45° lines. Each command
/// is one undo step.
/// </summary>
public static class HatchPatternCommands
{
    /// <summary>What a drafted hatch or a symbol's hatch takes when its pattern is deleted (a drafted hatch always has one).</summary>
    public const string DraftedFallback = "LINES45";

    const int MaxFamilies = 64, MaxDashes = 24, MaxPieces = 2000;

    static Entity Project(ICommandContext ctx) => Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");

    /// <summary>A pattern's name: one word (letters, digits, _ and -), upper case as <c>.pat</c> keeps it.</summary>
    public static string CheckName(string? name)
    {
        var n = name?.Trim().ToUpperInvariant() ?? "";
        if (!Regex.IsMatch(n, "^[A-Z0-9][A-Z0-9_-]{0,47}$")) throw new CommandException("A pattern's name is one word of letters, digits, _ and - (48 at most).");
        return n;
    }

    static bool IsBuiltIn(string name) => HatchLibrary.BuiltInPatterns.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    static double Num(JsonNode? n, string what, double? fallback = null) =>
        Json.Number(n) is { } v && double.IsFinite(v) ? v : n is null && fallback is { } f ? f : throw new CommandException($"{what} is a number.");

    static Vec2 Point(JsonNode? n, string what, Vec2? fallback = null) =>
        n is JsonArray a && a.Count == 2 ? new Vec2(Num(a[0], what), Num(a[1], what)) : n is null && fallback is { } f ? f : throw new CommandException($"{what} is [x, y].");

    /// <summary>
    /// A pattern as a command gives it, checked: <c>{units: paper | model, families: [{angle, origin: [x, y], deltaX, deltaY,
    /// dashes: [..]}]}</c> (millimetres; dashes positive drawn, negative gaps, 0 a dot; none: continuous), or <c>{units, tile:
    /// {width, height, lines: [[x1, y1, x2, y2]], arcs: [{c: [x, y], r, start, sweep}]}}</c> (degrees; sweep 360 a circle).
    /// </summary>
    public static HatchPattern Definition(JsonObject a, string name, string description)
    {
        var units = a["units"] is null ? HatchUnits.Paper : HatchScale.Parse((string?)a["units"]) ?? throw new CommandException("A pattern's units are \"paper\" or \"model\".");
        if (a["tile"] is JsonObject t)
        {
            var (w, h) = (Num(t["width"], "A tile's width"), Num(t["height"], "A tile's height"));
            if (!(w > 0.01 && h > 0.01 && w <= 100000 && h <= 100000)) throw new CommandException("A tile's width and height are over 0.01 mm.");
            var lines = (t["lines"] as JsonArray ?? []).Select(x => x is JsonArray l && l.Count == 4
                ? new TileLine(new Vec2(Num(l[0], "A tile's line"), Num(l[1], "A tile's line")), new Vec2(Num(l[2], "A tile's line"), Num(l[3], "A tile's line")))
                : throw new CommandException("A tile's line is [x1, y1, x2, y2].")).ToList();
            var arcs = (t["arcs"] as JsonArray ?? []).Select(x => x is JsonObject ar
                ? new TileArc(Point(ar["c"], "An arc's centre"), Num(ar["r"], "An arc's radius"), Num(ar["start"], "An arc's start", 0), Num(ar["sweep"], "An arc's sweep", 360))
                : throw new CommandException("A tile's arc is {c: [x, y], r, start, sweep}.")).ToList();
            if (arcs.Any(x => !(x.R > 0) || x.Sweep == 0)) throw new CommandException("An arc has a radius over 0 and a sweep.");
            if (lines.Count + arcs.Count == 0) throw new CommandException("A tile has a line, a dot or an arc.");
            if (lines.Count + arcs.Count > MaxPieces) throw new CommandException($"A tile has at most {MaxPieces} pieces.");
            return new HatchPattern(name, description, [], units) { Tile = new HatchTile(w, h, lines, arcs) };
        }
        if (a["families"] is not JsonArray fs || fs.Count == 0) throw new CommandException("A pattern has line families (families: [{angle, origin, deltaX, deltaY, dashes}]) or a tile.");
        if (fs.Count > MaxFamilies) throw new CommandException($"A pattern has at most {MaxFamilies} line families.");
        var families = new List<HatchLineFamily>();
        foreach (var x in fs)
        {
            if (x is not JsonObject f) throw new CommandException("A line family is {angle, origin: [x, y], deltaX, deltaY, dashes}.");
            var dy = Num(f["deltaY"], "A family's deltaY (the spacing between its lines)");
            if (!(Math.Abs(dy) >= 0.01)) throw new CommandException("A family's lines lie at least 0.01 mm apart (deltaY).");
            var dashes = (f["dashes"] as JsonArray ?? []).Select(d => Num(d, "A dash")).ToList();
            if (dashes.Count > MaxDashes) throw new CommandException($"A family has at most {MaxDashes} dashes.");
            if (dashes.Count > 0 && !(dashes.Sum(Math.Abs) > 0.01)) throw new CommandException("A family's dashes and gaps add up to more than 0.01 mm.");
            families.Add(new HatchLineFamily(Num(f["angle"], "A family's angle", 0), Point(f["origin"], "A family's origin", new Vec2(0, 0)), Num(f["deltaX"], "A family's deltaX", 0), dy, dashes));
        }
        return new HatchPattern(name, description, families, units);
    }

    /// <summary>A pattern as the protocol shows it: its kind (solid, lines, tile, batt), units, families or tile.</summary>
    public static JsonObject ToJson(HatchPattern p, bool project)
    {
        static JsonArray P(Vec2 v) => new(Math.Round(v.X, 6), Math.Round(v.Y, 6));
        var o = new JsonObject
        {
            ["name"] = p.Name, ["description"] = p.Description, ["units"] = HatchScale.Name(p.Units), ["project"] = project,
            ["kind"] = BattHatch.Is(p) ? "batt" : p.Tile != null ? "tile" : p.Solid ? "solid" : "lines",
        };
        if (p.Families.Count > 0)
            o["families"] = new JsonArray(p.Families.Select(f => (JsonNode)new JsonObject
            {
                ["angle"] = f.AngleDeg, ["origin"] = P(f.Origin), ["deltaX"] = f.DeltaX, ["deltaY"] = f.DeltaY,
                ["dashes"] = new JsonArray(f.Dashes.Select(d => (JsonNode)d).ToArray()),
            }).ToArray());
        if (p.Tile is { } t)
            o["tile"] = new JsonObject
            {
                ["width"] = t.Width, ["height"] = t.Height,
                ["lines"] = new JsonArray(t.Lines.Select(l => (JsonNode)new JsonArray(Math.Round(l.A.X, 6), Math.Round(l.A.Y, 6), Math.Round(l.B.X, 6), Math.Round(l.B.Y, 6))).ToArray()),
                ["arcs"] = new JsonArray(t.Arcs.Select(a => (JsonNode)new JsonObject { ["c"] = P(a.C), ["r"] = a.R, ["start"] = a.Start, ["sweep"] = a.Sweep }).ToArray()),
            };
        return o;
    }

    /// <summary>The project's patterns written back (none left: the attribute goes).</summary>
    public static void Save(ICommandContext ctx, IReadOnlyList<HatchPattern> patterns)
    {
        var project = Project(ctx);
        var attrs = project.CloneAttrs();
        if (patterns.Count > 0) attrs["hatchPatterns"] = Pat.Serialize(patterns); else attrs.Remove("hatchPatterns");
        ctx.Replace(project.With(attrs: attrs));
    }

    /// <summary>A new project pattern added (its name free, and not a built-in's); its name.</summary>
    public static string Add(ICommandContext ctx, HatchPattern p)
    {
        if (IsBuiltIn(p.Name)) throw new CommandException($"„{p.Name}” is a built-in pattern's name.");
        var mine = DraftingResources.Patterns(ctx.Model).ToList();
        if (mine.Any(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))) throw new CommandException($"There is a pattern „{p.Name}” already.");
        mine.Add(p);
        Save(ctx, mine);
        return p.Name;
    }

    /// <summary>
    /// A pattern's name changed (<paramref name="to"/>) or gone (null) wherever it is named, in every entity's attributes: a
    /// material's or a graphics entry's <c>hatch</c> (gone: none), a row's <c>cutHatch</c> or <c>surfaceHatch</c> (gone: "none"),
    /// a drafted or a symbol's hatch's <c>pattern</c> (gone: <see cref="DraftedFallback"/>). How many places named it.
    /// </summary>
    static int Repoint(ICommandContext ctx, string from, string? to)
    {
        var n = 0;
        foreach (var e in ctx.Model.Entities.ToList())
        {
            var attrs = e.CloneAttrs();
            var k = Walk(attrs, null, from, to, write: true);
            if (k == 0) continue;
            n += k;
            ctx.Replace(ctx.Model.Get(e.Id)!.With(attrs: attrs));
        }
        return n;
    }

    /// <summary>How many places in the model name a pattern.</summary>
    public static int Usage(IModelView m, string name) => m.Entities.Sum(e => Walk(e.Attrs, null, name, null, write: false));

    static int Walk(JsonNode? node, string? parentKey, string from, string? to, bool write)
    {
        var n = 0;
        switch (node)
        {
            case JsonObject o:
                foreach (var k in o.Select(kv => kv.Key).ToList())
                {
                    var v = o[k];
                    var named = v is JsonValue && (k is "hatch" or "cutHatch" or "surfaceHatch" || k == "pattern" && parentKey == "hatch")
                        && string.Equals((string?)v, from, StringComparison.OrdinalIgnoreCase);
                    if (named)
                    {
                        n++;
                        if (write) o[k] = to ?? (k switch { "hatch" => null, "pattern" => DraftedFallback, _ => "none" });
                    }
                    else if (v != null) n += Walk(v, k, from, to, write);
                }
                break;
            case JsonArray a:
                foreach (var x in a) if (x != null) n += Walk(x, parentKey, from, to, write);
                break;
        }
        return n;
    }

    public static void Register(IModuleRegistry r)
    {
        // A pattern's definition (the editor's): {name} → {name, description, units, kind, project, families?, tile?}.
        r.Query("hatch.get", (q, a) =>
        {
            var name = (string?)a["name"] ?? "";
            var own = DraftingResources.Patterns(q.Model).FirstOrDefault(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
            var p = own ?? HatchLibrary.Find(name) ?? throw new CommandException($"There is no hatch pattern „{name}”.");
            var o = ToJson(p, own != null);
            o["uses"] = Usage(q.Model, p.Name);
            return o;
        });

        // How many places name a pattern (before deleting it).
        r.Query("hatch.usage", (q, a) => new JsonObject { ["count"] = Usage(q.Model, (string?)a["name"] ?? "") });

        // A new project pattern from its numbers: {name, description?, units?, families | tile} → {name}.
        r.Command("hatch.create", "New hatch pattern", (ctx, a) =>
        {
            var name = CheckName((string?)a["name"]);
            var p = Definition(a, name, ((string?)a["description"])?.Trim() ?? "");
            return new JsonObject { ["name"] = Add(ctx, p) };
        });

        // A project pattern changed: {name, newName?, description?, units?, families? | tile?}; a new name is followed everywhere.
        r.Command("hatch.update", "Change hatch pattern", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim() ?? "";
            var mine = DraftingResources.Patterns(ctx.Model).ToList();
            var i = mine.FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (i < 0) throw new CommandException(IsBuiltIn(name) ? $"„{name}” is a built-in pattern: duplicate it to change it." : $"There is no project pattern „{name}”.");
            var had = mine[i];
            var newName = a["newName"] is null ? had.Name : CheckName((string?)a["newName"]);
            if (!newName.Equals(had.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (IsBuiltIn(newName)) throw new CommandException($"„{newName}” is a built-in pattern's name.");
                if (mine.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))) throw new CommandException($"There is a pattern „{newName}” already.");
            }
            var description = a["description"] is null ? had.Description : ((string?)a["description"])?.Trim() ?? "";
            HatchPattern next;
            if (a["families"] != null || a["tile"] != null)
            {
                var def = a.DeepClone().AsObject();
                if (def["units"] is null) def["units"] = HatchScale.Name(had.Units);
                next = Definition(def, newName, description);
            }
            else next = (had with { Name = newName, Description = description, Units = a["units"] is null ? had.Units : HatchScale.Parse((string?)a["units"]) ?? throw new CommandException("A pattern's units are \"paper\" or \"model\".") }) with { Tile = had.Tile };
            mine[i] = next;
            Save(ctx, mine);
            var repointed = next.Name != had.Name ? Repoint(ctx, had.Name, next.Name) : 0;
            return new JsonObject { ["name"] = next.Name, ["repointed"] = repointed };
        });

        // A project pattern deleted: {name, force?}. In use, only with force: what named it then draws none (a drafted hatch the 45° lines).
        r.Command("hatch.delete", "Delete hatch pattern", (ctx, a) =>
        {
            var name = ((string?)a["name"])?.Trim() ?? "";
            var mine = DraftingResources.Patterns(ctx.Model).ToList();
            var i = mine.FindIndex(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (i < 0) throw new CommandException(IsBuiltIn(name) ? $"„{name}” is a built-in pattern: it cannot be deleted." : $"There is no project pattern „{name}”.");
            var had = mine[i];
            mine.RemoveAt(i);
            // A built-in of the same name (an imported pattern overriding it) takes over: nothing to repoint.
            var uses = IsBuiltIn(had.Name) ? 0 : Usage(ctx.Model, had.Name);
            var force = a["force"] is JsonValue fv && fv.TryGetValue<bool>(out var fb) && fb;
            if (uses > 0 && !force)
                throw new CommandException($"„{had.Name}” is used in {uses} place{(uses == 1 ? "" : "s")}: delete it with force to draw those without it.");
            Save(ctx, mine);
            return new JsonObject { ["repointed"] = uses > 0 ? Repoint(ctx, had.Name, null) : 0 };
        });

        // The project's patterns (or those named) as a .pat for another program: {names?} → {text, skipped: [tile patterns left out]}.
        r.Query("hatch.exportPat", (q, a) =>
        {
            var names = (a["names"] as JsonArray)?.Select(x => (string?)x).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var (text, skipped) = Pat.External(DraftingResources.Patterns(q.Model).Where(p => names == null || names.Contains(p.Name)));
            return new JsonObject { ["text"] = text, ["skipped"] = new JsonArray(skipped.Select(x => (JsonNode)x).ToArray()) };
        });
    }
}
