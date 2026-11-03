using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Steel;

/// <summary>
/// The steel module (docs/plan-steel.md, decisions 266, 268), stage 1: steel members with configurable ends.
/// <para>
/// It owns <c>LapinSteelMember</c> and <c>LapinSteelMemberType</c>, written to IFC as IfcMember and IfcMemberType (Studio
/// owns IfcMember for its solids classed so, and a class has one owner): a member runs along a straight axis in space (its ends' points kept
/// relative to its storey's level), its section from its type (any of the structure module's sections, the catalogue's
/// hot-rolled, hollow and cold-formed profiles), turned about its axis, its insertion point on the axis. The structure
/// module's beams and columns stay its own (a class has one owner); a straight one of a steel profile takes ends of the
/// same kind, kept on it under <see cref="SteelEnd.Key"/>, and this module, as a geometry filter, builds its body from
/// them in place of the structure's. Every end is a rule (an extension, a cut, a mitre or a fit on another member by its
/// id, a cope); the body is derived from it, so a member mitred or fitted to another follows it when it moves.
/// </para>
/// <para>
/// IFC: the body as constructive solids (<see cref="IIfcSweptBodies"/>), the section extruded, clipped by the ends'
/// planes, the copes as openings. Convert to member reads a drafted closed outline as a band (<see cref="SketchBands"/>).
/// </para>
/// <para>
/// Portal frame halls (<see cref="Halls"/>, decision 270) and the connections of their frames, or of any members that
/// meet (<see cref="SteelNodes"/>, decision 273): column bases, eaves, apexes, splices, gable posts, their plates, bolts
/// and anchors derived from their parameters; the Steel parts schedule.
/// </para>
/// </summary>
public sealed class SteelModule : ILapinModule, IGeometryFilter, IIfcSweptBodies, IIfcSystems, IIfcConnections
{
    public string Id => "steel";
    public string Name => "Steel";

    /// <summary>A steel member, exported as an IfcMember: Studio owns IfcMember for its solids classed so (a class has one owner).</summary>
    public const string Member = "LapinSteelMember";
    /// <summary>A steel member's type, exported as an IfcMemberType.</summary>
    public const string MemberType = "LapinSteelMemberType";
    public static readonly string[] Predefined = ["MEMBER", "BRACE", "CHORD", "COLLAR", "MULLION", "PLATE", "POST", "PURLIN", "RAFTER", "STRINGER", "STRUT", "STUD"];

    public void Register(IModuleRegistry r)
    {
        r.Class(new(Member, "Member",
        [
            new("rotation", "Rotation", "angle"),
            new("insertion", "Insertion point", "choice", Choices: MemberAxis.Insertions),
        ], "#8a96a3"));
        r.Class(new(MemberType, "Member type", [], "#8a96a3", Kind: "type"));
        r.Geometry(Member, (e, m, s) => Geometry(e, m, s));
        r.Quantities(Member, Quantities);
        r.Service<IGeometryFilter>(this);
        r.Service<IIfcSweptBodies>(this);
        r.Service<IIfcSystems>(this);
        r.Service<IIfcConnections>(this);
        HallCommands.Register(r);
        NodeCommands.Register(r);

        r.Command("createType", "New member type", (ctx, a) =>
        {
            var section = Section.Parse(a["section"] ?? throw new CommandException("A member type needs a section."));
            return new JsonObject { ["id"] = CreateType(ctx, (string?)a["name"], section, (string?)a["material"]).Id };
        });

        // A member type's section or name changed: its members follow; a type named after its section takes the new one's name.
        r.Command("updateType", "Change member type", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["id"] ?? "") is { Class: MemberType } t ? t : throw new CommandException("There is no such member type.");
            var attrs = type.CloneAttrs();
            var auto = Section.From(type.Attrs["section"]) is { } old && type.Name == $"Member {old.Label}";
            Section? section = null;
            if (a["section"] != null) attrs["section"] = (section = Section.Parse(a["section"])).ToJson();
            var name = (string?)a["name"] is { Length: > 0 } n ? n.Trim() : auto && section != null ? $"Member {section.Label}" : type.Name;
            ctx.Replace(type.With(name: name, attrs: attrs));
            return null;
        });

        // A member along two points: in plan (no heights) on the storey's top, the section's top on its axis, as a beam; with heights, centred on its axis.
        r.Command("addMember", "Member", (ctx, a) =>
        {
            var storey = Spatial.Require(ctx.Model, (string?)a["storeyId"] ?? Spatial.Storeys(ctx.Model).FirstOrDefault()?.Id, IfcClass.BuildingStorey);
            var top = storey.Num("height", 3);
            var planned = a["z1"] == null && a["z2"] == null;
            var s = new Vec3(Args.Number(a, "x1"), Args.Number(a, "y1"), Json.Number(a["z1"]) ?? top);
            var e = new Vec3(Args.Number(a, "x2"), Args.Number(a, "y2"), Json.Number(a["z2"]) ?? Json.Number(a["z1"]) ?? top);
            if ((e - s).Length < 1e-3) throw new CommandException("A member needs two different points.");
            var type = TypeFor(ctx, (string?)a["typeId"]);
            var insertion = (string?)a["insertion"] ?? (planned ? "top" : "center");
            var id = Add(ctx, storey, type, s, e, Json.Number(a["rotation"]) ?? 0, insertion, (string?)a["predefinedType"]);
            return new JsonObject { ["id"] = id };
        });

        // A member's ends moved (x1…z2, each kept when not given), turned or inserted otherwise.
        r.Command("updateMember", "Move member", (ctx, a) =>
        {
            var e = ctx.Model.Get((string?)a["id"] ?? "") is { Class: Member } x ? x : throw new CommandException("Not a member.");
            var (s, t) = (MemberAxes.Point(e, "start"), MemberAxes.Point(e, "end"));
            s = new Vec3(Json.Number(a["x1"]) ?? s.X, Json.Number(a["y1"]) ?? s.Y, Json.Number(a["z1"]) ?? s.Z);
            t = new Vec3(Json.Number(a["x2"]) ?? t.X, Json.Number(a["y2"]) ?? t.Y, Json.Number(a["z2"]) ?? t.Z);
            if ((t - s).Length < 1e-3) throw new CommandException("A member needs two different points.");
            var attrs = e.CloneAttrs();
            attrs["start"] = P3(s);
            attrs["end"] = P3(t);
            if (Json.Number(a["rotation"]) is { } rot) attrs["rotation"] = rot;
            if ((string?)a["insertion"] is { } ins) attrs["insertion"] = MemberAxis.Insertions.Contains(ins) ? ins : throw new CommandException("A member's axis runs through its section's center, top or bottom.");
            ctx.Replace(e.With(attrs: attrs));
            return null;
        });

        // The ends of members, or of steel beams and columns: {ids, start?, end?}; an end given as null is square again.
        r.Command("setEnds", "Member ends", (ctx, a) =>
        {
            var ids = Args.Ids(a);
            if (ids.Count == 0) throw new CommandException("Select the members whose ends to set.");
            foreach (var id in ids)
            {
                var e = ctx.Model.Get(id) ?? throw new CommandException($"There is no {id}.");
                if (e.Class is not (Member or IfcClass.Beam or IfcClass.Column) || !MemberAxes.IsSteel(ctx.Model, e))
                    throw new CommandException($"„{e.Name}” is not a steel member: only members and straight beams and columns of a steel profile take ends.");
                var (start, end) = SteelEnd.Of(e);
                if (a.ContainsKey("start")) start = SteelEnd.Parse(a["start"]);
                if (a.ContainsKey("end")) end = SteelEnd.Parse(a["end"]);
                if (start.Ref == id || end.Ref == id) throw new CommandException("A member's end cannot be mitred or fitted to itself.");
                var attrs = e.CloneAttrs();
                if (start == SteelEnd.Square && end == SteelEnd.Square) attrs.Remove(SteelEnd.Key);
                else attrs[SteelEnd.Key] = SteelEnd.Json2(start, end);
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // Two members mitred at the corner where they meet: each end nearest the other mitred with it.
        r.Command("mitre", "Mitre", (ctx, a) => { Pair(ctx, a, "mitre"); return null; });
        // The first member fitted to the second's face at its end nearest it.
        r.Command("fit", "Fit", (ctx, a) => { Pair(ctx, a, "fit"); return null; });

        r.Command("setType", "Change type", (ctx, a) =>
        {
            var type = ctx.Model.Get((string?)a["typeId"] ?? "") is { Class: MemberType } t ? t : throw new CommandException("There is no such member type.");
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Member } x ? x : throw new CommandException("Only a member takes a member type.");
                // A member named as its type was is named as its new one.
                var named = e.Name == ctx.Model.RelatingOf(e.Id, IfcClass.RelDefinesByType)?.Name;
                ctx.Relate(IfcClass.RelDefinesByType, type.Id, e.Id);
                if (named) ctx.Replace(ctx.Model.Get(e.Id)!.With(name: type.Name));
            }
            return null;
        });

        // Flipped: the section mirrored across its up (an angle or a channel faces the other way).
        r.Command("flip", "Flip", (ctx, a) =>
        {
            foreach (var id in Args.Ids(a))
            {
                var e = ctx.Model.Get(id) is { Class: Member } x ? x : throw new CommandException("Only a member flips here.");
                var attrs = e.CloneAttrs();
                if (e.Bool("flipped")) attrs.Remove("flipped"); else attrs["flipped"] = true;
                ctx.Replace(e.With(attrs: attrs));
            }
            return null;
        });

        // Convert to member: {ids (drafted closed outlines), keep?, typeId?} → {ids (the members), skipped: [{id, problem}]}.
        r.Command("convertSketch", "Convert to member", (ctx, a) =>
        {
            var made = new JsonArray();
            var skipped = new JsonArray();
            var keep = a["keep"] is JsonValue kv && kv.TryGetValue<bool>(out var k) && k;
            foreach (var id in Args.Ids(a))
            {
                var (member, problem) = SketchMembers.Convert(ctx, id, (string?)a["typeId"]);
                if (member == null) { skipped.Add((JsonNode)new JsonObject { ["id"] = id, ["problem"] = problem }); continue; }
                made.Add((JsonNode)member);
                if (!keep) ctx.Remove(id);
            }
            if (made.Count == 0)
                throw new CommandException(skipped.Count == 1 ? (string)skipped[0]!["problem"]! : $"None of the selection is a closed outline of a band (two long parallel edges, an edge across each end): {(string)skipped[0]!["problem"]!}");
            return new JsonObject { ["ids"] = made, ["skipped"] = skipped };
        });

        // What an outline would make, for the interface to say before converting: {id} → {width, length, profile} or {problem}.
        r.Query("readSketch", (q, a) =>
        {
            var (band, _, problem) = SketchMembers.Read(q.Model, q.Services, (string?)a["id"] ?? "");
            if (band == null) return new JsonObject { ["problem"] = problem };
            return new JsonObject { ["width"] = Math.Round(band.Width, 6), ["length"] = Math.Round(band.Length, 6) };
        });

        r.Catalog("profiles", new JsonArray(Section.Catalogue.Values.Select(p => (JsonNode)p.ToJson()).ToArray()));
    }

    static JsonArray P3(Vec3 p) => new(Math.Round(p.X, 9), Math.Round(p.Y, 9), Math.Round(p.Z, 9));

    public static Entity CreateType(IEdit edit, string? name, Section section, string? material = null)
    {
        var type = new Entity(edit.NewId(), MemberType, string.IsNullOrWhiteSpace(name) ? $"Member {section.Label}" : name.Trim(),
            new JsonObject { ["section"] = section.ToJson(), ["material"] = Materials.Ensure(edit, material ?? (section.IsProfile ? "Steel" : "Reinforced concrete")).Id }, "MEMBER");
        edit.Add(type);
        return type;
    }

    /// <summary>The type asked for, else the project's first member type, else a new IPE 200.</summary>
    public static Entity TypeFor(IEdit edit, string? typeId)
    {
        if (typeId != null) return edit.Model.Get(typeId) is { Class: MemberType } t ? t : throw new CommandException("There is no such member type.");
        return edit.Model.OfClass(MemberType).FirstOrDefault() ?? CreateType(edit, null, Section.Catalogue["IPE 200"]);
    }

    /// <summary>A member on a storey from world points (kept relative to its level).</summary>
    public static string Add(IEdit edit, Entity storey, Entity type, Vec3 start, Vec3 end, double rotation, string insertion, string? predefined = null, SteelEnd? s = null, SteelEnd? e = null)
    {
        var pre = predefined?.ToUpperInvariant() is { } p && Predefined.Contains(p) ? p : "MEMBER";
        var attrs = new JsonObject { ["exportAs"] = "IfcMember", ["start"] = P3(start), ["end"] = P3(end), ["rotation"] = Math.Round(rotation, 9), ["insertion"] = MemberAxis.Insertions.Contains(insertion) ? insertion : "center" };
        if (s != null || e != null) attrs[SteelEnd.Key] = SteelEnd.Json2(s ?? SteelEnd.Square, e ?? SteelEnd.Square);
        var member = new Entity(edit.NewId(), Member, type.Name, attrs, pre);
        edit.Add(member);
        Spatial.Contain(edit, storey.Id, member.Id);
        edit.Relate(IfcClass.RelDefinesByType, type.Id, member.Id);
        return member.Id;
    }

    /// <summary>
    /// Two members, ends mitred or the first fitted: {ids: [a, b]}. The end of each nearest the other is the one set (a fit
    /// sets only the first's); the other end keeps what it had.
    /// </summary>
    static void Pair(ICommandContext ctx, JsonObject a, string cut)
    {
        var ids = Args.Ids(a);
        if (ids.Count != 2) throw new CommandException(cut == "mitre" ? "Select the two members to mitre." : "Select the member to fit, then the one it is fitted to.");
        var (ea, eb) = (ctx.Model.Get(ids[0]), ctx.Model.Get(ids[1]));
        if (ea == null || eb == null || MemberAxes.Of(ctx.Model, ea) is not { } xa || MemberAxes.Of(ctx.Model, eb) is not { } xb || !MemberAxes.IsSteel(ctx.Model, ea) || !MemberAxes.IsSteel(ctx.Model, eb))
            throw new CommandException("Both must be steel members (members, or straight beams and columns of a steel profile).");
        void Set(Entity e, MemberAxis self, MemberAxis other, string refId)
        {
            var mid = (other.Start + other.End) * 0.5;
            var atEnd = (self.End - mid).Length < (self.Start - mid).Length;
            var (s, t) = SteelEnd.Of(e);
            var end = new SteelEnd(Cut: cut, Ref: refId);
            var attrs = e.CloneAttrs();
            attrs[SteelEnd.Key] = atEnd ? SteelEnd.Json2(s, end) : SteelEnd.Json2(end, t);
            ctx.Replace(e.With(attrs: attrs));
        }
        Set(ea, xa, xb, eb.Id);
        if (cut == "mitre") Set(eb, xb, xa, ea.Id);
    }

    // ------------------------------------------------------------ geometry

    static ElementGeometry? Geometry(Entity e, IModelView m, IServices? services)
    {
        if (MemberAxes.Of(m, e) is not { } ax) return new ElementGeometry([], Diagnostics: ["The member has no length."]);
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        var material = type?.Str("material");
        return Body(e, m, services, ax, e.Name, Materials.ColorOf(m, material), material);
    }

    /// <summary>The member's body with its ends, its plan outline and its snaps.</summary>
    static ElementGeometry Body(Entity e, IModelView m, IServices? services, MemberAxis ax, string name, string color, string? material)
    {
        var (start, end) = SteelEnd.Of(e);
        var built = SteelBody.Build(m, ax, start, end, services?.Get<ISolidBooleans>());
        var problems = built.Problems.Count > 0 ? built.Problems : null;
        if (built.Solid is not { } solid) return new ElementGeometry([], Diagnostics: problems);
        var u = ax.U;
        // In plan: what is seen from above. A vertical member: its section where it stands; any other, its body's shadow.
        IReadOnlyList<PlanItem> plan = Math.Abs(u.Z) > 1 - 1e-6
            ? [new PlanItem("cut", true, ax.Profile.Outline.Select(p => ax.At(p, built.S0).Xy).ToList())]
            : [new PlanItem("outline", true, Polygon2.ConvexHull(solid.Vertices.Select(v => v.Xy)))];
        var a = (ax.Start + u * built.S0).Xy;
        var b = (ax.Start + u * built.S1).Xy;
        var parts = new List<GeometryPart> { new(name, color, solid, material, "body") };
        // A member's haunches (decision 270): the web and the flange of each, parts of its own.
        if (e.Class == Member)
        {
            var hp = new List<string>();
            foreach (var (at, _, shape) in Haunches.Of(e, ax, built, hp))
            {
                if (Haunches.Solid(ax, shape.WebOutline, shape.Web, "haunch") is { } w) parts.Add(new("Haunch", color, w, material, $"haunch:{at}:web"));
                if (Haunches.Solid(ax, shape.FlangeOutline, shape.Width, "haunch") is { } f) parts.Add(new("Haunch", color, f, material, $"haunch:{at}:flange"));
            }
            if (hp.Count > 0) problems = [.. problems ?? [], .. hp];
        }
        return new ElementGeometry(parts, plan, [a, b], problems);
    }

    static IEnumerable<QuantitySet> Quantities(Entity e, ElementGeometry? g, IModelView m)
    {
        if (g == null || g.Parts.Count == 0 || MemberAxes.Of(m, e) is not { } ax) yield break;
        var (start, end) = SteelEnd.Of(e);
        var built = SteelBody.Build(m, ax, start, end, null);
        // The member's own body: its haunches are pieces welded on (their weight in the hall's summary).
        var net = g.Parts.Where(p => p.Key == "body").Sum(p => Measure.Volume(p.Solid));
        var area = ax.Section.Area;
        yield return new QuantitySet("Qto_MemberBaseQuantities",
        [
            new("Length", built.Length, "LENGTH"),
            new("CrossSectionArea", area, "AREA", "Cross section"),
            new("GrossVolume", area * built.Length, "VOLUME", "Gross volume"),
            new("NetVolume", net, "VOLUME", "Net volume"),
            new("NetWeight", net * SteelDensity, "WEIGHT", "Net weight"),
        ]);
    }

    /// <summary>Steel's density, kg/m³.</summary>
    public const double SteelDensity = 7850;

    /// <summary>A steel beam or column with ends: its body built from them, in place of the structure's (which knows nothing of them).</summary>
    public ElementGeometry? Apply(Entity entity, ElementGeometry geometry, IModelView model, IServices services)
    {
        if (entity.Class is not (IfcClass.Beam or IfcClass.Column) || entity.Attrs[SteelEnd.Key] is not JsonObject) return null;
        if (MemberAxes.Of(model, entity) is not { Section.IsProfile: true } ax || geometry.Parts.Count == 0) return null;
        var part = geometry.Parts[0];
        var built = Body(entity, model, services, ax, part.Name, part.Color, part.Material);
        if (built.Parts.Count == 0) return geometry with { Diagnostics = [.. geometry.Diagnostics ?? [], .. built.Diagnostics ?? []] };
        // The structure's own snaps (a beam's column centres) stay.
        return built with { Snaps = [.. geometry.Snaps ?? [], .. built.Snaps ?? []] };
    }

    public IfcSweptBody? BodyOf(IModelView model, Entity element)
    {
        if (element.Class is not (Member or IfcClass.Beam or IfcClass.Column)) return null;
        if (element.Class != Member && element.Attrs[SteelEnd.Key] is not JsonObject) return null;
        if (MemberAxes.Of(model, element) is not { } ax || element.Class != Member && !ax.Section.IsProfile) return null;
        var (start, end) = SteelEnd.Of(element);
        if (SteelBody.Swept(model, ax, start, end) is not { } swept) return null;
        if (element.Class != Member || Haunch.Of(element) is (null, null)) return swept;
        // The haunches: a piece cut from the section (an IfcMember), or a web and a flange plate (IfcPlate); with the member, an assembly.
        var built = SteelBody.Build(model, ax, start, end, null);
        var parts = new List<IfcSweptPart>();
        foreach (var (end2, h, shape) in Haunches.Of(element, ax, built, []))
        {
            var web = Haunches.Sweep(ax, shape.WebOutline, shape.Web);
            var flange = Haunches.Sweep(ax, shape.FlangeOutline, shape.Width);
            if (h.Kind == "cut") parts.Add(new IfcSweptPart($"haunch:{end2}", "Haunch", "IfcMember", "USERDEFINED", "Haunch", [web, flange]));
            else
            {
                parts.Add(new IfcSweptPart($"haunch:{end2}:web", "Haunch web", "IfcPlate", "USERDEFINED", "Haunch web plate", [web]));
                parts.Add(new IfcSweptPart($"haunch:{end2}:flange", "Haunch flange", "IfcPlate", "USERDEFINED", "Haunch flange plate", [flange]));
            }
        }
        return parts.Count == 0 ? swept : swept with { Parts = parts, Assembly = "Member with haunches" };
    }

    /// <summary>The project's halls as IFC's building systems: LOADBEARING, their members assigned, their spec as Lapin_SteelHall.</summary>
    public IEnumerable<IfcSystem> Systems(IModelView model)
    {
        foreach (var hall in model.OfClass(Halls.Class))
        {
            var s = Halls.SpecOf(hall);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string L(double v) => v.ToString("0.###", inv);
            var props = new List<(string, string)>
            {
                ("Span", L(s.Span)), ("Length", L(s.Length)), ("Bays", string.Join(" ", s.Bays.Select(L))), ("EavesHeight", L(s.Eaves)), ("RoofPitch", L(s.Pitch)),
                ("Roof", s.Roof), ("Column", s.Column.Label), ("Rafter", s.Rafter.Label), ("Gable", s.Gable), ("ColumnBase", s.Base),
            };
            yield return new IfcSystem(hall.Id, hall.Name, "LOADBEARING",
                [.. Halls.Members(model, hall.Id).Values.Select(e => e.Id), .. Halls.Nodes(model, hall.Id).Values.Select(e => e.Id)], "Lapin_SteelHall", props);
        }
    }

    /// <summary>
    /// The steel connections as IFC's realizing elements (decision 273): an eaves and a gable post's between the member it is
    /// fixed to and the one that comes to it, an apex and a splice between their two members, a base between the footing
    /// under it (when there is one) and its column.
    /// </summary>
    public IEnumerable<IfcConnection> Connections(IModelView model)
    {
        foreach (var node in model.OfClass(SteelNodes.Class))
        {
            var js = SteelNodes.MembersOf(node);
            var kind = SteelNodes.KindOf(node);
            if (kind == "base")
            {
                if (js.Count > 0 && SteelNodes.Build(model, node).Footing is { } footing) yield return new IfcConnection(node.Id, footing.Id, js[0].Id, node.Name);
            }
            else if (js.Count >= 2) yield return new IfcConnection(node.Id, js[0].Id, js[1].Id, node.Name);
        }
    }
}
