using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Planning;

/// <summary>
/// The location-based schedule (LBS, a time–location or flow-line chart): time on one axis, the model's locations on the
/// other, a segment a task and a location it works in. The locations come from the model's spatial structure, so levels come
/// for free — the buildings, their storeys, and within a storey the user's own zones (the values of a field the elements carry:
/// a property, a parameter, a formula); a task's locations are those of the elements its rule and exceptions take
/// (<see cref="TaskElements"/>), its dates the critical path's (<see cref="Cpm"/>). A task in one location is a line rising
/// across the location's band from its start to its finish; a task over several neighbouring locations is a block over them.
/// The tasks are gathered by their names with the locations' names taken off (so „Masonry · Ground floor” and „Masonry · First
/// floor” read as one trade climbing the building), each group a colour. Derived on every ask, nothing stored; the view's own
/// settings (the level, the zones' field, the levels folded, the labels moved, the notes) are a <c>LapinLbs</c> entity's.
/// </summary>
public static class Lbs
{
    public const string Class = "LapinLbs";
    public static readonly string[] Levels = ["building", "storey", "zone"];
    /// <summary>A storey's height when the model gives none (no storey above, no height of its own).</summary>
    const double DefaultHeight = 3;

    /// <summary>A location: a building, a storey, a zone of a storey; where it sits on the axis (<see cref="At"/>, metres from the axis' foot) and how much of it it takes.</summary>
    public sealed class Location
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required string Level { get; init; }
        public string? Parent { get; init; }
        public double At { get; set; }
        public double Extent { get; set; }
        /// <summary>A storey's elevation in the model (null for the others).</summary>
        public double? Elevation { get; init; }
        public List<string> Children { get; } = [];
        /// <summary>A row of the chart: a leaf of the level asked, or a folded location; its place counting up from the foot (-1: not a row).</summary>
        public int Row { get; set; } = -1;
        public bool Folded { get; set; }
    }

    /// <summary>A task in a run of neighbouring rows: its dates, the rows it spans, whether a line or a block.</summary>
    public sealed record Segment(string Id, string Task, string Name, string Group, string Colour, string From, string To, IReadOnlyList<string> Locations,
        int Row0, int Row1, double Y0, double Y1, DateOnly Start, DateOnly Finish, string Rep, bool Critical, string Status, double Complete, int Count, int TotalFloat, int Duration);

    public sealed record Unlocated(string Task, string Name, DateOnly Start, DateOnly Finish, int Count, string Why);

    public sealed record Group(string Key, string Colour, List<string> Tasks);

    public sealed record Note(string Id, DateOnly Date, string Location, string Text, double Dx, double Dy);
    public sealed record Label(double Dx, double Dy, double Angle);

    /// <summary>The view's own settings: the level, the zones' field, the locations folded, the labels moved and turned, the notes.</summary>
    public sealed record Settings(string Level, string? ZoneField, IReadOnlyList<string> Folded, IReadOnlyDictionary<string, Label> Labels, IReadOnlyList<Note> Notes)
    {
        public static readonly Settings Default = new("storey", null, [], new Dictionary<string, Label>(), []);

        public static Settings Read(JsonObject? o)
        {
            if (o == null) return Default;
            var level = (string?)o["level"] is { } l && Levels.Contains(l) ? l : "storey";
            var folded = o["collapsed"] is JsonArray c ? c.Select(x => (string?)x).OfType<string>().Distinct().ToList() : [];
            var labels = new Dictionary<string, Label>();
            if (o["labels"] is JsonObject lo)
                foreach (var (k, v) in lo)
                    if (v is JsonObject x) labels[k] = new Label(Json.Number(x["dx"]) ?? 0, Json.Number(x["dy"]) ?? 0, Json.Number(x["angle"]) ?? 0);
            var notes = new List<Note>();
            if (o["notes"] is JsonArray na)
                foreach (var n in na.OfType<JsonObject>())
                    if ((string?)n["id"] is { } id && WorkCalendar.ParseDate((string?)n["date"]) is { } d && (string?)n["location"] is { } loc)
                        notes.Add(new Note(id, d, loc, (string?)n["text"] ?? "", Json.Number(n["dx"]) ?? 0, Json.Number(n["dy"]) ?? 0));
            return new Settings(level, ((string?)o["zoneField"])?.Trim() is { Length: > 0 } zf ? zf : null, folded, labels, notes);
        }

        public JsonObject ToJson()
        {
            var o = new JsonObject { ["level"] = Level };
            if (ZoneField != null) o["zoneField"] = ZoneField;
            if (Folded.Count > 0) o["collapsed"] = new JsonArray(Folded.Select(x => (JsonNode)x).ToArray());
            if (Labels.Count > 0) o["labels"] = new JsonObject(Labels.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)new JsonObject { ["dx"] = Math.Round(kv.Value.Dx, 2), ["dy"] = Math.Round(kv.Value.Dy, 2), ["angle"] = Math.Round(kv.Value.Angle, 2) })));
            if (Notes.Count > 0) o["notes"] = new JsonArray(Notes.Select(n => (JsonNode)new JsonObject { ["id"] = n.Id, ["date"] = WorkCalendar.Date(n.Date), ["location"] = n.Location, ["text"] = n.Text, ["dx"] = Math.Round(n.Dx, 2), ["dy"] = Math.Round(n.Dy, 2) }).ToArray());
            return o;
        }
    }

    /// <summary>What the chart shows: the locations (a tree, the rows counted from the foot), the segments, the groups, the tasks it cannot place.</summary>
    public sealed record Chart(Plan Plan, CpmResult Result, string Level, string? ZoneField, List<Location> Locations, List<Segment> Segments, List<Group> Groups, List<Unlocated> Unlocated, double Height);

    static readonly StringComparer Natural = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);

    /// <summary>The project's LBS view (the first; null: none yet).</summary>
    public static Entity? ViewOf(IModelView m, string? id = null) =>
        id != null ? m.Get(id) is { Class: Class } v ? v : null : m.OfClass(Class).FirstOrDefault();

    public static Chart Build(IQueryContext q, Settings s)
    {
        var p = PlanningModule.ReadResolved(q, out var links);
        var (r, _) = PlanningModule.Solve(p);
        return Build(q.Model, p, r, links, s);
    }

    public static Chart Build(IModelView m, Plan p, CpmResult r, TaskElements links, Settings s)
    {
        var level = s.Level == "zone" && s.ZoneField == null ? "storey" : s.Level;
        // ── The locations: buildings, their storeys (by elevation), the zones of each storey by the zones' field.
        var locs = new Dictionary<string, Location>();
        var order = new List<Location>();
        Location Add(Location l) { locs[l.Id] = l; order.Add(l); if (l.Parent != null && locs.TryGetValue(l.Parent, out var par)) par.Children.Add(l.Id); return l; }
        var storeys = Spatial.Storeys(m).ToList();
        var byBuilding = storeys.GroupBy(st => m.RelatingOf(st.Id, IfcClass.RelAggregates) is { Class: IfcClass.Building } b ? b.Id : "")
            .OrderBy(g => g.Key.Length == 0 ? int.MaxValue : m.Order(g.Key)).ToList();
        // The zone of each element a task takes (its storey, then its field's value).
        string? ZoneKey(string storey, string element)
        {
            var v = links.Field(element, s.ZoneField!);
            return $"{storey}/{(v.Exists ? v.Shown : "")}";
        }
        var elementZone = new Dictionary<string, string>();
        var zonesOf = new Dictionary<string, SortedSet<string>>();
        if (level == "zone")
            foreach (var own in links.ByTask.Values)
                foreach (var el in own.Keys)
                {
                    if (elementZone.ContainsKey(el) || links.Facts(el)?.Storey is not { } st) continue;
                    var key = ZoneKey(st, el)!;
                    elementZone[el] = key;
                    (zonesOf.TryGetValue(st, out var set) ? set : zonesOf[st] = new SortedSet<string>(Natural)).Add(key[(st.Length + 1)..]);
                }
        var axis = 0.0;
        foreach (var g in byBuilding)
        {
            var bid = g.Key.Length == 0 ? "building:" : g.Key;
            var building = Add(new Location { Id = bid, Name = g.Key.Length == 0 ? "Building" : m.Get(g.Key)?.Name ?? "Building", Level = "building", At = axis });
            var list = g.ToList();
            var foot = list.Count > 0 ? list[0].Num("elevation") : 0;
            var top = foot;
            for (var i = 0; i < list.Count; i++)
            {
                var st = list[i];
                var e = st.Num("elevation");
                var h = i + 1 < list.Count ? list[i + 1].Num("elevation") - e : st.NumOrNull("height") is { } sh && sh > 0 ? sh : DefaultHeight;
                if (h <= 1e-6) h = st.NumOrNull("height") is { } hh && hh > 0 ? hh : DefaultHeight;
                var storey = Add(new Location { Id = st.Id, Name = st.Name, Level = "storey", Parent = bid, At = axis + e - foot, Extent = h, Elevation = e });
                top = Math.Max(top, e + h);
                if (level == "zone" && zonesOf.TryGetValue(st.Id, out var zones))
                {
                    var k = 0;
                    var share = h / zones.Count;
                    foreach (var z in zones)
                        Add(new Location { Id = $"{st.Id}/{z}", Name = z.Length == 0 ? "(no zone)" : z, Level = "zone", Parent = st.Id, At = storey.At + share * k++, Extent = share });
                }
            }
            building.Extent = Math.Max(top - foot, list.Count == 0 ? DefaultHeight : 0);
            axis += building.Extent;
        }
        // ── The rows: the leaves of the level asked, a folded location standing for all under it.
        var folded = s.Folded.ToHashSet();
        var rows = new List<Location>();
        void Rows(Location l)
        {
            if (l.Level == level || l.Children.Count == 0) { l.Row = rows.Count; rows.Add(l); return; }
            if (folded.Contains(l.Id)) { l.Folded = true; l.Row = rows.Count; rows.Add(l); return; }
            foreach (var c in l.Children) Rows(locs[c]);
        }
        foreach (var b in order.Where(l => l.Level == "building")) Rows(b);
        // The row an element's location falls in (its zone, its storey, its building, or a folded one above them).
        var rowOfId = new Dictionary<string, Location>();
        foreach (var row in rows)
        {
            void Mark(Location l) { rowOfId[l.Id] = row; foreach (var c in l.Children) Mark(locs[c]); }
            Mark(row);
        }
        Location? RowOfElement(string el)
        {
            if (links.Facts(el)?.Storey is not { } st || !locs.ContainsKey(st)) return null;
            if (level == "zone" && elementZone.TryGetValue(el, out var z) && rowOfId.TryGetValue(z, out var zr)) return zr;
            return rowOfId.GetValueOrDefault(st);
        }

        // ── The segments: each leaf task's rows, in runs of neighbouring rows.
        var segments = new List<Segment>();
        var unlocated = new List<Unlocated>();
        var groups = new List<Group>();
        var groupOf = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        var gantt = GanttSettings.Of(p.Schedule);
        var values = gantt.ColourBy != null ? TaskFields.All(p, m, links, gantt.Fields) : null;
        var legend = gantt.ColourBy is { } cb && values != null ? TaskFields.Legend(gantt, values.Values.Select(v => v[cb])) : null;
        var names = order.Select(l => l.Name).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(n => n.Length).ToList();
        foreach (var t in p.Rows)
        {
            if (p.IsSummary(t.Id) || !r.Times.TryGetValue(t.Id, out var tm)) continue;
            var own = links.ByTask.GetValueOrDefault(t.Id);
            if (own == null || own.Count == 0) { unlocated.Add(new(t.Id, t.Name, tm.Start, tm.Finish, 0, "none")); continue; }
            var count = new Dictionary<int, int>();
            foreach (var el in own.Keys)
                if (RowOfElement(el) is { } row) count[row.Row] = count.GetValueOrDefault(row.Row) + 1;
            if (count.Count == 0) { unlocated.Add(new(t.Id, t.Name, tm.Start, tm.Finish, own.Count, "unplaced")); continue; }
            var key = GroupKey(t.Name, names);
            if (!groupOf.TryGetValue(key, out var group))
            {
                var colour = values != null && gantt.ColourBy is { } c && legend != null && values.GetValueOrDefault(t.Id)?.GetValueOrDefault(c) is { } fv ? TaskFields.ColourOf(fv, legend) : null;
                group = groupOf[key] = new Group(key, colour ?? TaskFields.Palette[groups.Count % TaskFields.Palette.Length], []);
                groups.Add(group);
            }
            group.Tasks.Add(t.Id);
            var milestone = t.Milestone || t.Duration == 0;
            var pr = p.ProgressOf(t.Id);
            foreach (var (a, b) in Runs(count.Keys))
            {
                var r0 = rows[a];
                var r1 = rows[b];
                var under = Enumerable.Range(a, b - a + 1).Select(i => rows[i].Id).ToList();
                var n = Enumerable.Range(a, b - a + 1).Sum(i => count.GetValueOrDefault(i));
                segments.Add(new Segment($"{t.Id}@{r0.Id}", t.Id, t.Name, key, group.Colour, r0.Id, r1.Id, under, a, b, r0.At, r1.At + r1.Extent,
                    tm.Start, tm.Finish, milestone ? "milestone" : a == b ? "line" : "block", tm.Critical, tm.Status, Math.Round(pr.Complete, 1), n, tm.TotalFloat, milestone ? 0 : t.Duration));
            }
        }
        return new Chart(p, r, level, s.ZoneField, order, segments, groups, unlocated, axis);
    }

    /// <summary>Rows in runs of neighbours (2, 3, 4 and 6 → 2–4 and 6).</summary>
    public static List<(int From, int To)> Runs(IEnumerable<int> rows)
    {
        var o = new List<(int, int)>();
        foreach (var i in rows.Distinct().Order())
        {
            if (o.Count > 0 && o[^1].Item2 == i - 1) o[^1] = (o[^1].Item1, i);
            else o.Add((i, i));
        }
        return o;
    }

    const string Separators = " \t·•-–—:,;/|()[]";

    /// <summary>
    /// A task's group: its name with the locations' names taken off and the separators left at its ends trimmed („Masonry ·
    /// Ground floor” → „Masonry”, „P+1 - Plaster” → „Plaster”); its name as it is when nothing is left.
    /// </summary>
    public static string GroupKey(string name, IEnumerable<string> locationNames)
    {
        var s = name;
        foreach (var n in locationNames)
        {
            if (n.Length == 0) continue;
            for (var i = s.IndexOf(n, StringComparison.OrdinalIgnoreCase); i >= 0; i = s.IndexOf(n, StringComparison.OrdinalIgnoreCase))
            {
                // Only a whole word (not „Roof” in „Roofing”).
                var before = i == 0 || !char.IsLetterOrDigit(s[i - 1]);
                var after = i + n.Length >= s.Length || !char.IsLetterOrDigit(s[i + n.Length]);
                if (!before || !after) break;
                s = s[..i] + " " + s[(i + n.Length)..];
            }
        }
        s = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim(Separators.ToCharArray());
        // Separators left doubled in the middle („Masonry ·  · east”) read as one.
        return s.Length == 0 ? name.Trim() : s;
    }

    static string D(DateOnly d) => WorkCalendar.Date(d);

    public static JsonObject ToJson(Chart c, Entity? view, Settings s)
    {
        var p = c.Plan;
        var locations = new JsonArray(c.Locations.Select(l =>
        {
            var o = new JsonObject { ["id"] = l.Id, ["name"] = l.Name, ["level"] = l.Level, ["parent"] = l.Parent, ["at"] = Math.Round(l.At, 4), ["extent"] = Math.Round(l.Extent, 4), ["row"] = l.Row };
            if (l.Elevation is { } e) o["elevation"] = Math.Round(e, 4);
            if (l.Folded) o["folded"] = true;
            if (l.Children.Count > 0) o["children"] = new JsonArray(l.Children.Select(x => (JsonNode)x).ToArray());
            return (JsonNode)o;
        }).ToArray());
        var finish = c.Result.Times.Count > 0 ? c.Result.Times.Values.Max(t => t.Finish) : p.Start;
        return new JsonObject
        {
            ["view"] = view?.Id, ["level"] = c.Level, ["zoneField"] = c.ZoneField, ["settings"] = s.ToJson(),
            ["start"] = D(p.Start), ["finish"] = D(finish), ["dataDate"] = p.DataDate is { } dd ? D(dd) : null, ["height"] = Math.Round(c.Height, 4),
            ["locations"] = locations,
            ["segments"] = new JsonArray(c.Segments.Select(x => (JsonNode)new JsonObject
            {
                ["id"] = x.Id, ["task"] = x.Task, ["name"] = x.Name, ["group"] = x.Group, ["colour"] = x.Colour, ["from"] = x.From, ["to"] = x.To,
                ["locations"] = new JsonArray(x.Locations.Select(l => (JsonNode)l).ToArray()), ["row0"] = x.Row0, ["row1"] = x.Row1,
                ["y0"] = Math.Round(x.Y0, 4), ["y1"] = Math.Round(x.Y1, 4), ["start"] = D(x.Start), ["finish"] = D(x.Finish), ["rep"] = x.Rep,
                ["critical"] = x.Critical, ["status"] = x.Status, ["complete"] = x.Complete, ["count"] = x.Count, ["totalFloat"] = x.TotalFloat, ["duration"] = x.Duration,
            }).ToArray()),
            ["groups"] = new JsonArray(c.Groups.Select(g => (JsonNode)new JsonObject { ["key"] = g.Key, ["colour"] = g.Colour, ["tasks"] = new JsonArray(g.Tasks.Select(t => (JsonNode)t).ToArray()) }).ToArray()),
            ["unlocated"] = new JsonArray(c.Unlocated.Select(u => (JsonNode)new JsonObject { ["task"] = u.Task, ["name"] = u.Name, ["start"] = D(u.Start), ["finish"] = D(u.Finish), ["count"] = u.Count, ["why"] = u.Why }).ToArray()),
        };
    }

    /// <summary>The settings a view holds, with what a query asks over them (a level, a zones' field, the levels folded).</summary>
    public static Settings SettingsOf(Entity? view, JsonObject? a)
    {
        var s = Settings.Read(view?.Attrs["lbs"] as JsonObject);
        if (a == null) return s;
        if ((string?)a["level"] is { } l && Levels.Contains(l)) s = s with { Level = l };
        if (a.ContainsKey("zoneField")) s = s with { ZoneField = ((string?)a["zoneField"])?.Trim() is { Length: > 0 } z ? z : null };
        if (a["collapsed"] is JsonArray c) s = s with { Folded = c.Select(x => (string?)x).OfType<string>().ToList() };
        return s;
    }

    /// <summary>
    /// A view's settings changed by a command's arguments: the level, the zones' field, the levels folded, a label moved or
    /// turned (<c>label: {id, dx, dy, angle}</c>, null values put it back), a note added or changed (<c>note: {id?, date,
    /// location, text, dx, dy}</c>) or taken off (<c>removeNote</c>).
    /// </summary>
    public static Settings Change(Settings s, JsonObject a, Func<string> newId)
    {
        if (a.ContainsKey("level"))
            s = s with { Level = (string?)a["level"] is { } l && Levels.Contains(l) ? l : throw new CommandException("A location level is building, storey or zone.") };
        if (a.ContainsKey("zoneField"))
        {
            var z = ((string?)a["zoneField"])?.Trim();
            if (!string.IsNullOrEmpty(z) && ElementFields.FormulaOf(z) is { } fx && !ParamFormulas.IsTemplate(fx)) ParamFormulas.Check(fx);
            s = s with { ZoneField = string.IsNullOrEmpty(z) ? null : z };
        }
        if (a.ContainsKey("collapsed"))
            s = s with { Folded = a["collapsed"] is JsonArray c ? c.Select(x => (string?)x).OfType<string>().Distinct().ToList() : [] };
        if (a["label"] is JsonObject lo && (string?)lo["id"] is { Length: > 0 } lid)
        {
            var labels = new Dictionary<string, Label>(s.Labels);
            if (lo["dx"] is null && lo["dy"] is null && lo["angle"] is null) labels.Remove(lid);
            else labels[lid] = new Label(Json.Number(lo["dx"]) ?? 0, Json.Number(lo["dy"]) ?? 0, Math.Clamp(Json.Number(lo["angle"]) ?? 0, -90, 90));
            s = s with { Labels = labels };
        }
        if (a["note"] is JsonObject no)
        {
            var notes = s.Notes.ToList();
            var id = (string?)no["id"] is { Length: > 0 } nid ? nid : newId();
            var at = notes.FindIndex(n => n.Id == id);
            var old = at >= 0 ? notes[at] : null;
            var date = no.ContainsKey("date") ? WorkCalendar.ParseDate((string?)no["date"]) ?? throw new CommandException("A note is pinned to a date (yyyy-mm-dd).") : old?.Date ?? throw new CommandException("A note is pinned to a date (yyyy-mm-dd).");
            var loc = (string?)no["location"] ?? old?.Location ?? throw new CommandException("A note is pinned to a location.");
            var text = no.ContainsKey("text") ? ((string?)no["text"])?.Trim() ?? "" : old?.Text ?? "";
            var note = new Note(id, date, loc, text, Json.Number(no["dx"]) ?? old?.Dx ?? 0, Json.Number(no["dy"]) ?? old?.Dy ?? 0);
            if (text.Length == 0) { if (at >= 0) notes.RemoveAt(at); }
            else if (at >= 0) notes[at] = note;
            else notes.Add(note);
            s = s with { Notes = notes };
        }
        if ((string?)a["removeNote"] is { } rid) s = s with { Notes = s.Notes.Where(n => n.Id != rid).ToList() };
        return s;
    }
}
