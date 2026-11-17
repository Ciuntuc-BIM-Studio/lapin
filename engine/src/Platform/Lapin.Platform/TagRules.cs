using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Lapin.Drawing;
using Lapin.Model;

namespace Lapin.Platform;

/// <summary>
/// Which tag type a tag is drawn with (issue #72, forum topic 114). The drafting standards' tag rules (<see cref="TagRule"/>), per
/// class and in their order, each a condition and a tag type: the first whose condition holds on the tagged element gives the
/// type, over the one the tag was made with; a tag whose type was chosen in its properties (<see cref="FixedKey"/>) keeps that
/// one, and so does a tag that shows a field of its own (no type) or a build-up. No rule, or none that holds: the tag's own type,
/// exactly as before. Worked out on every draw, so a tag follows its element (a door made wider takes the wide door's marker).
/// A condition is a formula (<see cref="Expr"/>) over the element's fields as <c>{= …}</c> reads them — its parameters and its
/// type's, its attributes, its scopes (<c>storey.code</c>), and what its module measures (<see cref="AddMeasure"/>: a door's or a
/// window's <c>width</c>, <c>height</c> and <c>sill</c> in metres) — written bare (<c>width &gt; 1.2</c>) or as <c>{= width &gt; 1.2}</c>;
/// it holds when its value is true (a number not 0, a text not empty, not „no”). One that does not read never holds.
/// A tag may also be switched off by itself (<see cref="OffKey"/>): it then prints nothing, and is drawn on screen only, faintly,
/// so it can still be clicked and switched on again.
/// </summary>
public static class TagRules
{
    /// <summary>The tag's attribute that says its type was chosen for it, in its properties: the rules leave it alone.</summary>
    public const string FixedKey = "typeFixed";
    /// <summary>The tag's attribute that switches it off: not printed, only a faint outline on screen.</summary>
    public const string OffKey = "off";

    static readonly ConcurrentDictionary<string, Func<IModelView, Entity, string, double?>> Measures = new(StringComparer.Ordinal);

    /// <summary>What a module measures on its class's elements for the conditions (a door's width), by name; null when it has no such measure.</summary>
    public static void AddMeasure(string cls, Func<IModelView, Entity, string, double?> measure) => Measures[cls] = measure;

    /// <summary>A condition's formula: its <c>{= … }</c> or a leading <c>=</c> taken off.</summary>
    public static string Formula(string when)
    {
        var f = when.Trim();
        if (f.StartsWith("{=", StringComparison.Ordinal) && f.EndsWith('}')) f = f[2..^1];
        else if (f.StartsWith('=')) f = f[1..];
        return f.Trim();
    }

    /// <summary>Whether a condition holds on <paramref name="e"/>; one that does not read, or names nothing the element has, does not.</summary>
    public static bool Holds(IModelView m, Entity e, string when)
    {
        var formula = Formula(when);
        if (formula.Length == 0) return false;
        var type = m.RelatingOf(e.Id, IfcClass.RelDefinesByType);
        Func<string, double?>? more = Measures.TryGetValue(e.Class, out var measure) ? n => measure(m, e, n) : null;
        var (value, _) = Expr.TryEvalValue(formula, n => (more?.Invoke(n) is { } x ? XValue.Of(x) : (XValue?)null) ?? ParamFormulas.Read(m, e, type, more, n));
        return value is { Truth: true };
    }

    /// <summary>The type the first rule of <paramref name="e"/>'s class that holds gives (a build-up never: it is asked for by name); null when none does.</summary>
    public static TagType? RuleType(IModelView m, StyleBook b, Entity e)
    {
        if (b.TagRules is not { Count: > 0 } rules) return null;
        foreach (var r in rules)
        {
            if (!string.Equals(r.Class, e.Class, StringComparison.Ordinal)) continue;
            if (Standards.TagTypeOf(b, r.TagType) is not { } type || type.Shape == "buildup") continue;
            if (Holds(m, e, r.When)) return type;
        }
        return null;
    }

    /// <summary>
    /// The type a tag is drawn with: its own when it was chosen for it (<see cref="FixedKey"/>), when it has none, when it is a
    /// build-up or its element is gone; else the first rule that holds, else its own.
    /// </summary>
    public static TagType? TypeOf(IModelView m, StyleBook b, Entity tag, Entity? e)
    {
        var own = Standards.TagTypeOf(b, tag.Str("tagType"));
        if (own == null || e == null || own.Shape == "buildup" || tag.Bool(FixedKey)) return own;
        return RuleType(m, b, e) ?? own;
    }

    /// <summary>Whether a tag is switched off.</summary>
    public static bool Off(Entity tag) => tag.Bool(OffKey);

    /// <summary>A rule as <c>standards.set</c> keeps it, checked: its class, its condition reading as a formula, its type one of the standards'.</summary>
    public static void Check(JsonObject rule, StyleBook b)
    {
        if (((string?)rule["class"])?.Trim() is not { Length: > 0 }) throw new CommandException("A tag rule is for a class (IfcDoor, IfcWindow…).");
        var when = ((string?)rule["when"])?.Trim() ?? "";
        var formula = Formula(when);
        if (formula.Length == 0) throw new CommandException("A tag rule's condition is a formula: width > 1.2, FireRating != \"\".");
        if (formula.Length > Expr.MaxLength) throw new CommandException($"A formula is at most {Expr.MaxLength} characters.");
        var (_, error) = Expr.TryEvalValue(formula, _ => XValue.Of(1));
        if (error != null) throw new CommandException(error);
        if ((string?)rule["tagType"] is not { Length: > 0 } type || Standards.TagTypeOf(b, type) == null) throw new CommandException("There is no such tag type.");
    }

    /// <summary>The colour a switched-off tag is drawn in on screen.</summary>
    public const string GhostColor = "#a1a1aa";

    /// <summary>
    /// A switched-off tag's mark: its lines on the construction layer (on screen, never printed), faint, its texts as the outlines
    /// of their boxes, its fills left out; what a click takes and its grips' data stay, told it is off.
    /// </summary>
    public static ViewMark Ghost(ViewMark mk, double mm)
    {
        for (var i = 0; i < mk.Lines.Count; i++)
        {
            var l = mk.Lines[i];
            if (l.Layer != "PICK") mk.Lines[i] = (l.A, l.B, Views.ConstructionLayer, 0, GhostColor, l.Dash);
        }
        foreach (var (t, _) in mk.Texts)
            foreach (var (a, b) in LabelParts.Sides(LabelParts.BoxOf(t, mm)))
            {
                mk.Lines.Add((a, b, Views.ConstructionLayer, 0, GhostColor, null));
                mk.Lines.Add((a, b, "PICK", 0, "none", null));
            }
        mk.Texts.Clear();
        mk.Fills.Clear();
        mk.Curves.Clear();
        mk.Data ??= [];
        mk.Data["off"] = true;
        return mk;
    }
}
