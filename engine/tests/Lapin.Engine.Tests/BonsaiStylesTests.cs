using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Lapin.Architecture;
using Lapin.Drawing;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Platform;
using Lapin.Structure;

namespace Lapin.Engine.Tests;

/// <summary>
/// Bonsai's drawing styles written with an IFC export (docs/spec-ifc.md, Stilurile de desen pentru Bonsai): the
/// stylesheet's classes as Bonsai names what it draws, each material's hatch and fill, the project's pens, the
/// patterns it fills with, and the IFC's BBIM_Documentation naming both beside the file.
/// </summary>
public class BonsaiStylesTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("lapin-bonsai").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    static Fixture House(out string wallType)
    {
        var f = new Fixture(new ArchitectureModule(), new StructureModule());
        wallType = f.Type("Brick 37", ("Plaster", 0.02, false), ("Brick", 0.25, true), ("Insulation", 0.1, false));
        f.Wall(0, 0, 6, 0, wallType);
        f.Wall(6, 0, 6, 4, wallType);
        return f;
    }

    static Entity MaterialNamed(Fixture f, string name) => f.Model.OfClass(IfcClass.Material).First(m => m.Name == name);

    [Fact]
    public void A_name_makes_the_class_Bonsai_makes_of_it()
    {
        Assert.Equal("ConcreteC2530", BonsaiStyles.ClassName("Concrete C25/30"));
        Assert.Equal("Brick", BonsaiStyles.ClassName("Brick"));
        // Every run of characters other than ASCII letters and digits goes, diacritics too.
        Assert.Equal("Crmidplin", BonsaiStyles.ClassName("Cărămidă plină"));
        Assert.Equal(("house.css", "house-patterns.svg"), BonsaiStyles.FileNames("/tmp/x/house.ifc"));
    }

    [Fact]
    public void The_stylesheet_has_each_materials_hatch_and_fill_and_the_projects_pens()
    {
        var f = House(out _);
        // The plaster's own cut: concrete's speckle, twice as large, on red; the walls' cut lines the project's pen 8.
        f.Session.Edit("graphics", e =>
        {
            var plaster = MaterialNamed(f, "Plaster");
            var a = plaster.CloneAttrs();
            a["section"] = new JsonObject { ["hatch"] = "CONCRETE", ["hatchScale"] = 2, ["fill"] = "#ff0000" };
            e.Replace(plaster.With(attrs: a));
            var project = Spatial.Project(e.Model)!;
            var pa = project.CloneAttrs();
            pa["objectStyles"] = new JsonObject { ["walls"] = new JsonObject { ["cut"] = new JsonObject { ["pen"] = 8 } } };
            e.Replace(project.With(attrs: pa));
        });
        var sheet = BonsaiStyles.Of(f.Model);
        var css = sheet.Css;
        // LAPIN_BONSAI_DUMP=<folder>: the two files and a page of every hatch, to look at in a browser or Inkscape.
        if (Environment.GetEnvironmentVariable("LAPIN_BONSAI_DUMP") is { Length: > 0 } dump)
        {
            Directory.CreateDirectory(dump);
            File.WriteAllText(Path.Combine(dump, "house.css"), css);
            File.WriteAllText(Path.Combine(dump, "house-patterns.svg"), sheet.Patterns);
            var tops = Regex.Matches(sheet.Patterns, "id=\"(lapin-[a-z0-9_-]+?)\" patternUnits=\"userSpaceOnUse\" width=\"1000\"").Select(x => x.Groups[1].Value).ToList();
            var defs = sheet.Patterns[(sheet.Patterns.IndexOf('>') + 1)..sheet.Patterns.LastIndexOf("</svg>", StringComparison.Ordinal)];
            var cells = string.Join("", tops.Select((id, i) => $"<rect x=\"{5 + i % 4 * 50}\" y=\"{5 + i / 4 * 50}\" width=\"40\" height=\"40\" fill=\"url(#{id})\" stroke=\"black\" stroke-width=\"0.35\"/><text x=\"{5 + i % 4 * 50}\" y=\"{49 + i / 4 * 50}\" font-size=\"3\">{id}</text>"));
            File.WriteAllText(Path.Combine(dump, "hatches.svg"), $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"210mm\" height=\"{(tops.Count / 4 + 1) * 50 + 10}mm\" viewBox=\"0 0 210 {(tops.Count / 4 + 1) * 50 + 10}\"><defs>{defs}</defs>{cells}</svg>");
        }

        // Brick: what its name suggests, the 45° masonry lines; insulation the batt (its zig-zag pattern).
        Assert.Contains(".cut.material-Brick, .surface.material-Brick, .material-Brick .cut, .material-Brick .surface, .cut.layer-material-Brick, .surface.layer-material-Brick { fill: url(#lapin-masonry); }", css);
        Assert.Contains(".cut.material-Insulation", css);
        Assert.Matches(@"\.cut\.material-Insulation[^{]*\{ fill: url\(#lapin-insulation\); \}", css);
        Assert.Contains("fill: url(#lapin-concrete-x2-ff0000)", css);
        // The layer set's class: its core's hatch.
        Assert.Matches(@"\.cut\.material-Brick37[^{]*\{ fill: url\(#lapin-masonry\); \}", css);
        // The base pens: Lapin's cut and seen lines; the walls' cut its pen 8 at 1:100.
        Assert.Contains(".cut { fill: white; stroke: black; stroke-width: 0.5; fill-rule: evenodd; }", css);
        Assert.Contains(".projection { fill: white; stroke: black; stroke-width: 0.25; }", css);
        var pen8 = LineStyles.PenWidth(LineStyles.DefaultPenTable(), 8, 100);
        Assert.Contains($".IfcWall.cut, .IfcWall .cut {{ stroke-width: {Math.Round(pen8, 4).ToString(System.Globalization.CultureInfo.InvariantCulture)}; }}", css);

        // Every pattern the stylesheet names is in the patterns file, an SVG whose children are the patterns.
        var svg = XDocument.Parse(sheet.Patterns);
        XNamespace ns = "http://www.w3.org/2000/svg";
        var ids = svg.Root!.Elements(ns + "pattern").Select(p => (string)p.Attribute("id")!).ToHashSet();
        var named = Regex.Matches(css + sheet.Patterns, @"url\(#(lapin-[^)]+)\)").Select(x => x.Groups[1].Value).ToHashSet();
        Assert.NotEmpty(named);
        Assert.All(named, id => Assert.Contains(id, ids));
        // The red background under the large speckle; a dotted family as a dash array of dots and gaps (the gypsum's stipple).
        var concrete = svg.Root.Elements(ns + "pattern").Single(p => (string?)p.Attribute("id") == "lapin-concrete-x2-ff0000");
        Assert.Equal("#ff0000", (string?)concrete.Elements(ns + "rect").First().Attribute("fill"));
        Assert.Equal(4, concrete.Elements(ns + "rect").Count(r => ((string?)r.Attribute("fill"))!.StartsWith("url(")));
        var family = svg.Root.Elements(ns + "pattern").Single(p => (string?)p.Attribute("id") == "lapin-concrete-x2-ff0000-0");
        Assert.StartsWith("scale(2 -2) translate(0 0) rotate(50)", (string?)family.Attribute("patternTransform"));
        Assert.Equal("0.9 6.7", (string?)family.Element(ns + "line")!.Attribute("stroke-dasharray"));
        // The hatch pen stays 0.13 mm on paper however large the pattern.
        Assert.Equal("0.065", (string?)family.Element(ns + "line")!.Attribute("stroke-width"));
    }

    [Fact]
    public void A_dotted_family_is_dots_and_gaps()
    {
        var f = House(out _);
        f.Session.Edit("graphics", e =>
        {
            var plaster = MaterialNamed(f, "Plaster");
            var a = plaster.CloneAttrs();
            a["section"] = new JsonObject { ["hatch"] = "GYPSUM" };
            e.Replace(plaster.With(attrs: a));
        });
        var svg = BonsaiStyles.Of(f.Model).Patterns;
        Assert.Contains("id=\"lapin-gypsum-0\"", svg);
        Assert.Contains("stroke-dasharray=\"0.01 4.4\"", svg);
        Assert.Contains("width=\"4.41\"", svg);
    }

    [Fact]
    public void An_export_with_Bonsai_styles_writes_them_beside_the_file_and_names_them_on_the_project()
    {
        var f = House(out _);
        var path = Path.Combine(_dir, "house.ifc");
        var r = IfcExport.Write(f.Session, path, new IfcExportOptions(BonsaiStyles: true));
        Assert.True(File.Exists(Path.Combine(_dir, "house.css")));
        Assert.True(File.Exists(Path.Combine(_dir, "house-patterns.svg")));
        Assert.Contains(r.Notes, n => n.Contains("house.css"));
        var step = StepFile.Read(path);
        var set = step.OfType("IFCPROPERTYSET").Single(p => (string?)p[2] == BonsaiStyles.SetName);
        var props = ((List<object?>)set[4]!).Select(x => step.Get(x)!).ToDictionary(p => (string)p[0]!, p => ((StepTyped)p[2]!).Value as string);
        Assert.Equal("house.css", props[BonsaiStyles.StylesheetKey]);
        Assert.Equal("house-patterns.svg", props[BonsaiStyles.PatternsKey]);
        var rel = step.OfType("IFCRELDEFINESBYPROPERTIES").Single(x => step.Get(x[5])!.Id == set.Id);
        Assert.Equal("IFCPROJECT", step.Get(((List<object?>)rel[4]!)[0])!.Type);
        Assert.Equal(File.ReadAllText(Path.Combine(_dir, "house.css")), BonsaiStyles.Of(f.Model).Css);

        // Without the option: no set, no files, the file as before.
        var plain = Path.Combine(_dir, "plain.ifc");
        IfcExport.Write(f.Session, plain);
        Assert.False(File.Exists(Path.Combine(_dir, "plain.css")));
        Assert.DoesNotContain(StepFile.Read(plain).OfType("IFCPROPERTYSET"), p => (string?)p[2] == BonsaiStyles.SetName);
    }
}
