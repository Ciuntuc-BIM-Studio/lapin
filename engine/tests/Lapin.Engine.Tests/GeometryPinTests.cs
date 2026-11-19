using System.Text;
using System.Text.Json.Nodes;
using Lapin.Architecture;
using Lapin.Architecture.Stairs;
using Lapin.Drawing;
using Lapin.Drawing.Views;
using Lapin.Geometry;
using Lapin.Model;
using Lapin.Platform;
using Xunit.Abstractions;

namespace Lapin.Engine.Tests;

/// <summary>
/// The second stage of the audit (D10, D11): the polygon clips, point-in-polygon tests, areas and offsets the modules had
/// each written for themselves now go through <see cref="Polygon2"/> and <see cref="Polyline2"/>. A small house's
/// quantities, volumes and drawings were taken before the change and must come out the same after (to a micrometre or a
/// square micrometre): the consolidation moves no element.
/// </summary>
public class GeometryPinTests(ITestOutputHelper output)
{
    sealed class Q(Fixture f) : IQueryContext
    {
        public IModelView Model => f.Model;
        public IServices Services => f.Session.Catalog;
        public ElementGeometry? Geometry(string entityId) => f.Session.Geometry(entityId);
    }

    static JsonArray Pts(params (double X, double Y)[] p) => new(p.Select(q => (JsonNode)new JsonArray(q.X, q.Y)).ToArray());

    static (Fixture F, List<string> Ids, List<string> Views) House()
    {
        var f = new Fixture(new ArchitectureModule());
        f.Run("storey.create", new JsonObject { ["name"] = "Level 1", ["elevation"] = 3.0, ["height"] = 3.0 });
        var type = f.Type("Brick 300", ("Plaster", 0.015, false), ("Brick", 0.25, true), ("Plaster", 0.015, false));
        var ids = new List<string>();
        // An L-shaped house, one wall askew.
        (double, double)[] l = [(0, 0), (8, 0), (8, 4), (5, 4.6), (5, 7), (0, 7)];
        for (var i = 0; i < l.Length; i++) ids.Add(f.Wall(l[i].Item1, l[i].Item2, l[(i + 1) % l.Length].Item1, l[(i + 1) % l.Length].Item2, type));
        ids.Add((string)f.Run("architecture.addDoor", new JsonObject { ["wallId"] = ids[0], ["along"] = 2.0 })!["id"]!);
        ids.Add((string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = ids[2], ["along"] = 1.2 })!["id"]!);
        ids.Add((string)f.Run("architecture.addWindow", new JsonObject { ["wallId"] = ids[5], ["along"] = 3.0 })!["id"]!);
        ids.Add((string)f.Run("architecture.addSlab", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts(l) })!["id"]!);
        var room = (string)f.Run("architecture.addRoom", new JsonObject { ["storeyId"] = f.Storey, ["x"] = 2.5, ["y"] = 2 })!["id"]!;
        ids.Add(room);
        ids.Add((string)f.Run("architecture.addSkirting", new JsonObject { ["storeyId"] = f.Storey, ["roomId"] = room })!["id"]!);
        ids.Add((string)f.Run("architecture.addCeiling", new JsonObject
        {
            ["storeyId"] = f.Storey, ["points"] = Pts((0.2, 0.2), (7.7, 0.2), (7.7, 3.8), (4.8, 4.4), (4.8, 6.8), (0.2, 6.8)), ["height"] = 2.6,
        })!["id"]!);
        ids.Add((string)f.Run("architecture.addRoof", new JsonObject { ["storeyId"] = f.Storey, ["points"] = Pts(l), ["style"] = "fr_provence" })!["id"]!);
        var stairType = (string)f.Run("architecture.createStairType", new JsonObject { ["spec"] = StairSpec.Default().ToJson() })!["id"]!;
        var stair = (string)f.Run("architecture.addStair", new JsonObject { ["storeyId"] = f.Storey, ["typeId"] = stairType, ["x"] = 1, ["y"] = 5, ["x2"] = 2, ["y2"] = 5, ["turn"] = "left" })!["id"]!;
        ids.Add(stair);
        ids.Add((string)f.Run("architecture.addUnderStair", new JsonObject { ["stairId"] = stair, ["style"] = "storage" })!["id"]!);
        // The plans as the pins were taken: without the finishes (decision 500's default then, a view's choice since decision 708).
        f.Run("viewStyle.update", new JsonObject { ["id"] = "architectural", ["layersPlan"] = "noFinishes" });
        var views = new List<string>
        {
            (string)f.Run("view.create", new JsonObject { ["kind"] = "plan", ["storeyId"] = f.Storey })!["id"]!,
            (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(2.5, -1), ["end"] = new JsonArray(2.5, 8) })!["id"]!,
            (string)f.Run("view.create", new JsonObject { ["kind"] = "section", ["start"] = new JsonArray(-1, 5.5), ["end"] = new JsonArray(9, 5.5) })!["id"]!,
            (string)f.Run("view.create", new JsonObject { ["kind"] = "elevation", ["direction"] = "south" })!["id"]!,
        };
        return (f, ids, views);
    }

    static double Area(IEnumerable<Vec2[]> loops) => loops.Where(l => l.Length >= 3).Sum(l => Math.Abs(Polygon2.SignedArea(l)));

    /// <summary>The numbers that stand for the house: per element its quantities, its parts' volumes, its plan's areas and lengths; per view what it draws.</summary>
    static SortedDictionary<string, double> Numbers(Fixture f, List<string> ids, List<string> views)
    {
        var o = new SortedDictionary<string, double>(StringComparer.Ordinal);
        for (var k = 0; k < ids.Count; k++)
        {
            var e = f.Model.Get(ids[k])!;
            var key = $"{k:00}.{e.Class}";
            foreach (var s in f.Session.Quantities(ids[k]))
                foreach (var q in s.Quantities)
                {
                    // A lintel's part is named by its new id: the ids are made afresh each run.
                    var name = $"{key}.q.{s.Name}.{System.Text.RegularExpressions.Regex.Replace(s.Part ?? "", "[0-9A-Za-z_$]{22}", "#")}.{q.Name}";
                    o[name] = o.GetValueOrDefault(name) + q.Value;
                }
            var g = f.Session.Geometry(ids[k]);
            if (g == null) continue;
            o[$"{key}.volume"] = g.Parts.Sum(p => Measure.Volume(p.Solid));
            o[$"{key}.parts"] = g.Parts.Count;
            foreach (var group in (g.Plan ?? []).GroupBy(p => p.Kind))
            {
                o[$"{key}.plan.{group.Key}.area"] = group.Where(p => p.Closed).Sum(p => Math.Abs(Polygon2.SignedArea(p.Points)));
                o[$"{key}.plan.{group.Key}.length"] = group.Sum(p => p.Closed ? Polygon2.Perimeter(p.Points) : Polyline2.Length(p.Points));
                o[$"{key}.plan.{group.Key}.items"] = group.Count();
            }
        }
        for (var k = 0; k < views.Count; k++)
        {
            var (d, _, problem) = Views.Generate(new Q(f), Views.Find(f.Model, views[k])!);
            Assert.True(d != null, problem);
            o[$"view{k}.cuts.area"] = d!.Cuts.Sum(c => Area(c.Loops));
            o[$"view{k}.cuts"] = d.Cuts.Count;
            o[$"view{k}.lines.length"] = d.Lines.Sum(x => (x.B - x.A).Length);
            foreach (var g in d.Lines.GroupBy(x => $"{x.Class}.{x.Role}")) o[$"view{k}.lines.{g.Key}.length"] = g.Sum(x => (x.B - x.A).Length);
            o[$"view{k}.fills.area"] = (d.Fills ?? []).Sum(x => Area(x.Loops));
            o[$"view{k}.outlines.length"] = (d.Outlines ?? []).Sum(x => (x.B - x.A).Length);
        }
        return o;
    }

    [Fact]
    public void The_house_measures_and_draws_as_it_did_before_the_geometry_was_shared()
    {
        var (f, ids, views) = House();
        var now = Numbers(f, ids, views);
        var sb = new StringBuilder();
        foreach (var (k, v) in now) sb.AppendLine(FormattableString.Invariant($"[\"{k}\"] = {v:R},"));
        output.WriteLine(sb.ToString());
        if (Environment.GetEnvironmentVariable("LAPIN_PIN_OUT") is { Length: > 0 } path) File.WriteAllText(path, sb.ToString());
        var before = Before;
        if (before.Count == 0) return;
        var problems = new List<string>();
        foreach (var (k, v) in before)
        {
            if (!now.TryGetValue(k, out var n)) { problems.Add($"{k}: missing"); continue; }
            var tol = k.EndsWith(".items") || k.EndsWith(".cuts") || k.EndsWith(".parts") ? 0 : 1e-6 * Math.Max(1, Math.Abs(v));
            if (Math.Abs(n - v) > tol) problems.Add(FormattableString.Invariant($"{k}: {v:R} → {n:R}"));
        }
        foreach (var k in now.Keys.Except(before.Keys)) problems.Add($"{k}: new");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // Taken on origin/main fab28778, before the consolidation (the lintels' ids, made afresh each run, as #).
    static readonly Dictionary<string, double> Before = new()
    {
        ["00.IfcWall.parts"] = 4,
        ["00.IfcWall.plan.cut.area"] = 1.9880000000000002,
        ["00.IfcWall.plan.cut.items"] = 6,
        ["00.IfcWall.plan.cut.length"] = 43.95195959492893,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetSideArea"] = 22.82805,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetVolume"] = 0.34242075000000033,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:0.Width"] = 0.015000000000000013,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetSideArea"] = 21.69720000000001,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetVolume"] = 5.424300000000002,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:1.Width"] = 0.25,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetSideArea"] = 21.23805,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetVolume"] = 0.3185707500000003,
        ["00.IfcWall.q.Lapin_WallLayerQuantities.layer:2.Width"] = 0.015000000000000013,
        ["00.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.CrossSectionArea"] = 0.0625,
        ["00.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.Length"] = 1.5,
        ["00.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.NetVolume"] = 0.09375,
        ["00.IfcWall.q.Qto_WallBaseQuantities..GrossSideArea"] = 24,
        ["00.IfcWall.q.Qto_WallBaseQuantities..GrossVolume"] = 6.720000000000001,
        ["00.IfcWall.q.Qto_WallBaseQuantities..Height"] = 3,
        ["00.IfcWall.q.Qto_WallBaseQuantities..Length"] = 8,
        ["00.IfcWall.q.Qto_WallBaseQuantities..NetSideArea"] = 22.068005357142866,
        ["00.IfcWall.q.Qto_WallBaseQuantities..NetVolume"] = 6.179041500000003,
        ["00.IfcWall.q.Qto_WallBaseQuantities..Width"] = 0.28,
        ["00.IfcWall.volume"] = 6.179041500000003,
        ["01.IfcWall.parts"] = 3,
        ["01.IfcWall.plan.cut.area"] = 1.1199999999999983,
        ["01.IfcWall.plan.cut.items"] = 3,
        ["01.IfcWall.plan.cut.length"] = 24.75804463918587,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetSideArea"] = 12.723372051331507,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetVolume"] = 0.19085058076997277,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:0.Width"] = 0.015000000000000013,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetSideArea"] = 11.999999999999963,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetVolume"] = 2.9999999999999907,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:1.Width"] = 0.25,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetSideArea"] = 11.276627948669509,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetVolume"] = 0.16914941923004279,
        ["01.IfcWall.q.Lapin_WallLayerQuantities.layer:2.Width"] = 0.015000000000000013,
        ["01.IfcWall.q.Qto_WallBaseQuantities..GrossSideArea"] = 12,
        ["01.IfcWall.q.Qto_WallBaseQuantities..GrossVolume"] = 3.3600000000000003,
        ["01.IfcWall.q.Qto_WallBaseQuantities..Height"] = 3,
        ["01.IfcWall.q.Qto_WallBaseQuantities..Length"] = 4,
        ["01.IfcWall.q.Qto_WallBaseQuantities..NetSideArea"] = 12.000000000000021,
        ["01.IfcWall.q.Qto_WallBaseQuantities..NetVolume"] = 3.360000000000006,
        ["01.IfcWall.q.Qto_WallBaseQuantities..Width"] = 0.28,
        ["01.IfcWall.volume"] = 3.360000000000006,
        ["02.IfcWall.parts"] = 4,
        ["02.IfcWall.plan.cut.area"] = 0.5206352782835832,
        ["02.IfcWall.plan.cut.items"] = 6,
        ["02.IfcWall.plan.cut.length"] = 12.440599932376836,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetSideArea"] = 7.31478512446711,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetVolume"] = 0.10972177686700675,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:0.Width"] = 0.015000000000000013,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetSideArea"] = 6.90833512446707,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetVolume"] = 1.7270837811167674,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:1.Width"] = 0.25,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetSideArea"] = 7.3147851244669235,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetVolume"] = 0.10972177686700395,
        ["02.IfcWall.q.Lapin_WallLayerQuantities.layer:2.Width"] = 0.015000000000000013,
        ["02.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.CrossSectionArea"] = 0.06196725103324494,
        ["02.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.Length"] = 1.7650452162436574,
        ["02.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.NetVolume"] = 0.10937499999999882,
        ["02.IfcWall.q.Qto_WallBaseQuantities..GrossSideArea"] = 9.178235124467012,
        ["02.IfcWall.q.Qto_WallBaseQuantities..GrossVolume"] = 2.5699058348507635,
        ["02.IfcWall.q.Qto_WallBaseQuantities..Height"] = 3,
        ["02.IfcWall.q.Qto_WallBaseQuantities..Length"] = 3.059411708155671,
        ["02.IfcWall.q.Qto_WallBaseQuantities..NetSideArea"] = 7.342508338752774,
        ["02.IfcWall.q.Qto_WallBaseQuantities..NetVolume"] = 2.055902334850777,
        ["02.IfcWall.q.Qto_WallBaseQuantities..Width"] = 0.28,
        ["02.IfcWall.volume"] = 2.055902334850777,
        ["03.IfcWall.parts"] = 3,
        ["03.IfcWall.plan.cut.area"] = 0.671999999999997,
        ["03.IfcWall.plan.cut.items"] = 3,
        ["03.IfcWall.plan.cut.length"] = 15.15804463918587,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetSideArea"] = 7.271627948669113,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetVolume"] = 0.10907441923003679,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:0.Width"] = 0.015000000000000013,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetSideArea"] = 7.199999999999993,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetVolume"] = 1.7999999999999983,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:1.Width"] = 0.25,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetSideArea"] = 7.128372051330556,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetVolume"] = 0.10692558076995844,
        ["03.IfcWall.q.Lapin_WallLayerQuantities.layer:2.Width"] = 0.015000000000000013,
        ["03.IfcWall.q.Qto_WallBaseQuantities..GrossSideArea"] = 7.200000000000001,
        ["03.IfcWall.q.Qto_WallBaseQuantities..GrossVolume"] = 2.0160000000000005,
        ["03.IfcWall.q.Qto_WallBaseQuantities..Height"] = 3,
        ["03.IfcWall.q.Qto_WallBaseQuantities..Length"] = 2.4000000000000004,
        ["03.IfcWall.q.Qto_WallBaseQuantities..NetSideArea"] = 7.199999999999975,
        ["03.IfcWall.q.Qto_WallBaseQuantities..NetVolume"] = 2.0159999999999934,
        ["03.IfcWall.q.Qto_WallBaseQuantities..Width"] = 0.28,
        ["03.IfcWall.volume"] = 2.0159999999999934,
        ["04.IfcWall.parts"] = 3,
        ["04.IfcWall.plan.cut.area"] = 1.4000000000000021,
        ["04.IfcWall.plan.cut.items"] = 3,
        ["04.IfcWall.plan.cut.length"] = 30.79195959492893,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetSideArea"] = 15.795000000000261,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetVolume"] = 0.23692500000000413,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:0.Width"] = 0.015000000000000013,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetSideArea"] = 15,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetVolume"] = 3.75,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:1.Width"] = 0.25,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetSideArea"] = 14.204999999999508,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetVolume"] = 0.2130749999999928,
        ["04.IfcWall.q.Lapin_WallLayerQuantities.layer:2.Width"] = 0.015000000000000013,
        ["04.IfcWall.q.Qto_WallBaseQuantities..GrossSideArea"] = 15,
        ["04.IfcWall.q.Qto_WallBaseQuantities..GrossVolume"] = 4.2,
        ["04.IfcWall.q.Qto_WallBaseQuantities..Height"] = 3,
        ["04.IfcWall.q.Qto_WallBaseQuantities..Length"] = 5,
        ["04.IfcWall.q.Qto_WallBaseQuantities..NetSideArea"] = 14.99999999999999,
        ["04.IfcWall.q.Qto_WallBaseQuantities..NetVolume"] = 4.1999999999999975,
        ["04.IfcWall.q.Qto_WallBaseQuantities..Width"] = 0.28,
        ["04.IfcWall.volume"] = 4.1999999999999975,
        ["05.IfcWall.parts"] = 4,
        ["05.IfcWall.plan.cut.area"] = 1.624,
        ["05.IfcWall.plan.cut.items"] = 6,
        ["05.IfcWall.plan.cut.length"] = 36.15195959492893,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetSideArea"] = 19.931549999999994,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:0.NetVolume"] = 0.2989732500000002,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:0.Width"] = 0.015000000000000013,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetSideArea"] = 18.730099999999997,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:1.NetVolume"] = 4.682524999999999,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:1.Width"] = 0.25,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetSideArea"] = 18.341550000000005,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:2.NetVolume"] = 0.2751232500000003,
        ["05.IfcWall.q.Lapin_WallLayerQuantities.layer:2.Width"] = 0.015000000000000013,
        ["05.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.CrossSectionArea"] = 0.0625,
        ["05.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.Length"] = 1.75,
        ["05.IfcWall.q.Qto_BeamBaseQuantities.lintel:#.NetVolume"] = 0.109375,
        ["05.IfcWall.q.Qto_WallBaseQuantities..GrossSideArea"] = 21,
        ["05.IfcWall.q.Qto_WallBaseQuantities..GrossVolume"] = 5.880000000000001,
        ["05.IfcWall.q.Qto_WallBaseQuantities..Height"] = 3,
        ["05.IfcWall.q.Qto_WallBaseQuantities..Length"] = 7,
        ["05.IfcWall.q.Qto_WallBaseQuantities..NetSideArea"] = 19.16427321428571,
        ["05.IfcWall.q.Qto_WallBaseQuantities..NetVolume"] = 5.3659965,
        ["05.IfcWall.q.Qto_WallBaseQuantities..Width"] = 0.28,
        ["05.IfcWall.volume"] = 5.3659965,
        ["06.IfcDoor.parts"] = 6,
        ["06.IfcDoor.plan.heavy.area"] = 0.08399999999999969,
        ["06.IfcDoor.plan.heavy.items"] = 7,
        ["06.IfcDoor.plan.heavy.length"] = 5.360000000000001,
        ["06.IfcDoor.plan.hit.area"] = 0.25200000000000017,
        ["06.IfcDoor.plan.hit.items"] = 1,
        ["06.IfcDoor.plan.hit.length"] = 2.3600000000000003,
        ["06.IfcDoor.plan.symbol.area"] = 0,
        ["06.IfcDoor.plan.symbol.items"] = 5,
        ["06.IfcDoor.plan.symbol.length"] = 4.328397388345703,
        ["06.IfcDoor.q.Lapin_Details..RevealAreaInside"] = 0.4590000000000001,
        ["06.IfcDoor.q.Lapin_Details..RevealAreaOutside"] = 0.4590000000000001,
        ["06.IfcDoor.q.Qto_DoorBaseQuantities..Area"] = 1.8900000000000001,
        ["06.IfcDoor.q.Qto_DoorBaseQuantities..Height"] = 2.1,
        ["06.IfcDoor.q.Qto_DoorBaseQuantities..Width"] = 0.9,
        ["06.IfcDoor.volume"] = 0.10557212826778112,
        ["07.IfcWindow.parts"] = 8,
        ["07.IfcWindow.plan.heavy.area"] = 0.050400000000001555,
        ["07.IfcWindow.plan.heavy.items"] = 10,
        ["07.IfcWindow.plan.heavy.length"] = 2.839999999999994,
        ["07.IfcWindow.plan.hit.area"] = 0.3360000000000003,
        ["07.IfcWindow.plan.hit.items"] = 1,
        ["07.IfcWindow.plan.hit.length"] = 2.9600000000000004,
        ["07.IfcWindow.plan.symbol.area"] = 0.31728000000000023,
        ["07.IfcWindow.plan.symbol.items"] = 9,
        ["07.IfcWindow.plan.symbol.length"] = 9.315999999999999,
        ["07.IfcWindow.q.Lapin_Details..RevealAreaInside"] = 0.44100000000000006,
        ["07.IfcWindow.q.Lapin_Details..RevealAreaOutside"] = 0.44100000000000006,
        ["07.IfcWindow.q.Qto_WindowBaseQuantities..Area"] = 1.7999999999999998,
        ["07.IfcWindow.q.Qto_WindowBaseQuantities..Height"] = 1.5,
        ["07.IfcWindow.q.Qto_WindowBaseQuantities..Width"] = 1.2,
        ["07.IfcWindow.volume"] = 0.11081844400001016,
        ["08.IfcWindow.parts"] = 8,
        ["08.IfcWindow.plan.heavy.area"] = 0.050400000000000084,
        ["08.IfcWindow.plan.heavy.items"] = 10,
        ["08.IfcWindow.plan.heavy.length"] = 2.8399999999999976,
        ["08.IfcWindow.plan.hit.area"] = 0.33599999999999997,
        ["08.IfcWindow.plan.hit.items"] = 1,
        ["08.IfcWindow.plan.hit.length"] = 2.96,
        ["08.IfcWindow.plan.symbol.area"] = 0.31727999999999995,
        ["08.IfcWindow.plan.symbol.items"] = 9,
        ["08.IfcWindow.plan.symbol.length"] = 9.316000000000003,
        ["08.IfcWindow.q.Lapin_Details..RevealAreaInside"] = 0.44100000000000006,
        ["08.IfcWindow.q.Lapin_Details..RevealAreaOutside"] = 0.44100000000000006,
        ["08.IfcWindow.q.Qto_WindowBaseQuantities..Area"] = 1.7999999999999998,
        ["08.IfcWindow.q.Qto_WindowBaseQuantities..Height"] = 1.5,
        ["08.IfcWindow.q.Qto_WindowBaseQuantities..Width"] = 1.2,
        ["08.IfcWindow.volume"] = 0.11081844400000003,
        ["09.IfcSlab.parts"] = 3,
        ["09.IfcSlab.plan.outline.area"] = 47.9,
        ["09.IfcSlab.plan.outline.items"] = 1,
        ["09.IfcSlab.plan.outline.length"] = 29.45941170815567,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:0.NetArea"] = 47.900000000000006,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:0.NetVolume"] = 0.9580000000000001,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:0.Width"] = 0.02,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:1.NetArea"] = 47.9,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:1.NetVolume"] = 2.395,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:1.Width"] = 0.05,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:2.NetArea"] = 47.9,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:2.NetVolume"] = 7.185,
        ["09.IfcSlab.q.Lapin_SlabLayerQuantities.layer:2.Width"] = 0.15,
        ["09.IfcSlab.q.Qto_SlabBaseQuantities..GrossArea"] = 47.9,
        ["09.IfcSlab.q.Qto_SlabBaseQuantities..GrossVolume"] = 10.538,
        ["09.IfcSlab.q.Qto_SlabBaseQuantities..NetArea"] = 47.9,
        ["09.IfcSlab.q.Qto_SlabBaseQuantities..NetVolume"] = 10.538,
        ["09.IfcSlab.q.Qto_SlabBaseQuantities..Perimeter"] = 29.45941170815567,
        ["09.IfcSlab.q.Qto_SlabBaseQuantities..Width"] = 0.22,
        ["09.IfcSlab.volume"] = 10.538,
        ["10.IfcSpace.parts"] = 1,
        ["10.IfcSpace.plan.room.area"] = 43.85408236085821,
        ["10.IfcSpace.plan.room.items"] = 1,
        ["10.IfcSpace.plan.room.length"] = 28.339411708155673,
        ["10.IfcSpace.plan.roomStamp.area"] = 0,
        ["10.IfcSpace.plan.roomStamp.items"] = 1,
        ["10.IfcSpace.plan.roomStamp.length"] = 0,
        ["10.IfcSpace.q.Qto_SpaceBaseQuantities..GrossFloorArea"] = 43.85408236085821,
        ["10.IfcSpace.q.Qto_SpaceBaseQuantities..GrossPerimeter"] = 28.339411708155673,
        ["10.IfcSpace.q.Qto_SpaceBaseQuantities..GrossVolume"] = 131.56224708257463,
        ["10.IfcSpace.q.Qto_SpaceBaseQuantities..Height"] = 3,
        ["10.IfcSpace.q.Qto_SpaceBaseQuantities..NetFloorArea"] = 43.85408236085821,
        ["10.IfcSpace.q.Qto_SpaceBaseQuantities..NetPerimeter"] = 28.339411708155673,
        ["10.IfcSpace.q.Qto_SpaceBaseQuantities..NetVolume"] = 131.56224708257463,
        ["10.IfcSpace.volume"] = 131.56224708257466,
        ["11.LapinSkirting.parts"] = 1,
        ["11.LapinSkirting.plan.hit.area"] = 0,
        ["11.LapinSkirting.plan.hit.items"] = 1,
        ["11.LapinSkirting.plan.hit.length"] = 27.43941170815567,
        ["11.LapinSkirting.plan.symbol.area"] = 0,
        ["11.LapinSkirting.plan.symbol.items"] = 1,
        ["11.LapinSkirting.plan.symbol.length"] = 27.343411708155678,
        ["11.LapinSkirting.q.Lapin_SkirtingQuantities..Height"] = 0.08,
        ["11.LapinSkirting.q.Lapin_SkirtingQuantities..Length"] = 27.43941170815567,
        ["11.LapinSkirting.q.Lapin_SkirtingQuantities..Pieces"] = 1,
        ["11.LapinSkirting.q.Lapin_SkirtingQuantities..Volume"] = 0.02504712625958609,
        ["11.LapinSkirting.q.Qto_CoveringBaseQuantities..GrossArea"] = 2.1951529366524536,
        ["11.LapinSkirting.q.Qto_CoveringBaseQuantities..NetArea"] = 2.1951529366524536,
        ["11.LapinSkirting.q.Qto_CoveringBaseQuantities..Width"] = 0.012,
        ["11.LapinSkirting.volume"] = 0.02504712625958609,
        ["12.LapinCeiling.parts"] = 3,
        ["12.LapinCeiling.plan.overhead.area"] = 41.67,
        ["12.LapinCeiling.plan.overhead.items"] = 1,
        ["12.LapinCeiling.plan.overhead.length"] = 27.66141857899217,
        ["12.LapinCeiling.q.Lapin_CeilingQuantities..AirDiffusers"] = 0,
        ["12.LapinCeiling.q.Lapin_CeilingQuantities..LightPanels"] = 0,
        ["12.LapinCeiling.q.Lapin_CeilingQuantities..Perimeter"] = 27.66141857899217,
        ["12.LapinCeiling.q.Lapin_CeilingQuantities..WholeTiles"] = 98,
        ["12.LapinCeiling.q.Qto_CoveringBaseQuantities..GrossArea"] = 41.67,
        ["12.LapinCeiling.q.Qto_CoveringBaseQuantities..NetArea"] = 41.67,
        ["12.LapinCeiling.q.Qto_CoveringBaseQuantities..Width"] = 0.015,
        ["12.LapinCeiling.volume"] = 0.7684217802282528,
        ["13.IfcRoof.parts"] = 78,
        ["13.IfcRoof.plan.hip.area"] = 0,
        ["13.IfcRoof.plan.hip.items"] = 7,
        ["13.IfcRoof.plan.hip.length"] = 22.005636605598614,
        ["13.IfcRoof.plan.overhead.area"] = 57.0978235124467,
        ["13.IfcRoof.plan.overhead.items"] = 1,
        ["13.IfcRoof.plan.overhead.length"] = 31.859411708155676,
        ["13.IfcRoof.plan.ridge.area"] = 0,
        ["13.IfcRoof.plan.ridge.items"] = 1,
        ["13.IfcRoof.plan.ridge.length"] = 1.9495097567963922,
        ["13.IfcRoof.plan.symbol.area"] = 0.04500000000000625,
        ["13.IfcRoof.plan.symbol.items"] = 6,
        ["13.IfcRoof.plan.symbol.length"] = 1.863497124738149,
        ["13.IfcRoof.plan.valley.area"] = 0,
        ["13.IfcRoof.plan.valley.items"] = 1,
        ["13.IfcRoof.plan.valley.length"] = 3.6206484172140496,
        ["13.IfcRoof.q.Lapin_RoofCoveringQuantities.covering.GrossArea"] = 62.5013490404597,
        ["13.IfcRoof.q.Lapin_RoofCoveringQuantities.covering.NetArea"] = 62.5013490404597,
        ["13.IfcRoof.q.Lapin_RoofCoveringQuantities.covering.Width"] = 0.05,
        ["13.IfcRoof.q.Lapin_RoofDrainageQuantities.drainage.DownpipeCount"] = 6,
        ["13.IfcRoof.q.Lapin_RoofDrainageQuantities.drainage.DownpipeLength"] = 19.2162,
        ["13.IfcRoof.q.Lapin_RoofDrainageQuantities.drainage.GutterLength"] = 31.859411708155676,
        ["13.IfcRoof.q.Lapin_RoofSnowGuardQuantities.snow.Length"] = 25.259411708155675,
        ["13.IfcRoof.q.Lapin_RoofStructureQuantities.timber.HipRafterLength"] = 22.906337980112234,
        ["13.IfcRoof.q.Lapin_RoofStructureQuantities.timber.PurlinLength"] = 19.688675862673463,
        ["13.IfcRoof.q.Lapin_RoofStructureQuantities.timber.RafterCount"] = 44,
        ["13.IfcRoof.q.Lapin_RoofStructureQuantities.timber.RafterLength"] = 78.61594265630804,
        ["13.IfcRoof.q.Lapin_RoofStructureQuantities.timber.RidgeBeamLength"] = 1.9495097567963922,
        ["13.IfcRoof.q.Lapin_RoofStructureQuantities.timber.TimberVolume"] = 2.614013042044099,
        ["13.IfcRoof.q.Lapin_RoofStructureQuantities.timber.ValleyRafterLength"] = 3.82925672235379,
        ["13.IfcRoof.q.Lapin_RoofStructureQuantities.timber.WallPlateLength"] = 29.45941170815567,
        ["13.IfcRoof.q.Qto_RoofBaseQuantities..EaveLength"] = 31.859411708155676,
        ["13.IfcRoof.q.Qto_RoofBaseQuantities..GrossArea"] = 62.5013490404597,
        ["13.IfcRoof.q.Qto_RoofBaseQuantities..HipLength"] = 22.943616442774243,
        ["13.IfcRoof.q.Qto_RoofBaseQuantities..NetArea"] = 62.5013490404597,
        ["13.IfcRoof.q.Qto_RoofBaseQuantities..ProjectedArea"] = 57.0978235124467,
        ["13.IfcRoof.q.Qto_RoofBaseQuantities..RidgeLength"] = 1.9495097567963922,
        ["13.IfcRoof.q.Qto_RoofBaseQuantities..ValleyLength"] = 3.82925672235379,
        ["13.IfcRoof.volume"] = 5.938673402993331,
        ["14.IfcStair.parts"] = 3,
        ["14.IfcStair.plan.outline.area"] = 0.0031999999999996476,
        ["14.IfcStair.plan.outline.items"] = 23,
        ["14.IfcStair.plan.outline.length"] = 33.177791164354645,
        ["14.IfcStair.q.Lapin_StairQuantities..ConcreteVolume"] = 1.2198305549424229,
        ["14.IfcStair.q.Lapin_StairQuantities..FinishArea"] = 4.845000000000001,
        ["14.IfcStair.q.Lapin_StairQuantities..FormworkArea"] = 8.612646682072352,
        ["14.IfcStair.q.Lapin_StairQuantities..GrossVolume"] = 1.2198305549424229,
        ["14.IfcStair.q.Lapin_StairQuantities..Height"] = 3,
        ["14.IfcStair.q.Lapin_StairQuantities..Length"] = 4.845000000000001,
        ["14.IfcStair.q.Lapin_StairQuantities..NetVolume"] = 1.2198305549424229,
        ["14.IfcStair.q.Lapin_StairQuantities..NumberOfRiser"] = 18,
        ["14.IfcStair.q.Lapin_StairQuantities..RailingLength"] = 5.612646682072352,
        ["14.IfcStair.q.Lapin_StairQuantities..RiserHeight"] = 0.16666666666666666,
        ["14.IfcStair.q.Lapin_StairQuantities..TreadLength"] = 0.28500000000000003,
        ["14.IfcStair.q.Lapin_StairQuantities..Width"] = 1,
        ["14.IfcStair.q.Qto_StairFlightBaseQuantities.flight:0.GrossVolume"] = 1.2198305549424229,
        ["14.IfcStair.q.Qto_StairFlightBaseQuantities.flight:0.Length"] = 4.845000000000001,
        ["14.IfcStair.q.Qto_StairFlightBaseQuantities.flight:0.NetVolume"] = 1.2198305549424229,
        ["14.IfcStair.q.Qto_StairFlightBaseQuantities.flight:0.NumberOfRiser"] = 18,
        ["14.IfcStair.q.Qto_StairFlightBaseQuantities.flight:0.NumberOfTreads"] = 17,
        ["14.IfcStair.q.Qto_StairFlightBaseQuantities.flight:0.RiserHeight"] = 0.16666666666666666,
        ["14.IfcStair.q.Qto_StairFlightBaseQuantities.flight:0.TreadLength"] = 0.28500000000000003,
        ["14.IfcStair.volume"] = 1.3816359141834864,
        ["15.LapinUnderStair.parts"] = 11,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-bookcase.Count"] = 7,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-door:1.Count"] = 1,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-door:2.Count"] = 1,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-door:3.Count"] = 1,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-door:4.Count"] = 1,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-door:5.Count"] = 1,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-door:6.Count"] = 1,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-door:7.Count"] = 1,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-handles.Count"] = 7,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-panel.Count"] = 2,
        ["15.LapinUnderStair.q.Lapin_UnderStairPart.nook-shelves.Count"] = 6,
        ["15.LapinUnderStair.q.Lapin_UnderStairQuantities..Depth"] = 1,
        ["15.LapinUnderStair.q.Lapin_UnderStairQuantities..FloorArea"] = 3.9664598815373466,
        ["15.LapinUnderStair.q.Lapin_UnderStairQuantities..GrossVolume"] = 5.790173049796074,
        ["15.LapinUnderStair.q.Lapin_UnderStairQuantities..Height"] = 2.6195671821855826,
        ["15.LapinUnderStair.q.Lapin_UnderStairQuantities..Length"] = 3.9664598815373466,
        ["15.LapinUnderStair.volume"] = 0.4172377108327134,
        ["view0.cuts"] = 11,
        ["view0.cuts.area"] = 6.594902864906589,
        ["view0.fills.area"] = 0,
        ["view0.lines.IfcDoor.cut.length"] = 2.2800000000000002,
        ["view0.lines.IfcDoor.projection.length"] = 1.8161324627819009,
        ["view0.lines.IfcRoof.overhead.length"] = 59.43520648776474,
        ["view0.lines.IfcRoof.projection.length"] = 1.863497124738149,
        ["view0.lines.IfcSlab.hidden.length"] = 28.55941170815567,
        ["view0.lines.IfcSlab.projection.length"] = 0.8999999999999999,
        ["view0.lines.IfcWall.cut.length"] = 57.94033926149114,
        ["view0.lines.IfcWall.hidden.length"] = 0.5600000000000012,
        ["view0.lines.IfcWall.overhead.length"] = 11.500000000000004,
        ["view0.lines.IfcWall.projection.length"] = 5.480000000000002,
        ["view0.lines.IfcWindow.cut.length"] = 4.039999999999991,
        ["view0.lines.IfcWindow.projection.length"] = 14.791999999999996,
        ["view0.lines.LapinCeiling.overhead.length"] = 333.3740995794866,
        ["view0.lines.LapinSkirting.hidden.length"] = 2.921999999999999,
        ["view0.lines.LapinSkirting.projection.length"] = 79.35493077165182,
        ["view0.lines.LapinUnderStair.cut.length"] = 8.032659763074694,
        ["view0.lines.LapinUnderStair.hidden.length"] = 18.038495250914515,
        ["view0.lines.LapinUnderStair.projection.length"] = 22.44672403830956,
        ["view0.lines.length"] = 653.33549644838,
        ["view0.outlines.length"] = 33.17779116435463,
        ["view1.cuts"] = 35,
        ["view1.cuts.area"] = 4.695956221724389,
        ["view1.fills.area"] = 0,
        ["view1.lines.IfcDoor.hidden.length"] = 27.991166942762355,
        ["view1.lines.IfcDoor.projection.length"] = 4.085000000000001,
        ["view1.lines.IfcRoof.cut.length"] = 28.52831014974269,
        ["view1.lines.IfcRoof.hidden.length"] = 237.52401864304215,
        ["view1.lines.IfcRoof.projection.length"] = 146.77897574697346,
        ["view1.lines.IfcSlab.cut.length"] = 42.44,
        ["view1.lines.IfcStair.cut.length"] = 4.825821617995354,
        ["view1.lines.IfcStair.hidden.length"] = 16.66265392954546,
        ["view1.lines.IfcStair.projection.length"] = 4.062887205143264,
        ["view1.lines.IfcWall.cut.length"] = 38.120000000000005,
        ["view1.lines.IfcWall.hidden.length"] = 107.50214207252613,
        ["view1.lines.IfcWall.projection.length"] = 17.4278579274739,
        ["view1.lines.IfcWindow.hidden.length"] = 22.483,
        ["view1.lines.IfcWindow.projection.length"] = 29.54699999999998,
        ["view1.lines.LapinCeiling.cut.length"] = 15.100000000000016,
        ["view1.lines.LapinCeiling.hidden.length"] = 88.76200000000004,
        ["view1.lines.LapinCeiling.projection.length"] = 39.454000000000036,
        ["view1.lines.LapinSkirting.cut.length"] = 0.35712329994861847,
        ["view1.lines.LapinSkirting.hidden.length"] = 3.1743616499743097,
        ["view1.lines.LapinSkirting.projection.length"] = 17.27252329994862,
        ["view1.lines.LapinUnderStair.cut.length"] = 3.296853662616778,
        ["view1.lines.LapinUnderStair.hidden.length"] = 0.7714260552662775,
        ["view1.lines.LapinUnderStair.projection.length"] = 0.7954260552662784,
        ["view1.lines.length"] = 896.962548258231,
        ["view1.outlines.length"] = 0,
        ["view2.cuts"] = 29,
        ["view2.cuts.area"] = 5.0505976943979585,
        ["view2.fills.area"] = 0,
        ["view2.lines.IfcRoof.cut.length"] = 17.926104665707655,
        ["view2.lines.IfcRoof.hidden.length"] = 191.77120958339412,
        ["view2.lines.IfcRoof.projection.length"] = 90.70891198654242,
        ["view2.lines.IfcSlab.cut.length"] = 30.439999999999998,
        ["view2.lines.IfcStair.cut.length"] = 24.461000320103828,
        ["view2.lines.IfcWall.cut.length"] = 37.12,
        ["view2.lines.IfcWall.hidden.length"] = 56.6421420725261,
        ["view2.lines.IfcWall.projection.length"] = 9.917857927473904,
        ["view2.lines.LapinCeiling.cut.length"] = 10.638000000000016,
        ["view2.lines.LapinCeiling.hidden.length"] = 32.572,
        ["view2.lines.LapinCeiling.projection.length"] = 16.624000000000002,
        ["view2.lines.LapinSkirting.cut.length"] = 0.35712329994861847,
        ["view2.lines.LapinSkirting.hidden.length"] = 1.4636366553365818,
        ["view2.lines.LapinSkirting.projection.length"] = 12.80468664461204,
        ["view2.lines.LapinUnderStair.cut.length"] = 38.972830431332156,
        ["view2.lines.length"] = 572.4195035869752,
        ["view2.outlines.length"] = 0,
        ["view3.cuts"] = 0,
        ["view3.cuts.area"] = 0,
        ["view3.fills.area"] = 0,
        ["view3.lines.IfcDoor.projection.length"] = 17.095154695341037,
        ["view3.lines.IfcRoof.projection.length"] = 279.4047174587151,
        ["view3.lines.IfcSlab.projection.length"] = 40.44,
        ["view3.lines.IfcStair.projection.length"] = 0.13240450728994893,
        ["view3.lines.IfcWall.projection.length"] = 38.25924214881713,
        ["view3.lines.length"] = 375.3315188101633,
        ["view3.outlines.length"] = 0,
    };
}
