using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture.Stairs;

/// <summary>
/// The space under one flight of a stair: its frame (<c>s</c> along the flight from its first riser, <c>d</c> in from the
/// open side's face, <c>z</c> up from the stair's foot), the soffit over it (the waist's underside, the treads' or the
/// stringers' for open stairs) as a polyline in <c>(s, z)</c>, and how far it runs.
/// </summary>
public sealed record UnderSpace(StairPlacement Place, Flight Flight, int Index, int Sign, double Width, double Depth, double S0, double S1, double Run, IReadOnlyList<(double S, double Z)> Soffit, Resolved Stair)
{
    /// <summary>The soffit's height over the floor at <paramref name="s"/>, its end pieces carried on past its ends.</summary>
    public double Top(double s)
    {
        var p = Soffit;
        if (p.Count == 1) return p[0].Z;
        var i = 0;
        while (i < p.Count - 2 && s > p[i + 1].S) i++;
        var (a, b) = (p[i], p[i + 1]);
        return Math.Abs(b.S - a.S) < 1e-12 ? Math.Min(a.Z, b.Z) : a.Z + (s - a.S) * (b.Z - a.Z) / (b.S - a.S);
    }

    /// <summary>The first <c>s</c> from which the soffit stands at least <paramref name="height"/> over the floor (the soffit only climbs); null when it never does.</summary>
    public double? Where(double height, double from, double to)
    {
        if (Top(from) >= height - 1e-9) return from;
        if (Top(to) < height - 1e-9) return null;
        double lo = from, hi = to;
        for (var k = 0; k < 60; k++) { var mid = (lo + hi) / 2; if (Top(mid) >= height) hi = mid; else lo = mid; }
        return hi;
    }

    double T(double d) => Sign * Width / 2 - Sign * d;
    public Vec3 World(double s, double d, double z) => Place.World(Flight.Start + Flight.Dir * s + Flight.Left * T(d), z);
    public Vec2 Plan(double s, double d) => Place.World(Flight.Start + Flight.Dir * s + Flight.Left * T(d));
}

/// <summary>
/// The space under a stair as an element (a tester's report, #71): <c>LapinUnderStair</c>, hosted by a straight-flighted stair
/// (<c>host</c>), filling the space under one of its flights (<c>flight</c>, from 1) between the floor and the soffit, from its
/// open side (<c>side</c>: auto, left or right as one climbs) in as deep as the stair is wide (<c>depth</c>), from where the
/// soffit is 300 mm over the floor (<c>start</c>, from the first riser) to the flight's last riser (<c>end</c>). What fills it is
/// its <see cref="UnderStairSpec"/> (<c>spec</c>, from a ready-made style or the project's own, <c>style</c>), made of simple
/// boards and boxes laid out to the space each time it is built, so it follows the stair when the stair changes; a part that
/// does not fit under the slope is cut by it or left out (and said). Exported as an IfcElementAssembly of IfcFurniture,
/// IfcDoor, IfcPlate, IfcDiscreteAccessory and IfcLightFixture, connected to its stair.
/// </summary>
public static class UnderStairs
{
    public const string Class = "LapinUnderStair";
    public const string StylesKey = "underStairStyles";
    public const string OwnPrefix = "own:";
    /// <summary>The gap left under the soffit (a board never touches the concrete).</summary>
    const double Clearance = 0.01;
    /// <summary>How low the space is used at its low end, by default.</summary>
    const double LowEnd = 0.3;

    public static void Register(IModuleRegistry r)
    {
        r.Class(new(Class, "Under-stair space",
        [
            new("side", "Open side", "choice", Choices: ["auto", "left", "right"]),
            new("flight", "Under flight", "number", Min: 1, Max: 3),
            new("start", "Starts at", "length", Min: 0, Max: 20),
            new("end", "Ends at", "length", Min: 0, Max: 20),
            new("depth", "Depth", "length", Min: 0.2, Max: 5),
        ], "#c99a5b"));
        r.Geometry(Class, (e, m, _) => Geometry(e, m));
        r.Quantities(Class, Quantities);
        r.Service<IScheduleSource>(new UnderStairSchedule());
        r.Catalog("underStair", new JsonObject
        {
            ["presets"] = new JsonArray(UnderStairSpec.Presets.Select(p => (JsonNode)new JsonObject { ["id"] = p.Id, ["label"] = p.Spec.Name, ["description"] = p.Description, ["spec"] = p.Spec.ToJson() }).ToArray()),
            ["fronts"] = new JsonArray(UnderStairSpec.FrontKinds.Select(x => (JsonNode)x).ToArray()),
            ["features"] = new JsonArray(UnderStairSpec.Features.Select(x => (JsonNode)x).ToArray()),
        });

        // {stairId, style? | spec?, side?, flight?, start?, end?, depth?}: the space under the stair, furnished.
        r.Command("addUnderStair", "Space under the stair", (ctx, a) =>
        {
            var stair = RequireStair(ctx.Model, (string?)a["stairId"]);
            var (spec, style) = SpecFrom(ctx.Model, a) ?? (UnderStairSpec.Default(), UnderStairSpec.Presets[0].Id);
            EnsureMaterials(ctx, spec);
            var attrs = new JsonObject
            {
                ["host"] = stair.Id, ["hostRelation"] = "Under stair", ["spec"] = spec.ToJson(),
                ["exportAs"] = "IfcElementAssembly", ["objectType"] = "Under-stair space",
            };
            if (style != null) attrs["style"] = style;
            Placement(a, attrs);
            var e = new Entity(ctx.NewId(), Class, $"{spec.Name} {Numbering.Next(ctx.Model.OfClass(Class))}", attrs, "USERDEFINED");
            if (Space(ctx.Model, stair, e) is { Problem: { } problem }) throw new CommandException(problem);
            ctx.Add(e);
            if (Spatial.StoreyOf(ctx.Model, stair.Id) is { } storey) Spatial.Contain(ctx, storey.Id, e.Id);
            return new JsonObject { ["id"] = e.Id };
        });

        // {id, style? | spec?, side?, flight?, start?, end?, depth?}: what fills it and where, as the configurator applies it (null frees a placement field).
        r.Command("updateUnderStair", "Change the space under the stair", (ctx, a) =>
        {
            var e = Require(ctx.Model, (string?)a["id"]);
            var attrs = e.CloneAttrs();
            var name = e.Name;
            if (SpecFrom(ctx.Model, a) is var (spec, style))
            {
                EnsureMaterials(ctx, spec);
                if (SpecOf(e) is { } before && e.Name.StartsWith(before.Name + " ", StringComparison.Ordinal)) name = spec.Name + e.Name[before.Name.Length..];
                attrs["spec"] = spec.ToJson();
                if (style != null) attrs["style"] = style; else attrs.Remove("style");
            }
            Placement(a, attrs);
            var next = e.With(name: name, attrs: attrs);
            var stair = RequireStair(ctx.Model, e.Str("host"));
            if (Space(ctx.Model, stair, next) is { Problem: { } problem }) throw new CommandException(problem);
            ctx.Replace(next);
            return null;
        });

        // The configurator's preview: {stairId | id, style? | spec?, side?…}: the elevation from the open side and the plan, what fits and what not.
        r.Query("underStairPreview", (q, a) =>
        {
            var e = (string?)a["id"] is { } id ? Require(q.Model, id) : null;
            var stair = RequireStair(q.Model, (string?)a["stairId"] ?? e?.Str("host"));
            var attrs = e?.CloneAttrs() ?? new JsonObject();
            var (spec, _) = SpecFrom(q.Model, a) ?? (e != null ? (SpecOf(e) ?? UnderStairSpec.Default(), null) : (UnderStairSpec.Default(), null));
            attrs["spec"] = spec.ToJson();
            Placement(a, attrs);
            var draft = new Entity(e?.Id ?? "draft", Class, e?.Name ?? spec.Name, attrs);
            return Preview(q.Model, stair, draft, spec);
        });

        // The project's own styles: what fills the space saved under a name (not where it stands); saved again, every space of the style follows.
        r.Command("saveUnderStairStyle", "Save under-stair style", (ctx, a) =>
        {
            var spec = UnderStairSpec.From(a["spec"]) ?? throw new CommandException("The under-stair style cannot be read.");
            var label = ((string?)a["name"] ?? "").Trim();
            if (label.Length == 0) throw new CommandException("An under-stair style needs a name.");
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var list = Styles(ctx.Model);
            var given = (string?)a["styleId"];
            var same = list.FirstOrDefault(s => given != null ? (string?)s["id"] == given : string.Equals((string?)s["label"], label, StringComparison.OrdinalIgnoreCase));
            if (given != null && same == null) throw new CommandException("The project has no such under-stair style.");
            var sid = (string?)same?["id"] ?? OwnPrefix + ctx.NewId();
            spec = spec with { Name = label };
            var style = new JsonObject { ["id"] = sid, ["label"] = label, ["spec"] = spec.ToJson() };
            var next = list.Select(s => (string?)s["id"] == sid ? style : s.DeepClone()).ToList();
            if (same == null) next.Add(style);
            var pa = project.CloneAttrs();
            pa[StylesKey] = new JsonArray([.. next]);
            ctx.Replace(project.With(attrs: pa));
            EnsureMaterials(ctx, spec);
            // The space it was saved from takes it, and every space of the style takes it again.
            var from = (string?)a["id"];
            foreach (var e in ctx.Model.OfClass(Class).Where(x => x.Id == from || x.Str("style") == sid).ToList())
            {
                var attrs = e.CloneAttrs();
                attrs["spec"] = spec.ToJson();
                attrs["style"] = sid;
                ctx.Replace(e.With(attrs: attrs));
            }
            return new JsonObject { ["styleId"] = sid };
        });
        r.Command("renameUnderStairStyle", "Rename under-stair style", (ctx, a) =>
        {
            var label = ((string?)a["name"] ?? "").Trim();
            if (label.Length == 0) throw new CommandException("An under-stair style needs a name.");
            var id = (string?)a["styleId"];
            EditStyles(ctx, list =>
            {
                var s = list.FirstOrDefault(x => (string?)x["id"] == id) ?? throw new CommandException("The project has no such under-stair style.");
                s["label"] = label;
                if (UnderStairSpec.From(s["spec"]) is { } spec) s["spec"] = (spec with { Name = label }).ToJson();
                return list;
            });
            return null;
        });
        // Deleted, its spaces keep what fills them, no longer of a style.
        r.Command("deleteUnderStairStyle", "Delete under-stair style", (ctx, a) =>
        {
            var id = (string?)a["styleId"];
            EditStyles(ctx, list => list.Any(x => (string?)x["id"] == id) ? [.. list.Where(x => (string?)x["id"] != id)] : throw new CommandException("The project has no such under-stair style."));
            foreach (var e in ctx.Model.OfClass(Class).Where(x => x.Str("style") == id).ToList())
            {
                var attrs = e.CloneAttrs();
                attrs.Remove("style");
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });
    }

    static readonly string[] PlacementKeys = ["side", "flight", "start", "end", "depth"];

    /// <summary>The placement fields a command gives (null takes one away).</summary>
    static void Placement(JsonObject a, JsonObject attrs)
    {
        foreach (var k in PlacementKeys)
        {
            if (!a.ContainsKey(k)) continue;
            if (k == "side") { if ((string?)a[k] is "left" or "right") attrs[k] = (string?)a[k]; else attrs.Remove(k); }
            else if (Json.Number(a[k]) is { } v) attrs[k] = v; else attrs.Remove(k);
        }
    }

    /// <summary>The spec a command names: a style (ready-made or the project's), else a spec given; null when it gives neither.</summary>
    static (UnderStairSpec Spec, string? Style)? SpecFrom(IModelView m, JsonObject a)
    {
        if ((string?)a["style"] is { } style)
        {
            var spec = UnderStairSpec.Preset(style) ?? (Styles(m).FirstOrDefault(s => (string?)s["id"] == style) is { } own ? UnderStairSpec.From(own["spec"]) : null)
                ?? throw new CommandException($"There is no under-stair style „{style}”.");
            // A style changed by hand in the configurator: the changes on top of it.
            if (a["spec"] is JsonObject over && UnderStairSpec.From(Merge(spec.ToJson(), over)) is { } changed) return (changed, changed == spec ? style : null);
            return (spec, style);
        }
        if (a["spec"] is JsonObject given) return (UnderStairSpec.From(given) ?? throw new CommandException("The under-stair space's specification cannot be read."), null);
        return null;
    }

    static JsonObject Merge(JsonObject a, JsonObject b)
    {
        foreach (var (k, v) in b) a[k] = v?.DeepClone();
        return a;
    }

    public static List<JsonObject> Styles(IModelView m) => (Spatial.Project(m)?.Attrs[StylesKey] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];

    static void EditStyles(ICommandContext ctx, Func<List<JsonObject>, List<JsonObject>> edit)
    {
        var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
        var list = edit(Styles(ctx.Model).Select(s => (JsonObject)s.DeepClone()).ToList());
        var pa = project.CloneAttrs();
        if (list.Count > 0) pa[StylesKey] = new JsonArray([.. list]); else pa.Remove(StylesKey);
        ctx.Replace(project.With(attrs: pa));
    }

    static void EnsureMaterials(ICommandContext ctx, UnderStairSpec spec)
    {
        foreach (var name in spec.Materials())
            Materials.Ensure(ctx, name, UnderStairSpec.Colors.TryGetValue(name, out var c) ? c : null);
    }

    public static UnderStairSpec? SpecOf(Entity e) => UnderStairSpec.From(e.Attrs["spec"]);

    static Entity Require(IModelView m, string? id) =>
        m.Get(id ?? "") is { Class: Class } e ? e : throw new CommandException($"No under-stair space „{id}”.");

    static Entity RequireStair(IModelView m, string? id) => m.Get(id ?? "") switch
    {
        { Class: IfcClass.Stair } s => s,
        { Class: IfcClass.Ramp } => throw new CommandException("A ramp has no space under it to furnish: choose a stair."),
        _ => throw new CommandException($"No stair „{id}”."),
    };

    /// <summary>The spaces under a stair.</summary>
    public static IEnumerable<Entity> Of(IModelView m, string stairId) => m.OfClass(Class).Where(e => e.Str("host") == stairId);

    // ─── The space ──────────────────────────────────────────────────────────

    /// <summary>The space under the stair as the element's placement asks for it, or why there is none.</summary>
    public static (UnderSpace? Space, string? Problem) Space(IModelView m, Entity stair, Entity e)
    {
        if (StairBuilder.Resolve(m, stair) is not var (r, place, spec)) return (null, "The stair stands on no storey.");
        if (r.Arc != null || r.Flights.Count == 0) return (null, "Only a stair with straight flights has a space under it to furnish.");
        if (spec.Construction.Kind == "solid") return (null, "The stair is solid down to the floor: there is no space under it.");
        var index = Math.Clamp((int)Math.Round(e.NumOrNull("flight") ?? 1) - 1, 0, r.Flights.Count - 1);
        var fl = r.Flights[index];
        var (k, h, b) = (fl.Risers, r.RiserHeight, r.Tread);
        if (k < 2) return (null, "The flight has no treads to go under.");
        var run = (k - 1) * b;
        var f = spec.Finish.TreadThickness;
        var waist = spec.Construction.Waist;
        List<(double S, double Z)> soffit;
        switch (spec.Construction.Kind)
        {
            case "treads" or "stringers":
            {
                // Under the open treads, the line through each tread's back underside, every tread above it.
                double Tread(double s) => h * s / b - f - waist;
                if (spec.Construction.Kind == "treads") { soffit = [(0, Tread(0)), (run, Tread(run))]; break; }
                // Stringers: their underside too, whichever is lower (both straight, so the line through the lower ends lies under both).
                var drop = 0.25 * Math.Sqrt(h * h + b * b) / b;
                var (s1, z1, s2, z2) = (drop * b / h - b * 0.5, 0.0, run, k * h - f - drop);
                double Stringer(double s) => z1 + (s - s1) * (z2 - z1) / (s2 - s1);
                soffit = [(0, Math.Min(Tread(0), Stringer(0))), (run, Math.Min(Tread(run), Stringer(run)))];
                break;
            }
            default:
            {
                // The waist's underside, as the flight's solid has it: level where it bears, then up to the top riser's.
                var wv = waist * Math.Sqrt(h * h + b * b) / b;
                var uA = Math.Min(wv * b / h, Math.Max(run - 0.01, 0));
                soffit = [(0, -f), (uA, -f), (run, (k - 1) * h - wv - f)];
                break;
            }
        }
        soffit = soffit.Select(p => (p.S, p.Z + fl.Z0 - Clearance)).ToList();
        var sign = e.Str("side") switch { "left" => 1, "right" => -1, _ => place.TurnLeft ? -1 : 1 };
        // A turning stair's first flight: open on the side away from the turn, the other flight beside it on the turn's.
        if (e.Str("side") is not ("left" or "right") && r.Flights.Count == 1) sign = -1;
        var width = r.WidthOf(fl);
        var depth = Math.Clamp(e.NumOrNull("depth") ?? width, 0.2, width);
        var space = new UnderSpace(place, fl, index, sign, width, depth, 0, run, run, soffit, r);
        var s0 = e.NumOrNull("start") is { } st ? Math.Clamp(st, 0, run) : space.Where(LowEnd, 0, run) ?? run;
        var s1End = e.NumOrNull("end") is { } en ? Math.Clamp(en, 0, run) : run;
        if (s1End - s0 < 0.3) return (null, "The space under the flight is too low or too short to furnish (less than 300 mm).");
        return (space with { S0 = s0, S1 = s1End }, null);
    }

    // ─── What fills it ──────────────────────────────────────────────────────

    /// <summary>A part of the furnishing: its pieces, each a polygon in (s, z) laid across the depth from d0 to d1.</summary>
    public sealed class Part(string key, string name, string role)
    {
        public string Key { get; } = key;
        public string Name { get; } = name;
        /// <summary>carcass, front, fabric, metal or light: which material or colour it takes.</summary>
        public string Role { get; } = role;
        public List<(List<(double S, double Z)> Poly, double D0, double D1)> Pieces { get; } = [];
        public int Count { get; set; }
    }

    /// <summary>What the space is filled with, laid out to it: the parts and the notes on what did not fit.</summary>
    public sealed record Layout(UnderSpace Space, UnderStairSpec Spec, List<Part> Parts, List<string> Notes);

    sealed class Builder(UnderSpace sp)
    {
        public readonly Dictionary<string, Part> Parts = [];

        public Part PartOf(string key, string name, string role) => Parts.TryGetValue(key, out var p) ? p : Parts[key] = new Part(key, name, role);

        /// <summary>A box, cut by the soffit (less <paramref name="gap"/>): counted as one piece when anything of it is left.</summary>
        public bool Box(string key, string name, string role, double s0, double s1, double d0, double d1, double z0, double z1, double gap = 0, double minLength = 0, bool count = true)
        {
            if (s1 - s0 < 1e-4 || d1 - d0 < 1e-4 || z1 - z0 < 1e-4) return false;
            var pieces = Clip([(s0, z0), (s1, z0), (s1, z1), (s0, z1)], gap);
            if (minLength > 0) pieces = pieces.Where(p => p.Max(q => q.S) - p.Min(q => q.S) >= minLength).ToList();
            if (pieces.Count == 0) return false;
            var part = PartOf(key, name, role);
            foreach (var p in pieces) part.Pieces.Add((p, d0, d1));
            if (count) part.Count++;
            return true;
        }

        /// <summary>A polygon cut by the soffit: split where the soffit bends, each piece under its straight stretch.</summary>
        List<List<(double S, double Z)>> Clip(List<(double S, double Z)> poly, double gap)
        {
            var o = new List<List<(double, double)>>();
            var rest = poly;
            var breaks = sp.Soffit.Skip(1).SkipLast(1).Select(p => p.S).ToList();
            var pieces = new List<List<(double S, double Z)>>();
            foreach (var x in breaks)
            {
                pieces.Add(Half(rest, p => x - p.S));
                rest = Half(rest, p => p.S - x);
            }
            pieces.Add(rest);
            foreach (var piece in pieces)
            {
                if (piece.Count < 3) continue;
                var mid = piece.Average(p => p.S);
                var i = 0;
                while (i < sp.Soffit.Count - 2 && mid > sp.Soffit[i + 1].S) i++;
                var (a, b) = (sp.Soffit[i], sp.Soffit[Math.Min(i + 1, sp.Soffit.Count - 1)]);
                var slope = Math.Abs(b.S - a.S) < 1e-12 ? 0 : (b.Z - a.Z) / (b.S - a.S);
                var cut = Half(piece, p => a.Z + (p.S - a.S) * slope - gap - p.Z);
                if (cut.Count >= 3 && Math.Abs(Area(cut)) > 1e-7) o.Add(cut);
            }
            return o;
        }

        static double Area(List<(double S, double Z)> p)
        {
            var a = 0.0;
            for (var i = 0; i < p.Count; i++) { var (x0, y0) = p[i]; var (x1, y1) = p[(i + 1) % p.Count]; a += x0 * y1 - x1 * y0; }
            return a / 2;
        }

        /// <summary>The part of a convex polygon where <paramref name="f"/> ≥ 0 (f linear).</summary>
        static List<(double S, double Z)> Half(List<(double S, double Z)> poly, Func<(double S, double Z), double> f)
        {
            var o = new List<(double S, double Z)>();
            for (var i = 0; i < poly.Count; i++)
            {
                var (p, q) = (poly[i], poly[(i + 1) % poly.Count]);
                var (fp, fq) = (f(p), f(q));
                if (fp >= -1e-12) o.Add(p);
                if ((fp >= -1e-12) != (fq >= -1e-12) && Math.Abs(fp - fq) > 1e-15)
                {
                    var t = fp / (fp - fq);
                    o.Add((p.S + (q.S - p.S) * t, p.Z + (q.Z - p.Z) * t));
                }
            }
            return o;
        }
    }

    /// <summary>
    /// The furnishing laid out to the space, from the high end down: an end board at the top, the bookcase, the feature, then the
    /// rest of the length to the low end closed as the front says (cupboard doors with shelves behind, a panel, or open), boards
    /// between them. Each feature takes the length where it has the headroom it needs, else it is left out and said.
    /// </summary>
    public static Layout Lay(UnderSpace sp, UnderStairSpec spec)
    {
        var b = new Builder(sp);
        var notes = new List<string>();
        var (T, D) = (spec.Thickness, sp.Depth);
        var (s0, s1) = (sp.S0, sp.S1);
        const string Panels = "nook-panel";
        void Board(double s) => b.Box(Panels, "Panels", "carcass", s - T / 2, s + T / 2, 0, D, 0, 10);
        var cursor = s1;
        // The high end closed by a board, unless all of it is left open.
        if (spec.Front != "open" || spec.Bookcase || spec.Feature != "none") b.Box(Panels, "Panels", "carcass", s1 - T, s1, 0, D, 0, 10);

        // The bookcase at the high end: open shelves up to the soffit.
        if (spec.Bookcase && cursor - spec.BookcaseWidth >= s0 + 0.2)
        {
            var (ba, bb) = (cursor - spec.BookcaseWidth, cursor - T);
            Board(ba);
            b.Box("nook-bookcase", "Bookcase", "carcass", ba + T / 2, bb, 0.02, D, 0.06, 0.06 + T);
            for (var z = 0.06 + spec.ShelfSpacing; z + T < sp.Top(bb) - 0.12; z += spec.ShelfSpacing)
                b.Box("nook-bookcase", "Bookcase", "carcass", ba + T / 2, bb, 0.02, D, z, z + T, 0.05, 0.15);
            cursor = ba;
        }
        else if (spec.Bookcase) notes.Add("The bookcase does not fit: the space is too short.");

        // The feature, beside it.
        var openings = new List<(double A, double B, double Z0, double Z1)>();
        var frontEnd = cursor;
        var featureStart = cursor;
        var feature = spec.Feature;
        if (feature != "none")
        {
            var fb = cursor - (cursor < s1 - 1e-9 ? T / 2 : T);
            var fa = Math.Max(s0, cursor - spec.FeatureWidth);
            featureStart = fa;
            // The headroom it needs, where the soffit gives it.
            var need = feature switch { "desk" => spec.DeskHeight + 0.5, "bench" => spec.BenchHeight + 0.45, "mudroom" => spec.BenchHeight + 0.4, _ => 0 };
            var from = need > 0 ? sp.Where(need, fa, fb) : fa;
            var label = feature switch { "desk" => "The desk", "bench" => "The bench", "mudroom" => "The mudroom bench", "playhouse" => "The playhouse door", _ => "The dog's opening" };
            if (from is not { } fa2 || fb - fa2 < (feature is "playhouse" or "dogbed" ? spec.OpeningWidth + 0.1 : 0.5))
            {
                notes.Add($"{label} does not fit under the slope here: it is left out.");
                feature = "none";
            }
            else
            {
                fa = fa2;
                switch (feature)
                {
                    case "desk":
                    {
                        var d0 = Math.Max(0.05, D - 0.6);
                        b.Box("nook-desk", "Desk", "carcass", fa, fb, d0, D, spec.DeskHeight - 0.03, spec.DeskHeight);
                        var shelf = spec.DeskHeight + 0.55;
                        var shelved = b.Box("nook-shelves", "Shelves", "carcass", fa, fb, Math.Max(0.05, D - 0.25), D, shelf, shelf + T, 0.05, 0.2);
                        if (spec.Light)
                        {
                            var c = (fa + fb) / 2;
                            var zl = shelved && sp.Top(c) > shelf + T + 0.05 ? shelf : Math.Min(sp.Top(c) - 0.02, 2.2);
                            b.Box("nook-light", "Light", "light", c - 0.2, c + 0.2, Math.Max(0.05, D - 0.24), Math.Max(0.1, D - 0.18), zl - 0.03, zl, 0.01);
                        }
                        break;
                    }
                    case "bench":
                    {
                        var seat = spec.BenchHeight - (spec.Cushion ? 0.08 : 0);
                        b.Box("nook-bench", "Bench", "carcass", fa, fb, T, D, 0, seat);
                        // Two drawers in its front.
                        var half = (fb - fa) / 2;
                        for (var i = 0; i < 2; i++) b.Box("nook-drawers", "Drawers", "front", fa + i * half + 0.003, fa + (i + 1) * half - 0.003, 0, T, 0.05, seat - 0.02);
                        if (spec.Cushion)
                        {
                            b.Box("nook-cushion", "Cushions", "fabric", fa, fb, 0.02, D, seat, spec.BenchHeight);
                            var w = Math.Min(0.5, half - 0.08);
                            for (var i = 0; i < 2 && w > 0.2; i++)
                            {
                                var c = fa + half * (i + 0.5);
                                b.Box("nook-cushion", "Cushions", "fabric", c - w / 2, c + w / 2, D - 0.14, D, spec.BenchHeight, spec.BenchHeight + 0.4, 0.02);
                            }
                        }
                        if (spec.Light) { var c = (fa + fb) / 2 + half / 2; b.Box("nook-light", "Light", "light", c - 0.08, c + 0.08, D - 0.2, D - 0.04, sp.Top(c) - 0.06, sp.Top(c) - 0.01); }
                        break;
                    }
                    case "mudroom":
                    {
                        b.Box("nook-bench", "Bench", "carcass", fa, fb, 0, D, spec.BenchHeight - (spec.Cushion ? 0.05 : 0) - T, spec.BenchHeight - (spec.Cushion ? 0.05 : 0));
                        var under = spec.BenchHeight - (spec.Cushion ? 0.05 : 0) - T;
                        var n = Math.Max(1, (int)Math.Round((fb - fa) / 0.35));
                        for (var i = 0; i <= n; i++)
                        {
                            var x = Math.Clamp(fa + i * (fb - fa) / n, fa + T / 2, fb - T / 2);
                            b.Box("nook-cubbies", "Cubbies", "carcass", x - T / 2, x + T / 2, 0, D, 0, under);
                        }
                        b.Box("nook-cubbies", "Cubbies", "carcass", fa, fb, 0, D, 0, 0.05);
                        if (under > 0.35) b.Box("nook-cubbies", "Cubbies", "carcass", fa, fb, 0, D, under / 2, under / 2 + T);
                        if (spec.Cushion) b.Box("nook-cushion", "Cushions", "fabric", fa, fb, 0.02, D, under + T, spec.BenchHeight);
                        // The coat hooks on a rail on the back, where there is headroom to hang a coat.
                        if (sp.Where(spec.HookHeight + 0.15, fa, fb) is { } ha && fb - ha > spec.HookSpacing)
                        {
                            b.Box(Panels, "Panels", "carcass", ha, fb, D - 0.02, D, spec.HookHeight - 0.06, spec.HookHeight + 0.06);
                            for (var x = ha + spec.HookSpacing / 2; x < fb - 0.05; x += spec.HookSpacing)
                                b.Box("nook-hooks", "Coat hooks", "metal", x - 0.01, x + 0.01, D - 0.1, D - 0.02, spec.HookHeight - 0.03, spec.HookHeight + 0.03);
                        }
                        else notes.Add("The coat hooks do not fit under the slope here: they are left out.");
                        break;
                    }
                    case "playhouse" or "dogbed":
                    {
                        // An opening in the front, near the high side of the feature's length, as tall as the slope lets it be.
                        var ob = fb - 0.1;
                        var oa = Math.Max(fa + 0.05, ob - spec.OpeningWidth);
                        var oh = Math.Min(spec.OpeningHeight, sp.Top(oa) - 0.15);
                        if (oh < 0.3) { notes.Add($"{label} does not fit under the slope here: it is left out."); break; }
                        if (oh < spec.OpeningHeight - 1e-6) notes.Add($"{label} is lowered to {Math.Round(oh * 1000)} mm to fit under the slope.");
                        openings.Add((oa, ob, 0, oh));
                        if (feature == "playhouse")
                        {
                            b.Box("nook-door:play", "Play door", "front", oa + 0.003, ob - 0.003, 0, T, 0.01, oh - 0.003);
                            var zh = Math.Min(0.6, oh / 2);
                            b.Box("nook-handles", "Handles", "metal", ob - 0.07, ob - 0.05, -0.03, 0, zh - 0.04, zh + 0.04);
                            b.Box("nook-cushion", "Play mat", "fabric", fa, fb, T + 0.05, D - 0.02, 0, 0.04);
                            // A window beside the door, where the front stands a metre high.
                            if (spec.Window && sp.Where(1.0, spec.Front == "panel" ? s0 : featureStart, oa - 0.45) is { } wa && wa + 0.35 <= oa - 0.1)
                            {
                                var wc = Math.Max(wa, (wa + oa - 0.1) / 2 - 0.175);
                                openings.Add((wc, wc + 0.35, 0.5, 0.85));
                            }
                            else if (spec.Window) notes.Add("The window does not fit beside the door: it is left out.");
                        }
                        else
                        {
                            // The opening framed on the front, the bed behind it.
                            const double Trim = 0.05;
                            b.Box("nook-trim", "Trim boards", "front", oa - Trim, oa, -0.012, 0, 0, oh + Trim, 0.005);
                            b.Box("nook-trim", "Trim boards", "front", ob, ob + Trim, -0.012, 0, 0, oh + Trim, 0.005);
                            b.Box("nook-trim", "Trim boards", "front", oa - Trim, ob + Trim, -0.012, 0, oh, oh + Trim, 0.005);
                            b.Box("nook-bed", "Dog bed", "fabric", Math.Max(fa, oa - 0.1), Math.Min(fb, ob + 0.1), T + 0.05, Math.Min(D - 0.02, T + 0.05 + 0.8), 0, 0.18);
                        }
                        break;
                    }
                }
                // A desk's, a bench's or a mudroom's length is open; a board closes the rest beside it.
                if (feature is "desk" or "bench" or "mudroom")
                {
                    frontEnd = fa;
                    if (fa > s0 + 0.05) Board(fa - T / 2);
                }
            }
        }

        // The rest, down to the low end: cupboard doors with shelves behind, a panel (round any openings), or left open.
        var restEnd = frontEnd - (feature != "none" ? T : 0);
        if (feature is "playhouse" or "dogbed")
        {
            // Its front a panel round its openings; one with the rest's when that is a panel too.
            if (spec.Front == "panel") restEnd = cursor;
            else
            {
                FrontPanel(b, featureStart, cursor, T, openings);
                if (featureStart > s0 + 0.05) Board(featureStart - T / 2);
                restEnd = featureStart - T;
            }
        }
        if (restEnd - s0 > 0.05)
        {
            switch (spec.Front)
            {
                case "doors":
                {
                    var len = restEnd - s0;
                    var n = Math.Max(1, (int)Math.Ceiling(len / spec.DoorWidth - 1e-6));
                    var w = len / n;
                    var leaf = 0;
                    for (var i = 0; i < n; i++)
                    {
                        var (a, c) = (s0 + i * w, s0 + (i + 1) * w);
                        // Too low at its high side for a door: a fixed panel.
                        if (sp.Top(c) < 0.35) { b.Box(Panels, "Panels", "carcass", a, c, 0, T, 0, 10); continue; }
                        leaf++;
                        b.Box($"nook-door:{leaf}", $"Door {leaf}", "front", a + 0.002, c - 0.002, 0, T, 0.01, 10, 0.003);
                        var zc = Math.Min(1.05, sp.Top(c) - 0.15);
                        if (zc - 0.08 > 0.1) b.Box("nook-handles", "Handles", "metal", c - 0.065, c - 0.05, -0.03, 0, zc - 0.08, zc + 0.08);
                    }
                    if (spec.Shelves)
                        for (var z = spec.ShelfSpacing; z < sp.Top(restEnd); z += spec.ShelfSpacing)
                            b.Box("nook-shelves", "Shelves", "carcass", s0, restEnd, T + 0.02, D, z, z + T, 0.03, 0.15);
                    break;
                }
                case "panel":
                    FrontPanel(b, s0, restEnd, T, openings);
                    break;
            }
        }

        var order = new[] { "nook-panel", "nook-door", "nook-trim", "nook-handles", "nook-shelves", "nook-bookcase", "nook-desk", "nook-light", "nook-bench", "nook-drawers", "nook-cubbies", "nook-cushion", "nook-hooks", "nook-bed" };
        var parts = b.Parts.Values.OrderBy(p => Array.IndexOf(order, p.Key.Split(':')[0])).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
        return new Layout(sp, spec, parts, notes);
    }

    /// <summary>A front panel from <paramref name="a"/> to <paramref name="c"/> round its openings: strips between the openings' sides, each cut round the openings over it.</summary>
    static void FrontPanel(Builder b, double a, double c, double T, List<(double A, double B, double Z0, double Z1)> openings)
    {
        var xs = new SortedSet<double> { a, c };
        foreach (var o in openings) { if (o.A > a && o.A < c) xs.Add(o.A); if (o.B > a && o.B < c) xs.Add(o.B); }
        var list = xs.ToList();
        var any = false;
        for (var i = 0; i + 1 < list.Count; i++)
        {
            var (x0, x1) = (list[i], list[i + 1]);
            var z = 0.0;
            foreach (var o in openings.Where(o => o.A <= x0 + 1e-9 && o.B >= x1 - 1e-9).OrderBy(o => o.Z0))
            {
                if (o.Z0 > z + 1e-6) any |= b.Box("nook-panel", "Panels", "carcass", x0, x1, 0, T, z, o.Z0, count: false);
                z = Math.Max(z, o.Z1);
            }
            any |= b.Box("nook-panel", "Panels", "carcass", x0, x1, 0, T, z, 10, count: false);
        }
        // The strips are one panel.
        if (any) b.PartOf("nook-panel", "Panels", "carcass").Count++;
    }

    // ─── Geometry ───────────────────────────────────────────────────────────

    /// <summary>The space and its furnishing as the element is now; null with the reason when it cannot be.</summary>
    public static (Layout? Layout, string? Problem) Build(IModelView m, Entity e)
    {
        if (e.Str("host") is not { } host || m.Get(host) is not { Class: IfcClass.Stair } stair) return (null, "The stair of this under-stair space is gone.");
        var (space, problem) = Space(m, stair, e);
        if (space == null) return (null, problem);
        return (Lay(space, SpecOf(e) ?? UnderStairSpec.Default()), null);
    }

    static (string? Material, string Color) Look(IModelView m, UnderStairSpec spec, string role)
    {
        string? Mat(string name) => Materials.Find(m, name)?.Id;
        return role switch
        {
            "carcass" => (Mat(spec.Carcass), Materials.ColorOf(m, Mat(spec.Carcass))),
            "front" => (Mat(spec.Fronts), Materials.ColorOf(m, Mat(spec.Fronts))),
            "fabric" => (Mat(spec.Fabric), Materials.ColorOf(m, Mat(spec.Fabric))),
            "light" => (null, "#ffe7a3"),
            _ => (null, "#3d3d3d"),
        };
    }

    public static ElementGeometry? Geometry(Entity e, IModelView m)
    {
        var (layout, problem) = Build(m, e);
        if (layout == null) return new ElementGeometry([], Diagnostics: [problem ?? "The space under the stair cannot be built."]);
        var sp = layout.Space;
        var parts = new List<GeometryPart>();
        foreach (var p in layout.Parts)
        {
            var solids = new List<Solid>();
            foreach (var (poly, d0, d1) in p.Pieces)
            {
                var ring = poly.Select(q => sp.World(q.S, d0, q.Z)).ToList();
                var across = sp.World(0, d1, 0) - sp.World(0, d0, 0);
                if (StairBuilder.Oriented(ring, across, p.Key) is { } s) solids.Add(s);
            }
            if (solids.Count == 0) continue;
            var (material, color) = Look(m, layout.Spec, p.Role);
            parts.Add(new GeometryPart(p.Name, color, Openings.Combine(solids, p.Key), material, p.Key));
        }
        Vec2[] snaps = [sp.Plan(sp.S0, 0), sp.Plan(sp.S1, 0), sp.Plan(sp.S0, sp.Depth), sp.Plan(sp.S1, sp.Depth)];
        return new ElementGeometry(parts, [], snaps, layout.Notes.Count > 0 ? layout.Notes : null);
    }

    /// <summary>Its length, depth and height at the high end, the floor it covers, the space's volume; each part's pieces.</summary>
    public static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (Build(m, e).Layout is not { } layout) yield break;
        var sp = layout.Space;
        var len = sp.S1 - sp.S0;
        // The space's volume: its section under the soffit, by steps of 5 cm, across its depth.
        var n = Math.Max(1, (int)Math.Ceiling(len / 0.05));
        var area = Enumerable.Range(0, n).Sum(i => Math.Max(0, sp.Top(sp.S0 + (i + 0.5) * len / n)) * len / n);
        yield return new QuantitySet("Lapin_UnderStairQuantities",
        [
            new("Length", len, "LENGTH"),
            new("Depth", sp.Depth, "LENGTH"),
            new("Height", sp.Top(sp.S1), "LENGTH", "Height at the high end"),
            new("FloorArea", len * sp.Depth, "AREA", "Floor area"),
            new("GrossVolume", area * sp.Depth, "VOLUME", "Volume"),
        ]);
        foreach (var p in layout.Parts)
            yield return new QuantitySet("Lapin_UnderStairPart", [new("Count", p.Count, "COUNT", "Pieces")], p.Key, p.Name);
    }

    // ─── The configurator's preview ─────────────────────────────────────────

    static JsonArray Pts(IEnumerable<(double X, double Y)> pts) => new([.. pts.SelectMany(p => new JsonNode[] { Math.Round(p.X, 4), Math.Round(p.Y, 4) })]);

    /// <summary>
    /// What the configurator draws: the elevation seen from the open side (the flight's steps and soffit, each part's pieces in
    /// (s, z), nearer ones last), the plan ((s, d) from the open side's face: the flight, each part's pieces), the space's numbers,
    /// each part with its pieces, and the notes on what did not fit; or the problem when there is no space.
    /// </summary>
    static JsonObject Preview(IModelView m, Entity stair, Entity draft, UnderStairSpec spec)
    {
        var (space, problem) = Space(m, stair, draft);
        var o = new JsonObject { ["spec"] = spec.ToJson(), ["flights"] = StairBuilder.Resolve(m, stair) is var (rr, _, _) ? rr.Flights.Count : 1 };
        if (space == null) { o["problem"] = problem; return o; }
        var layout = Lay(space, spec);
        var fl = space.Flight;
        var r = space.Stair;
        // The flight in elevation: its steps, then back down its soffit.
        var steps = new List<(double, double)> { (0, fl.Z0) };
        for (var i = 1; i <= fl.Risers; i++) { steps.Add(((i - 1) * r.Tread, fl.Z0 + i * r.RiserHeight)); if (i < fl.Risers) steps.Add((i * r.Tread, fl.Z0 + i * r.RiserHeight)); }
        steps.AddRange(space.Soffit.AsEnumerable().Reverse().Select(p => (p.S, p.Z + Clearance)));
        var elevation = new JsonArray { (JsonNode)new JsonObject { ["role"] = "stair", ["pts"] = Pts(steps) } };
        var plan = new JsonArray { (JsonNode)new JsonObject { ["role"] = "stair", ["pts"] = Pts([(0.0, 0.0), (space.Run, 0.0), (space.Run, space.Width), (0.0, space.Width)]) } };
        // Far ones first, so the front is drawn over what is behind it.
        foreach (var (p, piece) in layout.Parts.SelectMany(p => p.Pieces.Select(x => (p, x))).OrderByDescending(x => x.x.D0))
        {
            elevation.Add((JsonNode)new JsonObject { ["role"] = p.Role, ["key"] = p.Key, ["pts"] = Pts(piece.Poly.Select(q => (q.S, q.Z))) });
            var (a, c) = (piece.Poly.Min(q => q.S), piece.Poly.Max(q => q.S));
            plan.Add((JsonNode)new JsonObject { ["role"] = p.Role, ["key"] = p.Key, ["pts"] = Pts([(a, piece.D0), (c, piece.D0), (c, piece.D1), (a, piece.D1)]) });
        }
        o["elevation"] = elevation;
        o["plan"] = plan;
        o["space"] = new JsonObject
        {
            ["start"] = Math.Round(space.S0, 4), ["end"] = Math.Round(space.S1, 4), ["run"] = Math.Round(space.Run, 4), ["depth"] = Math.Round(space.Depth, 4), ["width"] = Math.Round(space.Width, 4),
            ["low"] = Math.Round(space.Top(space.S0), 4), ["high"] = Math.Round(space.Top(space.S1), 4), ["side"] = space.Sign > 0 ? "left" : "right", ["flight"] = space.Index + 1,
        };
        o["parts"] = new JsonArray([.. layout.Parts.Select(p => (JsonNode)new JsonObject { ["key"] = p.Key, ["name"] = p.Name, ["role"] = p.Role, ["count"] = p.Count })]);
        o["notes"] = new JsonArray([.. layout.Notes.Select(x => (JsonNode)x)]);
        return o;
    }
}

/// <summary>The under-stair spaces' schedule: for each space, each part with its pieces, its material and its volume of material; totals by material.</summary>
public sealed class UnderStairSchedule : IScheduleSource
{
    public string Kind => "underStairs";
    public string Label => "Under-stair spaces";

    public ScheduleGrid Grid(IQueryContext q)
    {
        var m = q.Model;
        var rows = new List<IReadOnlyList<string>>();
        var keys = new List<string?>();
        var totals = new Dictionary<string, (int Count, double Volume)>(StringComparer.OrdinalIgnoreCase);
        static string N(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
        foreach (var e in m.OfClass(UnderStairs.Class).OrderBy(x => Spatial.StoreyOf(m, x.Id)?.Num("elevation") ?? 0).ThenBy(x => x.Name, StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering)))
        {
            var storey = Spatial.StoreyOf(m, e.Id)?.Name ?? "";
            var stair = m.Get(e.Str("host") ?? "")?.Name ?? "";
            if (UnderStairs.Build(m, e).Layout is not { } layout) continue;
            var g = q.Geometry(e.Id);
            foreach (var p in layout.Parts)
            {
                var part = g?.Parts.FirstOrDefault(x => x.Key == p.Key);
                var material = part?.Material is { } mid ? m.Get(mid)?.Name ?? "" : "";
                var v = part != null ? Measure.Volume(part.Solid) : 0;
                rows.Add([storey, stair, e.Name, p.Name, p.Count.ToString(CultureInfo.InvariantCulture), material, N(v)]);
                keys.Add($"{e.Id}|{p.Key}");
                if (material.Length > 0) totals[material] = totals.TryGetValue(material, out var t) ? (t.Count + p.Count, t.Volume + v) : (p.Count, v);
            }
        }
        foreach (var (material, t) in totals.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(["Total", "", "", "", t.Count.ToString(CultureInfo.InvariantCulture), material, N(t.Volume)]);
            keys.Add(null);
        }
        return new ScheduleGrid(Label, ["Storey", "Stair", "Space", "Part", "Pieces", "Material", "Volume (m³)"], rows, keys);
    }
}
