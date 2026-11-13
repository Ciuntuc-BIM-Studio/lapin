using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lapin.Architecture;
using Lapin.Drafting;
using Lapin.Ifc;
using Lapin.Model;
using Lapin.Package;
using Lapin.Platform;
using Lapin.Protocol;
using Lapin.Studio;

namespace Lapin.Engine.Tests;

/// <summary>
/// Stage P3 of decision 520 (docs/plan-studio-parametric.md): a part is a type everywhere. Placed parts with the same values
/// share one body (built once, drawn as instances of one mesh); IFC writes the part as its class's IfcTypeProduct whose one map
/// is its defaults' body, placed parts of those values as mapped items of it, the parameters in Lapin_PartParameters; Lapin's
/// import brings them back as parts, measured by the fidelity gate; undo, redo and the .lapin package keep it all.
/// </summary>
public sealed class PartTypeIfcTests
{
    static Fixture F() => new(new ArchitectureModule(), new DraftingModule(), new StudioModule());
    static JsonArray J(params JsonNode?[] v) => new(v);

    // A shelf: W and D the instance's, T and the tilt A the type's, N a count, a finish, a lip when HasLip; Area derived.
    static JsonObject Shelf() => new()
    {
        ["ifcClass"] = "IfcFurnishingElement",
        ["params"] = J(
            new JsonObject { ["key"] = "W", ["default"] = 0.6, ["description"] = "The width" },
            new JsonObject { ["key"] = "D", ["default"] = 0.3 },
            new JsonObject { ["key"] = "T", ["default"] = 0.02, ["instance"] = false },
            new JsonObject { ["key"] = "A", ["kind"] = "angle", ["default"] = 90, ["instance"] = false },
            new JsonObject { ["key"] = "N", ["kind"] = "integer", ["default"] = 2 },
            new JsonObject { ["key"] = "Finish", ["kind"] = "text", ["default"] = "Oak" },
            new JsonObject { ["key"] = "HasLip", ["kind"] = "bool", ["default"] = 0 },
            new JsonObject { ["key"] = "Area", ["kind"] = "number", ["formula"] = "W * D" }),
        ["bodies"] = J(
            new JsonObject { ["name"] = "Board", ["plane"] = "xy", ["points"] = J(J(0, 0), J("W", 0), J("W", "D"), J(0, "D")), ["depth"] = "T" },
            new JsonObject { ["name"] = "Lip", ["plane"] = "xz", ["when"] = "HasLip", ["points"] = J(J(0, 0), J("W", 0), J("W", 0.05), J(0, 0.05)), ["depth"] = "T" }),
    };

    /// <summary>A part and <paramref name="n"/> placed parts in a row, every other one W 0.9 (and turned), so two sets of values.</summary>
    static (string Part, List<string> Placed) Placed(Fixture f, int n)
    {
        var part = (string)f.Run("studio.createPart", new JsonObject { ["name"] = "Shelf", ["def"] = Shelf() })!["id"]!;
        var ids = new List<string>();
        for (var i = 0; i < n; i++)
        {
            var a = new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(i * 1.5, 0), ["rotation"] = i % 4 * 30 };
            if (i % 2 == 1) a["values"] = new JsonObject { ["W"] = 0.9 };
            ids.Add((string)f.Run("studio.placePart", a)!["id"]!);
        }
        return (part, ids);
    }

    static JsonObject Def(Fixture f, string part) => Parts.DefOf(f.Model.Get(part))!;
    static double Volume(Fixture f, string id) => f.Session.Quantities(id).SelectMany(q => q.Quantities).Single(q => q.Name == "NetVolume").Value;

    static string Export(Fixture f)
    {
        using var ms = new MemoryStream();
        IfcExport.Write(f.Session, ms, "parts.ifc", new IfcExportOptions());
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    static int Count(string ifc, string what) => Regex.Matches(ifc, Regex.Escape(what)).Count;

    [Fact]
    public void Placed_parts_with_the_same_values_share_one_body()
    {
        var f = F();
        var (part, placed) = Placed(f, 50);
        var geos = placed.Select(id => f.Session.Geometry(id)!).ToList();
        // Two sets of values: two bodies built, whatever the turns.
        Assert.Equal(2, Parts.BodyBuilds(Def(f, part)));
        Assert.All(geos, g => Assert.NotNull(g.Instance));
        var keys = geos.Select(g => g.Instance!.Body).ToList();
        Assert.Equal(2, keys.Distinct().Count());
        Assert.All(keys, k => Assert.StartsWith("part:" + part + ":", k));
        Assert.Equal(keys[0], keys[2]);
        Assert.Equal(keys[1], keys[3]);
        // The same local parts for the same set: the session tessellates them once.
        Assert.Same(geos[0].Instance!.Local, geos[2].Instance!.Local);
        // Each placed one is its body moved and turned.
        Assert.Equal(0.6 * 0.3 * 0.02, Volume(f, placed[0]), 9);
        Assert.Equal(0.9 * 0.3 * 0.02, Volume(f, placed[1]), 9);
        var b = f.Bounds(placed[4]);
        Assert.Equal(6, b.Min.X, 6);
        Assert.Equal(6.6, b.Max.X, 6);
        // The interface is sent two meshes, each item an instance of one of them.
        var snap = f.Session.Snapshot();
        var items = snap.Render.Where(r => placed.Contains(r.NodeId)).ToList();
        Assert.Equal(50, items.Count);
        Assert.All(items, r => Assert.NotNull(r.Instance));
        Assert.Equal(keys.Distinct().Order(), items.Select(r => r.Instance!.Mesh).Distinct().Order());
        Assert.Equal(2, snap.Meshes!.Count(m => m.Id.StartsWith("part:", StringComparison.Ordinal)));
        // A mirrored one shares the mirrored body; one with a step of its own has its own body.
        var mirrored = (string)f.Run("studio.placePart", new JsonObject { ["typeId"] = part, ["storeyId"] = f.Storey, ["at"] = J(0, 5), ["mirror"] = true })!["id"]!;
        Assert.Equal(keys[0] + ":m", f.Session.Geometry(mirrored)!.Instance!.Body);
        Assert.Equal(0.6 * 0.3 * 0.02, Volume(f, mirrored), 9);
        Assert.Equal(-0.6, f.Bounds(mirrored).Min.X, 6);
        f.Run("studio.addFeature", new JsonObject { ["id"] = placed[4], ["feature"] = new JsonObject { ["op"] = "move", ["by"] = J(0, 0, 0.5) } });
        Assert.Null(f.Session.Geometry(placed[4])!.Instance);
        Assert.Equal(2, Parts.BodyBuilds(Def(f, part)));
    }

    [Fact]
    public void A_type_parameter_rebuilds_every_placed_part_and_an_instance_value_overrides()
    {
        var f = F();
        var (part, placed) = Placed(f, 4);
        var before = f.Session.Geometry(placed[0])!.Instance!.Body;
        // The type's thickness: every placed part follows, a new body per set.
        f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = "T", ["changes"] = new JsonObject { ["default"] = 0.04 } });
        Assert.Equal(0.6 * 0.3 * 0.04, Volume(f, placed[0]), 9);
        Assert.Equal(0.9 * 0.3 * 0.04, Volume(f, placed[1]), 9);
        Assert.NotEqual(before, f.Session.Geometry(placed[0])!.Instance!.Body);
        // A type parameter takes no instance value; an instance value is the instance's own.
        Assert.Throws<CommandException>(() => f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(placed[0]), ["values"] = new JsonObject { ["T"] = 0.1 } }));
        f.Run("studio.updatePartParam", new JsonObject { ["partId"] = part, ["key"] = "W", ["changes"] = new JsonObject { ["default"] = 0.5 } });
        Assert.Equal(0.5 * 0.3 * 0.04, Volume(f, placed[0]), 9);
        Assert.Equal(0.9 * 0.3 * 0.04, Volume(f, placed[1]), 9);
        // Given the default's value, a placed part shares the body of those without one.
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(placed[1]), ["values"] = new JsonObject { ["W"] = 0.5 } });
        Assert.Equal(f.Session.Geometry(placed[0])!.Instance!.Body, f.Session.Geometry(placed[1])!.Instance!.Body);
    }

    [Fact]
    public void Undo_redo_and_the_package_keep_the_shared_bodies()
    {
        var f = F();
        var (part, placed) = Placed(f, 4);
        var key0 = f.Session.Geometry(placed[0])!.Instance!.Body;
        var key1 = f.Session.Geometry(placed[1])!.Instance!.Body;
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(placed[0]), ["values"] = new JsonObject { ["W"] = 0.9 } });
        Assert.Equal(key1, f.Session.Geometry(placed[0])!.Instance!.Body);
        f.Session.Undo();
        Assert.Equal(key0, f.Session.Geometry(placed[0])!.Instance!.Body);
        Assert.Equal(0.6 * 0.3 * 0.02, Volume(f, placed[0]), 9);
        f.Session.Redo();
        Assert.Equal(key1, f.Session.Geometry(placed[0])!.Instance!.Body);
        f.Session.Undo();
        // Saved and opened: the same parts, values, bodies and keys.
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        var g = F();
        var content = LapinPackage.Read(new MemoryStream(ms.ToArray()));
        g.Session.Load(content.Entities, content.Relations, null, content.Classes);
        Assert.Equal(part, Parts.TypeOf(g.Model, g.Model.Get(placed[1])!)!.Id);
        foreach (var id in placed)
        {
            Assert.Equal(Volume(f, id), Volume(g, id), 9);
            Assert.Equal(f.Session.Geometry(id)!.Instance!.Body, g.Session.Geometry(id)!.Instance!.Body);
        }
    }

    [Fact]
    public void A_part_goes_to_IFC_as_its_class_type_with_its_defaults_body_as_its_map()
    {
        var f = F();
        var (part, placed) = Placed(f, 6);
        // A text or a count the bodies never read keeps the shared body.
        f.Run("studio.setPartValues", new JsonObject { ["ids"] = J(placed[0]), ["values"] = new JsonObject { ["Finish"] = "Walnut", ["N"] = 3 } });
        Assert.Equal(f.Session.Geometry(placed[2])!.Instance!.Body, f.Session.Geometry(placed[0])!.Instance!.Body);
        var ifc = Export(f);
        // The type: IfcFurnishingElementType (nine attributes: no predefined type), one representation map.
        var type = Regex.Match(ifc, @"#\d+=IFCFURNISHINGELEMENTTYPE\('([^']+)',\$,'Shelf',\$,\$,\((#[\d,#]+)\),\((#\d+)\),'[^']*',\$\);");
        Assert.True(type.Success, "the type with its map");
        Assert.Equal(part, type.Groups[1].Value);
        var map = type.Groups[3].Value;
        Assert.Equal(1, Count(ifc, "IFCREPRESENTATIONMAP("));
        // The three of the defaults are mapped items of it, the three of W 0.9 their own bodies.
        Assert.Equal(3, Regex.Matches(ifc, $@"IFCMAPPEDITEM\({map},").Count);
        Assert.Equal(3, Count(ifc, "'MappedRepresentation'"));
        Assert.Equal(1, Count(ifc, "IFCRELDEFINESBYTYPE("));
        Assert.Equal(6, Count(ifc, "IFCFURNISHINGELEMENT('"));
        // Parameters in Lapin's own set: the type's on the type, the instance's and the derived on each placed part.
        Assert.Equal(7, Count(ifc, $"'{PartIfc.SetName}'"));
        Assert.Contains("IFCPROPERTYSINGLEVALUE('T',$,IFCLENGTHMEASURE(0.02),$)", ifc);
        Assert.Contains($"IFCPROPERTYSINGLEVALUE('A',$,IFCPLANEANGLEMEASURE({Step.R(Math.PI / 2)}),$)", ifc);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('W','The width',IFCLENGTHMEASURE(0.9),$)", ifc);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('N',$,IFCINTEGER(3),$)", ifc);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Finish',$,IFCLABEL('Walnut'),$)", ifc);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('HasLip',$,IFCBOOLEAN(.F.),$)", ifc);
        Assert.Contains("IFCPROPERTYSINGLEVALUE('Area','Derived: W * D',IFCREAL(0.27),$)", ifc);
        Assert.DoesNotContain("'Pset_Lapin", ifc);
        // The definition with the part's id in the type's intent.
        Assert.Contains($"IFCPROPERTYSINGLEVALUE('LapinPartId',$,IFCLABEL('{part}'),$)", ifc);
        // Every GlobalId the same at the next export.
        static List<string> Gids(string text) => [.. Regex.Matches(text, @"^#\d+=IFC\w+\('([0-9A-Za-z_$]{22})'", RegexOptions.Multiline).Select(m => m.Groups[1].Value)];
        var first = Gids(ifc);
        Assert.Equal(first.Count, first.Distinct().Count());
        Assert.Equal(first, Gids(Export(f)));
        Assert.Contains(placed[3], first);
    }

    [Fact]
    public void Lapins_IFC_brings_parts_back_as_parts_within_the_fidelity_gate()
    {
        var f = F();
        var (part, placed) = Placed(f, 6);
        var bytes = Encoding.UTF8.GetBytes(Export(f));
        var g = F();
        var r = IfcImport.Run(g.Session, bytes, "parts.ifc", null, out _);
        Assert.Equal(part, Assert.Single(g.Model.OfClass(Parts.TypeClass)).Id);
        foreach (var id in placed)
        {
            var e = g.Model.Get(id)!;
            Assert.True(Parts.IsInstance(e));
            Assert.Equal(part, Parts.TypeOf(g.Model, e)!.Id);
            Assert.Equal(f.Model.Get(id)!.Attrs["values"]?.ToJsonString(), e.Attrs["values"]?.ToJsonString());
            Assert.Equal(Volume(f, id), Volume(g, id), 9);
            var (a, b) = (f.Bounds(id), g.Bounds(id));
            Assert.Equal(a.Min.X, b.Min.X, 6);
            Assert.Equal(a.Max.Y, b.Max.Y, 6);
            Assert.Equal(a.Max.Z, b.Max.Z, 6);
        }
        Assert.NotNull(r.Fidelity);
        Assert.Equal(6, r.Fidelity!.Measured);
        Assert.Equal(0, r.Fidelity.Failed);
    }

    [Fact]
    public void A_map_changed_in_the_file_leaves_its_placed_parts_references()
    {
        var f = F();
        var (_, placed) = Placed(f, 4);
        var ifc = Export(f);
        // The map's origin moved up 0.5 m: the defaults' placed parts no longer are what their part builds.
        var map = Regex.Match(ifc, @"(#\d+)=IFCREPRESENTATIONMAP\((#\d+),");
        var tampered = ifc.Replace(map.Value, $"{map.Groups[1].Value}=IFCREPRESENTATIONMAP(#999002,")
            .Replace("ENDSEC;\nEND-ISO", "#999001=IFCCARTESIANPOINT((0.,0.,0.5));\n#999002=IFCAXIS2PLACEMENT3D(#999001,$,$);\nENDSEC;\nEND-ISO");
        var g = F();
        var r = IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(tampered), "parts.ifc", null, out _);
        Assert.Equal(2, r.Fidelity!.Reverted);
        foreach (var id in placed)
        {
            var e = g.Model.Get(id)!;
            // The default's (0, 2) references with the file's body; the others parts.
            Assert.Equal(placed.IndexOf(id) % 2 == 1, Parts.IsInstance(e));
            if (placed.IndexOf(id) % 2 == 0) Assert.Equal(f.Bounds(id).Max.Z + 0.5, g.Bounds(id).Max.Z, 6);
        }
    }

    [Fact]
    public void Mapped_items_without_Lapins_intent_come_as_before()
    {
        var f = F();
        Placed(f, 4);
        var foreign = Export(f).Replace("'Lapin_Intent'", "'Other_Set'");
        var g = F();
        IfcImport.Run(g.Session, Encoding.UTF8.GetBytes(foreign), "foreign.ifc", null, out _);
        Assert.Empty(g.Model.OfClass(Parts.TypeClass));
        Assert.DoesNotContain(g.Model.Entities, e => Parts.IsInstance(e));
        // The two of the type's map share one body as references.
        Assert.Equal(2, g.Model.Entities.Count(e => e.Attrs[StoredBodies.BodyOf] != null));
    }
}
