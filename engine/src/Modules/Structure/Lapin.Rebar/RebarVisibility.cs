using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Rebar;

/// <summary>
/// What a view's Visibility / Graphics and Hide in view leave out of the reinforcement (decision 728, forum topic 174): the Rebar
/// category whole (every bar, the types' stirrups and column bars too, whose marks are their beams' and columns'), one of its
/// subcategories (longitudinal bars, stirrups and ties, rebar sets), a bar or a set hidden by itself or with its host.
/// </summary>
public sealed class RebarVisibility
{
    public const string Category = "rebar";
    readonly VisibilityGraphics vg;
    readonly HashSet<string> hiddenHere;

    /// <summary>The whole category hidden, and each subcategory.</summary>
    public bool All { get; private init; }
    public bool Longitudinal { get; private init; }
    public bool Stirrups { get; private init; }
    public bool Sets { get; private init; }

    RebarVisibility(VisibilityGraphics vg, HashSet<string> hiddenHere) { this.vg = vg; this.hiddenHere = hiddenHere; }

    public static RebarVisibility Of(IModelView m, Entity view)
    {
        var vg = VisibilityGraphics.Of(m, view);
        var subs = vg.Graphics?["categories"]?[Category]?["sub"] as JsonObject;
        bool Sub(string s) => subs?[s]?["hidden"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        return new RebarVisibility(vg, ViewHiding.Hidden(view))
        {
            All = vg.Hides(new Entity("", Rebars.Class, "Bar", new JsonObject())),
            Longitudinal = Sub("longitudinal"), Stirrups = Sub("stirrups"), Sets = Sub("sets"),
        };
    }

    /// <summary>The subcategory of a bar, a distribution or a set: a stirrup or a tie is a stirrup; a set its own; any other bar longitudinal.</summary>
    public static string? SubOf(Entity e) => e.Class switch
    {
        RebarSets.Class => "sets",
        Rebars.Class or Rebars.RunClass => RebarShapes.Def(Rebars.SpecOf(e).Shape).Layer == RebarShapes.StirrupLayer ? "stirrups" : "longitudinal",
        _ => null,
    };

    /// <summary>Whether the view leaves an element out: hidden in it by itself, by its category or a filter, or (a bar) by its subcategory.</summary>
    public bool Hides(Entity e)
    {
        if (hiddenHere.Contains(e.Id) || vg.Hides(e)) return true;
        return SubOf(e) switch { "sets" => Sets, "stirrups" => Stirrups, "longitudinal" => Longitudinal, _ => false };
    }
}
