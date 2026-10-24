using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;

namespace Lapin.Host;

/// <summary>
/// An old Lapin project (format 2) rebuilt through the engine's own commands,
/// so what comes out is native: storeys, materials, layered wall types, walls
/// (joined as they are drawn now), floors, columns, beams, door and window
/// types with their placements, plans, foundation plans and elevations. It
/// opens unsaved, without undo history: saving writes format 3. What has no
/// counterpart yet is listed (<see cref="LegacyProject.Skipped"/>).
/// </summary>
public static class LegacyImport
{
    public sealed record Result(Diff Diff, List<string> Notes);

    static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s : null;
    static double? Num(JsonNode? n) => Json.Number(n);

    public static Result Run(Session session, LegacyProject old)
    {
        session.New();
        var notes = new List<string>(old.Skipped);
        var has = session.Catalog.Commands.Select(c => c.Name).ToHashSet();
        string? Id(JsonNode? r) => (string?)r?["id"];
        JsonNode? Run(string command, JsonObject args)
        {
            if (!has.Contains(command)) return null;
            try { session.Execute(command, args, out var r); return r; }
            catch (CommandException e) { notes.Add($"{command}: {e.Message}"); return null; }
        }

        // Storeys: the first takes the new project's ground floor; each as high as the gap to the next (3 m for the last).
        var storeys = new Dictionary<string, string>();
        var ordered = old.Storeys.OrderBy(s => Num(s["elevation"]) ?? 0).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            var elevation = Num(s["elevation"]) ?? 0;
            var height = i + 1 < ordered.Count ? (Num(ordered[i + 1]["elevation"]) ?? elevation + 3) - elevation : 3;
            if (!(height > 0.1)) height = 3;
            string? id;
            if (i == 0)
            {
                id = Spatial.Storeys(session.Model).First().Id;
                Run("storey.update", new JsonObject { ["id"] = id, ["name"] = Str(s["name"]) ?? "Ground floor", ["elevation"] = elevation, ["height"] = height });
            }
            else id = Id(Run("storey.create", new JsonObject { ["name"] = Str(s["name"]), ["elevation"] = elevation, ["height"] = height }));
            if (id != null && Str(s["id"]) is { } oid) storeys[oid] = id;
        }
        string Storey(JsonObject e) => Str(e["storeyId"]) is { } s && storeys.TryGetValue(s, out var id) ? id : Spatial.Storeys(session.Model).First().Id;

        // Materials by name (a project's own names, colours kept).
        var materialNames = new Dictionary<string, string>();
        foreach (var m in old.Materials)
        {
            var name = Str(m["name"]) ?? "Material";
            if (Str(m["id"]) is { } oid) materialNames[oid] = name;
            if (Materials.Find(session.Model, name) == null) Run("material.create", new JsonObject { ["name"] = name, ["color"] = Str(m["color"]) });
        }

        // Types: layered walls (the thickest layer is the core), door and window specs.
        var wallTypes = new Dictionary<string, string>();
        var joineryTypes = new Dictionary<string, string>();
        foreach (var t in old.Types)
        {
            var oid = Str(t["id"]);
            var kind = Str(t["kind"]);
            if (oid == null) continue;
            if (kind == "wall" && t["layers"] is JsonArray layers && layers.Count > 0)
            {
                var thick = layers.Select(l => Num(l?["thickness"]) ?? 0).ToList();
                var core = thick.IndexOf(thick.Max());
                var rows = new JsonArray(layers.Select((l, i) => (JsonNode)new JsonObject
                {
                    ["material"] = materialNames.GetValueOrDefault(Str(l?["materialId"]) ?? "", Str(l?["name"]) ?? "Masonry"),
                    ["thickness"] = thick[i], ["core"] = i == core,
                }).ToArray());
                if (Id(Run("architecture.createWallType", new JsonObject { ["name"] = Str(t["name"]), ["layers"] = rows })) is { } id) wallTypes[oid] = id;
            }
            else if (kind is "door" or "window" && t["spec"] is JsonObject spec)
            {
                var s = (JsonObject)spec.DeepClone();
                // The old spec kept these under its props.
                if (s["props"] is JsonObject props)
                {
                    s["isExternal"] ??= props["isExternal"]?.DeepClone() ?? false;
                    s["thermalTransmittance"] ??= props["thermalTransmittance"]?.DeepClone();
                }
                s["kind"] = kind;
                s["name"] ??= Str(t["name"]) ?? kind;
                s["mark"] ??= Str(t["mark"]) ?? (kind == "door" ? "D" : "W");
                var r = Run("architecture.createJoineryType", new JsonObject { ["spec"] = s });
                if (Id(r) is { } id) joineryTypes[oid] = id;
                else notes.Add($"the {kind} type „{Str(t["name"])}” could not be read: the default {kind} takes its place");
            }
        }

        // Walls, joined as they are drawn now; layers flipped where they were.
        var walls = new Dictionary<string, string>();
        foreach (var w in old.Of("wall"))
        {
            var args = new JsonObject
            {
                ["storeyId"] = Storey(w), ["x1"] = Num(w["start_x"]), ["y1"] = Num(w["start_y"]), ["x2"] = Num(w["end_x"]), ["y2"] = Num(w["end_y"]),
                ["height"] = Num(w["height"]) ?? 3, ["baseOffset"] = Num(w["baseOffset"]) ?? 0,
            };
            if (Str(w["typeId"]) is { } t && wallTypes.TryGetValue(t, out var tid)) args["typeId"] = tid;
            else if (Num(w["thickness"]) is { } th && th > 0)
            {
                var key = $"thick:{th}";
                if (!wallTypes.TryGetValue(key, out var made) && Id(Run("architecture.createWallType", new JsonObject
                    {
                        ["name"] = $"Wall {th * 1000:0}", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Masonry", ["thickness"] = th, ["core"] = true }),
                    })) is { } created)
                    wallTypes[key] = made = created;
                if (made != null) args["typeId"] = made;
            }
            if (Id(Run("architecture.addWall", args)) is { } id)
            {
                if (Str(w["id"]) is { } oid) walls[oid] = id;
                if (w["flipLayers"] is JsonValue f && f.TryGetValue<bool>(out var flip) && flip) Run("architecture.flipWall", new JsonObject { ["id"] = id });
            }
        }

        // Floors: a type per thickness (reinforced concrete), the top where it was.
        var slabTypes = new Dictionary<double, string>();
        foreach (var s in old.Of("slab"))
        {
            if (s["outline"] is not JsonArray outline || outline.Count < 3) continue;
            var t = Num(s["thickness"]) ?? 0.2;
            if (!slabTypes.TryGetValue(t, out var tid) && Id(Run("architecture.createSlabType", new JsonObject
                {
                    ["name"] = $"Floor {t * 1000:0}", ["layers"] = new JsonArray(new JsonObject { ["material"] = "Reinforced concrete", ["thickness"] = t, ["core"] = true }),
                })) is { } made)
                slabTypes[t] = tid = made;
            Run("architecture.addSlab", new JsonObject { ["storeyId"] = Storey(s), ["points"] = outline.DeepClone(), ["offset"] = Num(s["topOffset"]) ?? 0, ["typeId"] = tid });
        }

        // Columns and beams: a type per section.
        JsonObject? Section(JsonNode? n)
        {
            if (n is not JsonObject o) return null;
            var type = Str(o["Type"]) ?? Str(o["kind"]) ?? "Rectangle";
            if (type.Equals("Circle", StringComparison.OrdinalIgnoreCase) && Num(o["Radius"]) is { } r) return new JsonObject { ["kind"] = "circle", ["width"] = 2 * r, ["depth"] = 2 * r };
            return Num(o["XDim"]) is { } x && Num(o["YDim"]) is { } y ? new JsonObject { ["kind"] = "rectangle", ["width"] = x, ["depth"] = y } : null;
        }
        var memberTypes = new Dictionary<string, string>();
        string? MemberType(string kind, JsonObject? section)
        {
            if (section == null) return null;
            var key = kind + section.ToJsonString();
            if (memberTypes.TryGetValue(key, out var id)) return id;
            var made = Id(Run(kind == "column" ? "structure.createColumnType" : "structure.createBeamType", new JsonObject { ["section"] = section }));
            if (made != null) memberTypes[key] = made;
            return made;
        }
        foreach (var c in old.Of("column"))
        {
            var args = new JsonObject { ["storeyId"] = Storey(c), ["x"] = Num(c["position_x"]), ["y"] = Num(c["position_y"]), ["rotation"] = Num(c["rotation"]) ?? 0 };
            if (MemberType("column", Section(c["section"])) is { } t) args["typeId"] = t;
            if (Id(Run("structure.addColumn", args)) is { } id && Num(c["height"]) is { } h && h > 0)
                Run("element.update", new JsonObject { ["id"] = id, ["attrs"] = new JsonObject { ["height"] = h } });
        }
        foreach (var b in old.Of("beam"))
        {
            var args = new JsonObject { ["storeyId"] = Storey(b), ["x1"] = Num(b["start_x"]), ["y1"] = Num(b["start_y"]), ["x2"] = Num(b["end_x"]), ["y2"] = Num(b["end_y"]) };
            if (MemberType("beam", Section(b["section"])) is { } t) args["typeId"] = t;
            Run("structure.addBeam", args);
        }
        if (old.Of("column").Any() || old.Of("beam").Any())
            if (!has.Contains("structure.addColumn")) notes.Add("columns and beams: this edition has no structure module");

        // Doors and windows in their walls, at the same place along them, flipped as they were.
        foreach (var kind in new[] { "door", "window" })
            foreach (var f in old.Of(kind))
            {
                if (Str(f["hostId"]) is not { } host || !walls.TryGetValue(host, out var wall)) { notes.Add($"a {kind} without its wall was left out"); continue; }
                var args = new JsonObject { ["wallId"] = wall, ["along"] = Num(f["along"]) ?? 0 };
                if (Str(f["typeId"]) is { } t && joineryTypes.TryGetValue(t, out var tid)) args["typeId"] = tid;
                if (kind == "window" && Num(f["sill"]) is { } sill) args["sill"] = sill;
                if (Id(Run(kind == "door" ? "architecture.addDoor" : "architecture.addWindow", args)) is not { } id) continue;
                if (f["flipHand"] is JsonValue h && h.TryGetValue<bool>(out var hand) && hand) Run("architecture.flipJoinery", new JsonObject { ["id"] = id, ["axis"] = "x" });
                if (f["flipFacing"] is JsonValue fc && fc.TryGetValue<bool>(out var facing) && facing) Run("architecture.flipJoinery", new JsonObject { ["id"] = id, ["axis"] = "y" });
            }
        if (old.Of("opening").Any()) notes.Add($"{old.Of("opening").Count()} bare opening(s) (without a door or window) were left out");

        // Views: plans of their own (the automatic ones are the storeys'), elevations, sections on the line they were drawn on.
        // The old ids map to the new, for the sheets.
        var levelStorey = ordered.ToDictionary(s => Str(s["name"]) ?? "", s => storeys.GetValueOrDefault(Str(s["id"]) ?? ""));
        var viewIds = new Dictionary<string, string>();
        var sectionsLost = 0;
        foreach (var v in old.Views)
        {
            var oldId = Str(v["id"]) ?? "";
            string? made = null;
            switch (Str(v["kind"]))
            {
                case "plan":
                    if (levelStorey.GetValueOrDefault(Str(v["level"]?["name"]) ?? "") is not { } st) break;
                    if (v["auto"] is JsonValue a && a.TryGetValue<bool>(out var auto) && auto) made = "plan:" + st;
                    else made = Id(Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = st, ["name"] = Str(v["name"]), ["cutHeight"] = Num(v["cutHeight"]) ?? 1.2 }));
                    break;
                case "elevation" when Str(v["direction"]) is { } dir:
                    made = Id(Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = dir, ["name"] = Str(v["name"]) }));
                    break;
                case "section":
                    // The old viewer's frame is y-up: a plan point is (x, −z). Flipped, it looked the other way.
                    if (v["marker"]?["a"] is JsonArray ma && v["marker"]?["b"] is JsonArray mb && ma.Count >= 3 && mb.Count >= 3)
                    {
                        JsonArray Plan(JsonArray q) => [Num(q[0]) ?? 0, -(Num(q[2]) ?? 0)];
                        var (start, end) = (Plan(ma), Plan(mb));
                        if (v["plane"]?["flipped"] is JsonValue fv && fv.TryGetValue<bool>(out var flipped) && flipped) (start, end) = (end, start);
                        made = Id(Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = start, ["end"] = end, ["name"] = Str(v["name"]) }));
                    }
                    else sectionsLost++;
                    break;
            }
            if (made != null) viewIds[oldId] = made;
        }
        if (sectionsLost > 0) notes.Add($"{sectionsLost} section(s) without their line: draw them again");

        // Sheets: their paper, number, title block, and the views on them at the scale they had there.
        var sheetsMade = 0;
        foreach (var sh in old.Sheets ?? [])
        {
            var paper = Str(sh["paper"]) is { } pp && pp.Length == 2 && pp[0] == 'A' ? pp : "A1";
            if (Id(Run("sheet.create", new JsonObject { ["number"] = Str(sh["number"]), ["name"] = Str(sh["name"]) ?? "Sheet", ["paper"] = paper, ["orientation"] = Str(sh["orientation"]) ?? "landscape" })) is not { } sheetId) continue;
            sheetsMade++;
            if (sh["titleBlock"] is JsonObject tb) Run("sheet.titleBlock", new JsonObject { ["id"] = sheetId, ["fields"] = tb.DeepClone() });
            foreach (var vp in (sh["viewports"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (!viewIds.TryGetValue(Str(vp["viewId"]) ?? "", out var view)) { notes.Add($"A view on sheet {Str(sh["number"])} was left out"); continue; }
                if (Num(vp["scale"]) is { } sc && sc > 0 && !view.StartsWith("plan:", StringComparison.Ordinal)) Run("element.update", new JsonObject { ["id"] = view, ["attrs"] = new JsonObject { ["scale"] = sc } });
                var placed = Run("sheet.addViewport", new JsonObject { ["id"] = sheetId, ["viewId"] = view, ["x"] = Num(vp["x"]) ?? 200, ["y"] = Num(vp["y"]) ?? 150 });
                if (placed?["viewportId"] is JsonValue vid && (Num(vp["width"]) ?? 0) > 0)
                    Run("sheet.updateViewport", new JsonObject
                    {
                        ["id"] = sheetId, ["viewportId"] = (string?)vid, ["width"] = Num(vp["width"]), ["height"] = Num(vp["height"]),
                        ["center"] = vp["center"] is JsonObject c ? new JsonArray(Num(c["x"]) ?? 0, Num(c["y"]) ?? 0) : null,
                    });
            }
        }
        if (sheetsMade > 0) notes.Add($"{sheetsMade} sheet(s) brought over");

        // The result is a new project: no undo back through the import, nothing saved yet.
        var diff = session.Load(session.Model.Entities.ToList(), session.Model.Relations.ToList(), null);
        return new Result(diff, notes);
    }
}
