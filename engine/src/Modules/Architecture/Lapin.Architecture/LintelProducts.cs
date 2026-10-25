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
            return new JsonObject { ["added"] = products.Count, ["skipped"] = new JsonArray(skipped.Select(s => (JsonNode)s).ToArray()) };
        });

        r.Command("removeLintelProduct", "Remove lintel product", (ctx, a) =>
        {
            var project = Spatial.Project(ctx.Model) ?? throw new CommandException("The model has no project.");
            var attrs = project.CloneAttrs();
            attrs["lintelProducts"] = new JsonArray(Of(ctx.Model).Where(p => p.Code != (string?)a["code"]).Select(p => (JsonNode)JsonSerializer(p)).ToArray());
            ctx.Replace(project.With(attrs: attrs));
            return null;
        });

        r.Query("architecture.lintelProducts", (q, _) => new JsonArray(Of(q.Model).Select(p => (JsonNode)JsonSerializer(p)).ToArray()));
    }

    static JsonNode JsonSerializer(LintelProduct p) => System.Text.Json.JsonSerializer.SerializeToNode(p, ArchitectureJson.Default.LintelProduct)!;
}
