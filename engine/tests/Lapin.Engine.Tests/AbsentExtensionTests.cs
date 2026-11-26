using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Cost;
using Lapin.Model;
using Lapin.Package;
using Lapin.Phases;
using Lapin.Planning;
using Lapin.Platform;
using Lapin.Protocol;

namespace Lapin.Engine.Tests;

/// <summary>
/// A project that uses an extension module the edition opening it does not have (docs/plan-extension-modules.md §4.4): the
/// package records the extension each module came in (<see cref="PackageModule.Extension"/>), the session reports the
/// absent ones (<c>project.open</c>'s <c>absentExtensions</c>) and the next save records them again; the data the extension
/// keeps in attributes of core entities (the cash flow's terms on an estimate, a task's procurement tag) comes back
/// unchanged through an edition without it, and a manifest without extensions is written as before.
/// </summary>
public sealed class AbsentExtensionTests
{
    sealed class PlusModule : ILapinModule
    {
        public string Id => "planning-plus";
        public string Name => "Planning+ (test)";
        public int SchemaVersion => 2;
        public void Register(IModuleRegistry r) { }
    }

    static Fixture F(bool plus)
    {
        ILapinModule[] modules = plus
            ? [new ArchitectureModule(), new PhasesModule(), new PlanningModule(), new CostModule(), new PlusModule()]
            : [new ArchitectureModule(), new PhasesModule(), new PlanningModule(), new CostModule()];
        var f = new Fixture(modules);
        if (plus) f.Session.Catalog.MarkExtension("planning-plus", "ro.ciuntucbimstudio.planning-plus", "1.2.0");
        return f;
    }

    static byte[] Save(Fixture f)
    {
        using var ms = new MemoryStream();
        ProjectPackage.Write(f.Session, ms);
        return ms.ToArray();
    }

    static Fixture Open(byte[] bytes, bool plus)
    {
        var content = LapinPackage.Read(new MemoryStream(bytes));
        var f = F(plus);
        f.Session.Load(content.Entities, content.Relations, null, content.Classes, content.Extensions);
        return f;
    }

    static JsonObject Terms => new() { ["retention"] = 5, ["advance"] = 10, ["paymentDays"] = 30, ["certificates"] = "monthly" };

    /// <summary>A schedule with a task the extension tagged, and an estimate holding its cash-flow terms, written as its commands write them.</summary>
    static (Fixture F, string Task, string Estimate) Project()
    {
        var f = F(plus: true);
        f.Run("planning.createSchedule", new JsonObject { ["start"] = "2027-03-01" });
        var task = (string)f.Run("planning.addTask", new JsonObject { ["name"] = "Order windows", ["duration"] = 10 })!["id"]!;
        var est = (string)f.Run("cost.createEstimate", new JsonObject { ["name"] = "Deviz" })!["id"]!;
        f.Session.Edit("Extension data", edit =>
        {
            var t = edit.Model.Get(task)!;
            var ta = t.CloneAttrs();
            ta["phase"] = "procurement";
            ta["procurementTag"] = new JsonObject { ["package"] = "windows", ["supplier"] = "Acme" };
            edit.Replace(t.With(attrs: ta));
            var e = edit.Model.Get(est)!;
            var ea = e.CloneAttrs();
            ea["cashFlow"] = Terms;
            edit.Replace(e.With(attrs: ea));
        });
        return (f, task, est);
    }

    /// <summary>The manifest as the package's JSON writes it.</summary>
    static JsonObject Manifest(byte[] bytes) =>
        (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(LapinPackage.Read(new MemoryStream(bytes)).Manifest, PackageJson.Default.PackageManifest))!;

    [Fact]
    public void The_package_records_the_extension_a_module_came_in_and_an_edition_without_it_reports_it()
    {
        var (f, _, _) = Project();
        var bytes = Save(f);
        var plus = LapinPackage.Read(new MemoryStream(bytes)).Manifest.Modules.Single(m => m.Id == "planning-plus");
        Assert.Equal(new PackageExtension("ro.ciuntucbimstudio.planning-plus", "1.2.0"), plus.Extension);
        Assert.Null(plus.Absent);
        Assert.Equal(2, plus.SchemaVersion);
        // The edition's own modules carry no extension: their records are written as before.
        var modules = Manifest(bytes)["modules"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.All(modules.Where(m => (string)m["id"]! != "planning-plus"), m => { Assert.False(m.ContainsKey("extension")); Assert.False(m.ContainsKey("absent")); });

        // Opened with it: nothing absent.
        Assert.Empty(Open(bytes, plus: true).Session.AbsentExtensions());
        // Opened without it: reported, as project.open says it.
        var g = Open(bytes, plus: false);
        var absent = Assert.Single(g.Session.AbsentExtensions());
        Assert.Equal(new FileExtension("planning-plus", 2, "ro.ciuntucbimstudio.planning-plus", "1.2.0"), absent);
        var json = ProjectPackage.AbsentExtensions(g.Session);
        Assert.Equal("""[{"module":"planning-plus","id":"ro.ciuntucbimstudio.planning-plus","version":"1.2.0"}]""", json.ToJsonString());
        // A project with only attribute-held data of the extension is not an absent module by its classes: the extension record is what says it.
        Assert.Empty(g.Session.AbsentModules());

        // Saved again without it: the record is carried over (absent), so the next opening still reports it; with it again, one record.
        var again = Save(g);
        var carried = LapinPackage.Read(new MemoryStream(again)).Manifest.Modules.Single(m => m.Id == "planning-plus");
        Assert.True(carried.Absent);
        Assert.Equal(2, carried.SchemaVersion);
        Assert.Single(Open(again, plus: false).Session.AbsentExtensions());
        var back = Open(again, plus: true);
        Assert.Empty(back.Session.AbsentExtensions());
        var resaved = LapinPackage.Read(new MemoryStream(Save(back))).Manifest.Modules.Where(m => m.Id == "planning-plus").ToList();
        Assert.Null(Assert.Single(resaved).Absent);
        // A new project forgets it.
        g.Session.New(plain: true);
        Assert.Empty(g.Session.AbsentExtensions());
    }

    [Fact]
    public void Attribute_held_data_of_the_extension_comes_back_unchanged_through_an_edition_without_it()
    {
        var (f, task, est) = Project();
        var taskAttrs = f.Model.Get(task)!.Attrs.DeepClone();
        // Opened, edited and saved by an edition without the extension: a change of its own to the same task and estimate.
        var g = Open(Save(f), plus: false);
        g.Run("planning.updateTask", new JsonObject { ["id"] = task, ["name"] = "Order the windows" });
        g.Run("cost.updateEstimate", new JsonObject { ["id"] = est, ["currency"] = "EUR" });
        var back = Open(Save(g), plus: true);
        var t = back.Model.Get(task)!;
        Assert.Equal("Order the windows", t.Name);
        Assert.Equal("procurement", t.Str("phase"));
        Assert.Equal("""{"package":"windows","supplier":"Acme"}""", t.Attrs["procurementTag"]!.ToJsonString());
        Assert.Equal(Terms.ToJsonString(), back.Model.Get(est)!.Attrs["cashFlow"]!.ToJsonString());
        Assert.Equal("EUR", back.Model.Get(est)!.Str("currency"));
        // Untouched, a round trip without it gives the same attributes.
        var h = Open(Save(Open(Save(f), plus: false)), plus: true);
        Assert.True(JsonNode.DeepEquals(taskAttrs, h.Model.Get(task)!.Attrs), h.Model.Get(task)!.Attrs.ToJsonString());
    }

    [Fact]
    public void A_module_is_marked_an_extension_only_when_it_is_loaded()
    {
        var f = F(plus: false);
        Assert.Throws<InvalidOperationException>(() => f.Session.Catalog.MarkExtension("planning-plus", "x", "1.0.0"));
        Assert.Throws<ArgumentException>(() => F(plus: true).Session.Catalog.MarkExtension("planning-plus", "x", " "));
        Assert.Empty(f.Session.Catalog.Extensions);
    }
}
