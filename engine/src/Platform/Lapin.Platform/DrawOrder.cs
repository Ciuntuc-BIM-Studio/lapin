using System.Text.Json.Nodes;
using Lapin.Geometry;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// The order a view's drafted elements draw and pick in (report #44, decision 339): its lines, polylines, arcs,
/// circles, rectangles, hatches and fills, texts, dimensions, tags and a DXF's shapes drawn on it, every
/// IfcAnnotation whose <c>viewId</c> is the view. Each keeps its place as a number of its own
/// (<see cref="Key"/>), so the order is per view (an annotation lies on one view), saves and undoes with it,
/// and a sheet, the PDF and the DXF draw it as the screen does.
/// <list type="bullet">
/// <item>Numbers above zero draw over the model, the higher on top; those below zero behind it (under the cut
/// and seen lines, over a room's colour fill). Zero or none: in the order they always drew, over the numbered ones,
/// so what is drawn after an order was set comes on top, as in a CAD program.</item>
/// <item>Each change numbers the whole view's stack again (1, 2, 3… over the model; −1, −2… behind it), the
/// elements whose number changes rewritten, all in one undo step.</item>
/// </list>
/// The model's own drawing does not change: drafting draws over it unless sent behind it.
/// </summary>
public static class DrawOrder
{
    public const string Key = "drawOrder";

    /// <summary>The ways an order changes: on top of all, one step up past what it overlaps, one step down, under all the drafting (still over the model), behind the model, just above or under other elements.</summary>
    public static readonly string[] Moves = ["front", "forward", "backward", "back", "behind", "above", "under"];

    /// <summary>An element drafted on <paramref name="viewId"/>.</summary>
    public static bool OnView(Entity e, string viewId) => e.Class == IfcClass.Annotation && e.Str("viewId") == viewId;

    /// <summary>An element's number (0: none).</summary>
    public static double Of(Entity e) => e.NumOrNull(Key) is { } d && double.IsFinite(d) ? d : 0;

    /// <summary>How the modules draw a view's unnumbered annotations, bottom up: drafted shapes, dimensions, texts, tags, the rest.</summary>
    static int KindRank(Entity e) => e.PredefinedType switch { "DRAFT" => 0, "DIMENSION" => 1, "TEXT" => 2, "TAG" => 3, _ => 4 };

    /// <summary>
    /// A view's drafted elements bottom up, as they draw now, and how many of them lie behind the model (the first ones).
    /// </summary>
    public static (List<Entity> Stack, int Behind) Stack(IModelView m, string viewId)
    {
        var all = m.OfClass(IfcClass.Annotation).Where(e => OnView(e, viewId)).Select((e, i) => (e, i)).ToList();
        var behind = all.Where(x => Of(x.e) < 0).OrderBy(x => Of(x.e)).ThenBy(x => x.i).Select(x => x.e).ToList();
        var over = all.Where(x => Of(x.e) > 0).OrderBy(x => Of(x.e)).ThenBy(x => x.i).Select(x => x.e);
        var free = all.Where(x => Of(x.e) == 0).OrderBy(x => KindRank(x.e)).ThenBy(x => x.i).Select(x => x.e);
        return ([.. behind, .. over, .. free], behind.Count);
    }

    /// <summary>
    /// <paramref name="ids"/> (on one view) moved in its stack by <paramref name="move"/>; <paramref name="refs"/>: the
    /// elements "above" and "under" go next to; <paramref name="boxes"/>: what each element covers in the view, so
    /// "forward" and "backward" step past the next element that overlaps (without boxes: the next one). The view's
    /// stack is numbered again; returns how many elements changed their number.
    /// </summary>
    public static int Apply(ICommandContext ctx, string viewId, IReadOnlyCollection<string> ids, string move, IReadOnlyCollection<string>? refs = null,
        IReadOnlyDictionary<string, (Vec2 Min, Vec2 Max)>? boxes = null)
    {
        var (stack, behind) = Stack(ctx.Model, viewId);
        // The model's place in the stack, as a slot of its own.
        const string Model = "\u0000model";
        var list = stack.Select(e => e.Id).ToList();
        list.Insert(behind, Model);
        var moving = list.Where(ids.Contains).ToList();
        if (moving.Count == 0) throw new CommandException("Select drafted elements on this view: lines, hatches, texts, dimensions, tags.");
        var set = moving.ToHashSet();
        bool Overlap(string a, string b)
        {
            if (b == Model || a == Model) return true;
            if (boxes == null) return true;
            if (!boxes.TryGetValue(a, out var p) || !boxes.TryGetValue(b, out var q)) return false;
            return p.Min.X <= q.Max.X && q.Min.X <= p.Max.X && p.Min.Y <= q.Max.Y && q.Min.Y <= p.Max.Y;
        }
        int ModelAt() => list.IndexOf(Model);
        switch (move)
        {
            case "front":
                list.RemoveAll(set.Contains);
                list.AddRange(moving);
                break;
            case "back":
                list.RemoveAll(set.Contains);
                list.InsertRange(ModelAt() + 1, moving);
                break;
            case "behind":
                list.RemoveAll(set.Contains);
                list.InsertRange(0, moving);
                break;
            case "above" or "under":
            {
                var others = (refs ?? []).Where(r => !set.Contains(r) && list.Contains(r)).ToList();
                if (others.Count == 0) throw new CommandException("Choose the elements to go above or under: drafted elements on this view, not those you move.");
                list.RemoveAll(set.Contains);
                var at = move == "above" ? others.Max(list.IndexOf) + 1 : others.Min(list.IndexOf);
                list.InsertRange(at, moving);
                break;
            }
            case "forward":
                // From the top one down: each past the next element over it that it overlaps, never past the model.
                foreach (var id in Enumerable.Reverse(moving))
                {
                    var i = list.IndexOf(id);
                    var limit = i < ModelAt() ? ModelAt() : list.Count;
                    var j = i + 1;
                    while (j < limit && (set.Contains(list[j]) || !Overlap(id, list[j]))) j++;
                    if (j >= limit) continue;
                    list.RemoveAt(i);
                    list.Insert(j, id);
                }
                break;
            case "backward":
                foreach (var id in moving)
                {
                    var i = list.IndexOf(id);
                    var limit = i > ModelAt() ? ModelAt() : -1;
                    var j = i - 1;
                    while (j > limit && (set.Contains(list[j]) || !Overlap(id, list[j]))) j--;
                    if (j <= limit) continue;
                    list.RemoveAt(i);
                    list.Insert(j, id);
                }
                break;
            default:
                throw new CommandException($"An order is one of {string.Join(", ", Moves)}.");
        }
        // Numbered again: behind the model −n…−1 (the bottom the lowest), over it 1…n.
        var model = ModelAt();
        var changed = 0;
        for (var k = 0; k < list.Count; k++)
        {
            if (k == model || ctx.Model.Get(list[k]) is not { } e) continue;
            double want = k - model;
            if (Of(e) == want) continue;
            var attrs = e.CloneAttrs();
            attrs[Key] = want;
            ctx.Replace(e.With(attrs: attrs));
            changed++;
        }
        return changed;
    }
}
