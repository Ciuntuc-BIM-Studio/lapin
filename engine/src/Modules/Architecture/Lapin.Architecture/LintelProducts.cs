using System.Globalization;
using System.Text.Json.Nodes;
using Lapin.Model;
using Lapin.Platform;

namespace Lapin.Architecture;

/// <summary>
/// The project's precast lintel products (a manufacturer's catalogue), read
/// from a CSV: code, width, height (mm) and the lengths made (mm, separated by
/// "|", "/" or spaces, or one length a column), and the manufacturer. A
/// header row and lines starting with # are skipped; ";" or "," separate.
/// </summary>
public static class LintelProducts
{
    public static List<LintelProduct> Of(IModelView m) =>
        (Spatial.Project(m)?.Attrs["lintelProducts"] as JsonArray)?.Select(n => System.Text.Json.JsonSerializer.Deserialize(n, ArchitectureJson.Default.LintelProduct)).OfType<LintelProduct>().ToList() ?? [];

    public static (List<LintelProduct> Products, List<string> Skipped) Parse(string text)
    {
        var o = new List<LintelProduct>();
        var skipped = new List<string>();
        var lineNo = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNo++;
            var line = raw.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var sep = line.Contains(';') ? ';' : ',';
            var f = line.Split(sep).Select(x => x.Trim().Trim('"')).ToArray();
            static double? Mm(string s) => double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v / 1000 : null;
            if (f.Length < 4 || Mm(f[1]) is not { } w || Mm(f[2]) is not { } h)
            {
                if (lineNo > 1 || f.Length < 4) skipped.Add($"line {lineNo}");
                continue; // a header
            }
            // The lengths: one field split on | / and spaces, or each remaining numeric column; a last non-numeric field is the manufacturer.
            var rest = f.Skip(3).ToList();
            string? maker = rest.Count > 1 && Mm(rest[^1].Split('|', '/', ' ')[0]) == null ? rest[^1] : null;
            if (maker != null) rest.RemoveAt(rest.Count - 1);
            var lengths = rest.SelectMany(x => x.Split(['|', '/', ' '], StringSplitOptions.RemoveEmptyEntries)).Select(Mm).OfType<double>().Distinct().Order().ToArray();
            if (lengths.Length == 0) { skipped.Add($"line {lineNo}"); continue; }
            o.Add(new LintelProduct(f[0], w, h, lengths, maker));
        }
        return (o, skipped);
    }

    public static void Register(IModuleRegistry r)
    {
        // A catalogue from a CSV's text: added to the project's (a product with the same code replaced).
        r.Command("importLintelProducts", "Lintel products", (ctx, a) =>
        {
            var (products, skipped) = Parse((string?)a["text"] ?? throw new CommandException("Which CSV?"));
            if (products.Count == 0) throw new CommandException("No lintel products in it: each line is code; width; height; lengths (mm); manufacturer.");
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var all = Of(ctx.Model).Where(p => products.All(n => n.Code != p.Code)).Concat(products).ToList();
            var attrs = project.CloneAttrs();
            attrs["lintelProducts"] = new JsonArray(all.Select(p => (JsonNode)JsonSerializer(p)).ToArray());
            ctx.Replace(project.With(attrs: attrs));
            // One catalogue (report #135): each product is a beam type too, the one of its code made again with the file's section and lengths
            // (when the product has the structure module, whose class it is; an architecture-only edition keeps the catalogue alone).
            var (made, updated) = (0, 0);
            foreach (var p in ctx.Describe(IfcClass.BeamType) != null ? products : [])
            {
                if (AsBeamType(ctx, p)) made++; else updated++;
            }
            return new JsonObject { ["added"] = products.Count, ["skipped"] = new JsonArray(skipped.Select(s => (JsonNode)s).ToArray()), ["typesMade"] = made, ["typesChanged"] = updated };
        });

        r.Query("architecture.lintelProducts", (q, _) => new JsonArray(Of(q.Model).Select(p => (JsonNode)JsonSerializer(p)).ToArray()));

        // The beam types a precast lintel can be (report #135): every beam type with a section, the lintels first.
        r.Query("architecture.lintelTypes", (q, _) => new JsonArray(LintelTypes.All(q.Model).Select(b => (JsonNode)LintelTypes.ToJson(b)).ToArray()));
    }

    /// <summary>
    /// A catalogue product as a beam type (PredefinedType LINTEL), named by its code: the beam type with that code (or that
    /// name) changed in place, its elements and the rules naming it following; else a new one. True when one was made.
    /// </summary>
    public static bool AsBeamType(ICommandContext ctx, LintelProduct p)
    {
        var existing = ctx.Model.OfClass(IfcClass.BeamType).FirstOrDefault(t => string.Equals(t.Str(LintelTypes.CodeKey), p.Code, StringComparison.OrdinalIgnoreCase))
            ?? ctx.Model.OfClass(IfcClass.BeamType).FirstOrDefault(t => string.Equals(t.Name, p.Code, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            LintelTypes.Create(ctx, p.Code, p.Width, p.Height, "Reinforced concrete", p.Lengths, null, p.Code, p.Manufacturer, null, null);
            return true;
        }
        var attrs = existing.CloneAttrs();
        attrs["section"] = new JsonObject { ["kind"] = "rectangle", ["width"] = p.Width, ["depth"] = p.Height };
        LintelTypes.Write(attrs, p.Lengths, null, p.Code, p.Manufacturer, Json.Number(attrs[LintelTypes.BearingKey]), existing.Str(LintelTypes.MarkKey));
        ctx.Replace(existing.With(attrs: attrs));
        return false;
    }

    static JsonNode JsonSerializer(LintelProduct p) => System.Text.Json.JsonSerializer.SerializeToNode(p, ArchitectureJson.Default.LintelProduct)!;
}
