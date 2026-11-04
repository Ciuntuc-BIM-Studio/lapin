using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Cost;

/// <summary>
/// The estimate in IFC (docs/plan-4d5d.md, stage 6): each estimate an IfcCostSchedule (PredefinedType COSTPLAN, or
/// PRICEDBILLOFQUANTITIES as set), its chapters IfcCostItems with its lines nested in them, each line's quantity and
/// its unit rate as four IfcCostValues (Material, Labour, Equipment, Transport) per unit, the elements it measures
/// controlled by it, the rates in Lapin_Estimate and the line's unit in Lapin_CostItem. Read back, an estimate keeps its
/// lines as they were written (lines of its own: the file's model may not be Lapin's), so its totals are the same.
/// </summary>
public sealed class CostIfc : IIfcControls
{
    static string N(double v) => v.ToString(CultureInfo.InvariantCulture);

    public IEnumerable<IfcCostScheduleData> CostSchedules(IQueryContext query)
    {
        foreach (var e in query.Model.OfClass(Costing.Class))
        {
            var r = Costing.Compute(query, e);
            var s = r.Settings;
            var items = new List<IfcCostItemData>();
            for (var ci = 0; ci < r.Chapters.Count; ci++)
            {
                var ch = r.Chapters[ci];
                var chId = $"{e.Id}/chapter/{ci + 1}";
                items.Add(new IfcCostItemData(chId, ch.Name, $"{ci + 1}", null, null, null, [], [], "Lapin_CostItem", [("Chapter", "true")]));
                foreach (var l in ch.Lines)
                {
                    var values = new List<IfcCostValueData>();
                    if (l.M != 0) values.Add(new("Material", l.M));
                    if (l.L != 0) values.Add(new("Labour", l.L));
                    if (l.E != 0) values.Add(new("Equipment", l.E));
                    if (l.T != 0) values.Add(new("Transport", l.T));
                    List<(string, string)> props = [("Unit", l.Unit), ("Basis", l.Basis)];
                    if (l.Hours is { } h) props.Add(("Hours", N(h)));
                    if (!l.Priced) props.Add(("Priced", "false"));
                    items.Add(new IfcCostItemData($"{e.Id}/line/{l.Key.Replace('\u001f', '/')}", l.Name, l.Code, chId, l.Qty, Units.KindOf(l.Basis), values,
                        l.Elements.ToList(), "Lapin_CostItem", props));
                }
            }
            yield return new IfcCostScheduleData(e.Id, e.Name, e.PredefinedType ?? "COSTPLAN", s.Currency, items, "Lapin_Estimate",
            [
                ("Currency", s.Currency), ("EurRate", N(s.EurRate)), ("LabourTax", N(s.LabourTax)), ("Overheads", N(s.Overheads)),
                ("Profit", N(s.Profit)), ("Vat", N(s.Vat)), ("Grouping", s.Grouping), ("Equivalent", s.Equivalent ? "true" : "false"),
            ]);
        }
    }

    public IfcControlImport Import(IEdit edit, IReadOnlyList<IfcCostScheduleData> schedules, Func<string, string?> idOf)
    {
        var notes = new List<string>();
        int made = 0, items = 0;
        foreach (var cs in schedules)
        {
            if (edit.Model.Get(cs.Id) != null) { notes.Add($"The estimate „{cs.Name}” is already in the project: left as it is."); continue; }
            var props = cs.Properties.ToDictionary(p => p.Name, p => p.Value);
            double? Num(string k) => props.TryGetValue(k, out var v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
            var byId = cs.Items.ToDictionary(i => i.Id);
            var lines = new JsonArray();
            foreach (var it in cs.Items.Where(i => i.Quantity != null))
            {
                var p = it.Properties.ToDictionary(x => x.Name, x => x.Value);
                double V(string cat) => it.Values.Where(v => Category(v.Category) == cat).Sum(v => v.UnitValue);
                var o = new JsonObject
                {
                    ["id"] = edit.NewId(),
                    ["chapter"] = it.Parent != null && byId.TryGetValue(it.Parent, out var parent) ? parent.Name : "Other",
                    ["code"] = it.Identification ?? "",
                    ["name"] = it.Name,
                    ["unit"] = p.TryGetValue("Unit", out var u) && u.Length > 0 ? u : it.QuantityKind switch { "VOLUME" => "m³", "AREA" => "m²", "LENGTH" => "m", "WEIGHT" => "kg", _ => "pcs" },
                    ["qty"] = it.Quantity,
                    ["m"] = V("material"), ["l"] = V("labour"), ["e"] = V("equipment"), ["t"] = V("transport"),
                };
                if (p.TryGetValue("Hours", out var hs) && double.TryParse(hs, NumberStyles.Float, CultureInfo.InvariantCulture, out var hv)) o["hours"] = hv;
                var els = it.Elements.Select(idOf).OfType<string>().Distinct().ToList();
                if (els.Count > 0) o["elements"] = new JsonArray(els.Select(x => (JsonNode)x).ToArray());
                lines.Add((JsonNode)o);
                items++;
            }
            var attrs = new JsonObject
            {
                ["currency"] = props.TryGetValue("Currency", out var c) && c is "RON" or "EUR" ? c : cs.Currency is "EUR" ? "EUR" : "RON",
                ["derive"] = false,
                ["lines"] = lines,
            };
            foreach (var (key, attr) in new[] { ("EurRate", "eurRate"), ("LabourTax", "labourTax"), ("Overheads", "overheads"), ("Profit", "profit"), ("Vat", "vat") })
                if (Num(key) is { } v) attrs[attr] = v;
            if (props.TryGetValue("Grouping", out var g) && g == "storey") attrs["grouping"] = g;
            if (props.TryGetValue("Equivalent", out var eq) && eq == "true") attrs["equivalent"] = true;
            var id = edit.Model.GetRelation(cs.Id) == null ? cs.Id : edit.NewId();
            edit.Add(new Entity(id, Costing.Class, cs.Name, attrs, cs.Predefined is "PRICEDBILLOFQUANTITIES" ? cs.Predefined : "COSTPLAN"));
            made++;
        }
        return new IfcControlImport(made, items, notes);
    }

    static string Category(string c) => PriceLists.KindOf(c) ?? (c.ToLowerInvariant() switch { "plant" => "equipment", _ => "material" });
}
