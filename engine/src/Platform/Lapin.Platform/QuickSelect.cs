using System.Text.Json.Nodes;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Quick select (forum topic 73, AutoCAD's QSELECT, Revit's Select by filter): the elements matching rules, read as a view
/// filter's are (<see cref="ViewFilters"/>: class, type, storey, material, a parameter or any field equal to, containing, or
/// in a range of values), all of them or any; in the whole project, or only those a view shows; the elements on a storey (a
/// door or a window on its wall's). Derived on every ask, never kept.
/// </summary>
public static class QuickSelect
{
    /// <summary>The elements a 2D view shows: the owners of the marks its drawing carries (an element's pick lines, its annotations' owners).</summary>
    public static HashSet<string> ShownIn(IQueryContext q, Entity view)
    {
        var shown = new HashSet<string>();
        if (view.Str("kind") == "3d" || view.Id == GraphicsSets.View3d)
        {
            var vg = VisibilityGraphics.Of(q.Model, view);
            var hidden = ViewHiding.Hidden(view);
            foreach (var e in q.Model.Entities)
                if (ViewFilters.OnStorey(q.Model, e) && !hidden.Contains(e.Id) && !vg.Hides(e)) shown.Add(e.Id);
            return shown;
        }
        foreach (var mk in Views.Render(q, view).Marks)
        {
            if (mk.Owner is not { } id) continue;
            shown.Add(id);
            foreach (var sep in new[] { '#', ':' })
                if (id.IndexOf(sep) is var k and > 0) shown.Add(id[..k]);
        }
        return shown;
    }

    /// <summary>The ids matching {rules, match?: all|any, viewId?, temp?, reveal?}, in the model's order.</summary>
    public static List<string> Query(IQueryContext q, JsonObject a)
    {
        var rules = ViewFilters.Rules(a["rules"]);
        if (rules.Count == 0) throw new CommandException("Quick select needs a rule at least.");
        var any = (string?)a["match"] == "any";
        HashSet<string>? inView = null;
        if ((string?)a["viewId"] is { Length: > 0 } vid)
        {
            var view = Views.Find(q.Model, vid) ?? throw new CommandException("There is no such view.");
            inView = ShownIn(q, ViewHiding.Temporary(q.Model, view, a));
        }
        var m = q.Model;
        // Lapin's one filter, with the geometry at hand: a field rule may read a quantity, a material an element's part's.
        var matcher = new ViewFilters.Matcher(q);
        var filter = ViewFilters.ToFilter(rules, any);
        return [.. m.Entities
            .Where(e => ViewFilters.OnStorey(m, e) && (inView == null || inView.Contains(e.Id)))
            .Where(e => matcher.Matches(e, filter))
            .Select(e => e.Id)];
    }

    public static void Register(IModuleRegistry r)
    {
        r.Query("select.query", (q, a) =>
        {
            var ids = Query(q, a);
            return new JsonObject { ["ids"] = new JsonArray(ids.Select(x => (JsonNode)x).ToArray()), ["count"] = ids.Count };
        });
    }
}
