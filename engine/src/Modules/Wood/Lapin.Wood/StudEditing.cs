using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Wood;

/// <summary>
/// A timber-frame wall's own exceptions to its type's frame, edited (audit 2026-11-15 §14, wood frame grips): its studs moved,
/// taken out or added (<see cref="Framing.StudEdits"/>, kept on the wall), and its openings' king and jack studs counted (the
/// opening's own rule, <c>woodFraming</c>). The frame stays a rule worked out on demand: a wall without exceptions is framed as
/// its type says, so saved projects open unchanged. In the frame elevation the studs, the outer kings and the outer jacks are
/// grips (<c>wood.frameGrip</c>); each change is one undo step.
/// </summary>
public static class StudEditing
{
    public static void Register(IModuleRegistry r)
    {
        // A wall's studs: {id (the wall), add?: [u], remove?: [u], move?: {from, to}, reset?: true} (u: a stud's centre along the wall from its start, m).
        r.Command("editStuds", "Edit studs", (ctx, a) =>
        {
            var (wall, f) = Framed(ctx, (string?)a["id"]);
            var t = f.Spec.Stud.W;
            var e = a["reset"] is JsonValue rv && rv.TryGetValue<bool>(out var reset) && reset ? new Framing.StudEdits([], []) : Framing.StudEdits.Of(wall) ?? new([], []);
            foreach (var u in Us(a["remove"])) e = Remove(e, u, t);
            foreach (var u in Us(a["add"])) e = Add(e, u, t, f);
            if (a["move"] is JsonObject mv)
                e = Move(e, Json.Number(mv["from"]) ?? throw new CommandException("Move which stud? from: its centre along the wall."), Json.Number(mv["to"]) ?? throw new CommandException("Move it where? to: along the wall."), t, f);
            Write(ctx, wall, e);
            return null;
        });

        // A grip of the frame elevation dragged: {id (the wall), viewId, key (stud:u | king:opening:L|R | jack:opening:L|R), point: [x, y] (the view's), remove?: true}.
        r.Command("frameGrip", "Frame grip", (ctx, a) =>
        {
            var (wall, f) = Framed(ctx, (string?)a["id"]);
            var key = (string?)a["key"] ?? throw new CommandException("Which grip?");
            var t = f.Spec.Stud.W;
            var remove = a["remove"] is JsonValue xv && xv.TryGetValue<bool>(out var rm) && rm;
            double U()
            {
                var view = Views.Find(ctx.Model, (string?)a["viewId"] ?? "") ?? throw new CommandException("The frame elevation is not in the project.");
                var frame = Views.Spec(ctx.Model, view, [], ctx.Services).Spec?.Frame ?? throw new CommandException("The view has no frame.");
                var p = Args.TryPoint(a["point"]) ?? throw new CommandException("Where to? point: [x, y].");
                var w = frame.Origin + frame.Right * p.X + frame.Up * p.Y;
                return Vec2.Dot(w.Xy - f.Start, f.Dir);
            }
            var parts = key.Split(':');
            switch (parts[0])
            {
                case "stud" when parts.Length == 2 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var from):
                    var e = Framing.StudEdits.Of(wall) ?? new([], []);
                    Write(ctx, wall, remove ? Remove(e, from, t) : Move(e, from, U(), t, f));
                    return null;
                case "king" or "jack" when parts.Length == 3 && f.Result.Openings.TryGetValue(parts[1], out var ro):
                {
                    var u = U();
                    var right = parts[2] == "R";
                    var rule = ro.Rule;
                    int Count(double edge) => Math.Clamp((int)Math.Round((right ? u - edge : edge - u) / t + 0.5), 0, 4);
                    rule = parts[0] == "jack"
                        ? rule with { Jacks = Count(right ? ro.U1 : ro.U0) }
                        : rule with { Kings = Count(right ? ro.U1 + rule.Jacks * t : ro.U0 - rule.Jacks * t) };
                    var opening = ctx.Model.Get(parts[1]) ?? throw new CommandException("The opening is not in the project.");
                    var attrs = opening.CloneAttrs();
                    attrs[WoodModule.OpeningKey] = rule.ToJson();
                    ctx.Replace(opening.With(attrs: attrs));
                    return null;
                }
                default:
                    throw new CommandException($"„{key}” is not a grip of the frame.");
            }
        });
    }

    static IEnumerable<double> Us(JsonNode? n) => n is JsonArray arr ? arr.Select(Json.Number).OfType<double>() : n is JsonValue && Json.Number(n) is { } one ? [one] : [];

    static (Entity Wall, WoodModule.WallFrame Frame) Framed(ICommandContext ctx, string? id)
    {
        var wall = ctx.Model.Get(id ?? "") is { Class: IfcClass.Wall } w ? w : throw new CommandException("Pick a timber-frame wall.");
        Pins.Refuse([wall]);
        var f = WoodModule.Of(ctx.Model, wall, ctx.Query.Geometry(wall.Id), ctx.Services) ?? throw new CommandException($"„{wall.Name}” is not a straight timber-frame wall.");
        return (wall, f);
    }

    static void Write(ICommandContext ctx, Entity wall, Framing.StudEdits e)
    {
        var attrs = ctx.Model.Get(wall.Id)!.CloneAttrs();
        if (e.Empty) attrs.Remove(Framing.StudEdits.Key); else attrs[Framing.StudEdits.Key] = e.ToJson();
        ctx.Replace(wall.With(attrs: attrs));
    }

    /// <summary>A stud taken out: one the wall added goes from its list; one on the module is listed as taken out.</summary>
    public static Framing.StudEdits Remove(Framing.StudEdits e, double u, double t)
    {
        if (e.Add.Any(x => Math.Abs(x - u) < t / 2)) return e with { Add = [.. e.Add.Where(x => Math.Abs(x - u) >= t / 2)] };
        return e.Remove.Any(x => Math.Abs(x - u) < t / 2) ? e : e with { Remove = [.. e.Remove, u] };
    }

    /// <summary>A stud added: where one of the module's was taken out, that one comes back; else the wall's own.</summary>
    public static Framing.StudEdits Add(Framing.StudEdits e, double u, double t, WoodModule.WallFrame f)
    {
        if (u < f.U0 || u > f.U1) throw new CommandException($"A stud stands on the wall: between {Math.Round(f.U0 * 1000)} and {Math.Round(f.U1 * 1000)} mm along it.");
        if (e.Remove.Any(x => Math.Abs(x - u) < 1e-3)) return e with { Remove = [.. e.Remove.Where(x => Math.Abs(x - u) >= 1e-3)] };
        if (f.Result.Members.Any(m => m.Upright && u + t / 2 > m.U0 + 1e-6 && u - t / 2 < m.U1 - 1e-6) && !e.Remove.Any(x => Math.Abs(x - u) < t / 2))
            throw new CommandException($"A stud at {Math.Round(u * 1000)} mm would stand on another member: move it clear.");
        return e with { Add = [.. e.Add, u] };
    }

    public static Framing.StudEdits Move(Framing.StudEdits e, double from, double to, double t, WoodModule.WallFrame f)
    {
        if (Math.Abs(from - to) < 1e-4) return e;
        var moved = Remove(e, from, t);
        // Clear of the others once this one is gone.
        if (f.Result.Members.Any(m => m.Upright && Math.Abs((m.U0 + m.U1) / 2 - from) >= t / 2 && to + t / 2 > m.U0 + 1e-6 && to - t / 2 < m.U1 - 1e-6))
            throw new CommandException($"A stud at {Math.Round(to * 1000)} mm would stand on another member: move it clear.");
        if (to < f.U0 || to > f.U1) throw new CommandException("A stud stays on its wall.");
        return moved.Remove.Any(x => Math.Abs(x - to) < 1e-3) ? moved with { Remove = [.. moved.Remove.Where(x => Math.Abs(x - to) >= 1e-3)] } : moved with { Add = [.. moved.Add, to] };
    }

    /// <summary>The grips a frame elevation offers, in its own coordinates: each stud, each opening's outer king and outer jack on both sides.</summary>
    public static JsonArray Grips(WoodModule.WallFrame f, Func<double, double, Vec2> p)
    {
        var o = new JsonArray();
        void G(string key, double u, double z) { var q = p(u, z); o.Add((JsonNode)new JsonObject { ["key"] = key, ["at"] = new JsonArray(Math.Round(q.X, 6), Math.Round(q.Y, 6)) }); }
        foreach (var m in f.Result.Members.Where(m => m.Role == "stud"))
            G(FormattableString.Invariant($"stud:{Math.Round((m.U0 + m.U1) / 2, 4)}"), (m.U0 + m.U1) / 2, (m.Z0 + m.Z1) / 2);
        foreach (var (id, ro) in f.Result.Openings)
        {
            var mine = f.Result.Members.Where(m => m.Opening == id).ToList();
            foreach (var role in new[] { "king", "jack" })
            {
                var left = mine.Where(m => m.Role == role && m.U1 <= ro.U0 + 1e-6).MinBy(m => m.U0);
                var right = mine.Where(m => m.Role == role && m.U0 >= ro.U1 - 1e-6).MaxBy(m => m.U1);
                var z = role == "king" ? (ro.Z0 + ro.Z1) / 2 : Math.Max(ro.Z0, 0.3);
                // None on a side: the grip stands at the rough opening's edge (the jacks') or the jacks' outer face (the kings').
                var t = f.Spec.Stud.W;
                var edgeL = role == "jack" ? ro.U0 : ro.U0 - ro.Rule.Jacks * t;
                var edgeR = role == "jack" ? ro.U1 : ro.U1 + ro.Rule.Jacks * t;
                G($"{role}:{id}:L", left != null ? (left.U0 + left.U1) / 2 : edgeL - t / 2, z);
                G($"{role}:{id}:R", right != null ? (right.U0 + right.U1) / 2 : edgeR + t / 2, z);
            }
        }
        return o;
    }
}

/// <summary>A wall's own stud exceptions carried with it when it is split, trimmed or carried on (audit §15): each on its part, measured from its start.</summary>
sealed class WoodWallPieces : IPathPieces
{
    public bool Handles(Entity e) => e.Class == IfcClass.Wall && e.Attrs[Framing.StudEdits.Key] != null;

    public string? Refuse(IModelView m, Entity e) => null;

    public void Piece(IModelView m, Entity e, JsonObject attrs, double s0, double s1, bool newStart, bool newEnd)
    {
        if (Framing.StudEdits.Of(e)?.Slice(s0, s1) is { } kept) attrs[Framing.StudEdits.Key] = kept.ToJson(); else attrs.Remove(Framing.StudEdits.Key);
    }

    public void Rehost(ICommandContext ctx, Entity e, IReadOnlyList<PathPiece> pieces) { }
}
