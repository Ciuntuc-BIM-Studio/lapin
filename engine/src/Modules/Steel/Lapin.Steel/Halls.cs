using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// Portal frame halls (docs/plan-steel.md, decision 270). A hall is a <c>LapinSteelHall</c> keeping its spec
/// (<see cref="HallSpec"/>) and aggregating the members it made (IfcRelAggregates), each knowing its place in it
/// (<c>hallRole</c>: "f2:col:a", "f0:raf:b", "f0:post:3"), so a hall changed is built again over the same members, which
/// keep their ids; and its own grid (an IfcGrid: axes 1, 2… at the frames, A and B at the columns). In IFC the hall is an
/// IfcBuildingSystem (LOADBEARING) its members are assigned to.
/// <para>
/// A frame is two columns (the structure module's IfcColumn of a steel profile, their web in the frame's plane, the top
/// cut along the roof's slope) and two rafters meeting at the apex, mitred (duo-pitch), or one rafter from the low column
/// to the high one (mono-pitch): steel members (IfcMember RAFTER) with their axis on their top, fitted to the columns'
/// inner faces, with their haunches at the eaves and at the apex. A gable is a regular frame, or gable posts at a spacing
/// (corner posts too) under a simple rafter that runs to the walls' outer line, the posts fitted to its underside.
/// </para>
/// </summary>
public static class Halls
{
    public const string Class = "LapinSteelHall";
    public const string RoleKey = "hallRole";

    /// <summary>How far the grid's axes run past the hall.</summary>
    const double AxisOverrun = 1.5;

    /// <summary>
    /// A member a hall plans: its role, what it is (column, rafter, post; purlin, rail, eaves, brace, fly) and its entity's
    /// attributes but the ends, which a frame's member takes from its role and a secondary one is planned with.
    /// </summary>
    internal sealed record Planned(string Role, string Kind, string Class, Section Section, JsonObject Attrs, string Predefined, (SteelEnd Start, SteelEnd End)? OwnEnds = null);

    /// <summary>The members a hall is made of (its own, by their role).</summary>
    public static Dictionary<string, Entity> Members(IModelView m, string hallId)
    {
        var o = new Dictionary<string, Entity>();
        foreach (var e in m.RelatedOf(hallId, IfcClass.RelAggregates))
            if (e.Str(RoleKey) is { } role) o.TryAdd(role, e);
        return o;
    }

    /// <summary>The connections a hall made (its own, by their place: "base@f1:col:a@start").</summary>
    public static Dictionary<string, Entity> Nodes(IModelView m, string hallId)
    {
        var o = new Dictionary<string, Entity>();
        foreach (var e in m.RelatedOf(hallId, IfcClass.RelAggregates))
            if (e.Class == SteelNodes.Class && e.Str(SteelNodes.RoleKey) is { } role) o.TryAdd(role, e);
        return o;
    }

    /// <summary>The hall a member belongs to, if any.</summary>
    public static Entity? Of(IModelView m, string memberId) => m.RelatingOf(memberId, IfcClass.RelAggregates) is { Class: Class } h ? h : null;

    /// <summary>The spec a hall keeps.</summary>
    public static HallSpec SpecOf(Entity hall) => HallSpec.From(hall.Attrs);

    static Vec2 Dx(HallSpec s) => new(Math.Cos(s.Angle * Math.PI / 180), Math.Sin(s.Angle * Math.PI / 180));
    static Vec2 Dy(HallSpec s) => new(-Math.Sin(s.Angle * Math.PI / 180), Math.Cos(s.Angle * Math.PI / 180));

    /// <summary>A point of the hall (x across the span from axis A, y along it from axis 1) in plan.</summary>
    public static Vec2 Plan(HallSpec s, double x, double y) => new Vec2(s.X, s.Y) + Dx(s) * x + Dy(s) * y;

    static JsonArray P3(Vec2 p, double z) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9), Math.Round(z, 9));

    /// <summary>
    /// Builds the hall's members and grid from its spec over what it had: a member of the same role keeps its id (and a
    /// haunch set on its own stays), one no longer needed goes, a new one is added. The hall itself is replaced by the
    /// caller with its new spec first.
    /// </summary>
    public static void Build(IEdit edit, Entity storey, Entity hall, HallSpec s)
    {
        var existing = Members(edit.Model, hall.Id);
        var plan = Plan(s);
        // Ids first: the ends of one member name the others.
        var ids = new Dictionary<string, string>();
        foreach (var p in plan) ids[p.Role] = existing.TryGetValue(p.Role, out var e) && e.Class == p.Class ? e.Id : edit.NewId();
        var types = new Dictionary<(string, Section), Entity>();
        Entity TypeOf(Planned p)
        {
            if (types.TryGetValue((p.Class, p.Section), out var t)) return t;
            var key = p.Section.ToJson();
            t = p.Class == IfcClass.Column
                ? edit.Model.OfClass(IfcClass.ColumnType).FirstOrDefault(x => JsonNode.DeepEquals(x.Attrs["section"], key) || Section.From(x.Attrs["section"]) == p.Section) ?? Columns.CreateType(edit, null, p.Section, null, "Steel")
                : edit.Model.OfClass(SteelModule.MemberType).FirstOrDefault(x => JsonNode.DeepEquals(x.Attrs["section"], key) || Section.From(x.Attrs["section"]) == p.Section) ?? SteelModule.CreateType(edit, null, p.Section, "Steel");
            return types[(p.Class, p.Section)] = t;
        }
        foreach (var p in plan)
        {
            var id = ids[p.Role];
            var attrs = (JsonObject)p.Attrs.DeepClone();
            attrs[RoleKey] = p.Role;
            var (start, end) = p.OwnEnds ?? Ends(s, p.Role, ids);
            attrs[SteelEnd.Key] = SteelEnd.Json2(start, end);
            if (p.Kind == "rafter" && HaunchesOf(s, p.Role) is { } planned)
            {
                var (hs, he) = planned;
                // A haunch set on its own (its kind, its plates) stays as it was.
                var (os, oe) = existing.TryGetValue(p.Role, out var old) ? Haunch.Of(old) : (null, null);
                if (os?.Own == true) hs = os;
                if (oe?.Own == true) he = oe;
                if (Haunch.Json2(hs, he) is { } hj) attrs[Haunch.Key] = hj;
            }
            var type = TypeOf(p);
            if (edit.Model.Get(id) is { } was)
            {
                // The name follows the type unless it was given one of its own.
                var named = was.Name == edit.Model.RelatingOf(was.Id, IfcClass.RelDefinesByType)?.Name;
                edit.Replace(new Entity(id, p.Class, named ? type.Name : was.Name, attrs, p.Predefined));
            }
            else edit.Add(new Entity(id, p.Class, type.Name, attrs, p.Predefined));
            if (Spatial.StoreyOf(edit.Model, id)?.Id != storey.Id) Spatial.Contain(edit, storey.Id, id);
            if (edit.Model.RelatingOf(id, IfcClass.RelDefinesByType)?.Id != type.Id) edit.Relate(IfcClass.RelDefinesByType, type.Id, id);
            if (edit.Model.RelatingOf(id, IfcClass.RelAggregates)?.Id != hall.Id) edit.Relate(IfcClass.RelAggregates, hall.Id, id);
        }
        foreach (var (role, e) in existing) if (!ids.ContainsKey(role) || ids[role] != e.Id) edit.Remove(e.Id);
        BuildNodes(edit, storey, hall, s, plan, ids);
        Grid(edit, hall, s);
    }

    /// <summary>A connection a hall plans: its place, kind, members (by role, with their ends), the hall's parameters for it and what else it keeps (a gusset's depth).</summary>
    internal sealed record PlannedNode(string Role, string Kind, List<(string Role, string? End)> Members, NodeParams Params, JsonObject? Extra = null);

    /// <summary>
    /// The connections of a hall's frames (decision 273): a base under every column and post (a post's pinned), an eaves
    /// where each rafter comes to a column, an apex where two rafters meet, a gable post's under each post; none when the
    /// hall has its connections off.
    /// </summary>
    static List<PlannedNode> PlanNodes(HallSpec s, IEnumerable<Planned> members)
    {
        var o = new List<PlannedNode>();
        if (!s.Nodes) return o;
        // The secondary steel's connections (decision 275): its cleats, gussets and fly braces' bolts.
        o.AddRange(HallSecondary.Plan(s).Nodes);
        var baseParams = new NodeParams(Base: s.Base, Anchor: s.Anchor).Over(s.NodeParamsOf("base"));
        var postBase = new NodeParams(Base: "pinned", Anchor: s.Anchor, AnchorLength: s.NodeParamsOf("base").AnchorLength, Grout: s.NodeParamsOf("base").Grout);
        var roles = members.Select(p => p.Role).ToHashSet();
        foreach (var p in members)
        {
            var bits = p.Role.Split(':');
            var f = bits[0];
            switch (p.Kind)
            {
                case "column":
                    o.Add(new($"base@{p.Role}@start", "base", [(p.Role, "start")], baseParams));
                    break;
                case "post":
                    o.Add(new($"base@{p.Role}@start", "base", [(p.Role, "start")], postBase));
                    var raf = s.Mono ? $"{f}:raf" : $"{f}:raf:{(Halls.PostX(s, p.Role) <= s.ApexX + 1e-9 ? "a" : "b")}";
                    o.Add(new($"post@{p.Role}@end", "post", [(raf, null), (p.Role, "end")], s.NodeParamsOf("post")));
                    break;
                case "rafter" when roles.Contains($"{f}:col:a") && !IsGable(s, f):
                    // A frame's rafter: its eaves at the column it starts from (a mono-pitch one at both).
                    var side = s.Mono ? "a" : bits[2];
                    o.Add(new($"eaves@{p.Role}@start", "eaves", [($"{f}:col:{side}", null), (p.Role, "start")], s.NodeParamsOf("eaves")));
                    if (s.Mono) o.Add(new($"eaves@{p.Role}@end", "eaves", [($"{f}:col:b", null), (p.Role, "end")], s.NodeParamsOf("eaves")));
                    break;
            }
            if (p.Kind == "rafter" && !s.Mono && bits.Length > 2 && bits[2] == "a")
                o.Add(new($"apex@{f}", "apex", [(p.Role, "end"), ($"{f}:raf:b", "end")], s.NodeParamsOf("apex")));
        }
        return o;
    }

    static bool IsGable(HallSpec s, string f) => s.Gable == "posts" && (f == "f0" || f == $"f{s.Bays.Count}");

    /// <summary>Where a gable's post (or corner post) stands across the span.</summary>
    static double PostX(HallSpec s, string role)
    {
        var bits = role.Split(':');
        return bits[1] == "col" ? (bits[2] == "a" ? 0 : s.Span) : s.PostStations[int.Parse(bits[2]) - 1];
    }

    /// <summary>
    /// Builds the hall's connections over those it had, as its members: one of the same place keeps its id (and its
    /// parameters when they were set on it), one no longer needed goes, a new one is added; each then writes its members'
    /// setbacks (their ends were just built again without them).
    /// </summary>
    static void BuildNodes(IEdit edit, Entity storey, Entity hall, HallSpec s, List<Planned> members, Dictionary<string, string> ids)
    {
        var existing = Nodes(edit.Model, hall.Id);
        var keep = new HashSet<string>();
        foreach (var p in PlanNodes(s, members))
        {
            // A connection whose members are not all planned (a fly brace's purlin past a gable) is not made.
            if (p.Members.Any(x => !ids.ContainsKey(x.Role))) continue;
            var joined = new JsonArray([.. p.Members.Select(x => (JsonNode)new SteelNodes.Joined(ids[x.Role], x.End).ToJson())]);
            var label = SteelNodes.Label(p.Kind);
            Entity node;
            if (existing.TryGetValue(p.Role, out var was))
            {
                var attrs = was.CloneAttrs();
                attrs["kind"] = p.Kind;
                attrs["objectType"] = label;
                attrs["members"] = joined;
                if (!was.Bool("own")) attrs["params"] = p.Params.ToJson();
                if (p.Extra != null) foreach (var (k, v) in p.Extra) attrs[k] = v?.DeepClone();
                node = was.With(attrs: attrs);
                edit.Replace(node);
                if (Spatial.StoreyOf(edit.Model, node.Id)?.Id != storey.Id) Spatial.Contain(edit, storey.Id, node.Id);
            }
            else
            {
                var attrs = new JsonObject
                {
                    ["exportAs"] = "IfcElementAssembly", ["objectType"] = label, ["kind"] = p.Kind, ["members"] = joined, ["params"] = p.Params.ToJson(), [SteelNodes.RoleKey] = p.Role,
                };
                if (p.Extra != null) foreach (var (k, v) in p.Extra) attrs[k] = v?.DeepClone();
                node = new Entity(edit.NewId(), SteelNodes.Class, label, attrs, "USERDEFINED");
                edit.Add(node);
                Spatial.Contain(edit, storey.Id, node.Id);
                edit.Relate(IfcClass.RelAggregates, hall.Id, node.Id);
            }
            keep.Add(node.Id);
            SteelNodes.Apply(edit, node);
        }
        foreach (var e in existing.Values) if (!keep.Contains(e.Id)) edit.Remove(e.Id);
    }

    /// <summary>The members a spec makes, frame by frame: their role, class, section and attributes (without their ends).</summary>
    static List<Planned> Plan(HallSpec s)
    {
        var o = new List<Planned>();
        var stations = s.Stations;
        var rot = s.Angle - 90;
        for (var i = 0; i < stations.Count; i++)
        {
            var y = stations[i];
            var gable = s.Gable == "posts" && (i == 0 || i == stations.Count - 1);
            var f = $"f{i}";
            if (!gable)
            {
                // The columns: their web in the frame's plane (their section's depth across the span), up to the rafters' top over their axis.
                foreach (var (side, x) in new[] { ("a", 0.0), ("b", s.Span) })
                    o.Add(new($"{f}:col:{side}", "column", IfcClass.Column, s.Column,
                        new JsonObject { ["at"] = new JsonArray(Plan(s, x, y).X, Plan(s, x, y).Y), ["rotation"] = Math.Round(rot, 9), ["height"] = Math.Round(s.Top(x), 9) }, "COLUMN"));
                o.AddRange(Rafters(s, f, y, s.Rafter, "rafter"));
            }
            else
            {
                // A gable of posts: the corner posts and those between, their strong axis across the gable wall, under the gable rafter.
                var xs = new List<double> { 0 };
                xs.AddRange(s.PostStations);
                xs.Add(s.Span);
                for (var k = 0; k < xs.Count; k++)
                {
                    var role = k == 0 ? $"{f}:col:a" : k == xs.Count - 1 ? $"{f}:col:b" : $"{f}:post:{k}";
                    var at = Plan(s, xs[k], y);
                    o.Add(new(role, "post", IfcClass.Column, s.Post,
                        new JsonObject { ["at"] = new JsonArray(at.X, at.Y), ["rotation"] = Math.Round(s.Angle, 9), ["height"] = Math.Round(s.Top(xs[k]), 9) }, "COLUMN"));
                }
                o.AddRange(Rafters(s, f, y, s.GableRafter, "rafter"));
            }
        }
        // The secondary steel (decision 275): purlins, rails, the eaves beam, bracing, fly braces.
        o.AddRange(HallSecondary.Plan(s).Members);
        return o;
    }

    /// <summary>A frame's rafters, their axis on their top, from the columns' axes to the apex (duo-pitch: a and b) or across (mono-pitch).</summary>
    static IEnumerable<Planned> Rafters(HallSpec s, string f, double y, Section section, string kind)
    {
        JsonObject A(double x1, double x2) => new()
        {
            ["exportAs"] = "IfcMember", ["start"] = P3(Plan(s, x1, y), s.Top(x1)), ["end"] = P3(Plan(s, x2, y), s.Top(x2)), ["rotation"] = 0.0, ["insertion"] = "top",
        };
        if (s.Mono) { yield return new($"{f}:raf", kind, SteelModule.Member, section, A(0, s.Span), "RAFTER"); yield break; }
        yield return new($"{f}:raf:a", kind, SteelModule.Member, section, A(0, s.ApexX), "RAFTER");
        yield return new($"{f}:raf:b", kind, SteelModule.Member, section, A(s.Span, s.ApexX), "RAFTER");
    }

    /// <summary>
    /// A member's ends by its role. A frame's column: its top cut along the slope over it, through the rafters' top on its
    /// axis. A frame's rafter: fitted to the column's inner face, mitred with the other at the apex (or fitted to the high
    /// column). A gable rafter: cut plumb at the walls' outer line (the frame columns' outer face), mitred at the apex; a
    /// gable post: fitted to the underside of the rafter over it.
    /// </summary>
    static (SteelEnd Start, SteelEnd End) Ends(HallSpec s, string role, IReadOnlyDictionary<string, string> ids)
    {
        var bits = role.Split(':');
        var f = bits[0];
        var gable = s.Gable == "posts" && (f == "f0" || f == $"f{s.Bays.Count}");
        var p = s.Pitch;
        string Raf(string side) => s.Mono ? ids[$"{f}:raf"] : ids[$"{f}:raf:{side}"];
        switch (bits[1])
        {
            case "col" when !gable:
                // The top's plane rises towards the apex: tilted back, away from it.
                var towards = bits[2] == "a" || s.Mono ? -p : p;
                return (SteelEnd.Square, new SteelEnd(Cut: "plane", ElevAngle: towards));
            case "col" or "post":
            {
                var x = bits[1] == "col" ? (bits[2] == "a" ? 0 : s.Span) : s.PostStations[int.Parse(bits[2]) - 1];
                return (SteelEnd.Square, new SteelEnd(Cut: "fit", Ref: Raf(x <= s.ApexX + 1e-9 || s.Mono ? "a" : "b")));
            }
            case "raf" when !gable:
            {
                var side = s.Mono ? "a" : bits[2];
                var col = ids[$"{f}:col:{side}"];
                var end = s.Mono ? new SteelEnd(Cut: "fit", Ref: ids[$"{f}:col:b"]) : new SteelEnd(Cut: "mitre", Ref: ids[$"{f}:raf:{(side == "a" ? "b" : "a")}"]);
                return (new SteelEnd(Cut: "fit", Ref: col), end);
            }
            default:
            {
                // A gable rafter reaches the walls' outer line, cut plumb (its plane turned back by the pitch).
                var reach = s.Column.Depth / 2 / Math.Cos(p * Math.PI / 180);
                var start = new SteelEnd(Extension: reach, Cut: "plane", ElevAngle: p);
                var end = s.Mono ? new SteelEnd(Extension: reach, Cut: "plane", ElevAngle: -p) : new SteelEnd(Cut: "mitre", Ref: ids[$"{f}:raf:{(bits[2] == "a" ? "b" : "a")}"]);
                return (start, end);
            }
        }
    }

    /// <summary>A rafter's haunches by its role: at the eaves (its start; a mono-pitch rafter's both ends) and at the apex; a gable rafter of posts has none.</summary>
    static (Haunch? Start, Haunch? End)? HaunchesOf(HallSpec s, string role)
    {
        var f = role.Split(':')[0];
        if (s.Gable == "posts" && (f == "f0" || f == $"f{s.Bays.Count}")) return null;
        return (s.EavesHaunch, s.Mono ? s.EavesHaunch : s.ApexHaunch);
    }

    /// <summary>The hall's own grid: axes 1, 2… across at the frames, A and B along at the columns, a little past the hall.</summary>
    static void Grid(IEdit edit, Entity hall, HallSpec s)
    {
        var axes = new JsonArray();
        JsonObject Axis(string tag, Vec2 a, Vec2 b) => new() { ["tag"] = tag, ["a"] = new JsonArray(Math.Round(a.X, 9), Math.Round(a.Y, 9)), ["b"] = new JsonArray(Math.Round(b.X, 9), Math.Round(b.Y, 9)) };
        var stations = s.Stations;
        for (var i = 0; i < stations.Count; i++)
            axes.Add((JsonNode)Axis((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Plan(s, -AxisOverrun, stations[i]), Plan(s, s.Span + AxisOverrun, stations[i])));
        axes.Add((JsonNode)Axis("A", Plan(s, 0, -AxisOverrun), Plan(s, 0, s.Length + AxisOverrun)));
        axes.Add((JsonNode)Axis("B", Plan(s, s.Span, -AxisOverrun), Plan(s, s.Span, s.Length + AxisOverrun)));
        if ((string?)hall.Attrs["gridId"] is { } gid && edit.Model.Get(gid) is { Class: IfcClass.Grid } grid)
        {
            var attrs = grid.CloneAttrs();
            attrs["axes"] = axes;
            edit.Replace(grid.With(attrs: attrs));
            return;
        }
        var made = new Entity(edit.NewId(), IfcClass.Grid, $"Grid {hall.Name}", new JsonObject { ["axes"] = axes });
        edit.Add(made);
        var ha = (edit.Model.Get(hall.Id) ?? hall).CloneAttrs();
        ha["gridId"] = made.Id;
        edit.Replace(hall.With(attrs: ha));
    }

    /// <summary>A new hall on a storey, built: its name the next "Hall n".</summary>
    public static Entity Add(IEdit edit, Entity storey, HallSpec s, string? style)
    {
        var n = 1 + edit.Model.OfClass(Class).Select(h => int.TryParse(h.Name.Split(' ')[^1], out var k) ? k : 0).DefaultIfEmpty(0).Max();
        var attrs = s.ToJson();
        attrs["exportAs"] = "IfcBuildingSystem";
        attrs["storeyId"] = storey.Id;
        if (style != null) attrs["style"] = style;
        var hall = new Entity(edit.NewId(), Class, $"Hall {n}", attrs, "LOADBEARING");
        edit.Add(hall);
        Build(edit, storey, hall, s);
        return edit.Model.Get(hall.Id)!;
    }

    /// <summary>A hall given a new spec (and style), its members built again.</summary>
    public static void Update(IEdit edit, Entity hall, HallSpec s, string? style)
    {
        var attrs = s.ToJson();
        foreach (var k in new[] { "exportAs", "storeyId", "gridId" }) if (hall.Attrs[k] is { } v) attrs[k] = v.DeepClone();
        if (style != null) attrs["style"] = style;
        var next = hall.With(attrs: attrs);
        edit.Replace(next);
        var storey = (string?)attrs["storeyId"] is { } sid && edit.Model.Get(sid) is { Class: IfcClass.BuildingStorey } st ? st
            : Members(edit.Model, hall.Id).Values.Select(m => Spatial.StoreyOf(edit.Model, m.Id)).FirstOrDefault(x => x != null) ?? Spatial.Storeys(edit.Model).First();
        Build(edit, storey, next, s);
    }

    // ------------------------------------------------------------ preview

    /// <summary>
    /// What a spec builds, without touching the model: the hall built in a model of its own (one storey at 0), then a typical
    /// frame's elevation (and a gable's when it has posts) as the members' outlines seen along the hall, the plan (grid,
    /// columns, rafters) in the hall's own x and y, and the summary: frames, heights, steel by kind (kg; the connections'
    /// plates, bolts and anchors too, counted), per m².
    /// </summary>
    public static JsonObject Preview(HallSpec s)
    {
        var store = new ModelStore();
        var history = new History(store);
        Entity? hall = null;
        history.Execute("Preview", e =>
        {
            var storey = new Entity(e.NewId(), IfcClass.BuildingStorey, "Preview", new JsonObject { ["elevation"] = 0, ["height"] = 3 });
            e.Add(storey);
            hall = Add(e, storey, s with { X = 0, Y = 0, Angle = 0 }, null);
        });
        var m = (IModelView)store;
        var members = Members(m, hall!.Id);
        string[] kinds = ["column", "rafter", "haunch", "post", "purlin", "rail", "eaves", "brace", "fly"];
        var weights = kinds.ToDictionary(k => k, _ => 0.0);
        var counts = kinds.ToDictionary(k => k, _ => 0);
        var problems = new List<string>();
        var typical = s.Bays.Count >= 2 ? 1 : 0;
        var frame = new JsonArray();
        var gable = new JsonArray();
        var plan = new JsonArray();
        foreach (var (role, e) in members.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var kind = KindOfRole(s, role);
            counts[kind]++;
            var fi = FrameOf(role);
            // Seen along the hall: a typical frame's own members, the purlins and rails of its first bay (their sections), its fly braces; a gable's rails.
            var inFrame = fi == typical || (kind is "purlin" or "rail" or "eaves" && !role.StartsWith("grail", StringComparison.Ordinal) && BayOf(role) is null or 1);
            var inGable = (fi == 0 && IsGable(s, "f0")) || role.StartsWith("grail:0:", StringComparison.Ordinal);
            foreach (var (part, solid) in Solids(m, e, problems))
            {
                var k = part == "haunch" ? "haunch" : kind;
                weights[k] += Measure.Volume(solid) * SteelModule.SteelDensity;
                if (inFrame && kind != "brace") frame.Add((JsonNode)Item(k, solid.Vertices.Select(v => new Vec2(v.X, v.Z))));
                if (inGable && kind is not ("brace" or "fly" or "purlin")) gable.Add((JsonNode)Item(k, solid.Vertices.Select(v => new Vec2(v.X, v.Z))));
                if (part != "haunch" && kind is not ("fly" or "rail")) plan.Add((JsonNode)Item(k, solid.Vertices.Select(v => v.Xy)));
            }
        }
        // The connections: their parts weighed and counted, a typical frame's drawn over it.
        var frameNodes = new JsonArray();
        var (nodeCount, plates, bolts, anchors, nodeKg) = (0, 0, 0, 0, 0.0);
        var byId = members.ToDictionary(x => x.Value.Id, x => x.Key);
        foreach (var (role, node) in Nodes(m, hall.Id))
        {
            nodeCount++;
            var built = SteelNodes.Build(m, node);
            var host = SteelNodes.MembersOf(node).Select(j => byId.GetValueOrDefault(j.Id)).FirstOrDefault(r => r != null && FrameOf(r) != null);
            var drawn = host != null && FrameOf(host) == typical && SteelNodes.KindOf(node) != "gusset";
            foreach (var part in built.Parts)
            {
                nodeKg += Measure.Volume(part.Solid) * SteelModule.SteelDensity;
                switch (SteelNodes.P(part.Properties ?? [], "Part")) { case "Plate" or "Anchor plate": plates++; break; case "Bolt": bolts++; break; case "Anchor": anchors++; break; }
                if (drawn) frameNodes.Add((JsonNode)Item("node", part.Solid.Vertices.Select(v => new Vec2(v.X, v.Z))));
            }
        }
        weights["node"] = nodeKg;
        var axes = new JsonArray();
        if ((string?)m.Get(hall.Id)!.Attrs["gridId"] is { } gid && m.Get(gid)?.Attrs["axes"] is JsonArray ga)
            foreach (var a in ga) axes.Add(a!.DeepClone());
        var total = weights.Values.Sum();
        var area = s.Span * s.Length;
        var w = new JsonObject();
        foreach (var (k, v) in weights) w[k] = Math.Round(v, 1);
        w["total"] = Math.Round(total, 1);
        var o = new JsonObject
        {
            ["frame"] = frame, ["frameNodes"] = frameNodes, ["plan"] = plan, ["axes"] = axes,
            ["summary"] = new JsonObject
            {
                ["frames"] = s.Frames, ["span"] = s.Span, ["length"] = Math.Round(s.Length, 6), ["area"] = Math.Round(area, 3),
                ["eaves"] = s.Eaves, ["apex"] = Math.Round(s.ApexHeight, 6),
                ["columns"] = counts["column"], ["rafters"] = counts["rafter"], ["posts"] = counts["post"],
                ["purlins"] = counts["purlin"], ["rails"] = counts["rail"], ["eavesBeams"] = counts["eaves"], ["braces"] = counts["brace"], ["flyBraces"] = counts["fly"],
                ["weights"] = w,
                ["nodes"] = nodeCount, ["plates"] = plates, ["bolts"] = bolts, ["anchors"] = anchors,
                ["perArea"] = Math.Round(total / area, 2),
            },
        };
        if (gable.Count > 0) o["gable"] = gable;
        if (problems.Count > 0) o["problem"] = problems[0];
        return o;
    }

    /// <summary>The frame a frame member's role names ("f2:col:a" → 2); null for the secondary steel's.</summary>
    public static int? FrameOf(string role) =>
        role.Length > 1 && role[0] == 'f' && char.IsDigit(role[1]) && int.TryParse(role.Split(':')[0][1..], out var i) ? i : null;

    /// <summary>The bay a secondary member that runs bay by bay is in ("pur:a:3:2" → 2); null for one that is not.</summary>
    static int? BayOf(string role)
    {
        var bits = role.Split(':');
        return bits[0] is "pur" or "rail" && bits.Length == 4 && int.TryParse(bits[3], out var b) ? b : bits[0] == "eb" && bits.Length == 3 && int.TryParse(bits[2], out var e) ? e : null;
    }

    /// <summary>What a hall member is by its role: column, rafter, post (a gable's), purlin, rail, eaves (beam), brace, fly (brace).</summary>
    public static string KindOfRole(HallSpec s, string role)
    {
        var bits = role.Split(':');
        return bits[0] switch
        {
            "pur" => "purlin", "rail" or "grail" => "rail", "eb" => "eaves", "rbr" or "wbr" => "brace", "fly" or "cfly" => "fly",
            _ => bits.Length > 1 && bits[1] == "raf" ? "rafter" : IsGable(s, bits[0]) ? "post" : "column",
        };
    }

    static JsonObject Item(string role, IEnumerable<Vec2> pts)
    {
        var hull = Polygon2.ConvexHull(pts);
        return new JsonObject { ["role"] = role, ["pts"] = new JsonArray([.. hull.SelectMany(p => new[] { Math.Round(p.X, 5), Math.Round(p.Y, 5) }).Select(v => (JsonNode)v)]) };
    }

    /// <summary>A hall member's solids, as its body is built: a column's or a member's body, and a rafter's haunch pieces.</summary>
    public static IEnumerable<(string Part, Solid Solid)> Solids(IModelView m, Entity e, List<string> problems)
    {
        if (MemberAxes.Of(m, e) is not { } ax) yield break;
        var (start, end) = SteelEnd.Of(e);
        var built = SteelBody.Build(m, ax, start, end, null);
        problems.AddRange(built.Problems);
        if (built.Solid is not { } body) yield break;
        yield return ("body", body);
        if (e.Class != SteelModule.Member) yield break;
        foreach (var (_, _, shape) in Haunches.Of(e, ax, built, problems))
        {
            if (Haunches.Solid(ax, shape.WebOutline, shape.Web, "haunch") is { } w) yield return ("haunch", w);
            if (Haunches.Solid(ax, shape.FlangeOutline, shape.Width, "haunch") is { } fl) yield return ("haunch", fl);
        }
    }
}
