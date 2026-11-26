using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Drawing;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Engine.Tests;

/// <summary>
/// Decision 737 (the project owner's, 19.11.2026: „marks built by the user, prefix and suffix, like a formula, and the tag the
/// same”): a mark rule per category, a pattern of live text with a prefix and a suffix, numbers kept on the elements. The built-in
/// rule gives the marks Lapin gave before; an element's own mark wins; Renumber is one undo step; a tag type has a prefix and a
/// suffix round its template; a project with rules gets the numbers its elements lack when it opens.
/// </summary>
public sealed class MarkRulesTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-mark-rules-").FullName;
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static Fixture F() => new(new ArchitectureModule(), new DraftingModule());

    static string Door(Fixture f, string wall, double along) => (string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = wall, ["along"] = along })!["id"]!;
    static string Window(Fixture f, string wall, double along) => (string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = wall, ["along"] = along })!["id"]!;
    static string Mark(Fixture f, string id) => TagText.Mark(f.Model, f.Model.Get(id)!);
    static string? TypeOf(Fixture f, string id) => f.Model.RelatingOf(id, IfcClass.RelDefinesByType)?.Id;

    static void Rule(Fixture f, string category, JsonObject? rule) =>
        f.Run("markRules.set", new JsonObject { ["rules"] = new JsonObject { [category] = rule } });

    /// <summary>What IFC writes: each wall's, door's and window's Tag by its GlobalId, each Reference in a property set.</summary>
    (Dictionary<string, string> Tags, List<string> References) Ifc(Fixture f)
    {
        var path = Path.Combine(_dir, $"m{Guid.NewGuid():N}.ifc");
        IfcExport.Write(f.Session, path);
        var file = StepFile.Read(File.ReadAllBytes(path));
        var tags = new[] { "IFCWALL", "IFCDOOR", "IFCWINDOW" }.SelectMany(file.OfType).ToDictionary(x => (string)x[0]!, x => (string?)x[7] ?? "");
        var refs = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), @"IFCPROPERTYSINGLEVALUE\('Reference',\$,IFCIDENTIFIER\('([^']*)'\)").Select(x => x.Groups[1].Value).Order().ToList();
        return (tags, refs);
    }

    static (Fixture F, string Wall, string D1, string D2, string W1) House()
    {
        var f = F();
        var wall = f.Wall(0, 0, 10, 0, height: 3);
        var d1 = Door(f, wall, 1.5);
        var d2 = Door(f, wall, 4);
        var w1 = Window(f, wall, 7.5);
        return (f, wall, d1, d2, w1);
    }

    [Fact]
    public void The_built_in_rules_give_the_marks_tags_and_ifc_of_before()
    {
        var (f, wall, d1, d2, w1) = House();
        // Nothing of the rules is stored while the user has none.
        Assert.Null(Spatial.Project(f.Model)!.Attrs[MarkRules.Key]);
        Assert.All(new[] { wall, d1, d2, w1 }, id => Assert.Null(f.Model.Get(id)!.Attrs[MarkRules.NumberKey]));
        // The doors' and window's names numbered as before, their marks their type's.
        Assert.Equal(["D1", "D2", "W1"], new[] { d1, d2, w1 }.Select(id => f.Model.Get(id)!.Name));
        Assert.Equal(["D", "D", "W"], new[] { d1, d2, w1 }.Select(id => Mark(f, id)));
        Assert.Equal(f.Model.Get(TypeOf(f, wall)!)!.Name, Mark(f, wall));
        // The markers' texts, the built-in tag types as they are.
        var book = DraftingResources.Book(f.Model);
        var door = Standards.TagTypeOf(book, "door")!;
        Assert.Equal("{mark}", door.Written);
        Assert.Equal(["D"], TagText.Lines(door.Written, f.Model, f.Model.Get(d1)!));
        // IFC: each Tag its id, the types' Reference their mark.
        var before = Ifc(f);
        Assert.Equal(d1, before.Tags.Single(t => t.Value == d1).Value);
        Assert.Contains("D", before.References);
        Assert.Contains("W", before.References);
        // A rule set, then taken back to the built-in one: everything as it was (its numbers stay, unread).
        Rule(f, "door", new JsonObject { ["prefix"] = "X-", ["pattern"] = "{prefix}{type.mark}{= n | 00}" });
        Assert.Equal("X-D01", Mark(f, d1));
        Rule(f, "door", null);
        Assert.Null(Spatial.Project(f.Model)!.Attrs[MarkRules.Key]);
        Assert.Equal(["D", "D", "W"], new[] { d1, d2, w1 }.Select(id => Mark(f, id)));
        var after = Ifc(f);
        Assert.Equal(before.Tags.Values.Order(), after.Tags.Values.Order());
        Assert.Equal(before.References, after.References);
    }

    [Fact]
    public void The_built_in_pattern_with_a_prefix_and_a_suffix_wraps_todays_mark()
    {
        var (f, _, d1, _, _) = House();
        Rule(f, "door", new JsonObject { ["prefix"] = "U-", ["suffix"] = "/A" });
        Assert.Equal("U-D/A", Mark(f, d1));
        // The pattern reads no number: nothing numbered.
        Assert.Null(f.Model.Get(d1)!.Attrs[MarkRules.NumberKey]);
    }

    [Fact]
    public void A_storey_code_and_a_padded_number_give_P_W01_and_new_windows_take_the_next()
    {
        var (f, wall, _, _, w1) = House();
        f.Run("element.setParam", new JsonObject { ["ids"] = new JsonArray(f.Storey), ["name"] = "code", ["kind"] = "text", ["value"] = "P" });
        Rule(f, "window", new JsonObject { ["pattern"] = "{storey.code}-W{= n | 00}" });
        Assert.Equal("P-W01", Mark(f, w1));
        var w2 = Window(f, wall, 9);
        Assert.Equal(2, MarkRules.Number(f.Model.Get(w2)!));
        Assert.Equal("P-W02", Mark(f, w2));
        // Tags and formulas read the rule's mark.
        Assert.Equal("P-W02", TagText.Value(f.Model, f.Model.Get(w2)!, "mark"));
        Assert.Equal("[P-W02]", TagText.Fill("[{mark}]", f.Model, f.Model.Get(w2)!));
        // Deleting one moves no other.
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(w1) });
        Assert.Equal("P-W02", Mark(f, w2));
        var w3 = Window(f, wall, 7.5);
        Assert.Equal("P-W03", Mark(f, w3));
    }

    [Fact]
    public void Numbers_are_seeded_from_the_names_lapin_numbered()
    {
        var f = F();
        var wall = f.Wall(0, 0, 12, 0, height: 3);
        var doors = new[] { 1.5, 4, 6.5 }.Select(x => Door(f, wall, x)).ToList();
        f.Run("element.delete", new JsonObject { ["ids"] = new JsonArray(doors[1]) });
        Rule(f, "door", new JsonObject { ["pattern"] = "{type.mark}{n}" });
        Assert.Equal("D1", Mark(f, doors[0]));
        Assert.Equal("D3", Mark(f, doors[2]));
        var next = Door(f, wall, 9.5);
        Assert.Equal("D4", Mark(f, next));
        Assert.Equal("D4", f.Model.Get(next)!.Name);
    }

    [Fact]
    public void An_own_mark_wins_and_cleared_goes_back_to_the_rule()
    {
        var (f, _, _, _, w1) = House();
        Rule(f, "window", new JsonObject { ["prefix"] = "F", ["pattern"] = "{prefix}{n}" });
        Assert.Equal("F1", Mark(f, w1));
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(w1), ["mark"] = "SPECIAL" });
        Assert.Equal("SPECIAL", Mark(f, w1));
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(w1), ["mark"] = "" });
        Assert.Equal("F1", Mark(f, w1));
    }

    [Fact]
    public void Renumber_goes_left_to_right_in_one_undo_step()
    {
        var f = F();
        var wall = f.Wall(0, 0, 12, 0, height: 3);
        // Placed right to left: numbered 1, 2, 3 from the right.
        var ws = new[] { 10.0, 6.5, 2 }.Select(x => Window(f, wall, x)).ToList();
        Rule(f, "window", new JsonObject { ["pattern"] = "W{n}" });
        Assert.Equal(["W1", "W2", "W3"], ws.Select(id => Mark(f, id)));
        var r = f.Run("markRules.renumber", new JsonObject { ["category"] = "window", ["order"] = "rows" });
        Assert.Equal(2, (int)r!["changed"]!);
        Assert.Equal(["W3", "W2", "W1"], ws.Select(id => Mark(f, id)));
        f.Session.Undo();
        Assert.Equal(["W1", "W2", "W3"], ws.Select(id => Mark(f, id)));
    }

    [Fact]
    public void A_storey_numbered_rule_counts_each_storey_apart()
    {
        var f = F();
        var ground = f.Storey;
        var upper = (string)f.Run("storey.create", [])!["id"]!;
        var w0 = f.Wall(0, 0, 5, 0, height: 3);
        var w1 = (string)f.Run("architecture.addWall", new JsonObject { ["storeyId"] = upper, ["x1"] = 0, ["y1"] = 0, ["x2"] = 5, ["y2"] = 0, ["height"] = 3 })!["id"]!;
        Rule(f, "wall", new JsonObject { ["pattern"] = "{storey.name}/{n}", ["scope"] = "storey" });
        Assert.Equal(1, MarkRules.Number(f.Model.Get(w0)!));
        Assert.Equal(1, MarkRules.Number(f.Model.Get(w1)!));
        Assert.EndsWith("/1", Mark(f, w1));
        Assert.NotEqual(ground, upper);
    }

    [Fact]
    public void A_type_rule_marks_types_without_their_own_and_ifc_writes_it()
    {
        var f = F();
        var type = f.Type("Porotherm 240", ("Brick", 0.24, true));
        var other = f.Type("Porotherm 300", ("Brick", 0.30, true));
        var wall = f.Wall(0, 0, 5, 0, type, height: 3);
        f.Wall(0, 4, 5, 4, other, height: 3);
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(other), ["mark"] = "P30" });
        Rule(f, "wallType", new JsonObject { ["pattern"] = "WT{n}" });
        var n = MarkRules.Number(f.Model.Get(type)!);
        Assert.NotNull(n);
        Assert.Equal($"WT{n}", Mark(f, wall));
        Assert.Equal($"WT{n}", TagText.Value(f.Model, f.Model.Get(wall)!, "TypeMark"));
        var refs = Ifc(f).References;
        Assert.Contains($"WT{n}", refs);
        Assert.Contains("P30", refs);
        // The wall rule: its Tag.
        Rule(f, "wall", new JsonObject { ["pattern"] = "{TypeMark}.{n}" });
        Assert.Contains(Ifc(f).Tags.Values, t => t == $"WT{n}.1");
    }

    [Fact]
    public void A_tag_types_prefix_and_suffix_wrap_its_formula()
    {
        var (f, _, d1, _, _) = House();
        f.Run("standards.set", new JsonObject { ["kind"] = "tag", ["entry"] = new JsonObject { ["id"] = "door-x", ["name"] = "Door code", ["template"] = "{mark}|{= 2 * 3}", ["prefix"] = "D-", ["suffix"] = " ●", ["classes"] = "IfcDoor" } });
        var type = Standards.TagTypeOf(DraftingResources.Book(f.Model), "door-x")!;
        Assert.Equal("D-", type.Prefix);
        var tag = new Entity("t", IfcClass.Annotation, "Tag", new JsonObject());
        Assert.Equal(["D-D", "6 ●"], Tags.LinesOf(f.Model, tag, f.Model.Get(d1)!, type, "mm"));
        // A mark rule shows through the tag.
        Rule(f, "door", new JsonObject { ["pattern"] = "{type.mark}{= n | 00}" });
        Assert.Equal(["D-D01", "6 ●"], Tags.LinesOf(f.Model, tag, f.Model.Get(d1)!, type, "mm"));
    }

    [Fact]
    public void Bad_rules_are_refused()
    {
        var f = F();
        Assert.Throws<CommandException>(() => Rule(f, "nope", new JsonObject { ["pattern"] = "{n}" }));
        Assert.Throws<CommandException>(() => Rule(f, "wall", new JsonObject { ["pattern"] = "{n" }));
        Assert.Throws<CommandException>(() => Rule(f, "wall", new JsonObject { ["prefix"] = "{x}" }));
        Assert.Throws<CommandException>(() => Rule(f, "wall", new JsonObject { ["pattern"] = "{n}", ["scope"] = "street" }));
    }

    [Fact]
    public void The_upgrade_numbers_what_a_rule_reads_and_leaves_projects_without_rules_as_they_came()
    {
        var (f, _, d1, d2, _) = House();
        var entities = f.Model.Entities.ToList();
        var relations = f.Model.Relations.ToList();
        var upgrade = new MarkRules.NumbersUpgrade();
        var (same, _) = upgrade.Upgrade(entities, relations);
        Assert.Same(entities, same);
        // A project whose door rule reads {n}, its doors without numbers (a file written elsewhere): D1 → 1, D2 → 2.
        var project = Spatial.Project(f.Model)!;
        var attrs = project.CloneAttrs();
        attrs[MarkRules.Key] = new JsonObject { ["door"] = new MarkRule(Pattern: "{type.mark}-{n}").ToJson() };
        var with = entities.Select(e => e.Id == project.Id ? e.With(attrs: attrs) : e).ToList();
        var (up, _) = upgrade.Upgrade(with, relations);
        Assert.Equal(1, MarkRules.Number(up.Single(e => e.Id == d1)));
        Assert.Equal(2, MarkRules.Number(up.Single(e => e.Id == d2)));
        // Again: nothing more to do.
        var (again, _) = upgrade.Upgrade(up, relations);
        Assert.Same(up, again);
    }

    // ─── {variant}: one number per size of a type (issue #148) ────────────────────

    static void Size(Fixture f, string id, double w, double h) =>
        f.Run("architecture.setJoinerySize", new JsonObject { ["id"] = id, ["width"] = w, ["height"] = h });

    [Fact]
    public void A_variant_numbers_each_size_of_a_type_in_the_order_the_sizes_first_appear()
    {
        var f = F();
        var wall = f.Wall(0, 0, 20, 0, height: 3);
        var (w1, w2, w3) = (Window(f, wall, 2), Window(f, wall, 6), Window(f, wall, 10));
        Size(f, w1, 1.2, 1.2); Size(f, w2, 0.8, 1.2); Size(f, w3, 1.2, 1.2);
        Rule(f, "window", new JsonObject { ["prefix"] = "V", ["pattern"] = "{prefix}{variant}" });
        // The tester's example: 1200×1200, 800×1200, 1200×1200 → V1, V2, V1.
        Assert.Equal(["V1", "V2", "V1"], new[] { w1, w2, w3 }.Select(id => Mark(f, id)));
        // With the sizes beside it, as the tester's rule writes them.
        Rule(f, "window", new JsonObject { ["prefix"] = "V", ["pattern"] = "{prefix}{variant}-{openingWidth}x{openingHeight}" });
        Assert.Equal(["V1-1200x1200", "V2-800x1200", "V1-1200x1200"], new[] { w1, w2, w3 }.Select(id => Mark(f, id)));
        Rule(f, "window", new JsonObject { ["prefix"] = "V", ["pattern"] = "{prefix}{variant}" });
        // A size added later takes the next number; those there keep theirs.
        var w4 = Window(f, wall, 14);
        Size(f, w4, 0.6, 0.6);
        Assert.Equal(["V1", "V2", "V1", "V3"], new[] { w1, w2, w3, w4 }.Select(id => Mark(f, id)));
        // A window given an existing size takes that size's number.
        Size(f, w4, 0.8, 1.2);
        Assert.Equal("V2", Mark(f, w4));
        // Nothing is kept on the windows: worked out on every ask.
        Assert.All(new[] { w1, w2, w3, w4 }, id => Assert.Null(f.Model.Get(id)!.Attrs["variant"]));
        // The tag and IFC's Tag write it too.
        Assert.Contains(Ifc(f).Tags.Values, t => t == "V2");
    }

    [Fact]
    public void Each_type_numbers_its_own_variants_and_a_rule_may_say_what_tells_them_apart()
    {
        var f = F();
        var wall = f.Wall(0, 0, 20, 0, height: 3);
        var (w1, w2, w3) = (Window(f, wall, 2), Window(f, wall, 6), Window(f, wall, 10));
        var first = TypeOf(f, w1)!;
        var other = (string)f.Run("types.duplicate", new JsonObject { ["id"] = first })!["id"]!;
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(first), ["mark"] = "A" });
        f.Run("element.setMark", new JsonObject { ["ids"] = new JsonArray(other), ["mark"] = "B" });
        f.Run("architecture.setJoineryType", new JsonObject { ["ids"] = new JsonArray(w3), ["typeId"] = other });
        Size(f, w1, 1.2, 1.2); Size(f, w2, 1.2, 1.5); Size(f, w3, 1.2, 1.5);
        Rule(f, "window", new JsonObject { ["pattern"] = "{type.mark}{variant}" });
        // By width × height: two variants of A, B's one its first.
        Assert.Equal(["A1", "A2", "B1"], new[] { w1, w2, w3 }.Select(id => Mark(f, id)));
        // Told by the width alone: one variant of A.
        Rule(f, "window", new JsonObject { ["pattern"] = "{type.mark}{variant}", ["variantBy"] = "{openingWidth}" });
        Assert.Equal(["A1", "A1", "B1"], new[] { w1, w2, w3 }.Select(id => Mark(f, id)));
        Assert.Equal("{openingWidth}", (string?)Spatial.Project(f.Model)!.Attrs[MarkRules.Key]!["window"]!["variantBy"]);
        // A formula reads it too.
        Rule(f, "window", new JsonObject { ["pattern"] = "{type.mark}{= variant | 00}" });
        Assert.Equal(["A01", "A02", "B01"], new[] { w1, w2, w3 }.Select(id => Mark(f, id)));
    }

    [Fact]
    public void A_rule_without_variant_is_written_as_before_and_a_variant_told_by_its_mark_is_refused()
    {
        var f = F();
        Rule(f, "window", new JsonObject { ["prefix"] = "V", ["pattern"] = "{prefix}{n}" });
        var kept = (JsonObject)Spatial.Project(f.Model)!.Attrs[MarkRules.Key]!["window"]!;
        Assert.Equal(["prefix", "suffix", "pattern", "scope"], kept.Select(kv => kv.Key));
        Assert.Throws<CommandException>(() => Rule(f, "window", new JsonObject { ["pattern"] = "{variant}", ["variantBy"] = "{mark}" }));
        Assert.Throws<CommandException>(() => Rule(f, "window", new JsonObject { ["pattern"] = "{variant}", ["variantBy"] = "{width" }));
        // A variantBy alone makes the rule the user's (not the built-in one).
        Assert.False(MarkRule.From(new JsonObject { ["variantBy"] = "{width}" }).IsDefault);
        Assert.True(MarkRule.From(new JsonObject()).IsDefault);
    }

    [Fact]
    public void Other_categories_are_one_variant_of_their_type_until_the_rule_says_what_tells_them_apart()
    {
        var f = F();
        var walls = new[] { f.Wall(0, -2, 4, -2, height: 3), f.Wall(0, -4, 6, -4, height: 3), f.Wall(0, -6, 4, -6, height: 2.5) };
        Rule(f, "wall", new JsonObject { ["pattern"] = "W{variant}" });
        Assert.Equal(["W1", "W1", "W1"], walls.Select(id => Mark(f, id)));
        Rule(f, "wall", new JsonObject { ["pattern"] = "W{variant}", ["variantBy"] = "{height}" });
        Assert.Equal(["W1", "W1", "W2"], walls.Select(id => Mark(f, id)));
    }
}
