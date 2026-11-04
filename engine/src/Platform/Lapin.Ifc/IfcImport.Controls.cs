using System.Globalization;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Ifc;

public static partial class IfcImport
{
    sealed partial class Reader
    {
        /// <summary>
        /// The file's cost schedules (IFC 4: IfcCostSchedule, the IfcCostItems it controls and those nested in them, each
        /// item's IfcPhysicalQuantity and IfcCostValues by category, the elements an item controls), handed to the module
        /// that keeps controls (IIfcControls). A cost value without a category counts as material; one without a unit basis
        /// is the item's whole value, divided by its quantity.
        /// </summary>
        void Controls(IEdit edit)
        {
            var services = s.Catalog.All<IIfcControls>();
            if (services.Count == 0) return;
            var schedules = f.OfType("IFCCOSTSCHEDULE").ToList();
            var items = f.OfType("IFCCOSTITEM").ToList();
            if (schedules.Count == 0 || items.Count == 0) return;
            var byId = items.ToDictionary(i => i.Id);
            var controls = f.OfType("IFCRELASSIGNSTOCONTROL").ToList();
            var parentOf = new Dictionary<int, int>();
            var childOrder = new Dictionary<int, int>();
            foreach (var r in f.OfType("IFCRELNESTS").Where(r => byId.ContainsKey(Ref(r[4]))))
            {
                var i = 0;
                foreach (var c in Refs(r[5])) if (byId.ContainsKey(c)) { parentOf.TryAdd(c, Ref(r[4])); childOrder.TryAdd(c, i++); }
            }
            var children = parentOf.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).OrderBy(c => childOrder.GetValueOrDefault(c)).ToList());
            // What each item measures: the elements (not items, not schedules) among the objects it controls, by GlobalId.
            var measured = new Dictionary<int, List<string>>();
            foreach (var r in controls)
                if (byId.ContainsKey(Ref(r[6])))
                    foreach (var o in Refs(r[4]))
                        if (f.Get(new StepRef(o)) is { } obj && obj.Type is not ("IFCCOSTITEM" or "IFCCOSTSCHEDULE" or "IFCTASK") && Str(obj[0]) is { } g)
                            (measured.TryGetValue(Ref(r[6]), out var l) ? l : measured[Ref(r[6])] = []).Add(g);
            string? IdOf(string gid)
            {
                if (edit.Model.Get(gid) != null) return gid;
                foreach (var (stepId, modelId) in typeIds) if (f.Get(new StepRef(stepId)) is { } e && Str(e[0]) == gid) return modelId;
                return null;
            }
            static string? KindOf(string type) => type switch
            {
                "IFCQUANTITYVOLUME" => "VOLUME", "IFCQUANTITYAREA" => "AREA", "IFCQUANTITYLENGTH" => "LENGTH", "IFCQUANTITYWEIGHT" => "WEIGHT", "IFCQUANTITYCOUNT" => "COUNT", _ => null,
            };
            var result = new List<IfcCostScheduleData>();
            foreach (var cs in schedules)
            {
                var top = controls.Where(r => Ref(r[6]) == cs.Id).SelectMany(r => Refs(r[4])).Where(byId.ContainsKey)
                    .Where(i => !parentOf.ContainsKey(i) || !byId.ContainsKey(parentOf[i])).Distinct().ToList();
                if (top.Count == 0) continue;
                var list = new List<IfcCostItemData>();
                var seen = new HashSet<int>();
                string Gid(StepEntity e) => Str(e[0]) ?? Step.Guid("cost-item", e.Id.ToString(CultureInfo.InvariantCulture));
                void Walk(int id, string? parent)
                {
                    if (!seen.Add(id)) return;
                    var it = byId[id];
                    double? quantity = null;
                    string? kind = null;
                    if (Items(it[8]).FirstOrDefault() is { } q && KindOf(q.Type) is { } k) { quantity = Num(q[3]); kind = k; }
                    var values = new List<IfcCostValueData>();
                    foreach (var v in Items(it[7]))
                    {
                        var amount = Num(v[2]);
                        // A value with no unit basis is the item's whole: per unit, it is shared by the quantity.
                        if (v[3] == null && quantity is { } qq && qq > 0) amount /= qq;
                        values.Add(new IfcCostValueData(Str(v[6]) ?? "Material", amount));
                    }
                    list.Add(new IfcCostItemData(Gid(it), Str(it[2]) ?? "Item", Str(it[5]), parent, quantity, kind, values,
                        measured.GetValueOrDefault(id) ?? [], "Lapin_CostItem", PropertiesOf(id, "Lapin_CostItem")));
                    foreach (var c in children.GetValueOrDefault(id) ?? []) Walk(c, Gid(it));
                }
                foreach (var t in top) Walk(t, null);
                var currency = f.OfType("IFCMONETARYUNIT").Select(u => Str(u[0])).FirstOrDefault(x => x != null) ?? "RON";
                result.Add(new IfcCostScheduleData(Str(cs[0]) ?? Step.Guid("cost-schedule", cs.Id.ToString(CultureInfo.InvariantCulture)), Str(cs[2]) ?? "Estimate",
                    (cs[6] as StepEnum)?.Value ?? "COSTPLAN", currency, list, "Lapin_Estimate", PropertiesOf(cs.Id, "Lapin_Estimate")));
            }
            if (result.Count == 0) return;
            foreach (var service in services)
            {
                var r = service.Import(edit, result, IdOf);
                if (r.Items > 0) notes.Add($"Estimate: {r.Items} item(s) (Cost → Estimate).");
                notes.AddRange(r.Notes);
            }
        }
    }
}
